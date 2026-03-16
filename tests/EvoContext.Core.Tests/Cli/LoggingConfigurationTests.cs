using System.Diagnostics;
using System.Text.Json;

namespace EvoContext.Core.Tests.Cli;

public sealed class LoggingConfigurationTests
{
    [Fact]
    public void AppSettings_KeepConsoleAtInformationAndEnableDebugFileCaptureInDevelopment()
    {
        var baseSettings = ReadJsonDocument(Path.Combine(
            TestDatasetPaths.RepoRoot,
            "src",
            "EvoContext.Cli",
            "appsettings.json"));
        var developmentSettings = ReadJsonDocument(Path.Combine(
            TestDatasetPaths.RepoRoot,
            "src",
            "EvoContext.Cli",
            "appsettings.Development.json"));

        var serilog = baseSettings.RootElement.GetProperty("Serilog");
        Assert.Equal("Information", serilog.GetProperty("MinimumLevel").GetProperty("Default").GetString());

        var writeTo = serilog.GetProperty("WriteTo").EnumerateArray().ToList();
        var consoleSink = writeTo.Single(item => item.GetProperty("Name").GetString() == "Console");
        var fileSink = writeTo.Single(item => item.GetProperty("Name").GetString() == "File");

        Assert.Equal("Information", consoleSink.GetProperty("Args").GetProperty("restrictedToMinimumLevel").GetString());
        Assert.Equal("Debug", fileSink.GetProperty("Args").GetProperty("restrictedToMinimumLevel").GetString());
        Assert.Equal("logs/evocontext-.log", fileSink.GetProperty("Args").GetProperty("path").GetString());
        Assert.Equal("Debug", developmentSettings.RootElement.GetProperty("Serilog").GetProperty("MinimumLevel").GetProperty("Default").GetString());
    }

    [Fact]
    public void Help_InDevelopment_DoesNotEmitDebugOutputToScreen()
    {
        var result = RunCli("help", "Development");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("EvoContext CLI", result.Output, StringComparison.Ordinal);
        Assert.Contains("Commands:", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("CLI command selected", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("{\"@t\"", result.Output, StringComparison.Ordinal);
    }

    private static JsonDocument ReadJsonDocument(string path)
    {
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static CliResult RunCli(string arguments, string environmentName)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project src/EvoContext.Cli -- " + arguments,
                WorkingDirectory = TestDatasetPaths.RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.Environment["DOTNET_ENVIRONMENT"] = environmentName;

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new CliResult(process.ExitCode, stdout + stderr);
    }

    private sealed record CliResult(int ExitCode, string Output);
}