# Demo Scenarios

EvoContext ships with two demonstration scenarios. Each is designed to expose a specific retrieval failure and show how the adaptive loop recovers from it.

---

## Scenario 1 — Policy Refund (`policy_refund_v1`)

**Query:** "What is the refund policy for annual subscriptions?"

**Dataset:** 8 policy documents covering subscription terms, refund conditions, cancellation procedures, billing error handling, and early termination rules.

**The retrieval problem:**

Similarity-only retrieval on this query surfaces the general refund terms (Doc 02) but misses several clauses that live in semantically distant documents:

- The prorated reimbursement rule for early termination of twelve-month contracts (Doc 06)
- The billing error exception and its processing timeline (Doc 05)

These documents use domain-specific language ("service commitment term", "contract year termination", "duplicate charges") that does not overlap closely with "refund policy for annual subscriptions". Cosine similarity ranks them below the threshold.

**Expected Run 1 behavior:**

The answer covers the 14-day cooling-off window and the general non-refundable rule but omits the proration clause, the billing error exception, and the processing timeline. Score: approximately 60/100.

**Expected Run 2 behavior:**

The evaluator identifies the three missing fact labels and maps them to 6 targeted expansion queries. Retrieval with these queries reaches Doc 05 and Doc 06. The improved answer covers all three missing clauses. Score: approximately 90/100.

**What this demonstrates:**

Query expansion driven by structured evaluation feedback can recover policy clauses that similarity search misses. The improvement is verifiable: the expansion queries, the newly retrieved chunks, and the recovered fact labels are all recorded in the trace artifact.

---

## Scenario 2 — Runbook 502 (`runbook_502_v1`)

**Query:** "The service returns 502. What do I do?"

**Dataset:** A set of runbook documents describing diagnostic and remediation steps for a 502 gateway error.

**The retrieval problem:**

Similarity-only retrieval surfaces the high-level runbook overview but may miss specific diagnostic steps that are described in separate documents with more technical language ("upstream health check", "deployment rollback", "log inspection").

**Expected Run 1 behavior:**

The answer produces an incomplete step list, missing one or more required diagnostic steps or presenting them out of order. Score: approximately 30/100.

**Expected Run 2 behavior:**

The evaluator identifies the missing or misordered steps and generates expansion queries targeting the specific procedures. The improved answer includes the required steps in the correct sequence. Score: approximately 40/100.

**What this demonstrates:**

The evaluation rubric for procedural scenarios checks both presence and ordering of steps, not just content. This is a harder recovery than the policy scenario — the score improvement is smaller but the mechanism is the same: structured feedback drives a more targeted second retrieval pass.

---

## Why Two Scenarios

The two scenarios test the system under different rubric types:

| | Policy Refund | Runbook 502 |
|---|---|---|
| Rubric type | Fact presence + hallucination detection | Step presence + step ordering |
| Evaluator | `PolicyRefundEvaluator` | `Runbook502Evaluator` |
| Primary failure mode | Semantically distant clauses | Incomplete or misordered procedure |
| Score range demonstrated | 60 → 90 | 30 → 40 |

The policy scenario shows a larger score delta and is the primary demo. The runbook scenario shows that the same adaptive pattern applies to a structurally different problem.
