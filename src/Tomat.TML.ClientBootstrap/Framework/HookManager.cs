using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Tomat.TML.ClientBootstrap.Framework;

public sealed class HookManager : IDisposable
{
    private readonly List<Hook> hooks = [];
    private readonly List<ILHook> edits = [];
    private readonly List<Action> applications = [];

    private bool applied;

    public void Add(MethodBase method, Delegate hook)
    {
        if (applied)
        {
            throw new InvalidOperationException("Cannot add hooks after manager has applied once; create a new instance.");
        }

        var detour = new Hook(method, hook, applyByDefault: false);
        hooks.Add(detour);
        applications.Add(detour.Apply);
    }

    public void Modify(MethodBase method, ILContext.Manipulator editCallback)
    {
        if (applied)
        {
            throw new InvalidOperationException("Cannot add hooks after manager has applied once; create a new instance.");
        }

        var edit = new ILHook(method, editCallback, applyByDefault: false);
        edits.Add(edit);
        applications.Add(edit.Apply);
    }

    public void Apply(bool inParallel)
    {
        if (applied)
        {
            throw new InvalidOperationException("Cannot apply hooks after manager has applied once; create a new instance.");
        }

        if (inParallel)
        {
            Parallel.Invoke(applications.ToArray());
        }
        else
        {
            foreach (var apply in applications)
            {
                apply();
            }
        }
    }

    public void Dispose()
    {
        foreach (var hook in hooks)
        {
            hook.Dispose();
        }

        foreach (var edit in edits)
        {
            edit.Dispose();
        }

        hooks.Clear();
        edits.Clear();
    }
}
