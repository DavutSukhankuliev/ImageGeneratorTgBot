using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ImageGeneratorTgBot.Services;

public class TaskSchedulerService(
	ILogger<TaskSchedulerService> _logger,
	HuggingFaceService _hf,
	ITelegramBotClient _telegramBotClient)
{
	private const string _textPromptPrefix = "Hello, imagine you are a prompt generator for an AI Model which generates images.";
	private const string _textPromptSuffix = "Send the prompt text only and nothing else.";

	private const string _imagePromptPrefix = "Hello, imagine you are an artist who creates wonderful masterpieces.";

	private const string _logTag = $"[{nameof(TaskSchedulerService)}]";

	public Task Schedule(string chatId, string timeString, string theme, string function)
	{
		DateTimeOffset targetDateTimeOffset = ParseTime(timeString);

		Task.Run(async () =>
		{
			while (true)
			{
				DateTimeOffset now = DateTimeOffset.UtcNow;
				DateTimeOffset targetTime = targetDateTimeOffset;

				if (targetTime <= now)
				{
					targetTime = targetTime.AddDays(1);
				}

				TimeSpan delay = targetTime - now;
				_logger.LogInformation($"{_logTag} Next scheduled task for chat {chatId} at {targetTime}");

				await Task.Delay(delay);
				try
				{
					_logger.LogInformation($"{_logTag} Executing scheduled task for chat {chatId}");

					var imageBytes = await HandleModel(theme, function);
					using var stream = new MemoryStream(imageBytes);
					var inputFile = new InputFileStream(stream, "photo.jpg");
					return await _telegramBotClient.SendPhoto(chatId, inputFile);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, $"{_logTag} Error executing daily task for chat {chatId}");
				}
			}
		});

		return Task.CompletedTask;
	}

	private async Task<byte[]> HandleModel(string theme, string function)
	{
		string textPrompt = $"{_textPromptPrefix} " +
		                    $"I want you to create a unique prompt for an AI model to generate an image of {theme} to express {function} " +
		                    $"{_textPromptSuffix}";
		string imagePrompt = $"{_imagePromptPrefix} ";

		imagePrompt += await _hf.SendPromptAsync<string>(textPrompt);

		byte[] generatedImage = await _hf.SendPromptAsync<byte[]>(imagePrompt);

		return generatedImage;
	}

	private DateTimeOffset ParseTime(string timeString)
	{
		try
		{
			if (!timeString.EndsWith(":00"))
			{
				timeString += ":00"; // Workaround: add seconds to supabase time zone format
			}

			DateTimeOffset dateTimeOffset = DateTimeOffset.ParseExact(
				timeString,
				"HH:mm:sszzz",
				CultureInfo.InvariantCulture);

			DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
			DateTimeOffset targetDateTimeOffset = new DateTimeOffset(
				nowUtc.Year, nowUtc.Month, nowUtc.Day,
				dateTimeOffset.Hour, dateTimeOffset.Minute, dateTimeOffset.Second,
				dateTimeOffset.Offset);

			if (targetDateTimeOffset < nowUtc)
			{
				targetDateTimeOffset = targetDateTimeOffset.AddDays(1);
			}

			_logger.LogInformation($"{_logTag} Successfully parsed time: {targetDateTimeOffset}");
			return targetDateTimeOffset;
		}
		catch (FormatException ex)
		{
			_logger.LogError(ex, $"{_logTag} Error parsing time string: {timeString}");
			throw;
		}
	}
}