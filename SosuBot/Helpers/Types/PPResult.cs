using SosuBot.PerformanceCalculator;

namespace SosuBot.Helpers.Types;

public sealed record PPResult
{
    public required PPCalculationResult? Current { get; set; }
    public required PPCalculationResult? IfFC { get; set; }
    public required PPCalculationResult? IfSS { get; set; }
}