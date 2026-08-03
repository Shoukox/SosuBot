namespace SosuBot.Graphics.Models;

public sealed record ScorePreviewText(IReadOnlyList<ScorePreviewTextRun> Runs)
{
    public string PlainText => string.Concat(Runs.Select(run => run.Text));
}

public sealed record ScorePreviewTextRun(string Text, string ColorHex, float GlowLevel);

public enum ScorePreviewTextParseError
{
    None,
    Empty,
    TooLong,
    TooManyRuns,
    InvalidMarkup,
    InvalidColor,
    InvalidGlow
}
