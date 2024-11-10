using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace ImageGeneratorTgBot.Services;

public class TelegramUpdateHandlerService(
	ILogger<TelegramUpdateHandlerService> _logger,
	ITelegramBotClient _telegramBotClient,
	HuggingFaceService _huggingFace) : IUpdateHandler
{
	private const string _logTag = $"[{nameof(TelegramUpdateHandlerService)}]";

	private static readonly InputPollOption[] _pollOptions = ["Hello", "World!"];

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
			{ EditedMessage: { } message }                  => OnMessage(message),
			{ CallbackQuery: { } callbackQuery }            => OnCallbackQuery(callbackQuery),
			{ InlineQuery: { } inlineQuery }                => OnInlineQuery(inlineQuery),
			{ ChosenInlineResult: { } chosenInlineResult }  => OnChosenInlineResult(chosenInlineResult),
			{ Poll: { } poll }                              => OnPoll(poll),
			{ PollAnswer: { } pollAnswer }                  => OnPollAnswer(pollAnswer),
			// ChannelPost:
			// EditedChannelPost:
			// ShippingQuery:
			// PreCheckoutQuery:

			_                                               => UnknownUpdateHandlerAsync(update)
		});
	}

	private async Task OnMessage(Message msg)
	{
		_logger.LogInformation("{LogTag} Receive message type: {MessageType}", _logTag, msg.Type);
		if (msg.Text is not { } messageText)
			return;

		Message sentMessage = await (messageText.Split(' ')[0] switch
		{
			"/photo" => SendPhoto(msg),
			"/inline_buttons" => SendInlineKeyboard(msg),
			"/keyboard" => SendReplyKeyboard(msg),
			"/remove" => RemoveKeyboard(msg),
			"/request" => RequestContactAndLocation(msg),
			"/inline_mode" => StartInlineQuery(msg),
			"/poll" => SendPoll(msg),
			"/poll_anonymous" => SendAnonymousPoll(msg),
			"/throw" => FailingHandler(msg),
			"/start" => Usage(msg),
			_ => SendText(msg)
		});
		_logger.LogInformation("{LogTag} The message was sent with id: {SentMessageId}", _logTag, sentMessage.Id);
	}

	async Task<Message> Usage(Message msg)
	{
		const string usage = """
		                     <b><u>Bot menu</u></b>:
		                     /photo          - send a photo
		                     /inline_buttons - send inline buttons
		                     /keyboard       - send keyboard buttons
		                     /remove         - remove keyboard buttons
		                     /request        - request location or contact
		                     /inline_mode    - send inline-mode results list
		                     /poll           - send a poll
		                     /poll_anonymous - send an anonymous poll
		                     /throw          - what happens if handler fails
		                     """;
		return await _telegramBotClient.SendMessage(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
	}

	async Task<Message> SendText(Message msg)
	{
		await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);
		var prompt = msg.Text;
		var answer = await _huggingFace.SendPromptAsync<string>(prompt);
		var textToSend = $"""
		               {answer}
		               
		               <b>{prompt}</b>. <i>Source</i>: <a href='https://huggingface.co/google/flan-t5-large'>HuggingFace Flan T5 Large Model</a>
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

	// Send inline keyboard. You can process responses in OnCallbackQuery handler
	async Task<Message> SendInlineKeyboard(Message msg)
	{
		var inlineMarkup = new InlineKeyboardMarkup()
			.AddNewRow("1.1", "1.2", "1.3")
			.AddNewRow()
			.AddButton("WithCallbackData", "CallbackData")
			.AddButton(InlineKeyboardButton.WithUrl("WithUrl", "https://github.com/TelegramBots/Telegram.Bot"));
		return await _telegramBotClient.SendMessage(msg.Chat, "Inline buttons:", replyMarkup: inlineMarkup);
	}

	async Task<Message> SendReplyKeyboard(Message msg)
	{
		var replyMarkup = new ReplyKeyboardMarkup(true)
			.AddNewRow("1.1", "1.2", "1.3")
			.AddNewRow().AddButton("2.1").AddButton("2.2");
		return await _telegramBotClient.SendMessage(msg.Chat, "Keyboard buttons:", replyMarkup: replyMarkup);
	}

	async Task<Message> RemoveKeyboard(Message msg)
	{
		return await _telegramBotClient.SendMessage(msg.Chat, "Removing keyboard", replyMarkup: new ReplyKeyboardRemove());
	}

	async Task<Message> RequestContactAndLocation(Message msg)
	{
		var replyMarkup = new ReplyKeyboardMarkup(true)
			.AddButton(KeyboardButton.WithRequestLocation("Location"))
			.AddButton(KeyboardButton.WithRequestContact("Contact"));
		return await _telegramBotClient.SendMessage(msg.Chat, "Who or Where are you?", replyMarkup: replyMarkup);
	}

	async Task<Message> StartInlineQuery(Message msg)
	{
		var button = InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("Inline Mode");
		return await _telegramBotClient.SendMessage(msg.Chat, "Press the button to start Inline Query\n\n" +
		                                                      "(Make sure you enabled Inline Mode in @BotFather)", replyMarkup: new InlineKeyboardMarkup(button));
	}

	async Task<Message> SendPoll(Message msg)
	{
		return await _telegramBotClient.SendPoll(msg.Chat, "Question", _pollOptions, isAnonymous: false);
	}

	async Task<Message> SendAnonymousPoll(Message msg)
	{
		return await _telegramBotClient.SendPoll(chatId: msg.Chat, "Question", _pollOptions);
	}

	static Task<Message> FailingHandler(Message msg)
	{
		throw new NotImplementedException("FailingHandler");
	}

	// Process Inline Keyboard callback data
	private async Task OnCallbackQuery(CallbackQuery callbackQuery)
	{
		_logger.LogInformation("Received inline keyboard callback from: {CallbackQueryId}", callbackQuery.Id);
		await _telegramBotClient.AnswerCallbackQuery(callbackQuery.Id, $"Received {callbackQuery.Data}");
		await _telegramBotClient.SendMessage(callbackQuery.Message!.Chat, $"Received {callbackQuery.Data}");
	}

	#region Inline Mode

	private async Task OnInlineQuery(InlineQuery inlineQuery)
	{
		_logger.LogInformation("Received inline query from: {InlineQueryFromId}", inlineQuery.From.Id);

		InlineQueryResult[] results = [ // displayed result
			new InlineQueryResultArticle("1", "Telegram.Bot", new InputTextMessageContent("hello")),
			new InlineQueryResultArticle("2", "is the best", new InputTextMessageContent("world"))
		];
		await _telegramBotClient.AnswerInlineQuery(inlineQuery.Id, results, cacheTime: 0, isPersonal: true);
	}

	private async Task OnChosenInlineResult(ChosenInlineResult chosenInlineResult)
	{
		_logger.LogInformation("Received inline result: {ChosenInlineResultId}", chosenInlineResult.ResultId);
		await _telegramBotClient.SendMessage(chosenInlineResult.From.Id, $"You chose result with Id: {chosenInlineResult.ResultId}");
	}

	#endregion

	private Task OnPoll(Poll poll)
	{
		_logger.LogInformation("Received Poll info: {Question}", poll.Question);
		return Task.CompletedTask;
	}

	private async Task OnPollAnswer(PollAnswer pollAnswer)
	{
		var answer = pollAnswer.OptionIds.FirstOrDefault();
		var selectedOption = _pollOptions[answer];
		if (pollAnswer.User != null)
			await _telegramBotClient.SendMessage(pollAnswer.User.Id, $"You've chosen: {selectedOption.Text} in poll");
	}

	private Task UnknownUpdateHandlerAsync(Update update)
	{
		_logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
		return Task.CompletedTask;
	}
}