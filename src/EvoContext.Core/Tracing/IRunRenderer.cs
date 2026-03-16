namespace EvoContext.Core.Tracing;

public interface IRunRenderer
{
    void OnEvent(TraceEvent evt);

    void OnRunComplete(RunSummary summary);
}
