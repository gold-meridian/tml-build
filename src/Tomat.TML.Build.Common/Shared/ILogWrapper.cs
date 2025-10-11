using System.Threading.Tasks;

namespace Tomat.TML.Build.Common.Shared;

/// <summary>
///     Wraps an arbitrary logger implementation.
/// </summary>
public interface ILogWrapper
{
    Task Info(string message);

    Task Error(string message);
}
