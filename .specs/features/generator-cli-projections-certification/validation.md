# Generator CLI, Projections and Certification Validation

**Date**: 2026-09-10
**Spec**: `.specs/features/generator-cli-projections-certification/spec.md`
**Diff range**: `524d73e..1701a27` (69 commits, T1..T66 plus two hygiene commits)
**Verifier**: independent sub-agent (author ≠ verifier); five batch workers authored the code, none of them wrote this report

---

## Verdict

**Result**: FAIL

The build-level gate is green (2050 passed, 0 failed, 0 skipped, 0 warnings) and 114 of 120
acceptance criteria are independently evidenced, but three criteria are **not met by the shipped
behaviour** and are nevertheless marked `Verified` in the traceability table. The headline one is the
feature's own MVP blocker B4: a real `analyze` of the mandatory certification corpus publishes
`facts/structural.json` at **83,371 bytes against a published ceiling of 32,768** — GCPC-038 says the
system SHALL publish no such artifact. This is not a test-authoring lag; it is an unimplemented code
path (`LayoutPlanner.AddCompoundFamily` never shards), normalised inside three separate test exclusion
lists rather than escalated to `context.md`'s Deferred Ideas or to the traceability table.

Everything else in the feature is in good shape. The gaps are narrow, well-localised and each has a
concrete fix task below.

---

## Task Completion

All 66 tasks carry fully checked `Done when` lists. The only unchecked boxes in `tasks.md` are the two
Definition-of-Done bullets at lines 2151 and 2153, deliberately left for this Verifier.

| Task range | Status | Notes |
| --- | --- | --- |
| T1–T7 (fixtures) | ✅ Done | `fixtures/CertificationCorpus` present; SyntheticSolution immutability guard in place |
| T8–T14 (document + config policy) | ✅ Done | - |
| T15–T24 (facet, entry capability, dispositions, HTTP) | ✅ Done | - |
| T25–T31 (coverage + certification) | ✅ Done | - |
| T32–T41 (wire v2, layout, manifest, provenance) | ⚠️ Partial | Compound fact families are planned but never sharded (Gap 1) |
| T42–T47 (labels, guide, scenario runner) | ✅ Done | Guide prose under sharding is a documented open item |
| T48–T52 (validate, compose, exit codes, CLI options) | ⚠️ Partial | Exit `6` covers generator version only, never a contract version (Gap 3) |
| T53–T56 (engine certification) | ✅ Done | - |
| T57–T62 (postings, security, determinism, batch) | ⚠️ Partial | GCPC-087/092 analysis half unimplemented (Gap 2) |
| T63–T66 (scale, readiness, LocalCorpus, decisions) | ✅ Done | Readiness checklist passes with an exclusion list that omits the largest artifacts |

---

## Spec-Anchored Acceptance Criteria

Grouped by the clusters `spec.md` itself uses. Every one of the 120 IDs carries an explicit verdict.
Evidence-or-zero: an ID with no locatable `file:line` assertion is not covered, whatever the table says.

### P1: Certified execution with verifiable denominators (GCPC-001..010)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-001 status is exactly passed/degraded/failed, never `not_evaluated` | the literal `not_evaluated` never published | `tests/Csharp2Md.Storage.Tests/Wire/CoverageAndCertificationEnvelopeTests.cs:15` `RunCertificationEnvelope_NotEvaluated_CannotBeConstructed`; `:22` cannot be deserialized; `tests/Csharp2Md.Analysis.Tests/Pipeline/RunCertifierTests.cs:109` enum has no such member. Empirically confirmed: a live `analyze` of the corpus publishes `run-certification.json` = `{"status":"degraded", …}` | ✅ PASS |
| GCPC-002 each metric publishes numerator/denominator/exclusions/unknowns/reasons | all five fields present per metric | `tests/Csharp2Md.Analysis.Tests/Pipeline/ValidationAndCoverageStageTests.cs:33,45,74,91,139,164,188,205`; `tests/Csharp2Md.Storage.Tests/Mapping/CoverageAndCertificationPublicationTests.cs:24` publishes computed values to disk. Empirically confirmed in the published `coverage.json` | ✅ PASS |
| GCPC-003 denominator is the named population and independently enumerable | test re-derives the population, never reads it back | `ValidationAndCoverageStageTests.cs:19` entry-point recount, `:55` linked-call recount, `:123` contract recount, `:174` persistence recount | ✅ PASS |
| GCPC-004 per degradation reason, the count of denominator members | each reason carries an affected count | `tests/Csharp2Md.Storage.Tests/Wire/CoverageAndCertificationEnvelopeTests.cs:92-118` round-trips `AffectedCount: 2` through `CanonicalJson`; `:141` rejects a negative count; `src/Csharp2Md.Storage/Mapping/DomainMapper.cs:238` maps it. **No pipeline path populates one**: `ValidationAndCoverageStage` always calls `CoverageMetric.Evaluated(…)` with the default empty reason array, and `LayoutPlanner`'s `record-exceeds-ceiling` reasons (`LayoutPlanner.cs:297`) are computed into `LayoutPlan.DegradationReasons` and read by no production code | ⚠️ Spec-precision gap (vacuously satisfied; see Fix 4) |
| GCPC-005 no recall/precision/derived ratio in the run envelopes | no such field exists | `ValidationAndCoverageStageTests.cs:101,112` reflect over `CoverageMetric`/`CoverageReport` asserting no recall or precision member | ✅ PASS |
| GCPC-006 all evaluated, nothing quarantined, no reason → `passed` | status `passed` | `tests/Csharp2Md.Analysis.Tests/Pipeline/RunCertifierTests.cs:16` | ✅ PASS |
| GCPC-007 a reason/unknown/unsupported capability → `degraded` | status `degraded` | `RunCertifierTests.cs:29`; `src/Csharp2Md.Analysis/Pipeline/RunCertifier.cs:64-75` | ✅ PASS |
| GCPC-008 quarantine / conflict / undisposed occurrence → `failed` | status `failed` | `RunCertifierTests.cs:42` | ✅ PASS |
| GCPC-009 empty population → `not_applicable` with a reason | never a satisfied ratio | `RunCertifierTests.cs:79`; `ValidationAndCoverageStage.cs:125,169,199,240`. Empirically confirmed: `persistence_coverage.state == "not_applicable"` with its reason in the published `coverage.json` | ✅ PASS |
| GCPC-010 every metric `not_applicable` → `degraded`, never `passed` | status `degraded` | `RunCertifierTests.cs:97` | ✅ PASS |

### P1: Complete invocation accounting (GCPC-011..018)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-011 exactly one disposition per occurrence | one of five kinds, never zero, never two | `tests/Csharp2Md.Analysis.Tests/Classification/InvocationDispositionTests.cs:62,77`; `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusInvocationTests.cs:134` asserts it over a real corpus analyze | ✅ PASS |
| GCPC-012 totals sum to recognized occurrences | exact sum | `tests/Csharp2Md.Analysis.Tests/Pipeline/InvocationAccountingTests.cs:24,86` | ✅ PASS |
| GCPC-013 undisposed occurrence → `failed`, occurrence named | named in the reasons | `InvocationAccountingTests.cs:71`; `RunCertifierTests.cs:56` | ✅ PASS |
| GCPC-014 unresolved + frontier overlap counted once | counted exactly once | `InvocationAccountingTests.cs:50`; `ValidationAndCoverageStageTests.cs:74` | ✅ PASS |
| GCPC-015 no occurrence in two totals | disjoint buckets | `InvocationAccountingTests.cs:24` | ✅ PASS |
| GCPC-016 out-of-scope callable → counted exclusion, no per-occurrence diagnostic | declared category, no diagnostic | `CertificationCorpusInvocationTests.cs:95` asserts the two framework calls carry `external-framework-callable` | ✅ PASS |
| GCPC-017 non-demonstrable continuation → terminal effect with declared cause | `OpenFrontier` carrying a cause, reachable from the occurrence | `tests/Csharp2Md.Analysis.Tests/Classification/InvokesPassTests.cs:217` reflection dispatch emits a frontier; `:193` asserts `FrontierCause.FurtherContinuationObserved`; `src/…/InvokesPass.cs:299` `OpenFrontier.Create(occurrence, cause)` binds it to the occurrence | ✅ PASS (no `Requirement` trait; cited by behaviour) |
| GCPC-018 interface/abstract → one candidate per concrete impl, no confirmed to the interface | exactly that shape | `CertificationCorpusInvocationTests.cs:66` `…IsExactlyOneCandidateNeverAConfirmedInvokesToTheInterface` | ✅ PASS |

### P1: Proven entry capability (GCPC-019..025)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-019 `EntryPoint` only with positive entry evidence | promotion predicate requires proven capability | `tests/Csharp2Md.Analysis.Tests/Classification/EntryPointPassTests.cs:134`; `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusEntryPointTests.cs:33` `Assert.Equal(2, widgetsEntryPoints.Length)` | ✅ PASS (traceability already corrected in `0fd25b4`) |
| GCPC-020 not externally reachable → no `EntryPoint` | regardless of declaring type | `tests/Csharp2Md.Analysis.Tests/Semantics/SymbolFactEmitterTests.cs:211,229,238,247` (private/internal/nested cases carry no facet); `CertificationCorpusEntryPointTests.cs:48` `Assert.DoesNotContain(… ChangeUriPlaceholder)` | ✅ PASS |
| GCPC-021 helper of a framework type with no entry evidence → no `EntryPoint` | not published | `EntryPointPassTests.cs:134` `Execute_PrivateHelperOnRecognizedController_DoesNotCreateEntryPoint`; `CertificationCorpusEntryPointTests.cs:48` | ✅ PASS (no trait; same assertions) |
| GCPC-022 conventional action, no route → still `EntryPoint` + missing-route diagnostic | both present | `CertificationCorpusEntryPointTests.cs:55` asserts `Index` is an EntryPoint **and** `missing-route-declaration` names it | ✅ PASS |
| GCPC-023 publish the evidence, cited by artifact key and ordinal | citation resolves in the same publication | `CertificationCorpusEntryPointTests.cs:77-106` resolves every `derived_from` citation to a real observation in the artifact its kind maps to | ✅ PASS |
| GCPC-024 undeterminable capability → candidate/unresolved, never confirmed | unresolved record instead | `EntryPointPassTests.cs:164` `…PublishesUnresolvedNotEntryPoint` | ✅ PASS |
| GCPC-025 regression reproduced in the versioned corpus, no eShop clone needed | corpus test, CI-runnable | `CertificationCorpusEntryPointTests.cs:20` + the whole class runs off `fixtures/CertificationCorpus` with no `LocalCorpus` category | ✅ PASS |

### P1: Supported-document policy (GCPC-026..035)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-026 explicit policy naming accepted/conditional/excluded classes | every class decided by category | `tests/Csharp2Md.Analysis.Tests/Inventory/SupportedDocumentPolicyTests.cs:10,22,37,75,87,99,111,123,135` — one test per category, not one sample | ✅ PASS |
| GCPC-027 matching document → inventoried, `Document` fact, `source/` artifact | all three | `tests/Csharp2Md.Analysis.Tests/Inventory/InventoryStageTests.cs:133`; `CertificationCorpusExcludedAssetTests.cs:50-52` | ✅ PASS |
| GCPC-028 non-matching → no fact, no `source/`, no relation, no individual diagnostic | all four absent | `CertificationCorpusExcludedAssetTests.cs:40,78,113`; `DocumentInventoryTests.cs:90` | ✅ PASS |
| GCPC-029 conditional extension admitted only on a declared consumer | admitted iff declared | `tests/Csharp2Md.Analysis.Tests/Inventory/SupportedDocumentPolicyTests.cs:49` (excluded) and `:60` (accepted); `ClassifierCapabilityRegistryTests.cs:9-58` | ✅ PASS |
| GCPC-030 allowlist admits the listed documents only | no other excluded class admitted | `SupportedDocumentPolicyTests.cs:147`; `AnalysisRequestAllowlistTests.cs:18` allowlisted `.ts` admitted while its sibling `.js` stays excluded; `:41` out-of-root entry rejected | ✅ PASS |
| GCPC-031 "every analyzed document" means "every accepted document" | a definition, not an observable outcome | no direct citation; its observable consequences are GCPC-027/028/032, all proven above | ⚠️ Spec-precision gap (definitional; no testable predicate of its own) |
| GCPC-032 `source/` for every accepted, none for any excluded | exact set equality | `CertificationCorpusExcludedAssetTests.cs:57-73` asserts both directions over all seven excluded and both accepted paths | ✅ PASS |
| GCPC-033 at most one aggregated exclusion diagnostic | one record naming count and extensions | `CertificationCorpusExcludedAssetTests.cs:94-108` `Assert.Single(…)`, `Assert.Contains("7 document(s)")`, every extension named | ✅ PASS |
| GCPC-034 accepted/excluded count and bytes per category | per-category totals | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusDocumentPolicyReportTests.cs:14` `…ReportsKnownCountsAndBytesPerCategory`; `Inventory/DocumentPolicyReportTests.cs` | ✅ PASS |
| GCPC-035 no consumed configuration/deployment/contract/persistence evidence removed | bindings still promoted | `CertificationCorpusExcludedAssetTests.cs:52` (`.cs`, `.csproj` retained); `tests/Csharp2Md.Analysis.Tests/Extraction/ConfigurationDocumentReaderTests.cs:289,307,325` each assert `count == 1` and a real binding key survives the policy | ✅ PASS (no trait; cited by behaviour) |

### P1: Bounded payloads and measured budgets (GCPC-036..045)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-036 declared per-scenario budget and per-artifact ceiling published | both published | `tests/Csharp2Md.Storage.Tests/Mapping/CeilingCalculatorTests.cs:49` `…NamesEveryInputNeededToRederiveIt`; `tests/Csharp2Md.Cli.Tests/AnalyzeBudgetAndAllowlistTests.cs:113` supplied budget reaches provenance. Empirically confirmed: `artifact_ceiling_bytes: 32768`, `token_estimator_id` in published provenance | ✅ PASS |
| GCPC-037 ceiling derived from the budget and the measured ratio, with the calculation | derivation reproducible | `CeilingCalculatorTests.cs:19,29,49` | ✅ PASS |
| GCPC-038 **no artifact exceeds the declared ceiling** | zero artifacts over 32,768 bytes | **Violated.** A real `analyze` of `fixtures/CertificationCorpus` publishes `facts/structural.json` = 83,371 B, `manifest.json` = 47,273 B, `facts/architecture.json` = 33,684 B. `src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs:105-127` plans all five compound fact families through `AddCompoundFamily`, which never splits. The three tests that would catch it each exclude exactly those keys: `tests/Csharp2Md.Storage.Tests/Scale/ScaleInputGenerator.cs:219-247`, `tests/Csharp2Md.Analysis.Tests/Readiness/LlmReadinessChecklist.cs:157-170` | ❌ GAP (Fix 1) |
| GCPC-039 facts, observations, relations, candidates, unresolved, frontiers, catalogs, postings each split within the ceiling | **facts** included in that list | Relations/observations/candidates/unresolved/frontiers/catalogs/postings: ✅ proven by `tests/Csharp2Md.Storage.Tests/Mapping/LayoutPlannerShardingTests.cs:21`, and empirically (`contains` split into six ~29 KB shards on the corpus). **Facts: never split** — `LayoutPlanner.cs:105-127`. `LayoutPlannerShardingTests.cs:14` states "GCPC-039 stays partial" in its own doc comment | ❌ GAP, partial (Fix 1) |
| GCPC-040 split preserves identity, ordering, hash, citation resolvability | all four preserved | `tests/Csharp2Md.Storage.Tests/Mapping/LayoutPlannerTests.cs:21,39,56`; `Mapping/PackagePublisherPlanDrivenTests.cs` | ✅ PASS |
| GCPC-041 cited ordinal in a split artifact yields the claimed record | citation resolves post-split | `tests/Csharp2Md.Storage.Tests/Mapping/PublishedPackageViewShardAwareTests.cs:35` `TryLocateRelation_SplitPayload_EveryCitationResolvesToTheClaimedRecord` | ✅ PASS |
| GCPC-042 same input → same shard assignment | identical assignment across runs | `LayoutPlannerShardingTests.cs:39,56` (bucket from fact id, not display name; two runs identical) | ✅ PASS |
| GCPC-043 no directory keyed by a high-cardinality fact identity | shards are flat files | `ScaleInputGenerator.cs:271-277` asserts no nested path component in any shard filename | ✅ PASS |
| GCPC-044 scale input splits `contains`, `belongs-to`, invocation **and publishes no artifact over the ceiling** | both clauses | Split clause ✅ (`ScaleInputGenerator.cs:262-268`). "No artifact over the ceiling" clause ❌ — the same test's loop skips 13 keys including `facts/structural.json` and `retrieval.md` | ❌ GAP, partial (Fix 1) |
| GCPC-045 byte size and token estimate of the largest artifact of each role | both published | `ScaleInputGenerator.cs:290-300` reads `ByteSize` from the manifest and derives the token estimate through the published estimator. Token estimate is derived, not stored | ✅ PASS (token estimate derivable rather than published as a field) |

### P1: Executable retrieval guide (GCPC-046..055)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-046 locating an identity directs to a catalog, never a canonical payload | no `facts/` or `relations/` key in the locate section | `tests/Csharp2Md.Projection.Tests/Guides/RetrievalGuideProjectorTests.cs:30-44` | ✅ PASS |
| GCPC-047 documents bucket selection and ordinal resolution | both, without whole-payload reads | `RetrievalGuideProjectorTests.cs:58-75` asserts "resolve by ordinal" and "never the whole canonical payload" | ✅ PASS |
| GCPC-048 a path for each of the seven relation kinds, naming the holding artifact | all seven, each with its artifact | `RetrievalGuideProjectorTests.cs:78,92,108`. Under real sharding the guide names no artifact for a split family and instead prints "no such relation is recognized" — documented in `context.md` (T52, confirmed at T63), unfixed | ⚠️ PASS with a documented correctness hole under sharding (Fix 5) |
| GCPC-049 separate paths for candidates, unresolved, frontiers | each with its own artifact | `RetrievalGuideProjectorTests.cs:125,162` | ✅ PASS |
| GCPC-050 never instruct a full read when an index addresses the record | no full-read instruction | no test asserts the negative across the whole guide; proxied by `RetrievalGuideProjectorTests.cs:42-43` (locate section names no payload key) and `:71` ("never the whole canonical payload"). "Open in full" has no precise testable predicate in the spec | ⚠️ Spec-precision gap |
| GCPC-051 stopping rule for all five conditions | five rules | `RetrievalGuideProjectorTests.cs:174` `Project_StoppingRules_DocumentsAllFive` | ✅ PASS |
| GCPC-052 executed scenario publishes reads/hops/bytes/tokens/relevant/noise | all six measured | `tests/Csharp2Md.Storage.Tests/Retrieval/RetrievalScenarioRunnerTests.cs:33,153`; `tests/Csharp2Md.Cli.Tests/ValidateCommandTests.cs:148` folds them into a real `measurements.json` | ✅ PASS |
| GCPC-053 scenario reaches its endpoint within budget | within 100k tokens / 25 reads | `RetrievalScenarioRunnerTests.cs:33`; `:69` a scenario with no instance is "not exercised", never "passed" | ✅ PASS |
| GCPC-054 every documented path executed automatically, run fails on non-resolution | failure names the offender | `RetrievalScenarioRunnerTests.cs:125,103,142` | ✅ PASS |
| GCPC-055 guide naming an absent key aborts, naming it | abort + offender named | `RetrievalGuideProjectorTests.cs:192,206,215,267` | ✅ PASS |

### P1: Provenance and manifest cardinality (GCPC-056..062)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-056 generator version + reproducible build identity | both present | `tests/Csharp2Md.Storage.Tests/Wire/ProvenanceDtoTests.cs:22` `Current_NamesTheRunningGeneratorBuild` | ✅ PASS |
| GCPC-057 all five version axes carried | five axes present | `ProvenanceDtoTests.cs:31` `Current_PublishesAllFiveVersionAxes`. Carried, yes — but the published **values** are `schema 1, taxonomy 2, observation 1, extractor 1, classifier 1`, contradicting the user-confirmed spec row "schema_version, taxonomy_version, extractor_set_version and classifier_set_version each advance to 2" and design.md's version table; `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyVersionsTests.cs:66` locks that in | ✅ PASS on the AC text; ❌ design/spec-assumption deviation (Fix 3) |
| GCPC-058 deterministic parameters: ceiling, policy version, allowlist digest | all three | `ProvenanceDtoTests.cs:44,54,63`. Empirically confirmed in the published provenance | ✅ PASS |
| GCPC-059 no timestamps or durations in manifest/provenance | none | `ProvenanceDtoTests.cs:73,84` reflect over both records asserting no timestamp or duration member | ✅ PASS |
| GCPC-060 byte-identical provenance across two runs of the same build | byte-identical | `ProvenanceDtoTests.cs:12` `Current_CalledTwice_IsByteIdentical`; `tests/Csharp2Md.Analysis.Tests/Determinism/WholePackageDeterminismTests.cs:38` | ✅ PASS |
| GCPC-061 manifest entry carries real count and byte size | equal to the artifact's real content | `tests/Csharp2Md.Storage.Tests/Mapping/ManifestRealCardinalityTests.cs:22,50`; `Validation/PackageValidatorManifestChecksTests.cs:20,39` reject a disagreement naming both values | ✅ PASS |
| GCPC-062 every file reachable from the manifest | exact set equality | `PackageValidatorManifestChecksTests.cs:56,69` (declared-but-absent and present-but-undeclared both rejected). Empirically confirmed: 242 files on disk, 241 manifest entries + `manifest.json`, zero in either difference | ✅ PASS |

### P1: Final CLI surface (GCPC-063..073)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-063 exactly three subcommands | `analyze`, `validate`, `compose` | `tests/Csharp2Md.Cli.Tests/AnalyzeCommandTreeTests.cs:11` `RootCommand_ExposesExactlyTheThreeCertificationVerbs` | ✅ PASS |
| GCPC-064 `validate` opens no solution, loads no project, runs no semantic analysis | dependency-level proof | `tests/Csharp2Md.Cli.Tests/ValidateCommandTests.cs:17` asserts the validate path references neither Roslyn nor MSBuild | ✅ PASS |
| GCPC-065 detects all seven defect classes | each class detected | `tests/Csharp2Md.Storage.Tests/Corruption/CorruptedPackageTests.cs` (one corrupted package per class); `ValidateCommandTests.cs:172` | ✅ PASS |
| GCPC-066 names artifact key, defect class and offending value | all three named | `PackageValidatorManifestChecksTests.cs:20` `…RejectsNamingArtifactAndBothValues`; `ValidateCommandTests.cs:172` | ✅ PASS |
| GCPC-067 reports the published status, recomputes nothing | reads, never recomputes | `ValidateCommandTests.cs:61` `…ReportsPublishedCertificationStatusAndMatchingExitCode` | ✅ PASS |
| GCPC-068 `compose` reproduces batch artifacts with no solution present | byte-for-byte equal | `tests/Csharp2Md.Cli.Tests/ComposeCommandTests.cs:13,109` | ✅ PASS |
| GCPC-069 passed → exit `0` | `0` | `tests/Csharp2Md.Cli.Tests/ExitCodeTests.cs:16` | ✅ PASS |
| GCPC-070 degraded → `3`, failed → `4` | both | `ExitCodeTests.cs` degraded/failed cases; `:185-187` pins the constants | ✅ PASS |
| GCPC-071 structural corruption → `5`; **incompatible provenance or contract version** → `6` | both halves | `5` ✅ `ExitCodeTests.cs:76`. `6` ✅ only for a newer **generator version** (`ExitCodeTests.cs:102`). The "contract version is incompatible" half is unimplemented: `src/Csharp2Md.Storage/Validation/PackageValidator.cs:146-159` compares `GeneratorVersion` only and never the five version axes it carries | ❌ GAP, partial (Fix 3) |
| GCPC-072 one unpublished solution → `2`, committed packages unmodified | both | `ComposeCommandTests.cs:73`; `ExitCodeTests.cs` partial-composition case | ✅ PASS |
| GCPC-073 invalid invocation → `1`, publishes nothing | both | `ExitCodeTests.cs:126`; `AnalyzeBudgetAndAllowlistTests.cs:13,66` | ✅ PASS |

### P1: Engine certification on labeled corpora (GCPC-074..081)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-074 corpora record source, expected state and rationale | all three per item | `fixtures/CertificationCorpus/labels/{entry-points,linked-calls,contracts,persistence}.json`; `tests/Csharp2Md.Analysis.Tests/Certification/LabeledCorpusIntegrityTests.cs` | ✅ PASS |
| GCPC-075 labels authored independently of classifier output | resolver never reads `Expected` | `tests/Csharp2Md.Analysis.Tests/Certification/EngineCertificationRunner.cs:75-82` — `Resolve` takes only the label id and the publication, never the entry's expectation | ✅ PASS |
| GCPC-076 positives, negatives and lookalikes per classifier | all three kinds present | `LabeledCorpusEntry.cs:9-16`; `labels/contracts.json` carries one of each | ✅ PASS |
| GCPC-077 precision and recall per certified area | computed from ground truth | `EngineCertificationRunner.cs:279-288` (`AreaCertificationResult.Precision`/`Recall`); `EngineCertificationRunnerTests.cs` | ✅ PASS |
| GCPC-078 normative thresholds 99/95, 99/90, 99/95, 99/90 | verbatim from `quality-and-security.md` | `tests/Csharp2Md.Analysis.Tests/Certification/EngineThresholdTests.cs:37-44` and `:70-84` (a `Theory` over all four areas) | ✅ PASS |
| GCPC-079 below threshold → fail, naming area, value and failing items | all three named | `EngineThresholdTests.cs:88-114` flips one label and asserts the verdict flips; `:118` asserts `Explain()` names area, measured value, threshold and item | ✅ PASS |
| GCPC-080 engine certification separate from run certification, no precision/recall in a package | no such field | `ValidationAndCoverageStageTests.cs:101,112`; `EngineCertificationRunner.cs:18` (nothing under `src/` references the corpora) | ✅ PASS |
| GCPC-081 versioned repository artifact, not a CLI subcommand | report file, three verbs only | `tests/Csharp2Md.Analysis.Tests/Certification/EngineCertificationReportTests.cs`; `tests/Csharp2Md.Cli.Tests/EngineCertificationSurfaceTests.cs`; `AnalyzeCommandTreeTests.cs:11` | ✅ PASS |

### P1: Security and fidelity under new projections (GCPC-082..086)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-082 redaction model preserved | sidecar, both hashes, ordinal-sorted spans | `tests/Csharp2Md.Projection.Tests/Security/SecretAbsenceTests.cs:73-92` asserts both 64-char hashes differ and the spans are ordinal-sorted | ✅ PASS |
| GCPC-083 no secret literal or individual hash in any published byte | absent everywhere | `SecretAbsenceTests.cs:48` (literal) and `:60` (SHA-256 of the individual secret), both `Assert.All` over **every** published file | ✅ PASS |
| GCPC-084 a label proven outside a redacted span before publication | no secret-derived label | `SecretAbsenceTests.cs:97-107`; `src/Csharp2Md.Projection/Labels/LabelProjector.cs` redaction check | ✅ PASS |
| GCPC-085 separate consent for generators, analyzers never run | both | `tests/Csharp2Md.Analysis.Tests/Semantics/TrustBoundaryTests.cs` | ✅ PASS |
| GCPC-086 no credential in a coverage/provenance/accounting envelope | absent | `SecretAbsenceTests.cs:112-124` over all five envelopes | ✅ PASS |

### P2: Contract and message-operation accounting (GCPC-087..092)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-087 every recognized message operation / payload slot reaches **exactly one** of contract binding, candidate, unresolved record, declared exclusion | one of the four record kinds per item | **Violated for a normally-named published message with zero handlers.** `src/Csharp2Md.Analysis/Classification/Passes/RelationPass.cs:136-140` — `if (typeArgument is not null && !IsAnonymousTypeName(typeArgument)) { continue; }` — so `OrderShipped` (the T4 fixture case) produces no `Contract`, no `ContractBinding`, no `CandidateLink`, no `UnresolvedRecord` and no declared exclusion. The only accounting is an arithmetic residual, `src/Csharp2Md.Analysis/Pipeline/InvocationAccounting.cs:135` `unresolved = recognizedTotal - contracted`, which is GCPC-088's aggregate, not one of the four outcomes GCPC-087 names. The label that looks like it guards this (`labels/contracts.json` → `contract-ordershipped`, expected `unresolved`) cannot detect it: `EngineCertificationRunner.cs:161-166` infers `Unresolved` from the mere presence of a message-operation observation, never from a published `UnresolvedRecord` | ❌ GAP (Fix 2) |
| GCPC-088 recognized counts vs per-outcome counts sum to the total | exact sum | `tests/Csharp2Md.Analysis.Tests/Pipeline/InvocationAccountingTests.cs:106` `…ContractAccountingTotalsSumToRecognizedTotal` | ✅ PASS |
| GCPC-089 absence of a contract never presented as absence of messaging | no such claim in any envelope/catalog/page | `ValidationAndCoverageStageTests.cs:139` (unhandled event sits in the denominator, not silently dropped); `tests/Csharp2Md.Projection.Tests/Postings/MessagingContractPostingTests.cs:125` | ✅ PASS |
| GCPC-090 never create a contract from name/structural similarity, path or prefix | no shared contract for the lookalike pair | `fixtures/CertificationCorpus/labels/contracts.json` → `contract-receipt-not-merged`, kind `lookalike`, expected `absent`, rationale citing GCPC-090; `EngineThresholdTests.cs:70-84` asserts Contract precision ≥ 0.99 across the whole area, and `EngineCertificationRunner.cs:272` `IsFalsePositive` would turn a name-similarity contract into a false positive dropping precision to 50 %; `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusContractTests.cs:45-49` asserts the two `Receipt` types are distinct identities in distinct projects | ✅ PASS — **flip to Verified** |
| GCPC-091 proven shared contract → producers and consumers in one posting hop | both recoverable in one hop | `tests/Csharp2Md.Projection.Tests/Postings/MessagingContractPostingTests.cs` (contract producers/consumers families). Proven on a hand-built messaging fixture rather than T4's `OrderCreated`, because pre-existing `ContractPass.IsSharedAcrossProjects` (EBC-25) never promotes a same-project handled event; the substitution is documented in `context.md` and is faithful to the AC's shape | ✅ PASS (substitute fixture accepted) |
| GCPC-092 unproven contract identity → candidate or unresolved with a declared cause | an actual published record | Projection half ✅ `MessagingContractPostingTests.cs:75,102`. **Analysis half not implemented and not tested**: both tests construct the `UnresolvedRecord` by hand (`:83`, comment at `:78` says "Simulates what a classifier … must publish"). Same root cause as GCPC-087 | ❌ GAP, partial — **downgrade from Verified** (Fix 2) |

### P2: Legible catalogs and pages (GCPC-093..098)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-093 catalogs and pages carry compact labels for the proven values | component/type/method/protocol/verb/route | `tests/Csharp2Md.Projection.Tests/Labels/LabelProjectorTests.cs`; `Catalogs/CatalogProjectorTests.cs` | ✅ PASS |
| GCPC-094 every label derived from the payload, citing key and ordinal | citation present and correct | `LabelProjectorTests.cs`; `tests/Csharp2Md.Storage.Tests/Validation/ProjectionValidatorLabelTests.cs` | ✅ PASS |
| GCPC-095 canonical identity stays the authority | label never authoritative | `CatalogProjectorTests.cs` (identity retained alongside every label) | ✅ PASS |
| GCPC-096 no page titled by fact type + encoded identity alone | title carries a label | `tests/Csharp2Md.Projection.Tests/Markdown/MarkdownTitleTests.cs` | ✅ PASS |
| GCPC-097 a disagreeing label/link/ordinal/locator aborts publication naming the offender | abort + offender named | `ProjectionValidatorLabelTests.cs` | ✅ PASS |
| GCPC-098 unproven value → omit the label, never infer | omitted | `LabelProjectorTests.cs`; `MarkdownTitleTests.cs` fallback path | ✅ PASS |

### P2: HTTP verb and route as first-class fields (GCPC-099..102)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-099 both proven → published in their own fields | `http_method` and `route` set | `tests/Csharp2Md.Analysis.Tests/Classification/BoundaryPassTests.cs`; `tests/Csharp2Md.Domain.Tests/Facts/BoundaryFactsTests.cs` | ✅ PASS |
| GCPC-100 the protocol key is not the only means of discovery | fields populated independently | `BoundaryPassTests.cs` | ✅ PASS |
| GCPC-101 pages and catalogs present the published values, never synthesized | same values as the payload | `MarkdownTitleTests.cs`; `CatalogProjectorTests.cs` | ✅ PASS |
| GCPC-102 only one proven → publish it, leave the other unset, record the unproven part | verb set, route unresolved | `BoundaryPassTests.cs` (conventional-route case) | ✅ PASS |

### P2: Configuration and inventory triage (GCPC-103..107)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-103 solution folder never diagnosed as a missing project | no such diagnostic | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusConfigurationTests.cs:46,56`; `Inventory/SolutionFileReaderTests.cs` | ✅ PASS |
| GCPC-104 genuinely absent project still diagnosed, naming both identities | both named | `CertificationCorpusConfigurationTests.cs:64-65` asserts the referencing solution and the missing path | ✅ PASS |
| GCPC-105 explicit parsing policy matching the .NET provider | comments, trailing comma, BOM accepted, policy stated | `tests/Csharp2Md.Analysis.Tests/Extraction/ConfigurationDocumentReaderTests.cs:289,307,325` each assert a real binding **and** no malformed diagnostic; `:403` asserts the policy string is stated | ✅ PASS |
| GCPC-106 outside the policy → still diagnosed malformed | diagnosed, no binding | `ConfigurationDocumentReaderTests.cs:348-362` `Assert.Equal(0, count)` + `Assert.Empty(observations)` + diagnostic | ✅ PASS |
| GCPC-107 ambiguous structure → no binding promoted | duplicate key rejected, no binding | `ConfigurationDocumentReaderTests.cs:366,385` both assert `count == 0` and `Assert.Empty(observations)` | ✅ PASS |

### P2: Determinism, isolation and batch certification (GCPC-108..114)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-108 two runs → byte-identical artifacts | byte-identical | `tests/Csharp2Md.Analysis.Tests/Determinism/WholePackageDeterminismTests.cs:38` | ✅ PASS |
| GCPC-109 two absolute paths → byte-identical | byte-identical | `WholePackageDeterminismTests.cs:64` | ✅ PASS |
| GCPC-110 reversed input / enumeration order → byte-identical | byte-identical | `WholePackageDeterminismTests.cs:98,121` | ✅ PASS |
| GCPC-111 batch keeps each solution's semantics isolated | no cross-contamination | `tests/Csharp2Md.Analysis.Tests/Determinism/BatchIsolationTests.cs:22,50` | ✅ PASS |
| GCPC-112 all required solutions published, compatible and certifiable → certify | certified | `tests/Csharp2Md.Storage.Tests/Validation/BatchValidatorTests.cs:15` | ✅ PASS |
| GCPC-113 otherwise → incomplete scope, reason named, not certified | all three | `BatchValidatorTests.cs:43,65,82` — one test per reason, each asserting the offending solution is named | ✅ PASS |
| GCPC-114 committed per-solution packages left unmodified and valid | untouched | `ComposeCommandTests.cs:73` `…LeavesTheCommittedPackageUntouched` | ✅ PASS |

### P1: Migration completion gate (GCPC-115..120)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-115 objective checklist matching the audit's readiness matrix | six criteria | `tests/Csharp2Md.Analysis.Tests/Readiness/LlmReadinessChecklist.cs` (six `ReadinessCriterion` values mapped to the audit's PARCIAL/FAIL rows) | ✅ PASS |
| GCPC-116 every criterion PASS on a package from the certification corpus | six PASS | The checklist reports PASS. Its Scale criterion reaches that verdict via the exclusion list at `LlmReadinessChecklist.cs:157-170`, which omits `facts/structural.json` (83,371 B) and `manifest.json` (47,273 B) — the two largest artifacts in the package | ⚠️ PASS, but the Scale criterion's evidence is weakened by Gap 1 |
| GCPC-117 every audit regression in a versioned fixture under CI | all seven cases | `fixtures/CertificationCorpus` + `CertificationCorpus*Tests` (entry point, invocation, contract, config/inventory, excluded assets) + `SyntheticSolutionImmutabilityTests.cs` | ✅ PASS |
| GCPC-118 no mandatory gate requires the eShop clone | gate runs without it | `tests/Csharp2Md.Analysis.Tests/Readiness/LocalCorpusReadinessTests.cs:27` `[Trait("Category","LocalCorpus")]`, excluded from every gate command; the full gate ran green with both clones absent | ✅ PASS |
| GCPC-119 clone present → same checklist evaluated and reported | evaluated, reported, never a build failure on absence | `LocalCorpusReadinessTests.cs:31-37` named skip; `:60-68` asserts all six verdicts carry cited evidence | ✅ PASS |
| GCPC-120 report migration complete when scenarios, provenance, budgets and validation all hold | all conditions | `LlmReadinessChecklist.cs` overall-readiness criterion; T66's decision record. The budgets condition is the one weakened by Gap 1 | ⚠️ PASS, conditional on Gap 1 |

**Status**: ❌ Gaps present — 114 ✅ PASS, 3 ❌ GAP (GCPC-038, GCPC-087, GCPC-092 analysis half; GCPC-039
and GCPC-044 partial under the same root cause as GCPC-038, GCPC-071 partial), 3 ⚠️ spec-precision gaps
(GCPC-004, GCPC-031, GCPC-050).

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| GCPC-004 | Pending | ⚠️ Verified (envelope-level; vacuous — no live producer) |
| GCPC-038 | Verified | ❌ Needs Fix |
| GCPC-039 | Verified | ⚠️ Partial (facts never sharded) |
| GCPC-044 | Verified | ⚠️ Partial (ceiling clause unproven) |
| GCPC-071 | Verified | ⚠️ Partial (contract-version half unimplemented) |
| GCPC-087 | Pending | ❌ Needs Fix |
| GCPC-090 | Pending | ✅ Verified |
| GCPC-092 | Verified | ❌ Needs Fix (analysis half) |

---

## Discrimination Sensor

**Skipped — standing project policy** (the user runs Stryker manually and does not want the sensor's
fault-injection pass run by the agent). Recorded in `CLAUDE.md`, `.specs/STATE.md`'s "Standing
engineering constraints" and every batch's task instructions, and skipped for every feature from
`knowledge-taxonomy-contract` onward. No scratch worktree was created and no mutation was injected.

---

## Gate Check

- **Gate command** (Build level, from tasks.md's Gate Check Commands table):
  `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- **Build**: 0 warnings, 0 errors (`TreatWarningsAsErrors` on)
- **Result**: **2050 passed, 0 failed, 0 skipped**
  - Domain 563, Analysis 823, Storage 380, Cli 58, Projection 226
- **Test count before feature** (`524d73e`): 1723
- **Test count after feature**: 2050
- **Delta**: +327
- **Skipped tests**: none. `Category=LocalCorpus` is filter-excluded by the gate itself, as designed
  (GCPC-118); the eShop clones are absent, so those tests would report a named skip rather than a pass.
- **Failures**: none
- **Test integrity**: no decrease in any project. The Storage and Projection rewrites in Phases 6 and 7
  were rewrites, not deletions — every project's count rose.

---

## Edge Cases

- [x] Two dispositions not required by the taxonomy → run `failed`, occurrence and both named —
      `InvocationDispositionTests.cs:77`, `RunCertifierTests.cs:56`
- [x] Every document in a project excluded → project fact with no documents, counted in the aggregate —
      `CertificationCorpusExcludedAssetTests.cs`, `CertificationCorpusDocumentPolicyReportTests.cs:14`
- [ ] **Record larger than the ceiling → own shard, degradation reason recorded, never truncated** — the
      own-shard and never-truncate halves are proven (`LayoutPlannerShardingTests.cs:74-88`), but the
      recorded reason is never published: `LayoutPlan.DegradationReasons` is read by no production code
      (Fix 4)
- [x] Scenario with no instance → marked not exercised with a reason, never passed —
      `RetrievalScenarioRunnerTests.cs:69`
- [x] `validate` target with no manifest → exit `1`, directory unchanged — `ValidateCommandTests.cs`,
      `PackageValidator.cs:171-174` (`not-a-package`)
- [x] Provenance naming a newer generator → exit `6`, no status reported — `ExitCodeTests.cs:102`
- [x] Unreachable labeled-corpus item → certification fails naming it — `LabeledCorpusIntegrityTests.cs`
- [x] Solution with no facts → manifest, registry, provenance still published; every metric
      `not_applicable`; status `degraded` — `RunCertifierTests.cs:97`,
      `tests/Csharp2Md.Storage.Tests/Mapping/EmptySnapshotMappingTests.cs`
- [x] Accepted document unreadable → degradation reason, never a silent policy exclusion —
      `DocumentInventoryTests.cs`
- [x] Route proven only by convention → verb published, route unresolved, entry point still published —
      `CertificationCorpusEntryPointTests.cs:55`, `BoundaryPassTests.cs`

---

## Code Quality

Sampled across all five batches, weighted toward the largest diffs and the tasks carrying Deviation
notes: `LayoutPlanner.cs` (402 lines), `CommandFactory.cs` (390), `RetrievalGuideProjector.cs` (+302),
`ValidationAndCoverageStage.cs` (314), `InvocationAccounting.cs`, `RunCertifier.cs`, `LabelProjector.cs`,
`PackageValidator.cs`, `RelationPass.cs`, `EngineCertificationRunner.cs`, `ScaleInputGenerator.cs`.

| Principle | Status |
| --- | --- |
| Minimum code — no features beyond what was asked | ✅ |
| No abstractions for single-use code | ✅ |
| No unnecessary flexibility | ✅ |
| Only touched files required for the task | ✅ |
| Didn't "improve" unrelated code | ✅ |
| Matches existing patterns/style | ✅ — `CommandFactory` keeps the `Invalid` helper and takes no `Csharp2Md.Domain` reference; `LayoutPlanner` reuses `ShardWriter.BucketKey`'s derivation; new envelopes go through `CanonicalJson` |
| Would a senior engineer approve? | ✅ for the code; ⚠️ for three test exclusion lists that normalise an unmet requirement (below) |
| Tests map to acceptance criteria and are non-shallow | ✅ — `[Trait("Requirement", "GCPC-NNN")]` on most tests; assertions target spec values (counts, category names, exact status strings), not mere presence |
| Spec-anchored outcome check | ⚠️ — three exceptions, all listed as gaps: `ScaleInputGenerator.cs:219-247` and `LlmReadinessChecklist.cs:157-170` assert "no artifact over the ceiling" while excluding the artifacts that are over it; `MessagingContractPostingTests.cs:75,102` assert GCPC-092 against a hand-built record the classifier never produces |
| Per-layer Coverage Expectation met | ✅ — domain/unit tests are 1:1 with ACs; CLI covers every subcommand and every exit code |
| Every test maps to a spec AC, edge case or Done-when | ✅ — no unclaimed tests found in the diff |
| Documented guidelines followed | ✅ — `CLAUDE.md` routing, `Directory.Build.props` `TreatWarningsAsErrors` (0 warnings), the multi-csproj gate convention, and the LocalCorpus fixture rule are all honoured |

**Quality observations that are not gaps:**

- `CertificationCorpusConfigurationTests.cs:105` is named `…AndPromotesNoBinding` but asserts only the
  diagnostic. The "no binding" half is genuinely proven one layer down at
  `ConfigurationDocumentReaderTests.cs:366` — the name is accurate about intent, just not about what
  that particular method checks.
- `InvokesPass.ConcreteImplementors` over-matching (pre-existing; `context.md`) remains. T23 scoped its
  assertion to "exactly one candidate to the concrete implementation" rather than silently accepting it.
  Correct handling of a pre-existing defect.

---

## Deferred Ideas — independent assessment

| Deferred item | Spec requires closing it here? | Verdict |
| --- | --- | --- |
| `InvokesPass.ConcreteImplementors` over-matching | No — pre-existing, and GCPC-018's text is satisfied | Deferral legitimate |
| GCPC-019 traceability gap | Already resolved in `0fd25b4` | Correctly closed |
| Byte-ceiling-as-default not wired | Resolved in T52; confirmed live (provenance carries 32768 and `contains` shards) | Correctly closed |
| `RetrievalScenarioRunner` not wired | Resolved in T48; confirmed by `ValidateCommandTests.cs:148` | Correctly closed |
| `RetrievalGuideProjector` prose under sharding (T52, confirmed at T63) | Partly — GCPC-048 requires the guide to name the artifact holding each relation kind, and under sharding it claims the kind is absent instead. T63's second symptom (the guide itself exceeding the ceiling) is a GCPC-038 violation | **Disagree with continued deferral.** It graduated from theoretical to reproducible at T63; Fix 5 |
| `ShardWriter` fixed 256-bucket depth | No — no current AC requires a single high-fan-in posting group to split, and `ScaleInputGenerator` calibrates around it deliberately | Deferral legitimate; worth a roadmap note |
| GCPC-092 discrete unresolved record (T57, investigated T64) | **Yes.** T64's reasoning is sound about the *readiness matrix* (contract coverage is an audit "non-blocking finding"), but that argues GCPC-115/116 are unaffected, not that GCPC-087/092 are met. Both ACs state the per-item requirement directly | **Disagree with deferral as a closure argument.** Fix 2 |

---

## Fix Plans

### Fix 1 — Compound fact families are never sharded; the published ceiling is violated (Blocker)

- **Requirements**: GCPC-038 (❌), GCPC-039 (partial), GCPC-044 (partial); weakens GCPC-116 and GCPC-120
- **Root cause**: `src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs:105-127` routes
  `facts/structural.json`, `facts/architecture.json`, `facts/contract.json`, `facts/persistence.json`,
  `facts/configuration.json` and `quarantine/records.json` through `AddCompoundFamily`, which plans one
  artifact per family regardless of size. `PlanFamily`'s adaptive prefix-extension loop is never reached
  for them. `manifest.json` and `retrieval.md` are likewise unbounded.
- **Evidence**: a real `analyze` of `fixtures/CertificationCorpus` (70 facts) publishes
  `facts/structural.json` at 83,371 bytes and `facts/architecture.json` at 33,684 bytes against the
  package's own `artifact_ceiling_bytes: 32768`. These grow linearly with symbol count, so on a real
  solution `facts/structural.json` becomes the new `contains.json` — the exact defect the feature exists
  to remove.
- **Fix task**:
  - *What*: give compound fact families the same adaptive sharding `PlanFamily` already applies to flat
    record arrays, or split each compound bundle into per-fact-type flat families that `PlanFamily` can
    shard. Bound `manifest.json` and `retrieval.md` too, or state their exemption in the spec.
  - *Where*: `src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs`,
    `src/Csharp2Md.Storage/Mapping/PackagePublisher.cs`, `src/Csharp2Md.Storage/FactualPackageReader.cs`
    (shard-aware read of compound families), `src/Csharp2Md.Projection/Guides/RetrievalGuideProjector.cs`.
  - *Verify*: delete the exclusion lists at `tests/Csharp2Md.Storage.Tests/Scale/ScaleInputGenerator.cs:219-247`
    and `tests/Csharp2Md.Analysis.Tests/Readiness/LlmReadinessChecklist.cs:157-170`, watch the loops fail
    against current code, then fix. Add a corpus-level assertion that walks every file of a real
    certification-corpus package and asserts none exceeds the published ceiling — the spec's own
    Independent Test for the story.
  - *Done when*: a real `analyze` of `fixtures/CertificationCorpus` and of the `ScaleInputGenerator`
    input both publish zero files over `artifact_ceiling_bytes`, with no exclusion list anywhere.
- **Priority**: Blocker

### Fix 2 — An unhandled, normally-named message operation reaches none of the four outcomes (Major)

- **Requirements**: GCPC-087 (❌), GCPC-092 (❌ analysis half)
- **Root cause**: `src/Csharp2Md.Analysis/Classification/Passes/RelationPass.cs:136-140` emits an
  `UnresolvedRecord(kind: UsesContract)` only when the published message's type argument is `null` or
  anonymous. A normally-named payload with zero in-solution handlers (`OrderShipped`) falls through every
  `AddCandidate`/`AddUnresolved` call site in both `RelationPass.cs` and `ContractPass.cs`.
- **Fix task**:
  - *What*: add a branch for "published message operation, nameable payload, zero inbound handlers" that
    emits an `UnresolvedRecord(kind: UsesContract, cause: NoCandidateFound)` carrying the operation as
    source and the message-operation observation as evidence.
  - *Where*: `src/Csharp2Md.Analysis/Classification/Passes/RelationPass.cs` (`EmitUnresolved`).
  - *Verify*: strengthen `EngineCertificationRunner.ResolveContract` (`:161-166`) so `Unresolved` is
    returned only when a real `UnresolvedRecord` or `CandidateLink` names the payload, never inferred
    from the observation alone — the current resolver cannot fail on this defect. Then add a
    corpus-level test asserting `OrderShipped` reaches exactly one of the four outcomes end to end,
    and an invariant test that every recognized message operation does.
  - *Done when*: `ContractAccounting`'s `unresolved` bucket is reconcilable against discrete published
    records rather than being a subtraction.
- **Priority**: Major

### Fix 3 — `validate` never compares contract versions; the version axes did not advance (Major)

- **Requirements**: GCPC-071 (partial); spec.md "Registry and version axes" (user-confirmed `y`) and
  design.md's version-axis table
- **Root cause**: `src/Csharp2Md.Storage/Validation/PackageValidator.cs:146-159`
  (`EnsureProvenanceCompatible`) compares only `GeneratorVersion`. The five version axes ride in
  provenance and are never read. Separately, `src/Csharp2Md.Domain/Registry/TaxonomyVersions.cs:26`
  advances only `TaxonomyVersion`, and `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyVersionsTests.cs:66`
  (`TaxonomyTables_Default_MovesOnlyTaxonomyVersionToTwo`) pins that. The spec and design both require
  `schema_version`, `extractor_set_version` and `classifier_set_version` to move to 2 — this feature
  interned the wire encoding, changed the manifest shape, added envelopes, changed what is extracted and
  changed how it is classified. A post-feature package is therefore indistinguishable from a pre-feature
  one by schema version.
- **Fix task**:
  - *What*: advance `schema_version`, `extractor_set_version` and `classifier_set_version` to 2, update
    `TaxonomyVersionsTests`, and extend `EnsureProvenanceCompatible` to reject a package whose
    `SchemaVersion` or `TaxonomyVersion` exceeds the running generator's.
  - *Where*: `src/Csharp2Md.Domain/Registry/TaxonomyVersions.cs`,
    `src/Csharp2Md.Storage/Validation/PackageValidator.cs`,
    `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyVersionsTests.cs`,
    `tests/Csharp2Md.Cli.Tests/ExitCodeTests.cs`.
  - *Verify*: an exit-code test mutating `SchemaVersion` (not `GeneratorVersion`) to a higher value and
    asserting `validate` exits `6`.
- **Priority**: Major

### Fix 4 — Degradation reasons are computed and never published (Minor)

- **Requirements**: GCPC-004 (⚠️), the GCPC-038/039 edge case ("SHALL record a degradation reason")
- **Root cause**: `LayoutPlanner.cs:297` builds `record-exceeds-ceiling` reasons into
  `LayoutPlan.DegradationReasons`, which no production code reads. `ValidationAndCoverageStage` never
  attaches a reason to any metric. The envelope field is therefore always `[]` in a real package.
- **Fix task**: route `LayoutPlan.DegradationReasons` (and any analysis-side reason, e.g. an unreadable
  accepted document) onto the affected coverage metric so `coverage.json` actually carries a reason with
  its affected count, and add an end-to-end assertion.
- **Priority**: Minor

### Fix 5 — Retrieval guide prose and size under sharding (Minor)

- **Requirements**: GCPC-048 (⚠️), contributes to GCPC-038
- **Root cause**: `src/Csharp2Md.Projection/Guides/RetrievalGuideProjector.cs` —
  `AppendRelationsSection`, `AppendDisposition` and `PostingHints` test `slots.Contains(baseKey)` and
  enumerate one line per shard key. A sharded family is reported as "not recognized in this package", and
  the guide itself grows past the ceiling.
- **Fix task**: recognise a family by stem/prefix rather than exact slot equality, and describe a
  family's bucketing once instead of enumerating every shard. `ScaleInputGenerator` is the fixture that
  reproduces both symptoms on demand; the test to drive the rewrite already exists.
- **Priority**: Minor

---

## Summary

**Overall**: ❌ Not Ready

**Spec-anchored check**: 114/120 ACs matched the spec-defined outcome; 3 ❌ GAPs (GCPC-038, GCPC-087,
GCPC-092), 3 partials under the same roots (GCPC-039, GCPC-044, GCPC-071), 3 ⚠️ spec-precision gaps
(GCPC-004, GCPC-031, GCPC-050)
**Sensor**: Skipped — standing project policy (user runs Stryker manually); see `CLAUDE.md` and `.specs/STATE.md`
**Gate**: 2050 passed, 0 failed, 0 skipped, 0 warnings

**What works**: `not_evaluated` is gone and every metric publishes a denominator a test re-derives
independently; the private-helper false positive is closed under a versioned fixture; every recognized
invocation carries exactly one disposition and the totals reconcile; the document policy removes the
549 diagnostics and keeps every consumed document; `contains.json` went from a 169 MiB monolith to ~29 KB
shards with every citation still resolving; provenance is byte-deterministic and every manifest entry's
count and byte size match its artifact; `analyze`/`validate`/`compose` with seven mapped exit codes;
engine certification measures precision and recall against independently authored ground truth and fails
on a flipped label; no secret literal or individual hash reaches any published byte; byte-identical
output across runs, paths and input order.

**Issues found**:
1. Compound fact families are never sharded — the published ceiling is violated on the mandatory
   certification corpus itself (Fix 1).
2. An unhandled, normally-named published message reaches none of GCPC-087's four outcomes, and the
   engine-certification label that appears to guard it cannot detect the defect (Fix 2).
3. `validate` never compares contract versions, and the version axes the spec required to advance to 2
   did not (Fix 3).
4. Degradation reasons are computed and never published (Fix 4).
5. The retrieval guide misdescribes sharded families and exceeds the ceiling under scale (Fix 5).

**Next steps**: route Fix 1 and Fix 2 first — both are narrow and each has an existing fixture that
reproduces the failure on demand. Fix 3 is mechanical. Re-verify after, then update `.specs/STATE.md`.
`.specs/STATE.md` is deliberately **not** updated by this report: the verdict is FAIL.
