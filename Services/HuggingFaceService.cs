using System.Text;
using System.Text.Json;
using ImageGeneratorTgBot.Configurations;
using ImageGeneratorTgBot.Models;
using Microsoft.Extensions.Options;

namespace ImageGeneratorTgBot.Services;

public class HuggingFaceService(
	HttpClient _httpClient,
	IOptions<HuggingFaceConfiguration> _config,
	ILogger<HuggingFaceService> _logger)
{
	private const string _logTag = $"[{nameof(HuggingFaceService)}]";

	public async Task<T> SendPromptAsync<T>(string prompt)
	{
		var payload = new { inputs = prompt };
		var jsonData = JsonSerializer.Serialize(payload);
		using var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

		var endpoint = GetEndpointFromType<T>();

		try
		{
			_logger.LogDebug($"{_logTag} Sending request to HuggingFace API at {endpoint}.");

			var response = await _httpClient.PostAsync(endpoint, content);
			response.EnsureSuccessStatusCode();

			_logger.LogDebug($"{_logTag} Received successful response from HuggingFace API.");

			if (typeof(T) == typeof(string))
			{
				var responseBody = await response.Content.ReadAsStringAsync();
				var modelResponse = JsonSerializer.Deserialize<FlanT5Model>(responseBody);
				if (modelResponse != null) 
					return (T)(object)modelResponse.GeneratedText;

				throw new JsonException("Unable to deserialize the response into FlanT5Model.");
			}

			if (typeof(T) == typeof(byte[]))
				return (T)(object) await response.Content.ReadAsByteArrayAsync();

			throw new InvalidOperationException("Unsupported return type");
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, $"{_logTag} HttpRequestException while sending prompt to HuggingFace: {ex.Message}");
			throw new ApplicationException("HttpRequestException while sending prompt to HuggingFace.", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, $"{_logTag} Unknown error while sending prompt to HuggingFace.");
			throw;
		}
	}

	private string GetEndpointFromType<T>()
	{
		if (typeof(T) == typeof(string))
			return _config.Value.TextGenerator;

		if (typeof(T) == typeof(byte[]))
			return _config.Value.ImageGenerator;

		return _config.Value.TextGenerator;
	}
}