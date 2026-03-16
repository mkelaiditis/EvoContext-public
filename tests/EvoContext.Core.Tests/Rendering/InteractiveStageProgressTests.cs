using System.Globalization;
using EvoContext.Demo;
using EvoContext.Core.Tracing;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EvoContext.Core.Tests.Rendering;

public sealed class InteractiveStageProgressTests
{
	[Fact]
	public async Task ExecuteStageAsync_WithSpinner_AnnouncesStageAndClearsSpinnerLine()
	{
		var reporter = CreateReporter(out var sink, out var writer);

		var result = await reporter.ExecuteStageAsync(
			"Retrieving context for query...",
			showSpinner: true,
			async cancellationToken =>
			{
				await Task.Delay(15, cancellationToken);
				return 42;
			},
			TestContext.Current.CancellationToken);

		Assert.Equal(42, result);
		Assert.Contains(sink.Messages, message => message == "Retrieving context for query...");

		var spinnerOutput = writer.ToString();
		Assert.Contains("\r⠋", spinnerOutput, StringComparison.Ordinal);
		Assert.Contains("\r \r", spinnerOutput, StringComparison.Ordinal);
	}

	[Fact]
	public void ExecuteStage_WithoutSpinner_AnnouncesStageWithoutSpinnerOutput()
	{
		var reporter = CreateReporter(out var sink, out var writer);

		var value = reporter.ExecuteStage("Selecting top chunks...", () => 7);

		Assert.Equal(7, value);
		Assert.Contains(sink.Messages, message => message == "Selecting top chunks...");
		Assert.Equal(string.Empty, writer.ToString());
	}

	[Fact]
	public async Task ExecuteStageAsync_ClearsSpinner_WhenTheStageFails()
	{
		var reporter = CreateReporter(out var sink, out var writer);

		await Assert.ThrowsAsync<InvalidOperationException>(() => reporter.ExecuteStageAsync(
			"Generating answer...",
			showSpinner: true,
			async cancellationToken =>
			{
				await Task.Delay(15, cancellationToken);
				throw new InvalidOperationException("generation failed");
			},
			TestContext.Current.CancellationToken));

		Assert.Contains(sink.Messages, message => message == "Generating answer...");
		Assert.Contains("\r \r", writer.ToString(), StringComparison.Ordinal);
	}

	private static InteractiveStageProgressReporter CreateReporter(out CollectingSink sink, out StringWriter writer)
	{
		sink = new CollectingSink();
		writer = new StringWriter(CultureInfo.InvariantCulture);

		var screenLogger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.WriteTo.Sink(sink)
			.CreateLogger();

		var spinner = new ConsoleSpinner(
			writer,
			TimeSpan.FromMilliseconds(1),
			(_, cancellationToken) => Task.Delay(1, cancellationToken),
			() => false);

		return new InteractiveStageProgressReporter(screenLogger, spinner);
	}

	private sealed class CollectingSink : ILogEventSink
	{
		public List<string> Messages { get; } = new();

		public void Emit(LogEvent logEvent)
		{
			Messages.Add(logEvent.RenderMessage(CultureInfo.InvariantCulture));
		}
	}
}