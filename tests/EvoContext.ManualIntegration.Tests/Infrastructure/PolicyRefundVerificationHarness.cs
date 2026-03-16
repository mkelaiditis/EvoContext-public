using System;
using System.Threading;
using System.Threading.Tasks;
using EvoContext.Cli.Utilities;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal sealed class PolicyRefundVerificationHarness
{
    public const string ScenarioId = "policy_refund_v1";
    public const string QueryText = "What is the refund policy for annual subscriptions?";
    public const string RunMode = "run2";
    public const string CategoryTrait = "ManualIntegration";

    public static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan DefaultTotalTimeout = TimeSpan.FromMinutes(5);

    private readonly CliStepRunner _cliStepRunner;
    private readonly EnvironmentPrerequisiteValidator _prerequisiteValidator;
    private readonly VerificationArtifactLocator _artifactLocator;
    private readonly RunVerificationArtifactReader _artifactReader;
    private readonly RunVerificationEvidenceReader _runVerificationEvidenceReader;
    private readonly Run2OutcomeValidator _run2OutcomeValidator;

    public PolicyRefundVerificationHarness()
        : this(QueryText)
    {
    }

    public PolicyRefundVerificationHarness(string queryText)
        : this(
            queryText,
            new CliStepRunner(),
            new EnvironmentPrerequisiteValidator(),
            new VerificationArtifactLocator(),
            new RunVerificationArtifactReader(),
                new RunVerificationEvidenceReader(),
            new Run2OutcomeValidator())
    {
    }

    private PolicyRefundVerificationHarness(
        string queryText,
        CliStepRunner cliStepRunner,
        EnvironmentPrerequisiteValidator prerequisiteValidator,
        VerificationArtifactLocator artifactLocator,
        RunVerificationArtifactReader artifactReader,
        RunVerificationEvidenceReader runVerificationEvidenceReader,
        Run2OutcomeValidator run2OutcomeValidator)
    {
        _cliStepRunner = cliStepRunner ?? throw new ArgumentNullException(nameof(cliStepRunner));
        _prerequisiteValidator = prerequisiteValidator ?? throw new ArgumentNullException(nameof(prerequisiteValidator));
        _artifactLocator = artifactLocator ?? throw new ArgumentNullException(nameof(artifactLocator));
        _artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
        _runVerificationEvidenceReader = runVerificationEvidenceReader ?? throw new ArgumentNullException(nameof(runVerificationEvidenceReader));
        _run2OutcomeValidator = run2OutcomeValidator ?? throw new ArgumentNullException(nameof(run2OutcomeValidator));

        VerificationCase = new ManualIntegrationVerificationCase(
            ScenarioId,
            queryText,
            RunMode,
            CategoryTrait,
            ManualIntegrationWorkspace.CliProjectPath,
            DefaultStepTimeout,
            DefaultTotalTimeout,
            ["OPENAI_API_KEY", "QDRANT_URL"]);
    }

    public PolicyRefundVerificationHarness(
        CliStepRunner cliStepRunner,
        EnvironmentPrerequisiteValidator prerequisiteValidator,
        VerificationArtifactLocator artifactLocator,
        RunVerificationArtifactReader artifactReader,
        RunVerificationEvidenceReader runVerificationEvidenceReader,
        Run2OutcomeValidator run2OutcomeValidator)
    {
        _cliStepRunner = cliStepRunner ?? throw new ArgumentNullException(nameof(cliStepRunner));
        _prerequisiteValidator = prerequisiteValidator ?? throw new ArgumentNullException(nameof(prerequisiteValidator));
        _artifactLocator = artifactLocator ?? throw new ArgumentNullException(nameof(artifactLocator));
        _artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
        _runVerificationEvidenceReader = runVerificationEvidenceReader ?? throw new ArgumentNullException(nameof(runVerificationEvidenceReader));
        _run2OutcomeValidator = run2OutcomeValidator ?? throw new ArgumentNullException(nameof(run2OutcomeValidator));

        VerificationCase = new ManualIntegrationVerificationCase(
            ScenarioId,
            QueryText,
            RunMode,
            CategoryTrait,
            ManualIntegrationWorkspace.CliProjectPath,
            DefaultStepTimeout,
            DefaultTotalTimeout,
            ["OPENAI_API_KEY", "QDRANT_URL"]);
    }

    public ManualIntegrationVerificationCase VerificationCase { get; }

    public VerificationReport CreateReport()
    {
        var report = new VerificationReport(
            VerificationCase,
            CliPathResolver.BuildScenarioCollectionName(VerificationCase.ScenarioId));
        report.SetPrerequisites(_prerequisiteValidator.Validate());
        return report;
    }

    public VerificationArtifactSnapshot CreateTraceArtifactSnapshot()
    {
        return _artifactLocator.CreateTraceArtifactSnapshot(VerificationCase.ScenarioId);
    }

    public async Task<VerificationReport> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var report = CreateReport();
        if (report.FinalStatus == VerificationFinalStatus.Failed)
        {
            return report;
        }

        using var totalTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        totalTimeoutCts.CancelAfter(VerificationCase.TotalTimeout);

        var currentStepName = CliStepName.Embed;
        var currentStepIsPreparation = true;

        try
        {
            var embedStep = await RunEmbedAsync(totalTimeoutCts.Token).ConfigureAwait(false);
            RecordStep(report, embedStep);
            if (!embedStep.Succeeded)
            {
                return report;
            }

            var traceArtifactSnapshot = CreateTraceArtifactSnapshot();
            currentStepName = CliStepName.Run;
            currentStepIsPreparation = false;

            var runStep = await RunScenarioAsync(totalTimeoutCts.Token).ConfigureAwait(false);
            RecordStep(report, runStep);
            if (!runStep.Succeeded)
            {
                return report;
            }

            if (!TryAttachNewTraceArtifact(report, traceArtifactSnapshot, out var artifactError))
            {
                report.MarkFailed("output_capture", artifactError ?? "No new trace artifact was created.");
                return report;
            }

            if (!_runVerificationEvidenceReader.TryRead(
                    report.ArtifactPaths.VerificationEvidencePath!,
                    report.FieldPaths,
                    out var run1Evidence,
                    out artifactError)
                || run1Evidence is null)
            {
                report.MarkFailed("output_capture", artifactError ?? "The verification evidence file could not be read.");
                return report;
            }

            report.SetRun1Evidence(run1Evidence);

            if (!_artifactReader.TryRead(
                    report.ArtifactPaths.TraceArtifactPath!,
                    report.FieldPaths,
                    out var artifact,
                    out artifactError)
                || artifact is null)
            {
                report.MarkFailed("output_capture", artifactError ?? "The trace artifact could not be read.");
                return report;
            }

            report.SetRun2Validation(_run2OutcomeValidator.Validate(artifact, run1Evidence.Answer));
            if (report.FinalStatus == VerificationFinalStatus.Failed)
            {
                return report;
            }

            report.MarkPassed();
            return report;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            report.RecordPhaseTimeout(
                currentStepName,
                currentStepIsPreparation,
                $"Verification timed out after {VerificationCase.TotalTimeout.TotalMinutes:0.#} minute(s) during the {currentStepName.ToString().ToLowerInvariant()} step.");
            return report;
        }
    }

    public Task<CliStepExecution> RunEmbedAsync(CancellationToken cancellationToken = default)
    {
        return _cliStepRunner.RunAsync(
            new CliStepRequest(
                CliStepName.Embed,
                ["embed", "--scenario", VerificationCase.ScenarioId],
                VerificationCase.StepTimeout,
                true),
            cancellationToken);
    }

    public Task<CliStepExecution> RunScenarioAsync(CancellationToken cancellationToken = default)
    {
        return _cliStepRunner.RunAsync(
            new CliStepRequest(
                CliStepName.Run,
                [
                    "run",
                    "--scenario", VerificationCase.ScenarioId,
                    "--query", VerificationCase.QueryText,
                    "--mode", VerificationCase.RunMode
                ],
                VerificationCase.StepTimeout,
                false),
            cancellationToken);
    }

    public void RecordStep(VerificationReport report, CliStepExecution stepExecution)
    {
        ArgumentNullException.ThrowIfNull(report);
        report.AddStep(stepExecution);
    }

    public bool TryAttachNewTraceArtifact(
        VerificationReport report,
        VerificationArtifactSnapshot snapshot,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (!_artifactLocator.TryFindNewTraceArtifact(snapshot, out var discovery, out error) || discovery is null)
        {
            return false;
        }

        report.SetArtifacts(discovery);
        return true;
    }
}