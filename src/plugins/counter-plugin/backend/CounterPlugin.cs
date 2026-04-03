using DevelopmentHub.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExamplePlugin.Backend;

public class CounterPlugin : IPlugin
{
    public string Id => "com.example.counter-plugin";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<CounterService>();
        services.AddControllers()
            .AddApplicationPart(typeof(CounterPlugin).Assembly);
    }

    public void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes)
    {
        // Controllers are mapped by the host — nothing extra needed here.
    }
}
