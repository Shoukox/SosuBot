using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osuTK;
using SosuBot.Calculators.Official;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using GameMod = osu.Game.Rulesets.Mods.Mod;

namespace SosuBot.Services.Ai;

/// <summary>How a beatmap plays, read from its .osu file. Times and BPM already include rate mods.</summary>
public sealed record BeatmapPatterns(
    string Mode,
    double Rate,
    BeatmapBpm Bpm,
    int LengthSeconds,
    int Objects,
    double AverageNotesPerSecond,
    double PeakNotesPerSecond,
    IReadOnlyDictionary<string, int> RhythmPercent,
    StandardPatterns? Standard,
    ManiaPatterns? Mania,
    TaikoPatterns? Taiko,
    CatchPatterns? Catch,
    IReadOnlyList<BeatmapSection> Sections);

public sealed record BeatmapBpm(double Main, double Min, double Max, int Changes);

/// <param name="StreamSpacing">How far apart stream notes are: stacked, spaced or wide.</param>
/// <param name="SharpJumpAnglePercent">Jumps that turn back sharply (back and forth) rather than flow on.</param>
public sealed record StandardPatterns(
    int Circles,
    int Sliders,
    int Spinners,
    int Streams,
    int LongestStream,
    double? StreamBpm,
    int StreamNotesPercent,
    string? StreamSpacing,
    int Bursts,
    int Jumps,
    int JumpPercentOfHalfBeats,
    int? AverageJumpPx,
    int? MaxJumpPx,
    int SharpJumpAnglePercent,
    int Kicksliders,
    int LongSliders,
    int SliderVelocityChanges);

public sealed record ManiaPatterns(int Keys, int LongNotePercent, int ChordNotePercent, int Jacks, double? FastestJackBpm);

public sealed record TaikoPatterns(int Hits, int DrumRolls, int Swells, int KatPercent, int ColourChangePercent, int LongestFastRun);

public sealed record CatchPatterns(int Fruits, int JuiceStreams, int BananaShowers, int Hyperdashes);

/// <param name="Kind">streams, bursts, jumps, sliders, mixed, calm or break.</param>
public sealed record BeatmapSection(string From, string To, string Kind, double NotesPerSecond);

/// <summary>
/// Reads a .osu file and describes its patterns: rhythm, streams, jumps, sliders, BPM and SV changes, density and a
/// timeline of sections, or the mode's own patterns for mania, taiko and catch. The beatmap is converted to the
/// requested ruleset with the mods applied first, so converts and HR/DT are described as they are played.
/// </summary>
public static class BeatmapPatternAnalyzer
{
    private const int MaxSections = 8;
    private const double SectionSeconds = 25;

    // Spacing thresholds in multiples of the circle diameter.
    private const double JumpDiameters = 2.5;
    private const double SpacedStreamDiameters = 0.9;
    private const double WideStreamDiameters = 1.6;

    private static readonly (string Label, double Beats)[] Rhythms =
    [
        ("1/8", 0.125), ("1/6", 1 / 6.0), ("1/4", 0.25), ("1/3", 1 / 3.0), ("1/2", 0.5), ("2/3", 2 / 3.0),
        ("3/4", 0.75), ("1/1", 1), ("2/1", 2)
    ];

    public static BeatmapPatterns Analyze(Stream beatmapFile, Playmode mode, GameMod[] mods)
    {
        beatmapFile.Position = 0;
        using var reader = new LineBufferedReader(beatmapFile, leaveOpen: true);
        Beatmap decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);

        Ruleset ruleset = mode switch
        {
            Playmode.Taiko => new TaikoRuleset(),
            Playmode.Catch => new CatchRuleset(),
            Playmode.Mania => new ManiaRuleset(),
            _ => new OsuRuleset()
        };
        IBeatmap playable = new SosuBot.PerformanceCalculator.LoadedBeatmap(decoded)
            .GetPlayableBeatmap(ruleset.RulesetInfo, mods);
        double rate = OfficialPerformanceHelper.GetClockRate(mods);
        List<HitObject> objects = playable.HitObjects.OrderBy(h => h.StartTime).ToList();

        double start = objects.Count > 0 ? objects[0].StartTime : 0;
        double end = objects.Count > 0 ? objects.Max(h => h.GetEndTime()) : 0;
        double lengthSeconds = Math.Max(0, end - start) / 1000 / rate;
        double[] times = objects.Select(h => h.StartTime).Distinct().ToArray();

        var section = new SectionCounter(start, end, rate);
        foreach (double time in times)
            section.CountObject(time);

        StandardPatterns? standard = null;
        ManiaPatterns? mania = null;
        TaikoPatterns? taiko = null;
        CatchPatterns? catchPatterns = null;
        switch (mode)
        {
            case Playmode.Mania:
                mania = AnalyzeMania(playable, objects);
                break;
            case Playmode.Taiko:
                taiko = AnalyzeTaiko(playable, objects);
                break;
            case Playmode.Catch:
                catchPatterns = AnalyzeCatch(objects);
                break;
            default:
                standard = AnalyzeStandard(playable, objects.OfType<OsuHitObject>().ToList(), rate, section);
                break;
        }

        return new BeatmapPatterns(
            mode.ToGamemode(),
            rate,
            DescribeBpm(playable.ControlPointInfo, start, end, rate),
            (int)Math.Round(lengthSeconds),
            objects.Count,
            lengthSeconds > 0 ? Math.Round(times.Length / lengthSeconds, 1) : 0,
            PeakNotesPerSecond(times, rate),
            RhythmShares(playable.ControlPointInfo, times),
            standard,
            mania,
            taiko,
            catchPatterns,
            section.Describe(standard is not null));
    }

    private static StandardPatterns AnalyzeStandard(IBeatmap beatmap, List<OsuHitObject> objects, double rate,
        SectionCounter sections)
    {
        double diameter = 2 * (objects.FirstOrDefault()?.Radius ?? 36.5);
        int streams = 0, bursts = 0, longestStream = 0, streamNotes = 0;
        var streamGaps = new List<double>();
        var streamSpacings = new List<double>();
        var jumpDistances = new List<double>();
        int halfBeatPairs = 0, sharpAngles = 0, angledJumps = 0;

        // A run is a chain of notes each a 1/4 beat (or faster) after the previous one.
        var run = new List<OsuHitObject>();
        void CloseRun()
        {
            if (run.Count >= 5)
            {
                streams++;
                streamNotes += run.Count;
                longestStream = Math.Max(longestStream, run.Count);
                for (int i = 1; i < run.Count; i++)
                {
                    streamGaps.Add(run[i].StartTime - run[i - 1].StartTime);
                    streamSpacings.Add(Vector2.Distance(EndPosition(run[i - 1]), run[i].StackedPosition));
                }

                foreach (OsuHitObject note in run)
                    sections.CountStream(note.StartTime);
            }
            else if (run.Count >= 3)
            {
                bursts++;
                foreach (OsuHitObject note in run)
                    sections.CountBurst(note.StartTime);
            }

            run.Clear();
        }

        for (int i = 0; i < objects.Count; i++)
        {
            OsuHitObject current = objects[i];
            if (current is Slider)
                sections.CountSlider(current.StartTime);
            if (i == 0 || current is Spinner || objects[i - 1] is Spinner)
            {
                CloseRun();
                run.Add(current);
                continue;
            }

            OsuHitObject previous = objects[i - 1];
            double beats = (current.StartTime - previous.StartTime) / BeatLength(beatmap.ControlPointInfo, current.StartTime);
            double distance = Vector2.Distance(EndPosition(previous), current.StackedPosition);

            if (beats <= 0.28 && previous is not Slider)
            {
                if (run.Count == 0)
                    run.Add(previous);
                run.Add(current);
            }
            else
            {
                CloseRun();
                run.Add(current);
            }

            bool halfBeat = beats is >= 0.3 and <= 0.7;
            if (halfBeat)
                halfBeatPairs++;
            if (halfBeat && distance >= JumpDiameters * diameter)
            {
                jumpDistances.Add(distance);
                sections.CountJump(current.StartTime);
                if (i >= 2 && objects[i - 2] is not Spinner)
                {
                    Vector2 incoming = current.StackedPosition - EndPosition(previous);
                    Vector2 back = objects[i - 2].StackedPosition - previous.StackedPosition;
                    if (incoming.Length > 0 && back.Length > 0)
                    {
                        angledJumps++;
                        double angle = MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(incoming), Vector2.Normalize(back)), -1, 1));
                        // A small angle between "where it came from" and "where it goes" means it turns back.
                        if (angle < Math.PI / 3)
                            sharpAngles++;
                    }
                }
            }
        }

        CloseRun();

        Slider[] sliders = objects.OfType<Slider>().ToArray();
        int kicksliders = sliders.Count(s => s.Duration <= 0.3 * BeatLength(beatmap.ControlPointInfo, s.StartTime));
        int longSliders = sliders.Count(s => s.Duration >= 2 * BeatLength(beatmap.ControlPointInfo, s.StartTime));
        int velocityChanges = 0;
        for (int i = 1; i < sliders.Length; i++)
        {
            if (Math.Abs(sliders[i].SliderVelocityMultiplier - sliders[i - 1].SliderVelocityMultiplier) > 0.01)
                velocityChanges++;
        }

        double? spacing = streamSpacings.Count > 0 ? streamSpacings.Average() / diameter : null;
        return new StandardPatterns(
            objects.Count(o => o is HitCircle),
            sliders.Length,
            objects.Count(o => o is Spinner),
            streams,
            longestStream,
            // The mean gap: .osu times are whole milliseconds, so single gaps are off by up to one.
            streamGaps.Count > 0 ? Math.Round(15_000 / streamGaps.Average() * rate) : null,
            Percent(streamNotes, objects.Count),
            spacing switch
            {
                null => null,
                < SpacedStreamDiameters => "stacked",
                < WideStreamDiameters => "spaced",
                _ => "wide"
            },
            bursts,
            jumpDistances.Count,
            Percent(jumpDistances.Count, halfBeatPairs),
            jumpDistances.Count > 0 ? (int)jumpDistances.Average() : null,
            jumpDistances.Count > 0 ? (int)jumpDistances.Max() : null,
            Percent(sharpAngles, angledJumps),
            kicksliders,
            longSliders,
            velocityChanges);
    }

    private static ManiaPatterns AnalyzeMania(IBeatmap beatmap, List<HitObject> objects)
    {
        int keys = (beatmap as ManiaBeatmap)?.TotalColumns ?? 0;
        ManiaHitObject[] notes = objects.OfType<ManiaHitObject>().ToArray();
        int chordNotes = notes.GroupBy(n => n.StartTime).Where(g => g.Count() > 1).Sum(g => g.Count());

        int jacks = 0;
        double? fastestJack = null;
        foreach (IGrouping<int, ManiaHitObject> column in notes.GroupBy(n => n.Column))
        {
            ManiaHitObject[] ordered = column.OrderBy(n => n.StartTime).ToArray();
            for (int i = 1; i < ordered.Length; i++)
            {
                double gap = ordered[i].StartTime - ordered[i - 1].StartTime;
                if (gap / BeatLength(beatmap.ControlPointInfo, ordered[i].StartTime) > 0.5)
                    continue;

                jacks++;
                fastestJack = fastestJack is null ? gap : Math.Min(fastestJack.Value, gap);
            }
        }

        return new ManiaPatterns(
            keys,
            Percent(notes.Count(n => n is HoldNote), notes.Length),
            Percent(chordNotes, notes.Length),
            jacks,
            // A jack every 1/4 beat at BPM X: X = 15000 / gap.
            fastestJack is { } gapMs && gapMs > 0 ? Math.Round(15_000 / gapMs) : null);
    }

    private static TaikoPatterns AnalyzeTaiko(IBeatmap beatmap, List<HitObject> objects)
    {
        Hit[] hits = objects.OfType<Hit>().ToArray();
        int colourChanges = 0, longestRun = 0, run = 1;
        for (int i = 1; i < hits.Length; i++)
        {
            if (hits[i].Type != hits[i - 1].Type)
                colourChanges++;

            double beats = (hits[i].StartTime - hits[i - 1].StartTime) / BeatLength(beatmap.ControlPointInfo, hits[i].StartTime);
            run = beats <= 0.28 ? run + 1 : 1;
            longestRun = Math.Max(longestRun, run);
        }

        return new TaikoPatterns(
            hits.Length,
            objects.Count(o => o is DrumRoll),
            objects.Count(o => o is Swell),
            Percent(hits.Count(h => h.Type == HitType.Rim), hits.Length),
            Percent(colourChanges, Math.Max(0, hits.Length - 1)),
            longestRun);
    }

    private static CatchPatterns AnalyzeCatch(List<HitObject> objects)
    {
        IEnumerable<PalpableCatchHitObject> palpable = objects
            .SelectMany(o => o is JuiceStream stream ? stream.NestedHitObjects.AsEnumerable() : [o])
            .OfType<PalpableCatchHitObject>();
        return new CatchPatterns(
            objects.Count(o => o is Fruit),
            objects.Count(o => o is JuiceStream),
            objects.Count(o => o is BananaShower),
            palpable.Count(o => o.HyperDash));
    }

    private static BeatmapBpm DescribeBpm(ControlPointInfo controlPoints, double start, double end, double rate)
    {
        TimingControlPoint[] points = controlPoints.TimingPoints.ToArray();
        if (points.Length == 0)
            return new BeatmapBpm(0, 0, 0, 0);

        // The main BPM is the one that lasts longest between the first and the last object.
        var durations = new Dictionary<double, double>();
        for (int i = 0; i < points.Length; i++)
        {
            double from = Math.Max(points[i].Time, start);
            double to = i + 1 < points.Length ? Math.Min(points[i + 1].Time, end) : end;
            double bpm = Math.Round(points[i].BPM * rate);
            durations[bpm] = durations.GetValueOrDefault(bpm) + Math.Max(0, to - from);
        }

        double[] all = points.Select(p => Math.Round(p.BPM * rate)).ToArray();
        int changes = 0;
        for (int i = 1; i < all.Length; i++)
        {
            if (Math.Abs(all[i] - all[i - 1]) >= 1)
                changes++;
        }

        return new BeatmapBpm(durations.MaxBy(pair => pair.Value).Key, all.Min(), all.Max(), changes);
    }

    private static IReadOnlyDictionary<string, int> RhythmShares(ControlPointInfo controlPoints, double[] times)
    {
        var counts = new Dictionary<string, int>();
        int total = 0;
        for (int i = 1; i < times.Length; i++)
        {
            double beats = (times[i] - times[i - 1]) / BeatLength(controlPoints, times[i]);
            if (beats > 2.5)
                continue; // a pause, not rhythm

            total++;
            (string label, double expected) = Rhythms.MinBy(r => Math.Abs(r.Beats - beats));
            string key = Math.Abs(expected - beats) <= expected * 0.12 ? label : "other";
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts
            .Where(pair => Percent(pair.Value, total) >= 1)
            .OrderByDescending(pair => pair.Value)
            .ToDictionary(pair => pair.Key, pair => Percent(pair.Value, total));
    }

    private static double PeakNotesPerSecond(double[] times, double rate)
    {
        int best = 0;
        for (int from = 0, to = 0; to < times.Length; to++)
        {
            while ((times[to] - times[from]) / rate >= 1000)
                from++;
            best = Math.Max(best, to - from + 1);
        }

        return best;
    }

    private static double BeatLength(ControlPointInfo controlPoints, double time) =>
        Math.Max(1, controlPoints.TimingPointAt(time).BeatLength);

    private static Vector2 EndPosition(OsuHitObject hitObject) =>
        hitObject is Slider slider ? slider.StackedEndPosition : hitObject.StackedPosition;

    private static int Percent(int part, int whole) => whole > 0 ? (int)Math.Round(100.0 * part / whole) : 0;

    /// <summary>Splits the map into equal stretches of time and names what each one is mostly made of.</summary>
    private sealed class SectionCounter
    {
        private readonly double _start;
        private readonly double _length;
        private readonly double _rate;
        private readonly int[] _objects;
        private readonly int[] _streams;
        private readonly int[] _bursts;
        private readonly int[] _jumps;
        private readonly int[] _sliders;

        public SectionCounter(double start, double end, double rate)
        {
            _start = start;
            _rate = rate;
            double seconds = Math.Max(0, end - start) / 1000 / rate;
            int count = Math.Clamp((int)Math.Ceiling(seconds / SectionSeconds), 1, MaxSections);
            _length = Math.Max(1, end - start) / count;
            _objects = new int[count];
            _streams = new int[count];
            _bursts = new int[count];
            _jumps = new int[count];
            _sliders = new int[count];
        }

        public void CountObject(double time) => _objects[Index(time)]++;
        public void CountStream(double time) => _streams[Index(time)]++;
        public void CountBurst(double time) => _bursts[Index(time)]++;
        public void CountJump(double time) => _jumps[Index(time)]++;
        public void CountSlider(double time) => _sliders[Index(time)]++;

        public IReadOnlyList<BeatmapSection> Describe(bool standard)
        {
            double seconds = _length / 1000 / _rate;
            var sections = new List<BeatmapSection>();
            for (int i = 0; i < _objects.Length; i++)
            {
                double density = Math.Round(_objects[i] / seconds, 1);
                string kind = !standard ? Density(density)
                    : _objects[i] == 0 ? "break"
                    : _streams[i] >= _objects[i] * 0.35 ? "streams"
                    : _jumps[i] >= _objects[i] * 0.3 ? "jumps"
                    : _bursts[i] >= _objects[i] * 0.3 ? "bursts"
                    : _sliders[i] >= _objects[i] * 0.5 ? "sliders"
                    : density < 2 ? "calm"
                    : "mixed";
                sections.Add(new BeatmapSection(Clock(i * _length), Clock((i + 1) * _length), kind, density));
            }

            return sections;
        }

        private static string Density(double notesPerSecond) => notesPerSecond switch
        {
            0 => "break",
            < 3 => "calm",
            < 7 => "mixed",
            _ => "dense"
        };

        private int Index(double time) => Math.Clamp((int)((time - _start) / _length), 0, _objects.Length - 1);

        private string Clock(double offset)
        {
            var span = TimeSpan.FromMilliseconds((_start + offset) / _rate);
            return $"{(int)span.TotalMinutes}:{span.Seconds:00}";
        }
    }
}
