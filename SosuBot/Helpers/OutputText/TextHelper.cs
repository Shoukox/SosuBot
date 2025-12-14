using SosuBot.Extensions;
using SosuBot.Helpers.Types;
using System.Text;

namespace SosuBot.Helpers.OutputText;

public static class TextHelper
{
    /// <summary>
    ///     Returns a read friendly text in form of a table
    /// </summary>
    /// <param name="rows">
    ///     The first element should contain column names. The other ones - the values of each column field.
    ///     Rows[0..].Count should be equal
    /// </param>
    /// <returns>Read friendly string representing a table</returns>
    public static string GetReadfriendlyTable(List<List<string>> rows)
    {
        var columnCount = rows[0].Count;
        var columnWidths = new int[columnCount];

        for (var col = 0; col < columnCount; col++) columnWidths[col] = rows.Max(row => row[col].Length);

        var result = "";
        foreach (var row in rows)
        {
            for (var col = 0; col < columnCount; col++) result += row[col].PadRight(columnWidths[col] + 2);
            result += "\n";
        }

        return result;
    }

    public static Playmode? GetPlaymodeFromParameters(string[] parameters, out string[] parametersWithoutPlaymode)
    {
        var playmodeParameter = parameters.Where(m => m.Length == 1 && char.IsAsciiLetter(m[0])).FirstOrDefault();
        if (playmodeParameter == null)
        {
            parametersWithoutPlaymode = parameters;
            return null;
        }

        Playmode? playmode = playmodeParameter[0] switch
        {
            't' or 'T' => Playmode.Taiko,
            'c' or 'C' => Playmode.Catch,
            'm' or 'M' => Playmode.Mania,
            'o' => Playmode.Osu,
            _ => null
        };
        if (playmode != null)
        {
            parametersWithoutPlaymode = parameters.Where(m => m != playmodeParameter).ToArray();
        }
        else parametersWithoutPlaymode = parameters;
        return playmode;
    }

    public static Stream TextToStream(string text)
    {
        var buffer = Encoding.Default.GetBytes(text);
        return new MemoryStream(buffer);
    }
}