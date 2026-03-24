namespace DevelopmentHub.Plugins;

public interface IPluginRegistry
{
    IReadOnlyList<LoadedPlugin> Plugins { get; }
    LoadedPlugin? GetById(string id);
}

public class PluginRegistry : IPluginRegistry
{
    public IReadOnlyList<LoadedPlugin> Plugins { get; }

    public PluginRegistry(IReadOnlyList<LoadedPlugin> plugins)
    {
        Plugins = plugins;
    }

    public LoadedPlugin? GetById(string id) =>
        Plugins.FirstOrDefault(p => p.Manifest.Id == id);
}
