using System.Globalization;
using System.Reflection;
using System.Text.Json;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Rulesets.Mods;
using ApiModSettings = OsuApi.BanchoV2.Models.Settings;

namespace SosuBot.Helpers;

/// <summary>
/// Bridges the settings dictionary returned by osu!api and lazer's bindable mod settings.
/// </summary>
public static class OsuModSettingsHelper
{
    private static readonly IReadOnlyDictionary<string, string> SettingLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["accuracy_judge_mode"] = "accuracy mode",
            ["adjust_pitch"] = "pitch",
            ["affects_hit_sounds"] = "hit sounds",
            ["always_play_tail_sample"] = "tail sample",
            ["approach_rate"] = "AR",
            ["circle_size"] = "CS",
            ["drain_rate"] = "HP",
            ["overall_difficulty"] = "OD",
            ["scroll_speed"] = "SV",
            ["speed_change"] = "speed",
            ["initial_rate"] = "start rate",
            ["final_rate"] = "end rate",
            ["minimum_accuracy"] = "min accuracy",
        };

    /// <summary>
    /// Applies every setting known by the target lazer mod. Unknown API settings are deliberately ignored,
    /// which keeps the bot compatible with newer lazer versions and older ruleset packages.
    /// </summary>
    public static void Apply(ApiModSettings? settings, Mod mod)
    {
        ArgumentNullException.ThrowIfNull(mod);

        if (settings is null)
            return;

        IReadOnlyDictionary<string, JsonElement> values = GetValues(settings);

        foreach ((_, PropertyInfo property) in mod.GetSettingsSourceProperties())
        {
            string settingName = property.Name.ToSnakeCase();
            if (!values.TryGetValue(settingName, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
                continue;

            if (property.GetValue(mod) is not IBindable bindable)
                continue;

            try
            {
                object? convertedValue = ConvertValue(value, GetBindableValueType(bindable));
                if (convertedValue is null)
                    continue;

                if (bindable is IParseable parseable)
                    parseable.Parse(convertedValue, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                // A setting may be introduced by the API before the bot's osu.Game package knows its type.
                // It is safer to keep the mod usable with its default value in that case.
            }
        }
    }

    /// <summary>
    /// Formats all non-null API mod settings for score and map output.
    /// </summary>
    public static string FormatForDisplay(ApiModSettings? settings)
    {
        if (settings is null)
            return string.Empty;

        IReadOnlyDictionary<string, JsonElement> values = GetValues(settings);
        if (values.Count == 0)
            return string.Empty;

        IEnumerable<KeyValuePair<string, JsonElement>> orderedValues = values
            .OrderByDescending(pair => pair.Key.Equals("speed_change", StringComparison.OrdinalIgnoreCase));
        return $"({string.Join(", ", orderedValues.Select(FormatValue))})";
    }

    private static IReadOnlyDictionary<string, JsonElement> GetValues(ApiModSettings settings)
    {
        JsonElement serialized = JsonSerializer.SerializeToElement(settings);
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        foreach (JsonProperty property in serialized.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Null)
                values[property.Name] = property.Value.Clone();
        }

        return values;
    }

    private static string FormatValue(KeyValuePair<string, JsonElement> setting)
    {
        string name = setting.Key;
        JsonElement value = setting.Value;

        if (name.Equals("speed_change", StringComparison.OrdinalIgnoreCase))
            return $"{FormatNumber(value, fixedPoint: true)}x";

        if (name is "initial_rate" or "final_rate")
            return $"{SettingLabels[name]}={FormatNumber(value, fixedPoint: true)}x";

        string label = SettingLabels.GetValueOrDefault(name) ?? Humanize(name);
        return $"{label}={FormatValue(value)}";
    }

    private static string FormatValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => "on",
            JsonValueKind.False => "off",
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => FormatNumber(value),
            _ => value.ToString(),
        };
    }

    private static string FormatNumber(JsonElement value, bool fixedPoint = false)
    {
        if (!value.TryGetDouble(out double number))
            return value.ToString();

        return number.ToString(fixedPoint ? "0.00" : number == Math.Truncate(number) ? "0" : "0.##", CultureInfo.InvariantCulture);
    }

    private static string Humanize(string settingName) =>
        string.Join(' ', settingName.Split('_', StringSplitOptions.RemoveEmptyEntries));

    private static Type GetBindableValueType(IBindable bindable)
    {
        PropertyInfo? valueProperty = bindable.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        return Nullable.GetUnderlyingType(valueProperty?.PropertyType ?? typeof(string)) ?? valueProperty?.PropertyType ?? typeof(string);
    }

    private static object? ConvertValue(JsonElement value, Type targetType)
    {
        if (targetType == typeof(string))
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();

        if (targetType == typeof(bool))
        {
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return value.GetBoolean();

            return bool.Parse(value.ToString());
        }

        if (targetType.IsEnum)
        {
            if (value.ValueKind == JsonValueKind.String)
                return Enum.Parse(targetType, value.GetString()!, ignoreCase: true);

            return Enum.ToObject(targetType, value.GetInt32());
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            double number = value.GetDouble();
            return targetType switch
            {
                _ when targetType == typeof(double) => number,
                _ when targetType == typeof(float) => (float)number,
                _ when targetType == typeof(decimal) => (decimal)number,
                _ when targetType == typeof(long) => checked((long)number),
                _ when targetType == typeof(int) => checked((int)number),
                _ when targetType == typeof(short) => checked((short)number),
                _ when targetType == typeof(byte) => checked((byte)number),
                _ => Convert.ChangeType(number, targetType, CultureInfo.InvariantCulture),
            };
        }

        return Convert.ChangeType(value.ToString(), targetType, CultureInfo.InvariantCulture);
    }
}
