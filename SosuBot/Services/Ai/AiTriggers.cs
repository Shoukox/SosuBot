using System.Text.RegularExpressions;
using Telegram.Bot.Types;

namespace SosuBot.Services.Ai;

/// <summary>
/// Decides whether an ordinary (non-command) message is addressed to the AI: only a message that mentions the bot
/// by its @username is. The commands /ai and /c ai reach her on their own. Access is checked separately.
/// </summary>
public static class AiTriggers
{
    /// <returns>The message text without the mention, or <c>null</c> when the message is not for the AI.</returns>
    public static string? Detect(Message message, string botUsername)
    {
        string? text = message.Text ?? message.Caption;
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(botUsername) ||
            message.ForwardOrigin is not null || message.From?.IsBot == true)
            return null;

        // "@sosubot" but not "@sosubot_fan": the username must end there.
        var mention = new Regex($@"@{Regex.Escape(botUsername)}(?![A-Za-z0-9_])", RegexOptions.IgnoreCase);
        return mention.IsMatch(text) ? mention.Replace(text, "").Trim() : null;
    }
}
