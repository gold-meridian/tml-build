using Microsoft.Build.Utilities;
using Tomat.TML.Build.Common.Shared;
using Task = System.Threading.Tasks.Task;

namespace Tomat.TML.Build.MSBuild;

public readonly struct Logger(TaskLoggingHelper logger) : ILogWrapper
{
    public Task Info(string message)
    {
        logger.LogMessage(message);
        return Task.CompletedTask;
    }

    public Task Error(string message)
    {
        logger.LogError(message);
        return Task.CompletedTask;
    }
}
