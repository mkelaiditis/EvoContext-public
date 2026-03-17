using System.Reflection;
using EvoContext.Infrastructure.Services;
using Qdrant.Client.Grpc;

namespace EvoContext.Core.Tests.Retrieval;

public sealed class RetrievalProvenanceMappingTests
{
    [Fact]
    public void GetOptionalPayloadString_ReturnsNull_WhenPayloadKeyIsAbsent()
    {
        var point = new ScoredPoint();

        var mapped = InvokeGetOptionalPayloadString(point, "document_title");

        Assert.Null(mapped);
    }

    [Fact]
    public void GetOptionalPayloadString_TrimsStringValues()
    {
        var point = new ScoredPoint();
        point.Payload["document_title"] = "  Refund Policy  ";

        var mapped = InvokeGetOptionalPayloadString(point, "document_title");

        Assert.Equal("Refund Policy", mapped);
    }

    [Fact]
    public void GetOptionalPayloadString_ReturnsNull_ForWhitespaceOnlyString()
    {
        var point = new ScoredPoint();
        point.Payload["section"] = "   ";

        var mapped = InvokeGetOptionalPayloadString(point, "section");

        Assert.Null(mapped);
    }

    private static string? InvokeGetOptionalPayloadString(ScoredPoint point, string key)
    {
        var method = typeof(RetrievalService)
            .GetMethod("GetOptionalPayloadString", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        return (string?)method!.Invoke(null, new object[]
        {
            point,
            key
        });
    }
}
