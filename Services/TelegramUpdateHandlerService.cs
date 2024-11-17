using ImageGeneratorTgBot.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using User = ImageGeneratorTgBot.Models.User;

namespace ImageGeneratorTgBot.Services;

public class TelegramUpdateHandlerService : IUpdateHandler
{
	private const string _logTag = $"[{nameof(TelegramUpdateHandlerService)}]";

	private readonly Dictionary<long, string> _userStates = new();
	private readonly Dictionary<string, object> _filter = new();
	private readonly Dictionary<string, Func<Message, Task<string>>> _stateHandlers;
	private readonly Dictionary<string, Func<Message, Task<Message>>> _commandHandlers;

	private readonly ILogger<TelegramUpdateHandlerService> _logger;
	private readonly ITelegramBotClient _telegramBotClient;
	private readonly HuggingFaceService _huggingFace;
	private readonly SupabaseService _supabaseService;
	private readonly TaskSchedulerService _scheduler;

	public TelegramUpdateHandlerService(ILogger<TelegramUpdateHandlerService> logger,
		ITelegramBotClient telegramBotClient,
		HuggingFaceService huggingFace,
		SupabaseService supabaseService,
		TaskSchedulerService scheduler)
	{
		_logger = logger;
		_telegramBotClient = telegramBotClient;
		_huggingFace = huggingFace;
		_supabaseService = supabaseService;
		_scheduler = scheduler;

		_stateHandlers = new Dictionary<string, Func<Message, Task<string>>>
		{
			["awaiting_time"] = HandleAwaitingTimeAsync,
			["awaiting_themes"] = HandleAwaitingThemeAsync,
			["awaiting_plots"] = HandleAwaitingFunctionAsync
		};

		_commandHandlers = new Dictionary<string, Func<Message, Task<Message>>>
		{
			["/photo"] = SendPhoto,
			["/text"] = SendText,
			["/start"] = SendWelcomeText,
			["/refresh"] = Refresh,
		};
	}

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

		if (_userStates.TryGetValue(msg.From.Id, out var state) 
		    && _stateHandlers.TryGetValue(state, out var handler))
		{
			try
			{
				var response = await handler(msg);
				await _telegramBotClient.SendMessage(msg.Chat.Id, response);
			}
			catch (Exception ex)
			{
				_logger.LogError("{LogTag} Error handling state: {Exception}", _logTag, ex);
				await _telegramBotClient.SendMessage(msg.Chat.Id, "An error occurred. Please try again later.");
			}
		}
		else
		{
			if (msg.Text is not { } messageText)
				return;

			if (_commandHandlers.TryGetValue(messageText.Split(' ')[0], out var commandHandler))
			{
				var sentMessage = await commandHandler(msg);
				_logger.LogInformation("{LogTag} The message was sent with id: {SentMessageId}", _logTag, sentMessage.Id);
			}
			else
			{
				await Usage(msg);
			}
		}
	}

	async Task<Message> Usage(Message msg)
	{
		const string usage = """
		                     <b><u>Bot menu</u></b>:
		                     /photo          - send a photo
		                     /text           - send a generated text
		                     /start          - set settings
		                     /refresh        - refresh User settings
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

	async Task<Message> Refresh(Message msg)
	{
		await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);

		string theme = string.Empty;
		string function = string.Empty;
		string time = string.Empty;
		string chatId =string.Empty;

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
				theme = userSettingsExists.Themes;
				function = userSettingsExists.Function;
				time = userSettingsExists.TimeToSend;
				chatId = userExists.ChatId;
			}
		}

		_ = _scheduler.Schedule(chatId, time, theme, function);
		return await _telegramBotClient.SendMessage(msg.Chat, "Refreshed");
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

	private async Task<User?> GetUserByChatIdAsync(long chatId)
	{
		_filter["chat_id"] = chatId.ToString();
		var user = await _supabaseService.GetDataAsync<User>(_filter);
		_filter.Remove("chat_id");
		return user;
	}

	private async Task<UserSettings?> GetUserSettingsAsync(int userId)
	{
		_filter["user_id"] = userId;
		var settings = await _supabaseService.GetDataAsync<UserSettings>(_filter);
		_filter.Remove("user_id");
		return settings;
	}

	private async Task<UserSettings> EnsureUserSettingsAsync(int userId)
	{
		var settings = await GetUserSettingsAsync(userId);
		if (settings != null) return settings;

		var newSettings = new UserSettings
		{
			UserId = userId,
			TimeToSend = string.Empty,
			Themes = string.Empty,
			Function = string.Empty
		};
		await _supabaseService.AddDataAsync(newSettings);
		return newSettings;
	}

	private async Task<string> HandleAwaitingTimeAsync(Message msg)
	{
		if (!DateTimeOffset.TryParse(msg.Text, out var timeOffset))
			return "Invalid time format. Please use the format HH:MM:SS+ZZ";

		return await HandleUserSettingsAsync(msg, settings => settings.TimeToSend = timeOffset.ToString());
	}

	private async Task<string> HandleAwaitingThemeAsync(Message msg)
	{
		var theme = msg.Text;
		return await HandleUserSettingsAsync(msg, settings => settings.Themes = theme);
	}

	private async Task<string> HandleAwaitingFunctionAsync(Message msg)
	{
		var function = msg.Text;
		return await HandleUserSettingsAsync(msg, settings => settings.Function = function);
	}


	private async Task<string> HandleUserSettingsAsync(Message msg, Action<UserSettings> updateSettings)
	{
		_userStates.Remove(msg.From.Id);

		var user = await GetUserByChatIdAsync(msg.From.Id);
		if (user == null)
			return "User not found!";

		var settings = await EnsureUserSettingsAsync(user.Id);
		updateSettings(settings);
		await _supabaseService.UpdateDataAsync(settings.Id, settings);

		return "Settings saved!";
	}

}