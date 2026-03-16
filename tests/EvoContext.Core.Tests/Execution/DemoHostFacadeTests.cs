using EvoContext.Demo;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace EvoContext.Core.Tests.Execution;

public sealed class DemoHostFacadeTests
{
    [Fact]
    public void Constructor_CreatesFacadeWithRequiredDependencies()
    {
        var configuration = new ConfigurationBuilder().Build();
        var logger = new LoggerConfiguration().CreateLogger();

        var facade = new DemoHostFacade(logger, configuration);

        Assert.NotNull(facade);
    }

    [Fact]
    public void ParseRunMode_ReturnsExpectedAllowRun2Flag()
    {
        Assert.False(DemoHostFacade.ParseRunMode("run1"));
        Assert.True(DemoHostFacade.ParseRunMode("run2"));
    }

    [Fact]
    public void ParseRunMode_ThrowsForInvalidMode()
    {
        Assert.Throws<ArgumentException>(() => DemoHostFacade.ParseRunMode("invalid"));
    }
}
