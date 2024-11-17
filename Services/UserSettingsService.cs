using ImageGeneratorTgBot.Models;

namespace ImageGeneratorTgBot.Services;

public class UserSettingsService
{
	public event Action<long, UserSettings>? SettingsUpdated;

	private readonly Dictionary<string, object> _filter = new();

	private readonly SupabaseService _supabaseService;

	public UserSettingsService(SupabaseService supabaseService)
	{
		_supabaseService = supabaseService;
	}

	public async Task UpdateSettingsAsync(long chatId, UserSettings settings)
	{
		await _supabaseService.UpdateDataAsync(settings.Id, settings);
		SettingsUpdated?.Invoke(chatId, settings);
	}

	public async Task<User?> GetUserAsync(long chatId)
	{
		_filter["chat_id"] = chatId.ToString();
		var user = await _supabaseService.GetDataAsync<User>(_filter);
		_filter.Remove("chat_id");
		return user;
	}

	public async Task<UserSettings?> GetUserSettingsAsync(int userId)
	{
		_filter["user_id"] = userId;
		var settings = await _supabaseService.GetDataAsync<UserSettings>(_filter);
		_filter.Remove("user_id");
		return settings;
	}

	public async Task<UserSettings> EnsureUserSettingsAsync(int userId)
	{
		var settings = await GetUserSettingsAsync(userId);
		return settings;
	}
}