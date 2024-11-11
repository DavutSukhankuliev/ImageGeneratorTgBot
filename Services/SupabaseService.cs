using System.Net;
using Supabase.Interfaces;
using Supabase.Postgrest.Models;
using Supabase.Realtime;
using Client = Supabase.Client;
using Constants = Supabase.Postgrest.Constants;

namespace ImageGeneratorTgBot.Services;

public class SupabaseService(
	Client _client,
	ILogger<SupabaseService> _logger)
{
	private const string _logTag = $"[{nameof(SupabaseService)}]";

	public async Task<bool> UpdateDataAsync<T>(int id, T data) where T : BaseModel, new()
	{
		_logger.LogDebug("{LogTag} Updating data in table {TableName} for ID: {Id}", _logTag, typeof(T).Name, id);

		try
		{
			var response = await _client
				.From<T>()
				.Filter("id", Constants.Operator.Equals, id)
				.Update(data);

			if (response.ResponseMessage.StatusCode == HttpStatusCode.OK)
			{
				_logger.LogInformation("{LogTag} Data successfully updated in table {TableName}", _logTag, typeof(T).Name);
				return true;
			}
			else
			{
				_logger.LogError("{LogTag} Error updating data in table {TableName}. Status code: {StatusCode}", _logTag, typeof(T).Name, response.ResponseMessage.StatusCode);
				return false;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{LogTag} Error updating data: {Message}", _logTag, ex.Message);
			return false;
		}
	}

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
			var response = await _client
				.From<T>()
				.Insert(new[] { data });

			if (response.ResponseMessage.StatusCode == HttpStatusCode.Created || 
			    response.ResponseMessage.StatusCode == HttpStatusCode.OK)
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

	public async Task<T?> GetDataAsync<T>(Dictionary<string, object> filters) where T : BaseModel, new()
	{
		_logger.LogDebug("{LogTag} Retrieving data from table {TableName} with filters: {Filters}", _logTag, typeof(T).Name, string.Join(", ", filters.Select(kv => $"{kv.Key}={kv.Value}")));

		try
		{
			var query = _client.From<T>().Select("*");

			// Apply each filter condition
			foreach (var filter in filters)
			{
				query = query.Filter(filter.Key, Constants.Operator.Equals, filter.Value);
			}

			var response = await query.Get();

			if (response.ResponseMessage.StatusCode == HttpStatusCode.OK && response.Models.Count > 0)
			{
				_logger.LogInformation("{LogTag} Data successfully retrieved from table {TableName} with specified filters", _logTag, typeof(T).Name);
				return response.Models.FirstOrDefault();
			}
			else
			{
				_logger.LogWarning("{LogTag} No data found in table {TableName} with specified filters", _logTag, typeof(T).Name);
				return null;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{LogTag} Error retrieving data: {Message}", _logTag, ex.Message);
			return null;
		}
	}
}