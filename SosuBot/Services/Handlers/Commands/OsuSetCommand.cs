﻿using Microsoft.EntityFrameworkCore;
using OsuApi.V2.Clients.Users.HttpIO;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands;

public class OsuSetCommand : CommandBase<Message>
{
    public static string[] Commands = ["/set"];

    public override async Task ExecuteAsync()
    {
        ILocalization language = new Russian();

        var msgText = Context.Update.Text!;
        var parameters = msgText.GetCommandParameters()!;
        var osuUsername = string.Join(' ', parameters);

        if (string.IsNullOrEmpty(osuUsername))
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_nameIsEmpty);
            return;
        }

        var osuUserInDatabase =
            await Context.Database.OsuUsers.FirstOrDefaultAsync(m => m.TelegramId == Context.Update.From!.Id);
        var response = await Context.OsuApiV2.Users.GetUser($"@{osuUsername}", new GetUserQueryParameters());
        if (response is null)
        {
            await Context.Update.ReplyAsync(Context.BotClient, language.error_userNotFound);
            return;
        }

        var user = response.UserExtend!;

        var playmode = user.Playmode!.ParseRulesetToPlaymode();
        if (osuUserInDatabase is null)
        {
            var newOsuUser = new OsuUser
            {
                TelegramId = Context.Update.From!.Id,
                OsuUsername = user.Username!,
                OsuUserId = user.Id.Value,
                OsuMode = playmode
            };
            newOsuUser.SetPP(user.Statistics!.Pp!.Value, playmode);

            await Context.Database.OsuUsers.AddAsync(newOsuUser);
            osuUserInDatabase = newOsuUser;
        }
        else
        {
            osuUserInDatabase.Update(user, playmode);
        }

        var sendText = language.command_set.Fill([
            $"{UserHelper.GetUserProfileUrlWrappedInUsernameString(user.Id.Value, user.Username!)}",
            $"{osuUserInDatabase.GetPP(playmode):N2}", osuUserInDatabase.OsuMode.ToGamemode()!
        ]);
        await Context.Update.ReplyAsync(Context.BotClient, sendText);
    }
}