using EvoContext.Core.AdaptiveMemory;
using EvoContext.Core.Runs;
using EvoContext.Core.Text;
using Serilog;

namespace EvoContext.Cli.Services;

public sealed class RetrievalSummaryRenderer : IRetrievalSummaryRenderer
{
    public void WriteSummary(ILogger logger, RunResult result, int run, int repeat, bool includeAnswer)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(result);

        if (repeat > 1)
        {
            logger.Information("Run {Run}/{Repeat}: {RunId}", run, repeat, result.RunId);
        }

        logger.Information("Retrieved candidates (Qdrant):");
        foreach (var candidate in result.RetrievalSummary.RetrievedCandidates)
        {
            logger.Information("Rank {Rank}", candidate.RankWithinQuery);

            if (candidate.DocumentTitle.HasValue())
            {
                logger.Information("Document: {DocumentTitle:l}", candidate.DocumentTitle);
            }

            if (candidate.Section.HasValue())
            {
                logger.Information("Section:  {Section:l}", candidate.Section);
            }

            logger.Information(
                "doc_id={DocId:l}  chunk_id={ChunkId:l}  chunk_index={ChunkIndex}",
                candidate.DocumentId,
                candidate.ChunkId,
                candidate.ChunkIndex);
            logger.Information("Similarity: {Similarity}", candidate.SimilarityScore);

            if (Run2QueryIdentifier.TryParse(candidate.QueryIdentifier, out var queryOrder))
            {
                if (candidate.QueryText.HasValue())
                {
                    logger.Information("Matched query: {QueryText:l}", candidate.QueryText);
                }
                else
                {
                    logger.Information(
                        "Query source: {Source:l}",
                        Run2QueryIdentifier.IsFeedbackExpansion(queryOrder)
                            ? "feedback expansion"
                            : "base query");
                }
            }
        }

        logger.Information("Retrieved: {Count}", result.RetrievalSummary.RetrievedCandidates.Count);
        logger.Information("Selected: {Count}", result.RetrievalSummary.SelectedChunks.Count);
        logger.Information("Selected chunks:");
        for (var i = 0; i < result.RetrievalSummary.SelectedChunks.Count; i++)
        {
            var chunk = result.RetrievalSummary.SelectedChunks[i];
            var displayIndex = i + 1;

            if (chunk.DocumentTitle.HasValue())
            {
                var title = chunk.Section.HasValue()
                    ? $"{chunk.DocumentTitle} - {chunk.Section}"
                    : chunk.DocumentTitle!;

                logger.Information(
                    "{Index}. {Title:l}  [doc_id={DocId:l} chunk_id={ChunkId:l}]",
                    displayIndex,
                    title,
                    chunk.DocumentId,
                    chunk.ChunkId);
                continue;
            }

            logger.Information(
                "{Index}. doc_id={DocId:l} chunk_id={ChunkId:l} chunk_index={ChunkIndex}",
                displayIndex,
                chunk.DocumentId,
                chunk.ChunkId,
                chunk.ChunkIndex);
        }

        logger.Information("Context chars: {CharCount}", result.RetrievalSummary.ContextPack.CharCount);
        if (includeAnswer)
        {
            logger.Information("Answer: {Answer}", result.Answer ?? string.Empty);
        }

        logger.Information(string.Empty);
    }
}