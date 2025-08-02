namespace SosuBot.Extensions;

public static class IntegerExtensions
{
    public static string ReplaceIfNull(this int? num, string replace = "-")
    {
        return num is null ? replace : num.ToString()!;
    }
}