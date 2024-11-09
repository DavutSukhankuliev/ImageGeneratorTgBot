var builder = WebApplication.CreateBuilder(args);

// services and dependencies here

var app = builder.Build();

// middleware

app.UseHttpsRedirection();

// endpoints

app.Run();
