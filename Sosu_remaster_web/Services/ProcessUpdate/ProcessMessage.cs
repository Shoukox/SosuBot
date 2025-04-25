using Sosu.Services.ProcessUpdate.MessageCommands;
using Sosu.Services.ProcessUpdate.MessageCommands.AdminCommands;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate
{
    public class ProcessMessage : IProcessUpdate
    {
        public async Task OnReceived(ITelegramBotClient bot, Update update)
        {
            Message? message = update.Message;

            if (message?.Text == null)
                return;

            CheckForNewDataInUpdate.Check(message);

            Console.WriteLine($"{message.From.Id} @{message.From.Username}: {message.Text}");

            string[] splittedMessageText = message.Text.Split(" ");
            string command = splittedMessageText[0];

            command = command.Replace($"@{Variables.bot.Username}", "");

            ICommand handler = command switch
            {
                //~~~~~~admin~~~~~~~~
                SendMessageCommand.commandText => new SendMessageCommand(),
                DeleteCommand.commandText => new DeleteCommand(),
                GetCommand.commandText => new GetCommand(),
                ForceSaveCommand.commandText => new ForceSaveCommand(),
                //~~~~~~~~~~~~~~~~~~~

                "/start" => new StartCommand(),
                "/help" => new HelpCommand(),
                "/set" => new OsuSetCommand(),
                "/userbest" => new OsuUserbestCommand(),
                "/chat_stats" => new OsuChatstatsCommand(),
                "/remove" => new OsuDeletePlayersFromChatstats(),
                "/compare" => new OsuCompareCommand(),

                "/last" => new OsuLastCommand(),
                "/l" => new OsuLastCommand(),

                "/user" => new OsuUserCommand(),
                "/u" => new OsuUserCommand(),

                "/score" => new OsuScoreCommand(),
                "/s" => new OsuScoreCommand(),

                "/msg" => new MsgCommand(),

                _ => new OtherCommand()
            };

            await handler.action(bot, update);
        }
    }
}
