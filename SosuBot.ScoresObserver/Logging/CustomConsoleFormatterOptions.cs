using Microsoft.Extensions.Logging.Console;

namespace SosuBot.ScoresObserver.Logging;

internal class CustomConsoleFormatterOptions : ConsoleFormatterOptions
{
    public string? CustomPrefix { get; set; }
}