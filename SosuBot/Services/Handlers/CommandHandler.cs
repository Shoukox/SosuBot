using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SosuBot.Services.Handlers
{
    internal class CommandHandler : IRequestHandler<Message>
    {
        public async Task OnReceived(ITelegramBotClient bot, Message message)
        {
            if (message.Text == null)
                return;

            //CheckForNewDataInUpdate.Check(message);

            //Console.WriteLine($"{message.From.Id} @{message.From.Username}: {message.Text}");

            //string[] splittedMessageText = message.Text.Split(" ");
            //string command = splittedMessageText[0];

            //command = command.Replace($"@{Variables.bot.Username}", "");

            //ICommand handler = command switch
            //{
            //    //~~~~~~admin~~~~~~~~
            //    SendMessageCommand.commandText => new SendMessageCommand(),
            //    DeleteCommand.commandText => new DeleteCommand(),
            //    GetCommand.commandText => new GetCommand(),
            //    //~~~~~~~~~~~~~~~~~~~

            //    "/start" => new StartCommand(),
            //    "/help" => new HelpCommand(),
            //    "/set" => new OsuSetCommand(),
            //    "/userbest" => new OsuUserbestCommand(),
            //    "/chat_stats" => new OsuChatstatsCommand(),
            //    "/compare" => new OsuCompareCommand(),

            //    "/last" => new OsuLastCommand(),
            //    "/l" => new OsuLastCommand(),

            //    "/user" => new OsuUserCommand(),
            //    "/u" => new OsuUserCommand(),

            //    "/score" => new OsuScoreCommand(),
            //    "/s" => new OsuScoreCommand(),


            //    _ => new OtherCommand()
            //};

            //await handler.action(bot, update);
        }
    }
}
