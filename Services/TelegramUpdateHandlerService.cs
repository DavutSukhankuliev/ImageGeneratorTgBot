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
			"/text" => SendText(msg),

			_ => Usage(msg)
		});
		_logger.LogInformation("{LogTag} The message was sent with id: {SentMessageId}", _logTag, sentMessage.Id);
	}

	async Task<Message> Usage(Message msg)
	{
		const string usage = """
		                     <b><u>Bot menu</u></b>:
		                     /photo          - send a photo
		                     /text           - send a generated text
		                     """;
		return await _telegramBotClient.SendMessage(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
	}

	async Task<Message> SendText(Message msg)
	{
		await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);
		var prompt = msg.Text.Split(' ', 2)[1];
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

	// Process Inline Keyboard callback data
	private async Task OnCallbackQuery(CallbackQuery callbackQuery)
	{
		_logger.LogInformation("Received inline keyboard callback from: {CallbackQueryId}", callbackQuery.Id);
		await _telegramBotClient.AnswerCallbackQuery(callbackQuery.Id, $"Received {callbackQuery.Data}");
		await _telegramBotClient.SendMessage(callbackQuery.Message!.Chat, $"Received {callbackQuery.Data}");
	}

	private Task UnknownUpdateHandlerAsync(Update update)
	{
		_logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
		return Task.CompletedTask;
	}
}