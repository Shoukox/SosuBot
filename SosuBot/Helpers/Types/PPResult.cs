namespace SosuBot.Helpers.Types
{
    public sealed record PPResult
    {
        public required double? Current { get; set; }
        public required double? IfFC { get; set; }
        public required double? IfSS { get; set; }
    }
}
