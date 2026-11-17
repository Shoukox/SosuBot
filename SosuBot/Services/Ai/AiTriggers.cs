using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Ai;

/// <summary>
/// Decides whether an ordinary (non-command) message is addressed to the AI. Access is checked separately.
/// </summary>
public static partial class AiTriggers
{
    /// <summary>The persona's name; a group message starting with it calls her.</summary>
    public const string PersonaName = "Соня";

    /// <returns>The text to answer, or <c>null</c> when the message is not for the AI.</returns>
    /// <param name="hasOsuLink">
    /// The message has an osu! link the bot answers with a card; in private chats such messages stay with it.
    /// </param>
    public static string? Detect(Message message, string botUsername, long botId, bool hasOsuLink)
    {
        string? text = message.Text ?? message.Caption;
        if (string.IsNullOrWhiteSpace(text) || message.ForwardOrigin is not null || message.From?.IsBot == true)
            return null;

        if (message.Chat.Type == ChatType.Private)
            return hasOsuLink ? null : text.Trim();

        string mention = "@" + botUsername;
        if (text.Contains(mention, StringComparison.OrdinalIgnoreCase))
            return Regex.Replace(text, Regex.Escape(mention), "", RegexOptions.IgnoreCase).Trim();

        if (message.ReplyToMessage?.From?.Id == botId)
            return text.Trim();

        return NamePattern().IsMatch(text) ? text.Trim() : null;
    }

    [GeneratedRegex(@"^\s*(соня|сонь|сонечка|сонька|sonya|sonia)(?=$|[\s,.!?:;)])", RegexOptions.IgnoreCase)]
    private static partial Regex NamePattern();
}
