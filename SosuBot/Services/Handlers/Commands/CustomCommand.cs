using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using osu.Game.Overlays.Settings.Sections.Gameplay;
using osu.Game.Rulesets.Osu.Mods;
using OsuApi.V2;
using OsuApi.V2.Models;
using OsuApi.V2.Users.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers.Commands;

public sealed class CustomCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/c"];
    private OpenAiService _openaiService = null!;
    private ApiV2 _osuApiV2 = null!;

    public override Task BeforeExecuteAsync()
    {
        _openaiService = Context.ServiceProvider.GetRequiredService<OpenAiService>();
        _osuApiV2 = Context.ServiceProvider.GetRequiredService<ApiV2>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await BeforeExecuteAsync();
        
        var osuUserInDatabase = await Context.Database.OsuUsers.FindAsync(Context.Update.From!.Id);
        if (osuUserInDatabase is null || !osuUserInDatabase.IsAdmin)
        {
            await Context.Update.ReplyAsync(Context.BotClient, "Пшол вон!");
            return;
        }

        var parameters = Context.Update.Text!.GetCommandParameters()!;
        if (parameters[0] == "json")
        {
            var result = JsonConvert.SerializeObject(Context.Update,
                Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            if (parameters.Length >= 2 && parameters[1] == "text")
                await Context.Update.ReplyAsync(Context.BotClient, result);
            else
                await Context.Update.ReplyDocumentAsync(Context.BotClient, TextHelper.TextToStream(result));
        }
        else if (parameters[0] == "test")
        {
            await Context.Update.ReplyAsync(Context.BotClient, new string('a', (int)Math.Pow(2, 14)));
        }
        else if (parameters[0] == "getuser")
        {
            var osuUserInReply = await Context.Database.OsuUsers.FindAsync(Context.Update.ReplyToMessage!.From!.Id);

            var result = JsonConvert.SerializeObject(osuUserInReply,
                Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            await Context.Update.ReplyAsync(Context.BotClient, result);
        }
        else if (parameters[0] == "countryflag")
        {
            await Context.Update.ReplyAsync(Context.BotClient, UserHelper.CountryCodeToFlag(parameters[1]));
        }
        else if (parameters[0] == "ai")
        {
            ILocalization language = new Russian();
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            string userInput = string.Join(" ", parameters[1..]);
            Result<string> output = await _openaiService.GetResponseAsync(userInput, Context.Update.From.Id);
            if (!output.IsSuccess || string.IsNullOrEmpty(output.Data))
            {
                switch (output.Exception?.Code)
                {
                    case ErrorCode.Locked:
                    {
                        await waitMessage.EditAsync(Context.BotClient, "Подожди обработки предыдущего запроса!");
                        return;
                    }
                }
                await waitMessage.EditAsync(Context.BotClient, language.error_baseMessage);
                return;
            }

            try
            {
                await waitMessage.EditAsync(Context.BotClient, output.Data!, ParseMode.Markdown);
            }
            catch
            {
                await waitMessage.EditAsync(Context.BotClient, output.Data!, ParseMode.None);
            }
        }
        else if (parameters[0] == "slavik")
        {
            
            ILocalization language = new Russian();
            var waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            int countPlayersFromRanking = 100;
            int countBestScoresPerPlayer = 200;
            
            var uzOsuStdUsers = await OsuApiHelper.GetUsersFromRanking(_osuApiV2, count: countPlayersFromRanking);
           
            var getBestScoresTask = uzOsuStdUsers!.Select(m =>
                _osuApiV2.Users.GetUserScores(m.User!.Id!.Value, ScoreType.Best,
                    new() { Limit = countBestScoresPerPlayer })).ToArray();
            await Task.WhenAll(getBestScoresTask);
            
            Score[] uzBestScores = getBestScoresTask.SelectMany(m => m.Result!.Scores).ToArray();
            
            var bestScoresByMods = uzBestScores.GroupBy(m => string.Join("", ScoreHelper.GetModsText(m.Mods!.Where(mod => !mod.Acronym!.Equals("CL", StringComparison.InvariantCultureIgnoreCase)).ToArray()))).Select(m => (m.Key, m.MaxBy(s => s.Pp)!)).OrderByDescending(m => m.Item2.Pp).ToArray();

            string sendText = "";
            foreach (var pair in bestScoresByMods)
            {
                string lazer =
                    pair.Item2.Mods!.Any(m => m.Acronym!.Equals("CL", StringComparison.InvariantCultureIgnoreCase))
                        ? ""
                        : "lazer";
                sendText +=
                    $"{pair.Key} - max. {ScoreHelper.GetScoreUrlWrappedInString(pair.Item2.Id!.Value, $"{pair.Item2.Pp:N2}pp")}{lazer} by {UserHelper.GetUserProfileUrlWrappedInUsernameString(pair.Item2.UserId!.Value, pair.Item2.User!.Username!)}\n";
            }

            await waitMessage.EditAsync(Context.BotClient, sendText, splitValue: "\n");
        }
    }
}