using Microsoft.Build.Utilities;
using Tomat.TML.Build.Common.Shared;

namespace Tomat.TML.Build.MSBuild;

public readonly struct Logger(TaskLoggingHelper logger) : ILogWrapper
{
    public void Info(string message)
    {
        logger.LogMessage(message);
    }

    public void Error(string message)
    {
        logger.LogError(message);
    }
}
