using System.Text.Json.Serialization;

namespace ImageGeneratorTgBot.Models;

public class FlanT5Model
{
	[JsonPropertyName("generated_text")] public string GeneratedText { get; set; } = default!;
}