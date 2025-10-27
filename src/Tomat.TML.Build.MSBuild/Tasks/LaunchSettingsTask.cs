using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tomat.TML.Build.MSBuild.Tasks;

public sealed class LaunchSettingsTask : BaseTask
{
    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    protected override bool Run()
    {
        var propertiesDir = Path.Combine(ProjectDirectory, "Properties");
        Log.LogMessage($"Properties directory: \"{propertiesDir}\"");

        var launchSettingsPath = Path.Combine(propertiesDir, "launchSettings.json");
        Log.LogMessage($"launchSettings.json path: {launchSettingsPath}");

        if (!File.Exists(launchSettingsPath))
        {
            Log.LogMessage("Launch settings not found, will generate new file...");
        }
        else
        {
            Log.LogMessage("Launch settings found, will only add or overwrite specific profiles...");
        }

        Directory.CreateDirectory(propertiesDir);

        var launchSettingsJson = File.Exists(launchSettingsPath) ? File.ReadAllText(launchSettingsPath) : "{}";
        try
        {
            var launchSettings = JObject.Parse(launchSettingsJson);

            Log.LogMessage("Creating generated profiles...");

            var profiles = (JObject?)launchSettings["profiles"] ?? new JObject();
            launchSettings["profiles"] = profiles;

            profiles["Terraria Client"] = MakeLaunchSettings("client");
            profiles["Terraria Server"] = MakeLaunchSettings("server");

            Log.LogMessage("Writing generated profiles...");
            File.WriteAllText(launchSettingsPath, launchSettings.ToString(Formatting.Indented));
            return true;
        }
        catch (Exception e)
        {
            Log.LogError($"Failed to read and write launch settings, aborting: {e}");
            return false;
        }

        static JObject MakeLaunchSettings(string mode)
        {
            var settings = new[]
            {
                // The working directory to change to.
                "--working-directory", "\"$(tMLSteamPath).\"",

                // The name of the tModLoader assembly in the new working
                // directory.
                "--binary", "\"$(tMLPath)\"",

                // The mode (client/server).
                "--mode", mode,

                // The requesting mod.
                "--mod", "$(AssemblyName)",

                // Any additional features/plugins for the bootstrapper to
                // enable.
                "--features", "$(TmlBuildBootstrapFeatures)",
            };

            return new JObject
            {
                ["commandName"] = "Executable",
                ["executablePath"] = "$(DotNetName)",
                ["commandLineArgs"] = $"Tomat.TML.ClientBootstrap.dll {string.Join(" ", settings)}",
                ["workingDirectory"] = "$(TmlBuildBootstrapRoot)",
            };
        }
    }
}
