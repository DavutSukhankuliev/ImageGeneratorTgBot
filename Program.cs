using ImageGeneratorTgBot.Configurations;
using ImageGeneratorTgBot.Services;
using Microsoft.Extensions.Options;
using Supabase;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// services and dependencies here
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<TelegramBotConfiguration>(
	builder.Configuration.GetSection("TelegramBotConfiguration"));

builder.Services.AddHttpClient("TelegramWebhook")
	.AddTypedClient<ITelegramBotClient>((httpClient, serviceProvider) => 
	{
		var botConfig = serviceProvider.GetRequiredService<IOptions<TelegramBotConfiguration>>().Value;
		return new TelegramBotClient(botConfig.BotToken, httpClient);
	})
	.RemoveAllLoggers(); // WARNING: TOKEN EXPOSURE

builder.Services.AddSingleton<TelegramUpdateHandlerService>();
builder.Services.AddSingleton<TelegramWebhookSetupService>();
builder.Services.ConfigureTelegramBotMvc();

builder.Services.Configure<SupabaseConfiguration>(
	builder.Configuration.GetSection("SupabaseConfiguration"));

builder.Services.AddSingleton(provider =>
{
	var config = provider.GetRequiredService<IOptions<SupabaseConfiguration>>().Value;
	var options = new SupabaseOptions
	{
		AutoRefreshToken = true,
		AutoConnectRealtime = true,
		// SessionHandler = new SupabaseSessionHandler() 
	};

	return new Client(config.BaseUrl.AbsoluteUri, config.AuthToken, options);
});

builder.Services.AddSingleton<SupabaseService>();

builder.Services.Configure<HuggingFaceConfiguration>(
	builder.Configuration.GetSection("HuggingFaceConfiguration"));

builder.Services.AddHttpClient<HuggingFaceService>("HuggingFaceService", (serviceProvider, httpClient) =>
	{
		var config = serviceProvider.GetRequiredService<IOptions<HuggingFaceConfiguration>>().Value;

		httpClient.BaseAddress = new Uri(config.BaseUrl.AbsoluteUri);
		httpClient.DefaultRequestHeaders.Authorization =
			new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.AuthToken);
	})
	.RemoveAllLoggers(); // WARNING: TOKEN EXPOSURE

builder.Services.AddSingleton<HuggingFaceChatAdapter>();

builder.Services.AddSingleton<TaskSchedulerService>();

builder.Services.AddControllers();

var app = builder.Build();

// middleware
using(var scope = app.Services.CreateScope())
{
	var webhookSetupService = scope.ServiceProvider.GetRequiredService<TelegramWebhookSetupService>();
	await webhookSetupService.SetWebhookAsync(CancellationToken.None);
}

//app.UseHttpsRedirection(); // WARNING: CERTIFICATE NEEDED
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
