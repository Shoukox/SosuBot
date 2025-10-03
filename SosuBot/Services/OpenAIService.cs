using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.Responses;
using osu.Game.Online.API.Requests;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;
using OsuApi.V2.Users.Models;
using Polly;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.Types;
using SosuBot.Helpers.Types.Statistics;

#pragma warning disable OPENAI001

namespace SosuBot.Services;

/// <summary>
/// Service for managing OpenAI chat interactions with osu! integration capabilities.
/// Provides conversation management, function calling for osu! user data retrieval,
/// and thread-safe access control for concurrent user interactions.
/// </summary>
public sealed class OpenAiService
{
    /// <summary>
    /// Thread-safe dictionary that stores chat conversation history for each user.
    /// Key: Telegram user ID (long)
    /// Value: List of ResponseItem objects representing the conversation messages
    /// </summary>
    private readonly ConcurrentDictionary<long, List<ResponseItem>> _chatDictionary = new();

    /// <summary>
    /// Thread-safe dictionary that tracks the processing state for each user to prevent concurrent requests.
    /// Key: Telegram user ID (long)
    /// Value: Boolean indicating if the user currently has an active request being processed
    /// </summary>
    private readonly ConcurrentDictionary<long, bool> _syncDictionary = new();

    private readonly string _developerPrompt =
        "name=ShkX;role=osu!assistant;domain=osu!;lang=ru;tone=cynical,short;terms=keep_english_osu;rules=no_questions,not_repetitive;offtopic=mark;format=telegram_markdown;osuprofiles_compare_priority=top_scores_pp>total_pp>playtime>register_timestamp>count_top>recency;analyze_osubeatmaps=stream_vs_aim;" +
        "Не давай полную сводку. Всегда используй markdown стили (bold/italic) в своем ответе.";

    private readonly FunctionTool _getOsuUserTool = ResponseTool.CreateFunctionTool(
        FunctionNames.GetOsuUser,
        functionDescription: "Gets osu! user profile as json",
        functionParameters: BinaryData.FromBytes(
            """
                {
                  "type": "object",
                  "properties": {
                      "user_id": {
                          "anyOf": [
                              { "type": "string" },
                              { "type": "integer" }
                            ],
                          "description": "Username (optionally prefixed with '@') or numeric user id of an osu! player"
                      }
                  },
                  "additionalProperties": false,
                  "required": [ "user_id" ],
                  "examples": [
                           { "user_id": 15319810 },
                           { "user_id": "@shoukko" },
                           { "user_id": "shoukko" }
                  ]
                }
                """u8.ToArray()),
        strictModeEnabled: false
    );

    private readonly FunctionTool _getUserBestTool = ResponseTool.CreateFunctionTool(
        FunctionNames.GetUserScores,
        functionDescription: "Gets osu!player's recent/best/pinned scores",
        functionParameters: BinaryData.FromBytes(
            """
                {
                  "type": "object",
                  "properties": {
                        "user_id": {
                            "type": "integer",
                            "description": "Numeric user id of an osu! player. If you have only username, you can gain the id via GetOsuUser"
                        },
                        "score_type":{
                            "type": "string",
                            "description": "Use 'best' for best scores. Use 'recent' for recent scores. Use 'pinned' for pinned scores. Use 'firsts' for the scores where the used gained rank #1 on the beatmap"
                        },
                        "include_fails": {
                            "type": "integer",
                            "description": "Use 1 to include failed scores. Else 0."
                        },
                        "mode": {
                            "type": "integer",
                            "description": "From which osu! gamemode (std, taiko, catch, mania) should scores be returned. Use 0=std, 1=taiko, 2=catch, 3=mania"
                        },
                        "limit": {
                            "type": "integer",
                            "description": "Indicates, how many scores will be returned. Interval=[1; 200]"
                        }
                  },
                  "additionalProperties": false,
                  "required": [ "user_id", "score_type", "include_fails", "mode", "limit"],
                  "examples": [
                           { "user_id": 15319810, "score_type": "best", "include_fails": 0, "mode": 0, "limit": 100 },
                           { "user_id": 15319810, "score_type": "recent", "include_fails": 1, "mode": 0, "limit": 5 },
                           { "user_id": 15319810, "score_type": "pinned", "include_fails": 0, "mode": 0, "limit": 2 }
                  ]
                }
                """u8.ToArray()),
        strictModeEnabled: false
    );

    private readonly FunctionTool _getCountryRankingTool = ResponseTool.CreateFunctionTool(
        FunctionNames.GetCountryRanking,
        functionDescription: "Gets players from the country leaderboard. You can use it to gain players from a given country",
        functionParameters: BinaryData.FromBytes(
            """
                {
                  "type": "object",
                  "properties": {
                        "count": {
                            "type": "integer",
                            "description": "How many players going to be retrieved from the ranking"
                        },
                        "country_code":{
                            "type": "string",
                            "description": "Two-letter country code to retrieve the ranking from"
                        }
                  },
                  "additionalProperties": false,
                  "required": [ "count", "country_code"],
                  "examples": [
                           { "count": 50, "country_code": "uz" },
                           { "count": 75, "country_code": "ru"},
                           { "count": 100, "country_code": "uz" }
                  ]
                }
                """u8.ToArray()),
        strictModeEnabled: false
    );
        
    private readonly string _model = "gpt-5-nano";
    private readonly string _openaiToken = Environment.GetEnvironmentVariable("OpenAIToken")!;
    private readonly ApiV2 _osuApiV2;
    private readonly OpenAIResponseClient _responseClient;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(ApiV2 osuApiV2, ILogger<OpenAiService> logger)
    {
        _logger = logger;
        _osuApiV2 = osuApiV2;
        _responseClient = new OpenAIResponseClient(_model, _openaiToken);
    }

    public async Task<Result<string>> GetResponseAsync(string userInput, long userTelegramId)
    {
        if (_syncDictionary.TryGetValue(userTelegramId, out var locked) && locked)
        {
            return new Result<string>(null, new Error(ErrorCode.Locked), false);
        }

        _syncDictionary[userTelegramId] = true;

        List<ResponseItem> inputItems;
        MessageResponseItem developerMessageItem = ResponseItem.CreateDeveloperMessageItem(_developerPrompt);
        MessageResponseItem userResponseItem = ResponseItem.CreateUserMessageItem(userInput);
        if (_chatDictionary.ContainsKey(userTelegramId))
        {
            _chatDictionary[userTelegramId].Add(userResponseItem);
            TrimMessageHistory(_chatDictionary[userTelegramId]);
            inputItems = new List<ResponseItem>(_chatDictionary[userTelegramId]);
        }
        else
        {
            inputItems =
            [
                developerMessageItem,
                userResponseItem
            ];
            _chatDictionary[userTelegramId] = new List<ResponseItem>(inputItems);
        }

        ResponseCreationOptions options = new()
        {
            Tools = { _getOsuUserTool, _getUserBestTool, _getCountryRankingTool }
        };

        var output = "";
        bool requiresAction;
        try
        {
            do
            {
                requiresAction = false;
                OpenAIResponse response = await _responseClient.CreateResponseAsync(inputItems, options);
                inputItems.AddRange(response.OutputItems);

                foreach (var functionCall in response.OutputItems.OfType<FunctionCallResponseItem>())
                {
                    switch (functionCall.FunctionName)
                    {
                        case FunctionNames.GetOsuUser:
                        {
                            var userId =
                                functionCall.FunctionArguments.ToObjectFromJson<Dictionary<string, string>>()![
                                    "user_id"];
                            GetUserResponse? getUserResponse =
                                await _osuApiV2.Users.GetUser(userId, new GetUserQueryParameters());
                            string functionOutput = JsonConvert.SerializeObject(getUserResponse?.UserExtend);
                            inputItems.Add(new FunctionCallOutputResponseItem(functionCall.CallId, functionOutput));
                            break;
                        }
                        case FunctionNames.GetUserScores:
                        {
                            OpenAiFunctionCallParameters parameters = functionCall.FunctionArguments
                                .ToObjectFromJson<OpenAiFunctionCallParameters>()!;
                            long userId = parameters.UserId!.Value;
                            string scoreType = parameters.ScoreType!;
                            string mode = ((Playmode)parameters.Mode!).ToRuleset();
                            int limit = parameters.Limit!.Value;

                            var getUserBestResponse =
                                await _osuApiV2.Users.GetUserScores(userId, scoreType,
                                    new() { Mode = mode, Limit = limit });
                            string functionOutput = JsonConvert.SerializeObject(getUserBestResponse?.Scores);
                            inputItems.Add(new FunctionCallOutputResponseItem(functionCall.CallId, functionOutput));
                            break;
                        }
                        case FunctionNames.GetCountryRanking:
                        {
                            OpenAiFunctionCallParameters parameters = functionCall.FunctionArguments
                                .ToObjectFromJson<OpenAiFunctionCallParameters>()!;
                            int count = parameters.Count!.Value;
                            string countryCode = parameters.CountryCode!;

                            List<UserStatistics>? getUserBestResponse =
                                await OsuApiHelper.GetUsersFromRanking(_osuApiV2, countryCode, count);
                            string functionOutput = JsonConvert.SerializeObject(getUserBestResponse);
                            inputItems.Add(new FunctionCallOutputResponseItem(functionCall.CallId, functionOutput));
                            break;
                        }
                        default:
                        {
                            throw new NotImplementedException($"Unknown response item type: {functionCall.Id}");
                        }
                    }

                    requiresAction = true;
                }

                var messageResponseItems = response.OutputItems.OfType<MessageResponseItem>();
                foreach (var messageResponseItem in messageResponseItems)
                {
                    string currentOutputText = messageResponseItem.Content[0].Text;
                    output += currentOutputText;
                    _chatDictionary[userTelegramId].Add(ResponseItem.CreateAssistantMessageItem(currentOutputText));
                }
            } while (requiresAction);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception while requesting to openai api");
            _syncDictionary[userTelegramId] = false;
            return new Result<string>(null, new Error(ErrorCode.Error), false);
        }

        _syncDictionary[userTelegramId] = false;
        return new Result<string>(output, null, true);
    }

    void TrimMessageHistory(List<ResponseItem> items, int keepLast = 7)
    {
        if (items.Count <= keepLast) return;
        items.RemoveRange(1, items.Count - keepLast);
    }

    public static class FunctionNames
    {
        public const string GetOsuUser = "GetOsuUser";
        public const string GetUserScores = "GetUserScores";
        public const string GetCountryRanking = "GetCountryRanking";
    }
}