using EvoContext.Core.Tracing;
using EvoContext.Infrastructure.Services;

namespace EvoContext.Core.Tests;

public sealed class TraceEmitterTests
{
    [Fact]
    public async Task InMemoryTraceEmitter_AccumulatesEventsInOrder_AndClears()
    {
        var emitter = new InMemoryTraceEmitter();
        var first = new TraceEvent(
            TraceEventType.RunStarted,
            "run-1",
            "policy_refund_v1",
            1,
            new Dictionary<string, object?>());
        var second = new TraceEvent(
            TraceEventType.RunFinished,
            "run-1",
            "policy_refund_v1",
            2,
            new Dictionary<string, object?>());

        await emitter.EmitAsync(first, TestContext.Current.CancellationToken);
        await emitter.EmitAsync(second, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { first, second }, emitter.Events);

        emitter.Clear();
        Assert.Empty(emitter.Events);
    }

    [Fact]
    public async Task CompositeTraceEmitter_EmitsToAllEmittersInSequence()
    {
        var sequence = new List<string>();
        var first = new RecordingTraceEmitter("first", sequence);
        var second = new RecordingTraceEmitter("second", sequence);
        var composite = new CompositeTraceEmitter(first, second);

        var traceEvent = new TraceEvent(
            TraceEventType.RunStarted,
            "run-2",
            "policy_refund_v1",
            1,
            new Dictionary<string, object?>());
        await composite.EmitAsync(traceEvent, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "first", "second" }, sequence);
    }

    private sealed class RecordingTraceEmitter : ITraceEmitter
    {
        private readonly string _name;
        private readonly List<string> _sequence;

        public RecordingTraceEmitter(string name, List<string> sequence)
        {
            _name = name;
            _sequence = sequence;
        }

        public Task EmitAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
        {
            _sequence.Add(_name);
            return Task.CompletedTask;
        }
    }
}
