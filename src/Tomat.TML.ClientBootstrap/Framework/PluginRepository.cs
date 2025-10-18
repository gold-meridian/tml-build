using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Tomat.TML.ClientBootstrap.Framework;

public sealed class PluginRepository
{
    private readonly Dictionary<string, LaunchPlugin> plugins = [];

    public bool TryGetPlugin(
        string pluginName,
        [NotNullWhen(returnValue: true)] out LaunchPlugin? plugin
    )
    {
        return this.plugins.TryGetValue(pluginName, out plugin);
    }

    public void AddPluginsFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsSubclassOf(typeof(LaunchPlugin)) || type.GetConstructor([]) is null)
            {
                continue;
            }

            var instance = Activator.CreateInstance(type);
            if (instance is not LaunchPlugin plugin)
            {
                continue;
            }

            plugins[plugin.UniqueId] = plugin;
        }
    }

    public void AddPluginsFromThisAssembly()
    {
        AddPluginsFromAssembly(Assembly.GetCallingAssembly());
    }
}
