# Call Linking, Flow Frontiers Validation

**Date**: 2026-08-26
**Spec**: `.specs/features/call-linking-flow-frontiers/spec.md`
**Diff range**: `5bc4d68^..80efa26`
**Verifier**: independent sub-agent (author ≠ verifier)
**Result**: FAIL

T1–T10 each have a Conventional Commit on `feature/call-linking-flow-frontiers` (`5bc4d68` … `80efa26`). Implementation starts at `b05dd17`. HEAD is `80efa26`. T10 left `validate_state.py` unchecked because `validation.md` did not exist yet.

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1 | Done | `b05dd17` `SymbolFacet.Abstract` |
| T2 | Done | `bb15dd6` `bound::` signature annotation |
| T3 | Done | `e80c432` WS4 bound-message tests |
| T4 | Done | `8edc6e5` SnapshotAccumulator AddCandidate/Unresolved/OpenFrontier |
| T5 | Done | `95b1d01` InvokesPass |
| T6 | Done | `fd122ec` ExecutesPass |
| T7 | Done | `976827a` pipeline wiring |
| T8 | Done | `cb6b03d` storage round-trip |
| T9 | Done | `85731a1` isolation + ReceiverShapes e2e |
| T10 | Done (partial) | `80efa26` AD-018 + Handoff. `validate_state.py` checkbox deferred (expected); remains unchecked because this report is FAIL |

No blocked tasks. T10's deferred state-gate checkbox is expected and stays open.

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| WHEN Invocation owner and bound target are both callable Symbols THEN ConfirmedRelation Invokes with EvidenceMethod.Semantic (CLLF-01) | Kind Invokes; source = owner; target = bound symbol. EvidenceMethod is a Create-time guard (not a persisted property; same ROSE-17 / EBC-07 pattern). Taxonomy minimum for `invokes` is Semantic | `InvokesPassTests.cs:29-38` `Assert.Single` confirmed; `Assert.Equal(RelationKind.Invokes, relation.Kind)`; `Assert.Equal(source.Reference, relation.Source)`; `Assert.Equal(target.Reference, relation.Target)`; `Assert.Equal(InvokesPass.Identity, relation.Classifier)`; `Assert.Contains(... ObservationKind.Invocation)`; `Assert.Empty` candidates and unresolved | PASS |
| WHEN ObjectCreation owner and constructor resolve to callable Symbols THEN ConfirmedRelation Invokes Semantic (CLLF-02) | Kind Invokes; source = owner; target = constructor symbol | `InvokesPassTests.cs:53-56` `Assert.Equal(RelationKind.Invokes, relation.Kind)`; `Assert.Equal(source.Reference, relation.Source)`; `Assert.Equal(ctor.Reference, relation.Target)` | PASS |
| WHEN EntryPoint has a non-default Symbol THEN ConfirmedRelation Executes from EntryPoint to that Symbol, Semantic (CLLF-03) | Kind Executes; source = EntryPoint; target = named Symbol | `ExecutesPassTests.cs:35-42` `Assert.Equal(RelationKind.Executes, relation.Kind)`; `Assert.Equal(action.Reference, relation.Target)`; `Assert.Equal(ExecutesPass.Identity, relation.Classifier)`; `:150-154` `relation.Kind == "executes"` and `relation.Source.Id == controllerEntry.Identity.Id` and `relation.Target.Id == controllerEntry.Symbol.Id` | PASS |
| SHALL NOT emit confirmed Invokes when source or target FactReference is not a known Symbol (CLLF-04) | No Invokes relation | `InvokesPassTests.cs:72-74` `Assert.DoesNotContain(... relation.Kind is RelationKind.Invokes)` (owner is a Component). Unmatched target: `InvokesPassTests.cs:374-377` external package, `Assert.Empty` confirmed | PASS |
| SHALL NOT emit duplicate ConfirmedRelation for the same (Kind, Source, Target, Facets) (CLLF-05) | One Invokes for two occurrences of the same pair | `InvokesPassTests.cs:90-91` `Assert.Equal(1, result.RelationCount)`; `Assert.Single(...ConfirmedRelations)` | PASS |
| WHEN target signature is stored as structured annotation THEN classifier uses it and SHALL NOT re-invoke Roslyn (CLLF-06) | Diagnostic.Message is `bound::<signature>`; payload empty; InvokesPass reads `TryExtractTargetSignature`; classifier members expose no `Microsoft.CodeAnalysis` | `AlwaysWhenBindableWalkerTests.cs:120-125` `Assert.StartsWith("bound::", ...)`; `Assert.Contains("metadata=Target", ...)`; `Assert.Equal("bound", ...Code)`; payload has no signature key. `:210` `Assert.Equal(signature, TryExtractTargetSignature("bound::" + signature))`. `InvokesPassTests.cs:476` `BoundMessage` is `"bound::" + target.Signature.Value`. `ClassifierIsolationTests.cs:61-77` `Assert.DoesNotContain("InvokesPass")` on public surface; `offending is null` for CodeAnalysis on classifier members | PASS |
| WHEN invocation resolves to interface/abstract AND concrete implementors exist THEN CandidateLink Invokes per implementor, not confirmed (CLLF-07) | Kind Invokes; source = caller; ProposedTarget = concrete; confirmed empty | `InvokesPassTests.cs:113-117` `Assert.Empty` confirmed; `Assert.Equal(RelationKind.Invokes, candidate.Kind)`; `Assert.Equal(source.Reference, candidate.Source)`; `Assert.Equal(implementor.Reference, candidate.ProposedTarget)` | PASS |
| WHEN abstract/interface AND no concrete implementor THEN UnresolvedRecord NoCandidateFound referencing the observation (CLLF-08) | Kind Invokes; cause NoCandidateFound; source = caller | `InvokesPassTests.cs:140-145` `Assert.Equal(RelationKind.Invokes, unresolved.Kind)`; `Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause)`; `Assert.Equal(source.Reference, unresolved.Source)`; `Assert.Empty` confirmed. `Available` / observation identity is not asserted on the classifier path (round-trip `InvokesRecordRoundTripTests.cs:61-66` asserts kind/source/cause on a constructed record) | PASS |
| SHALL NOT emit confirmed Invokes whose target is an interface or abstract member (CLLF-09) | No confirmed relation targeting the abstract symbol | `InvokesPassTests.cs:113` `Assert.Empty` confirmed; `:118-120` `Assert.DoesNotContain(... relation.Target.Equals(abstractTarget.Reference))` | PASS |
| WHEN declared receiver is the concrete type THEN confirmed Invokes to the concrete Symbol, not a candidate (CLLF-10) | Confirmed target = concrete; candidates empty | `InvokesPassTests.cs:167-169` `Assert.Equal(concrete.Reference, relation.Target)`; `Assert.Empty` candidates | PASS |
| WHEN Invocation targets a delegate, unmatched local function, or System.Reflection.* THEN OpenFrontier FurtherContinuationObserved (CLLF-11) | No confirmed Invokes; frontier cause FurtherContinuationObserved | Unbound: `InvokesPassTests.cs:188-192` `Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause)`; `Assert.Equal(FrontierCause.FurtherContinuationObserved, frontier.Cause)`; `Assert.Empty` confirmed. Delegate: `:208-210` empty confirmed, single frontier, single unresolved. Reflection: `:232-233` empty confirmed, single frontier. `OpenFrontier.Occurrence` is not asserted on the classifier path (round-trip `InvokesRecordRoundTripTests.cs:83-85` asserts occurrence + cause on a constructed frontier) | PASS |
| WHEN generic type-parameter call and no concrete Symbol can be confirmed THEN CandidateLink if candidates exist, else UnresolvedRecord NoCandidateFound, plus OpenFrontier FurtherContinuationObserved (CLLF-12) | Candidates path: ProposedTarget = named callable, frontier present, no confirmed. Else: cause NoCandidateFound + frontier | With named: `InvokesPassTests.cs:250-253` `Assert.Equal(named.Reference, candidate.ProposedTarget)`; `Assert.Single` frontiers; `Assert.Empty` confirmed. Without: `:269-271` `Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause)`; `Assert.Single` frontiers | PASS |
| WHEN a call site produces UnresolvedRecord THEN also OpenFrontier on that same observation identity, unless InsufficientEvidence (CLLF-13) | Unresolved and frontier both present (NoCandidateFound path) | `InvokesPassTests.cs:292-293` `Assert.Single` unresolved and `Assert.Single` frontiers. Same-identity coupling is not asserted. Unless-clause is CLLF-14 | PASS |
| IF owner is project-level fallback Symbol THEN UnresolvedRecord InsufficientEvidence and SHALL NOT emit OpenFrontier (CLLF-14) | Cause InsufficientEvidence; frontiers empty; no confirmed Invokes | `InvokesPassTests.cs:311-314` `Assert.Equal(UnresolvedCause.InsufficientEvidence, unresolved.Cause)`; `Assert.Empty` frontiers; `Assert.Empty` confirmed | PASS |
| WHEN owner is established via field, property, constructor-parameter, or pattern-variable receiver THEN same confirmed Invokes as a local (CLLF-15) | Each ReceiverShapes member has exactly one Invokes to PaymentClient.Authorize | `InvokesPassFixtureTests.cs:38-44` `Assert.Single(invokes)`; `Assert.Equal("invokes", relation.Kind)`; source contains owner member and `ReceiverShapes`; target contains `PaymentClient` and `Authorize` | PASS |
| SHALL NOT produce duplicate invokes for the same (source, target) across receiver shapes in one callable (CLLF-16) | Four distinct (source, target) pairs, one per shape | `InvokesPassFixtureTests.cs:56-63` `Assert.Equal(4, invokes.Length)` and distinct source+target count equals length; `Assert.Contains` ViaField, ViaProperty, ViaPatternVariable, `.ctor` | PASS |
| WHEN target signature matches a Symbol in a different project in the same solution THEN confirmed Invokes (CLLF-17) | Source OrderService.AuthorizeViaPaymentClientAsync; target PaymentClient.Authorize; projects differ | Unit: `InvokesPassTests.cs:350-353` source/target refs and `Assert.NotEqual(source.OwningProject, target.OwningProject)`. Fixture: `InvokesPassFixtureTests.cs:17-24` `relation.Kind == "invokes"` and source/target id fragments | PASS |
| WHEN target project is outside solution scope THEN UnresolvedRecord NoCandidateFound (CLLF-18) | Cause NoCandidateFound; frontier present; no confirmed | `InvokesPassTests.cs:374-377` `Assert.Equal(UnresolvedCause.NoCandidateFound, unresolved.Cause)`; `Assert.Single` frontiers; `Assert.Empty` confirmed | PASS |
| WHEN Invocation/ObjectCreation is skipped by all passes (e.g. fallback owner already recorded InsufficientEvidence) THEN SHALL NOT emit a diagnostic redundantly — UnresolvedRecord is sufficient (CLLF-19) | No DiagnosticRecord on that skip path | no evidence | GAP |
| WHEN Invocation binds to a framework method outside solution scope THEN silent skip: no UnresolvedRecord, no diagnostic (CLLF-20) | Confirmed, unresolved, frontiers, diagnostics all empty for `global::System.` / `global::Microsoft.` | `InvokesPassTests.cs:398-401` `Assert.Empty` confirmed, unresolved, frontiers, diagnostics. `:422-424` Microsoft.Extensions similarly. Fixture: `InvokesPassFixtureTests.cs:73-86` `Assert.DoesNotContain` System/Microsoft targets; no diagnostic code containing `invokes` | PASS |

**Status**: Gaps present. 19/20 ACs matched spec outcome. 2 spec-precision items flagged (Independent Test vs CLLF-07/09; multi-match edge). CLLF-19 has no `file:line` evidence.

P1 Independent Test (`OrderService.PlaceOrderAsync` → `IEventBus.PublishAsync` confirmed Invokes) contradicts CLLF-07/09 (interface member → CandidateLink, not confirmed). Tests follow CLLF-07/09. Flagged as spec-precision, not an uncovered numbered AC.

---

## Discrimination Sensor

| Mutation | File:line | Description | Killed? |
| -------- | --------- | ----------- | ------- |
| — | — | Not run | SKIPPED |

**Sensor depth**: skipped
**Result**: SKIPPED (standing user request for csharp2md, same as `symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`, `knowledge-taxonomy-contract`, `engine-bootstrap`, `factual-storage`, `roslyn-observation-extraction`, and `entrypoints-boundaries-contracts`). No git worktree, no file mutation, no Stryker.

Static gap analysis only (`dotnet-test:test-gap-analysis` step 4 without 4b live mutation):

- Flipping `EvidenceMethod.Semantic` to `Syntactic` on `ConfirmedRelation.Create` for Invokes would fail Domain `RequireSufficientEvidence` (minimum Semantic). Construction-killed.
- Removing `bound::` parsing (`TryExtractTargetSignature` always null) would drop CLLF-01 confirmed edges; `InvokesPassTests.Execute_BoundMethodTargetInSameProject_CreatesConfirmedInvokesWithSemanticEvidence` would fail.
- Emitting a diagnostic on the fallback-owner path would not fail any current test. That is the CLLF-19 gap: `Execute_FallbackOwner_EmitsInsufficientEvidenceWithoutOpenFrontier` does not `Assert.Empty` diagnostics (`InvokesPassTests.cs:311-314`). Unverified (static reasoning); treated as an uncovered AC, not as a sensor kill.
- `SymbolFactEmitter.Facets` adding `Abstract` for interface/abstract methods has no emitter test. CLLF-07/09 unit tests inject `SymbolFacet.Abstract` by hand, so a missing emitter branch would not fail them. Fixture tests never assert `IEventBus.PublishAsync` as CandidateLink. Unverified (static reasoning); not scored as an uncovered numbered AC.

---

## Interactive UAT Results

Not performed. This feature is backend/CLI infrastructure; automated checks are sufficient per validate.md.

---

## Code Quality

| Principle | Status |
| --------- | ------ |
| Minimum code | PASS |
| Surgical changes | PASS |
| No scope creep | PASS |
| Matches patterns | PASS |
| Spec-anchored outcome check (asserted values match spec) | FAIL (CLLF-19 no evidence) |
| Per-layer Coverage Expectation met (domain 1:1 ACs; analysis classifiers happy+edge+error; storage round-trip) | FAIL (P3 diagnostic skip path has no test) |
| Every test maps to a spec requirement - no unclaimed tests | PASS |
| Documented guidelines followed: `AGENTS.md` / `CLAUDE.md` (net10.0, Workspaces.MSBuild 5.6.0, no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults`, SyntheticSolution-only versioned fixture) | PASS |

Diff `5bc4d68^..80efa26` stays on Domain `SymbolFacet.Abstract`, extractor `bound::` annotation, SnapshotAccumulator lists, `InvokesPass` / `ExecutesPass`, pipeline wiring, storage round-trip tests, and matching tests. No unrelated improvements. No `// SPEC_DEVIATION` in 5B sources. Pre-existing `BoundaryPass.cs` EBC-06 markers are out of this diff.

`InvokesPass` and `ExecutesPass` have no `Microsoft.CodeAnalysis` usings. Classification members do not expose Roslyn types (`ClassifierIsolationTests.cs:71-77`). `Directory.Packages.props` pins `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0. Target framework is `net10.0`.

Spot-check (P1 call edges): CLLF-01/07/14/20 assert kind, source/target or proposed target, cause enums, and negatives (empty confirmed / empty frontiers). Not trait-only.

Payload/conjunction: classifier tests assert Kind/Source/Target/Cause on the records they emit. `OpenFrontier.Occurrence` and `UnresolvedRecord.Available` are asserted on storage round-trip of constructed records, not on classifier emission. Noted; not scored as extra AC gaps.

`dotnet-test:assertion-quality` / `test-anti-patterns`: no assertion-free tests in the new files; no `Thread.Sleep`; no always-true asserts. CLLF-13's dedicated test is presence-only (`Assert.Single` ×2) and does not pin observation identity. Identity tests for classifier id/version are Done-when, not unclaimed.

---

## Edge Cases

- [ ] Signature matches multiple Symbol facts → UnresolvedRecord(InsufficientEvidence): spec also says a non-corrupt accumulator makes the match unique (WS4 identity). `InvokesPass.cs:133-137` emits CandidateLinks + OpenFrontier when several non-abstract signature matches exist. **⚠️ Spec-precision gap**. No test asserts `UnresolvedCause.InsufficientEvidence` for multi-match.
- [x] EntryPoint Symbol missing from snapshot: diagnostic naming both ids, no Executes — `ExecutesPassTests.cs:61-67` `Assert.Equal(0, result.RelationCount)`; `Assert.Empty` confirmed; `Assert.Equal("executes-pass", diagnostic.Code)`; message contains entry and missing symbol ids
- [ ] Empty or default AnalysisVariants: pass must validate before `ConfirmedRelation.Create`. Production guards: `InvokesPass.cs:159-163` `continue`; `ExecutesPass.cs:26-29` early return. **No dedicated test**
- [x] SnapshotAccumulator AddCandidate / AddUnresolved / AddOpenFrontier: `SnapshotAccumulatorTests.cs:188-219` `Assert.Equal` the added record in the snapshot; `:224-236` empty collections when nothing added
- [ ] Zero entry points → no Executes: `ExecutesPass` iterates an empty list. **No dedicated test**. Valid library-project outcome is unasserted

---

## Gate Check

- **Gate command**: `dotnet build` then per-csproj `dotnet test` with VSTest `--filter "Category!=LocalCorpus"` (xUnit 2.9.3 + `xunit.runner.visualstudio`, not xUnit v3 MTP `--filter-not-trait`). Multi-csproj `dotnet test` hits MSB1008.
- **Result**: 1075 passed, 0 failed, 0 skipped among selected tests
- **Per-project passed counts**:
  - `Csharp2Md.Domain.Tests`: 549 passed
  - `Csharp2Md.Analysis.Tests`: 330 passed
  - `Csharp2Md.Storage.Tests`: 166 passed
  - `Csharp2Md.Cli.Tests`: 27 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Projection.Tests`: 3 passed
- **Test count before feature** (EBC, `Category!=LocalCorpus`): 1019
- **Test count after feature**: 1075
- **Delta**: +56 executed tests. Increase; no silent deletions.
- **Skipped tests**: none among the filtered gate. `LocalCorpusAnalyzeTests` (2 theory cases) excluded by `Category!=LocalCorpus`.
- **Failures**: none on the serial Category!=LocalCorpus gate.

---

## LocalCorpus

Ran `dotnet test tests/Csharp2Md.Cli.Tests --filter "Category=LocalCorpus"`.

- `fixtures/eShop`: absent. Theory case threw `$XunitDynamicSkip$local eShop clone is not present at '...eShop.slnx'`. Expected skip.
- `fixtures/eShopOnContainers`: directory present (`ApiGateways`, `Services`, `Web`, …) but `eShopOnContainers-ServicesAndWebApps.sln` is absent (no `.sln` / `.slnx` anywhere in the clone). Theory case threw `$XunitDynamicSkip$local eShopOnContainers clone is not present at '...eShopOnContainers-ServicesAndWebApps.sln'`. Analyze did **not** run.

This runner recorded both `$XunitDynamicSkip$` throws as **failed** tests (2 failed, 0 skipped), not as xUnit dynamic skips. That is a runner/skip-protocol mismatch, not a 5B product defect. The clone is incomplete, so the present-directory rule could not be satisfied.

**Blocker**: eShopOnContainers was not analyzed. Do not treat this as a green LocalCorpus pass. Clones were not added to git.

---

## Fix Plans

### Fix 1: CLLF-19 has no test evidence

- **Root cause**: No test is tagged `CLLF-19`. The fallback-owner path records `UnresolvedRecord(InsufficientEvidence)` (`InvokesPassTests.cs:311-314`) but never asserts `Assert.Empty(...Diagnostics)`. `InvokesPass` never calls `AddDiagnostic`, so the SHALL NOT is true by construction and unenforced.
- **Fix task**: In `InvokesPassTests`, on the fallback-owner (and owner-not-symbol skip) paths, assert `Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics)` in addition to the existing unresolved/frontier asserts. Tag `[Trait("Requirement", "CLLF-19")]`.
- **Priority**: Major (uncovered AC; P3 but listed)

### Fix 2: Strengthen observation-identity conjunction (optional with Fix 1)

- **Root cause**: CLLF-11/13 name “on that observation identity”. Classifier tests assert frontier/unresolved counts and causes, not `frontier.Occurrence` or `unresolved.Available.DerivedFrom`.
- **Fix task**: Assert `frontier.Occurrence` equals the invocation identity and `unresolved.Available.DerivedFrom` contains that identity on the unbound / external-package paths.
- **Priority**: Minor

---

## Requirement Traceability Update

Not applied to Verified (overall FAIL). CLLF-19 set to Needs Fix in `spec.md`. Other IDs stay Implementing.

| Requirement | Previous Status | New Status |
| ----------- | --------------- | ---------- |
| CLLF-01..CLLF-18, CLLF-20 | Implementing | Implementing (evidence present; not Verified while feature FAIL) |
| CLLF-19 | Pending | ❌ Needs Fix |

---

## Summary

**Overall**: Not Ready

**Spec-anchored check**: 19/20 ACs matched spec outcome | 1 uncovered AC (CLLF-19) | 2 spec-precision gaps
**Sensor**: SKIPPED (standing user request)
**Gate**: 1075 passed (`Category!=LocalCorpus`)

**What works**: Bound same-solution and cross-project callable invocations confirm `invokes`. Abstract/interface targets become candidates or `NoCandidateFound`. Fallback owners are unresolved without a frontier. BCL/Microsoft calls are silent. ReceiverShapes each confirm one `invokes` to `PaymentClient.Authorize`. Entry points confirm `executes`. Storage round-trips candidates, unresolved records, and frontiers. Build gate is green.

**Issues found**: CLLF-19 (no-redundant-diagnostic on skip/fallback) has no `file:line` evidence. Independent Test vs CLLF-07/09 conflict is unresolved in spec prose. Multi-match edge is underspecified. LocalCorpus could not analyze eShopOnContainers (sln missing).

**Next steps**: Route Fix 1 to an implementer, then re-dispatch the Verifier. Restore or complete the eShopOnContainers solution file if LocalCorpus analyze is required.
