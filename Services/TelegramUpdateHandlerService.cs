using ImageGeneratorTgBot.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using User = ImageGeneratorTgBot.Models.User;

namespace ImageGeneratorTgBot.Services;

public class TelegramUpdateHandlerService(
	ILogger<TelegramUpdateHandlerService> _logger,
	ITelegramBotClient _telegramBotClient,
	HuggingFaceService _huggingFace,
	SupabaseService _supabaseService) : IUpdateHandler
{
	private const string _logTag = $"[{nameof(TelegramUpdateHandlerService)}]";

	private readonly Dictionary<long, string> _userStates = new();
	private readonly Dictionary<string, object> _filter = new();

	public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
	{
		_logger.LogError("{LogTag} HandleError: {Exception}", _logTag, exception);
		// Cooldown in case of network connection error
		if (exception is RequestException)
			await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
	}

	public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await (update switch
		{
			{ Message: { } message }                        => OnMessage(message),
			{ CallbackQuery: { } callbackQuery }            => OnCallbackQuery(callbackQuery),

			_                                               => UnknownUpdateHandlerAsync(update)
		});
	}

	private async Task OnMessage(Message msg)
	{
		_logger.LogInformation("{LogTag} Receive message type: {MessageType}", _logTag, msg.Type);

		if (_userStates.TryGetValue(msg.From.Id, out string? state))
		{
			string response;

			_logger.LogInformation("{LogTag} Receive message: {MessageType}", _logTag, msg.Text);

			switch (state)
			{
				case "awaiting_time":
					if (DateTimeOffset.TryParse(msg.Text, out var timeOffset))
					{
						response = "Time saved!";
						_userStates.Remove(msg.From.Id);

						_logger.LogInformation("{LogTag} Parsed time: {Time}", _logTag, timeOffset);

						_filter.Add("chat_id", msg.From.Id.ToString());
						var userExists = await _supabaseService.GetDataAsync<User>(_filter);
						_filter.Remove("chat_id");
						if (userExists != null)
						{
							_filter.Add("user_id", userExists.Id);
							var userSettingsExists = await _supabaseService.GetDataAsync<UserSettings>(_filter);
							_filter.Remove("user_id");
							if (userSettingsExists != null)
							{
								var newSettings = userSettingsExists;
								newSettings.TimeToSend = timeOffset.ToString();
								await _supabaseService.UpdateDataAsync(newSettings.Id, newSettings);
							}
							else
							{
								var newSettings = new UserSettings
								{
									UserId = userExists.Id,
									TimeToSend = timeOffset.ToString(),
									Themes = string.Empty,
									Function = string.Empty
								};
								await _supabaseService.AddDataAsync(newSettings);
							}
						}
					}
					else
					{
						response = "Invalid time format. Please use the format HH:MM:SS+ZZ";
					}
					break;

				case "awaiting_themes":
					var themes = msg.Text;
					response = "Themes saved!";

					_userStates.Remove(msg.From.Id);
					_filter.Add("chat_id", msg.From.Id.ToString());
					var userExistsForThemes = await _supabaseService.GetDataAsync<User>(_filter);
					_filter.Remove("chat_id");
					if (userExistsForThemes != null)
					{
						_filter.Add("user_id", userExistsForThemes.Id);
						var userSettingsExists = await _supabaseService.GetDataAsync<UserSettings>(_filter);
						_filter.Remove("user_id");
						if (userSettingsExists != null)
						{
							var newSettings = userSettingsExists;
							newSettings.Themes = themes;
							await _supabaseService.UpdateDataAsync(newSettings.Id, newSettings);
						}
						else
						{
							var newSettings = new UserSettings
							{
								UserId = userExistsForThemes.Id,
								Themes = themes,
								TimeToSend = string.Empty,
								Function = string.Empty
							};
							await _supabaseService.AddDataAsync(newSettings);
						}
					}
					break;

				case "awaiting_plots":
					var plots = msg.Text;
					response = "Plots saved!";
					_userStates.Remove(msg.From.Id);

					_filter.Add("chat_id", msg.From.Id.ToString());
					var userExistsForPlots = await _supabaseService.GetDataAsync<User>(_filter);
					_filter.Remove("chat_id");
					if (userExistsForPlots != null)
					{
						_filter.Add("user_id", userExistsForPlots.Id);
						var userSettingsExists = await _supabaseService.GetDataAsync<UserSettings>(_filter);
						_filter.Remove("user_id");
						if (userSettingsExists != null)
						{
							var newSettings = userSettingsExists;
							newSettings.Function = plots; // сохраняем сюжет
							await _supabaseService.UpdateDataAsync(newSettings.Id, newSettings);
						}
						else
						{
							var newSettings = new UserSettings
							{
								UserId = userExistsForPlots.Id,
								Themes = string.Empty,
								Function = plots,
								TimeToSend = string.Empty
							};
							await _supabaseService.AddDataAsync(newSettings);
						}
					}
					break;

				default:
					response = "Unknown input.";
					break;
			}

			await _telegramBotClient.SendMessage(msg.Chat.Id, response);
		}
		else
		{
			if (msg.Text is not { } messageText)
				return;

			Message sentMessage = await (messageText.Split(' ')[0] switch
			{
				"/photo" => SendPhoto(msg),
				"/text" => SendText(msg),
				"/start" => SendWelcomeText(msg),

				_ => Usage(msg)
			});
			_logger.LogInformation("{LogTag} The message was sent with id: {SentMessageId}", _logTag, sentMessage.Id);
		}
	}

	async Task<Message> Usage(Message msg)
	{
		const string usage = """
		                     <b><u>Bot menu</u></b>:
		                     /photo          - send a photo
		                     /text           - send a generated text
		                     /start          - set settings
		                     """;
		return await _telegramBotClient.SendMessage(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
	}

	async Task<Message> SendWelcomeText(Message msg)
	{
		await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);
		string _welcomeText = $"Hello, {msg.Chat.FirstName}. I'm an everyday image generator bot!";

		var exists = _filter.TryAdd("chat_id", msg.From.Id.ToString());
		if (!exists)
		{
			_filter["chat_id"] = msg.From.Id.ToString();
		}

		var result = await _supabaseService.GetDataAsync<User>(_filter);

		if (result != null)
		{
			_logger.LogInformation($"{_logTag} ChatId was found user active");
		}
		else
		{
			_logger.LogInformation($"{_logTag} ChatId was not found user creating");
			var newUser = new User
			{
				ChatId = msg.Chat.Id.ToString()
			};
			await _supabaseService.AddDataAsync(newUser);
		}
		_filter.Remove("chat_id");
		await _telegramBotClient.SendMessage(msg.Chat, _welcomeText, ParseMode.None);
		return await SendReplyKeyboard(msg);
	}

	async Task<Message> SendReplyKeyboard(Message msg)
	{
		var inlineButtons = new InlineKeyboardMarkup()
			.AddNewRow()
				.AddButton("Set Time", "set_time")
				.AddButton("Set 3 Themes", "set_themes")
				.AddButton("Set 3 Plots", "set_plots")
			.AddNewRow()
				.AddButton("Expiration", "expire");
		return await _telegramBotClient.SendMessage(msg.Chat, "Please set your preferences:", replyMarkup: inlineButtons);
	}

	async Task<Message> SendText(Message msg)
	{
		await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);
		var prompt = msg.Text.Split(' ', 2)[1];
		var answer = await _huggingFace.SendPromptAsync<string>(prompt);
		var textToSend = $"""
		               {answer}
		               
		               <b>{prompt}</b>. <i>Source</i>: <a href='https://huggingface.co/meta-llama/Llama-3.2-3B-Instruct'>HuggingFace Meta llama 3.2</a>
		               """;


		return await _telegramBotClient.SendMessage(msg.Chat, textToSend, ParseMode.Html);
	}

	async Task<Message> SendPhoto(Message msg)
	{
		await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.UploadPhoto);
		var prompt = msg.Text.Split(' ', 2)[1];
		var photoBytes = await _huggingFace.SendPromptAsync<byte[]>(prompt);
		using var stream = new MemoryStream(photoBytes);
		var inputFile = new InputFileStream(stream, "photo.jpg");
		var caption = $"<b>{prompt}</b>. <i>Source</i>: <a href='https://huggingface.co/stabilityai/stable-diffusion-3.5-large'>HuggingFace StableDiffusion Model</a>";

		return await _telegramBotClient.SendPhoto(msg.Chat, inputFile, caption, ParseMode.Html);
	}

	private async Task OnCallbackQuery(CallbackQuery callbackQuery)
	{
		_logger.LogInformation("Received inline keyboard callback from: {CallbackQueryId}", callbackQuery.Id);

		string answer = string.Empty;

		switch (callbackQuery.Data)
		{
			case "set_time":
				answer = $"""
				          Write down time using the format HH:MM:SS+ZZ. +ZZ is GMT. (Moscow is +03:00)
				          Ex: 13:45:30+03:00
				          """;
				_userStates[callbackQuery.From.Id] = "awaiting_time";
				break;
			case "set_themes":
				answer = $"""
				          Write down 3 themes separated by comma.
				          Ex: cars, racing, football
				          """;
				_userStates[callbackQuery.From.Id] = "awaiting_themes";
				break;
			case "set_plots":
				answer = $"""
				          Write down 3 feelings or plots separated by comma.
				          Ex: upbeating, amazing, mysterious
				          """;
				_userStates[callbackQuery.From.Id] = "awaiting_plots";
				break;
			case "expire":
				string timeLeft = string.Empty;
				answer = $"""
				          Your plan will expire in {timeLeft}.
				          """;
				break;

			default:
				answer = "Unknown callback query";
				break;
		}
		await _telegramBotClient.SendMessage(callbackQuery.Message!.Chat, answer);
		
	}

	private Task UnknownUpdateHandlerAsync(Update update)
	{
		_logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
		return Task.CompletedTask;
	}
}