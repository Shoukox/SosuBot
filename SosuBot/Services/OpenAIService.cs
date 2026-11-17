using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenAI.Responses;
using OsuApi.BanchoV2;
using OsuApi.BanchoV2.Clients.Users.HttpIO;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Configuration;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.Types;
using SosuBot.Helpers.Types.Statistics;
using System.Collections.Concurrent;

#pragma warning disable OPENAI001

namespace SosuBot.Services;

/// <summary>
///     Service for managing OpenAI chat interactions with osu! integration capabilities.
///     Provides conversation management, function calling for osu! user data retrieval,
///     and thread-safe access control for concurrent user interactions.
/// </summary>
public sealed class OpenAiService
{
    /// <summary>
    ///     Thread-safe dictionary that stores chat conversation history for each user.
    ///     Key: Telegram user ID (long)
    ///     Value: List of ResponseItem objects representing the conversation messages
    /// </summary>
    private readonly ConcurrentDictionary<long, List<ResponseItem>> _chatDictionary = new();

    /// <summary>When each conversation in <see cref="_chatDictionary" /> was last used.</summary>
    private readonly ConcurrentDictionary<long, DateTime> _conversationLastUsedUtc = new();

    /// <summary>A conversation nobody continued for this long is forgotten.</summary>
    private static readonly TimeSpan ConversationIdleTimeout = TimeSpan.FromHours(6);

    /// <summary>Upper bound on remembered conversations; the least recently used ones go first.</summary>
    private const int MaxConversations = 1_000;

    private readonly FunctionTool _getCountryRankingTool = ResponseTool.CreateFunctionTool(
        FunctionNames.GetCountryRanking,
        functionDescription:
        "Gets players from the country leaderboard. You can use it to gain players from a given country",
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
                        },
                        "mode": {
                            "type": "integer",
                            "description": "From which osu! gamemode (std, taiko, catch, mania) should scores be returned. Use 0=std, 1=taiko, 2=catch, 3=mania"
                        }
                  },
                  "additionalProperties": false,
                  "required": [ "count", "country_code", "mode"],
                  "examples": [
                           { "count": 50, "country_code": "uz", "mode": 0 },
                           { "count": 75, "country_code": "ru", "mode": 1 },
                           { "count": 100, "country_code": "uz", "mode": 2 }
                  ]
                }
                """u8.ToArray()),
        strictModeEnabled: false
    );

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
                            "description": "Numeric user id of an osu! player. If you have only username, you can gain the id via function call GetOsuUser"
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
                            "description": "Indicates, how many scores will be returned. Interval=[1; 5]"
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

    private readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    private readonly ILogger<OpenAiService> _logger;

    private readonly string _openaiToken;
    private readonly BanchoApiV2 _osuApiV2;

    /// <summary>
    ///     Users with a request in progress, to prevent concurrent requests from one user. An entry lives only
    ///     while its request runs.
    /// </summary>
    private readonly ConcurrentDictionary<long, byte> _activeRequests = new();

    private string _model;
    private readonly ResponsesClient _responseClient;

    public OpenAiService(BanchoApiV2 osuApiV2, ILogger<OpenAiService> logger, IOptions<OpenAiConfiguration> openAiConfig)
    {
        _logger = logger;
        _osuApiV2 = osuApiV2;

        _openaiToken = Environment.GetEnvironmentVariable("OPEN_AI_TOKEN") ?? openAiConfig.Value.Token;
        _model = openAiConfig.Value.Model;

        DeveloperPrompt = File.ReadAllText("developer-prompt.txt");
        _responseClient = new ResponsesClient(_openaiToken);
    }

    private string DeveloperPrompt { get; }

    /// <summary>
    ///     Changes the model for this gpt instance
    /// </summary>
    /// <param name="model">
    ///     <see cref="Model" />
    /// </param>
    public void ChangeModel(string model)
    {
        _model = model;
    }

    public async Task<Result<string>> GetResponseAsync(string userInput, long userTelegramId)
    {
        if (!_activeRequests.TryAdd(userTelegramId, 0))
            return new Result<string>(null, new Error(ErrorCode.Locked), false);

        try
        {
            _conversationLastUsedUtc[userTelegramId] = DateTime.UtcNow;
            ForgetStaleConversations();
            return await GetResponseCoreAsync(userInput, userTelegramId);
        }
        finally
        {
            _activeRequests.TryRemove(userTelegramId, out _);
        }
    }

    private void ForgetStaleConversations()
    {
        DateTime idleSince = DateTime.UtcNow - ConversationIdleTimeout;
        foreach ((long userId, DateTime lastUsed) in _conversationLastUsedUtc)
        {
            if (lastUsed < idleSince && !_activeRequests.ContainsKey(userId))
                ForgetConversation(userId);
        }

        int excess = _conversationLastUsedUtc.Count - MaxConversations;
        if (excess <= 0)
            return;

        foreach (long userId in _conversationLastUsedUtc
                     .Where(pair => !_activeRequests.ContainsKey(pair.Key))
                     .OrderBy(pair => pair.Value)
                     .Take(excess)
                     .Select(pair => pair.Key)
                     .ToList())
            ForgetConversation(userId);
    }

    private void ForgetConversation(long userId)
    {
        _chatDictionary.TryRemove(userId, out _);
        _conversationLastUsedUtc.TryRemove(userId, out _);
    }

    private async Task<Result<string>> GetResponseCoreAsync(string userInput, long userTelegramId)
    {
        List<ResponseItem> inputItems;
        MessageResponseItem developerMessageItem = ResponseItem.CreateDeveloperMessageItem(DeveloperPrompt);
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

        var output = "";
        bool requiresAction;
        try
        {
            do
            {
                requiresAction = false;
                CreateResponseOptions options = new()
                {
                    Model = _model,
                    Tools = { _getOsuUserTool, _getUserBestTool, _getCountryRankingTool },
                    Temperature = 0.7f
                };
                foreach (ResponseItem inputItem in inputItems) options.InputItems.Add(inputItem);

                ResponseResult response = await _responseClient.CreateResponseAsync(options);
                inputItems.AddRange(response.OutputItems);

                foreach (FunctionCallResponseItem functionCall in response.OutputItems.OfType<FunctionCallResponseItem>())
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
                                getUserResponse!.UserExtend!.Cover = null;
                                getUserResponse.UserExtend.CoverUrl = null;
                                getUserResponse.UserExtend.DefaultGroup = null;
                                getUserResponse.UserExtend.Groups = null;
                                getUserResponse.UserExtend.MaxBlocks = null;
                                getUserResponse.UserExtend.MaxFriends = null;
                                getUserResponse.UserExtend.ProfileOrder = null;
                                getUserResponse.UserExtend.UserAchievements = null;
                                getUserResponse.UserExtend.ReplaysWatchedCounts = null;

                                var functionOutput = JsonConvert.SerializeObject(getUserResponse.UserExtend,
                                    Formatting.None, _jsonSerializerSettings);
                                inputItems.Add(new FunctionCallOutputResponseItem(functionCall.CallId, functionOutput));
                                break;
                            }
                        case FunctionNames.GetUserScores:
                            {
                                OpenAiFunctionCallParameters parameters = functionCall.FunctionArguments
                                    .ToObjectFromJson<OpenAiFunctionCallParameters>()!;
                                var userId = parameters.UserId!.Value;
                                var scoreType = parameters.ScoreType!;
                                var mode = ((Playmode)parameters.Mode!).ToRuleset();
                                var limit = parameters.Limit!.Value;

                                GetUserScoresResponse? getUserBestResponse =
                                    await _osuApiV2.Users.GetUserScores(userId, scoreType,
                                        new GetUserScoreQueryParameters { Mode = mode, Limit = limit });
                                Score[] scores = getUserBestResponse!.Scores;
                                Array.ForEach(scores, m =>
                                {
                                    m.Beatmap!.Checksum = null;
                                    m.Beatmap.Failtimes = null;
                                    m.Beatmap.Mode = null;
                                    m.Beatmap.Owners = null;

                                    m.Beatmapset!.User = null;
                                    m.Beatmapset.UserId = null;
                                    m.Beatmapset.ArtistUnicode = null;
                                    m.Beatmapset.Availability = null;
                                    m.Beatmapset.Beatmaps = null;
                                    m.Beatmapset.Converts = null;
                                    m.Beatmapset.UserId = null;
                                    m.Beatmapset.CurrentNominations = null;
                                    m.Beatmapset.Description = null;
                                    m.Beatmapset.Covers = null;
                                    m.Beatmapset.Genre = null;
                                    m.Beatmapset.Spotlight = null;
                                    m.Beatmapset.Hype = null;
                                    m.Beatmapset.FavouriteCount = null;
                                    m.Beatmapset.NominationsSummary = null;
                                    m.Beatmapset.PackTags = null;
                                    m.Beatmapset.PreviewUrl = null;
                                    m.Beatmapset.Source = null;
                                    m.Beatmapset.Title = null;
                                    m.Beatmapset.Video = null;
                                    m.Beatmapset.PlayCount = null;
                                    m.Beatmapset.Nsfw = null;
                                    m.Statistics = null;
                                    m.Preserve = null;
                                    m.BestId = null;
                                    m.BuildId = null;
                                    m.ClassicTotalScore = null;
                                    m.EndedAt = null;
                                    m.IsPerfectCombo = null;
                                    m.LegacyPerfect = null;
                                    m.LegacyScoreId = null;
                                    m.LegacyTotalScore = null;
                                    m.MaximumStatistics = null;
                                    m.Mode = null;
                                    m.ModeInt = null;
                                    m.Passed = null;
                                    m.PlaylistItemId = null;
                                    m.StartedAt = null;
                                    m.TotalScore = null;
                                    m.TotalScoreWithoutMods = null;
                                    m.Weight = null;
                                    m.Type = null;
                                    m.UserId = null;
                                    m.User = null;
                                    m.Processed = null;
                                    m.Ranked = null;
                                });

                                var functionOutput = JsonConvert.SerializeObject(scores, _jsonSerializerSettings);
                                inputItems.Add(new FunctionCallOutputResponseItem(functionCall.CallId, functionOutput));
                                break;
                            }
                        case FunctionNames.GetCountryRanking:
                            {
                                OpenAiFunctionCallParameters parameters = functionCall.FunctionArguments
                                    .ToObjectFromJson<OpenAiFunctionCallParameters>()!;
                                var count = parameters.Count!.Value;
                                var countryCode = parameters.CountryCode!;
                                var mode = parameters.Mode!.Value;

                                List<UserStatistics>? getUserBestResponse =
                                    await OsuApiHelper.GetUsersFromRanking(_osuApiV2, (Playmode)mode, countryCode, count);
                                var functionOutput = JsonConvert.SerializeObject(getUserBestResponse, Formatting.None,
                                    _jsonSerializerSettings);
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

                IEnumerable<MessageResponseItem> messageResponseItems = response.OutputItems.OfType<MessageResponseItem>();
                foreach (MessageResponseItem messageResponseItem in messageResponseItems)
                {
                    var currentOutputText = messageResponseItem.Content[0].Text;
                    output += currentOutputText;
                    if (_chatDictionary.TryGetValue(userTelegramId, out List<ResponseItem>? history))
                        history.Add(ResponseItem.CreateAssistantMessageItem(currentOutputText));
                }
            } while (requiresAction);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception while requesting to openai api");
            return new Result<string>(null, new Error(ErrorCode.Error), false);
        }

        return new Result<string>(output, null, true);
    }

    private void TrimMessageHistory(List<ResponseItem> items, int keepLast = 7)
    {
        if (items.Count <= keepLast) return;
        items.RemoveRange(1, items.Count - keepLast);
    }

    private static class FunctionNames
    {
        public const string GetOsuUser = "GetOsuUser";
        public const string GetUserScores = "GetUserScores";
        public const string GetCountryRanking = "GetCountryRanking";
    }

    public static class Model
    {
        public const string Gpt5 = "gpt-5";
        public const string Gpt5Mini = "gpt-5-mini";
        public const string Gpt5Nano = "gpt-5-nano";
    }
}
