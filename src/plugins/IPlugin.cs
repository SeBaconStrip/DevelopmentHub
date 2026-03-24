using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevelopmentHub.Plugins;

/// <summary>
/// Implement this interface in your plugin assembly to register services
/// and participate in the ASP.NET Core pipeline.
/// </summary>
public interface IPlugin
{
    string Id { get; }

    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes);
}
