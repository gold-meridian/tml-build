using log4net;
using Microsoft.Xna.Framework;
using Terraria;

namespace Tomat.TML.ClientBootstrap.Features.AssetHotReloading;

public readonly ref struct GameLogWrapper(ILog log)
{
    public void Info(object? message)
    {
        log.Info(message);
        NewText(message, Color.White);
    }

    public void Warn(object? message)
    {
        log.Warn(message);
        NewText(message, Color.Yellow);
    }

    public void Error(object? message)
    {
        log.Error(message);
        NewText(message, Color.Red);
    }

    private void NewText(object? message, Color color)
    {
        if (!Main.gameMenu)
        {
            return;
        }

        Main.NewText($"[{log.Logger.Name} (tml-build)]" + message, color);
    }
}
