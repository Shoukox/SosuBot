using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Windows.Input;
using SosuBot.Extensions;
using SosuBot.Services.Handlers.MessageCommands;
using SosuBot.Database;
using OsuApi.Core.V2;
using System.Data.Common;

namespace SosuBot.Services.Handlers;

public class UpdateHandler(ApiV2 osuApi, BotContext database, ILogger<UpdateHandler> logger) : IUpdateHandler
{
    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        logger.LogInformation("HandleError: {Exception}", exception);
        return Task.CompletedTask;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task eventHandler = update switch
        {
            { Message: { } message } => OnMessage(botClient, message),
            { CallbackQuery: { } callbackQuery } => OnCallbackQuery(botClient, callbackQuery),
            _ => DoNothing()
        };

        try
        {
            await eventHandler;
        }
        catch (Exception e)
        {
            await HandleErrorAsync(botClient, e, HandleErrorSource.HandleUpdateError, cancellationToken);
        }
    }

    private async Task OnMessage(ITelegramBotClient botClient, Message msg)
    {
        if (msg.Text is not { Length: > 0 }) return;

        if (msg.Chat is not null
            && msg.From is not null) await database.AddOrUpdateTelegramChat(msg);
        else return;

        if (msg.Text[0] == '/')
        {

            await OnCommand(botClient, msg);
        }
        else
        {
            await OnText(botClient, msg);
        }
    }

    private async Task OnCommand(ITelegramBotClient botClient, Message msg)
    {
        string command = msg.Text!.GetCommand()!;
        CommandBase<Message> executableCommand;
        switch(command) 
        {
            //~~~~~~admin~~~~~~~~
            //"/sendm" => new SendMessageCommand(),
            //DeleteCommand.commandText => new DeleteCommand(),
            //GetCommand.commandText => new GetCommand(),
            //ForceSaveCommand.commandText => new ForceSaveCommand(),
            //~~~~~~~~~~~~~~~~~~~

            case string cmd when StartCommand.Commands.Contains(cmd):
                executableCommand = new StartCommand();
                break;
            case string cmd when HelpCommand.Commands.Contains(cmd):
                executableCommand = new HelpCommand();
                break;
            case string cmd when OsuSetCommand.Commands.Contains(cmd):
                executableCommand = new OsuSetCommand();
                break;
            case string cmd when OsuSetModeCommand.Commands.Contains(cmd):
                executableCommand = new OsuSetModeCommand();
                break;
            case string cmd when OsuUserbestCommand.Commands.Contains(cmd):
                executableCommand = new OsuUserbestCommand();
                break;
            case string cmd when OsuChatstatsCommand.Commands.Contains(cmd):
                executableCommand = new OsuChatstatsCommand();
                break;
            case string cmd when OsuChatstatsExcludeCommand.Commands.Contains(cmd):
                executableCommand = new OsuChatstatsExcludeCommand();
                break;
            case string cmd when OsuChatstatsIncludeCommand.Commands.Contains(cmd):
                executableCommand = new OsuChatstatsIncludeCommand();
                break;
            case string cmd when OsuCompareCommand.Commands.Contains(cmd):
                executableCommand = new OsuCompareCommand();
                break;
            case string cmd when OsuLastCommand.Commands.Contains(cmd):
                executableCommand = new OsuLastCommand();
                break;
            default:
                executableCommand = new HelpCommand();
                break;

            //"/last" => new OsuLastCommand(),
            //"/l" => new OsuLastCommand(),

            //"/user" => new OsuUserCommand(),
            //"/u" => new OsuUserCommand(),

            //"/score" => new OsuScoreCommand(),
            //"/s" => new OsuScoreCommand(),

            //"/msg" => new MsgCommand(),
        };
        executableCommand.SetContext(msg);
        executableCommand.SetDatabase(database);
        executableCommand.SetBotClient(botClient);
        executableCommand.SetOsuApiV2(osuApi);

        await executableCommand.ExecuteAsync();
        await database.SaveChangesAsync();
    }

    private async Task OnText(ITelegramBotClient botClient, Message msg)
    {

    }

    private Task OnCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        logger.LogInformation("Received inline keyboard callback from: {CallbackQueryId}", callbackQuery.Id);
        return Task.CompletedTask;
    }

    private Task DoNothing() => Task.CompletedTask;
}
