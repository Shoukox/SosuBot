using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.Services.Handlers.Abstract;
using SosuBot.Services.Handlers.Commands;
using SosuBot.Services.Handlers.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using DummyCallback = SosuBot.Services.Handlers.Callbacks.DummyCallback;

// ReSharper disable ConvertTypeCheckPatternToNullCheck

namespace SosuBot.Services.Handlers;

public class UpdateHandler(
    BotContext database,
    IOptions<BotConfiguration> botConfig,
    ILogger<UpdateHandler> logger,
    IServiceProvider serviceProvider,
    HybridCache cache) : IUpdateHandler
{
    public static Dictionary<string, CommandBase<Message>> Commands { get; set; } = new();
    public static Dictionary<string, CommandBase<CallbackQuery>> Callbacks { get; set; } = new();

    private Update? _currentUpdate;

    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source,
        CancellationToken cancellationToken)
    {
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
            await callbackQuery.AnswerAsync(botClient);
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
        CommandBase<CallbackQuery> executableCommand = Callbacks.GetValueOrDefault(command, new DummyCallback());

        executableCommand.SetContext(
            new CommandContext<CallbackQuery>(
                botClient,
                callbackQuery,
                database,
                serviceProvider,
                cache,
                cancellationToken));

        await executableCommand.ExecuteAsync();
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task OnCommand(ITelegramBotClient botClient, Message msg, CancellationToken cancellationToken)
    {
        var command = msg.Text!.GetCommand().RemoveUsernamePostfix(botConfig.Value.Username);
        CommandBase<Message> executableCommand = Commands.GetValueOrDefault(command, new DummyCommand());

        executableCommand.SetContext(
            new CommandContext<Message>(
                botClient,
                msg,
                database,
                serviceProvider,
                cache,
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
                cache,
                cancellationToken));

        await textHandler.ExecuteAsync();
        await database.SaveChangesAsync(cancellationToken);
    }

    private Task DoNothing()
    {
        return Task.CompletedTask;
    }
}