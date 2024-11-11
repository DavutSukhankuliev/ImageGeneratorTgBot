using System.Net;
using Supabase;
using Supabase.Postgrest.Models;

namespace ImageGeneratorTgBot.Services;

public class SupabaseService(
	Client _client,
	ILogger<SupabaseService> _logger)
{
	private const string _logTag = $"[{nameof(SupabaseService)}]";

	public async Task<bool> AddDataAsync<T>(T data) where T : BaseModel, new()
	{
		/*var user = new User
		{
			Login = "log",
			Password = "pass",
			Name = "name",
			ChatId = "9999999"
		};
		await AddDataAsync<User>(user);*/

		_logger.LogDebug("{LogTag} Adding data to table {TableName}", _logTag, data.TableName);

		try
		{
			var response = await _client.From<T>().Insert(new[] { data });

			if (response.ResponseMessage.StatusCode == HttpStatusCode.Created)
			{
				_logger.LogInformation("{LogTag} Data successfully added to table {TableName}", _logTag, data.TableName);
				return true;
			}
			else
			{
				_logger.LogError("{LogTag} Error adding data to table {TableName}. Status code: {StatusCode}", _logTag, data.TableName, response.ResponseMessage.StatusCode);
				return false;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{LogTag} Error adding data: {Message}", _logTag, ex.Message);
			return false;
		}
	}
}