using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using SosuBot.Services.Ai;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;
using Xunit;

namespace SosuBot.PerformanceCalculator.Tests;

/// <summary>The AI's tools without network: their wiring, the text cleanup and the command guide.</summary>
public sealed class AiToolsTests
{
    // Registered but deliberately not documented: admin commands stay secret, /userw and /lw are hidden
    // (Telegram forbade sendWidget for ordinary bots), /ll and /postvideo are not in /help.
    private static readonly HashSet<string> UndocumentedCommands =
        ["/c", "/db", "/del", "/msg", "/userw", "/lw", "/ll", "/postvideo"];

    private static readonly AiToolContext GroupContext = new(-100, PrivateChat: false, SpeakerId: 1, _ => Task.FromResult(true));

    [Fact]
    public void ToolNamesAreUniqueAndEveryToolIsDispatched()
    {
        var toolbox = new AiToolbox(null!, null!, null!, null!, null!, null!, NullLogger<AiToolbox>.Instance);
        string[] names = AiToolbox.Definitions.Select(tool => tool.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());

        foreach (string name in names)
        {
            // Without services the call fails inside the tool, which proves the dispatcher knows it.
            string output = toolbox.ExecuteAsync(name, BinaryData.FromString("{}"), GroupContext, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert.DoesNotContain("unknown tool", output);
        }
    }

    [Fact]
    public void WikiMarkdownBecomesPlainText()
    {
        const string markdown = """
            ---
            tags:
              - HD
            ---

            # Hidden

            ![Icon](/wiki/shared/mods/HD.png "Hidden")

            **Hidden** (`HD`) hides [approach circles](/wiki/Gameplay/Hit_object/Approach_circle) when AR < 5 [^note].

            ## Score multiplier {id=multiplier}

            See the [mods][mods-link].<!-- translator note -->

            [mods-link]: /wiki/Gameplay/Game_modifier
            """;

        string text = AiText.CleanWikiMarkdown(markdown);

        Assert.Equal("# Hidden\n\nHidden (HD) hides approach circles when AR < 5 .\n\n## Score multiplier\n\nSee the mods.", text);
    }

    [Fact]
    public void WikiSectionsCanBeReadOneByOne()
    {
        string text = AiText.CleanWikiMarkdown("Intro\n\n## Aim\n\nMove.\n\n### Flow\n\nSmooth.\n\n## Speed\n\nTap.");
        IReadOnlyList<AiText.Section> sections = AiText.SplitSections(text);

        Assert.Equal(["", "Aim", "Flow", "Speed"], sections.Select(section => section.Title));
        Assert.Equal("Move.\n\n### Flow\nSmooth.", AiText.FindSection(sections, "aim"));
        Assert.Equal("Tap.", AiText.FindSection(sections, "## Spe"));
        Assert.Null(AiText.FindSection(sections, "Reading"));
    }

    [Fact]
    public void NewsHtmlBecomesPlainText()
    {
        string text = AiText.HtmlToText(
            "<h2>Results</h2><p>Team&nbsp;UZ won &amp; celebrated!</p><ul><li>1st: UZ</li><li>2nd: RU</li></ul><script>x()</script>");

        Assert.Equal("Results\n\nTeam UZ won & celebrated!\n\n- 1st: UZ\n- 2nd: RU", text);
    }

    [Fact]
    public void LongTextIsCutAtAWord()
    {
        string cut = AiText.Truncate(string.Join(' ', Enumerable.Repeat("stream", 2_000)), 100);

        Assert.True(cut.Length <= 100 + " …(обрезано)".Length);
        Assert.EndsWith("stream …(обрезано)", cut);
        Assert.Equal("short", AiText.Truncate("short", 100));
    }

    [Theory]
    [InlineData("https://osu.ppy.sh/wiki/ru/Gameplay/Hit_object#approach", "Gameplay/Hit_object")]
    [InlineData("https://osu.ppy.sh/wiki/en/Game_modifier/Hidden?locale=ru", "Game_modifier/Hidden")]
    [InlineData("Game modifier/Hidden", "Game_modifier/Hidden")]
    [InlineData("/pt-br/People/Tournament_Committee", "People/Tournament_Committee")]
    [InlineData("Skinning", "Skinning")]
    public void WikiPathsAreTakenFromLinks(string input, string expected) =>
        Assert.Equal(expected, AiToolbox.WikiPath(input));

    [Fact]
    public void CommandGuideDocumentsEveryPublicCommandAndNoHiddenOne()
    {
        string guide = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ai-bot-commands.txt"));
        string[] commands = typeof(AiToolbox).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(CommandBase<Message>).IsAssignableFrom(type))
            .Select(type => type.GetField("Commands", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                ?.GetValue(null) as string[])
            .OfType<string[]>()
            .Select(names => names[0])
            .Distinct()
            .ToArray();

        Assert.NotEmpty(commands);
        foreach (string command in commands.Where(command => !UndocumentedCommands.Contains(command)))
            Assert.Matches($@"(?<![\w/]){System.Text.RegularExpressions.Regex.Escape(command)}(?![\w])", guide);
        foreach (string hidden in UndocumentedCommands)
            Assert.DoesNotMatch($@"(?<![\w/]){System.Text.RegularExpressions.Regex.Escape(hidden)}(?![\w])", guide);
    }
}
