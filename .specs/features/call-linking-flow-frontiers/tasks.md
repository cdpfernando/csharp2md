# Call Linking, Flow Frontiers — Tasks

## Execution Protocol (MANDATORY — do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and
Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the
full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

**User-confirmed deviation from the standard flow:** the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by standing user request, as it was for
`symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`,
`knowledge-taxonomy-contract`, `engine-bootstrap`, `factual-storage`, and `roslyn-observation-extraction`.
No mutants are injected at any point. The Verifier's spec-anchored outcome check, per-AC `file:line`
evidence, and `validation.md` report still run as normal.

**WS5A coordination note:** Tasks T1–T2 assume WS5A (`IClassifierPass`, `ClassifierContext`,
`ClassificationAndPromotionStage`) is already merged. If it is not, T1 must also define those three types.
Verify before starting Execute.

---

**Spec**: `.specs/features/call-linking-flow-frontiers/spec.md`
**Design**: `.specs/features/call-linking-flow-frontiers/design.md`
**Status**: Approved — Execute pending

---

## Test Coverage Matrix

> Generated from codebase sampling and project guidelines. Guidelines found: `AGENTS.md`, `CLAUDE.md`
> (routing test quality to `dotnet-test:*` skills). Codebase samples:
> `tests/Csharp2Md.Analysis.Tests/Extraction/AlwaysWhenBindableWalkerTests.cs`,
> `tests/Csharp2Md.Analysis.Tests/Pipeline/SnapshotAccumulatorTests.cs`,
> `tests/Csharp2Md.Domain.Tests/Relations/ConfirmedRelationTests.cs`,
> `tests/Csharp2Md.Domain.Tests/Relations/CandidateLinkTests.cs`,
> `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisPublicSurfaceTests.cs`.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain — `SymbolFacet.Abstract` extension | unit | Enum value defined; `SymbolFacetSet` accepts and rejects it; bijection test covers new value | `tests/Csharp2Md.Domain.Tests/Facets/` | `dotnet test tests/Csharp2Md.Domain.Tests --filter "Category!=LocalCorpus"` |
| Extraction — `AlwaysWhenBindableWalker` target annotation | unit + integration | Bound `Invocation`/`ObjectCreation` messages start with `"bound::"` + signature; unbound stays `"unbound"`; non-method bound stays `"bound"`; identity unchanged (payload empty, ordinals stable) | `tests/Csharp2Md.Analysis.Tests/Extraction/AlwaysWhenBindableWalkerTests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` |
| Analysis — `SnapshotAccumulator` extensions | unit | `AddCandidate`, `AddUnresolved`, `AddOpenFrontier` add records; `ToSnapshot()` returns them; no dedup for unresolved/frontier (pass owns dedup) | `tests/Csharp2Md.Analysis.Tests/Pipeline/SnapshotAccumulatorTests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` |
| Analysis — `InvokesPass` | unit + integration | Per-AC 1:1 coverage for CLLF-01 through CLLF-14 and CLLF-17/18/20; fixture assertions on `Acme.Orders.slnx`; all receiver shapes (CLLF-15/16); dedup (CLLF-05); BCL skip (CLLF-20) | `tests/Csharp2Md.Analysis.Tests/Classification/` (new) | `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` |
| Analysis — `ExecutesPass` | unit + integration | Per-AC CLLF-03; fixture assertion `EntryPoint → Symbol` produces `Executes`; missing symbol produces diagnostic | `tests/Csharp2Md.Analysis.Tests/Classification/` (new) | `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` |
| Analysis — `PipelineStages` wiring | unit | `PipelineStages.CreateDefault()` includes `InvokesPass` and `ExecutesPass` at the correct position; `ClassificationAndPromotionStage` pass list count matches expectation | `tests/Csharp2Md.Analysis.Tests/Pipeline/` | `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` |
| Analysis — public surface | unit | `AnalysisPublicSurfaceTests` allowlist updated to include any newly public types (expected: none — all new types are `internal`) | `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisPublicSurfaceTests.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` |
| Storage — round-trip | unit + integration | `CandidateLink`, `UnresolvedRecord`, `OpenFrontier` records survive the commit/read round-trip; `PackageValidatorTests` updated for new `BindingDiagnostic.Message` format | `tests/Csharp2Md.Storage.Tests/` | `dotnet test tests/Csharp2Md.Storage.Tests --filter "Category!=LocalCorpus"` |

## Gate Check Commands

> Generated from codebase — confirm before Execute. Five test projects run separately due to MSB1008 (multi-csproj
> `dotnet test` hits the single solution restriction). `Category!=LocalCorpus` excludes local corpus tests that
> require `fixtures/eShop` or `fixtures/eShopOnContainers`.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks with unit tests in a single project | `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` OR `dotnet test tests/Csharp2Md.Domain.Tests --filter "Category!=LocalCorpus"` (whichever project was modified) |
| Full | After tasks touching multiple projects or integration tests | Run both: `dotnet test tests/Csharp2Md.Domain.Tests --filter "Category!=LocalCorpus"` and `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` and `dotnet test tests/Csharp2Md.Storage.Tests --filter "Category!=LocalCorpus"` |
| Build | After phase completion or config/entity-only tasks | `dotnet build` then all five projects: Domain, Analysis, Storage, Projection, Cli test projects with `--filter "Category!=LocalCorpus"` |

---

## Execution Plan

Phases run sequentially; tasks within a phase execute in order.

### Phase 1: Domain and Extractor Foundation

Establishes the `SymbolFacet.Abstract` flag and the extractor annotation that all classification passes depend on.

```
T1 → T2 → T3
```

### Phase 2: Accumulator and Classifier Infrastructure

Extends `SnapshotAccumulator` and adds the two new passes + wiring. Depends on Phase 1.

```
T4 → T5 → T6 → T7
```

### Phase 3: Integration, Coverage, and Hardening

Storage round-trip, public-surface check, WS4 test repairs, and fixture integration tests. Depends on Phase 2.

```
T8 → T9 → T10
```

---

## Task Breakdown

---

### T1: Add `SymbolFacet.Abstract` to Domain and update `SymbolFactEmitter`

**What**: Add `Abstract` to the `SymbolFacet` enum and emit it in `SymbolFactEmitter.Facets` when `IMethodSymbol.IsAbstract` is `true` or the containing type is an interface; add corresponding TaxonomyTables entry and bijection coverage.
**Where**: `src/Csharp2Md.Domain/Facets/` and `src/Csharp2Md.Analysis/Semantics/SymbolFactEmitter.cs`
**Depends on**: None
**Reuses**: Existing `SymbolFacet.Callable` pattern in `SymbolFactEmitter.Facets`; existing `SymbolFacetSet` construction
**Requirement**: CLLF-07, CLLF-09 (abstract/interface detection)

**Tools**:
- MCP: context7 (`/dotnet/roslyn` — verify `IMethodSymbol.IsAbstract`, `INamedTypeSymbol.TypeKind == TypeKind.Interface`)
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [x] `SymbolFacet.Abstract` value defined in enum
- [x] `SymbolFactEmitter.Facets` emits `Abstract` when `symbol is IMethodSymbol { IsAbstract: true }` OR containing type `TypeKind == TypeKind.Interface`
- [x] `SymbolFacetSet` accepts the new value without throwing
- [x] Domain bijection test (existing `TaxonomyTablesAdditivityTests` or equivalent) covers the new facet value
- [x] `dotnet test tests/Csharp2Md.Domain.Tests --filter "Category!=LocalCorpus"` passes
- [x] `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` passes (no regression)
- [x] Test count: ≥ 3 new tests in `tests/Csharp2Md.Domain.Tests/Facets/` covering: enum presence, `SymbolFacetSet` accepts it, `SymbolFacetSet` rejects unknown value (existing)

**Tests**: unit
**Gate**: full (Domain + Analysis)

**Commit**: `feat(domain): add SymbolFacet.Abstract for abstract and interface member detection`

---

### T2: Extend `AlwaysWhenBindableWalker` to annotate invocation diagnostic messages

**What**: Modify `TryEmitBindable` so that when `kind` is `Invocation` or `ObjectCreation` and the bound symbol is an `IMethodSymbol` with a computable `TrySignature`, the `BindingDiagnostic.Message` is set to `"bound::<signature>"` instead of `"bound"`. Add `TryExtractTargetSignature` as an `internal static` helper.
**Where**: `src/Csharp2Md.Analysis/Extraction/AlwaysWhenBindableWalker.cs`
**Depends on**: T1 (needs `SymbolFactEmitter.TrySignature` to handle the `Abstract` facet correctly — no hard dependency but ensures consistent signature production)
**Reuses**: `SymbolFactEmitter.TrySignature` (same assembly, internal); existing `BindingDiagnostic` construction pattern
**Requirement**: CLLF-06

**Tools**:
- MCP: context7 (`/dotnet/roslyn` — verify `IMethodSymbol` as the kind for method invocation targets)
- Skill: NONE

**Done when**:
- [ ] `TryEmitBindable` emits `"bound::<sig>"` in `Diagnostic.Message` for bound `Invocation`/`ObjectCreation` when target is an `IMethodSymbol` and `TrySignature` returns non-null
- [ ] Non-method-target bound invocations (e.g., delegate calls, property invocations resolved as non-method) keep `Diagnostic.Message == "bound"` (Code unchanged in all cases)
- [ ] Unbound invocations keep `Diagnostic.Message == "unbound"`; Code == "unbound"
- [ ] `TryExtractTargetSignature(string message)` helper returns the signature string or `null`; unit-tested
- [ ] `ObservationIdentity` is unchanged (payload still empty for Invocation/ObjectCreation — verified by existing ROSE-32/45 tests still passing)
- [ ] Existing WS4 test `ExtractInto_AcmeOrders_YieldsEachAlwaysWhenBindableKindWithEmptyPayload` still passes (payload stays empty)
- [ ] New tests: `Invocation_BoundToMethod_DiagnosticMessageContainsSignature`, `Invocation_BoundToNonMethod_DiagnosticMessageIsPlainBound`, `Invocation_Unbound_DiagnosticMessageIsUnbound`, `TryExtractTargetSignature_BoundSignatureMessage_ReturnsSignature`, `TryExtractTargetSignature_PlainBound_ReturnsNull`
- [ ] `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` passes
- [ ] Test count: ≥ 5 new tests; 0 existing tests deleted or weakened

**Tests**: unit
**Gate**: quick (Analysis)

**Commit**: `feat(extraction): annotate Invocation/ObjectCreation diagnostic messages with bound target signature`

---

### T3: Fix WS4 tests that assert `Diagnostic.Message == "bound"` exactly

**What**: Update `tests/Csharp2Md.Storage.Tests/Validation/PackageValidatorTests.cs` (and any other test file) where `new BindingDiagnostic("bound", "bound")` or `Assert.Equal("bound", ...)` on `Diagnostic.Message` for invocation observations breaks after T2's change.
**Where**: `tests/Csharp2Md.Storage.Tests/Validation/PackageValidatorTests.cs` (and any other affected file — audit first)
**Depends on**: T2
**Reuses**: Existing test patterns; no new logic
**Requirement**: CLLF-06 (WS4 identity invariants preserved)

**Tools**:
- Skill: `dotnet-test:assertion-quality`

**Done when**:
- [ ] All five test projects compile and all tests pass with `"Category!=LocalCorpus"` filter
- [ ] No test uses `Assert.Equal("bound", observation.Diagnostic.Message)` for an Invocation observation without `StartsWith("bound")`
- [ ] `PackageValidatorTests.cs` updated: any fixture `BindingDiagnostic("bound", "bound")` for an Invocation observation uses `StartsWith("bound")` or a new fixture value
- [ ] `dotnet test tests/Csharp2Md.Storage.Tests --filter "Category!=LocalCorpus"` passes
- [ ] Test count: 0 tests deleted; adjustments only

**Tests**: unit
**Gate**: full (all five projects compile and pass)

**Commit**: `fix(tests): update WS4 tests for new invocation diagnostic message format`

---

### T4: Extend `SnapshotAccumulator` with `AddCandidate`, `AddUnresolved`, `AddOpenFrontier`

**What**: Add three backing lists and three corresponding `Add*` methods to `SnapshotAccumulator`, and update `ToSnapshot()` to use them instead of the three `.Empty` placeholders.
**Where**: `src/Csharp2Md.Analysis/Pipeline/SnapshotAccumulator.cs`
**Depends on**: T3
**Reuses**: Existing `AddRelation`, `AddDiagnostic` pattern
**Requirement**: CLLF-11, CLLF-12, CLLF-13 (OpenFrontier); CLLF-07/08 (CandidateLink); CLLF-08/18 (UnresolvedRecord)

**Tools**:
- Skill: NONE

**Done when**:
- [ ] `private readonly List<CandidateLink> _candidates` and `AddCandidate(CandidateLink)` added
- [ ] `private readonly List<UnresolvedRecord> _unresolved` and `AddUnresolved(UnresolvedRecord)` added
- [ ] `private readonly List<OpenFrontier> _frontiers` and `AddOpenFrontier(OpenFrontier)` added
- [ ] `ToSnapshot()` uses `[.. _candidates]`, `[.. _unresolved]`, `[.. _frontiers]` instead of `.Empty`
- [ ] **Coordination check**: if WS5A already added `AddCandidate`/`AddUnresolved`, verify they match this design and do not add duplicates — document the check in the commit message
- [ ] New tests in `SnapshotAccumulatorTests.cs`: `AddCandidate_AppearsInSnapshot`, `AddUnresolved_AppearsInSnapshot`, `AddOpenFrontier_AppearsInSnapshot`, `ToSnapshot_NoAdditions_ReturnsEmptyCollections` (for regression)
- [ ] `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` passes
- [ ] Test count: ≥ 4 new tests; existing SnapshotAccumulator tests still pass

**Tests**: unit
**Gate**: quick (Analysis)

**Commit**: `feat(pipeline): add AddCandidate, AddUnresolved, AddOpenFrontier to SnapshotAccumulator`

---

### T5: Implement `InvokesPass`

**What**: Create `InvokesPass : IClassifierPass` that classifies `Invocation` and `ObjectCreation` observations into `ConfirmedRelation(Invokes)`, `CandidateLink(Invokes)`, `UnresolvedRecord(Invokes)`, and `OpenFrontier` records using the target signature from `Diagnostic.Message`.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/InvokesPass.cs` (new)
**Depends on**: T4
**Reuses**: `IClassifierPass` (WS5A); `ClassifierContext` indexes; `AlwaysWhenBindableWalker.TryExtractTargetSignature`; `ConfirmedRelation.Create`, `CandidateLink.Create`, `UnresolvedRecord.Create`, `OpenFrontier.Create`
**Requirement**: CLLF-01, CLLF-02, CLLF-04, CLLF-05, CLLF-07, CLLF-08, CLLF-09, CLLF-10, CLLF-11, CLLF-12, CLLF-13, CLLF-14, CLLF-15, CLLF-16, CLLF-17, CLLF-18, CLLF-20

**Tools**:
- MCP: context7 (`/dotnet/roslyn` — if any Roslyn type inspection is needed; should be none — classifier is Roslyn-free)
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-test:assertion-quality`

**Done when**:
- [ ] `InvokesPass` implements `IClassifierPass`; classifier identity `csharp2md.classifier.invokes` v1
- [ ] Index building: `symbolsBySig` (all-project, by sig value), `symbolByProjectAndSig` (same-project preference), pre-built before iteration
- [ ] Bound + method-target + same-project match → `ConfirmedRelation(Invokes)` with `EvidenceMethod.Semantic` and `sourceFact: ownerSymbol` (CLLF-01)
- [ ] Bound + constructor `ObjectCreation` → `ConfirmedRelation(Invokes)` (CLLF-02)
- [ ] No confirmed `Invokes` when owner or target not a `Symbol` fact (CLLF-04)
- [ ] Dedup `HashSet<(RelationKind, FactId, FactId)>` prevents duplicate relations (CLLF-05)
- [ ] Abstract/interface target + concrete overrides in scope → `CandidateLink` per override (CLLF-07, CLLF-10)
- [ ] Abstract/interface target + no concrete overrides → `UnresolvedRecord(NoCandidateFound)` (CLLF-08)
- [ ] No confirmed `Invokes` to abstract/interface member (CLLF-09)
- [ ] Fallback-owner observation → `UnresolvedRecord(InsufficientEvidence)` only; no `OpenFrontier` (CLLF-14)
- [ ] BCL/framework prefix skip: container starts with `global::System.` or `global::Microsoft.` → silent skip (CLLF-20)
- [ ] Cross-project call (target in different project) → confirmed `Invokes` if in same solution (CLLF-17)
- [ ] External-package call (target signature matches no `Symbol` fact and not BCL-skip) → `UnresolvedRecord(NoCandidateFound)` + `OpenFrontier` (CLLF-18)
- [ ] Unbound observation → `UnresolvedRecord(NoCandidateFound)` + `OpenFrontier` (CLLF-11)
- [ ] Receiver shapes: field/property/parameter/pattern-variable — all produce the same confirmed `Invokes` when target is concrete (CLLF-15); no duplicates for same `(source, target)` pair (CLLF-16)
- [ ] Unit tests in `tests/Csharp2Md.Analysis.Tests/Classification/InvokesPassTests.cs`: one test per numbered AC above, using in-memory `ClassifierContext` with crafted `Symbol` facts and `Observation` objects
- [ ] Integration test in `tests/Csharp2Md.Analysis.Tests/Classification/InvokesPassFixtureTests.cs`: load `Acme.Orders.slnx` through full pipeline; assert `OrderService.AuthorizeViaPaymentClientAsync → PaymentClient.Authorize` produces `ConfirmedRelation(Invokes)` (CLLF-17, cross-project)
- [ ] `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` passes
- [ ] Test count: ≥ 18 new tests (≥ 15 unit + ≥ 3 integration)

**Tests**: unit + integration
**Gate**: full (Analysis)

**Commit**: `feat(classification): implement InvokesPass for confirmed call-linking and open frontiers`

---

### T6: Implement `ExecutesPass`

**What**: Create `ExecutesPass : IClassifierPass` that emits `ConfirmedRelation(Executes)` from each `EntryPoint` to its named callable `Symbol`, with a diagnostic when the `Symbol` is absent from the snapshot.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/ExecutesPass.cs` (new)
**Depends on**: T5 (same `ClassificationAndPromotionStage`; `EntryPoint` facts from WS5A's `EntryPointPass` must exist)
**Reuses**: `IClassifierPass` (WS5A); `ClassifierContext.FactsByType<EntryPoint>()`, `ClassifierContext.FactsByType<Symbol>()`; `ConfirmedRelation.Create`
**Requirement**: CLLF-03

**Tools**:
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [ ] `ExecutesPass` implements `IClassifierPass`; classifier identity `csharp2md.classifier.executes` v1
- [ ] For each `EntryPoint` whose `Symbol` reference resolves in the snapshot: `ConfirmedRelation(Executes, entryPoint.Reference, symbol.Reference, EvidenceMethod.Semantic, targetFact: symbol)` emitted
- [ ] For each `EntryPoint` whose `Symbol` reference is absent: `DiagnosticRecord` naming both IDs; no `Executes` relation
- [ ] One-to-one: each `EntryPoint` produces exactly one `Executes` or one diagnostic
- [ ] Unit tests in `tests/Csharp2Md.Analysis.Tests/Classification/ExecutesPassTests.cs`: `EntryPointWithKnownSymbol_ProducesExecutesRelation`, `EntryPointWithMissingSymbol_ProducesDiagnosticNotRelation`, `MultipleEntryPoints_EachProducesOneExecutes`
- [ ] Integration test: load `Acme.Orders.slnx` through full pipeline; assert each controller action `EntryPoint` produces an `Executes` relation to its callable
- [ ] `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` passes
- [ ] Test count: ≥ 5 new tests

**Tests**: unit + integration
**Gate**: full (Analysis)

**Commit**: `feat(classification): implement ExecutesPass for entry-point-to-callable executes relations`

---

### T7: Wire `InvokesPass` and `ExecutesPass` into `PipelineStages.CreateDefault()`

**What**: Add `InvokesPass` and `ExecutesPass` to the ordered pass list in `ClassificationAndPromotionStage` (or the factory that produces it in `PipelineStages.CreateDefault()`), ensuring they run after WS5A's `RelationPass`.
**Where**: `src/Csharp2Md.Analysis/Pipeline/PipelineStages.cs` and/or wherever `ClassificationAndPromotionStage` is constructed with its pass list
**Depends on**: T6
**Reuses**: `PipelineStages.CreateDefault()` existing pattern; WS5A's pass-list construction
**Requirement**: CLLF-01 through CLLF-14 (pipeline integration)

**Tools**:
- Skill: NONE

**Done when**:
- [ ] `InvokesPass` appears in the `ClassificationAndPromotionStage` pass list after `RelationPass`
- [ ] `ExecutesPass` appears after `InvokesPass`
- [ ] `PipelineStagesTests` (existing) updated to assert the correct total pass count
- [ ] `StageSubstitutionTests` (existing) still pass — wiring test verifies substitution still works
- [ ] `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` passes
- [ ] Test count: existing tests adjusted; no new tests required (integration tests in T5/T6 cover this)

**Tests**: unit
**Gate**: quick (Analysis)

**Commit**: `feat(pipeline): wire InvokesPass and ExecutesPass into ClassificationAndPromotionStage`

---

### T8: Storage round-trip for candidates, unresolved records, and open frontiers

**What**: Verify and fix (if needed) that `CandidateLink`, `UnresolvedRecord`, and `OpenFrontier` records in a `FactualSnapshot` survive the Storage `Stage → Commit → Read` round-trip, and that `PackageValidatorTests` and `FactRoundTripTests` cover them.
**Where**: `tests/Csharp2Md.Storage.Tests/Mapping/`, `tests/Csharp2Md.Storage.Tests/Validation/`; `src/Csharp2Md.Storage/` if any mapper changes are needed
**Depends on**: T7
**Reuses**: Existing `FactRoundTripTests` pattern; `InMemoryTransactionalStore`
**Requirement**: CLLF-01 through CLLF-14 (package completeness — all five record types committed)

**Tools**:
- Skill: `dotnet-test:assertion-quality`

**Done when**:
- [ ] A round-trip test asserts `CandidateLink` present before commit → present after read
- [ ] A round-trip test asserts `UnresolvedRecord` present before commit → present after read
- [ ] A round-trip test asserts `OpenFrontier` present before commit → present after read
- [ ] `PackageValidatorTests` for `BindingDiagnostic.Message` assertions are consistent with T3's fix (no regression)
- [ ] `dotnet test tests/Csharp2Md.Storage.Tests --filter "Category!=LocalCorpus"` passes
- [ ] Test count: ≥ 3 new round-trip tests; no existing tests deleted

**Tests**: unit
**Gate**: full (Storage)

**Commit**: `test(storage): verify CandidateLink, UnresolvedRecord, OpenFrontier survive round-trip`

---

### T9: Update `AnalysisPublicSurfaceTests` and `PortLedgerTests`

**What**: Verify that `AnalysisPublicSurfaceTests` allowlist requires no changes (all new types are `internal`); update `PortLedgerTests` if new storage → domain relations were added; verify `ReceiverShapes` fixture-based integration tests pass end-to-end.
**Where**: `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisPublicSurfaceTests.cs` and `tests/Csharp2Md.Analysis.Tests/Isolation/PortLedgerTests.cs`
**Depends on**: T8
**Reuses**: Existing `AnalysisIsolationTests` and `PortLedgerTests` patterns
**Requirement**: CLLF-15, CLLF-16 (receiver shapes end-to-end)

**Tools**:
- Skill: `dotnet-test:test-anti-patterns`

**Done when**:
- [ ] `AnalysisPublicSurfaceTests` passes without allowlist changes (confirming all new classifier types are `internal`)
- [ ] `PortLedgerTests` passes — no new `Storage → Domain` dependency introduced by 5B
- [ ] Fixture integration test asserts `ReceiverShapes.ViaField`, `ReceiverShapes.ViaProperty`, `ReceiverShapes.ViaPatternVariable`, `ReceiverShapes..ctor` each produce exactly one confirmed `Invokes` to `PaymentClient.Authorize` (CLLF-15/16) — add to `InvokesPassFixtureTests.cs` if not yet present from T5
- [ ] `dotnet test tests/Csharp2Md.Analysis.Tests --filter "Category!=LocalCorpus"` passes
- [ ] Test count: ≥ 4 new or updated integration tests; 0 allowlist changes required

**Tests**: unit + integration
**Gate**: full (Analysis)

**Commit**: `test(isolation): verify 5B types are internal; add ReceiverShapes end-to-end coverage`

---

### T10: Record AD-018 in STATE.md and run full gate

**What**: Append AD-018 (`SymbolFacet.Abstract` decision) to `.specs/STATE.md`; run the full five-project gate; update STATE.md Handoff to reflect 5B Execute complete.
**Where**: `.specs/STATE.md`
**Depends on**: T9
**Reuses**: AD-NNN entry format from existing `STATE.md`
**Requirement**: Project governance (design.md project-level decision note)

**Tools**:
- Skill: NONE

**Done when**:
- [ ] AD-018 appended: `SymbolFacet.Abstract` — Domain extends `SymbolFacetSet` with `Abstract` facet; emitted by `SymbolFactEmitter` for abstract methods and interface members; required by 5B+ classifiers to distinguish abstract/interface targets without re-entering Roslyn
- [ ] `validate_state.py call-linking-flow-frontiers` exits 0 (feature validation report exists — will be written by Verifier, but state gate checks structure)
- [ ] Full build gate passes: all five test projects with `--filter "Category!=LocalCorpus"`
- [ ] Handoff section updated in `STATE.md`

**Tests**: none — governance task; Test Coverage Matrix row for `STATE.md` says none
**Gate**: build (all five projects)

**Commit**: `chore(state): record AD-018 SymbolFacet.Abstract; update handoff for CLLF Execute complete`

---

## Phase Execution Map

```
Phase 1: T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7 -> T8 -> T9 -> T10
```

Execution is strictly sequential — no intra-phase parallelism. A single agent (or batch worker) works one task at a time, in order.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: `SymbolFacet.Abstract` + `SymbolFactEmitter` | 1–2 files, one Domain extension | ✅ Granular |
| T2: Walker target annotation | 1 file, one method change + 1 helper | ✅ Granular |
| T3: Fix WS4 test assertions | 1–2 test files, assertion updates only | ✅ Granular |
| T4: `SnapshotAccumulator` extensions | 1 file, 3 methods | ✅ Granular |
| T5: `InvokesPass` | 1 new source file + 2 new test files | ✅ Granular (cohesive pass + its tests) |
| T6: `ExecutesPass` | 1 new source file + 1 test file | ✅ Granular |
| T7: Pipeline wiring | 1 file + test adjustment | ✅ Granular |
| T8: Storage round-trip | Test files only + possible mapper fix | ✅ Granular |
| T9: Isolation + receiver shapes | Test files, surface check | ✅ Granular |
| T10: STATE.md + full gate | 1 file + gate run | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | Start of Phase 1 | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 (Phase 1→2 boundary) | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | T5 → T6 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T7 | T7 → T8 (Phase 2→3 boundary) | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T9 | T9 → T10 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1: `SymbolFacet.Abstract` | Domain facet enum + `SymbolFactEmitter` | unit | unit | ✅ OK |
| T2: Walker annotation | Extraction — `AlwaysWhenBindableWalker` | unit | unit | ✅ OK |
| T3: Fix WS4 tests | Test files only | — (test repair) | unit | ✅ OK |
| T4: `SnapshotAccumulator` | Analysis pipeline accumulator | unit | unit | ✅ OK |
| T5: `InvokesPass` | Analysis classifier pass | unit + integration | unit + integration | ✅ OK |
| T6: `ExecutesPass` | Analysis classifier pass | unit + integration | unit + integration | ✅ OK |
| T7: Pipeline wiring | Analysis `PipelineStages` | unit | unit | ✅ OK |
| T8: Storage round-trip | Storage mapping | unit | unit | ✅ OK |
| T9: Isolation + receiver shapes | Analysis isolation + integration test | unit + integration | unit + integration | ✅ OK |
| T10: STATE.md | Governance file | none | none | ✅ OK |
