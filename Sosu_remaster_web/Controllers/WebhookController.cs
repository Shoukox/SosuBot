using Microsoft.AspNetCore.Mvc;
using Sosu.Services;
using Telegram.Bot.Types;

namespace Sosu.Controllers
{
    public class WebhookController : Controller
    {
        [HttpPost]
        public IActionResult Post([FromServices] HandleUpdateService handleUpdateService,
                                                    [FromBody] Update update)
        {
            Task.Run(async () => await handleUpdateService.EchoAsync(update));
            return Ok();
        }
    }
}