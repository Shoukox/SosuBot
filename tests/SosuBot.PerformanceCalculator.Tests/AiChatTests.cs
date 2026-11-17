using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Responses;
using SosuBot.Services;
using SosuBot.Services.Ai;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

#pragma warning disable OPENAI001

namespace SosuBot.PerformanceCalculator.Tests;

/// <summary>Chat memory, triggers and reply splitting of the AI persona; the parts that run without OpenAI.</summary>
public sealed class AiChatTests
{
    private const long BotId = 999;
    private const string BotUsername = "sosubot";

    private static Message GroupMessage(int id, string text, long chatId = -100, long userId = 1, Message? reply = null) => new()
    {
        Id = id,
        Chat = new Chat { Id = chatId, Type = ChatType.Supergroup, Title = "osu! чат" },
        From = new User { Id = userId, FirstName = "Шахзод", Username = "shoukko" },
        Date = DateTime.UtcNow,
        Text = text,
        ReplyToMessage = reply
    };

    [Fact]
    public void MemoryKeepsTheLastTwentyMessagesOfAChat()
    {
        var memory = new AiChatMemory();
        for (var i = 1; i <= 30; i++)
            memory.Remember(GroupMessage(i, $"сообщение {i}"));

        IReadOnlyList<AiChatMessage> recent = memory.GetRecent(-100);
        Assert.Equal(AiChatMemory.MessagesPerChat, recent.Count);
        Assert.Equal("сообщение 11", recent[0].Text);
        Assert.Equal("сообщение 30", recent[^1].Text);
    }

    [Fact]
    public void MemoryForgetsChatsBeyondItsLimit()
    {
        var memory = new AiChatMemory();
        for (long chat = 1; chat <= AiChatMemory.MaxChats + 1; chat++)
            memory.Remember(GroupMessage(1, "привет", chatId: -chat));

        Assert.True(memory.ChatCount <= AiChatMemory.MaxChats, $"{memory.ChatCount} chats");
        Assert.NotEmpty(memory.GetRecent(-(AiChatMemory.MaxChats + 1)));
    }

    [Fact]
    public void MemoryHidesAdminCommandsButKeepsWhatWasAskedOfTheAi()
    {
        var memory = new AiChatMemory();
        memory.Remember(GroupMessage(1, "/c msg 123 секрет"));
        memory.Remember(GroupMessage(2, "/c ai как дела?\nчто нового"));
        memory.Remember(GroupMessage(3, "/calc 98%"));

        Assert.Equal(["как дела?\nчто нового", "/calc 98%"], memory.GetRecent(-100).Select(m => m.Text));
        Assert.Equal("оцени\nскор", AiChatMemory.ExtractAiCommandPrompt("/c@sosubot ai оцени\nскор"));
        Assert.Equal("", AiChatMemory.ExtractAiCommandPrompt("/c ai"));
        Assert.Null(AiChatMemory.ExtractAiCommandPrompt("/c msg ai"));
    }

    [Fact]
    public void MemoryRemembersRepliesAndTheAssistantsOwnMessages()
    {
        var memory = new AiChatMemory();
        Message question = GroupMessage(1, "кто фкнул?");
        memory.Remember(GroupMessage(2, "я", userId: 2, reply: question));
        memory.RememberAssistant(-100, 3, "умничка", "Шахзод", "я");

        IReadOnlyList<AiChatMessage> recent = memory.GetRecent(-100);
        Assert.Equal("Шахзод", recent[0].ReplyToAuthorName);
        Assert.Equal("кто фкнул?", recent[0].ReplyToText);
        Assert.True(memory.IsAssistantMessage(-100, 3));
        Assert.False(memory.IsAssistantMessage(-100, 2));
    }

    [Theory]
    [InlineData("@sosubot подбери карту", "подбери карту")]
    [InlineData("Соня, оцени скор", "Соня, оцени скор")]
    [InlineData("сонь глянь", "сонь глянь")]
    [InlineData("просто болтаем", null)]
    [InlineData("сонный я сегодня", null)]
    public void GroupMessagesCallTheAiByMentionOrName(string text, string? expected)
    {
        Assert.Equal(expected, AiTriggers.Detect(GroupMessage(1, text), BotUsername, BotId, hasOsuLink: false));
    }

    [Fact]
    public void RepliesToTheBotAndPrivateMessagesCallTheAi()
    {
        Message botMessage = GroupMessage(1, "S rank 98%", userId: BotId);
        Assert.Equal("ну как?", AiTriggers.Detect(GroupMessage(2, "ну как?", reply: botMessage), BotUsername, BotId, false));

        Message privateMessage = GroupMessage(3, "привет");
        privateMessage.Chat = new Chat { Id = 1, Type = ChatType.Private };
        Assert.Equal("привет", AiTriggers.Detect(privateMessage, BotUsername, BotId, hasOsuLink: false));
        // A beatmap link in a private chat still gets the beatmap card.
        Assert.Null(AiTriggers.Detect(privateMessage, BotUsername, BotId, hasOsuLink: true));
    }

    [Fact]
    public void RepliesAreSplitAtTheSeparatorAndTelegramsLimit()
    {
        Assert.Equal(["ахах", "ну ты даёшь"], AiChatResponder.SplitIntoMessages("ахах\n|||\nну ты даёшь"));
        Assert.Empty(AiChatResponder.SplitIntoMessages("  "));
        Assert.Equal(4, AiChatResponder.SplitIntoMessages("1|||2|||3|||4|||5|||6").Count);
        Assert.Equal("4\n5\n6", AiChatResponder.SplitIntoMessages("1|||2|||3|||4|||5|||6")[^1]);
        Assert.Equal(2, AiChatResponder.SplitIntoMessages(new string('a', 5_000)).Count);
        Assert.Equal("карта - огонь\nзацени", AiChatResponder.SplitIntoMessages("карта — **огонь**  \nзацени")[0]);
    }

    [Fact]
    public void ToolSchemasSatisfyOpenAiStrictMode()
    {
        var toolbox = new AiToolbox(null!, null!, null!, null!, null!, null!, NullLogger<AiToolbox>.Instance);

        foreach (FunctionTool tool in toolbox.Tools.Cast<FunctionTool>())
        {
            using JsonDocument schema = JsonDocument.Parse(tool.FunctionParameters.ToString());
            JsonElement root = schema.RootElement;
            string[] properties = root.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order().ToArray();
            string[] required = root.GetProperty("required").EnumerateArray().Select(p => p.GetString()!).Order().ToArray();

            Assert.True(tool.StrictModeEnabled, tool.FunctionName);
            Assert.False(root.GetProperty("additionalProperties").GetBoolean(), tool.FunctionName);
            // Strict mode demands every property in "required"; optional ones are nullable instead.
            Assert.Equal(properties, required);
        }

        Assert.Contains(toolbox.Tools.Cast<FunctionTool>(), tool => tool.FunctionName == "calculate_pp");
        Assert.Equal("|||", OpenAiService.MessageSeparator);
    }
}
