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

## Quarantined (failed when applied - ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
