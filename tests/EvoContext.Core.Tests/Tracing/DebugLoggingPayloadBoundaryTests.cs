using System.Globalization;
using EvoContext.Demo;
using EvoContext.Core.Evaluation;
using EvoContext.Infrastructure.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using CoreContextPack = EvoContext.Core.Context.ContextPack;

namespace EvoContext.Core.Tests.Tracing;

public sealed class DebugLoggingPayloadBoundaryTests
{
    [Fact]
    public async Task InteractiveReporter_SuppressesAnnouncementsAndSpinner_WhenOutputIsRedirected()
    {
        var sink = new CollectingSink();
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var spinner = new ConsoleSpinner(
            writer,
            TimeSpan.FromMilliseconds(1),
            (_, cancellationToken) => Task.Delay(1, cancellationToken),
            () => true);
        var reporter = new InteractiveStageProgressReporter(logger, spinner);

        var invoked = false;
        await reporter.ExecuteStageAsync(
            "Generating answer...",
            showSpinner: true,
            cancellationToken =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.True(invoked);
        Assert.Empty(sink.Messages);
        Assert.Equal(string.Empty, writer.ToString());
    }

    [Fact]
    public async Task AnswerGenerationService_DebugLogs_DoNotContainAnswerOrContextText()
    {
        const string sensitiveAnswer = "SENSITIVE_ANSWER_PAYLOAD";
        const string sensitiveContext = "SENSITIVE_CONTEXT_PAYLOAD";

        var logger = CreateLogger(out var sink);
        var service = new AnswerGenerationService(
            new Phase3PromptBuilder(),
            new StubAnswerGenerator(sensitiveAnswer),
            new AnswerFormatValidator(),
            logger);

        await service.GenerateAsync(
            "What is the refund policy?",
            new CoreContextPack(sensitiveContext, sensitiveContext.Length, 1, 2200),
            PolicyRefundEvaluator.PolicyScenarioId,
            cancellationToken: TestContext.Current.CancellationToken);

        var debugEvent = Assert.Single(sink.Events, evt => evt.MessageTemplate.Text == "Answer generation completed");
        Assert.Equal(sensitiveAnswer.Length, GetScalarInt(debugEvent, "answer_length"));
        Assert.False(EventContains(debugEvent, sensitiveAnswer));
        Assert.False(EventContains(debugEvent, sensitiveContext));
        Assert.DoesNotContain("raw_model_output", debugEvent.Properties.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("prompt_context", debugEvent.Properties.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void EvaluationDebugLogs_DoNotContainAnswerOrContextText()
    {
        const string sensitiveAnswer = "SENSITIVE_POLICY_ANSWER";
        const string sensitiveContext = "SENSITIVE_POLICY_CONTEXT";

        var logger = CreateLogger(out var sink);
        var evaluator = new Phase4Evaluator(logger: logger);

        evaluator.Evaluate(new EvaluationInput(
            "run-001",
            PolicyRefundEvaluator.PolicyScenarioId,
            sensitiveAnswer,
            new List<SelectedChunk>
            {
                new("doc-01", "doc-01-000", 0, sensitiveContext)
            }));

        Assert.All(sink.Events, evt =>
        {
            Assert.False(EventContains(evt, sensitiveAnswer));
            Assert.False(EventContains(evt, sensitiveContext));
        });

        var startEvent = Assert.Single(sink.Events, evt => evt.MessageTemplate.Text == "Phase 4 evaluation started");
        Assert.Equal(sensitiveAnswer.Length, GetScalarInt(startEvent, "answer_length"));
        Assert.Equal(sensitiveContext.Length, GetScalarInt(startEvent, "context_length"));
    }

    [Fact]
    public void RunbookDetectorDebugLogs_DoNotContainAnswerOrContextText()
    {
        const string sensitiveAnswer = "SENSITIVE_RUNBOOK_ANSWER";
        const string sensitiveContext = "SENSITIVE_RUNBOOK_CONTEXT";

        var logger = CreateLogger(out var sink);
        var stepEvaluator = new Runbook502StepEvaluator(logger);
        var hallucinationDetector = new Runbook502HallucinationDetector(logger);

        stepEvaluator.Evaluate(sensitiveAnswer, sensitiveContext);
        hallucinationDetector.Evaluate(sensitiveAnswer, sensitiveContext);

        Assert.All(sink.Events, evt =>
        {
            Assert.False(EventContains(evt, sensitiveAnswer));
            Assert.False(EventContains(evt, sensitiveContext));
        });
    }

    private static ILogger CreateLogger(out CollectingSink sink)
    {
        sink = new CollectingSink();
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();
    }

    private static bool EventContains(LogEvent logEvent, string text)
    {
        if (logEvent.RenderMessage(CultureInfo.InvariantCulture).Contains(text, StringComparison.Ordinal))
        {
            return true;
        }

        return logEvent.Properties.Values.Any(value => PropertyContains(value, text));
    }

    private static bool PropertyContains(LogEventPropertyValue value, string text)
    {
        return value switch
        {
            ScalarValue scalar when scalar.Value is string stringValue => stringValue.Contains(text, StringComparison.Ordinal),
            SequenceValue sequence => sequence.Elements.Any(element => PropertyContains(element, text)),
            StructureValue structure => structure.Properties.Any(property => PropertyContains(property.Value, text)),
            DictionaryValue dictionary => dictionary.Elements.Any(element => PropertyContains(element.Key, text) || PropertyContains(element.Value, text)),
            _ => false
        };
    }

    private static int GetScalarInt(LogEvent logEvent, string propertyName)
    {
        var scalar = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        return Convert.ToInt32(scalar.Value, CultureInfo.InvariantCulture);
    }

    private sealed class StubAnswerGenerator : IAnswerGenerator
    {
        private readonly string _answer;

        public StubAnswerGenerator(string answer)
        {
            _answer = answer;
        }

        public Task<string> GenerateAnswerAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_answer);
        }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public IReadOnlyList<string> Messages => Events
            .Select(logEvent => logEvent.RenderMessage(CultureInfo.InvariantCulture))
            .ToList();

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}