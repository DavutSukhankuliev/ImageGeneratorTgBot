using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace ImageGeneratorTgBot.Services;

public class GoogleSheetsService(
	ILogger<GoogleSheetsService> _logger)
{
	private const string _logTag = $"{nameof(GoogleSheetsService)}";

	private readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
	private readonly string ApplicationName = "TelegramBotAnalytics";
	private readonly string SpreadsheetId = "1iHIJjkPekG6AhYfKMoGZgETdAYD-h1hUBQt-WKbzank";
	private readonly string SheetRange = "Users/Times!A:C";
	private readonly string SheetRangeExpiration = "Expiration!A:C";
	private readonly string SheetRangeThemes = "Themes!A:D";

	public async Task AddEventToSheet(string userId, string timeToSend)
	{
		try
		{
			_logger.LogInformation("{LogTag} Adding event to the sheet...", _logTag);

			var credential = await GoogleCredential.FromFileAsync("ai-image-everyday-2897fb17db73.json", CancellationToken.None);
			var service = new SheetsService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = ApplicationName,
			});

			var valueRange = new ValueRange
			{
				Values = new List<IList<object>> { new List<object> { DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), userId, timeToSend } }
			};

			var updateRequest = service.Spreadsheets.Values.Append(valueRange, SpreadsheetId, SheetRange);
			updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;

			var response = await updateRequest.ExecuteAsync();
			_logger.LogInformation("{LogTag} Successfully added event to the sheet. Updated range: {Range}", _logTag, response.Updates?.UpdatedRange);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{LogTag} Failed to add event to the sheet.", _logTag);
		}
	}

	public async Task AddEventExpirationToSheet(string userId, string expirationDate)
	{
		try
		{
			_logger.LogInformation("{LogTag} Adding event to the sheet...", _logTag);

			var credential = await GoogleCredential.FromFileAsync("ai-image-everyday-2897fb17db73.json", CancellationToken.None);
			var service = new SheetsService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = ApplicationName,
			});

			var valueRange = new ValueRange
			{
				Values = new List<IList<object>> { new List<object> { DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), userId, expirationDate } }
			};

			var updateRequest = service.Spreadsheets.Values.Append(valueRange, SpreadsheetId, SheetRangeExpiration);
			updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;

			var response = await updateRequest.ExecuteAsync();
			_logger.LogInformation("{LogTag} Successfully added event to the sheet. Updated range: {Range}", _logTag, response.Updates?.UpdatedRange);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{LogTag} Failed to add event to the sheet.", _logTag);
		}
	}

	public async Task AddEventThemesToSheet(string userId, string themes, string functions)
	{
		try
		{
			_logger.LogInformation("{LogTag} Adding event to the sheet...", _logTag);

			var credential = await GoogleCredential.FromFileAsync("ai-image-everyday-2897fb17db73.json", CancellationToken.None);
			var service = new SheetsService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = ApplicationName,
			});

			var valueRange = new ValueRange
			{
				Values = new List<IList<object>> { new List<object> { DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), userId, themes, functions } }
			};

			var updateRequest = service.Spreadsheets.Values.Append(valueRange, SpreadsheetId, SheetRangeThemes);
			updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;

			var response = await updateRequest.ExecuteAsync();
			_logger.LogInformation("{LogTag} Successfully added event to the sheet. Updated range: {Range}", _logTag, response.Updates?.UpdatedRange);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{LogTag} Failed to add event to the sheet.", _logTag);
		}
	}
}