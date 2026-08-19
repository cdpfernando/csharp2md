# Markdown Cleanup Specification

## Problem Statement

The `## Factual annotations` section of every generated Markdown document repeats data that already exists, aggregated, in the frontmatter (`analysis_summary.resolution/symbol_count/relation_count/diagnostic_count`, `diagnostics[]`, `facts_ref`): one bullet line per symbol (`` `class` — unresolved ``, `` `method` — unresolved ``, ...) and one bullet line per diagnostic occurrence. On a document with many unresolved symbols this section is pure repeated noise — it adds tokens without adding information a reader or an LLM doesn't already have from the frontmatter or from `facts.json`. The full per-symbol, per-relation, per-diagnostic detail (IDs, evidence, provenance) is already the responsibility of `facts.json` per AD-009/AD-010; the Markdown body should stay a readable projection of the source, not a second copy of the factual ledger.

## Goals

- [ ] Replace the per-symbol/per-diagnostic-occurrence body listing with one compact, aggregated block per document.
- [ ] Reduce emitted Markdown tokens on documents with many symbols/diagnostics, with zero loss of traceability (full detail stays reachable via `facts_ref`).
- [ ] Keep the existing structural guarantees untouched: verbatim source, span-coverage partition, one section per namespace/class/field/constructor/method, `facts_ref` in frontmatter.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Changing `facts.json` / `ValidatedFactFragment` / schema-version-2 fact model | The full structured detail must keep living there unchanged (AD-009, AD-010); this feature only changes what the Markdown *body* renders from it. |
| Changing frontmatter (`FrontmatterV2`, `schemas/frontmatter-v2.schema.json`) | Frontmatter already carries `analysis_summary` (flat counts + resolution) and an aggregated `diagnostics[]` (by code+severity+count) plus `facts_ref`. Nothing here is redundant with the body change in a way that requires touching the frontmatter contract, and changing a `const: 2`-pinned JSON Schema is a bigger, separate decision. |
| Adding a resolution-kind breakdown (exact/partial/syntactic/unresolved counts) to frontmatter | The new breakdown is introduced only in the compact body block (new information, not currently in frontmatter). Whether it should *also* be promoted into frontmatter/schema-version-3 is a follow-up decision, not this feature's. |
| Renaming/relocating `raw/` layout, manifest, or any non-Markdown artifact | Not touched by this change. |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here — nothing is left silently unclear. The improvement request gave an illustrative YAML shape but did not fully pin every formatting detail; the choices below are the concrete contract this spec commits to.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Section heading | Rename `## Factual annotations` to `## Analysis` | The new content is a compact analysis summary, not per-item annotations; the old heading would now describe the wrong thing. | n |
| Block format | A fenced ` ```yaml ` block under the heading, hand-emitted with the same manual-StringBuilder style `FrontmatterV2.ToYaml()` already uses (2-space indent, no external YAML writer) | Keeps one YAML-emission style in the codebase; a real (if small) YAML document is easy for both humans and LLMs to parse, matching the request's own example. | n |
| Top-level wrapper key | No `analysis:` wrapper — the block starts directly with `resolution:`, since the heading already says "Analysis" | The request's `analysis:` root key becomes redundant once it's already the heading; avoids one indent level of pure ceremony. | n |
| `facts_ref` duplication | Not repeated inside the body block; it stays in frontmatter only | Repeating it would reintroduce exactly the redundancy this feature removes. AC "`facts_ref` continua disponível" is satisfied by frontmatter, which is already present on every document. | n |
| Which resolution kinds are shown under `symbols:` / `relations:` | Every `FactResolution` kind (`exact`, `partial`, `syntactic`, `unresolved`, `notapplicable`) that has a **non-zero** count for this document, in that fixed order; kinds absent from the document are omitted entirely | The request's own example shows only 3 of 5 possible kinds — read as "show what's present," not as a hardcoded 3-key schema. A fixed 3-key schema would silently under-count (and mislead) any document with `partial` or `notapplicable` facts, which breaks "sem perda de rastreabilidade." Show-what's-nonzero keeps the sum of shown counts equal to `symbol_count`/`relation_count` in frontmatter. | n |
| Top-level `resolution:` value in the block | Reuse `document.Header.Resolution` verbatim (the same value already surfaced as `analysis_summary.resolution` in frontmatter) | Guarantees the body and the frontmatter never disagree; no new aggregation logic needed for this field. | n |
| Diagnostics aggregation key | Group by `Code` only (drop `Severity` from the body view), one `code: count` line per distinct code | Matches the request's explicit example and AC ("agregados por código e quantidade"); severity remains available per-code in frontmatter's `diagnostics[]` and in full in `facts.json`. | n |
| Empty diagnostics rendering | `diagnostics: {}` (empty inline map), not an omitted key | Keeps the block a single valid, always-parseable YAML document rather than a block whose key set varies; mirrors the existing `diagnostics: []` empty-array convention in `FrontmatterV2.ToYaml()`. | n |
| Whole-section omission rule | Omit the entire `## Analysis` section when the document has zero symbols, zero relations, and zero diagnostics (extends the current guard, which only checked symbols + diagnostics, to also check relations) | Preserves today's behavior for trivial/empty documents (e.g. a file with only usings) where there is nothing to summarize. | n |
| Relation scope | Same relation set already used for frontmatter's `relation_count` today: `fragment.Facts.OfType<RelationFact>()` with no additional per-document filter | `FrontmatterV2.Create` already counts relations this way with no `DocumentId` filter (unlike symbols, which do filter by `DocumentId`); the compact block's `relations:` total must match that number for the two to stay consistent, so it reuses the same fact set. | n |
| Version bump | Bump `<Version>` in `Csharp2Md.Cli`/packaging from `3.0.0` to `3.0.1` as part of this feature, no new AD entry | Matches this repo's established practice (v1→2.0.0, LLMWiki→2.0.0, v3→3.0.0) of bumping on every output-affecting release, and the user's own command names this "v3.0.1." This is a Markdown-body formatting change, not a `facts.json`/frontmatter schema break, so patch-level is consistent with semver as actually used here (a minor/major bump is not warranted). | n |
| Branch | Not decided in this spec — raised as a separate question before Execute starts, since `feat/csharp2md-v3` is already complete-but-unpushed (per `.specs/STATE.md` Handoff) and AD-007 requires an explicit branch/PR decision for work like this. | Branching is a process/workflow decision, not a requirements question; it doesn't block writing or confirming the spec itself. | n |

**Open questions:** none — all resolved or logged above. All rows above are proposed defaults pending the user's confirmation of this spec (marked `n` deliberately, not silently assumed `y`).

---

## User Stories

### P1: Compact per-document analysis summary ⭐ MVP

**User Story**: As a person or LLM reading a generated Markdown document, I want one small, aggregated analysis summary per document instead of a line-per-symbol and line-per-diagnostic-occurrence listing, so that the document stays focused on readable source and navigation while the full factual detail remains one hop away in `facts.json`.

**Why P1**: This is the entire feature; there is no smaller independently-shippable slice.

**Acceptance Criteria** (each line is one EARS pattern):

1. The system SHALL preserve the C# source verbatim in the rendered code sections, byte-for-byte, exactly as today (no change to `AD-002`'s span-coverage partition or section fencing). <!-- ubiquitous -->
2. The system SHALL continue to separate namespace, class/struct/record/interface/enum, field, constructor, and method content into distinct `##` sections exactly as today. <!-- ubiquitous -->
3. The system SHALL continue to emit `facts_ref` in the document's frontmatter. <!-- ubiquitous -->
4. WHEN a document has at least one symbol, relation, or diagnostic THEN the system SHALL emit a single `## Analysis` section, placed where `## Factual annotations` is placed today (before the first structural section), containing one fenced ` ```yaml ` block. <!-- event-driven -->
5. WHILE emitting that block THE system SHALL NOT emit an individual line per symbol (no per-symbol `` `kind` — resolution `` bullets) and SHALL NOT emit an individual line per diagnostic occurrence. <!-- state-driven -->
6. The system SHALL set the block's `resolution:` value to the document's aggregate `FactResolution` (the same value already exposed as `analysis_summary.resolution` in frontmatter). <!-- ubiquitous -->
7. The system SHALL emit a `symbols:` map whose keys are the `FactResolution` kinds present (count > 0) among this document's symbols and whose values are their counts, and whose values sum to `analysis_summary.symbol_count`. <!-- ubiquitous -->
8. The system SHALL emit a `relations:` map with the same shape as `symbols:` (kinds present with count > 0, values summing to `analysis_summary.relation_count`), computed over the same relation set frontmatter already uses (no additional document filter). <!-- ubiquitous -->
9. The system SHALL emit a `diagnostics:` map keyed by diagnostic `Code` with the count of occurrences of that code in this document, aggregating across all severities; the map SHALL be `{}` when the document has zero diagnostics. <!-- ubiquitous -->
10. IF a document has zero symbols, zero relations, and zero diagnostics THEN the system SHALL omit the `## Analysis` section entirely (same behavior as today's empty-document case, extended to also check relations). <!-- unwanted-behavior -->
11. WHEN the same document is rendered twice from the same validated fragment THEN the system SHALL produce byte-identical `## Analysis` output both times (deterministic map ordering: fixed resolution-kind order `exact, partial, syntactic, unresolved, notapplicable`; diagnostics ordered by `Code`, ordinal). <!-- event-driven -->

**Independent Test**: Render a fixture document with a mix of exact/syntactic/unresolved symbols and one repeated diagnostic code (e.g. the existing `Acme.Orders/OrderService.cs` fixture used by `V3DeterminismTests`, which currently has `C2M-BIND-002` recurring). Assert the `## Analysis` block has one `symbols:` line per resolution kind present, no `- \`method\` — ...` per-symbol bullets, and one `diagnostics:` line per distinct code with the correct aggregated count.

---

## Edge Cases

- IF a document has symbols but zero diagnostics THEN the `diagnostics:` map SHALL render as `{}` rather than being omitted, keeping the block a single always-valid YAML document. <!-- unwanted-behavior -->
- IF a document has zero symbols but at least one relation or diagnostic THEN the `symbols:` map SHALL render as `{}` (not omitted), by the same rule. <!-- unwanted-behavior -->
- IF a symbol carries `FactResolution.NotApplicable` THEN it SHALL still be counted under a `notapplicable:` key in `symbols:` when present, never silently dropped from the sum. <!-- unwanted-behavior -->
- WHEN a document has a source-fence character run (backticks) inside its code, unrelated to this feature THEN the existing longer-fence logic SHALL remain unaffected — the `## Analysis` YAML fence always uses plain ` ```yaml `, independent of the source-code fence length calculation. <!-- state-driven -->

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| MDCLN-01 | P1 | Execute | Verified |
| MDCLN-02 | P1 | Execute | Verified |
| MDCLN-03 | P1 | Execute | Verified |
| MDCLN-04 | P1 | Execute | Verified |
| MDCLN-05 | P1 | Execute | Verified |
| MDCLN-06 | P1 | Execute | Verified |
| MDCLN-07 | P1 | Execute | Verified |
| MDCLN-08 | P1 | Execute | Verified |
| MDCLN-09 | P1 | Execute | Verified |
| MDCLN-10 | P1 | Execute | Verified |
| MDCLN-11 | P1 | Execute | Verified — closed 3 evidence gaps flagged by the independent Verifier (diagnostics ordinal ordering, `notapplicable` symbol count, YAML fence independence from source backtick length) |

**ID format:** `MDCLN-NN`, numbered in the same order as the P1 acceptance criteria above (MDCLN-01 = AC1, ... MDCLN-11 = AC11).

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 11 total, 11 mapped to this single P1 story, 0 unmapped.

---

## Success Criteria

How we know the feature is successful:

- [ ] Every existing `MarkdownProjector`/`FrontmatterV2`/`V3DeterminismTests` test that asserted the old per-symbol/per-diagnostic body listing is updated to assert the new compact block instead (no test deleted to make this pass; behavior asserted, not implementation).
- [ ] The two approved Markdown snapshots (`MarkdownProjectorTests...verified.md`, `V3DeterminismTests...verified.md`) are re-approved against the new output and human-reviewed as part of the task, not blindly accepted.
- [ ] On the `Acme.Orders/OrderService.cs` representative fixture (6 symbols, all `syntactic`, 0 diagnostics today), the `## Analysis` block is measurably shorter than today's 8-line bulleted list.
- [ ] `dotnet build -c Release`, `dotnet format --verify-no-changes`, and the full test suite stay green.
- [ ] `facts.json` byte output and the frontmatter schema are unchanged (no diff in `schemas/frontmatter-v2.schema.json`, no diff in `FactualJsonContracts`/persisted fact JSON shape).
