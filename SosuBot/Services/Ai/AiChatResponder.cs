using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SosuBot.Configuration;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Ai;

/// <summary>
/// Answers a chat message as the AI persona: gathers the chat context, shows progress the way Telegram allows
/// (a streamed draft with a stop button in private chats, "typing…" in groups) and sends the reply as one or a few
/// short messages, like a person would.
/// </summary>
public sealed class AiChatResponder(
    OpenAiService ai,
    AiChatMemory memory,
    IOptions<BotConfiguration> botConfiguration,
    ILogger<AiChatResponder> logger)
{
    private const int MaxReplyMessages = 4;
    private static readonly TimeSpan DraftInterval = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan TypingInterval = TimeSpan.FromSeconds(4);

    private readonly ConcurrentDictionary<long, byte> _busyChats = new();
    private readonly ConcurrentDictionary<(long ChatId, long DraftId), CancellationTokenSource> _drafts = new();

    // Becomes false the first time the Bot API server rejects sendMessageDraft (an outdated self-hosted server).
    private volatile bool _draftsSupported = true;

    /// <summary>Handles the stop button under a streamed draft.</summary>
    public void Stop(long chatId, long draftId)
    {
        if (_drafts.TryGetValue((chatId, draftId), out CancellationTokenSource? generation))
            generation.Cancel();
    }

    /// <param name="prompt">What the speaker said to the AI (the message text without the command).</param>
    public async Task RespondAsync(ITelegramBotClient bot, Message message, string prompt, BotContext database,
        CancellationToken cancellationToken)
    {
        long chatId = message.Chat.Id;
        if (!_busyChats.TryAdd(chatId, 0))
        {
            await SendAsync(bot, message, "секу, я ещё прошлое дописываю", reply: true, cancellationToken);
            return;
        }

        bool privateChat = message.Chat.Type == ChatType.Private;
        int? threadId = message.IsTopicMessage ? message.MessageThreadId : null;
        long draftId = Random.Shared.NextInt64(1, long.MaxValue);
        using var generation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var typing = new CancellationTokenSource();
        Task? typingLoop = null;
        try
        {
            AiReplyRequest request = await BuildRequestAsync(message, prompt, database, cancellationToken);
            var toolContext = new AiToolContext(emoji => ReactAsync(bot, message, emoji, cancellationToken));

            Func<string, Task>? onText = null;
            if (privateChat && _draftsSupported)
            {
                _drafts[(chatId, draftId)] = generation;
                onText = CreateDraftStreamer(bot, chatId, draftId, threadId, generation.Token);
                // An empty draft shows Telegram's "Thinking…" placeholder until the first words arrive.
                if (!await TrySendDraftAsync(bot, chatId, draftId, "", threadId, generation.Token))
                    onText = null;
            }

            if (onText is null)
                typingLoop = KeepTypingAsync(bot, chatId, threadId, typing.Token);

            AiReply reply = await ai.GenerateAsync(request, toolContext, onText, generation.Token);
            await typing.CancelAsync();
            if (typingLoop is not null)
                await typingLoop;

            if (reply.Stopped && cancellationToken.IsCancellationRequested)
                return;

            // Nothing written means she only reacted, or was stopped before the first word.
            IReadOnlyList<string> parts = SplitIntoMessages(reply.Text);
            for (var i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                    await PauseLikeTypingAsync(bot, chatId, threadId, parts[i], cancellationToken);

                // In groups the first message replies to the speaker so it is clear whom she answers.
                Message sent = await SendAsync(bot, message, parts[i], reply: i == 0 && !privateChat,
                    cancellationToken);
                memory.RememberAssistant(chatId, sent.MessageId, parts[i],
                    i == 0 ? AiChatMemory.DisplayName(message.From!) : null,
                    i == 0 ? prompt : null);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "AI reply failed in chat {ChatId}", chatId);
            await SendAsync(bot, message, "ой, у меня что-то зависло( напиши чуть позже", reply: true,
                CancellationToken.None);
        }
        finally
        {
            await typing.CancelAsync();
            _drafts.TryRemove((chatId, draftId), out _);
            _busyChats.TryRemove(chatId, out _);
        }
    }

    /// <summary>
    /// Splits the model's text into the messages to send: at the separator the model was told to use, then at
    /// Telegram's length limit. Never more than <see cref="MaxReplyMessages" /> messages.
    /// </summary>
    public static IReadOnlyList<string> SplitIntoMessages(string text)
    {
        List<string> parts = text
            .Split(OpenAiService.MessageSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Humanize)
            .Where(part => part.Length > 0)
            .ToList();
        if (parts.Count > MaxReplyMessages)
            parts = [.. parts.Take(MaxReplyMessages - 1), string.Join("\n", parts.Skip(MaxReplyMessages - 1))];

        var messages = new List<string>();
        foreach (string part in parts)
        {
            for (var start = 0; start < part.Length; start += TelegramConstants.MaximumMessageLength)
                messages.Add(part.Substring(start, Math.Min(TelegramConstants.MaximumMessageLength, part.Length - start)));
        }

        return messages;
    }

    /// <summary>
    /// Removes what gives a model away in a chat even when the prompt forbids it: em dashes, markdown emphasis and
    /// markdown line-break spaces.
    /// </summary>
    public static string Humanize(string text)
    {
        string cleaned = text.Replace(" — ", " - ").Replace("—", "-").Replace("**", "").Replace("__", "");
        return string.Join('\n', cleaned.Split('\n').Select(line => line.TrimEnd())).Trim();
    }

    private async Task<AiReplyRequest> BuildRequestAsync(Message message, string prompt, BotContext database,
        CancellationToken cancellationToken)
    {
        User speaker = message.From!;
        IReadOnlyList<AiChatMessage> history = memory.GetRecent(message.Chat.Id)
            .Where(m => m.MessageId != message.MessageId)
            .TakeLast(AiChatMemory.MessagesPerChat - 1)
            .ToList();

        HashSet<long> people = history.Where(m => m.AuthorId is not null).Select(m => m.AuthorId!.Value).ToHashSet();
        people.Add(speaker.Id);
        if (message.ReplyToMessage?.From is { } replyAuthor)
            people.Add(replyAuthor.Id);
        Dictionary<long, (string Username, Playmode Mode)> osuAccounts = await database.OsuUsers.AsNoTracking()
            .Where(user => people.Contains(user.TelegramId) && user.OsuUsername != "")
            .ToDictionaryAsync(user => user.TelegramId, user => (user.OsuUsername, user.OsuMode), cancellationToken);

        DateTime now = DateTime.UtcNow;
        var context = new StringBuilder();
        context.AppendLine(CultureInfo.InvariantCulture,
            $"Сейчас {now:dd.MM.yyyy HH:mm} UTC ({now.AddHours(5):HH:mm} по Ташкенту).");
        context.AppendLine(message.Chat.Type == ChatType.Private
            ? $"Это личка с {AiChatMemory.DisplayName(speaker)}."
            : $"Это групповой чат «{message.Chat.Title}». Ниже последние сообщения чата, они просто для контекста.");
        context.Append($"Тебе пишет {Describe(speaker, osuAccounts)}");
        context.AppendLine(osuAccounts.ContainsKey(speaker.Id)
            ? "."
            : ". Свой osu! аккаунт в боте не привязал (это делается командой /set ник).");
        if (message.ReplyToMessage is { } replied)
        {
            string author = replied.From?.Id == botConfiguration.Value.Id
                ? "бота (это вывод какой-то команды бота, если это не твоё сообщение)"
                : replied.From is { } from ? Describe(from, osuAccounts) : "кого-то";
            if (memory.IsAssistantMessage(message.Chat.Id, replied.MessageId))
                author = "тебя";
            string? quote = message.Quote?.Text;
            context.AppendLine(CultureInfo.InvariantCulture,
                $"Он(а) отвечает на сообщение {author}: «{quote ?? AiChatMemory.Describe(replied)}»{(quote is null ? "" : " (процитировал(а) кусок)")}.");
        }

        List<AiTurn> turns = history
            .Select(m => new AiTurn(m.FromAssistant, m.FromAssistant ? m.Text : FormatChatMessage(m, osuAccounts, now)))
            .ToList();
        string current = $"{Describe(speaker, osuAccounts)}: {(string.IsNullOrWhiteSpace(prompt) ? "(позвали тебя без текста)" : prompt)}";

        return new AiReplyRequest(context.ToString().Trim(), turns, current, speaker.Id);
    }

    private static string FormatChatMessage(AiChatMessage message,
        IReadOnlyDictionary<long, (string Username, Playmode Mode)> osuAccounts, DateTime now)
    {
        var text = new StringBuilder(message.AuthorName);
        if (message.AuthorUsername is { } username)
            text.Append(" (@").Append(username).Append(')');
        if (message.AuthorId is { } id && osuAccounts.TryGetValue(id, out var osu))
            text.Append(" [osu: ").Append(osu.Username).Append(']');
        int minutes = (int)(now - message.SentAtUtc).TotalMinutes;
        if (minutes >= 2)
            text.Append(" · ").Append(minutes < 120 ? $"{minutes} мин назад" : $"{minutes / 60} ч назад");
        text.Append(": ").Append(message.Text);
        if (message.ReplyToAuthorName is { } replyAuthor)
            text.Append(" (в ответ ").Append(replyAuthor).Append(message.ReplyToText is { } quoted ? $": «{quoted}»" : "").Append(')');
        return text.ToString();
    }

    private static string Describe(User user, IReadOnlyDictionary<long, (string Username, Playmode Mode)> osuAccounts)
    {
        var text = new StringBuilder(AiChatMemory.DisplayName(user));
        if (user.Username is { } username)
            text.Append(" (@").Append(username).Append(')');
        if (osuAccounts.TryGetValue(user.Id, out var osu))
            text.Append(", ник в osu!: ").Append(osu.Username).Append(", основной режим ").Append(osu.Mode.ToGamemode());
        return text.ToString();
    }

    private Func<string, Task> CreateDraftStreamer(ITelegramBotClient bot, long chatId, long draftId, int? threadId,
        CancellationToken cancellationToken)
    {
        DateTime lastSent = DateTime.MinValue;
        return async text =>
        {
            if (!_draftsSupported || DateTime.UtcNow - lastSent < DraftInterval)
                return;

            lastSent = DateTime.UtcNow;
            // The draft shows what is written so far; separate messages appear as paragraphs until sent.
            string draft = text.Replace(OpenAiService.MessageSeparator, "").Trim();
            if (draft.Length > TelegramConstants.MaximumMessageLength)
                draft = draft[..TelegramConstants.MaximumMessageLength];
            await TrySendDraftAsync(bot, chatId, draftId, draft, threadId, cancellationToken);
        };
    }

    private async Task<bool> TrySendDraftAsync(ITelegramBotClient bot, long chatId, long draftId, string text,
        int? threadId, CancellationToken cancellationToken)
    {
        try
        {
            await bot.SendMessageDraft(chatId, draftId, text, messageThreadId: threadId, canStop: true,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (ApiRequestException exception) when (exception.ErrorCode is 404 or 400 &&
                                                    !exception.Message.Contains("too many", StringComparison.OrdinalIgnoreCase))
        {
            // The self-hosted Bot API server predates drafts; fall back to "typing…" from now on.
            logger.LogWarning("sendMessageDraft is unavailable ({Error}); using the typing status instead",
                exception.Message);
            _draftsSupported = false;
            return false;
        }
        catch (ApiRequestException exception)
        {
            logger.LogDebug("Skipped an AI draft update: {Error}", exception.Message);
            return true;
        }
    }

    private static async Task KeepTypingAsync(ITelegramBotClient bot, long chatId, int? threadId,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await bot.SendChatAction(chatId, ChatAction.Typing, threadId, cancellationToken: cancellationToken);
                await Task.Delay(TypingInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ApiRequestException)
        {
            // The status is a nicety.
        }
    }

    private static async Task PauseLikeTypingAsync(ITelegramBotClient bot, long chatId, int? threadId, string text,
        CancellationToken cancellationToken)
    {
        try
        {
            await bot.SendChatAction(chatId, ChatAction.Typing, threadId, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException)
        {
        }

        await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(500 + text.Length * 25, 700, 2_500)), cancellationToken);
    }

    private static async Task<Message> SendAsync(ITelegramBotClient bot, Message message, string text, bool reply,
        CancellationToken cancellationToken) =>
        await bot.SendMessage(
            message.Chat.Id,
            text,
            ParseMode.None,
            replyParameters: reply ? new ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true } : null,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            messageThreadId: !reply && message.IsTopicMessage ? message.MessageThreadId : null,
            cancellationToken: cancellationToken);

    private async Task<bool> ReactAsync(ITelegramBotClient bot, Message message, string emoji,
        CancellationToken cancellationToken)
    {
        if (!AiToolbox.Reactions.Contains(emoji))
            return false;

        try
        {
            await bot.SetMessageReaction(message.Chat.Id, message.MessageId, [new ReactionTypeEmoji { Emoji = emoji }],
                cancellationToken: cancellationToken);
            return true;
        }
        catch (ApiRequestException exception)
        {
            logger.LogDebug("AI reaction {Emoji} was rejected: {Error}", emoji, exception.Message);
            return false;
        }
    }
}
