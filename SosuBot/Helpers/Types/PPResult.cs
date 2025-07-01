namespace SosuBot.Helpers.OsuTypes
{
    public record PPResult
    {
        public required double? Current { get; set; }
        public required double? IfSS { get; set; }
    }
}
