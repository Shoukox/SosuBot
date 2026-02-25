using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SosuBot.Configuration;
using SosuBot.Database;
using SosuBot.Extensions;
using SosuBot.TelegramHandlers.Abstract;
using SosuBot.TelegramHandlers.Commands;
using SosuBot.TelegramHandlers.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using DummyCallback = SosuBot.TelegramHandlers.Callbacks.DummyCallback;

// ReSharper disable ConvertTypeCheckPatternToNullCheck

namespace SosuBot.TelegramHandlers;

public class UpdateHandler(
    BotContext database,
    IOptions<BotConfiguration> botConfig,
    ILogger<UpdateHandler> logger,
    IServiceProvider serviceProvider) : IUpdateHandler
{
    public static Dictionary<string, Func<CommandBase<Message>>> Commands { get; set; } = new();
    public static Dictionary<string, Func<CommandBase<CallbackQuery>>> Callbacks { get; set; } = new();

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "HandleError (source: {Source})", source);
        return Task.CompletedTask;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
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
            await HandleErrorForUpdateAsync(botClient, update, e, HandleErrorSource.HandleUpdateError, cancellationToken);
        }
    }

    private async Task HandleErrorForUpdateAsync(ITelegramBotClient botClient, Update update, Exception exception,
        HandleErrorSource source, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "HandleError (source: {Source})", source);

        // if a text-command message
        if (update.Message is { Text: string } msg && msg.Text.IsCommand())
        {
            var admin = await database.OsuUsers.FirstOrDefaultAsync(u => u.IsAdmin, cancellationToken);
            if (admin is null) return;

            var errorText =
                $"Произошла ошибка.\n" +
                $"Пожалуйста, сообщите о ней <a href=\"tg://user?id={admin.TelegramId}\">создателю</a> (@Shoukkoo)";
            await msg.ReplyAsync(botClient, errorText);
        }
        // if a callback query
        else if (update.CallbackQuery is { Data: string } callbackQuery)
        {
            await callbackQuery.AnswerAsync(botClient);
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

        var command = data.Split(" ")[0];
        var callbackFactory = Callbacks.GetValueOrDefault(command, () => new DummyCallback());
        CommandBase<CallbackQuery> executableCommand = callbackFactory();

        executableCommand.SetContext(
            new CommandContext<CallbackQuery>(
                botClient,
                callbackQuery,
                serviceProvider,
                cancellationToken));

        await executableCommand.BeforeExecuteAsync();
        await executableCommand.ExecuteAsync();
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task OnCommand(ITelegramBotClient botClient, Message msg, CancellationToken cancellationToken)
    {
        var command = msg.Text!.GetCommand().RemoveUsernamePostfix(botConfig.Value.Username);
        var commandFactory = Commands.GetValueOrDefault(command, () => new DummyCommand());
        CommandBase<Message> executableCommand = commandFactory();

        executableCommand.SetContext(
            new CommandContext<Message>(
                botClient,
                msg,
                serviceProvider,
                cancellationToken));

        await executableCommand.BeforeExecuteAsync();
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
                serviceProvider,
                cancellationToken));

        await textHandler.BeforeExecuteAsync();
        await textHandler.ExecuteAsync();
        await database.SaveChangesAsync(cancellationToken);
    }

    private Task DoNothing()
    {
        return Task.CompletedTask;
    }
}

