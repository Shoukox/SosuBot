using Microsoft.AspNetCore.Mvc;
using Sosu.Main.Services;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Sosu.Main.Controllers
{
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(ILogger<WebhookController> logger)
        {
            _logger = logger;
        }

        [HttpPost("bot/{token}")]
        public async Task<IActionResult> Post([FromBody] Update update, [FromServices] UpdateHandler updateHandler)
        {
            await updateHandler.HandleUpdateAsync(update);
            return Ok();
        }

        [BindProperty(Name = "token")]
        public string token { get; set; }
    }
}