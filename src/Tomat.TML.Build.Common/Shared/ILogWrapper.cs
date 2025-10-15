namespace Tomat.TML.Build.Common.Shared;

/// <summary>
///     Wrapper around a logger implementation to provide a consistent API.
/// </summary>
public interface ILogWrapper
{
    void Info(string message);

    void Error(string message);
}
