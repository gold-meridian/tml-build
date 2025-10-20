using System.Collections;
using System.Collections.Generic;

namespace Tomat.Parsing.Diagnostics;

public sealed class DiagnosticsCollection : IEnumerable<ReportableDiagnostic>
{
    public bool HasErrors { get; private set; }

    private readonly List<ReportableDiagnostic> diagnostics = [];

    public DiagnosticsCollection Add(ReportableDiagnostic diagnostic)
    {
        HasErrors |= diagnostic.IsError;
        diagnostics.Add(diagnostic);
        return this;
    }

    public DiagnosticsCollection AddRange(IEnumerable<ReportableDiagnostic> values)
    {
        // TODO: Can use AddRange instead, but we need to get every value here
        //       anyway.
        foreach (var diagnostic in values)
        {
            HasErrors |= diagnostic.IsError;
            diagnostics.Add(diagnostic);
        }

        return this;
    }

    public DiagnosticsCollection AddRange(DiagnosticsCollection collection)
    {
        HasErrors |= collection.HasErrors;
        diagnostics.AddRange(collection.diagnostics);
        return this;
    }

#region Enumerable
    public IEnumerator<ReportableDiagnostic> GetEnumerator()
    {
        return diagnostics.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)diagnostics).GetEnumerator();
    }
#endregion
}
