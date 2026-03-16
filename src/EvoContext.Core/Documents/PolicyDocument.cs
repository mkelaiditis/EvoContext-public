using System.Collections.Generic;

namespace EvoContext.Core.Documents;

public sealed record PolicyDocument(
    string DocId,
    string Title,
    string SourcePath,
    string RawText,
    string NormalizedText,
    IReadOnlyDictionary<string, object> Metadata);
