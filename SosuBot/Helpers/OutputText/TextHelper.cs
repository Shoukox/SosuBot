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

    public static Stream TextToStream(string text)
    {
        var buffer = Encoding.Default.GetBytes(text);
        return new MemoryStream(buffer);
    }
}