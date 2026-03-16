using EvoContext.Core.Retrieval;

namespace EvoContext.Core.Context;

public interface IContextPacker
{
    ContextPack Pack(IReadOnlyList<RetrievalCandidate> selectedChunks);
}
