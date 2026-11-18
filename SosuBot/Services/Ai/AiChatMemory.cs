using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Ai;

/// <summary>One remembered chat message, already reduced to what the AI needs.</summary>
public sealed record AiChatMessage(
    int MessageId,
    long? AuthorId,
    string AuthorName,
    string? AuthorUsername,
    bool FromAssistant,
    string Text,
    DateTime SentAtUtc,
    string? ReplyToAuthorName,
    string? ReplyToText);

/// <summary>
/// The last messages of every chat the bot sees, so the AI understands what people were talking about.
/// Lives in memory only and is bounded: <see cref="MessagesPerChat" /> messages per chat, at most
/// <see cref="MaxChats" /> chats, and chats silent for <see cref="ChatIdleTimeout" /> are forgotten.
/// </summary>
public sealed partial class AiChatMemory
{
    public const int MessagesPerChat = 20;
    public const int MaxChats = 2_000;
    public const int MaxTextLength = 700;
    public static readonly TimeSpan ChatIdleTimeout = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<long, ChatLog> _chats = new();
    private readonly TimeProvider _time;
    private int _trimming;

    public AiChatMemory() : this(TimeProvider.System)
    {
    }

    public AiChatMemory(TimeProvider time) => _time = time;

    public int ChatCount => _chats.Count;

    /// <summary>Remembers a message somebody sent to a chat. Admin commands other than /c ai are skipped.</summary>
    /// <remarks>/ai and /c ai are remembered as the question alone.</remarks>
    public void Remember(Message message)
    {
        if (message.From is null)
            return;

        string? text = Describe(message);
        if (text is null)
            return;

        Add(message.Chat.Id, new AiChatMessage(
            message.MessageId,
            message.From.Id,
            DisplayName(message.From),
            message.From.Username,
            FromAssistant: false,
            Shorten(text),
            message.Date.Kind == DateTimeKind.Utc ? message.Date : message.Date.ToUniversalTime(),
            message.ReplyToMessage?.From is { } replyAuthor ? DisplayName(replyAuthor) : null,
            message.ReplyToMessage is { } reply ? Describe(reply) is { } replyText ? Shorten(replyText, 200) : null : null));
    }

    /// <summary>Remembers a message the AI sent, so it knows what it already said.</summary>
    public void RememberAssistant(long chatId, int messageId, string text, string? replyToAuthorName,
        string? replyToText)
    {
        Add(chatId, new AiChatMessage(messageId, null, "", null, FromAssistant: true, Shorten(text),
            _time.GetUtcNow().UtcDateTime, replyToAuthorName,
            replyToText is null ? null : Shorten(replyToText, 200)));
    }

    public bool IsAssistantMessage(long chatId, int messageId) =>
        _chats.TryGetValue(chatId, out ChatLog? log) && log.Snapshot().Any(m => m.FromAssistant && m.MessageId == messageId);

    /// <summary>The chat's remembered messages, oldest first.</summary>
    public IReadOnlyList<AiChatMessage> GetRecent(long chatId) =>
        _chats.TryGetValue(chatId, out ChatLog? log) ? log.Snapshot() : [];

    /// <summary>
    /// Text that stands for a message: its text or caption, or a short note about the media. <c>null</c> for
    /// messages the AI should not see.
    /// </summary>
    public static string? Describe(Message message)
    {
        string? text = message.Text ?? message.Caption;
        if (text is not null)
        {
            // Of /ai and /c ai only what was asked of the AI is kept.
            Match ai = AiCommandPattern().Match(text);
            if (ai.Success)
                return ai.Groups["prompt"].Value.Trim() is { Length: > 0 } prompt ? prompt : "(позвали тебя)";

            // Other admin commands stay private.
            bool adminCommand = text.StartsWith("/c", StringComparison.OrdinalIgnoreCase) &&
                                (text.Length == 2 || char.IsWhiteSpace(text[2]) || text[2] == '@');
            if (adminCommand)
                return null;

            // A command is something done with the bot, not a topic of conversation.
            return text.StartsWith('/') ? $"[команда боту: {text}]" : text;
        }

        return message.Type switch
        {
            MessageType.Sticker => $"[стикер {message.Sticker?.Emoji}]".Replace(" ]", "]"),
            MessageType.Photo => "[фото]",
            MessageType.Video => "[видео]",
            MessageType.Animation => "[гифка]",
            MessageType.Voice => "[голосовое]",
            MessageType.VideoNote => "[кружок]",
            MessageType.Audio => "[аудио]",
            MessageType.Document => $"[файл {message.Document?.FileName}]".Replace(" ]", "]"),
            MessageType.Poll => $"[опрос: {message.Poll?.Question}]",
            _ => null
        };
    }

    /// <summary>"/ai text", "/c ai text" and their "@bot" forms: the text after "ai", line breaks included.</summary>
    public static string? ExtractAiCommandPrompt(string text)
    {
        Match match = AiCommandPattern().Match(text);
        return match.Success ? match.Groups["prompt"].Value.Trim() : null;
    }

    public static string DisplayName(User user)
    {
        string name = string.IsNullOrWhiteSpace(user.LastName) ? user.FirstName : $"{user.FirstName} {user.LastName}";
        return string.IsNullOrWhiteSpace(name) ? user.Username ?? "кто-то" : name.Trim();
    }

    private void Add(long chatId, AiChatMessage message)
    {
        ChatLog log = _chats.GetOrAdd(chatId, _ => new ChatLog());
        log.Add(message, _time.GetUtcNow());

        if (_chats.Count > MaxChats)
            ForgetQuietChats();
    }

    private void ForgetQuietChats()
    {
        if (Interlocked.Exchange(ref _trimming, 1) == 1)
            return;

        try
        {
            DateTimeOffset idleSince = _time.GetUtcNow() - ChatIdleTimeout;
            foreach ((long chatId, ChatLog log) in _chats)
            {
                if (log.LastActivity < idleSince)
                    _chats.TryRemove(chatId, out _);
            }

            int excess = _chats.Count - MaxChats / 10 * 9;
            if (excess <= 0)
                return;

            foreach (long chatId in _chats.OrderBy(pair => pair.Value.LastActivity).Take(excess).Select(pair => pair.Key)
                         .ToList())
                _chats.TryRemove(chatId, out _);
        }
        finally
        {
            Volatile.Write(ref _trimming, 0);
        }
    }

    private static string Shorten(string text, int maxLength = MaxTextLength)
    {
        text = text.Trim();
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    [GeneratedRegex(@"^/(?:c(?:@\w+)?\s+ai|ai(?:@\w+)?)(?:\s+(?<prompt>[\s\S]*))?$", RegexOptions.IgnoreCase)]
    private static partial Regex AiCommandPattern();

    private sealed class ChatLog
    {
        private readonly Queue<AiChatMessage> _messages = new();
        private long _lastActivityTicks;

        public DateTimeOffset LastActivity => new(Interlocked.Read(ref _lastActivityTicks), TimeSpan.Zero);

        public void Add(AiChatMessage message, DateTimeOffset now)
        {
            lock (_messages)
            {
                _messages.Enqueue(message);
                while (_messages.Count > MessagesPerChat)
                    _messages.Dequeue();
            }

            Interlocked.Exchange(ref _lastActivityTicks, now.UtcTicks);
        }

        public IReadOnlyList<AiChatMessage> Snapshot()
        {
            lock (_messages)
                return _messages.ToArray();
        }
    }
}
