using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Newtonsoft.Json;

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
            var launchSettings = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(launchSettingsJson);
            if (launchSettings is null)
            {
                Log.LogMessage("Failed to deserialize launchSettings.json, aborting...");
                return false;
            }

            Log.LogMessage("Creating generated profiles...");

            if (!launchSettings.TryGetValue("profiles", out var profiles))
            {
                launchSettings["profiles"] = profiles = [];
            }

            profiles["Terraria Client"] = new Dictionary<string, string>
            {
                { "commandName", "Executable" },
                { "executablePath", "$(DotNetName)" },
                { "commandLineArgs", "Tomat.TML.ClientBootstrap.dll --working-directory \"$(tMLSteamPath).\" --binary \"$(tMLPath)\" --mode client --mod $(AssemblyName) --features $(TmlBuildBootstrapFeatures)" },
                { "workingDirectory", "$(TmlBuildBootstrapRoot)" },
            };

            profiles["Terraria Server"] = new Dictionary<string, string>
            {
                { "commandName", "Executable" },
                { "executablePath", "$(DotNetName)" },
                { "commandLineArgs", "Tomat.TML.ClientBootstrap.dll --working-directory \"$(tMLSteamPath).\" --binary \"$(tMLPath)\" --mode server --mod $(AssemblyName) --features $(TmlBuildBootstrapFeatures)" },
                { "workingDirectory", "$(TmlBuildBootstrapRoot)" },
            };

            Log.LogMessage("Writing generated profiles...");
            File.WriteAllText(launchSettingsPath, JsonConvert.SerializeObject(launchSettings, Formatting.Indented));
            return true;
        }
        catch (Exception e)
        {
            Log.LogError($"Failed to read and write launch settings, aborting: {e}");
            return false;
        }
    }
}
