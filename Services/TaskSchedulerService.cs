using System.Collections.Concurrent;
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

	private readonly ConcurrentDictionary<string, CancellationTokenSource> _scheduledTasks = new();

	public void CancelTask(string chatId)
	{
		if (_scheduledTasks.TryRemove(chatId, out var cts))
		{
			cts.Cancel();
			_logger.LogInformation($"{_logTag} Task for chat {chatId} has been canceled.");
		}
		else
		{
			_logger.LogWarning($"{_logTag} No task found for chat {chatId} to cancel.");
		}
	}

	public Task Schedule(string chatId, string timeString, string theme, string function)
	{
		if (_scheduledTasks.TryRemove(chatId, out var existingCts))
			existingCts.Cancel();

		var cts = new CancellationTokenSource();
		_scheduledTasks[chatId] = cts;

		Task.Run(() => RunTask(chatId, timeString, theme, function, cts.Token), cts.Token);

		return Task.CompletedTask;
	}

	private async Task RunTask(string chatId, string timeString, string theme, string function, CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				DateTimeOffset now = DateTimeOffset.UtcNow;
				DateTimeOffset targetTime = ParseTime(timeString);

				if (targetTime <= now)
					targetTime = targetTime.AddDays(1);

				TimeSpan delay = targetTime - now;
				_logger.LogInformation($"{_logTag} Next scheduled task for chat {chatId} at {targetTime}");

				await Task.Delay(delay, cancellationToken); // Поддержка отмены через токен

				if (cancellationToken.IsCancellationRequested)
					break;

				await ExecuteTask(chatId, theme, function);
			}
		}
		catch (TaskCanceledException)
		{
			_logger.LogInformation($"{_logTag} Task for chat {chatId} was cancelled.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, $"{_logTag} Error in scheduled task for chat {chatId}");
		}
	}

	private async Task ExecuteTask(string chatId, string theme, string function)
	{
		try
		{
			_logger.LogInformation($"{_logTag} Executing scheduled task for chat {chatId}");

			var imageBytes = await HandleModel(theme, function);
			using var stream = new MemoryStream(imageBytes);
			var inputFile = new InputFileStream(stream, "photo.jpg");

			await _telegramBotClient.SendPhoto(chatId, inputFile);

			_logger.LogInformation($"{_logTag} Task for chat {chatId} executed successfully.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, $"{_logTag} Error executing task for chat {chatId}");
		}
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
			if (!timeString.Contains(":"))
				throw new FormatException("Invalid time format.");

			if (timeString.Count(c => c == ':') == 1)
				timeString += ":00";

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
				targetDateTimeOffset = targetDateTimeOffset.AddDays(1);

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