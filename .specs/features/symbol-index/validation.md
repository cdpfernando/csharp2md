# SymbolIndex Validation

## Validation: symbol-index - PASS ✅

**Date**: 2026-08-20
**Spec**: `.specs/features/symbol-index/spec.md`
**Diff range**: `6cfb491..HEAD` (HEAD = `2527d47`)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Task Completion

| Task | Status  | Notes |
| ---- | ------- | ----- |
| T1 - `TypeNameNormalizer` | ✅ Done | `src/Csharp2Md.Core/Analysis/Semantics/TypeNameNormalizer.cs`; all 16 aliases + `global::` + idempotency covered in `TypeNameNormalizerTests.cs`. |
| T2 - `SymbolFact` extension + syntax population | ✅ Done | `Facts/Model/Facts.cs`, `Analysis/Syntax/SyntaxFactExtractor.cs`. |
| T3 - `FactValidator` `ContainingSymbolId` check | ✅ Done | `Facts/Validation/FactValidator.cs:80`. |
| T4 - JSON contract + `SchemaVersion` 2→3 | ✅ Done | Contracts, mapper, schema, and the one affected snapshot (`symbols: []`, only needed the version-number line) all updated; `FactualSchemaSyncTests` renamed and re-asserts `3`. |
| T5 - `SymbolFactEnricher` semantic population | ✅ Done | `Analysis/Semantics/SymbolFactEnricher.cs`. |
| T6 - `SymbolIndex`/`SymbolIndexBuilder` core | ✅ Done | `Analysis/Indexes/SymbolIndex.cs`. |
| T7 - `FindMethods`/`MethodLookup` | ✅ Done | Same file, `FindMethods` + `ArgumentTypeMatchScore`. |
| T8 - `FindCandidates`/ambiguity | ✅ Done | Same file, `FindCandidates` + `PriorityTier`. |
| T9 - Build-time diagnostics | ✅ Done | `SymbolIndexBuilder.Diagnose`. |
| T10 - Metrics + `IndexedSymbolKind` | ✅ Done | `IndexedSymbolKindMap`, `SymbolIndexMetrics`, `SymbolIndexBuilder.Measure`. |
| T11 - `AnalysisEngine` wiring | ⚠️ Done, one disclosed deviation | `Analysis/AnalysisEngine.cs`; second Done-when box intentionally unchecked - see below. |

**T11's disclosed deviation — independently verified, confirmed correctly reasoned:**

The implementer's note claims spec.md's literal P1 Independent Test instruction ("re-run the same
assertions in trusted-solution mode and confirm `PaymentsService`/`AuthorizePayment` now report
`Resolution = Exact`") cannot be satisfied on the real fixture, because `PaymentsService` derives from
gRPC-generated `Payments.PaymentsBase` and `AuthorizePayment`'s parameters are `Grpc.Core` types,
neither of which the fixture compilation can bind. I read the fixture directly to check this claim
rather than accept it:

- `fixtures/SyntheticSolution/Acme.Payments/PaymentsService.cs:6,16-17` - `PaymentsService : Payments.PaymentsBase`, `AuthorizePayment(AuthorizePaymentRequest request, ServerCallContext context)`.
- `fixtures/SyntheticSolution/Acme.Payments/Acme.Payments.csproj` - references `Grpc.AspNetCore` and compiles `Protos/payments.proto` (`GrpcServices="Server"`), so `Payments.PaymentsBase`/`AuthorizePaymentRequest`/`ServerCallContext` only exist as **generated** code, never as hand-written source.
- `fixtures/SyntheticSolution/Acme.Payments/obj/Debug/net10.0/` contains only `AssemblyAttributes.cs`, `AssemblyInfo.cs`, `.AssemblyReference.cache`, `.editorconfig`, `GlobalUsings.g.cs` - **no** generated gRPC stub file (`Payments.cs`/`PaymentsGrpc.cs`) is present, confirming the codegen never ran for this fixture (no restore/protobuf-tooling pass), so those types are genuinely unbindable in this analysis run.

This substantiates the claim: `PaymentsService`/`AuthorizePayment` truly cannot bind to `Exact` on this
fixture as it stands. The implementer's substitute evidence also checks out:

- `SwaggerOperationDefaultsFilter`/`Apply` (`fixtures/SyntheticSolution/Acme.Orders/Api/SwaggerOperationDefaultsFilter.cs:16-19,27-34`) declares its own stand-in `Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter` interface **inline in the same file**, with no external package dependency - so it is guaranteed to bind cleanly, unlike `PaymentsService`.
- `tests/Csharp2Md.Core.Tests/Analysis/SymbolIndexEndToEndTests.cs:52-65` (`P1_TrustedSolutionRun_SurfacesExactResolutionThroughTheLiveIndex`) asserts `Resolution = FactResolution.Exact` and `ContainsErrorSymbol = false` for that type/member pair, through the live trusted-mode index built by a real `AnalysisEngine.AnalyzeAsync` run - proving the `Exact` path genuinely works end-to-end.
- `SymbolIndexEndToEndTests.cs:73-86` (`P1_TrustedSolutionRun_PaymentsServiceFailsToBindAndIsIndexedUnresolvedNeverExact`) pins the actual outcome for the two symbols spec.md named: `Resolution = Unresolved`, `ContainsErrorSymbol = true`, explicitly asserting `NotEqual(FactResolution.Exact, ...)` for both - directly satisfying spec.md P1 criterion 9's "never `Exact`" requirement.

**Verdict on the deviation**: correctly reasoned and honestly disclosed. SYMIDX-23/Goal-3 (the real-run
proof that the pipeline actually invokes the index, and that trusted mode can produce `Exact`) is still
genuinely proven - just through a different type/member pair than spec.md's literal example, because
that example was written before anyone had checked whether the specific fixture file could satisfy it.
Logged as a spec-precision gap below and in `.specs/lessons.json`, not as a failed AC - it does not
block the feature.

---

## Spec-Anchored Acceptance Criteria

### P1: Every discovered symbol is findable by identity, name, and location

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| SYMIDX-01: one `IndexedSymbol` entry per `SymbolFact` | Every distinct `SymbolFactId` becomes exactly one entry | `tests/.../SymbolIndexTests.cs:206-216` `Build_NoFacts_ReturnsAnEmptyQueryableIndexRatherThanThrowing`; `SymbolIndexTests.cs:240-248` `Build_DuplicateIdentities_CollapseToOneEntryWithoutThrowing` (`Assert.Single(index.Symbols)`) | ✅ PASS |
| SYMIDX-02: order-independent construction | Same facts, shuffled order → identical query results | `SymbolIndexTests.cs:218-238` `Build_ShuffledInputOrder_ProducesIdenticalQueryResults` | ✅ PASS |
| SYMIDX-03: `GetById` is O(1), not a scan | Direct `FrozenDictionary` lookup | `SymbolIndexTests.cs:36-44` `GetById_IsBackedByADirectKeyLookupNotALinearScan` (reflects the backing field type); implementation `SymbolIndex.cs:160,201` uses `FrozenDictionary<SymbolFactId,SymbolFact>.GetValueOrDefault` | ✅ PASS |
| SYMIDX-04: `FindByName` returns every match, ordinal-ordered by Id, empty is valid | All matches, `Id`-ordinal order, zero → empty not null/throw | `SymbolIndexTests.cs:46-59` `FindByName_SharedSimpleNameAcrossProjects_ReturnsEveryMatchOrderedByIdOrdinal`; `:61-67` `FindByName_NoMatch_ReturnsEmptyRatherThanNullOrThrow` | ✅ PASS |
| SYMIDX-05: `FindByQualifiedName` matches on normalized form | `System.String`/`global::System.String`/`string` all resolve together | `SymbolIndexTests.cs:69-82` `FindByQualifiedName_KeywordQualifiedAndGlobalQualifiedSpellings_AllResolveTogether` | ✅ PASS |
| SYMIDX-06: original spelling preserved alongside normalized form | `Signature` keeps raw spelling; `ParameterTypes`/`FullyQualifiedName` normalized | `SymbolIndexTests.cs:196-204` `Build_IndexedSymbolRetainsItsOriginalNonNormalizedSignatureSpelling`; `SyntaxFactExtractorTests.cs` (diff) `Extract_RawSignatureKeepsTheOriginalSpellingTheNormalizedFieldsCollapse` | ✅ PASS |
| SYMIDX-07: `FindMembers` returns every member regardless of `Resolution` | All 3 resolution levels returned | `SymbolIndexTests.cs:92-106` `FindMembers_ReturnsEveryMemberRegardlessOfItsOwnResolution` | ✅ PASS |
| SYMIDX-08: predefined-type + `global::` normalization, 16 keywords | Every keyword → `System.*`; `global::` collapsed | `TypeNameNormalizerTests.cs:31-38` (`PredefinedTypeAliases`, count=16) + `:40-53` (`global::` prefix cases) | ✅ PASS |
| SYMIDX-09: syntax-only/unbound declarations index under syntactic id, never `Exact` | `Resolution ∈ {Syntactic, Unresolved}`, never `Exact` | `SyntaxFactExtractorTests.cs` (diff) `Extract_PopulatesIdentityFieldsWithoutASemanticModelAndNeverClaimsExactResolution` (`Assert.All(... FactResolution.Syntactic)`); real-run: `SymbolIndexEndToEndTests.cs:73-86` (Unresolved, `NotEqual(Exact, ...)`) | ✅ PASS |
| SYMIDX-10: `Name`/`Namespace`/`ContainingType` from syntax alone, no trust precondition | Populated without `SemanticModel` | `SyntaxFactExtractorTests.cs` (diff) `Extract_NestedTypeMember_ReportsOuterNamespaceAndTheNestedTypesOwnQualifiedName` | ✅ PASS |
| SYMIDX-11: empty solution → empty queryable index, not a throw | Zero facts → empty, non-throwing | `SymbolIndexTests.cs:206-216` `Build_NoFacts_ReturnsAnEmptyQueryableIndexRatherThanThrowing` | ✅ PASS |
| SYMIDX-12: same simple name, different namespace/containing type never merged | Distinct entries kept | `SymbolIndexTests.cs:137-148` `Build_SameSimpleNameInDifferentNamespaces_KeepsThemDistinctNeverMerged` | ✅ PASS |

### P2: Method lookup and contextual candidate ranking with explicit ambiguity

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| SYMIDX-13: `FindMethods` `ArgumentCount` exact-match filter | Only exact `ParameterTypes.Count` match, incl. zero-boundary | `SymbolIndexTests.cs:250-266` `FindMethods_ArgumentCountSet_...`; `:268-286` `FindMethods_ZeroArgumentCount_...IsNotAnUnsetFilterNoOp` | ✅ PASS |
| SYMIDX-14: `ArgumentTypes` ranks, never discards | Exact-type match ranked first, mismatched candidate still present | `SymbolIndexTests.cs:288-309` `FindMethods_ArgumentTypesSet_RanksTheExactTypeMatchFirstWithoutDroppingTheMismatchedCandidate` (deliberately Id-ordinal-adverse fixture) | ✅ PASS |
| SYMIDX-15: `FindCandidates` priority sequence | id → qualified name → containing type → namespace → import → project → global | `SymbolIndexTests.cs:426-471` `FindCandidates_ContextualHints_OrderCandidatesByThePrioritySequence`; `:474-491` `FindCandidates_ExactIdThenQualifiedName_OutrankTheGlobalSimpleNameTier` | ✅ PASS |
| SYMIDX-16: ambiguity reporting with every tied candidate | `Status = Ambiguous`, all tied candidates listed | `SymbolIndexTests.cs:360-379` `FindCandidates_PaymentServiceDeclaredInTwoNamespaces_ReportsAmbiguousWithExactlyThoseTwoCandidates` (spec's own P2 example) | ✅ PASS |
| SYMIDX-17: no silent arbitrary tie-break | Tie within one tier never resolved implicitly | `SymbolIndexTests.cs:394-412` `FindCandidates_TieWithinOnePriorityTier_ReportsAmbiguousAndReturnsBothNeverOneArbitraryWinner` | ✅ PASS |
| SYMIDX-18: unique match → `Status = Unique` | Never `Ambiguous` for a genuinely unique match | `SymbolIndexTests.cs:381-392` `FindCandidates_ExactlyOneCandidateMatches_ReportsUniqueNeverAmbiguous` | ✅ PASS |

### P3: Build-time diagnostics and index metrics

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| SYMIDX-19: `duplicated-symbol-id` diagnostic, keeps ordinal-first, never throws | One diagnostic, one surviving entry | `SymbolIndexTests.cs:493-510` `Build_TwoFactsSharingOneIdentity_RecordsDuplicatedSymbolIdAndKeepsExactlyOneEntry` (asserts code `C2M-SYMIDX-001`) | ✅ PASS |
| SYMIDX-20: `invalid-containing-symbol` diagnostic, symbol still indexed | Diagnostic + symbol queryable under its own id | `SymbolIndexTests.cs:512-530` `Build_ContainingSymbolIdReferencingAnAbsentId_RecordsInvalidContainingSymbolAndStillIndexesTheSymbol` (code `C2M-SYMIDX-002`) | ✅ PASS |
| SYMIDX-21: `ambiguous-symbol-lookup` at build time, not only on query | One diagnostic per ambiguous name, unprompted | `SymbolIndexTests.cs:532-549` `Build_SimpleNameWithCandidatesInTwoNamespaces_RecordsAmbiguousSymbolLookupWithoutAnyQuery` (code `C2M-SYMIDX-003`, no `Find*` call precedes the assertion) | ✅ PASS |
| SYMIDX-22: metrics (total, by-resolution, by-kind, duplicate count, ambiguous count) | Exact counts | `SymbolIndexTests.cs:643-653` `Metrics_TotalSymbols_...`; `:655-672` `Metrics_ByResolutionAndByKind_EachSumToTotalSymbols`; `:674-695` `Metrics_DuplicateAndAmbiguousCounts_MatchTheRecordedDiagnosticCountsExactly` | ✅ PASS |
| SYMIDX-23: syntax-only mode builds a full index, no trust precondition | Full, queryable index in default mode | `SymbolIndexEndToEndTests.cs:88-102` `P1_BothModes_BuildTheIndexFromTheLiveRunRatherThanLeavingItEmpty` (asserts syntax-only `ByResolution` is `[Syntactic]` only, non-empty, `Metrics.TotalSymbols` matches) | ✅ PASS |

**Status**: ✅ All 23 ACs covered with spec-anchored assertions; one disclosed, independently-verified spec-precision gap on the P1 Independent Test's literal fixture example (see T11 note above and Summary).

---

## Code Quality Check

| Principle | Status |
| --- | --- |
| No features beyond what was asked | ✅ - Scope matches design.md's component list exactly; no `RelationResolver`, no persistence, no parameter-entity, all confirmed absent per Out of Scope table |
| No abstractions for single-use code | ✅ - `ISymbolIndex` exists specifically because design.md calls for a future consumer to depend on a contract, not the concrete class; `MethodLookup`/`SymbolLookup` mirror the user's own sketch |
| No unnecessary "flexibility" added | ✅ |
| Only touched files required for task | ✅ - Non-`SymbolIndex` file changes are exactly the mechanical `SymbolFact` positional-record call-site updates forced by T2's new required fields (`ProjectClassifierTests.cs`, `SolutionAnalysisIndexTests.cs`, `FactualDetectorContractTests.cs`, `DetectorHostTests.cs`, `FactMergerTests.cs`, `FactualModelTests.cs`, `FactStoreTests.cs`, `MarkdownProjectorTests.cs` - all verified by diff, each adds only the 8 new constructor args) |
| Didn't "improve" unrelated code | ✅ |
| Matches existing patterns/style | ✅ - `FrozenDictionary` construction idiom mirrors `SolutionAnalysisIndex`; diagnostic construction mirrors `FactValidator`/`SymbolFactEnricher`'s existing `AnalysisDiagnostic.Create` pattern |
| Would senior engineer approve? | ✅ |
| Tests map to acceptance criteria and are non-shallow (spot-check one story) | ✅ - Spot-checked P2 (`FindMethods`/`FindCandidates`): assertions target exact ordering, exact diagnostic codes, and use deliberately Id-ordinal-adverse fixtures to prove ranking logic (not ordinal tie-break) actually drives the result (`SymbolIndexTests.cs:303-305`) |
| Spec-anchored outcome check: asserted value matches spec-defined outcome | ✅ - see table above; no vague "an assertion exists" cases found |
| Per-layer Coverage Expectation met (domain 1:1 ACs; routes/e2e happy+edge+error) | ✅ - unit layer has 1:1 AC mapping per the Test Coverage Matrix; integration layer (`SymbolIndexEndToEndTests.cs`) covers the real-run happy path, the semantic-Exact path, and the unbindable-error path |
| Every test in scope maps to a spec AC, listed edge case, or Done-when criterion | ✅ - reviewed `SymbolIndexTests.cs` in full (786 lines); no test found without a traceable AC/edge-case anchor |
| Documented project quality/testing guidelines followed | `AGENTS.md`/`CLAUDE.md` route testing-quality gates to `dotnet-test:*` skills post-hoc, not a coverage-threshold config - none apply directly to this pass; strong defaults applied |

---

## Edge Cases (from spec.md)

- [x] `FindByQualifiedName` with no match → empty, not `null`/exception - `SymbolIndexTests.cs:84-90`
- [x] Two projects, identical namespace+simple name (true duplicate) → both returned as distinct candidates - `SymbolIndexTests.cs:150-161` `Build_TwoProjectsDeclaringTheIdenticalQualifiedName_KeepsBothAsDistinctCandidates`
- [x] `partial` type/member across documents → indexed under separate `SymbolFactId`s, no merging - `SymbolIndexTests.cs:163-174` `Build_PartialDeclarationsAcrossDocuments_AreIndexedUnderTheirOwnIdentitiesNotMerged`
- [x] Zero-parameter method + `ArgumentCount = 0` → matches (boundary, not no-op) - `SymbolIndexTests.cs:268-286`
- [x] `ContainsErrorSymbol = true` → still indexed, `Resolution` reflects its own level, never upgraded to `Exact` - `SymbolIndexTests.cs:176-193` `Build_SymbolContainingAnErrorSymbol_IsStillIndexedAndKeepsItsOwnResolution`; real-run confirmation `SymbolIndexEndToEndTests.cs:73-86`

All five listed edge cases handled correctly with direct assertions.

---

## Gate Check

- **Gate command**: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- **Build**: ✅ 0 warnings, 0 errors
- **Format**: ✅ `--verify-no-changes` clean (no output, exit 0)
- **Test result**: 1378 passed, 1 failed, 0 skipped, 1379 total
- **Test count before feature** (measured via a disposable `git worktree add` at `6cfb491`, then removed - real working tree confirmed unaffected via `git status --porcelain` before/after): 1208 passed, 1 failed, 1209 total
- **Test count after feature**: 1378 passed, 1 failed, 1379 total
- **Delta**: +170 new tests, all passing
- **Integration-tagged subset** (`--filter "Category=Integration"`, T11's own gate command): 333 passed, 0 failed - includes all 5 new `SymbolIndexEndToEndTests`
- **Skipped tests**: none
- **Failures**: `Csharp2Md.Core.Tests.Analysis.Semantics.MSBuild.DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` (`DotnetMsBuildEvaluatorTests.cs:110`, `Assert.True(before.SetEquals(after))`) - **pre-existing, not caused by this feature.** Evidence: (1) this test file's last commit (`d5f5357`) predates the feature's baseline `6cfb491`, i.e. this feature never touched it; (2) it fails identically, with the same assertion, when run against the `6cfb491` baseline in an isolated worktree (1208/1209, same single failure); (3) run alone (`--filter "FullyQualifiedName~DotnetMsBuildEvaluatorTests"`) it passes 12/12 deterministically. This is a parallel-execution race on a shared temp-directory glob check (`csharp2md-*.preprocessed.xml` under `Path.GetTempPath()`), unrelated to `SymbolIndex`'s diff surface. Not a gate blocker for this feature; flagged for separate attention (see lesson below).

**Gate verdict**: ✅ PASS (the one failure is confirmed pre-existing and out of this feature's diff scope)

---

## Discrimination Sensor

**Skipped — user standing preference, see tasks.md header** (`.specs/features/symbol-index/tasks.md` lines 11-16: "the automated Verifier's discrimination-sensor (mutation-testing) sub-step is skipped for this feature by explicit user request — the user will run it manually afterward — same standing preference as `relation-collector`"). No mutations were injected. All other Verifier steps (spec-anchored check, edge cases, gate, code quality, report, traceability) ran as normal.

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| SYMIDX-01 | Verified (spec.md claim) | ✅ Verified (independently confirmed) |
| SYMIDX-02 | Verified | ✅ Verified |
| SYMIDX-03 | Verified | ✅ Verified |
| SYMIDX-04 | Verified | ✅ Verified |
| SYMIDX-05 | Verified | ✅ Verified |
| SYMIDX-06 | Verified | ✅ Verified |
| SYMIDX-07 | Verified | ✅ Verified |
| SYMIDX-08 | Verified | ✅ Verified |
| SYMIDX-09 | Verified | ✅ Verified |
| SYMIDX-10 | Verified | ✅ Verified |
| SYMIDX-11 | Verified | ✅ Verified |
| SYMIDX-12 | Verified | ✅ Verified |
| SYMIDX-13 | Verified | ✅ Verified |
| SYMIDX-14 | Verified | ✅ Verified |
| SYMIDX-15 | Verified | ✅ Verified |
| SYMIDX-16 | Verified | ✅ Verified |
| SYMIDX-17 | Verified | ✅ Verified |
| SYMIDX-18 | Verified | ✅ Verified |
| SYMIDX-19 | Verified | ✅ Verified |
| SYMIDX-20 | Verified | ✅ Verified |
| SYMIDX-21 | Verified | ✅ Verified |
| SYMIDX-22 | Verified | ✅ Verified |
| SYMIDX-23 | Verified | ✅ Verified |

All 23 requirement IDs independently re-derived and confirmed against real evidence, not taken on the author's word.

---

## Fix Plans

None required. The one disclosed T11 deviation is a spec-precision gap in the P1 Independent Test's literal example, not a failed AC — the underlying capability (trusted-mode `Exact` resolution proven through a live pipeline run, plus the correct `Unresolved`/never-`Exact` handling of an unbindable symbol) is genuinely and directly proven. No code change is needed; recorded as a lesson instead (see below).

---

## Summary

**Overall**: ✅ Ready

**Spec-anchored check**: 23/23 ACs matched their spec-defined outcome with direct `file:line` evidence; 0 uncovered; 1 disclosed spec-precision gap (T11's literal fixture example, independently verified as correctly reasoned)

**Sensor**: Skipped per user standing preference (tasks.md header)

**Gate**: 1378 passed, 1 failed (pre-existing, unrelated flake — confirmed present at baseline `6cfb491` too), 1379 total; build and format both clean

**What works**: The full P1/P2/P3 query surface (`GetById`, `FindByName`, `FindByQualifiedName`, `FindMembers`, `FindMethods`, `FindCandidates`) is built and proven against a real `AnalysisEngine.AnalyzeAsync` run over `fixtures/SyntheticSolution` in both syntax-only and trusted-solution mode (`SymbolIndexEndToEndTests.cs`), not only hand-built unit fixtures — directly satisfying the feature's third Goal (avoiding the `SolutionAnalysisIndex`/`DetectorHost` "built but never invoked" fate). Ambiguity is never silently resolved (`SymbolLookupStatus.Ambiguous` with every tied candidate, proven with deliberately Id-ordinal-adverse fixtures). `SchemaVersion` 2→3 round-trips cleanly with the one affected approved snapshot re-approved.

**Issues found**: None requiring a fix task. One disclosed, independently-confirmed spec-precision gap: spec.md's P1 Independent Test names `PaymentsService`/`AuthorizePayment` as the trusted-mode `Exact`-resolution example, but that pair cannot bind on the real fixture (gRPC-generated base type/parameters never restored into `obj/`) — proven instead via `SwaggerOperationDefaultsFilter`/`Apply` plus an explicit pin of `PaymentsService`'s real `Unresolved` outcome. Logged as a lesson (below), not a fix task.

**Next steps**: None blocking. Optional follow-up (not part of this feature): investigate `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml`'s temp-directory race under parallel test execution — pre-existing, out of scope here.
