namespace ImageGeneratorTgBot.Configurations;

public class HuggingFaceConfiguration
{
	public string AuthToken { get; init; } = default!;
	public Uri BaseUrl { get; init; } = default!;
	public string ImageGenerator { get; init; } = default!;
	public string TextGenerator { get; init; } = default!;
	public string TextEndpoint { get; init; } = default!;
}