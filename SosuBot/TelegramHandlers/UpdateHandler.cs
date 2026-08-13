using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SosuBot.Configuration;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Monitoring;
using SosuBot.Localization;
using SosuBot.Services;
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
    BotMetrics metrics,
    CommandUsageRecorder commandUsageRecorder,
    IServiceProvider serviceProvider,
    TelegramUserDirectory userDirectory) : IUpdateHandler
{
    public static Dictionary<string, Func<CommandBase<Message>>> Commands { get; set; } = new();
    public static Dictionary<string, string> CommandMetricNames { get; set; } = new();
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
        metrics.RecordUpdateReceived(update);
        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();

        try
        {
            await (update switch
            {
                { Message: { } message } => OnMessage(botClient, message, cancellationToken),
                { ChatMember: { } chatMember } => OnChatMemberUpdated(chatMember, cancellationToken),
                { CallbackQuery: { } callbackQuery } => OnCallbackQuery(botClient, callbackQuery, cancellationToken),
                _ => DoNothing()
            });
            metrics.RecordUpdateProcessed(update, "success", System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            metrics.RecordUpdateProcessed(update, "cancelled", System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
            throw;
        }
        catch (Telegram.Bot.Exceptions.ApiRequestException exception) when (IsMessageNotModified(exception))
        {
            metrics.RecordUpdateProcessed(update, "ignored", System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
            logger.LogDebug("Ignored an unchanged Telegram message response");
        }
        catch (Exception e)
        {
            metrics.RecordUpdateProcessed(update, "error", System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalSeconds);
            await HandleErrorForUpdateAsync(botClient, update, e, HandleErrorSource.HandleUpdateError, cancellationToken);
        }
    }

    private async Task HandleErrorForUpdateAsync(ITelegramBotClient botClient, Update update, Exception exception,
        HandleErrorSource source, CancellationToken cancellationToken)
    {
        // Telegram Bot API error 400: Bad Request: message is not modified: specified new message content and reply markup are exactly the same as a current content and reply markup of the message
        if (exception is Telegram.Bot.Exceptions.ApiRequestException apiEx && IsMessageNotModified(apiEx))
        {
            logger.LogDebug("Ignored an unchanged Telegram message response (source: {Source})", source);
            return;
        }

        logger.LogError(exception, "Failed to handle Telegram update (source: {Source})", source);

        if (OsuApiAvailabilityHelper.IsUnavailable(exception))
        {
            ILocalization language = database.GetLocalization(
                update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id);
            if (update.Message is { } osuMessage)
            {
                await osuMessage.ReplyAsync(botClient, language.error_osuServerUnavailable);
            }
            else if (update.CallbackQuery is { } osuCallback)
            {
                await osuCallback.AnswerAsync(
                    botClient,
                    language.error_osuServerUnavailable,
                    showAlert: true);
            }

            return;
        }

        // if a text-command message
        if (update.Message is { Text: string } msg && msg.Text.IsCommand())
        {
            OsuUser? admin = await database.OsuUsers.FirstOrDefaultAsync(u => u.IsAdmin, cancellationToken);
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
        userDirectory.Remember(msg.From);
        userDirectory.Remember(msg.ReplyToMessage?.From);
        foreach (User newMember in msg.NewChatMembers ?? [])
            userDirectory.Remember(newMember);

        // Add new chat and update chat members
        await database.AddOrUpdateTelegramChat(msg, logger, cancellationToken);

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

    private Task OnChatMemberUpdated(ChatMemberUpdated chatMember, CancellationToken cancellationToken)
    {
        userDirectory.Remember(chatMember.NewChatMember.User);
        return database.AddOrUpdateTelegramChat(chatMember, logger, cancellationToken);
    }

    private async Task OnCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is not { } data) return;

        var command = data.Split(" ")[0];
        Func<CommandBase<CallbackQuery>> callbackFactory = Callbacks.GetValueOrDefault(command, () => new DummyCallback());
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
        bool isKnownCommand = Commands.TryGetValue(command, out Func<CommandBase<Message>>? commandFactory);
        commandFactory ??= () => new DummyCommand();
        CommandBase<Message> executableCommand = commandFactory();
        string recordedCommand = isKnownCommand
            ? CommandMetricNames.GetValueOrDefault(command, command)
            : "unknown";
        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        string status = "success";

        executableCommand.SetContext(
            new CommandContext<Message>(
                botClient,
                msg,
                serviceProvider,
                cancellationToken));

        try
        {
            await executableCommand.BeforeExecuteAsync();
            await executableCommand.ExecuteAsync();
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = "cancelled";
            throw;
        }
        catch (Telegram.Bot.Exceptions.ApiRequestException exception) when (IsMessageNotModified(exception))
        {
            status = "success";
            throw;
        }
        catch
        {
            status = "error";
            throw;
        }
        finally
        {
            await commandUsageRecorder.RecordAsync(
                recordedCommand,
                status,
                System.Diagnostics.Stopwatch.GetElapsedTime(startedAt),
                cancellationToken);
        }
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

    private static bool IsMessageNotModified(Telegram.Bot.Exceptions.ApiRequestException exception) =>
        exception.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase);
}

