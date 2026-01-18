using SosuBot.Helpers.OutputText;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SosuBot.Extensions;

public static class TelegramUpdateExtensions
{
    public static Task<Message> ReplyAsync(this Message message, ITelegramBotClient botClient, string text,
        bool privateAsnwer = false, ParseMode parseMode = ParseMode.Html,
        InlineKeyboardMarkup? replyMarkup = null, string? splitValue = null, bool linkPreviewEnabled = false)
    {
        if (splitValue == null)
            return TelegramHelper.SendMessageConsideringTelegramLength(message.Id, message.Chat.Id, botClient, text,
                parseMode, replyMarkup, linkPreviewEnabled);

        return TelegramHelper.SendMessageConsideringTelegramLengthAndSplitValue(message.Id, message.Chat.Id, botClient,
            text,
            parseMode, replyMarkup, false, splitValue, linkPreviewEnabled);
    }

    public static Task<Message> ReplyPhotoAsync(this Message message, ITelegramBotClient botClient, InputFile photo,
        string? caption = null, bool privateAsnwer = false, ParseMode parseMode = ParseMode.Html,
        InlineKeyboardMarkup? replyMarkup = null)
    {
        return botClient.SendPhoto(privateAsnwer ? message.From!.Id : message.Chat.Id, photo, caption,
            parseMode, new ReplyParameters { MessageId = message.MessageId },
            replyMarkup);
    }

    public static Task<Message> ReplyDocumentAsync(this Message message, ITelegramBotClient botClient,
        InputFile document, string? caption = null, bool privateAsnwer = false,
        ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null)
    {
        return botClient.SendDocument(privateAsnwer ? message.From!.Id : message.Chat.Id, document, caption,
            parseMode, new ReplyParameters { MessageId = message.MessageId },
            replyMarkup);
    }

    public static Task<Message> EditAsync(this Message message, ITelegramBotClient botClient, string text,
        ParseMode parseMode = ParseMode.Html, InlineKeyboardMarkup? replyMarkup = null, string? splitValue = null, bool linkPreviewEnabled = false)
    {
        if (splitValue == null)
            return TelegramHelper.SendMessageConsideringTelegramLength(message.Id, message.Chat.Id, botClient, text,
                parseMode, replyMarkup, true, linkPreviewEnabled);

        return TelegramHelper.SendMessageConsideringTelegramLengthAndSplitValue(message.Id, message.Chat.Id, botClient,
            text,
            parseMode, replyMarkup, true, splitValue, linkPreviewEnabled);
    }

    public static Task AnswerAsync(this CallbackQuery callbackQuery, ITelegramBotClient botClient,
        string? text = null, bool showAlert = false)
    {
        return botClient.AnswerCallbackQuery(callbackQuery.Id, text, showAlert);
    }

    public static IEnumerable<string> GetAllLinks(this Message message)
    {
        if ((message.Text == null || message.Entities == null) && message.Caption == null) return [];
        message.Text ??= message.Caption ?? "";
        message.Entities ??= message.CaptionEntities ?? [];


        List<string> links = [];
        foreach (var me in message.Entities.Where(e =>
                     e.Type is MessageEntityType.Url or MessageEntityType.TextLink))
            links.Add(me.Url ?? message.Text.Substring(me.Offset, me.Length));

        return links;
    }

    public static async Task DownloadFileConsideringLocalServer(this ITelegramBotClient botClient, TGFile tgfile, Stream stream) 
    {
        if (botClient.LocalBotServer)
        {
            //tgfile.FilePath = string.Join('/', tgfile.FilePath!.Split('/')[4..]);
            Console.WriteLine();
            Console.WriteLine(tgfile.FilePath);
            using var fs = new FileStream(tgfile.FilePath!, FileMode.Open, FileAccess.Read);
            await fs.CopyToAsync(stream);
            return;
        }
        await botClient.DownloadFile(tgfile, stream);
    }
}