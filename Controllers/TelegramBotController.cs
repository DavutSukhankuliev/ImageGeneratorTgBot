using ImageGeneratorTgBot.Configurations;
using ImageGeneratorTgBot.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ImageGeneratorTgBot.Controllers;

[ApiController]
[Route("bot")]
public class TelegramBotController(
	ILogger<TelegramBotController> _logger,
	IOptions<TelegramBotConfiguration> _config) : ControllerBase
{
	private const string _logTag = $"[{nameof(TelegramBotController)}]";

	[HttpPost]
	[Route("update-receiver")]
	public async Task<IActionResult> HandleUpdate([FromBody] Update update, [FromServices] ITelegramBotClient bot, [FromServices] TelegramUpdateHandlerService handleUpdateService, CancellationToken ct)
	{
		var secretToken = Request.Headers["X-Telegram-Bot-Api-Secret-Token"];
		if (secretToken != _config.Value.SecretToken)
		{
			_logger.LogDebug("{LogTag} SecretToken invalid {SecretToken}", _logTag, secretToken);

			return Forbid();
		}

		try
		{
			await handleUpdateService.HandleUpdateAsync(bot, update, ct);
		}
		catch (Exception exception)
		{
			await handleUpdateService.HandleErrorAsync(bot, exception, Telegram.Bot.Polling.HandleErrorSource.HandleUpdateError, ct);
		}
		return Ok();
	}
}