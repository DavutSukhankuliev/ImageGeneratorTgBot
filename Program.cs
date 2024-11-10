using ImageGeneratorTgBot.Configurations;
using ImageGeneratorTgBot.Services;
using Microsoft.Extensions.Options;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// services and dependencies here
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<TelegramBotConfiguration>(
	builder.Configuration.GetSection("BotConfiguration"));

builder.Services.AddHttpClient("TelegramWebhook")
	.AddTypedClient<ITelegramBotClient>((httpClient, sp) => 
	{
		var botConfig = sp.GetRequiredService<IOptions<TelegramBotConfiguration>>().Value;
		return new TelegramBotClient(botConfig.BotToken, httpClient);
	})
	.RemoveAllLoggers(); // WARNING: TOKEN EXPOSURE

builder.Services.AddSingleton<TelegramUpdateHandlerService>();
builder.Services.AddSingleton<TelegramWebhookSetupService>();
builder.Services.ConfigureTelegramBotMvc();

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
