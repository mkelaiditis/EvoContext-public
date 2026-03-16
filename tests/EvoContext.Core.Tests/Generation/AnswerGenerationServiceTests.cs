using EvoContext.Infrastructure.Services;
using Serilog;
using CoreContextPack = EvoContext.Core.Context.ContextPack;
using EvoContext.Core.Evaluation;
using EvoContext.Core.Evidence;

namespace EvoContext.Core.Tests;

public sealed class AnswerGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_BuildsPromptFromTemplate()
    {
        var builder = new Phase3PromptBuilder();
        var generator = new FakeAnswerGenerator(TestAnswerBuilder.BuildAnswer(170));
        var validator = new AnswerFormatValidator();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        var service = new AnswerGenerationService(builder, generator, validator, logger);

        var contextPack = new CoreContextPack("context text", 12, 1, 2200);

        var result = await service.GenerateAsync("question text", contextPack, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Phase3PromptTemplate.SystemInstructions, generator.SystemPrompt);
        Assert.Contains("Context:\ncontext text", generator.UserPrompt);
        Assert.Contains("Question:\nquestion text", generator.UserPrompt);
        Assert.Contains("Answer Instructions:", generator.UserPrompt);
        Assert.Equal(TestAnswerBuilder.BuildAnswer(170), result.Answer);
        Assert.Equal(Phase3PromptTemplate.TemplateVersion, result.PromptTemplateVersion);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsInjectedEvidenceBlock()
    {
        var builder = new Phase3PromptBuilder();
        var generator = new FakeAnswerGenerator(TestAnswerBuilder.BuildAnswer(170));
        var validator = new AnswerFormatValidator();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        var service = new AnswerGenerationService(builder, generator, validator, logger);

        var contextPack = new CoreContextPack("context text", 12, 1, 2200);
        var detectedEvidence = new[]
        {
            new DetectedEvidenceItem(
                "06",
                "F2",
                Phase4RuleTables.PresentAnnualProrationRule,
                "prorated reimbursement",
                "Customers may receive prorated reimbursement for unused service value.")
        };

        var result = await service.GenerateAsync(
            "question text",
            contextPack,
            PolicyRefundEvaluator.PolicyScenarioId,
            detectedEvidence,
            TestContext.Current.CancellationToken);

        Assert.Contains("Detected evidence from retrieved context:", result.EvidenceBlock);
        Assert.Contains("Document 06 [PRESENT_ANNUAL_PRORATION_RULE]", result.EvidenceBlock);
        Assert.Contains("Customers may receive prorated reimbursement for unused service value.", result.EvidenceBlock);
        Assert.Contains(result.EvidenceBlock, generator.UserPrompt, StringComparison.Ordinal);
    }

    private sealed class FakeAnswerGenerator : IAnswerGenerator
    {
        private readonly string _answer;

        public FakeAnswerGenerator(string answer)
        {
            _answer = answer;
        }

        public string? SystemPrompt { get; private set; }
        public string? UserPrompt { get; private set; }

        public Task<string> GenerateAnswerAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            SystemPrompt = systemPrompt;
            UserPrompt = userPrompt;
            return Task.FromResult(_answer);
        }
    }
}
