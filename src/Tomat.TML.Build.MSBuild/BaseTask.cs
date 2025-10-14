using System;
using Microsoft.Build.Utilities;

namespace Tomat.TML.Build.MSBuild;

public abstract class BaseTask : Task
{
    public sealed override bool Execute()
    {
        try
        {
            return Run();
        }
        catch (Exception e)
        {
            Log.LogErrorFromException(e);
            return false;
        }
    }

    protected abstract bool Run();
}
