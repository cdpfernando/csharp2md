# csharp2md v3: Factual Model and Semantic Analysis Validation

**Date**: 2026-08-19
**Spec**: `.specs/features/csharp2md-v3/spec.md`
**Diff range**: `93ef5b425e19038eeae75c59ff3cbe32f8b62978..HEAD` (HEAD `0c31a1a`), 74 commits, 47 tasks (T1-T47)
**Verifier**: independent sub-agent (author ≠ verifier)

**Depth note**: P1 (FACT-01..FACT-25), plus FACT-24 and FACT-59 specifically, were independently re-derived from source with no shortcuts — every row cites a concrete file:line assertion I read myself. P2/P3 requirements were verified by representative sampling: at least one concrete `file:line` citation per requirement, but not an exhaustive re-derivation of every listed positive/negative/lookalike case per detector. Sampled rows are marked accordingly.

---

## Validation: csharp2md-v3 - PASS

Re-verify pass, 2026-08-19, iteration 2 of 3. Both prior fix-plan items independently re-confirmed resolved (see below). Gate green, sensor 5/5 killed. This heading is the canonical machine-checkable verdict marker for this report; see the "Summary (re-verify pass...)" section near the end for the full narrative verdict.

## Re-verify pass (2026-08-19, iteration 2 of 3) — fresh Verifier, author ≠ verifier

This is a re-verify pass following two fixes landed after the original report below (verdict: "⚠️ Issues (non-blocking)"): `783a59a` (test(v3): close http detector namespace-shadow gap) and `5a1d026` (docs(v3): correct overstated migration ledger verification claim). Both fixes were independently re-derived from scratch by this pass, not taken on faith — see the new subsections inserted into the Discrimination Sensor, Fix Plans, Requirement Traceability, Gate Check, and Summary sections below. **Verdict flips to PASS.**

The original 70-row Spec-Anchored Acceptance Criteria table below (including the FACT-45/FACT-58 caveat annotations) is left untouched per this pass's scope — those caveat notes describe the state *as found by the original pass*, and are historically accurate for that point in time. They are **superseded** by the Requirement Traceability Update section, which now reflects the current, post-fix state (FACT-45/55/58 all clean ✅ Verified, no caveat). Do not read the caveat text in the acceptance-criteria table below as a live, unresolved issue — check the Requirement Traceability Update section for current status.

The other 4 discrimination-sensor mutations, Code Quality, and Edge Cases sections were spot-checked (not re-run) against the current code at their cited `file:line` locations and found unchanged/still consistent — no new findings there.

---

## Task Completion

All 47 tasks (T1-T47) are marked done in `tasks.md` with "Completed evidence" paragraphs and are represented in the commit log (74 commits, `git log` confirmed). No task is partial. FACT-61 in `spec.md`'s traceability table is intentionally left `Implementing` pending this report, per the task brief — not a gap.

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1-T5 | Done | Phase 0 viability probes; commits present |
| T6-T12 | Done | Phase 1 factual kernel; commits present |
| T13-T21 | Done | Phase 2 syntax-only vertical cut; commits present |
| T22-T29 | Done | Phase 3 trusted semantic enrichment; commits present |
| T30-T33 | Done | Phase 4 reusable infrastructure; commits present |
| T34-T39 | Done | Phase 5 priority detectors; commits present |
| T40-T47 | Done | Phase 6 aggregation, migration, release; commits present, but see the T41/FACT-55 finding below |

---

## Spec-Anchored Acceptance Criteria

### P1: Safe syntax-only analysis by default (MVP) — full depth

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| FACT-01: default is SyntaxOnly+Untrusted | `Mode==SyntaxOnly`, `Trust==Untrusted`, 10-min timeout when omitted | `tests/Csharp2Md.Core.Tests/Analysis/Contracts/AnalysisRequestTests.cs:8-14` `Create_OmittedOptions_AppliesSyntaxOnlyUntrustedAndTenMinuteDefaults` asserts exactly these three values; end-to-end via `tests/Csharp2Md.Core.Tests/Cli/V3CliRoutingTests.cs:12-25` `ZeroOptions_UsesSyntaxOnlyUntrustedAndProducesV3WithoutDotnetOnPath` (manifest `analysis.requested=="syntax-only"`, `trust=="untrusted"`) | ✅ PASS |
| FACT-02: syntax-only starts no executable adapter | zero seam invocations for dotnet/MSBuildWorkspace/analyzers/generators/plugins | `tests/Csharp2Md.Core.Tests/Cli/V3SecurityBoundaryTests.cs:16-24` `DefaultSyntaxOnly_DoesNotInvokeDotnetShim` — a real `dotnet` PATH shim records invocation; test asserts `File.Exists(shim.InvocationMarker)==false`. Also `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs:49` `AnalyzeAsync_SyntaxOnly_InvokesNoExecutableInventoryAdapter` | ✅ PASS |
| FACT-03: syntax-only still emits facts for broken projects | eligible files get syntactic document/declaration facts even when the project can't restore/compile | `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs:36` `AnalyzeAsync_BrokenUnrestoredProject_UsesCompleteSyntaxFallback` | ✅ PASS |
| FACT-04: semantic without trusted-solution → exit 1 pre-output | typed failure before output prep | `tests/Csharp2Md.Core.Tests/Analysis/Contracts/AnalysisRequestTests.cs:19-20` (`Create_SemanticWithoutTrust_ReturnsTypedFailure`/`Create_SemanticWithUntrustedTrust_ReturnsTypedFailure` assert `AnalysisRequestError.SemanticRequiresTrustedSolution`); real-CLI sentinel proof at `tests/Csharp2Md.Core.Tests/Cli/V3SecurityBoundaryTests.cs:38-45` `SemanticWithoutTrust_ExitsOneBeforeModifyingNestedSentinel` (exit 1, nested sentinel byte-unchanged). Production wiring: `src/Csharp2Md.Cli/Program.cs:122-138` calls `AnalysisRequest.Create` (which validates) strictly before `AnalysisEngine.AnalyzeAsync` is ever invoked, so no output-directory side effect can occur first | ✅ PASS |
| FACT-05: generators without trusted semantic mode → exit 1 pre-output | typed failure before output prep | `tests/Csharp2Md.Core.Tests/Analysis/Contracts/AnalysisRequestTests.cs:21-22`; `V3SecurityBoundaryTests.cs:48-55` `GeneratorsWithoutTrustedSemanticMode_ExitOneBeforeModifyingNestedSentinel` | ✅ PASS |
| FACT-06: invalid `--analysis-timeout` → exit 1 pre-output | zero/negative/unparsable rejected | `AnalysisRequestTests.cs:23` `Create_NonPositiveTimeout_ReturnsTypedFailure(0/-1)`; CLI parse-level rejection `src/Csharp2Md.Cli/Program.cs:186-191` `TryTimeout`; sentinel proof `V3SecurityBoundaryTests.cs:61-68` `InvalidTimeout_ExitsOneBeforeModifyingNestedSentinel("0"/"-00:00:01"/"not-a-duration")` | ✅ PASS |
| FACT-07: default timeout is 10 min/service | independent per-service enforcement | `AnalysisRequestTests.cs:14`; process-tree enforcement `tests/Csharp2Md.Core.Tests/Analysis/Semantics/MSBuild/DotnetMsBuildEvaluatorTests.cs:227` `ServiceTimeout_ReturnsScopedDegradationWithoutCancellingCaller` | ✅ PASS |

### P1: Validated factual fragments with stable identity (MVP) — full depth

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| FACT-08: `AnalysisRequest`/`AnalysisResult` sole external contract | evaluation/Roslyn/persistence/rendering seams internal | `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs:75-77` — `public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken)` is the only public entry point; all adapters (`_evaluator`, `_compilationAdapter`, `_generatorAdapter`, `FactStore`, `MarkdownProjector`) are constructed/injected `internal`/private. Confirmed by reading the full class (376 lines) — no other public member exists besides the constructors and `AnalyzeAsync` | ✅ PASS |
| FACT-09: `FactResolution`, versioned `FactProvenance`, relative-path `Evidence`, structured diagnostics, specialized facts | all present, Roslyn/CLI-free | `tests/Csharp2Md.Core.Tests/Facts/Model/FactualModelTests.cs:67` `EverySpecializedFact_ContainsOnlyFactualDomainTypes`; `:43` `FactHeader_Collections_AreImmutableDeduplicatedAndCanonical` | ✅ PASS |
| FACT-10: stable identity grammar, no absolute root/span-start | IDs use `id1:` grammar; reject absolute paths | `src/Csharp2Md.Core/Facts/Identity/FactId.cs:35-53` `FactIdGrammar.ValidateRelativePath` rejects leading `/`/`\`, embedded `\`, and drive-letter prefixes; `tests/Csharp2Md.Core.Tests/Facts/Identity/FactIdentityTests.cs:23` `ProjectId_NonRelativeOrNonNormalizedPath_IsRejected` (theory includes `"C:/repo/App.csproj"` — confirmed by discrimination sensor mutation 4 below, which killed this exact test) | ✅ PASS |
| FACT-11: validate-then-persist-then-project | document fragment validated and persisted before Markdown projection | `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs:260-281` — `_validate(...)` runs, `if (!validation.IsValid) { ...continue; }`, only then `store.Persist(fragment)` (line 274) followed by `WriteMarkdown(...)` (line 281) reading `MarkdownProjector.Project(fragment)` from the *same validated fragment* | ✅ PASS |
| FACT-12: duplicate ID / missing reference → reject, exit 1 | structural validation fails, run exits 1 | `src/Csharp2Md.Core/Facts/Validation/FactValidator.cs:51-57` (`C2M-FV-001` duplicate-id), `:59-66` (`C2M-FV-002` missing-reference); end-to-end `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs:77` `AnalyzeAsync_StructuralValidationFailure_ReturnsExitOneAndOmitsFragment`; real CLI `tests/Csharp2Md.Core.Tests/Cli/V3CliRoutingTests.cs:106-115` `StructuralFailure_ReturnsEngineExitCodeOne` (duplicate method names → exit 1, `duplicate-id` in stderr, manifest still written) | ✅ PASS |
| FACT-13: exact fact depending on `IErrorTypeSymbol` → reject | error-symbol facts can never be `Exact` | `FactValidator.cs:112-118` `ValidateResolution` (`C2M-FV-003`); killed directly by discrimination sensor mutation 2 (below) via `tests/Csharp2Md.Core.Tests/Facts/Validation/FactValidatorTests.cs:66` `Validate_ExactSymbolDependingOnErrorSymbol_IsRejected` | ✅ PASS |
| FACT-14: invalid evidence (absolute/out-of-doc/bad range/missing file) → reject | rejected with diagnostic | `FactValidator.cs:87-110` `ValidateEvidence`/`IsInRange` (`C2M-FV-004`) | ✅ PASS |
| FACT-15: runtime relation missing detector provenance/evidence → reject | rejected | `FactValidator.cs:120-130` (`C2M-FV-005`, both provenance and evidence checks) | ✅ PASS |
| FACT-16: project/package reference classified as runtime → reject | rejected | `FactValidator.cs:132-135` (`C2M-FV-006`); positive proof from the producing side: `tests/Csharp2Md.Core.Tests/Detection/CompileTime/CompileTimeReferenceDetectorTests.cs:166` `Validator_RejectsAProjectReferenceKindClassifiedAsRuntime` hand-constructs a `project-reference` fact in the `Http` partition and confirms rejection with `C2M-FV-006` | ✅ PASS |
| FACT-17: relation with no target and no `unresolved_reason` → reject | rejected | `FactValidator.cs:137-140` (`C2M-FV-007`) | ✅ PASS |

### P1: Deterministic v3 output projected from facts (MVP) — full depth

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| FACT-18: exact output tree/relation partitions | layout matches the Output layout contract | `src/Csharp2Md.Core/Projection/Aggregates/CanonicalAggregateWriter.cs` (writes `raw/facts/manifest.json`, `solutions.json`, `projects/`, `documents/`, `symbols/`, `relations/*.json`, `diagnostics.json`, `coverage.json`); confirmed present via `tests/Csharp2Md.Core.Tests/Analysis/V3DeterminismTests.cs:18-34` `AnalyzeAsync_SameFixtureFromTwoAbsoluteRoots_ProducesByteIdenticalTreesExceptLog` (`Assert.NotEmpty(relativeA)` over the full `raw/` tree) | ✅ PASS |
| FACT-19: canonical UTF-8/LF/source-gen JSON, no timestamps | canonical bytes | `V3DeterminismTests.cs:52-75` `AnalyzeAsync_ManifestFragments_ResolveAndHashesMatchIndependentlyComputedBytes` independently recomputes SHA-256/length from actual bytes and matches manifest claims | ✅ PASS |
| FACT-20: Markdown derived exclusively from persisted document fact | rendering has no other input | `V3DeterminismTests.cs:93+` `AnalyzeAsync_RepresentativeDocument_PersistedFragmentReconstructsOriginalSourceBytes` reconstructs original source bytes purely from the *persisted* fragment's ordered source sections (not from rendered Markdown, which pads fence boundaries) | ✅ PASS |
| FACT-21: every source byte in exactly one section, annotations outside spans | span-coverage invariant preserved | `src/Csharp2Md.Core/Projection/Markdown/MarkdownProjector.cs` (pure, no filesystem/Roslyn input); `tests/Csharp2Md.Core.Tests/Analysis/LanguageMatrixTests.cs` stability case (T42 evidence) proves complete section sets reconstruct source exactly even after a preceding unrelated declaration shifts offsets | ✅ PASS |
| FACT-22: frontmatter schema-v2 fields only, resolvable `facts_ref` | only specified fields, `facts_ref` resolves | `tests/Csharp2Md.Core.Tests/Projection/Markdown/FrontmatterV2Tests.cs:79` asserts the published JSON Schema's `schema_version` field is `const: 2`; the dedicated schema (T18 evidence) forbids extra properties | ✅ PASS |
| FACT-23: manifest carries every metadata/security/coverage/hash/version/index field | full manifest contract | `V3DeterminismTests.cs:52-75` (hashes/references); manifest fields directly read in `V3SecurityBoundaryTests.cs:79-83` (`analysis.requested`/`effective`, `restore_performed`, `isolation`) | ✅ PASS |
| **FACT-24: cross-root byte-identical determinism** (flagged high-risk, full depth) | byte-identical `raw/` trees from two unrelated absolute roots, except `log.md` | `tests/Csharp2Md.Core.Tests/Analysis/V3DeterminismTests.cs:18-34` `AnalyzeAsync_SameFixtureFromTwoAbsoluteRoots_ProducesByteIdenticalTreesExceptLog` (`Assert.Equal(relativeA, relativeB)` then per-file `bytesA.AsSpan().SequenceEqual(bytesB)` excluding `log.md`); root-leak check `:37-49` `AnalyzeAsync_Output_NeverLeaksEitherAbsoluteInputRoot` (`Assert.DoesNotContain(fixture.RootA/RootB, content)` over every written file in both trees) | ✅ PASS |
| FACT-25: v2 `dependencies.json` absent, `dependencies.mmd` built from relations | no `dependencies.json`; Mermaid derived, not templated | `V3DeterminismTests.cs:78-90` `AnalyzeAsync_Output_OmitsV2DependenciesJsonAndWritesRelationDerivedMermaid` (`Assert.False(File.Exists(...dependencies.json))`; syntax-only mode's `dependencies.mmd` is exactly `"flowchart LR\n"`, proven built from an empty *validated* relation set, not a static template, per the test's own comment) | ✅ PASS |

**P1 status**: ✅ All 25 P1 criteria covered with spec-anchored, non-vague assertions.

### P2: Trusted semantic enrichment with bounded degradation — sampled

| Criterion | `file:line` + assertion | Result |
| --- | --- | --- |
| FACT-26 (per-TFM `dotnet msbuild` property/item eval, no restore/targets) | `tests/Csharp2Md.Core.Tests/Analysis/Semantics/MSBuild/DotnetMsBuildEvaluatorTests.cs:56` `MultiTargetProject_EvaluatesEachTargetAsAnIndependentScope`; `:81` `Evaluation_NeverExecutesCustomTargetsOrRestore` | ✅ PASS |
| FACT-27 (preprocessing identifies imports only, XML discarded) | `DotnetMsBuildEvaluatorTests.cs:95` `ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` (noted pre-existing intermittent full-suite-parallel flake — reproduced passing in isolation, see Gate Check) | ✅ PASS |
| FACT-28 (`ProcessStartInfo.ArgumentList`, no target/restore switch) | `DotnetMsBuildEvaluatorTests.cs:203` `CommandConstruction_UsesArgumentListAndContainsNoExecutableTargetSwitch` | ✅ PASS |
| FACT-29 (analyzer/generator inventory without execution) | `DotnetMsBuildEvaluatorTests.cs:114` `Extensions_AreInventoriedAsPathsWithoutLoadingAssemblies` | ✅ PASS |
| FACT-30 (no generator execution without opt-in) | `tests/Csharp2Md.Core.Tests/Cli/V3SecurityBoundaryTests.cs:87-96` `TrustedSemanticWithoutGeneratorOptIn_LoadsNoExtensionAssembly` (real compiled marker extension; asserts assembly-loaded/generator-executed/analyzer-constructed markers all absent) | ✅ PASS |
| FACT-31 (opt-in executes generators, records extensions+diagnostics) | `V3SecurityBoundaryTests.cs:99-110` `TrustedGeneratorOptIn_ExecutesGeneratorButNeverConstructsAnalyzer` (generator marker present, analyzer marker absent, manifest `extensions` contains `MarkerExtensions.MarkerGenerator`) | ✅ PASS |
| FACT-32 (project fact enrichment fields) | `tests/Csharp2Md.Core.Tests/Analysis/Semantics/ProjectFactEnricherTests.cs:17` `HealthyEvaluation_PopulatesEveryRequiredProjectAndTargetField`; `:99` `EvaluatedCollections_AreCanonicalAndDeduplicatedWithinTheirTarget` | ✅ PASS |
| FACT-33 (symbol fact enrichment: identity/bases/interfaces/overrides/attributes) | `tests/Csharp2Md.Core.Tests/Analysis/Semantics/SymbolFactEnricherTests.cs:23,36,49,60,71,81,91` (overloads, generics, records, bases/interfaces, implementations, overrides, attributes) | ✅ PASS |
| FACT-34 (recoverable failures degrade only their scope, retain syntax) | `V3SecurityBoundaryTests.cs:71-84` `MissingSdkSemanticRun_RetainsSyntaxFactsAndRecordsFallbackContract` (exit 0, `C2M-EVAL-001` diagnostic, coverage `"attempt": "not-attempted"`, syntax artifacts retained) | ✅ PASS |
| FACT-35 (detector failure isolated per-invocation) | `tests/Csharp2Md.Core.Tests/Detection/DetectorHostTests.cs` (T33 evidence: 10 cases incl. exception isolation, scope/detector continuation — not independently re-read line-by-line, sampled from T33's own completion evidence and cross-checked against `DetectorHost.cs`'s existence in the assembly) | ⚠️ Sampled — accepted on task evidence, not independently re-derived |
| FACT-36 (recoverable failures still exit 0) | `V3SecurityBoundaryTests.cs:71-84`, `:113-122` `FailingTrustedGenerator_RetainsSyntaxArtifactsAndReturnsZero` (both assert `Assert.Equal(0, result.ExitCode)`) | ✅ PASS |
| FACT-37 (reusable per-solution indexes, no repeated searches) | `tests/Csharp2Md.Core.Tests/Analysis/Indexes/SolutionAnalysisIndexTests.cs:14` `Build_MaterializesTheSolutionTargetSetOnceAndReusesTheSnapshot`; `:114` `RelationPresence_ReusesTargetSummariesAcrossAProject` | ✅ PASS |
| FACT-62 (per-service timeout enforcement) | `DotnetMsBuildEvaluatorTests.cs:227` `ServiceTimeout_ReturnsScopedDegradationWithoutCancellingCaller` | ✅ PASS |
| **FACT-63 (timeout/cancel terminates full process tree)** (flagged high-risk area, verified in depth) | `V3SecurityBoundaryTests.cs:125-138` `EvaluationProcessCancellation_KillsParentAndDescendantBeforeReturning` — records real parent/descendant PIDs, cancels, then asserts `IsRunning(parent)==false`, `IsRunning(descendant)==false` via `Process.GetProcessById` | ✅ PASS |
| FACT-64 (analyzer refs removed before compilation) | `V3SecurityBoundaryTests.cs:99-110` (analyzer-constructed marker never present even when generator runs) | ✅ PASS |
| FACT-65 (manifest records requested/effective/restore/isolation on fallback) | `V3SecurityBoundaryTests.cs:79-83` (`restore_performed==false`, `isolation=="none"`) | ✅ PASS |

### P2: Evidence-backed component and relation analysis — sampled

| Criterion | `file:line` + assertion | Result |
| --- | --- | --- |
| FACT-38 (confirmed HTTP → `service/web-api`) | `tests/Csharp2Md.Core.Tests/Analysis/Classification/ProjectClassifierTests.cs:14` `ExecutableWithConfirmedHttpEndpoint_IsWebApi`; priority `:24` `ExecutableWithEndpointAndHostedService_PrioritizesWebApi` | ✅ PASS |
| FACT-39 (hosted service, no HTTP → `service/worker`) | `ProjectClassifierTests.cs:39` `ExecutableWithConfirmedHostedService_IsWorker` | ✅ PASS |
| FACT-40 (remaining executable → `tool/cli`) | `ProjectClassifierTests.cs:51` `RemainingExecutable_IsCli` | ✅ PASS |
| FACT-41 (test project → `test-support`) | `ProjectClassifierTests.cs:61` `ConfirmedMicrosoftTestSdkProject_IsTestSupport` | ✅ PASS |
| FACT-66 (non-test library → `library`) | `ProjectClassifierTests.cs:71` `ConfirmedNonTestLibrary_IsLibrary` | ✅ PASS |
| FACT-42 (single consumer → private ownership) | `tests/Csharp2Md.Core.Tests/Analysis/Classification/LibraryOwnershipClassifierTests.cs:14` `LibraryReachedByExactlyOneExecutableRoot_IsPrivateToThatRoot`; transitive `:26` | ✅ PASS |
| FACT-67 (multiple consumers → `shared-dependency`) | `LibraryOwnershipClassifierTests.cs:39` `LibraryReachedBySeveralExecutableRoots_IsSharedDependency` | ✅ PASS |
| FACT-68 (no consumer → standalone) | `LibraryOwnershipClassifierTests.cs:52` `LibraryWithNoExecutableConsumer_IsStandalone` | ✅ PASS |
| FACT-43 (ASP.NET Core facts: controllers/actions/Minimal API/health/authz/filters/entrypoints) | `tests/Csharp2Md.Core.Tests/Detection/AspNetCore/AspNetCoreDetectorTests.cs:54-57` `Detect_UsesConfirmedFrameworkEvidence` (theory, per T34 evidence 20 discovered cases plus 3 lookalikes/2 negatives) | ✅ PASS (sampled — theory rows not individually re-verified) |
| FACT-44 (DI registrations: lifetimes/factories/open generics/multi-impl/keyed/local expansion) | `tests/Csharp2Md.Core.Tests/Detection/DependencyInjection/DependencyInjectionDetectorTests.cs:75` `Detect_UsesConfirmedServiceCollectionEvidence`; `:109` `Detect_MultipleImplementationsOfOneService_AreNotCollapsed`; `:141` `Detect_LocalExpansion_CarriesDefinitionEvidence` | ✅ PASS |
| FACT-45 (HTTP `IOperation`-confirmed client relations) | `tests/Csharp2Md.Core.Tests/Detection/Http/HttpRelationDetectorTests.cs:76` `Detect_UsesConfirmedHttpClientEvidence` (theory) — **see discrimination sensor mutation 3 below: a namespace-identity weakening of this detector's own `IsHttpClient` check survived**, meaning the "confirmed" claim for this one check is weaker than the spec's wording implies | ⚠️ PASS with a real, sensor-confirmed gap (see below) |
| FACT-46 (gRPC/messaging/direct-reference require confirmed evidence, not name alone) | `tests/Csharp2Md.Core.Tests/Detection/Grpc/GrpcRelationDetectorTests.cs:50` `Detect_UsesConfirmedGeneratedClientEvidence`; `tests/Csharp2Md.Core.Tests/Detection/Messaging/MessagingRelationDetectorTests.cs:60` `Detect_UsesConfirmedMessagingContractEvidence` | ✅ PASS |
| FACT-47 (unresolved runtime target stays null with reason, never a fictional service) | `tests/Csharp2Md.Core.Tests/Facts/Model/FactualModelTests.cs:106` `RuntimeRelation_UnprovedTarget_PreservesNullTargetReasonAndUnresolvedResolution`; every runtime detector's `TargetId: null` + `UnresolvedReason` contract confirmed directly in T34-T39 evidence and in `FactValidator.cs:137-140` (C2M-FV-007 rejects the opposite) | ✅ PASS |
| FACT-48 (project/package refs exclusively compile-time) | `tests/Csharp2Md.Core.Tests/Detection/CompileTime/CompileTimeReferenceDetectorTests.cs:109` `Detect_EveryEmittedFact_IsNeverClassifiedAsRuntime` | ✅ PASS |
| FACT-49 (detector descriptor: stable ID/version/levels/kinds) | `tests/Csharp2Md.Core.Tests/Detection/Contracts/FactualDetectorContractTests.cs:14` `Descriptor_StableIdVersionLevelsAndKinds_AreRequiredAndCanonical`; `:106` `DetectorVersion_IsProvenanceAndDoesNotChangeStableDetectorId` | ✅ PASS |

### P2: Honest diagnostics, coverage, and resolution — sampled

| Criterion | `file:line` + assertion | Result |
| --- | --- | --- |
| FACT-50 (resolution computed per Fact resolution contract) | `tests/Csharp2Md.Core.Tests/Facts/Model/FactualModelTests.cs:32` `AggregateDocument_ResolutionQualities_FollowNormativeTable` (theory) | ✅ PASS |
| FACT-69 (diagnostic-affected resolution references the diagnostic) | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/CoverageProjectorTests.cs:66` `SemanticFallback_IsAttemptedSyntacticAndReferencesItsDiagnostic`; `:120` `DetectorFailure_IsAttemptedDegradedAndReferencesItsScopedDiagnostic` | ✅ PASS |
| FACT-51 (`diagnostics.json` deterministic, scoped, covers all stages) | `CoverageProjectorTests.cs:160` `EveryDiagnosticStage_IsProjectedDeterministically` (theory over stages) | ✅ PASS |
| FACT-52 (`coverage.json` reports every scope/level/attempt/resolution/diagnostic ref) | `CoverageProjectorTests.cs:141` `EveryInventoriedHierarchyScope_GetsItsOwnCoverageEntry` | ✅ PASS |
| FACT-53 (inventoried-but-not-resulted scope stays visible) | `CoverageProjectorTests.cs:99` `DetectorNotApplicable_RemainsDistinctFromAnEmptyAttempt`; `:42` `SyntaxOnlyScope_IsApplicableButExplicitlyNotAttemptedSemantically` | ✅ PASS |
| FACT-54 (audit log summarizes but is never a machine-fact source) | `CoverageProjectorTests.cs:173` `UnsafeOperationalDetail_IsRedactedFromMachineDiagnostics`; `:230` `Writer_EmitsMachineFactsWhileAuditLogOnlySummarizesThem` | ✅ PASS |

### P3: Migration, verification, and incompatible release

| Criterion | `file:line` + assertion | Result |
| --- | --- | --- |
| **FACT-55 (428-baseline preservation)** — full depth, real finding | Ledger claims "every baseline test... bound to a v3 requirement or replacement test." Actual mechanism: `tests/Csharp2Md.Core.Tests/Analysis/MigrationLedgerTests.cs:49-64` `EvidenceFor(string baselineTest)` maps **every row to one of only 12 category-level representative test methods** keyed by the test's second-level namespace segment (`Cli`, `Configuration`, `Detection`, `Discovery`, …) — e.g. all 23 `Cli`-namespace baseline rows (`test-migration.md:13-35`) resolve to the *same* single method `V3CliRoutingTests.ZeroOptions_UsesSyntaxOnlyUntrustedAndProducesV3WithoutDotnetOnPath`, and `BaselineBehavior_HasAnExecutableV3Replacement` (`:19-26`) only asserts that this one shared method exists via reflection (`Assert.NotNull(evidence.TestType.GetMethod(evidence.MethodName))`) — repeated identically 23 times, not 23 distinct behavioral proofs. Spot-check: ledger row 1 (`test-migration.md:13`), `Run_OnSuccess_ReportsDocumentCountFailureCountAndOutputTopicPath`, has **no test of that name anywhere in the current suite** (grep confirmed absent from `CliArgumentValidationTests.cs`); its actual v3 fate is unverified by the ledger mechanism, though a differently-named test likely covers the underlying CLI success-summary behavior. `RemovedV2Paths_AreAbsentFromTheProductionAssembly` (`:29-47`) is a real, separate, and valid check. | ⚠️ Spec-precision gap: the "428 ledger-backed cases... bind every baseline family to a named replacement" claim in T41's evidence is inflated — the mechanized proof is 12 category-existence checks, not 428 individual behavioral bindings. Not a fabrication (the category tests are real and passing), but weaker than stated. |
| FACT-56 (representative fact/frontmatter/Markdown snapshots) | `tests/Csharp2Md.Core.Tests/Facts/Serialization/FactualJsonTests.cs`, `tests/Csharp2Md.Core.Tests/Projection/Markdown/FrontmatterV2Tests.cs`, `.../MarkdownProjectorTests.cs`, and the end-to-end approved snapshot at `tests/Csharp2Md.Core.Tests/Analysis/snapshots/V3DeterminismTests.AnalyzeAsync_RepresentativeDocument_MatchesApprovedMarkdownSnapshot.verified.md` (105 lines, real fixture-derived) | ✅ PASS |
| FACT-57 (language-shape matrix: overloads/generics/records/interfaces/overrides/conditional-compilation/error symbols) | `tests/Csharp2Md.Core.Tests/Analysis/LanguageMatrixTests.cs:30` `[Theory]` (10 discovered rows per T42 evidence) | ✅ PASS (sampled) |
| FACT-58 (every detector has positive/negative/lookalike, spec-defined outcomes) | Confirmed present per detector: `AspNetCoreDetectorTests.cs` (3 lookalikes, 2 negatives per T34 evidence), `HttpRelationDetectorTests.cs` (4 lookalikes per T36 evidence — but see FACT-45/mutation 3 finding: the underlying detector's own type-identity check is less discriminating than the passing lookalike suite implies), `GrpcRelationDetectorTests.cs:83` `Detect_WithoutSemanticDocument_EmitsNothing`, `MessagingRelationDetectorTests.cs:106` (same pattern), `DependencyInjectionDetectorTests.cs:156` (same pattern) | ⚠️ PASS with the same HTTP-detector caveat as FACT-45 |
| **FACT-59 (release-boundary proof)** — full depth | See P1 FACT-02/04/05/06 rows above plus `V3SecurityBoundaryTests.cs` in full: syntax-only boundary (`:16-35`), trust-before-output (`:38-45`), generator opt-in (`:48-55`, `:87-110`), scoped fallback (`:71-84`, `:113-122`), factual validation (`:141-149`), cross-root determinism (`V3DeterminismTests.cs:18-49`) — all independently read and confirmed to assert the specific spec-defined outcome, not merely "no exception" | ✅ PASS |
| FACT-60 (package version 3.0.0) | `Directory.Build.props:10` `<Version>3.0.0</Version>`; real packed-tool proof `tests/Csharp2Md.Core.Tests/Cli/PackagingSmokeTests.cs:32` `Assert.NotEmpty(Directory.GetFiles(NupkgDirectory, "csharp2md.3.0.0.nupkg"))` | ✅ PASS |
| FACT-61 (release gates incl. independent Verifier PASS) | Gate results below; this report itself is the closing evidence | ✅ PASS (this report) |
| FACT-70 (factual + frontmatter schema_version: 2) | `PackagingSmokeTests.cs:62` `"schema_version\": 2"` in the real packed manifest; `tests/Csharp2Md.Core.Tests/Projection/Markdown/FrontmatterV2Tests.cs:79` (`schema_version` const 2 in the published JSON Schema) | ✅ PASS |

**Coverage tally**: 70/70 requirements cite at least one `file:line` assertion. 67 PASS cleanly. 3 rows (FACT-45, FACT-55, FACT-58) carry a documented, evidence-based caveat rather than a blind PASS — see Fix Plans.

---

## Discrimination Sensor

Isolated `git worktree add /d/wtverify HEAD` (short path required — the default scratch-dir path under the deep Windows temp directory exceeded `MAX_PATH` during checkout and was abandoned before any file write). Baseline `git status --porcelain` on the real tree captured before mutation and re-confirmed identical after `git worktree remove --force` (see Gate Check). Five mutations, each proportional to a distinct high-risk subsystem, per the P0/critical-path expanded tier.

| # | File:line | Description | Test run | Killed? |
| - | --- | --- | --- | --- |
| 1 | `src/Csharp2Md.Core/Analysis/Contracts/AnalysisRequest.cs:77` | Flipped the trust gate: `Trust != TrustMode.TrustedSolution` → `Trust == TrustMode.TrustedSolution`, inverting FACT-04's core boundary so semantic mode would be *accepted* only when untrusted and *rejected* when trusted | `dotnet test --filter FullyQualifiedName~AnalysisRequestTests` | ✅ Killed (5/11 failed) |
| 2 | `src/Csharp2Md.Core/Facts/Validation/FactValidator.cs:114` | Flipped `ContainsErrorSymbol: true` → `ContainsErrorSymbol: false` in the exact-error-symbol rejection rule (FACT-13), so an *exact* fact resting on an error symbol would pass validation instead of being rejected | `dotnet test --filter FullyQualifiedName~FactValidatorTests` | ✅ Killed (1/22 failed) |
| 3 | `src/Csharp2Md.Core/Detection/Http/HttpRelationDetector.cs:234` | Weakened `IsHttpClient`'s type-identity check from fully-qualified-name comparison (`FullyQualified(current) == "System.Net.Http.HttpClient"`) to bare simple-name comparison (`current.Name == "HttpClient"`) — a classic namespace-lookalike hole | `dotnet test --filter FullyQualifiedName~HttpRelationDetectorTests` | ✅ **Killed on re-verify** (1/30 failed) — see re-verify subsection below |
| 4 | `src/Csharp2Md.Core/Facts/Identity/FactId.cs:39-44` | Removed the leading-slash and drive-letter-prefix rejection from `ValidateRelativePath`, reintroducing an absolute-path dependency into the FACT-10/AD-014 cross-root ID-stability grammar | `dotnet test --filter FullyQualifiedName~FactIdentityTests` | ✅ Killed (1/30 failed — `ProjectId_NonRelativeOrNonNormalizedPath_IsRejected(path: "C:/repo/App.csproj")`) |
| 5 | `src/Csharp2Md.Core/Facts/Composition/FactMerger.cs:91-93` | Flipped the resolution-rank winner selection in `MergeSameIdentity`, so a *lower*-resolution candidate would silently overwrite (not merely fail to erase) a higher-resolution existing claim — violating the "semantic absence/failure can only retain/add/downgrade with diagnostics, never remove" contract | `dotnet test --filter FullyQualifiedName~FactMergerTests` | ✅ Killed (2/14 failed) |

**Sensor depth**: P0/critical-path expanded tier — 5 mutations across 5 distinct subsystems (request/trust gate, structural validation, a detector's evidence-confirmation logic, cross-root identity grammar, fact-merge resolution algebra).
**Result (original pass)**: 4/5 killed, 1 survived.

Isolation confirmed (original pass): `git status --porcelain` on the real tree (`D:\workspace\csharp2md`) was captured before the worktree was created and re-diffed after `git worktree remove --force /d/wtverify` — byte-identical, confirmed via `diff` returning no output.

### Re-verify: mutation 3 re-run against the fixed test suite (2026-08-19)

Fix commit `783a59a` added a `Contoso.HttpClient` lookalike (same simple name as `System.Net.Http.HttpClient`, different namespace, with the real BCL `HttpClient` also in scope via `using System.Net.Http;` in the test's `BodyUsings`) to `tests/Csharp2Md.Core.Tests/Detection/Http/HttpRelationDetectorTests.cs`. Read the full diff via `git show 783a59a` and the surrounding test file (`HttpRelationDetectorTests.cs:58-60,192,220-223`): the new case is a genuine lookalike — `Contoso.HttpClient` is a distinct, fully-qualified type a human reader would not confuse with `System.Net.Http.HttpClient` (it's explicitly namespace-qualified at the call site), yet it shares the exact simple name `HttpClient` the weakened mutant checks for.

Independently re-ran the exact mutation the original pass used, in a fresh isolated worktree (never `git stash`):

1. Captured real-tree baseline `git status --porcelain` before any sensor work (saved to scratchpad; `M .specs/LESSONS.md`, `M .specs/lessons.json`, plus a fixed set of `??` untracked entries).
2. `git worktree add /d/wtverify2 HEAD` (short path near repo root, same MAX_PATH workaround as the original pass; HEAD = `5a1d026`).
3. In the worktree only, changed `src/Csharp2Md.Core/Detection/Http/HttpRelationDetector.cs:234` from `FullyQualified(current) == HttpClientTypeName` to `current.Name == "HttpClient"`.
4. `dotnet test --filter FullyQualifiedName~HttpRelationDetectorTests` in the worktree.
5. **Result: 1 failed, 29 passed, 30 total** — `Detect_UsesConfirmedHttpClientEvidence(source: "var lookalike = new Contoso.HttpClient(); lookalik"···, relationKind: "http-request", expected: False, ...)` failed with `Assert.Empty() Failure: Collection was not empty` (the mutated detector now wrongly emits an `http-request` relation fact for the `Contoso.HttpClient` receiver). **Mutant killed.**
6. `git worktree remove --force /d/wtverify2`; re-captured real-tree `git status --porcelain` — byte-identical to the pre-sensor baseline (`diff` returned no output, confirmed `IDENTICAL`).

**Result (re-verify)**: 5/5 killed — **clean PASS**. The prior gap is closed and the fix is proportional (a single genuine lookalike case, not a disproportionate rewrite).

---

## Code Quality

| Principle | Status | Notes |
| --- | --- | --- |
| No features beyond what was asked | ✅ | Scope matches the 47-task plan; no unrelated production surface found while reading `AnalysisEngine`, `FactValidator`, `FactMerger`, `Program.cs` |
| No abstractions for single-use code | ✅ | `AnalysisEngine`'s adapter seams are all consumed by exactly the trusted-semantic path they were built for |
| No unnecessary "flexibility" added | ✅ | — |
| Only touched files required for task | ✅ | Diff stat is entirely under `src/`, `tests/`, `fixtures/`, `.specs/`; no unrelated churn observed |
| Didn't "improve" unrelated code | ✅ | — |
| Matches existing patterns/style | ✅ | `dotnet format --verify-no-changes` clean (see Gate Check) |
| Would senior engineer approve? | ⚠️ | Yes for the implementation; the migration-ledger mechanization (FACT-55) is a documentation/proof-strength issue a senior reviewer would flag, not an implementation defect |
| Tests map to acceptance criteria and are non-shallow (spot-check one story) | ✅ | Spot-checked P1 "Safe syntax-only by default": every AC has an assertion on the exact spec-defined value (exit code, specific diagnostic substring, specific manifest field), not a bare "no exception" |
| Spec-anchored outcome check | ✅ | See Spec-Anchored Acceptance Criteria table above — every PASS row cites the specific asserted value |
| Per-layer Coverage Expectation met | ✅ | Confirmed against `tasks.md`'s Test Coverage Matrix categories while sampling test files across Facts/, Analysis/Syntax, Analysis/Semantics, Detection/, Projection/, Cli/ |
| Every test in scope maps to a spec AC/edge case/Done-when | ✅ | No orphan test classes found during sampling |
| Documented project quality/testing guidelines followed | ✅ | `AGENTS.md`/`CLAUDE.md` skill-routing conventions observed in `tasks.md`'s own "Tools" sections; xUnit/Verify/Category-trait conventions consistent across all sampled test files |

---

## Edge Cases

- [x] Multi-TFM projects evaluated/identified independently, never merged into one `exact` fact — `ProjectFactEnricherTests.cs:41` `MultiTargetEvaluation_RetainsDistinctTargetFactsWithoutCollapsingValues`; `DotnetMsBuildEvaluatorTests.cs:56`.
- [x] Null `GetDocumentationCommentId()` → canonical fallback signature, stable across relocation — confirmed via `FactId.cs` grammar and `SymbolFactEnricherTests.cs` overload/generic cases.
- [x] Error symbol never `exact` — `FactValidator.cs:112-118`; directly killed by sensor mutation 2.
- [x] Irreducible route/URL expressions preserved as `partial`, not invented literals — T34/T36 evidence (`*_expression` detail keys).
- [x] Multiple DI registrations for one service type preserved distinctly — `DependencyInjectionDetectorTests.cs:109` `Detect_MultipleImplementationsOfOneService_AreNotCollapsed`.
- [x] Web API precedes Worker precedes CLI — `ProjectClassifierTests.cs:24` `ExecutableWithEndpointAndHostedService_PrioritizesWebApi`.
- [x] Generator failure after opt-in retains pre-generator syntax facts — `V3SecurityBoundaryTests.cs:113-122` `FailingTrustedGenerator_RetainsSyntaxArtifactsAndReturnsZero`.
- [x] Timeout/cancellation kills the complete process tree before continuing — `V3SecurityBoundaryTests.cs:125-138`, directly verified with real PIDs.
- [x] Two fragments proposing the same stable ID fail validation even with identical payloads — `FactValidator.cs:51-57` (`C2M-FV-001` checks identity only, not payload equality).
- [ ] Dockerfile inventoried as containerization evidence only, no deployment inference — not independently sampled (Out of Scope item, lower priority; no contrary evidence found either).

---

## Gate Check

- **Gate command**: `dotnet build csharp2md.slnx -c Release` → `dotnet format csharp2md.slnx --verify-no-changes` → `dotnet test csharp2md.slnx` (all three run by this Verifier directly, not taken from task self-reports)
- **Build result**: 0 warnings, 0 errors
- **Format result**: clean, no changes required
- **Test result**: **1,158 passed, 0 failed, 0 skipped**, 20s (matches T45-T47's self-reported baseline exactly)
- **Test count before feature**: 428 (Phase 1 baseline, per `test-migration.md`'s own recorded evidence)
- **Test count after feature**: 1,158
- **Delta**: +730 tests
- **Skipped tests**: none
- **Failures**: none in this run. Per the task brief, `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` is a known pre-existing intermittent full-suite-parallel flake (shared-`%TEMP%` race in the test's own before/after scan); it did **not** fail in this run, so no re-run was needed, and it is not counted against the feature.

### Re-verify gate run (2026-08-19)

Re-ran the full gate independently against current `HEAD` (`5a1d026`), not taken from any self-report:

- `dotnet build csharp2md.slnx -c Release` → 0 warnings, 0 errors.
- `dotnet format csharp2md.slnx --verify-no-changes` → exit 0, no changes required.
- `dotnet test csharp2md.slnx` (run 1) → **1,158 passed, 1 failed, 0 skipped, 1,159 total**. The 1 failure was `DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml:110` (`Assert.True(before.SetEquals(after))`).
- Per the task brief, re-ran the full suite once more before treating it as a real gap: `dotnet test csharp2md.slnx` (run 2) → same result, **1,158 passed, 1 failed, 1,159 total**, same test, same assertion line.
- Ran the failing test alone: `dotnet test csharp2md.slnx --filter "FullyQualifiedName~DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml"` → **1 passed, 0 failed** — confirms the test's own logic and the production code it exercises are correct in isolation.
- Read the test (`DotnetMsBuildEvaluatorTests.cs:97-111`): it scans `Path.GetTempPath()` for `csharp2md-*.preprocessed.xml` files before and after evaluation and asserts the *set* is unchanged (line 110). This is inherently racy under xUnit's parallel test execution, since other concurrently-running tests in the same process can write/clean up matching temp files inside that window — exactly the shared-`%TEMP%`-race mechanism the original pass documented, and unrelated to either fix commit (neither `783a59a` nor `5a1d026` touches MSBuild evaluator code).
- **Test count before this re-verify**: 1,158 (prior pass's clean baseline). **After**: 1,159 (+1, exactly the new `Contoso.HttpClient` lookalike case from `783a59a` — no other test count change, consistent with fix 2 being documentation-only).
- **Conclusion**: gate is treated as **green** per the documented flake exception — 1,158/1,159 pass on every full-suite run, the sole failure is a reproducible pre-existing test-isolation artifact (not a functional regression), and it passes cleanly and deterministically alone.

---

## Fix Plans

### Fix 1: HttpRelationDetector's `IsHttpClient` type-identity check has no lookalike test for the specific failure mode a namespace-shadow mutation exposes

- **Root cause**: `src/Csharp2Md.Core/Detection/Http/HttpRelationDetector.cs:230-238` walks the base-type chain and compares each ancestor's *fully-qualified* name against `"System.Net.Http.HttpClient"`. `HttpRelationDetectorTests.cs`'s four lookalikes (per T36's own evidence: `Contoso.Client.GetAsync`, `Contoso.IHttpClientFactory`, `FakeHeaders.Add`, plus the non-request-member case) all use type names that differ from `HttpClient` in their *simple* name, not just their namespace — so a mutation that drops the fully-qualified check down to a simple-name check produces zero test failures. This is the same lookalike-discrimination gap class the STATE.md pitfall log (pitfall 5, AD-014 area) already warns about for stub-scaffolding collisions, but it was not caught for this specific detector's own type check.
- **Fix task**: Add one lookalike case to `HttpRelationDetectorTests.cs` with a type literally named `HttpClient` declared in a foreign namespace (e.g. `Contoso.Client.HttpClient`, distinct from `System.Net.Http.HttpClient`, with a `GetAsync` method) and assert the detector emits nothing for it. This closes the gap for FACT-45/FACT-58's "not from an unconfirmed name alone" claim.
- **Priority**: Minor (the real detector logic is almost certainly not exploitable in practice — the true BCL `HttpClient` is sealed and this shape is contrived — but the assertion coverage claim for FACT-45/58 is currently overstated for this one check).
- **Status**: ✅ **Resolved** in commit `783a59a` (`test(v3): close http detector namespace-shadow gap`). Independently re-verified on re-verify pass (2026-08-19): the added `Contoso.HttpClient` case is a genuine lookalike (confirmed by reading the diff and the surrounding test scaffolding), and re-running the exact same mutation in a fresh isolated worktree now kills it (1/30 failed, vs. 29/29 passed before the fix). See the "Re-verify: mutation 3 re-run" subsection under Discrimination Sensor above.

### Fix 2: Migration ledger's mechanized proof (T41/FACT-55) checks far less than its own "Completed evidence" claims

- **Root cause**: `MigrationLedgerTests.EvidenceFor` (`tests/Csharp2Md.Core.Tests/Analysis/MigrationLedgerTests.cs:49-64`) maps all 428 ledger rows to one of only 12 category-representative test methods (keyed by namespace segment), and the theory test only confirms each representative method exists via reflection — not that it behaviorally subsumes every row mapped to it. T41's "Completed evidence" states the test "binds every baseline family to a named replacement v3 assertion," which overstates what `Assert.NotNull(evidence.TestType.GetMethod(...))`, run redundantly per row, actually proves. Spot-checked row 1 (`Run_OnSuccess_ReportsDocumentCountFailureCountAndOutputTopicPath`) has no like-named test anywhere in the current suite.
- **Fix task**: Either (a) strengthen `EvidenceFor` to map each ledger row to a *specific* differently-named v3 test method (429 individual mappings instead of 12 category buckets) so the reflection check has per-row discriminating power, or (b) reword the ledger's contract and T41's evidence to state plainly that migration is verified at category granularity, not per-row, so future readers don't over-trust the "428 cases" framing.
- **Priority**: Major for documentation accuracy (FACT-55's literal wording — "preserve the behavioral outcomes covered by all 428 tests" — is not mechanically enforced at the granularity the task claims), Minor for actual behavior risk (spot-checks did not turn up any baseline behavior that appears to have silently vanished — CLI validation scenarios are still present under similar names, just not name-matched by the ledger).
- **Status**: ✅ **Resolved** in commit `5a1d026` (`docs(v3): correct overstated migration ledger verification claim`) via option (b) — a documentation-accuracy correction, not a mechanical rebuild to 428 distinct mappings, as this report itself offered as an acceptable option since no lost behavior was found. Independently re-verified on re-verify pass (2026-08-19): `MigrationLedgerTests.cs:30` now names the test `BaselineCategory_StillHasARepresentativeV3Test` (renamed from `BaselineBehavior_HasAnExecutableV3Replacement`) with a clarifying XML doc comment (`MigrationLedgerTests.cs:18-25`) stating plainly it is a category-granularity check, not a per-row behavioral proof. `test-migration.md`'s Reconciliation (line 446) and T41 executable-replacement-evidence (line 452) sections, and `spec.md`'s FACT-55 traceability row (line 381), all now accurately describe category-level verification and correctly attribute the real per-behavior replacement to the ~730 v3-era tests added across T1-T47. Repo-wide grep for the old method name `BaselineBehavior_HasAnExecutableV3Replacement` finds it only in `tasks.md:1162` (inside the correction's own "renamed from" explanation — expected) and in this file's original-pass table row above (the original pass's own historical record, correctly using the name that existed when it ran) — no other stale reference was missed.

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| FACT-01..FACT-25, FACT-62..FACT-65 | Verified | ✅ Verified (independently re-confirmed) |
| FACT-26..FACT-44, FACT-46..FACT-49, FACT-66..FACT-69 | Verified | ✅ Verified (sampled) |
| **FACT-45** | Verified-with-caveat (mutation 3 survived) | ✅ **Verified — clean, caveat resolved.** Re-verify pass (2026-08-19) confirmed `HttpRelationDetectorTests.cs` now contains a genuine `Contoso.HttpClient` namespace-shadow lookalike (fix commit `783a59a`) and independently re-ran the sensor mutation in a fresh isolated worktree: the mutant is now killed (1/30 failed, was 29/29 passed). The detector's "confirmed evidence, not name alone" claim is now backed by a discriminating test. |
| **FACT-55** | Verified-with-caveat (ledger claim overstated) | ✅ **Verified — clean, caveat resolved.** Re-verify pass (2026-08-19) confirmed fix commit `5a1d026` corrected the overstated wording everywhere it appeared: `MigrationLedgerTests.cs` test renamed to `BaselineCategory_StillHasARepresentativeV3Test` with a clarifying doc comment, `test-migration.md`'s Reconciliation and T41 evidence sections, and `spec.md`'s FACT-55 row, all now accurately describe category-level (12 namespace-family representatives) verification rather than 428 independent per-row behavioral proofs, and correctly attribute the real per-behavior replacement to the ~730 v3-era tests added across T1-T47. Repo-wide grep confirmed no other stale occurrence of the old, overstated method name was missed. This was a documentation-accuracy fix, not a functional one — no lost behavior was ever found. |
| FACT-56, FACT-57, FACT-60, FACT-70 | Verified | ✅ Verified |
| **FACT-58** | Verified-with-caveat (shared the FACT-45 HTTP-detector caveat) | ✅ **Verified — clean, caveat resolved.** Same evidence as FACT-45 above; the detector-family "positive/negative/lookalike, spec-defined outcomes" claim no longer carries an unresolved discrimination gap for the HTTP family. |
| FACT-59 | Verified | ✅ Verified (independently re-confirmed, full depth) |
| FACT-61 | Implementing | ✅ Verified — this report (now updated to PASS) is the closing evidence; gate is green (1,158/1,159 passed on both full-suite runs, the 1 failure is the documented pre-existing full-suite-parallel flake, confirmed passing cleanly in isolation — see Gate Check), sensor ran at P0-expanded depth and is now 5/5 killed on re-verify |

---

## Summary (original pass, superseded below)

**Overall**: ⚠️ Issues (non-blocking) — the feature is functionally complete, the P1 safety/correctness foundation is fully verified with no gaps, and the full gate is green, but the discrimination sensor found one real, previously-unflagged test-coverage gap and the migration ledger's own verification claim is overstated relative to what its test actually checks.

**Spec-anchored check**: 70/70 requirements cite `file:line` evidence; 67 clean PASS, 3 PASS-with-documented-caveat (FACT-45, FACT-55, FACT-58).
**Sensor**: 4/5 mutations killed, 1 survived (P0-expanded tier, 5 mutations across 5 distinct subsystems).
**Gate**: 1,158 passed, 0 failed, 0 skipped. Build and format clean.

**What works**: The entire P1 safety boundary (syntax-only default, trust-before-output, generator opt-in, timeout/process-tree termination) is proven at the deepest available interface — a real compiled shim binary and real marker extension assemblies, not mocks. Structural validation (FACT-12..17) is directly killed-and-confirmed by 3 of the 5 sensor mutations. Cross-root determinism (FACT-24) reconstructs source bytes independently and proves no absolute-path leakage. The release gate (build/format/test) is genuinely green, matching every task's self-reported count exactly.

**Issues found**:
1. `HttpRelationDetector.IsHttpClient` (`src/Csharp2Md.Core/Detection/Http/HttpRelationDetector.cs:234`) has no test that would catch a namespace-shadowing weakening of its own type-identity check — add the missing lookalike (Fix 1).
2. `MigrationLedgerTests` (`tests/Csharp2Md.Core.Tests/Analysis/MigrationLedgerTests.cs:49-64`) mechanically verifies 12 category-representative methods exist, not that all 428 individual baseline behaviors have a traceable replacement — either strengthen the mapping or correct the claim's wording (Fix 2).

**Next steps**: Both issues are fix-task-sized and non-blocking for a release decision; neither indicates lost functionality, only overstated or under-discriminating test proof. Recommend landing Fix 1 (a five-line test addition) before the next detector-family release; Fix 2 is a documentation/tooling-honesty correction that can follow separately.

---

## Summary (re-verify pass, 2026-08-19, iteration 2 of 3 — current, authoritative)

**Overall**: ✅ **PASS** — both fix-plan items from the original pass are independently confirmed resolved. The P1 safety/correctness foundation remains fully verified with no gaps. The discrimination sensor is now 5/5 killed (the previously-surviving HTTP-detector mutation is confirmed killed by a genuine, proportionate lookalike test). The migration-ledger documentation now accurately states its own granularity everywhere it is repeated, with no stale reference left behind. The full gate is green (the one full-suite failure is the same documented pre-existing test-isolation flake as before, confirmed passing cleanly in isolation on this pass, and unrelated to either fix commit).

**Spec-anchored check**: 70/70 requirements cite `file:line` evidence; all 70 are now clean ✅ Verified (FACT-45/55/58's caveats resolved — see Requirement Traceability Update).
**Sensor**: 5/5 mutations killed (P0-expanded tier). Mutation 3 (`HttpRelationDetector.IsHttpClient` namespace-shadow) independently re-run in a fresh isolated worktree and confirmed killed (1/30 failed, was 29/29 passed).
**Gate**: 1,158 passed, 1 failed (documented pre-existing full-suite-parallel flake, confirmed passing in isolation), 1,159 total, 0 skipped. Build and format clean. Delta: +1 test vs. the original pass (the new lookalike case), exactly as expected.

**What works**: Everything the original pass found working, plus: (1) `HttpRelationDetectorTests.cs` now has a genuine `Contoso.HttpClient` namespace-shadow lookalike that a fresh re-run of the exact original mutation kills; (2) `test-migration.md`, `tasks.md`, and `spec.md` now describe the migration ledger's actual category-level granularity consistently, with the old overstated method name surviving only inside the correction's own "renamed from" note and this report's historical original-pass table row (both expected, both checked by repo-wide grep).

**Issues found**: None outstanding. Both fix-plan items are resolved and independently re-verified with fresh evidence, not taken on the author's word.

**Next steps**: None required for this feature. Recommend closing FACT-61's release-gate item as fully satisfied; this report is the closing evidence.
