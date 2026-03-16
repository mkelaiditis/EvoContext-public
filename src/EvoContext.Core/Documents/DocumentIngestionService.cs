using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EvoContext.Core.Documents;

public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private static readonly UTF8Encoding Utf8Encoding = new(false, true);

    public async Task<IReadOnlyList<PolicyDocument>> LoadDocumentsAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        var result = await LoadDocumentsInternalAsync(folderPath, cancellationToken).ConfigureAwait(false);
        return result.Documents;
    }

    public IReadOnlyList<DocumentChunk> ChunkDocuments(
        IReadOnlyList<PolicyDocument> documents,
        int chunkSizeChars,
        int chunkOverlapChars,
        CancellationToken cancellationToken = default)
    {
        if (documents is null)
        {
            throw new ArgumentNullException(nameof(documents));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var ordered = documents
            .OrderBy(document => document.DocId, StringComparer.Ordinal)
            .ToList();

        var chunks = new List<DocumentChunk>();
        foreach (var document in ordered)
        {
            chunks.AddRange(ChunkDocument(document, chunkSizeChars, chunkOverlapChars));
        }

        var orderedChunks = chunks
            .OrderBy(chunk => chunk.DocumentId, StringComparer.Ordinal)
            .ThenBy(chunk => chunk.ChunkIndex)
            .ToList();

        return orderedChunks;
    }

    public async Task<IngestResult> IngestAsync(
        string folderPath,
        int chunkSizeChars,
        int chunkOverlapChars,
        CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadDocumentsInternalAsync(folderPath, cancellationToken).ConfigureAwait(false);
        var chunks = ChunkDocuments(loadResult.Documents, chunkSizeChars, chunkOverlapChars, cancellationToken);

        return new IngestResult(loadResult.Documents, chunks, loadResult.SkippedFiles.Count, loadResult.SkippedFiles);
    }

    internal static async Task<LoadResult> LoadDocumentsInternalAsync(
        string folderPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Dataset folder path is required.", nameof(folderPath));
        }

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Dataset folder not found: {folderPath}");
        }

        var skippedFiles = new List<string>();
        var candidates = Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => IsMarkdownFile(file) && !IsHiddenFile(file))
            .Select(file => new FileCandidate(file.FullName, file.Name, TryParseDocId(file.Name, out var docId), docId))
            .Where(candidate =>
            {
                if (candidate.IsValid)
                {
                    return true;
                }

                skippedFiles.Add(candidate.FileName);
                return false;
            })
            .OrderBy(candidate => candidate.DocId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.FileName, StringComparer.Ordinal)
            .ToList();

        var documents = new List<PolicyDocument>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawBytes = await File.ReadAllBytesAsync(candidate.Path, cancellationToken).ConfigureAwait(false);
            var rawText = DecodeUtf8(rawBytes);
            var normalizedText = TextNormalization.NormalizeLineEndings(rawText);
            var title = TitleExtraction.ExtractFirstH1(normalizedText);

            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["filename"] = candidate.FileName,
                ["byte_length"] = rawBytes.Length,
                ["char_length"] = normalizedText.Length
            };

            documents.Add(new PolicyDocument(
                candidate.DocId,
                title,
                candidate.Path,
                rawText,
                normalizedText,
                metadata));
        }

        return new LoadResult(documents, skippedFiles);
    }

    private static IReadOnlyList<DocumentChunk> ChunkDocument(
        PolicyDocument document,
        int chunkSizeChars,
        int chunkOverlapChars)
    {
        var normalizedText = document.NormalizedText ?? string.Empty;
        return DocumentChunking.CreateChunks(document.DocId, normalizedText, chunkSizeChars, chunkOverlapChars);
    }

    private static bool IsMarkdownFile(FileInfo file)
    {
        return string.Equals(file.Extension, ".md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHiddenFile(FileInfo file)
    {
        if (file.Name.StartsWith(".", StringComparison.Ordinal))
        {
            return true;
        }

        return (file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
    }

    internal static bool TryParseDocId(string filename, out string docId)
    {
        docId = string.Empty;
        if (filename.Length < 3)
        {
            return false;
        }

        if (!char.IsDigit(filename[0]) || !char.IsDigit(filename[1]) || filename[2] != '_')
        {
            return false;
        }

        docId = filename.Substring(0, 2);
        return true;
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var text = Utf8Encoding.GetString(bytes);
        return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
    }

    internal sealed record LoadResult(
        IReadOnlyList<PolicyDocument> Documents,
        IReadOnlyList<string> SkippedFiles);

    private sealed record FileCandidate(
        string Path,
        string FileName,
        bool IsValid,
        string DocId);
}
