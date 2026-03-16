using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EvoContext.ManualIntegration.Tests.Infrastructure;

internal enum CliStepName
{
    Embed,
    Run
}

internal enum CliStepStatus
{
    NotStarted,
    Succeeded,
    Failed,
    TimedOut
}

internal sealed record CliStepRequest(
    CliStepName StepName,
    IReadOnlyList<string> Arguments,
    TimeSpan TimeoutBudget,
    bool CombinedPreparationMember)
{
    public string CommandLine => BuildCommandLine(Arguments);

    private static string BuildCommandLine(IReadOnlyList<string> arguments)
    {
        var segments = new[] { "dotnet", "run", "--project", "src/EvoContext.Cli", "--" }
            .Concat(arguments)
            .Select(QuoteIfNeeded);

        return string.Join(" ", segments);
    }

    private static string QuoteIfNeeded(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
    }
}

internal sealed record CliStepExecution(
    CliStepName StepName,
    string CommandLine,
    TimeSpan TimeoutBudget,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    int? ExitCode,
    CliStepStatus Status,
    bool CombinedPreparationMember,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => Status == CliStepStatus.Succeeded && ExitCode == 0;

    public TimeSpan? Duration => FinishedAtUtc is null ? null : FinishedAtUtc.Value - StartedAtUtc;
}

internal sealed class CliStepRunner
{
    public async Task<CliStepExecution> RunAsync(CliStepRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(request.Arguments)
        };

        var startedAtUtc = DateTimeOffset.UtcNow;
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        var waitForExitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(request.TimeoutBudget);

        CliStepStatus status;
        DateTimeOffset? finishedAtUtc;
        int? exitCode;

        try
        {
            var completedTask = await Task.WhenAny(waitForExitTask, timeoutTask).ConfigureAwait(false);
            if (completedTask == timeoutTask)
            {
                status = CliStepStatus.TimedOut;
                KillProcessTree(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                await waitForExitTask.ConfigureAwait(false);
                status = process.ExitCode == 0 ? CliStepStatus.Succeeded : CliStepStatus.Failed;
            }

            finishedAtUtc = DateTimeOffset.UtcNow;
            exitCode = process.HasExited ? process.ExitCode : null;
        }
        catch
        {
            KillProcessTree(process);
            throw;
        }

        var standardOutput = await standardOutputTask.ConfigureAwait(false);
        var standardError = await standardErrorTask.ConfigureAwait(false);

        return new CliStepExecution(
            request.StepName,
            request.CommandLine,
            request.TimeoutBudget,
            startedAtUtc,
            finishedAtUtc,
            exitCode,
            status,
            request.CombinedPreparationMember,
            NormalizeLineEndings(standardOutput),
            NormalizeLineEndings(standardError));
    }

    private static ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = ManualIntegrationWorkspace.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var (key, value) in ManualIntegrationConfiguration.ResolveForwardedValues())
        {
            startInfo.Environment[key] = value;
        }

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add("src/EvoContext.Cli");
        startInfo.ArgumentList.Add("--");

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}