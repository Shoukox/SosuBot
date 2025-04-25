using Telegram.Bot.Types;

namespace Sosu.Services.ProcessUpdate
{
    public class CheckForNewDataInUpdate
    {
        public static void Check(Message message)
        {
            bool isGroup = message.Chat.Id != message.From.Id;
            var chat = Variables.chats.FirstOrDefault(m => m.chat.Id == message.Chat.Id);
            if (chat == default)
            {
                Variables.chats.Add(new Sosu.Types.Chat(message.Chat, 0));
                chat = Variables.chats.Last();
                //Variables.db.InsertOrUpdateOsuChatsTable(chat, true);
            }
            else
            {
                chat.chat = message.Chat;
            }

            if (isGroup)
            {
                if (!chat.members.Contains(message.From.Id)) chat.members.Add(message.From.Id);
                //Variables.db.InsertOrUpdateOsuChatsTable(chat, false);
            }
        }
    }
}
