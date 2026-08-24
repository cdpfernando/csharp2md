# Quality and security

## Two certifications

`engine_certification` reports classifier precision and recall on labeled corpora. `run_certification` reports coverage, integrity and limitations of one analyzed input. A strong engine can produce a degraded run; structural facts may still be published when causal certification fails.

## Run coverage

Every metric publishes numerator, denominator, exclusions, unknowns and degradation reasons.

| Metric | Denominator |
| --- | --- |
| `entry_point_coverage` | Framework candidates mechanically recognizable in the analyzed variants |
| `linked_call_coverage` | In-solution invocations semantically bindable to a destination |
| `contract_coverage` | Boundary payload slots requiring accepted/returned/published/consumed contracts |
| `persistence_coverage` | Recognized data-access operations requiring operation and target resolution |

Reflection, runtime delegates and other inherently dynamic paths are reported as open frontiers, not silently added to a recall denominator. A solution-level recall percentage is forbidden without labeled ground truth.

## Engine gates

Initial labeled-corpus thresholds:

| Area | Precision | Recall |
| --- | ---: | ---: |
| Entry points | 99% | 95% |
| Linked calls | 99% | 90% |
| Contracts | 99% | 95% |
| Persistence | 99% | 90% |

Additional zero-tolerance gates:

- no confirmed causal relation based only on a name, prefix or path;
- no confirmed relation without evidence chain;
- no unexplained UNKNOWN regression;
- no secret duplicated into facts, observations, indexes or diagnostics;
- no identity collision, dangling reference, invalid hash or nondeterministic factual byte.

## Corpora

Validation uses:

1. minimal positive, negative and lookalike fixtures per classifier;
2. integrated end-to-end solutions;
3. independently labeled samples from real repositories and the `custom` scale corpus.

Ground truth is authored independently of classifier implementation and records source, expected presence/absence/unresolved state and rationale.

## Degradation and failure

```text
legitimate candidate or unknown
  -> publish with explanation

invalid derived fact
  -> quarantine, diagnostic, certification failure

identity/hash/structural corruption
  -> abort commit and preserve last valid output
```

Unsupported file, unavailable optional capability and selected-adapter failure are distinct outcomes. An unsupported capability that may hide entry points, contracts or persistence affects run certification.

## Security

Analysis operates locally and does not send code, telemetry or diagnostics. Semantic MSBuild/Roslyn paths require trusted input; source generators require separate consent; analyzers never run.

The byte-faithful source projection is treated as sensitive. Facts and observations use a literal allowlist for structural values such as routes, protocol names, channels, schema/table/field names, configuration keys and client names. They do not duplicate credentials, connection strings, tokens, certificates or authorization values.

Evidence containing a suspected secret stores document/span/hash and a redacted excerpt. Individual secret values are never hashed. Redaction occurs only in projections/retrieval; semantic analysis uses the original local source.

## Performance

The scale baseline records peak memory, time per pipeline stage, file count, factual/observation/index bytes, largest record/shard and retrieval-scenario cost. Gates use relative regression budgets after baseline variance is known plus absolute structural limits for records and shards.

Tests and generated code remain identifiable. Tests are excluded from runtime graph projections by default but stay available as qualified evidence. Generated code participates when it implements runtime behavior or contracts; metrics remain separated by origin.
