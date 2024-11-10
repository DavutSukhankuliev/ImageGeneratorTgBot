using System.Text;
using System.Text.Json;
using ImageGeneratorTgBot.Configurations;
using Microsoft.Extensions.Options;

namespace ImageGeneratorTgBot.Services;

public class HuggingFaceService(
	HttpClient _httpClient,
	HuggingFaceChatAdapter _chatAdapter,
	IOptions<HuggingFaceConfiguration> _config,
	ILogger<HuggingFaceService> _logger)
{
	private const string _logTag = $"[{nameof(HuggingFaceService)}]";

	public async Task<T> SendPromptAsync<T>(string prompt)
	{
		var endpoint = GetEndpointFromType<T>();

		try
		{
			if (typeof(T) == typeof(string))
			{
				_logger.LogDebug("{LogTag} Wrapping prompt {Prompt}", _logTag, prompt);

				var request = _chatAdapter.CreateRequest(endpoint, prompt);

				_logger.LogInformation("{LogTag} Sending request to HuggingFace API at {Endpoint}", _logTag, endpoint);

				var response = await _httpClient.PostAsync(_config.Value.TextEndpoint, request);
				response.EnsureSuccessStatusCode();

				_logger.LogInformation("{LogTag} Received successful response from HuggingFace API", _logTag);

				var responseStream = await response.Content.ReadAsStreamAsync();
				var responseText = await _chatAdapter.ProcessStreamedResponse(responseStream);

				return (T)(object) responseText;
			}

			if (typeof(T) == typeof(byte[]))
			{
				var payload = new { inputs = prompt };
				var jsonData = JsonSerializer.Serialize(payload);
				using var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

				_logger.LogInformation("{LogTag} Sending request to HuggingFace API at {Endpoint}", _logTag, endpoint);

				var response = await _httpClient.PostAsync(endpoint, content);
				response.EnsureSuccessStatusCode();

				_logger.LogInformation("{LogTag} Received successful response from HuggingFace API", _logTag);

				return (T)(object) await response.Content.ReadAsByteArrayAsync();
			}

			throw new InvalidOperationException("Unsupported return type");
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "{LogTag} HttpRequestException while sending prompt to HuggingFace: {Message}", _logTag, ex.Message);
			throw new ApplicationException("HttpRequestException while sending prompt to HuggingFace.", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{LogTag} Unknown error while sending prompt to HuggingFace", _logTag);
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
