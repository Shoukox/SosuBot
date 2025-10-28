using SosuBot.Helpers;

namespace SosuBot.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    ///     Converts a datetime object from local/utc time to the time in the specified country
    /// </summary>
    /// <param name="dateTime">A <see cref="DateTime" />instance</param>
    /// <param name="country">A country</param>
    /// <returns>
    ///     <see cref="DateTime" />
    /// </returns>
    /// <exception cref="NotImplementedException">Occurs if the specified country is unknown</exception>
    public static DateTime ChangeTimezone(this DateTime dateTime, Country country)
    {
        switch (country)
        {
            case Country.Uzbekistan:
            {
                return TimeZoneInfo.ConvertTime(dateTime,
                    TimeZoneInfo.FindSystemTimeZoneById("West Asia Standard Time"));
            }
            default:
            {
                throw new NotImplementedException();
            }
        }
    }
}