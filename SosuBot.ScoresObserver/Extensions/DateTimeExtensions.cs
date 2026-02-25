using SosuBot.ScoresObserver.Models;

namespace SosuBot.ScoresObserver.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    ///     Converts a datetime object from local/utc time to the time in the specified country
    /// </summary>
    /// <param name="dateTime">A <see cref="DateTime" />instance</param>
    /// <param name="country">A country</param>
    /// <returns>
    ///     <see cref="DateTimeOffset" /> with the target timezone offset
    /// </returns>
    /// <exception cref="NotImplementedException">Occurs if the specified country is unknown</exception>
    public static DateTimeOffset ChangeTimezone(this DateTime dateTime, Country country)
    {
        return country switch
        {
            Country.Uzbekistan => ConvertToTimeZoneOffset(dateTime, "West Asia Standard Time"),
            _ => throw new NotImplementedException()
        };
    }

    public static DateTimeOffset ChangeTimezone(this DateTimeOffset dateTime, Country country)
    {
        return country switch
        {
            Country.Uzbekistan => ConvertToTimeZoneOffset(dateTime.UtcDateTime, "West Asia Standard Time"),
            _ => throw new NotImplementedException()
        };
    }

    private static DateTimeOffset ConvertToTimeZoneOffset(DateTime dateTime, string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var utc = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        var offset = tz.GetUtcOffset(localTime);
        return new DateTimeOffset(localTime, offset);
    }
}
