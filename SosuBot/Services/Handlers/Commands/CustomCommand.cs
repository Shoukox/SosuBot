using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Types;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Data;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers.Commands;

public sealed class CustomCommand : CommandBase<Message>
{
    public static string[] Commands = ["/c"];
    private OpenAiService _openaiService = null!;

    public override Task BeforeExecuteAsync()
    {
        _openaiService = Context.ServiceProvider.GetRequiredService<OpenAiService>();
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync()
    {
        await base.ExecuteAsync();
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
            if (!output.IsSuccess)
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
            
            await waitMessage.EditAsync(Context.BotClient, output.Data!, ParseMode.Markdown);
        }
    }
}