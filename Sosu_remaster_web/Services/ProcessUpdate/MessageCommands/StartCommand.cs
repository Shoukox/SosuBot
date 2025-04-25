using Sosu.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate.MessageCommands
{
    public class StartCommand : ICommand
    {
        //public string pattern
        //{
        //    get => "/start";
        //}
        public Func<ITelegramBotClient, Update, Task> action => new Func<ITelegramBotClient, Update, Task>(async (bot, update) =>
            {
                Message message = update.Message;
                var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
                ILocalization language = Localization.Localization.Methods.GetLang(chat.language);

                var beatmap = await Variables.osuApi.GetBeatmapByBeatmapIdAsync(970048);
                var lastScore = (await Variables.osuApi.GetRecentScoresByNameAsync("Shoukko")).First();

                //var perfCalc = new OsuPerformanceCalculator();
                //var stats = new Statistics()
                //{
                //    Hit300 = int.Parse(lastScore.count300),
                //    Hit100 = int.Parse(lastScore.count100),
                //    Hit50 = int.Parse(lastScore.count50),
                //    Miss = int.Parse(lastScore.countmiss),
                //    Accuracy = lastScore.accuracy(),
                //    MaxCombo = int.Parse(lastScore.maxcombo),
                //    Mods = Variables.osuApi.CalculateModsMods(int.Parse(lastScore.enabled_mods))
                //};

                //var difficultyAttributes = new DifficultyAttributes()
                //{
                //    MaxCombo = int.Parse(beatmap.max_combo),
                //};
                //try
                //{
                //    var a = perfCalc.CreatePerformanceAttributes(stats, difficultyAttributes);
                //    var a1 = 1;
                //}
                //catch (Exception ex)
                //{
                //    var a = 1;
                //}
                string text = language.command_start;
                await bot.SendTextMessageAsync(message.Chat.Id, text);
            });

        public async Task nachAction(ITelegramBotClient bot, Message message)
        {

        }
    }
}
