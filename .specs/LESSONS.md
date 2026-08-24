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

### L-004 - When an AC requires deterministic ordering by a key, the covering test must include at least two distinct key values, not one value repeated, or the ordering claim is unfalsifiable.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `testing` · harmful: 0
- features: markdown-cleanup
- evidence: MDCLN-11 (testing)
- last seen: 2026-08-19T14:19:30Z

### L-005 - Every named enum value called out in a spec's edge cases needs its own fixture; testing only a subset of an enum's values leaves the untested values unverified even when the code path is generic.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `testing` · harmful: 0
- features: markdown-cleanup
- evidence: spec.md Edge Cases (NotApplicable symbol counting) (testing)
- last seen: 2026-08-19T14:19:30Z

### L-006 - When two independent formatting rules can interact (a computed fence length and a hardcoded fence), add one test that combines both trigger conditions instead of testing each rule in isolation.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `testing` · harmful: 0
- features: markdown-cleanup
- evidence: spec.md Edge Cases (yaml fence independent of source backtick-run length) (testing)
- last seen: 2026-08-19T14:19:30Z

### L-007 - When a criterion names the specific inputs a lookup must be built from, assert each input actually changes the result, not just that the lookup succeeds
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `relations` · harmful: 0
- features: relation-resolver
- evidence: RELR-05 src/Csharp2Md.Core/Analysis/Relations/Resolution/ReceiverTypeStrategy.cs:47 (relations)
- last seen: 2026-08-21T15:22:46Z

### L-008 - Assert the exact identity of an appended provenance or metadata entry, not merely that the collection is non-empty
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `facts` · harmful: 0
- features: relation-resolver
- evidence: RELR-20 tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationFragmentBuilderTests.cs:29 (facts)
- last seen: 2026-08-21T15:22:46Z

### L-009 - When an identity is minted by the orchestrator, have a strategy return a description of its diagnostic rather than a built one that would need the identity first
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `relations` · harmful: 0
- features: relation-resolver
- evidence: src/Csharp2Md.Core/Analysis/Relations/Resolution/IRelationResolutionStrategy.cs:31 (relations)
- last seen: 2026-08-21T15:22:54Z

### L-010 - Pass the set of identities the run actually produced explicitly into a resolver; an index over one fact family cannot answer whether a target from another family exists
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `relations` · harmful: 0
- features: relation-resolver
- evidence: src/Csharp2Md.Core/Analysis/Relations/Resolution/RelationResolver.cs:76 (relations)
- last seen: 2026-08-21T15:22:54Z

## Quarantined (failed when applied - ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
