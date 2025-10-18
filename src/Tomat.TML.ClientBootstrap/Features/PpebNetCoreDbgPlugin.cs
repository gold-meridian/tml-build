using Tomat.TML.ClientBootstrap.Framework;

namespace Tomat.TML.ClientBootstrap.Features;

// Adapted from:
// https://github.com/ppebb/tml-netcoredbg-patcher

public sealed class PpebNetCoreDbgPlugin : LaunchPlugin
{
    public override string UniqueId => "ppeb.netcoredbg";

    public override void ApplyPatches(LaunchContext ctx)
    {
        // TODO
    }
}
