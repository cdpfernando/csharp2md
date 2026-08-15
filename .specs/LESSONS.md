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

## Quarantined (failed when applied - ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
