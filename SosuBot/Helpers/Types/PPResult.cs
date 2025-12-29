using SosuBot.PerformanceCalculator;


// ReSharper disable InconsistentNaming

namespace SosuBot.Helpers.Types;

public sealed record PPResult
{
    public required PPCalculationResult? Current { get; set; }
    public required PPCalculationResult? IfFC { get; set; }
}