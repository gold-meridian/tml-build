using System.Collections.Concurrent;

namespace Tomat.TML.ClientBootstrap.Framework;

public static class ObjectHolder
{
    // ReSharper disable once CollectionNeverQueried.Local
    private static readonly ConcurrentBag<object> graveyard = [];

    public static void Add(object obj)
    {
        graveyard.Add(obj);
    }
}
