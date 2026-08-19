# LESSONS - auto-maintained by scripts/lessons.py

> Machine-owned. Do NOT hand-edit. Changes are overwritten on the next `lessons.py` write.
> Canonical state lives in `.specs/lessons.json`. Edit lessons only via the script.
> promote_threshold=2 distinct features · window_days=45 · quarantine_threshold=2

## Confirmed (load these at Specify/Design)

Corroborated across multiple features. Safe to apply as guidance.

_none_

## Candidates (under observation - do NOT load as guidance yet)

Seen once or not yet corroborated. Tracked, not trusted.

### L-001 - When an AC defines an edge's direction and type but omits its classification field, decide the value in the spec rather than in code - state each derived-record field explicitly so the implementer never has to infer one.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `spec-authoring` · harmful: 0
- features: csharp2md
- evidence: P2-14 / src/Csharp2Md.Core/Graph/GraphBuilder.cs:109 (spec-authoring)
- last seen: 2026-08-15T05:29:47Z

### L-002 - When a detector confirms evidence by walking a type's identity, add a lookalike test using a type literally sharing the real type's simple name in a foreign namespace, not just a differently-named lookalike, so a fully-qualified-to-simple-name weakening is caught.
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `detection` · harmful: 0
- features: csharp2md-v3
- evidence: src/Csharp2Md.Core/Detection/Http/HttpRelationDetector.cs:234 (detection)
- last seen: 2026-08-19T13:10:28Z

### L-003 - A migration ledger's mechanized proof must bind each individual baseline row to its own named replacement assertion, not a shared category-representative method, or the 'every row has a replacement' claim is unverified at the granularity it advertises.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `test-migration` · harmful: 0
- features: csharp2md-v3
- evidence: tests/Csharp2Md.Core.Tests/Analysis/MigrationLedgerTests.cs:49-64 (test-migration)
- last seen: 2026-08-19T13:10:30Z

## Quarantined (failed when applied - ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
