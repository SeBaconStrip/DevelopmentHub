using System.Reflection;
using System.Runtime.Loader;

namespace DevelopmentHub.Plugins;

/// <summary>
/// Isolated load context for each plugin assembly.
/// Falls back to the host's default context for shared framework assemblies,
/// ensuring only one copy of ASP.NET Core / Microsoft.Extensions.* is loaded.
/// </summary>
public class PluginAssemblyLoadContext(string pluginAssemblyPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Let host assemblies resolve from the default context to avoid type-identity mismatches.
        var hostAssembly = Default.Assemblies
            .FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (hostAssembly is not null)
            return null; // null = fall through to default context

        var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
        return resolved is not null ? LoadFromAssemblyPath(resolved) : null;
    }
}
