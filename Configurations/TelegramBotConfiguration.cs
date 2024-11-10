namespace ImageGeneratorTgBot.Configurations;

public class TelegramBotConfiguration
{
	public string BotToken { get; init; } = default!;
	public string SecretToken { get; init; } = default!;
	public Uri BotWebhookUrl { get; init; } = default!;
}