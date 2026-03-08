using DevelopmentHub.Api.BackgroundServices;
using DevelopmentHub.Api.Configuration;
using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Hubs;
using DevelopmentHub.Api.Services;
using MongoDB.Driver;
using System.Net.Http.Headers;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<AppSettings>(builder.Configuration);

var appSettings = builder.Configuration.Get<AppSettings>()!;

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(appSettings.MongoConnectionString));

builder.Services.AddSingleton<DashboardDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var database = new DashboardDatabase(client, appSettings.MongoDatabaseName);
    database.EnsureIndexesAsync().GetAwaiter().GetResult();
    return database;
});

// ── HttpClient for Azure DevOps ───────────────────────────────────────────────
// Auth header is applied per-request inside AzureDevOpsService using the PAT stored in MongoDB.
builder.Services.AddHttpClient("AzureDevOps", client =>
{
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IGitService, GitService>();
builder.Services.AddScoped<ILauncherService, LauncherService>();
builder.Services.AddScoped<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();
builder.Services.AddSingleton<IUserConfigService, UserConfigService>();

// ── Background Services ───────────────────────────────────────────────────────
builder.Services.AddHostedService<RepositoryScannerService>();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── MVC / Controllers ─────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // required for SignalR
    });
});

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseCors("LocalDev");

app.MapControllers();
app.MapHub<LogHub>("/hubs/log");

app.Run();
