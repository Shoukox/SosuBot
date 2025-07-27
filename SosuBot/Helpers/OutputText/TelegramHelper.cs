using Telegram.Bot.Types;

namespace SosuBot.Helpers.OutputText;

public static class TelegramHelper
{
    public static string GetUserUrl(long userId)
    {
        return $"{TelegramConstants.BaseUserUrlWithUserId}{userId}";
    }
    
    public static string GetUserUrlWrappedInString(long userId, string text)
    {
        return $"<a href=\"{GetUserUrl(userId)}\">{text}</a>";
    }

    public static string GetUserFullName(User telegramUser)
    {
        return  $"{telegramUser.FirstName} {telegramUser.LastName}";
    }
}