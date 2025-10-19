using System.Collections.Frozen;
using System.Collections.Generic;

namespace Tomat.TML.ClientBootstrap.Framework;

public sealed class PluginArguments : Dictionary<string, string?>
{
    internal PluginArguments(Dictionary<string, string?> source) : base(source, source.Comparer) { }
}

public sealed class ArgumentRepository(Dictionary<string, Dictionary<string, string?>> prefixedArguments)
{
    private readonly FrozenDictionary<string, Dictionary<string, string?>> prefixedArguments = prefixedArguments.ToFrozenDictionary();
    private readonly Dictionary<string, PluginArguments> cachedArguments = [];

    public PluginArguments GetArguments(LaunchPlugin plugin)
    {
        return GetArguments(plugin.UniqueId);
    }

    public PluginArguments GetArguments(string pluginId)
    {
        if (cachedArguments.TryGetValue(pluginId, out var args))
        {
            return args;
        }

        return cachedArguments[pluginId] = new PluginArguments(prefixedArguments.GetValueOrDefault(pluginId) ?? []);
    }
}
