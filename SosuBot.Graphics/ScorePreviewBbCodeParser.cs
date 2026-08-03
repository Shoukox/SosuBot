using SosuBot.Graphics.Models;
using System.Globalization;
using System.Text;

namespace SosuBot.Graphics;

public static class ScorePreviewBbCodeParser
{
    public const int MaximumTextLength = 120;
    public const int MaximumRuns = 32;
    public const float MaximumGlowLevel = 40;
    public const float DefaultGlowLevel = 10;
    public const string DefaultColor = "#FFFFFF";

    private static readonly IReadOnlyDictionary<string, string> NamedColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["white"] = "#FFFFFF",
            ["black"] = "#000000",
            ["red"] = "#FF0000",
            ["green"] = "#00FF00",
            ["blue"] = "#0000FF",
            ["yellow"] = "#FFFF00",
            ["orange"] = "#FFA500",
            ["pink"] = "#FF69B4",
            ["purple"] = "#800080",
            ["cyan"] = "#00FFFF"
        };

    public static bool TryParse(string? input, out ScorePreviewText? text,
        out ScorePreviewTextParseError error)
    {
        text = null;
        error = ScorePreviewTextParseError.None;

        string source = input?.Trim() ?? string.Empty;
        if (source.Length == 0)
        {
            error = ScorePreviewTextParseError.Empty;
            return false;
        }

        List<ScorePreviewTextRun> runs = [];
        StringBuilder buffer = new();
        Stack<StyleFrame> frames = new();
        TextStyle currentStyle = new(DefaultColor, DefaultGlowLevel);
        int visibleLength = 0;

        for (int index = 0; index < source.Length;)
        {
            if (source[index] != '[')
            {
                buffer.Append(source[index]);
                visibleLength++;
                index++;
                continue;
            }

            int closingBracket = source.IndexOf(']', index + 1);
            if (closingBracket < 0)
            {
                error = ScorePreviewTextParseError.InvalidMarkup;
                return false;
            }

            string tag = source[(index + 1)..closingBracket].Trim();
            FlushRun(runs, buffer, currentStyle);

            if (tag.StartsWith('/'))
            {
                string closingName = tag[1..].Trim();
                if (frames.Count == 0 ||
                    !string.Equals(frames.Peek().Tag, closingName, StringComparison.OrdinalIgnoreCase))
                {
                    error = ScorePreviewTextParseError.InvalidMarkup;
                    return false;
                }

                currentStyle = frames.Pop().PreviousStyle;
            }
            else
            {
                int separator = tag.IndexOf('=');
                if (separator <= 0 || separator == tag.Length - 1)
                {
                    error = ScorePreviewTextParseError.InvalidMarkup;
                    return false;
                }

                string name = tag[..separator].Trim();
                string value = tag[(separator + 1)..].Trim();
                TextStyle previousStyle = currentStyle;

                if (name.Equals("color", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryNormalizeColor(value, out string color))
                    {
                        error = ScorePreviewTextParseError.InvalidColor;
                        return false;
                    }

                    currentStyle = currentStyle with { ColorHex = color };
                }
                else if (name.Equals("glow", StringComparison.OrdinalIgnoreCase))
                {
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                            out float glowLevel) ||
                        !float.IsFinite(glowLevel) || glowLevel < 0 || glowLevel > MaximumGlowLevel)
                    {
                        error = ScorePreviewTextParseError.InvalidGlow;
                        return false;
                    }

                    currentStyle = currentStyle with { GlowLevel = glowLevel };
                }
                else
                {
                    error = ScorePreviewTextParseError.InvalidMarkup;
                    return false;
                }

                frames.Push(new StyleFrame(name, previousStyle));
            }

            index = closingBracket + 1;
        }

        FlushRun(runs, buffer, currentStyle);
        if (frames.Count != 0)
        {
            error = ScorePreviewTextParseError.InvalidMarkup;
            return false;
        }

        if (visibleLength == 0)
        {
            error = ScorePreviewTextParseError.Empty;
            return false;
        }

        if (visibleLength > MaximumTextLength)
        {
            error = ScorePreviewTextParseError.TooLong;
            return false;
        }

        if (runs.Count > MaximumRuns)
        {
            error = ScorePreviewTextParseError.TooManyRuns;
            return false;
        }

        text = new ScorePreviewText(runs);
        return true;
    }

    private static void FlushRun(List<ScorePreviewTextRun> runs, StringBuilder buffer, TextStyle style)
    {
        if (buffer.Length == 0)
            return;

        string value = buffer.ToString();
        buffer.Clear();

        if (runs.LastOrDefault() is { } previous &&
            previous.ColorHex == style.ColorHex && previous.GlowLevel.Equals(style.GlowLevel))
        {
            runs[^1] = previous with { Text = previous.Text + value };
            return;
        }

        runs.Add(new ScorePreviewTextRun(value, style.ColorHex, style.GlowLevel));
    }

    private static bool TryNormalizeColor(string value, out string color)
    {
        if (NamedColors.TryGetValue(value, out string? namedColor))
        {
            color = namedColor;
            return true;
        }

        string hex = value.StartsWith('#') ? value[1..] : value;
        if (hex.Length == 3 && hex.All(Uri.IsHexDigit))
        {
            color = $"#{char.ToUpperInvariant(hex[0])}{char.ToUpperInvariant(hex[0])}" +
                    $"{char.ToUpperInvariant(hex[1])}{char.ToUpperInvariant(hex[1])}" +
                    $"{char.ToUpperInvariant(hex[2])}{char.ToUpperInvariant(hex[2])}";
            return true;
        }

        if (hex.Length == 6 && hex.All(Uri.IsHexDigit))
        {
            color = $"#{hex.ToUpperInvariant()}";
            return true;
        }

        color = string.Empty;
        return false;
    }

    private sealed record TextStyle(string ColorHex, float GlowLevel);
    private sealed record StyleFrame(string Tag, TextStyle PreviousStyle);
}
