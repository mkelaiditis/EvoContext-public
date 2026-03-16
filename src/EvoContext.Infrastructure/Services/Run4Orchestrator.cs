using EvoContext.Core.Evaluation;

namespace EvoContext.Infrastructure.Services;

public sealed class Run4Orchestrator
{
    private readonly ScenarioEvaluatorDispatcher _dispatcher;

    public Run4Orchestrator(ScenarioEvaluatorDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task<EvaluationResult> ExecuteAsync(
        EvaluationInput input,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var result = _dispatcher.Evaluate(input);
        return Task.FromResult(result);
    }
}
