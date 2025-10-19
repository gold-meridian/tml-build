using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using log4net;
using Terraria.ModLoader.Core;
using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap.Features;

// Adapted from:
// https://github.com/ppebb/tml-netcoredbg-patcher

public sealed class PpebNetCoreDbgPlugin : LaunchPlugin
{
    private const string id = "ppeb.netcoredbg";

    public override string UniqueId => id;

    private static readonly ILog logger = LogManager.GetLogger(id);

    public override void ApplyPatches(LaunchContext ctx)
    {
        base.ApplyPatches(ctx);

        // Completely re-implement ModLoadContext::LoadAssemblies based on the
        // referenced patch.  Loads the mod assembly from disk directly if it is
        // available.
        ctx.Hooks.Add(
            typeof(AssemblyManager.ModLoadContext).GetMethod(nameof(AssemblyManager.ModLoadContext.LoadAssemblies), BindingFlags.Public | BindingFlags.Instance)!,
            (AssemblyManager.ModLoadContext self) =>
            {
                try
                {
                    using (self.modFile.Open())
                    {
                        foreach (var dll in self.properties.dllReferences)
                        {
                            self.LoadAssembly(self.modFile.GetBytes("lib/" + dll + ".dll"));
                        }

                        var possibleAssemblyPath = string.IsNullOrEmpty(self.properties.eacPath) ? null : Path.ChangeExtension(self.properties.eacPath, ".dll");
                        logger.Info($"Loading assemblies for \"{self.Name}\" with EAC path: {self.properties.eacPath}");
                        logger.Info($"Assuming possible assembly location: {possibleAssemblyPath ?? "<null>"}");

                        if (Debugger.IsAttached && File.Exists(self.properties.eacPath))
                        {
                            if (File.Exists(possibleAssemblyPath))
                            {
                                logger.Info($"Attempting to load \"{self.Name}\" (assembly=\"{possibleAssemblyPath}\", pdb=\"{self.properties.eacPath}\")...");
                                self.assembly = self.LoadFromAssemblyPath(possibleAssemblyPath);
                                logger.Info($"Successfully loaded \"{self.Name}\"!");
                            }
                            else
                            {
                                logger.Warn($"Could not find assembly for \"{self.Name}\": {possibleAssemblyPath}, loading bytes...");
                                self.assembly = self.LoadAssembly(self.modFile.GetModAssembly(), File.ReadAllBytes(self.properties.eacPath));
                            }
                        }
                        else
                        {
                            self.assembly = self.LoadAssembly(self.modFile.GetModAssembly(), self.modFile.GetModPdb());
                        }

                        var mlc = new MetadataLoadContext(new AssemblyManager.ModLoadContext.MetadataResolver(self));
                        self.loadableTypes = AssemblyManager.GetLoadableTypes(self, mlc);
                    }
                }
                catch (Exception e)
                {
                    e.Data["mod"] = self.Name;
                    throw;
                }
            }
        );
    }
}
