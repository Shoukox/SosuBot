using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OsuApi.Core.V2;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Services.Handlers.Abstract;
using SosuBot.Services.Handlers.Callbacks;
using SosuBot.Services.Handlers.Commands;
using SosuBot.Services.Handlers.Text;
using SosuBot.Synchonization.MessageSpamResistance;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.Services.Handlers;

public class UpdateHandler(
    ApiV2 osuApi,
    BotContext database,
    ILogger<UpdateHandler> logger,
    IOptions<BotConfiguration> botConfig,
    IServiceProvider serviceProvider) : IUpdateHandler
{
    private Update? _currentUpdate;

    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        // if a text-command message
        if (_currentUpdate!.Message is { Text: string } msg && msg.Text!.IsCommand())
        {
            long userId = (await database.OsuUsers.FirstAsync(u => u.IsAdmin)).TelegramId;
            string errorText =
                $"Произошла ошибка.\n" +
                $"Пожалуйста, сообщите о ней <a href=\"tg://user?id={userId}\">создателю</a> (@Shoukkoo)";
            await msg.ReplyAsync(botClient, errorText);
        }
        // if a callback query
        else if (_currentUpdate!.CallbackQuery is { Data: string } callbackQuery)
        {
            await callbackQuery.AnswerAsync(botClient, "Произошла ошибка! Пожалуйста, сообщите о ней @Shoukkoo", true);
        }

        // log in console
        logger.LogError("HandleError: {Exception}", exception);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        _currentUpdate = update;
        cancellationToken.ThrowIfCancellationRequested();

        Task eventHandler = update switch
        {
            { Message: { } message } => OnMessage(botClient, message, cancellationToken),
            { CallbackQuery: { } callbackQuery } => OnCallbackQuery(botClient, callbackQuery, cancellationToken),
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

    private async Task OnMessage(ITelegramBotClient botClient, Message msg, CancellationToken cancellationToken)
    {
        // Add new chat and update chat members
        await database.AddOrUpdateTelegramChat(msg);

        if (msg.Text is not { } text) return;
        if (msg.Chat is null || msg.From is null) return;

        // SpamResistance
        var (canSend, sendWarning) = await SpamResistance.Instance.CanSendMessage(msg.From.Id, msg.Date);
        if (!canSend)
        {
            if (sendWarning)
            {
                await Task.Delay(1000);
                await botClient.SendMessage(msg.Chat.Id, $"Не спамь!\nТы заблокирован на {SpamResistance.BlockInterval.TotalSeconds} сек.", ParseMode.Html, msg.MessageId, linkPreviewOptions: false);
            }
            return;
        }

        // Execute necessary functions
        if (text.IsCommand())
        {
            await OnCommand(botClient, msg, cancellationToken);
        }
        else
        {
            await OnText(botClient, msg, cancellationToken);
        }
    }

    private async Task OnCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is not { } data) return;

        string command = data.Split(" ")[1];
        CommandBase<CallbackQuery> executableCommand;
        switch (command)
        {
            case string user when OsuUserCallbackCommand.Command.Equals(user):
                executableCommand = new OsuUserCallbackCommand();
                break;
            case string userbest when OsuUserBestCallbackCommand.Command.Equals(userbest):
                executableCommand = new OsuUserBestCallbackCommand();
                break;
            case string songPreview when OsuSongPreviewCallbackCommand.Command.Equals(songPreview):
                executableCommand = new OsuSongPreviewCallbackCommand();
                break;
            default:
                executableCommand = new Callbacks.DummyCommand();
                break;
        }
        executableCommand.SetContext(
            new CommandContext<CallbackQuery>(
                botClient,
                callbackQuery,
                database,
                osuApi,
                serviceProvider.GetRequiredService<ILogger<ICommandContext<CallbackQuery>>>(),
                cancellationToken));

        await executableCommand.ExecuteAsync();
        await callbackQuery.AnswerAsync(botClient);
        await database.SaveChangesAsync();
    }

    private async Task OnCommand(ITelegramBotClient botClient, Message msg, CancellationToken cancellationToken)
    {
        string command = msg.Text!.GetCommand().RemoveUsernamePostfix(botConfig.Value.Username);
        CommandBase<Message> executableCommand;
        switch (command)
        {
            //~~~~~~admin~~~~~~~~
            //"/sendm" => new SendMessageCommand(),
            //DeleteCommand.commandText => new DeleteCommand(),
            //GetCommand.commandText => new GetCommand(),
            //ForceSaveCommand.commandText => new ForceSaveCommand(),
            //~~~~~~~~~~~~~~~~~~~

            case string start when StartCommand.Commands.Contains(start):
                executableCommand = new StartCommand();
                break;
            case string help when HelpCommand.Commands.Contains(help):
                executableCommand = new HelpCommand();
                break;
            case string set when OsuSetCommand.Commands.Contains(set):
                executableCommand = new OsuSetCommand();
                break;
            case string mode when OsuModeCommand.Commands.Contains(mode):
                executableCommand = new OsuModeCommand();
                break;
            case string userbest when OsuUserbestCommand.Commands.Contains(userbest):
                executableCommand = new OsuUserbestCommand();
                break;
            case string chatstats when OsuChatstatsCommand.Commands.Contains(chatstats):
                executableCommand = new OsuChatstatsCommand();
                break;
            case string exclude when OsuChatstatsExcludeCommand.Commands.Contains(exclude):
                executableCommand = new OsuChatstatsExcludeCommand();
                break;
            case string include when OsuChatstatsIncludeCommand.Commands.Contains(include):
                executableCommand = new OsuChatstatsIncludeCommand();
                break;
            case string compare when OsuCompareCommand.Commands.Contains(compare):
                executableCommand = new OsuCompareCommand();
                break;
            case string last when OsuLastCommand.Commands.Contains(last):
                executableCommand = new OsuLastCommand();
                break;
            case string user when OsuUserCommand.Commands.Contains(user):
                executableCommand = new OsuUserCommand();
                break;
            case string score when OsuScoreCommand.Commands.Contains(score):
                executableCommand = new OsuScoreCommand();
                break;
            case string sendMsg when MsgCommand.Commands.Contains(sendMsg):
                executableCommand = new MsgCommand();
                break;
            case string db when DbCommand.Commands.Contains(db):
                executableCommand = new DbCommand();
                break;
            case string delete when DeleteCommand.Commands.Contains(delete):
                executableCommand = new DeleteCommand();
                break;
            default:
                executableCommand = new Commands.DummyCommand();
                break;
        }
        executableCommand.SetContext(
            new CommandContext<Message>(
                botClient,
                msg,
                database,
                osuApi,
                serviceProvider.GetRequiredService<ILogger<ICommandContext<Message>>>(),
                cancellationToken));

        await executableCommand.ExecuteAsync();
        await database.SaveChangesAsync();
    }

    private async Task OnText(ITelegramBotClient botClient, Message msg, CancellationToken cancellationToken)
    {
        CommandBase<Message> textHandler = new TextHandler();
        textHandler.SetContext(
            new CommandContext<Message>(
                botClient,
                msg,
                database,
                osuApi,
                serviceProvider.GetRequiredService<ILogger<ICommandContext<Message>>>(),
                cancellationToken));

        await textHandler.ExecuteAsync();
        await database.SaveChangesAsync();
    }

    private Task DoNothing() => Task.CompletedTask;
}
