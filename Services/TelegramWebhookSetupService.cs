using ImageGeneratorTgBot.Configurations;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace ImageGeneratorTgBot.Services;

public class TelegramWebhookSetupService(
	ITelegramBotClient _botClient,
	IOptions<TelegramBotConfiguration> _config,
	ILogger<TelegramWebhookSetupService> _logger)
{
	private const string _logTag = $"[{nameof(TelegramWebhookSetupService)}]";

	public async Task<string> SetWebhookAsync(CancellationToken cancellationToken)
	{
		try
		{
			var webhookUrl = _config.Value.BotWebhookUrl.AbsoluteUri;
			await _botClient.SetWebhook(
				webhookUrl, 
				allowedUpdates: [
					UpdateType.Message
				],
				secretToken: _config.Value.SecretToken,
				cancellationToken: cancellationToken);

			_logger.LogDebug("{LogTag} Webhook was set to {WebhookUrl}.", _logTag, webhookUrl);
			return $"Webhook was set to {webhookUrl}.";
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{LogTag} Failed to set webhook.", _logTag);
		}
		return "Failed to set webhook.";
	}
}