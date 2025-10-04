using SosuBot.Helpers;

namespace SosuBot.Extensions;

public static class DateTimeExtensions
{
    public static DateTime ChangeTimezone(this DateTime dateTime, Country country)
    {
        switch (country)
        {
            case Country.Uzbekistan:
            {
                return TimeZoneInfo.ConvertTime(dateTime, TimeZoneInfo.FindSystemTimeZoneById("West Asia Standard Time"));
            }
            default:
            {
                throw new NotImplementedException();
            }
        }
    }
}