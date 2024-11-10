using System.Text;
using System.Text.Json;

namespace ImageGeneratorTgBot.Services;

public class HuggingFaceChatAdapter(ILogger<HuggingFaceChatAdapter> _logger)
{
	private const string _logTag = $"[{nameof(HuggingFaceChatAdapter)}]";

	public StringContent CreateRequest(string model, string prompt)
	{
		_logger.LogDebug("{LogTag} Creating request for model: {Model} with prompt: {Prompt}", _logTag, model, prompt);

		var requestData = new
		{
			model = model,
			messages = new[] { new { role = "user", content = prompt } },
			max_tokens = 500,
			stream = false
		};

		var requestContent = new StringContent(
			JsonSerializer.Serialize(requestData),
			Encoding.UTF8,
			"application/json");

		_logger.LogDebug("{LogTag} Request created successfully with max_tokens: {MaxTokens}, stream: {Stream}", _logTag, 500, false);

		return requestContent;
	}

	public async Task<string> ProcessStreamedResponse(Stream responseStream)
	{
		var output = new StringBuilder();
		using var reader = new StreamReader(responseStream);

		_logger.LogDebug("{LogTag} Processing streamed response", _logTag);

		while (!reader.EndOfStream)
		{
			var line = await reader.ReadLineAsync();
			if (string.IsNullOrWhiteSpace(line)) continue;

			try
			{
				var jsonElement = JsonSerializer.Deserialize<JsonElement>(line);

				if (jsonElement.TryGetProperty("choices", out var choices) &&
				    choices.ValueKind == JsonValueKind.Array &&
				    choices.GetArrayLength() > 0 &&
				    choices[0].TryGetProperty("message", out var message) &&
				    message.TryGetProperty("content", out var content))
				{
					string contentStr = content.GetString();
					output.Append(contentStr);
					_logger.LogDebug("{LogTag} Parsed content: {Content}", _logTag, contentStr);
				}
				else
				{
					_logger.LogWarning("{LogTag} Unexpected JSON structure in line: {Line}", _logTag, line);
				}
			}
			catch (JsonException ex)
			{
				_logger.LogError(ex, "{LogTag} Error processing JSON data for line: {Line}", _logTag, line);
			}
		}

		_logger.LogInformation("{LogTag} Streamed response processed successfully", _logTag);

		return output.ToString();
	}
}
