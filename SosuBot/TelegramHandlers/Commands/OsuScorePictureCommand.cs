using Microsoft.Extensions.DependencyInjection;
using OsuApi.BanchoV2.Models;
using OsuApi.BanchoV2.Users.Models;
using SosuBot.Database.Models;
using SosuBot.Extensions;
using SosuBot.Helpers;
using SosuBot.Helpers.OutputText;
using SosuBot.Localization;
using SosuBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SosuBot.TelegramHandlers.Commands;

/// <summary>/score as a picture: the player's top score on the beatmap, drawn over the beatmap's background.</summary>
public sealed class OsuScorePictureCommand : OsuScoreCommand
{
    public new static readonly string[] Commands = ["/scorep"];
    public new static readonly string Description = "[beatmap_link или beatmap_id] ваш лучший рекорд на карте картинкой";

    protected override string UsageText(ILocalization language) => language.command_scorePicture_usage;

    protected override async Task SendScoresAsync(Message waitMessage, ILocalization language, UserExtend player,
        Score[] scores, BeatmapExtended beatmap, BeatmapsetExtended beatmapset, Playmode playmode)
    {
        Score score = scores[0];
        await using MemoryStream picture = await Context.ServiceProvider.GetRequiredService<ScoreCardService>()
            .GenerateAsync(score, beatmap, playmode, Context.CancellationToken);

        string caption =
            $"🎮 {UserHelper.GetUserProfileUrlWrappedInUsernameString(player.Id!.Value, $"<b>{player.Username.EncodeHtml()}</b>")}\n" +
            $"🗺 <a href=\"{beatmap.Url}\">{beatmapset.Title.EncodeHtml()} [{beatmap.Version.EncodeHtml()}]</a>\n" +
            $"📅 {score.EndedAt:dd.MM.yyyy HH:mm} · {_scoreHelper.GetScoreUrlWrappedInString(score.Id!.Value, "link")}";

        await Context.BotClient.EditMessageMedia(
            waitMessage.Chat.Id,
            waitMessage.MessageId,
            new InputMediaPhoto(new InputFileStream(picture, "score.jpg"))
            {
                Caption = caption,
                ParseMode = ParseMode.Html
            },
            cancellationToken: Context.CancellationToken);
    }
}
