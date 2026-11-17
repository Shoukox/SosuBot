using Microsoft.Extensions.DependencyInjection;
using SosuBot.Database;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Services;
using SosuBot.Services.Ai;
using SosuBot.TelegramHandlers.Abstract;
using Telegram.Bot.Types;

namespace SosuBot.TelegramHandlers.Commands;

/// <summary>
/// Talks to the AI persona, like <c>/c ai</c>. Only admins can use it while it is in development; everyone else is
/// told so.
/// </summary>
public sealed class AiCommand : CommandBase<Message>
{
    public static readonly string[] Commands = ["/ai"];
    public static readonly string Description = "поговорить с ИИ (в разработке)";

    private AiChatResponder _aiChatResponder = null!;
    private BotContext _database = null!;

    public override async Task BeforeExecuteAsync()
    {
        await base.BeforeExecuteAsync();
        _aiChatResponder = Context.ServiceProvider.GetRequiredService<AiChatResponder>();
        _database = Context.ServiceProvider.GetRequiredService<BotContext>();
    }

    public override async Task ExecuteAsync()
    {
        long senderId = Context.Update.From!.Id;
        OsuUser? sender = await _database.OsuUsers.FindAsync(senderId);
        if (sender?.IsAdmin != true && !ChatModerationService.IsCreator(senderId))
        {
            await Context.Update.ReplyAsync(Context.BotClient, Context.GetLocalization().ai_inDevelopment);
            return;
        }

        // Everything after the command, line breaks included; a bare "/ai" in reply to a message asks about it.
        string prompt = AiChatMemory.ExtractAiCommandPrompt(Context.Update.Text!) ?? "";
        await _aiChatResponder.RespondAsync(Context.BotClient, Context.Update, prompt, _database,
            Context.CancellationToken);
    }
}
