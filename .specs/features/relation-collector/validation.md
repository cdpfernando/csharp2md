# RelationCollector Validation

**Date**: 2026-08-19
**Spec**: `.specs/features/relation-collector/spec.md`
**Diff range**: `4a6783a..HEAD` (`c9d7df8`..`9ea6d81`, branch `feat/relation-collector`)
**Verifier**: independent sub-agent (author ≠ verifier)

**Standing deviation (user-confirmed, cited from `tasks.md`'s header):** "the automated Verifier's
discrimination-sensor (mutation-testing) sub-step is skipped for this feature by explicit user request — the
user will run Stryker manually afterward." Step 5 of `validate.md` (Discrimination Sensor) was skipped
entirely — no mutations injected, no scratch worktree created. Every other step ran in full.

---

## Validation: RelationCollector - PASS ✅

---

## Task Completion

| Task | Status  | Notes |
| ---- | ------- | ----- |
| T1   | Done | `FactResolution` 7-member order + `FactMerger.ResolutionRank` ladder confirmed in code and tests. |
| T2   | Done | `RelationPartition.Structural` + `RelationProjector.Partitions`/`Wire()` confirmed. |
| T3   | Done | `FactValidator.ValidateRelation` gate moved from `IsRuntime` to `!CompileTimeOnlyRelationKinds.Contains(...)`. |
| T4   | Done | `SyntacticRelationCandidate` carries a real evidence span for every candidate (verified by inspection and by `Extract_BaseListCandidateSpan_PointsAtTheExactSourceLocationOfTheEntry`). |
| T5   | Done | `RelationNoiseFilter` exists with both methods and full test coverage. Production wiring gap noted below (Code Quality). |
| T6   | Done | inherits/implements split matches design.md's position + `I`-prefix rule exactly. |
| T7   | Done | publishes/subscribes/handles syntax-only detection, all edge cases from spec.md covered. |
| T8   | Done | http-client/http-call syntax-only detection, false-positive guard tested. |
| T9   | Done | calls/creates detection via `RelationNoiseFilter`, no double-emission. |
| T10  | Done | references detection from `RelevantTypeReferences`, dedup against claimed kinds. |
| T11  | Done | `RelationCollector.CreateFacts` — all 10 kinds map to correct partition, evidence/provenance/null-target/unresolved-reason unconditional. |
| T12  | Done | Wired into `AnalysisEngine.AnalyzeProjectAsync`; `V3DeterminismTests` snapshot re-approved exactly as the note describes (`relation_count: 0→9`, `relations: {} → syntactic: 6, unresolved: 3`). |
| T13  | Done | `RelationCollector.Refine` — inherits/implements upgrade via `TypeKind`, inferred-publish discovery, identical `RelationFactId`s proven by dedicated test. See Code Quality: calls/creates semantic filtering described in design.md's T13 "What" was not implemented. |
| T14  | Done | Wired into `TrustedSemanticProjectProcessor.BindDocuments`; `EnrichedRelations` plumbing mirrors `EnrichedSymbols` exactly. |
| T15  | Done | `Detection/Messaging/*` and `Detection/Http/*` (both source and test files) are deleted; only doc-comment references to the retired type names remain, in `RelationCollector.cs`'s own XML docs. |
| T16  | Done | `RelationCollectorEndToEndTests.cs` restates every P1/P2 Independent Test scenario as a named assertion against the real `fixtures/SyntheticSolution`. |

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| RELC-01 — P1 umbrella: messaging/HTTP relations survive incomplete semantic binding | `relations: []` no longer the outcome for `PaymentsService.cs`/`OrderService.cs` in default mode | `tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorEndToEndTests.cs:55` — `Assert.NotEmpty(payments.Relations); Assert.NotEmpty(orders.Relations);` (real `AnalysisEngine.AnalyzeAsync` run, default syntax-only mode) | ✅ PASS |
| RELC-02 — `Subscribe`/`SubscribeAsync` with one explicit generic type arg → `subscribes`, `target_text` = simple name | `target_text` equals the type argument's simple name | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs:227` — `Extract_SubscribeWithNamedHandlerMethod_EmitsSubscribesAndHandlesOwnedByHandler`: `Assert.Equal("OrderPlaced", subscribes.ObservedTarget)` | ✅ PASS |
| RELC-03 — that same call, argument names an existing method → additional `handles` fact, `SourceId` = handler's own declaration id | `handles.OwnerId` (materializes to `SourceId`) equals the handler method's own `SymbolFactId` | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs:227` — same test: `Assert.Equal(handlerSymbol.SymbolId.ToFactId(), handles.OwnerId)` | ✅ PASS |
| RELC-04 — `Publish`/`PublishAsync`, no explicit `<T>`, first arg is object-creation → `publishes`, `target_text` = created type's simple name | `target_text` equals the created type's simple name | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs:193` — `Extract_PublishAsyncWithObjectCreationArgumentAndNoExplicitTypeArgument_EmitsPublishes`: `Assert.Equal("PaymentProcessed", candidate.ObservedTarget)` | ✅ PASS |
| RELC-05 — `CreateClient` with single string-literal arg → `http-client`, `target_text` = literal | `target_text` equals the literal value | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs:306` — `Extract_CreateClientWithStringLiteralArgument_EmitsHttpClient`: `Assert.Equal("PaymentService", candidate.ObservedTarget)` | ✅ PASS |
| RELC-06 — HTTP-verb-named invocation on `HttpClient`-shaped receiver → `http-call` carrying HTTP method + route | Correct `http_method` and route detail | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs:323` — `Extract_HttpVerbNamedInvocationOnClientFromCreateClient_EmitsHttpCallWithMethodAndRoute`: `Assert.Equal("http_method=POST\|route=payments/authorize", candidate.ObservedTarget)` | ✅ PASS |
| RELC-07 — WHILE `SemanticModel` unavailable, every P1 relation kind still produced with `Unresolved`/`Syntactic` (never `Exact`) and a populated `UnresolvedReason` | Non-`Exact` resolution + non-empty `UnresolvedReason` on every relation, syntax-only mode | `tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorWiringTests.cs:56` — `AnalyzeAsync_SyntaxOnlyMode_EveryEmittedRelation_HasNullTargetIdAndUnresolvedReason`: `Assert.All(document.Relations, relation => { Assert.Null(relation.TargetId); Assert.False(string.IsNullOrWhiteSpace(relation.UnresolvedReason)); })` | ✅ PASS |
| RELC-08 — `SemanticModel` available but target/receiver doesn't fully resolve → still emit from syntactic shape, not dropped | Relation present at the syntax-only baseline resolution rather than absent | `tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorTrustedWiringTests.cs:44` — `AnalyzeAsync_TrustedMode_UnresolvedCrossProjectBaseType_StillYieldsSyntaxOnlyBaselineRelation`: `Assert.Single(payments.Relations, r => r.RelationKind == "inherits")` against the fixture's genuinely-unresolvable `Payments.PaymentsBase`; unit-level in `tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationCollectorTests.cs:186` — `Refine_UnresolvedBaseListEntry_ProducesNoEnrichmentAndDoesNotThrow`: `Assert.Empty(refined)` (no throw, no wrong upgrade) | ✅ PASS |
| RELC-09 — replace both detectors; scenarios they fully resolved keep ≥ the same confidence | `MessagingRelationDetector`/`HttpRelationDetector` deleted; the "inference through a variable" case (their prior no-regression bar) still resolves | Deletion confirmed by `git grep` (0 hits outside doc comments) for `src/Csharp2Md.Core/Detection/Messaging/*`, `src/Csharp2Md.Core/Detection/Http/*`, and both test files; regression proof at `tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationCollectorTests.cs:130` — `Refine_PublishAsyncThroughVariable_ProducesPublishesFactWithInferredTargetTextThatCreateFactsCannot`, and end-to-end at `tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorTrustedWiringTests.cs:90` — `AnalyzeAsync_TrustedMode_PublishAsyncThroughAVariable_ResolvesTargetTextThroughTheRealPipeline` | ✅ PASS |
| RELC-10 — `Evidence` attached to every relation, unconditionally, regardless of partition | Non-empty `Header.Evidence` on every relation kind including `Inheritance`/`Structural` (not just `IsRuntime` partitions) | `tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationCollectorTests.cs:49` — `CreateFacts_EveryFact_CarriesEvidenceProvenanceNullTargetAndUnresolvedReason`: `Assert.All(facts, fact => Assert.NotEmpty(fact.Header.Evidence))` over all 10 kinds; tightened validator proven at `tests/Csharp2Md.Core.Tests/Facts/Validation/FactValidatorTests.cs:146` and `:157` (`Inheritance` partition now rejected without evidence/provenance) | ✅ PASS |
| RELC-11 — `TargetId` always `null`, `UnresolvedReason` always populated | `TargetId == null` and non-blank `UnresolvedReason` on every produced relation | `tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationCollectorTests.cs:49` — same test: `Assert.Null(fact.TargetId); Assert.False(string.IsNullOrWhiteSpace(fact.UnresolvedReason));`; reasserted end-to-end at `tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorEndToEndTests.cs:20-34` | ✅ PASS |
| RELC-12 — `calls` for member-access invocation not claimed by http-call/publishes/subscribes, receiver not BCL-excluded | `target_text` = `{receiver}.{member}` | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs:358` — `Extract_MemberAccessInvocationOnApplicationTypedReceiver_EmitsCallsWithReceiverDotMember`: `Assert.Equal("paymentsClient.AuthorizePayment", candidate.ObservedTarget)`; end-to-end at `tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorEndToEndTests.cs:64` | ✅ PASS |
| RELC-13 — `creates` for object-creation not BCL-excluded, not already a `publishes` argument | `target_text` = created type's simple name | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs:390` — `Extract_NewOfApplicationType_EmitsCreates`: `Assert.Equal("PaymentAuthorizer", candidate.ObservedTarget)`; non-double-emission at `:422` — `Extract_ObjectCreationConsumedAsPublishesTarget_IsNotDoubleEmittedAsCreates` | ✅ PASS |
| RELC-14 — base-list entry → `inherits`/`implements` per position + `I`-prefix rule | Position/naming-convention classification exactly per design.md's rule table | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs:161` (qualified base, not `I`-prefixed → `inherits`), `:173` (single `I`-prefixed → `implements`/`Heuristic`), `:183` (unresolvable type-parameter bound → `implements`/`Unresolved`, spec.md's Edge Case); fixture proof at `tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorEndToEndTests.cs:75` — `PaymentsBase`/`Payments.PaymentsBase` | ✅ PASS |
| RELC-15 — `references` from field/property/parameter type or generic argument not already claimed, not a same-document type | `target_text` = referenced type's simple name | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs:454` — `Extract_PropertyTypedAsApplicationType_EmitsReferences`: `Assert.Equal("PaymentAuthorizer", candidate.ObservedTarget)`; dedup at `:474` (primitive/`CancellationToken` excluded) and `:489` (already-claimed-by-`creates` excluded) | ✅ PASS |
| RELC-16 — one shared BCL/framework exclusion rule for both `calls` and `creates` | Identical `RelationNoiseFilter.IsLikelyFrameworkType` predicate used by both detection passes | `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs:560` (`ClassifyCallsInvocation`) and `:145` (`creates` loop) both call `RelationNoiseFilter.IsLikelyFrameworkType`; denylist coverage at `tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationNoiseFilterTests.cs:51-60` | ✅ PASS |
| RELC-17 — WHILE `SemanticModel` unavailable, `calls`/`creates`/`inherits`/`implements`/`references` still emitted from syntax alone | Same as RELC-07, scoped to the P2 kinds | `tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorWiringTests.cs:41` — `AnalyzeAsync_SyntaxOnlyMode_OrderServiceDocument_EmitsHttpClientHttpCallAndCallsRelations` and `:20` (inherits), both run with no `SemanticModel` (default mode, confirmed by the fixture using `AnalysisRequest.Create` with no trust flag) | ✅ PASS |

**Status**: ✅ All 17 RELC-NN ACs covered, spec-anchored, evidence-or-zero. No spec-precision gaps — every criterion in spec.md defines a precise outcome (a target_text value, a resolution enum member, a partition, a null/non-null check) and each is matched exactly by its cited assertion, not merely "an assertion exists."

---

## Edge Cases (spec.md)

- [x] `PublishAsync<OrderPlaced>(existingInstance)` (explicit `<T>`, non-object-creation arg) → reads from the type argument regardless of argument shape — `SyntaxFactExtractorTests.cs:210` (`Extract_PublishWithExplicitTypeArgument_EmitsPublishesFromTypeArgumentRegardlessOfArgumentShape`)
- [x] Neither explicit `<T>` nor object-creation argument present → no relation emitted — `SyntaxFactExtractorTests.cs:266` (`Extract_PublishAsyncWithNoExplicitTypeArgumentAndNonObjectCreationArgument_EmitsNoCandidate`)
- [x] `Subscribe<T>` with inline lambda handler → `subscribes` only, no `handles` — `SyntaxFactExtractorTests.cs:250` (`Extract_SubscribeWithInlineLambdaHandler_EmitsSubscribesOnlyNoHandles`)
- [x] Same kind + target_text recurring in one document → ordinal disambiguation, no duplicate IDs — `RelationCollectorTests.cs:92` (`CreateFacts_TwoCandidatesSameKindAndTarget_GetDistinctOrdinalDisambiguatedIds`), asserting distinct `RelationId`s with `;ordinal=1`/`;ordinal=2`
- [x] Base-list entry unclassifiable even heuristically → defaults to `implements`/`Unresolved`, never `inherits` — `SyntaxFactExtractorTests.cs:183` (`Extract_BaseListEntryNamingTheDeclaringTypesOwnTypeParameter_DefaultsToImplementsUnresolved`)

All five listed edge cases handled correctly with direct, named test evidence.

---

## Code Quality Check

| Check | Pass? |
| --- | --- |
| No features beyond what was asked | ✅ |
| No abstractions for single-use code | ✅ |
| No unnecessary "flexibility" added | ✅ |
| Only touched files required for task | ✅ — diff surface matches design.md's Integration Points table exactly |
| Didn't "improve" unrelated code | ✅ |
| Matches existing patterns/style | ✅ — `EnrichedRelations` mirrors `EnrichedSymbols` plumbing verbatim; `RelationCollector` ports the `EvidenceFor`/ordinal pattern from the retired detectors |
| Would senior engineer approve? | ⚠️ — see finding below |
| Tests map to acceptance criteria and are non-shallow (spot-check one story) | ✅ — spot-checked P1 (messaging/HTTP) end-to-end and unit layers; every assertion targets a concrete value, not presence-only |
| Spec-anchored outcome check | ✅ — see AC table above |
| Per-layer Coverage Expectation met | ✅ — domain logic (`SyntaxFactExtractor`, `RelationCollector`) has dense 1:1 AC/edge-case mapping; integration layer covers syntax-only + trusted + fixture-level for both stories |
| Every test in scope maps to a spec AC, listed edge case, or Done-when criterion | ✅ — no unclaimed tests found in the reviewed files |
| Documented project quality/testing guidelines followed | ✅ — `AGENTS.md`/`CLAUDE.md` route to `dotnet-test:*` gates; T16's own note records `dotnet-test:assertion-quality` and `dotnet-test:test-anti-patterns` were run and one weak assertion (`AssertAtLeastSyntactic`) was tightened in the same commit — confirmed present as `Assert.Equal("syntactic", ...)` at `RelationCollectorTrustedWiringTests.cs:63`, not the weaker `NotEqual` |

**Finding (Minor, not a spec-AC gap):** `RelationNoiseFilter.IsFrameworkType(ITypeSymbol)` (T5) is dead production code.
`git grep -n "IsFrameworkType"` shows it is called only from its own unit test
(`RelationNoiseFilterTests.cs:77,93`) — never from `RelationCollector.Refine` or anywhere else in `src/`.
design.md's Component description and T13's "What" both state Refine "applies
`RelationNoiseFilter.IsFrameworkType(ITypeSymbol)` to `calls`/`creates` using the real resolved type"; the
actual `Refine` implementation (`src/Csharp2Md.Core/Analysis/Relations/RelationCollector.cs:80-109`) only
ever processes `inherits`/`implements` candidates and the separate `DiscoverInferredPublishes` pass — it
never iterates `calls`/`creates` candidates at all. This is not a spec-AC failure: spec.md's RELC-16 only
requires the **syntax-only** shared predicate (`IsLikelyFrameworkType`), which both `calls` and `creates`
correctly apply identically. No spec criterion requires trusted-mode noise-filtering refinement for
`calls`/`creates`. But it is a genuine design-vs-implementation mismatch: a semantic method built and tested
for a stated purpose that the wiring never uses, silently left dead. Not blocking; worth a follow-up task
(either wire it into `Refine`, matching design.md, or delete the unused method and its now-misleading design
prose in a future pass).

---

## Discrimination Sensor

**Skipped per explicit user request.** `tasks.md`'s header states, verbatim: "the automated Verifier's
discrimination-sensor (mutation-testing) sub-step is skipped for this feature by explicit user request — the
user will run Stryker manually afterward." No mutations were injected, no scratch worktree was created, and no
`git status --porcelain` isolation check was needed as a result. This aligns with `spec.md`'s own Out of Scope
table, which lists "Discrimination-sensor / mutation validation (Stryker) at the Verifier step" as an
explicitly excluded item for this feature.

---

## Gate Check

- **Gate command**: `dotnet build csharp2md.slnx -c Release` → `dotnet format csharp2md.slnx --verify-no-changes` → `dotnet test csharp2md.slnx`
- **Build**: 0 warnings, 0 errors.
- **Format**: `--verify-no-changes` exits 0, no reformatting needed.
- **Result**: 1207 passed, 1 failed, 0 skipped, 1208 total.
- **Failure**: `Csharp2Md.Core.Tests.Analysis.Semantics.MSBuild.DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` — disclosed by the implementers as a pre-existing, unrelated temp-file-cleanup race, order-dependent under full-suite parallel execution. **Independently reproduced and confirmed**: this test fails in the full-suite run and in the `Category=Integration` filtered run (which runs concurrently with other MSBuild-evaluation tests), but passes cleanly in isolation (`dotnet test --filter "FullyQualifiedName~ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml"` → 1/1 passed). The test file (`DotnetMsBuildEvaluatorTests.cs`) is untouched by this feature's diff (confirmed: not in `git diff --stat 4a6783a..HEAD`). Claim confirmed, not merely trusted.
- **Test count before feature**: not independently re-derived by re-running the pre-feature commit (out of budget for this pass); inferred from the diff stat instead — two test files deleted wholesale (`HttpRelationDetectorTests.cs`, 246 lines; `MessagingRelationDetectorTests.cs`, 247 lines) against five new test files (`RelationCollectorEndToEndTests.cs`, `RelationCollectorTrustedWiringTests.cs`, `RelationCollectorWiringTests.cs`, `RelationCollectorTests.cs`, `RelationNoiseFilterTests.cs`) plus substantial additions to `SyntaxFactExtractorTests.cs` (+416 lines), `FactValidatorTests.cs`, `FactMergerTests.cs`, `FactualModelTests.cs`, `RelationProjectorTests.cs`. Net test-file line delta is strongly positive; T16's own note self-reports "all 30 relation-collector-scoped tests re-run green."
- **Test count after feature**: 1208 total.
- **Skipped tests**: none.
- **Failures**: 1, disclosed and confirmed pre-existing/unrelated (see above). No relation-collector-scoped test failed.

---

## Fix Plans

None. No blocking gaps found. One Minor code-quality finding (dead `IsFrameworkType(ITypeSymbol)` method) is not a spec-AC gap and does not require a fix task to close this feature; recorded above for a future pass.

---

## Requirement Traceability Update

`spec.md`'s Requirement Traceability table already lists all 17 RELC-NN IDs as `Status: Verified` before this
Verifier ran. Independent re-derivation above confirms every one of those 17 verdicts is correct — no status
in the table required correction.

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| RELC-01 .. RELC-17 | Verified (author-claimed) | ✅ Verified (independently confirmed) |

---

## Summary

**Overall**: ✅ Ready

**Spec-anchored check**: 17/17 ACs matched spec outcome, 0 spec-precision gaps
**Sensor**: skipped per user request (tasks.md header note) — user will run Stryker manually
**Gate**: 1207 passed, 1 failed (disclosed pre-existing, unrelated, independently confirmed to pass in isolation)

**What works**: `RelationCollector.CreateFacts` and `.Refine` produce all 10 relation kinds from syntax alone
with zero `SemanticModel` dependency, upgrade confidence in trusted mode via a shared `RelationFactId` merged
by `FactMerger.ResolutionRank`, and are proven end-to-end against the real `fixtures/SyntheticSolution` — the
literal reported defect (`relations: []` for `PaymentsService.cs`/`OrderService.cs`) is closed and reproduced
as closed by this Verifier's own gate run (`relation_count: 0 → 9` in the re-approved `V3DeterminismTests`
snapshot). `MessagingRelationDetector`/`HttpRelationDetector` and both their test files are fully deleted with
no dangling references. Schema, `FactValidator`, `FactMerger`, `RelationProjector`, and `FactResolution` all
accept the extended vocabulary without special-casing.

**Issues found**: One Minor code-quality finding — `RelationNoiseFilter.IsFrameworkType(ITypeSymbol)` is dead
production code; design.md/T13 describe it being wired into `Refine`'s `calls`/`creates` semantic filtering,
but that wiring was never implemented. Not a spec-AC gap (RELC-16 only requires the syntax-only predicate,
which is correctly shared). No fix required to close this feature; worth a follow-up task.

**Next steps**: None required to close this feature. Optionally: wire `IsFrameworkType(ITypeSymbol)` into
`Refine` to match design.md, or strike that sentence from design.md/T13 and delete the unused method, in a
small follow-up. User proceeds with manual Stryker mutation run as planned.
