using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using OsuApi.V2.Users.Models;
using SosuBot.DanserWrapper;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Helpers.Scoring;
using SosuBot.Helpers.Types.Statistics;
using SosuBot.Localization;
using SosuBot.Localization.Languages;
using SosuBot.Services.BackgroundServices;
using SosuBot.Services.Handlers.Abstract;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers.Commands
{
    public class ReplayRenderCommand : CommandBase<Message>
    {
        public static string[] Commands = ["/render"];

        public override async Task ExecuteAsync()
        {
            if (await Context.Update.IsUserSpamming(Context.BotClient))
                return;

            if (Context.Update.ReplyToMessage?.Document == null || Context.Update.ReplyToMessage?.Document.FileName![^4..] != ".osr")
                return;

            ILocalization language = new Russian();
            Message waitMessage = await Context.Update.ReplyAsync(Context.BotClient, language.waiting);

            string replayPath = Path.GetTempFileName();
            var tgfile = await Context.BotClient.GetFile(Context.Update.ReplyToMessage.Document.FileId);
            
            StreamWriter sw = new StreamWriter(replayPath);
            await Context.BotClient.DownloadFile(tgfile, sw.BaseStream);
            sw.Close();

            var danser = new DanserGo();
            var result = await danser.ExecuteAsync($"-r {replayPath} -out {Context.Update.From!.Id}{Path.GetFileNameWithoutExtension(replayPath)}");

            var match = Regex.Match(result.Output, "Video is available at: (\\S+)");
            string fileName = match.Groups[1].Value;
            string sendText = $"{match.Value}\n\n" +
                              "http://[2a03:4000:6:417a:1::105]/" + fileName;
            await waitMessage.EditAsync(Context.BotClient, sendText);
        }
    }
}