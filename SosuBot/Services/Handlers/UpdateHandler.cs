using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Services.Handlers.Abstract;
using SosuBot.Services.Handlers.Callbacks;
using SosuBot.Services.Handlers.Commands;
using SosuBot.Services.Handlers.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using DummyCommand = SosuBot.Services.Handlers.Callbacks.DummyCommand;

// ReSharper disable ConvertTypeCheckPatternToNullCheck

namespace SosuBot.Services.Handlers;

public class UpdateHandler(
    BotContext database,
    IOptions<BotConfiguration> botConfig,
    ILogger<UpdateHandler> logger,
    IServiceProvider serviceProvider) : IUpdateHandler
{
    private Update? _currentUpdate;

    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        // log in console
        logger.LogError("HandleError: {Exception}", exception);

        // if a text-command message
        if (_currentUpdate!.Message is { Text: string } msg && msg.Text!.IsCommand())
        {
            var userId = (await database.OsuUsers.FirstAsync(u => u.IsAdmin, cancellationToken)).TelegramId;
            var errorText =
                $"Произошла ошибка.\n" +
                $"Пожалуйста, сообщите о ней <a href=\"tg://user?id={userId}\">создателю</a> (@Shoukkoo)";
            await msg.ReplyAsync(botClient, errorText);
        }
        // if a callback query
        else if (_currentUpdate!.CallbackQuery is { Data: string } callbackQuery)
        {
            await callbackQuery.AnswerAsync(botClient, "Произошла ошибка! Пожалуйста, сообщите о ней @Shoukkoo", true);
        }
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        _currentUpdate = update;
        cancellationToken.ThrowIfCancellationRequested();

        var eventHandler = update switch
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

        if (msg.Text == null)
        {
            if (msg.Caption != null)
            {
                msg.Text = msg.Caption;
                msg.Entities = msg.CaptionEntities;
            }
            else
            {
                return;
            }
        }
        if (msg.From is null) return;
        
        // msg.Text is guaranteed to be not null
        // Execute necessary functions
        if (msg.Text.IsCommand())
            await OnCommand(botClient, msg, cancellationToken);
        else
            await OnText(botClient, msg, cancellationToken);
    }

    private async Task OnCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is not { } data) return;

        var command = data.Split(" ")[1];
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
                executableCommand = new DummyCommand();
                break;
        }

        executableCommand.SetContext(
            new CommandContext<CallbackQuery>(
                botClient,
                callbackQuery,
                database,
                serviceProvider,
                cancellationToken));

        await executableCommand.ExecuteAsync();
        await callbackQuery.AnswerAsync(botClient);
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task OnCommand(ITelegramBotClient botClient, Message msg, CancellationToken cancellationToken)
    {
        var command = msg.Text!.GetCommand().RemoveUsernamePostfix(botConfig.Value.Username);
        CommandBase<Message> executableCommand;
        switch (command)
        {
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
            case string lastpassed when OsuLastPassedCommand.Commands.Contains(lastpassed):
                executableCommand = new OsuLastPassedCommand();
                break;
            case string user when OsuUserCommand.Commands.Contains(user):
                executableCommand = new OsuUserCommand();
                break;
            case string userId when OsuUserIdCommand.Commands.Contains(userId):
                executableCommand = new OsuUserIdCommand();
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
            case string c when CustomCommand.Commands.Contains(c):
                executableCommand = new CustomCommand();
                break;
            case string delete when DeleteCommand.Commands.Contains(delete):
                executableCommand = new DeleteCommand();
                break;
            case string get when GetDailyStatisticsCommand.Commands.Contains(get):
                executableCommand = new GetDailyStatisticsCommand();
                break;
            case string ranking when GetRankingCommand.Commands.Contains(ranking):
                executableCommand = new GetRankingCommand();
                break;
            case string render when ReplayRenderCommand.Commands.Contains(render):
                executableCommand = new ReplayRenderCommand();
                break;
            case string track when TrackCommand.Commands.Contains(track):
                executableCommand = new TrackCommand();
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
                serviceProvider,
                cancellationToken));

        await executableCommand.ExecuteAsync();
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task OnText(ITelegramBotClient botClient, Message msg, CancellationToken cancellationToken)
    {
        CommandBase<Message> textHandler = new TextHandler();
        textHandler.SetContext(
            new CommandContext<Message>(
                botClient,
                msg,
                database,
                serviceProvider,
                cancellationToken));

        await textHandler.ExecuteAsync();
        await database.SaveChangesAsync(cancellationToken);
    }

    private Task DoNothing()
    {
        return Task.CompletedTask;
    }
}