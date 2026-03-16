using System;
using System.Collections.Generic;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal sealed class FieldPathRegistry
{
    private readonly Dictionary<string, string> _fieldPaths = new(StringComparer.Ordinal);

    public void Record(string label, string jsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);

        _fieldPaths[label] = jsonPath;
    }

    public bool TryGet(string label, out string jsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return _fieldPaths.TryGetValue(label, out jsonPath!);
    }

    public IReadOnlyDictionary<string, string> Snapshot()
    {
        return new Dictionary<string, string>(_fieldPaths, StringComparer.Ordinal);
    }

    public void Clear()
    {
        _fieldPaths.Clear();
    }
}