namespace EvoContext.Core.Tests.TestData;

public static class HeadingMarkdownSamples
{
    public const string MixedHeadings = """
# Refund Policy

## Billing Rules
General billing details.

### Cooling-Off Window
Customers may cancel within 14 days.

Inline hash should not match: text ### Not a heading

## Annual Plan
Annual plan details.
""";

    public const string LineStartOnlyHeading = """
# Runbook 502

Paragraph with inline marker ### not a heading.

## Diagnostics
Check health endpoints first.

### Restart Sequence
Restart API before worker.
""";
}