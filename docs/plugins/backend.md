# Backend Development

Plugin backends are .NET 9 class libraries that integrate directly into the host's ASP.NET Core pipeline. They can register services, add controllers, and run background tasks.

## Project structure

```
my-plugin/
└── backend/
    ├── MyPlugin.csproj
    ├── MyPlugin.cs          ← IPlugin implementation
    └── MyController.cs      ← API controller
```

The compiled output goes to `backend-dist/` (next to `manifest.json`) so the manifest can reference it with a stable path.

## `.csproj` setup

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>MyPlugin</AssemblyName>

    <!-- Output next to manifest.json in a fixed location. -->
    <OutputPath>../backend-dist</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <!-- Required for ASP.NET Core types (IApplicationBuilder, IServiceCollection, etc.) -->
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <!--
      Reference the host SDK for compile-time types only.
      The host provides the actual assembly at runtime — do NOT include it in the output.
    -->
    <PackageReference Include="DevelopmentHub.Plugins"
                      Version="1.0.0"
                      ExcludeAssets="runtime" />
  </ItemGroup>

</Project>
```

> `ExcludeAssets="runtime"` ensures `DevelopmentHub.Plugins.dll` is not copied into `backend-dist/`. At runtime, the host's already-loaded assembly is used.

### Installing the package

Download `DevelopmentHub.Plugins.{version}.nupkg` from the GitHub Release, then add a local NuGet source pointing to the folder that contains it:

```sh
dotnet nuget add source /path/to/folder --name DevelopmentHub
```

Or reference it directly in a `nuget.config` next to your `.csproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="DevelopmentHub" value="/path/to/folder" />
  </packageSources>
</configuration>
```

## The `IPlugin` interface

```csharp
public interface IPlugin
{
    string Id { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes);
}
```

| Member | Called when | Purpose |
|---|---|---|
| `Id` | Assembly load | Must match `manifest.json id`. |
| `ConfigureServices` | Startup, before the request pipeline builds | Register services, controllers, background workers. |
| `Configure` | Startup, after services are built | Map custom middleware or endpoints. |

## Minimal `IPlugin` implementation

```csharp
using DevelopmentHub.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyPlugin;

public class MyPlugin : IPlugin
{
    public string Id => "com.yourname.my-plugin";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register this assembly's controllers with the host's MVC pipeline.
        services.AddControllers()
            .AddApplicationPart(typeof(MyPlugin).Assembly);
    }

    public void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes)
    {
        // Controllers are already mapped by the host — nothing required here.
    }
}
```

## Adding API controllers

Controllers in the plugin assembly are registered via `AddApplicationPart`. Use a route prefix under `/api/plugins/{plugin-name}/` to avoid conflicts.

```csharp
using Microsoft.AspNetCore.Mvc;

namespace MyPlugin.Controllers;

[ApiController]
[Route("api/plugins/my-plugin")]
public class MyController : ControllerBase
{
    [HttpGet("items")]
    public IActionResult GetItems()
    {
        return Ok(new[] { new { id = 1, name = "Item A" } });
    }

    [HttpPost("items")]
    public IActionResult CreateItem([FromBody] CreateItemRequest request)
    {
        // ... create logic
        return Created("", new { id = 2, name = request.Name });
    }
}

public record CreateItemRequest(string Name);
```

## Registering services

Use `ConfigureServices` exactly as you would in a standard ASP.NET Core `Startup`:

```csharp
public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // Register controllers
    services.AddControllers()
        .AddApplicationPart(typeof(MyPlugin).Assembly);

    // Register a scoped service
    services.AddScoped<IMyService, MyService>();

    // Register a singleton
    services.AddSingleton<MyCache>();

    // Register a background worker
    services.AddHostedService<MyBackgroundWorker>();
}
```

Injecting services into controllers works exactly as in standard ASP.NET Core:

```csharp
[ApiController]
[Route("api/plugins/my-plugin")]
public class MyController(IMyService myService) : ControllerBase
{
    [HttpGet("data")]
    public IActionResult GetData() => Ok(myService.GetData());
}
```

## Adding custom middleware or endpoint routes

Use `Configure` to add middleware that runs for every request, or map endpoints directly:

```csharp
public void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes)
{
    // Map a minimal API endpoint
    routes.MapGet("/api/plugins/my-plugin/ping", () => Results.Ok("pong"));
}
```

## Reading configuration

The `IConfiguration` passed to `ConfigureServices` is the host's full configuration object. Plugins can read application settings from it:

```csharp
public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    var pluginsPath = configuration["AppSettings:PluginsPath"];
    // ...
}
```

## Assembly isolation

Each plugin assembly loads into its own `AssemblyLoadContext` (`PluginAssemblyLoadContext`). When the context resolves an assembly:

1. If the host has already loaded an assembly with that name, the host copy is returned. This ensures a single copy of `Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`, and other shared framework assemblies.
2. Otherwise, the assembly is resolved from the plugin's own output directory.

**Consequences:**

- Do not depend on NuGet packages that the host also references unless you are certain the versions are compatible. The host version wins.
- Third-party packages that are not in the host will be loaded from `backend-dist/` and bundled alongside the plugin DLL.

## Build and deploy

```sh
dotnet build backend/MyPlugin.csproj
```

Output lands in `backend-dist/MyPlugin.dll`.

For a release build:

```sh
dotnet build backend/MyPlugin.csproj -c Release
```

Restart the host to reload the plugin.

## `.gitignore`

Add these lines to avoid committing build artifacts:

```
plugins/*/backend-dist/
plugins/*/backend/bin/
plugins/*/backend/obj/
```
