using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using WebRTCWebSocketServer.Handlers;
using WebRTCWebSocketServer.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => 
{
    options.AddPolicy("AllowSpecificOrigins", 
    policy => {
        policy.WithOrigins("https://192.168.1.25:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5217, listenOptions =>
    {
        listenOptions.UseHttps(new X509Certificate2("certificates/localhost.pfx", "2165"));
    });

    options.ListenAnyIP(5216);
});

builder.Services.AddDbContext<ApplicationDbContext>(options => {
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sqlOptions => {
        sqlOptions.CommandTimeout(120);
    });
});

builder.Services.AddControllers();
builder.Services.AddTransient<WebSocketHandler>();

var app = builder.Build();

app.UseCors("AllowSpecificOrigins");
app.UseWebSockets();
app.UseRouting();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/wss")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var webSocketHandler = context.RequestServices.GetRequiredService<WebSocketHandler>();
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await webSocketHandler.HandleWebSocketConnection(webSocket);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }
    else
    {
        await next(context);
    }
});


app.MapControllers();
app.Urls.Add("https://0.0.0.0:5217");
app.Urls.Add("http://0.0.0.0:5216");
app.Run();