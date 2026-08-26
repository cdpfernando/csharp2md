# LESSONS - auto-maintained by scripts/lessons.py

> Machine-owned. Do NOT hand-edit. Changes are overwritten on the next `lessons.py` write.
> Canonical state lives in `.specs/lessons.json`. Edit lessons only via the script.
> promote_threshold=2 distinct features · window_days=45 · quarantine_threshold=2

## Confirmed (load these at Specify/Design)

Corroborated across multiple features. Safe to apply as guidance.

_none_

## Candidates (under observation - do NOT load as guidance yet)

Seen once or not yet corroborated. Tracked, not trusted.

### L-001 - When a construction-time guard needs data (a materialized fact or an evidence method) that the approved factory signature does not carry, wire it in via an explicit signature parameter or document it as a mandatory pre-call for callers -- do not ship it as a standalone public guard whose only call site is its own test.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · harmful: 0
- features: knowledge-taxonomy-contract
- evidence: TAX-46,TAX-50,TAX-51,TAX-53 / src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs:46-83, RelationShapeGuards.cs:18-101
- last seen: 2026-08-25T10:32:02Z

### L-002 - When a boundary AC says any production project, enumerate every production assembly in the theory, not a subset closed by a sibling requirement.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `isolation` · harmful: 0
- features: engine-bootstrap
- evidence: ENG-06 (isolation)
- last seen: 2026-08-25T15:35:07Z

### L-003 - When an AC names the process temp directory, snapshot it or record an approved design mitigation; a working-tree snapshot alone does not cover that clause.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `pipeline` · harmful: 0
- features: engine-bootstrap
- evidence: ENG-16 (pipeline)
- last seen: 2026-08-25T15:35:37Z

### L-004 - When an AC requires observations from bindable occurrences after compile errors, run extraction on that tree and assert a bound observation, not only a diagnostic and a kept compilation.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `extraction` · harmful: 0
- features: roslyn-observation-extraction
- evidence: ROSE-23 (extraction)
- last seen: 2026-08-26T03:25:39Z

### L-005 - When an AC requires commit abort on identity collision, assert Unpublished plus a named identity and return the accumulator corruption flag as the stage result the orchestrator reads.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `pipeline` · harmful: 0
- features: roslyn-observation-extraction
- evidence: ROSE-21 (pipeline)
- last seen: 2026-08-26T03:26:08Z

### L-006 - Do not specify empty-string StructuralLiteral outcomes; Domain TAX-80 rejects empty canonical text
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `domain-literals` · harmful: 0
- features: entrypoints-boundaries-contracts
- evidence: spec.md EBC-06 empty-template edge; BoundaryPassTests.cs:106; StructuralLiteralTests.cs:66 (domain-literals)
- last seen: 2026-08-26T06:33:57Z

### L-007 - When a spec outcome is unrepresentable in Domain, record SPEC_DEVIATION and assert the Domain-feasible diagnostic instead of inventing an empty literal
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `analysis-classification` · harmful: 0
- features: entrypoints-boundaries-contracts
- evidence: BoundaryPass.cs:85 SPEC_DEVIATION EBC-06 (analysis-classification)
- last seen: 2026-08-26T06:33:57Z

### L-008 - When two spec IF-conditions collapse to one ledger payload shape, assert each remaining distinguishable shape's UnresolvedRecord kind and cause.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `analysis-classification` · harmful: 0
- features: persistence-knowledge
- evidence: PK-39/PK-40 PersistenceModelBuilder.cs:84 (analysis-classification)
- last seen: 2026-08-26T16:56:36Z

### L-009 - When a spec WHEN is multi-TFM uniqueness, assert two target frameworks or analysis variants, not emitter re-entry on a single-TFM ledger.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · harmful: 0
- features: components-deployments-configuration
- evidence: CDC-04
- last seen: 2026-08-26T21:45:33Z

### L-010 - A diagnostic that must name both the referencing project and the missing target must assert both identities, not only the missing path.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · harmful: 0
- features: components-deployments-configuration
- evidence: CDC-07
- last seen: 2026-08-26T21:45:46Z

### L-011 - When an Independent Test names a published envelope file, assert those records on that file after a fixture analyze, not only on a hand-built snapshot.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · harmful: 0
- features: components-deployments-configuration
- evidence: P2 Independent Test CDC-56/CDC-57
- last seen: 2026-08-26T21:45:54Z

## Quarantined (failed when applied - ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
