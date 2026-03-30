using DevelopmentHub.Api.BackgroundServices;
using DevelopmentHub.Api.Configuration;
using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Hubs;
using DevelopmentHub.Api.Logging;
using DevelopmentHub.Api.Services;
using DevelopmentHub.Api.Services.Todos.Sync;
using DevelopmentHub.Api.Services.Todos.Sync.MicrosoftTodo;
using DevelopmentHub.Plugins;
using DevelopmentHub.Workflow;
using DevelopmentHub.Workflow.Executors;
using Serilog;
using Serilog.Events;
using System.Net.WebSockets;

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
            .MinimumLevel.Override("DevelopmentHub.Api", LogEventLevel.Debug)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With(new CallerInfoEnricher())
            .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {CallerClass}.{CallerMethod} {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {CallerClass}.{CallerMethod} {Message:lj}{NewLine}{Exception}")
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
        var liteDbPath = string.IsNullOrWhiteSpace(appSettings.LiteDbPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DevelopmentHub", "developmenthub.db")
            : appSettings.LiteDbPath;

        builder.Services.AddSingleton(new DashboardDatabase(liteDbPath));

        // ── HttpClient for Azure DevOps ───────────────────────────────────────
        builder.Services.AddHttpClient("AzureDevOps", client =>
        {
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });
        builder.Services.AddHttpClient("GitHub", client =>
        {
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("User-Agent", "DevelopmentHub");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        });

        // ── Services ──────────────────────────────────────────────────────────
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<IBrowserTabCommandBridge, BrowserTabCommandBridge>();
        builder.Services.AddScoped<IGitService, GitService>();
        builder.Services.AddScoped<ILauncherService, LauncherService>();
        builder.Services.AddScoped<IPullRequestService, PullRequestService>();
        builder.Services.AddScoped<IPullRequestProvider, AzureDevOpsPullRequestProvider>();
        builder.Services.AddScoped<IPullRequestProvider, GitHubPullRequestProvider>();
        builder.Services.AddScoped<IRepositoryService, RepositoryService>();
        builder.Services.AddScoped<ITodoService, TodoService>();
        builder.Services.AddSingleton<IUserConfigService, UserConfigService>();
        builder.Services.AddSingleton<IMicrosoftTodoAuthService, MicrosoftTodoAuthService>();
        builder.Services.AddSingleton<ITodoSyncProvider, MicrosoftTodoSyncProvider>();
        builder.Services.AddSingleton<ITodoSyncService, TodoSyncService>();
        builder.Services.AddHostedService<TodoSyncBackgroundService>();
        builder.Services.AddHttpClient("MicrosoftGraph");
        builder.Services.AddSingleton<IWorkflowStepExecutor, DownloadFileExecutor>();
        builder.Services.AddSingleton<IWorkflowStepExecutor, DownloadGitHubReleaseAssetExecutor>();
        builder.Services.AddSingleton<IWorkflowStepExecutor, DownloadAzureDevOpsPipelineArtifactAssetExecutor>();
        builder.Services.AddSingleton<IWorkflowStepExecutor, ExtractArchiveExecutor>();
        builder.Services.AddSingleton<IWorkflowStepExecutor, RunExecutableExecutor>();
        builder.Services.AddSingleton<IWorkflowStepExecutor, PatchJsonExecutor>();
        builder.Services.AddSingleton<IWorkflowStepExecutor, RestartWindowsServiceExecutor>();
        builder.Services.AddSingleton<IWorkflowStepExecutor, CopyExecutor>();
        builder.Services.AddSingleton<IWorkflowStepExecutor, CallWorkflowExecutor>();
        builder.Services.AddSingleton<IWorkflowService, WorkflowService>();
        builder.Services.AddSingleton<ApiTokenService>();

        // ── Plugins ───────────────────────────────────────────────────────────
        var pluginsPath = string.IsNullOrWhiteSpace(appSettings.PluginsPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DevelopmentHub", "plugins")
            : appSettings.PluginsPath;

        var pluginLoader = new PluginLoader(
            LoggerFactory.Create(b => b.AddSerilog()).CreateLogger<PluginLoader>());
        var loadedPlugins = pluginLoader.LoadAll(pluginsPath);

        builder.Services.AddSingleton<IPluginRegistry>(new PluginRegistry(loadedPlugins));

        foreach (var lp in loadedPlugins)
            lp.Plugin?.ConfigureServices(builder.Services, builder.Configuration);

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

            options.AddPolicy("BrowserExtension", policy =>
            {
                policy.SetIsOriginAllowed(origin =>
                      {
                          if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                          {
                              return uri.Scheme is "chrome-extension"
                                  or "ms-browser-extension"
                                  or "moz-extension"
                                  or "safari-extension";
                          }
                          return false;
                      })
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        var app = builder.Build();
        var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        startupLogger.LogInformation(
            "DevelopmentHub backend starting. Version={Version} Environment={Environment} ContentRoot={ContentRoot} WebRoot={WebRoot} LiteDbPath={LiteDbPath} LogDir={LogDir}",
            ResolveBackendVersion(),
            app.Environment.EnvironmentName,
            app.Environment.ContentRootPath,
            app.Environment.WebRootPath ?? "(none)",
            liteDbPath,
            logDir);

        // ── Middleware pipeline ───────────────────────────────────────────────
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        });
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseRouting();
        app.UseWebSockets();
        app.UseCors("LocalDev");

        // Require the per-process token on all API and hub routes so that other local processes
        // cannot call the API without the user's knowledge. The token is injected into the WebView
        // as window.__devHubToken and is only known to this process.
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path.Value ?? "";
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
            {
                var tokenSvc = ctx.RequestServices.GetRequiredService<ApiTokenService>();

                // Standard API calls send the token in a custom header.
                var token = ctx.Request.Headers["X-Dev-Hub-Token"].FirstOrDefault();

                // SignalR WebSocket upgrade cannot carry custom headers; it sends the token as
                // a query parameter when accessTokenFactory is configured on the client.
                if (token is null)
                    token = ctx.Request.Query["access_token"].FirstOrDefault();

                // SignalR negotiate (HTTP) sends the token as Authorization: Bearer <token>.
                if (token is null)
                {
                    var auth = ctx.Request.Headers.Authorization.FirstOrDefault() ?? "";
                    if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        token = auth["Bearer ".Length..];
                }

                if (token != tokenSvc.Token)
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }

            await next();
        });

        if (app.Environment.IsDevelopment())
        {
        }
        else if (Directory.Exists(wwwroot))
        {
            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                        ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
                }
            });
        }

        // ── Plugin middleware/routes ──────────────────────────────────────────
        foreach (var lp in loadedPlugins)
            lp.Plugin?.Configure(app, app);

        app.MapControllers();
        app.MapHub<LogHub>("/hubs/log");
        app.Map("/ws/browser-tab-bridge", async context =>
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("BrowserTabBridgeEndpoint");

            if (!context.WebSockets.IsWebSocketRequest)
            {
                logger.LogWarning(
                    "Rejected non-WebSocket request for browser tab bridge from {RemoteIp}",
                    context.Connection.RemoteIpAddress?.ToString() ?? "(unknown)");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("WebSocket connection expected.");
                return;
            }

            logger.LogInformation(
                "Accepting browser tab bridge WebSocket from {RemoteIp} UserAgent={UserAgent}",
                context.Connection.RemoteIpAddress?.ToString() ?? "(unknown)",
                context.Request.Headers.UserAgent.ToString());

            var bridge = context.RequestServices.GetRequiredService<IBrowserTabCommandBridge>();
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await bridge.HandleConnectionAsync(webSocket, context.RequestAborted);
        });

        startupLogger.LogInformation(
            "DevelopmentHub backend configured. BrowserTabBridgePath={BridgePath} LogHubPath={LogHubPath}",
            "/ws/browser-tab-bridge",
            "/hubs/log");

        if (!app.Environment.IsDevelopment() && Directory.Exists(wwwroot))
        {
            app.MapFallbackToFile("index.html");
        }

        return app;
    }

    private static string ResolveBackendVersion()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidateDirectories = new[]
        {
            baseDirectory,
            Directory.GetParent(baseDirectory)?.FullName,
            Directory.GetParent(Directory.GetParent(baseDirectory ?? string.Empty)?.FullName ?? string.Empty)?.FullName,
            Directory.GetCurrentDirectory()
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in candidateDirectories)
        {
            var versionFile = Path.Combine(directory!, "version.txt");
            if (!File.Exists(versionFile))
                continue;

            var version = File.ReadAllText(versionFile).Trim();
            if (!string.IsNullOrWhiteSpace(version))
                return version;
        }

        return typeof(BackendHost).Assembly
            .GetName()
            .Version?
            .ToString() ?? "unknown";
    }
}
