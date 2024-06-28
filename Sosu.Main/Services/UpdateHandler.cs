using Microsoft.Extensions.Logging;
using Sosu.Web.Services.HandleUpdateType;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Sosu.Main.Services
{
    public class UpdateHandler
    {
        public readonly ILogger<UpdateHandler> _logger;
        public readonly ITelegramBotClient _bot;

        public UpdateHandler(ILogger<UpdateHandler> logger, ITelegramBotClient bot)
        {
            _logger = logger;
            _bot = bot;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            _logger.LogInformation("Got Update with id:{0}", update.Id);
            IHandler handler = update.Type switch
            {
                UpdateType.Message => new HandleMessage(update.Message, _bot),
                UpdateType.EditedMessage => new HandleMessage(update.Message, _bot),

                UpdateType.Unknown => new HandleUnknown(_bot),
                _ => new HandleUnknown(_bot)
            };

            try
            {
                await handler.HandleAsync();
            }
            catch (ApiRequestException apiException)
            {
                await handler.HandleErrorAsync(apiException);
                _logger.LogError(apiException.Message);
            }
            catch (Exception exception)
            {
                await handler.HandleErrorAsync(exception);
                _logger.LogError(exception.Message);
            }
            finally
            {
                _logger.LogInformation("Processed Update with id:{0}", update.Id);
            }
        }
    }
}
