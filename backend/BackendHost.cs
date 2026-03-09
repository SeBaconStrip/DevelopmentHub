using DevelopmentHub.Api.BackgroundServices;
using DevelopmentHub.Api.Configuration;
using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Hubs;
using DevelopmentHub.Api.Services;
using MongoDB.Driver;
using Serilog;
using Serilog.Events;

namespace DevelopmentHub.Api;

public static class BackendHost
{
    public static WebApplication Create(string[] args)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevelopmentHub", "logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Host.UseSerilog();

        // ── Configuration ─────────────────────────────────────────────────────
        builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

        builder.Services.Configure<AppSettings>(builder.Configuration);

        var appSettings = builder.Configuration.Get<AppSettings>()!;

        // ── WebRoot (React static files in production) ─────────────────────────
        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(wwwroot))
            builder.WebHost.UseWebRoot(wwwroot);

        // ── Database ──────────────────────────────────────────────────────────
        builder.Services.AddSingleton<IMongoClient>(_ =>
            new MongoClient(appSettings.MongoConnectionString));

        builder.Services.AddSingleton<DashboardDatabase>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var database = new DashboardDatabase(client, appSettings.MongoDatabaseName);
            database.EnsureIndexesAsync().GetAwaiter().GetResult();
            return database;
        });

        // ── HttpClient for Azure DevOps ───────────────────────────────────────
        builder.Services.AddHttpClient("AzureDevOps", client =>
        {
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });

        // ── Services ──────────────────────────────────────────────────────────
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<IGitService, GitService>();
        builder.Services.AddScoped<ILauncherService, LauncherService>();
        builder.Services.AddScoped<IRepositoryService, RepositoryService>();
        builder.Services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();
        builder.Services.AddSingleton<IUserConfigService, UserConfigService>();

        // ── Background Services ───────────────────────────────────────────────
        builder.Services.AddHostedService<RepositoryScannerService>();

        // ── SignalR ───────────────────────────────────────────────────────────
        builder.Services.AddSignalR();

        // ── MVC / Controllers ─────────────────────────────────────────────────
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(BackendHost).Assembly)
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

        // ── Swagger ───────────────────────────────────────────────────────────
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // ── CORS (development only — not needed when serving from same origin) ─
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("LocalDev", policy =>
            {
                policy.WithOrigins("http://localhost:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        var app = builder.Build();

        // ── Middleware pipeline ───────────────────────────────────────────────
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseRouting();

        if (app.Environment.IsDevelopment())
        {
            app.UseCors("LocalDev");
        }
        else if (Directory.Exists(wwwroot))
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.MapControllers();
        app.MapHub<LogHub>("/hubs/log");

        if (!app.Environment.IsDevelopment() && Directory.Exists(wwwroot))
        {
            app.MapFallbackToFile("index.html");
        }

        return app;
    }
}
