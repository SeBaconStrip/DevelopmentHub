using DevelopmentHub.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExamplePlugin.Backend;

public class HelloPlugin : IPlugin
{
    public string Id => "com.example.hello-plugin";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register this assembly's controllers with the host's MVC pipeline.
        services.AddControllers()
            .AddApplicationPart(typeof(HelloPlugin).Assembly);
    }

    public void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes)
    {
        // Controllers are mapped by the host — nothing extra needed here.
    }
}
