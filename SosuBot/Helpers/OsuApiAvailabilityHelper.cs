using SosuBot.Services;

namespace SosuBot.Helpers;

public static class OsuApiAvailabilityHelper
{
    public static bool IsUnavailable(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OsuApiUnavailableException => true,
            AggregateException aggregate => aggregate.InnerExceptions.Any(IsUnavailable),
            _ => exception.InnerException is not null && IsUnavailable(exception.InnerException),
        };
    }
}
