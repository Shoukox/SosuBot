using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using osu.Framework.Extensions.IEnumerableExtensions;
using SosuBot.Database.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.InlineQueryResults;

namespace SosuBot.Database.Extensions
{
    public static class DatabaseExtensions
    {
        public static string ToReadfriendlyTableString(this DbSet<OsuUser> set, int intervalSpaceCount = 5)
        {
            OsuUser[] users = set.ToArray();
            var rows = new string[users.Length + 1]; // +heading

            var osuUserType = typeof(OsuUser);
            var osuUserAttributes = osuUserType.GetProperties();
            string intervalString = new string(' ', intervalSpaceCount);

            // index at every row
            int indexPadding = users.Length.ToString().Length;
            rows = rows.Select((row, index) => $"{index + 1}".PadRight(indexPadding) + intervalString).ToArray();

            // ### heading into the heading row
            rows[0] = new string('#', indexPadding) + intervalString;

            foreach (var property in osuUserAttributes)
            {
                string propertyName = property.Name;

                // 4 is "null".Length
                string[] propertyValues = users.Select(u => property.GetValue(u)?.ToString() ?? "null").ToArray();

                // padding to use
                int padding = Math.Max(propertyValues.Select(m => m.Length).Max(), propertyName.Length);

                // add heading value
                rows[0] += propertyName.PadRight(padding) + intervalString;

                for (int i = 0; i <= users.Length - 1; i++)
                {
                    int j = i + 1;
                    string propertyValue = propertyValues[i];
                    rows[j] += propertyValue.PadRight(padding) + intervalString;
                }
            }

            return string.Join("\n", rows);
        }

        public static string ToReadfriendlyTableString(this DbSet<TelegramChat> set, int intervalSpaceCount = 5)
        {
            TelegramChat[] users = set.ToArray();
            var rows = new string[users.Length + 1]; // +heading

            var telegramChatType = typeof(TelegramChat);
            var telegramChatAttributes = telegramChatType.GetProperties();
            string intervalString = new string(' ', intervalSpaceCount);

            // index at every row
            int indexPadding = users.Length.ToString().Length;
            rows = rows.Select((row, index) => $"{index + 1}".PadRight(indexPadding) + intervalString).ToArray();

            // ### heading into the heading row
            rows[0] = new string('#', indexPadding) + intervalString;

            foreach (var property in telegramChatAttributes)
            {
                string propertyName = property.Name;

                // 4 is "null".Length
                bool propertyIsArray = typeof(IEnumerable).IsAssignableFrom(property.PropertyType);
                string[] propertyValues = users.Select(chat =>
                {
                    if (propertyIsArray)
                    {
                        IEnumerable? array = (IEnumerable?)property.GetValue(chat);
                        if (array is null) return "null";
                        string text = "";
                        foreach (var item in array)
                        {
                            text += item.ToString() + " ";
                        }

                        if (text.Length != 0)
                        {
                            text = text[0..^1];
                        }

                        return text;
                    }
                    else
                    {
                        return property.GetValue(chat)?.ToString() ?? "null";
                    }
                }).ToArray();

                // padding to use
                int padding = Math.Max(propertyValues.Select(m => m.Length).Max(), propertyName.Length);

                // add heading value
                rows[0] += propertyName.PadRight(padding) + intervalString;

                for (int i = 0; i <= users.Length - 1; i++)
                {
                    int j = i + 1;
                    string propertyValue = propertyValues[i];
                    rows[j] += propertyValue.PadRight(padding) + intervalString;
                }
            }

            return string.Join("\n", rows);
        }

        public static List<List<string>> RawSqlQuery(this DbContext context, string query)
        {
            Func<DbDataReader, List<string>> readRow = (DbDataReader reader) =>
            {
                List<string> row = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.GetString(i));
                }
                return row;
            };
            
            Func<DbDataReader, List<string>> readFieldNames = (DbDataReader reader) =>
            {
                List<string> row = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.GetName(i));
                }
                return row;
            };
            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = query;
                command.CommandType = CommandType.Text;

                context.Database.OpenConnection();
                
                using (var result = command.ExecuteReader())
                {
                    var entities = new List<List<string>>();
                    entities.Add(readFieldNames(result));
                    
                    while (result.Read())
                    {
                        entities.Add(readRow(result));
                    }
                    return entities;
                }
            }
        }
    }
}