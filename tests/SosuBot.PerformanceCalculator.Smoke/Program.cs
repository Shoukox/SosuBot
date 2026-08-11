using SosuBot.PerformanceCalculator;

var calculator = new PPCalculator();
using var beatmap = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "testdata", "native-fixture.osu"));
PPCalculationResult? result = calculator.CalculatePpAsync(
    beatmapId: 1,
    beatmapFile: beatmap,
    accuracy: 0.995,
    scoreMaxCombo: 220,
    scoreMods: [new osu.Game.Rulesets.Osu.Mods.OsuModHidden(), new osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime()],
    rulesetId: 0).GetAwaiter().GetResult();

if (result is null ||
    !double.IsFinite(result.PP) ||
    !double.IsFinite(result.DifficultyAttributes.StarRating) ||
    result.PP <= 0 ||
    result.DifficultyAttributes.StarRating <= 0)
{
    Console.Error.WriteLine("Official pp smoke calculation returned invalid values.");
    return 1;
}

Console.WriteLine(
    $"Official pp smoke succeeded: {result.PP:F6}pp, {result.DifficultyAttributes.StarRating:F6} stars");
return 0;
