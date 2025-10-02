using Newtonsoft.Json;
using OpenAI.Responses;
using OsuApi.V2;
using OsuApi.V2.Clients.Users.HttpIO;

#pragma warning disable OPENAI001

namespace SosuBot.Services.Data;

public sealed class OpenAIService
{
    private ApiV2 _osuApiV2;
    private OpenAIResponseClient  _responseClient;
    private string _openaiToken = Environment.GetEnvironmentVariable("OpenAIToken")!;
    private string _model = "gpt-5-nano";
    
    public OpenAIService(ApiV2 osuApiV2)
    {
        _osuApiV2 = osuApiV2;
        _responseClient = new OpenAIResponseClient(_model, _openaiToken);
    }

    private FunctionTool _getOsuUserTool = ResponseTool.CreateFunctionTool(
        functionName: FunctionNames.GetOsuUser,
        functionDescription: "Gets osu! user profile as json",
        functionParameters: BinaryData.FromBytes(
            """
                {
                  "type": "object",
                  "properties": {
                      "user_id": {
                          "oneOf": [
                              { "type": "string" },
                              { "type": "integer" }
                            ],
                          "description": "Username (optionally prefixed with '@') or numeric user id of an osu! player"
                      }
                  },
                  "required": [ "user_id" ],
                  "additionalProperties": false,
                  "examples": [
                           { "user_id": 15319810 },
                           { "user_id": "@shoukko" },
                           { "user_id": "shoukko" }
                  ]
                }
                """u8.ToArray()),
        strictModeEnabled: false
    );

    public async Task<string> GetResponseAsync(string userInput)
    {
        List<ResponseItem> inputItems =
        [
            ResponseItem.CreateSystemMessageItem(_systemPrompt),
            ResponseItem.CreateDeveloperMessageItem(_developerPrompt),
            ResponseItem.CreateUserMessageItem(userInput)
        ];

        ResponseCreationOptions options = new()
        {
            Tools = { _getOsuUserTool }
        };

        string output = "";
        bool requiresAction;
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
                        string userId = functionCall.FunctionArguments.ToObjectFromJson<Dictionary<string, string>>()!["user_id"];
                        GetUserResponse? getUserResponse = await _osuApiV2.Users.GetUser(userId, new());
                        string functionOutput = JsonConvert.SerializeObject(getUserResponse?.UserExtend);
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

            output += string.Join("\n", response.OutputItems.OfType<MessageResponseItem>().Select(m => m.Content[0].Text));
        } while (requiresAction);

        return output;
    }

    private string _systemPrompt = "Ты - ShkX. Нейросеть, созданная студентом Shoukko. Неформальный стиль общения разрешен. Давай ответы коротко и ясно.";
    private string _developerPrompt = "Ты — ShkX, ассистент для игроков osu!. Используй термины игры (AR, OD, HP, mods, pp). Формат ответов: будь краток, неформальный стиль разрешен. Не используй мат. Не задавай вопросы. Для osu!supporter используй слово \"саппортер\"\n";

    public static class FunctionNames
    {
        public const string GetOsuUser = "GetOsuUser";
    }
}