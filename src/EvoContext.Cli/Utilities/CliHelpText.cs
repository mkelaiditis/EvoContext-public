namespace EvoContext.Cli.Utilities;

public static class CliHelpText
{
    public const string Text = """
EvoContext CLI

Usage:
  evocontext <command> [options]

Commands:
  ingest  Run document ingestion
  embed   Run embedding ingestion and Gate A probe
  run     Run a scenario demo
  run1    Run Phase 1 retrieval
  run3    Run Phase 3 answer generation
  run4    Run Phase 4 evaluation and scoring
  run5    Run Phase 5 adaptive memory (Run 1 + Run 2)
  replay  Replay a run
  stats   Show run stats and retrieval diagnostics
  config  Show resolved configuration (secrets masked)

Options for ingest:
  --scenario <id>   Scenario identifier (required)
  --dataset <path>  Dataset path override (optional)

Options for embed:
  --scenario <id>   Scenario identifier (required)
  --dataset <path>  Dataset path override (optional)

Options for run:
  --scenario <id>   Scenario identifier (required)
  --query <text>    Query text (optional, defaults to scenario primary query)
  --mode <run1|run2>  Run mode (required)

Options for run1:
  --scenario <id>   Scenario identifier (required)
  --query <text>    Query text (optional, defaults to scenario primary query)
  --repeat <n>      Repeat runs to check determinism (default: 1)

Options for run3:
  --scenario <id>   Scenario identifier (required)
  --query <text>    Query text (optional, defaults to scenario primary query)
  --repeat <n>      Repeat runs to check determinism (default: 1)

Options for run4:
  --input <path>    Evaluation input JSON path

Options for run5:
  --scenario <id>   Scenario identifier (required)
  --query <text>    Query text (required)
  --repeat <n>      Repeat runs to check determinism (default: 1)

Options for replay:
  --run-id <id>     Run identifier (required)

Options for stats:
  --scenario <id>   Scenario identifier (required)
  --k <n>           Optional retrieval diagnostics top-K override
""";
}