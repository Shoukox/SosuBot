using System.Collections;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SosuBot.Database.Models;

namespace SosuBot.Database.Extensions;

public static class DatabaseExtensions
{
    public static string ToReadfriendlyTableString(this DbSet<OsuUser> set, int intervalSpaceCount = 5)
    {
        var users = set.ToArray();
        var rows = new string[users.Length + 1]; // +heading

        var osuUserType = typeof(OsuUser);
        var osuUserAttributes = osuUserType.GetProperties();
        var intervalString = new string(' ', intervalSpaceCount);

        // index at every row
        var indexPadding = users.Length.ToString().Length;
        rows = rows.Select((_, index) => $"{index + 1}".PadRight(indexPadding) + intervalString).ToArray();

        // ### heading into the heading row
        rows[0] = new string('#', indexPadding) + intervalString;

        foreach (var property in osuUserAttributes)
        {
            var propertyName = property.Name;

            // 4 is "null".Length
            var propertyValues = users.Select(u => property.GetValue(u)?.ToString() ?? "null").ToArray();

            // padding to use
            var padding = Math.Max(propertyValues.Select(m => m.Length).Max(), propertyName.Length);

            // add heading value
            rows[0] += propertyName.PadRight(padding) + intervalString;

            for (var i = 0; i <= users.Length - 1; i++)
            {
                var j = i + 1;
                var propertyValue = propertyValues[i];
                rows[j] += propertyValue.PadRight(padding) + intervalString;
            }
        }

        return string.Join("\n", rows);
    }

    public static string ToReadfriendlyTableString(this DbSet<TelegramChat> set, int intervalSpaceCount = 5)
    {
        var users = set.ToArray();
        var rows = new string[users.Length + 1]; // +heading

        var telegramChatType = typeof(TelegramChat);
        var telegramChatAttributes = telegramChatType.GetProperties();
        var intervalString = new string(' ', intervalSpaceCount);

        // index at every row
        var indexPadding = users.Length.ToString().Length;
        rows = rows.Select((_, index) => $"{index + 1}".PadRight(indexPadding) + intervalString).ToArray();

        // ### heading into the heading row
        rows[0] = new string('#', indexPadding) + intervalString;

        foreach (var property in telegramChatAttributes)
        {
            var propertyName = property.Name;

            // 4 is "null".Length
            var propertyIsArray = typeof(IEnumerable).IsAssignableFrom(property.PropertyType);
            var propertyValues = users.Select(chat =>
            {
                if (propertyIsArray)
                {
                    var array = (IEnumerable?)property.GetValue(chat);
                    if (array is null) return "null";
                    var text = "";
                    foreach (var item in array) text += item + " ";

                    if (text.Length != 0) text = text[..^1];

                    return text;
                }

                return property.GetValue(chat)?.ToString() ?? "null";
            }).ToArray();

            // padding to use
            var padding = Math.Max(propertyValues.Select(m => m.Length).Max(), propertyName.Length);

            // add heading value
            rows[0] += propertyName.PadRight(padding) + intervalString;

            for (var i = 0; i <= users.Length - 1; i++)
            {
                var j = i + 1;
                var propertyValue = propertyValues[i];
                rows[j] += propertyValue.PadRight(padding) + intervalString;
            }
        }

        return string.Join("\n", rows);
    }

    public static List<List<string>> RawSqlQuery(this DbContext context, string query)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = query;
        command.CommandType = CommandType.Text;

        context.Database.OpenConnection();

        using (var result = command.ExecuteReader())
        {
            var entities = new List<List<string>>();
            entities.Add(result.ReadDbRow(true));

            while (result.Read()) entities.Add(result.ReadDbRow());

            return entities;
        }
    }

    private static List<string> ReadDbRow(this DbDataReader reader, bool readerHeaderRow = false)
    {
        var row = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            string stringValue;
            if (!readerHeaderRow && reader.IsDBNull(i))
            {
                stringValue = "null";
            }
            else
            {
                if (readerHeaderRow)
                    stringValue = reader.GetName(i);
                else
                    stringValue = reader.GetString(i);
            }

            row.Add(stringValue);
        }

        return row;
    }
}