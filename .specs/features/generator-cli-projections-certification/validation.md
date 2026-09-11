# Generator CLI, Projections and Certification Validation

**Date**: 2026-09-11
**Spec**: `.specs/features/generator-cli-projections-certification/spec.md`
**Diff range**: `524d73e..de559e1` (76 commits: T1–T66, two hygiene commits, the two docs commits that
routed iteration 1's FAIL, and fix commits F1–F5)
**Verifier**: independent sub-agent (author ≠ verifier). Five batch workers authored T1–T66; separate
fix-implementer workers authored F1–F5; none of them wrote this report.
**Iteration**: 2 of a bounded 3-iteration fix→re-verify loop

---

## Verdict

**Result**: FAIL

F2, F3 and F5 fully closed their targeted gaps, proven by live runs rather than by their own tests. F1
closed most of its gap and F4 built a real producer where iteration 1 found none. But the feature's
headline MVP blocker, **B4 / GCPC-038, is still open**, and re-deriving coverage independently this
round surfaced **three further gaps iteration 1 did not reach**, two of them Major.

The decisive evidence is a live `analyze` of a versioned fixture. On
`fixtures/SyntheticSolution/Acme.Orders` the published package contains **three** artifacts over its own
published `artifact_ceiling_bytes: 32768`:

| Artifact | Bytes | × ceiling |
| --- | ---: | ---: |
| `manifest.json` | 174,166 | 5.3× |
| `markdown/component/70fa37fb…ce0.md` | 71,495 | 2.2× |
| `facts/architecture.70fa.json` | 45,354 | 1.4× |

On the mandatory `fixtures/CertificationCorpus` one artifact is over: `manifest.json` at **57,903 bytes**
— larger than iteration 1's 47,273, because F1's sharding multiplied the entry count the manifest has to
enumerate. GCPC-038 says the system SHALL publish no artifact over the ceiling, and the story's own
Independent Test says "walk every file in the package and assert none exceeds the published ceiling". The
exclusion lists iteration 1 flagged were narrowed from 13 keys to 6, but `manifest.json` — the single
artifact that is actually over, and the largest file in every package examined — is still in all three of
them.

Beyond that, three previously unexamined behaviours fail:

- `analyze` exits **0** while publishing certification `degraded` (GCPC-069, GCPC-070).
- Every `source/` manifest entry publishes `byte_size: 0` for a real file (GCPC-061) — audit finding I5
  left half-closed on the largest artifact class by count.
- The manifest publishes two contradicting sets of version axes: `1/1/1` at the top level against
  `2/2/1/2/2` in provenance (GCPC-057).

The gaps are all narrow and each has a concrete fix task below.

---

## Task Completion

| Task range | Status | Notes |
| --- | --- | --- |
| T1–T7 (fixtures) | ✅ Done | - |
| T8–T14 (document + config policy) | ✅ Done | - |
| T15–T24 (facet, entry capability, dispositions, HTTP) | ✅ Done | - |
| T25–T31 (coverage + certification) | ✅ Done | - |
| T32–T41 (wire v2, layout, manifest, provenance) | ⚠️ Partial | Manifest cardinality is wrong for every `source/` entry (Gap 3); manifest itself unbounded (Gap 1) |
| T42–T47 (labels, guide, scenario runner) | ✅ Done | - |
| T48–T52 (validate, compose, exit codes, CLI options) | ⚠️ Partial | `analyze` never maps certification to an exit code (Gap 2) |
| T53–T56 (engine certification) | ✅ Done | - |
| T57–T62 (postings, security, determinism, batch) | ✅ Done | F2 closed the GCPC-087/092 analysis half |
| T63–T66 (scale, readiness, LocalCorpus, decisions) | ⚠️ Partial | The checklist's Scale criterion still exempts the one over-ceiling artifact |
| F1 (shard compound fact families) | ⚠️ Partial | Fact families shard; `manifest.json`, oversized Markdown pages and a solitary oversized fact record remain over the ceiling |
| F2 (discrete unresolved record) | ✅ Done | Verified by live `analyze`, not by its own test |
| F3 (version axes + validate comparison) | ✅ Done | Verified by live `validate` exit `6` |
| F4 (degradation reasons onto metrics) | ⚠️ Partial | A real producer exists, but not for the family where oversized records actually occur |
| F5 (retrieval guide under sharding) | ✅ Done | - |

All 66 T-tasks and all 5 F-tasks carry fully checked `Done when` lists. The only unchecked boxes in
`tasks.md` are the two Definition-of-Done bullets at lines 2151 and 2153, deliberately left for this
Verifier.

---

## Spec-Anchored Acceptance Criteria

All 120 GCPC IDs were re-derived from `spec.md` this session. Every `file:line` below was located and
read in this session; iteration 1's report was read as context only and none of its citations were
inherited. Empirical claims come from live runs performed this session:

- `analyze --solution fixtures/CertificationCorpus/CertificationCorpus.slnx` (package
  `s-7e968f867df278de81a0529e094b2729`, 305 files, 304 manifest entries)
- `analyze --solution fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx` (package
  `s-4fa15a7cd543552f0b7f8134f9a5beb7`, 938 manifest entries)
- the same corpus analyzed a second time, byte-compared
- `validate` against the corpus package, and against three mutated copies

### P1: Certified execution with verifiable denominators (GCPC-001..010)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-001 status exactly passed/degraded/failed, never `not_evaluated` | the literal never published | `tests/Csharp2Md.Analysis.Tests/Pipeline/RunCertifierTests.cs:108` `RunCertificationStatus_HasNoNotEvaluatedMember`; `tests/Csharp2Md.Storage.Tests/Wire/CoverageAndCertificationEnvelopeTests.cs:15`. Live: corpus `run-certification.json` = `{"status":"degraded",…}` | ✅ PASS |
| GCPC-002 each metric publishes numerator/denominator/exclusions/unknowns/reasons | all five fields per metric | `tests/Csharp2Md.Analysis.Tests/Pipeline/ValidationAndCoverageStageTests.cs:32` `…EntryPointNumeratorMatchesPublishedEntryPointFacts`. Live: all four metrics in `coverage.json` carry all five fields | ✅ PASS |
| GCPC-003 denominator is the named population, independently enumerable | test re-derives, never reads back | `ValidationAndCoverageStageTests.cs:18` `…EntryPointDenominatorMatchesIndependentRecount` (+ 4 sibling recounts) | ✅ PASS |
| GCPC-004 per degradation reason, the affected denominator count | a real reason with a real count | `tests/Csharp2Md.Storage.Tests/Mapping/CoverageDegradationRoutingTests.cs:28` `Publish_OversizedInvokesRelation_RoutesADegradationReasonOntoLinkedCallCoverage`; live production path at `src/Csharp2Md.Storage/Mapping/PublicationPipeline.cs:115-121`. **But** the routing covers only `invokes`/`accesses-data`/`uses-contract` confirmed-relation families (`src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs:234-240`). The oversized record that actually occurs on a versioned fixture is an *architecture fact* (`facts/architecture.70fa.json`, one Component, 45,354 B), which is unrouted — so the live `coverage.json` for that run publishes `degradation_reasons: []` on all four metrics | ⚠️ Producer exists but misses the case that fires (Fix 9) |
| GCPC-005 no recall/precision/derived ratio in the run envelopes | no such field | `ValidationAndCoverageStageTests.cs:100` `CoverageMetric_PublishesNoRecallOrPrecisionField` (reflection over the record) | ✅ PASS |
| GCPC-006 all evaluated, nothing quarantined, no reason → `passed` | `passed` | `RunCertifierTests.cs:15` `Certify_AllMetricsEvaluatedWithNoDegradationNoQuarantineNoUnaccounted_IsPassed` | ✅ PASS |
| GCPC-007 reason/unknown/unsupported capability → `degraded` | `degraded` | `RunCertifierTests.cs:28` `Certify_AMetricCarriesAnUnknownOccurrence_IsDegraded`. Live: corpus status `degraded`, reasons name `linked_call_coverage` (4 unknowns) and `contract_coverage` (3) | ✅ PASS |
| GCPC-008 quarantine / conflict / undisposed → `failed` | `failed` | `RunCertifierTests.cs:41` `Certify_QuarantinedDerivedFact_IsFailed` | ✅ PASS |
| GCPC-009 empty population → `not_applicable` with a reason | never a satisfied ratio | `tests/Csharp2Md.Analysis.Tests/Storage/FactualSnapshotTests.cs:194` `CoverageMetric_NotApplicable_RequiresAReason`. Live: `persistence_coverage.state == "not_applicable"` with its reason | ✅ PASS |
| GCPC-010 every metric `not_applicable` → `degraded`, never `passed` | `degraded` | `RunCertifierTests.cs:78` `Certify_LibraryOnlySolutionWithNoEntryPoints_IsDegradedNeverPassed` | ✅ PASS |

### P1: Complete invocation accounting (GCPC-011..018)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-011 exactly one disposition per occurrence | one of five, never zero or two | `tests/Csharp2Md.Analysis.Tests/Classification/InvocationDispositionTests.cs:12`; `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusInvocationTests.cs:27` over a real corpus analyze | ✅ PASS |
| GCPC-012 totals sum to recognized occurrences | exact sum | `tests/Csharp2Md.Analysis.Tests/Pipeline/InvocationAccountingTests.cs:85` `ExecuteAsync_CertificationCorpus_InvocationAccountingTotalsSumToRecognizedOccurrences` | ✅ PASS |
| GCPC-013 undisposed occurrence → `failed`, occurrence named | named in the reasons | `InvocationAccountingTests.cs:70` `Build_OccurrenceWithNoDisposition_IsNamedAsUnaccounted` | ✅ PASS |
| GCPC-014 unresolved + frontier overlap counted once | counted exactly once | `InvocationAccountingTests.cs:49` `Build_OccurrenceWithBothUnresolvedRecordAndOpenFrontier_IsCountedOnceInTheExclusiveTotal` | ✅ PASS |
| GCPC-015 no occurrence in two totals | disjoint buckets | `InvocationAccountingTests.cs:23` `Build_OnePerDispositionOccurrenceEach_TotalsSumToRecognizedOccurrences` | ✅ PASS |
| GCPC-016 out-of-scope callable → counted exclusion, no per-occurrence diagnostic | declared category, no diagnostic | `InvocationDispositionTests.cs:34` `Create_CategorizedExclusion_IsRepresentable`; `CertificationCorpusInvocationTests.cs` framework-call case. Live: corpus `linked_call_coverage.exclusions == 7`, and the corpus `diagnostics.json` holds five records, none per-occurrence | ✅ PASS |
| GCPC-017 non-demonstrable continuation → terminal effect with declared cause | frontier carrying a cause, reachable from the occurrence | `src/Csharp2Md.Analysis/Classification/Passes/InvokesPass.cs` `OpenFrontier.Create(occurrence, cause)`; `tests/Csharp2Md.Analysis.Tests/Classification/InvokesPassTests.cs:217`. Live: corpus `relations/frontiers.json` is populated and the `disposition:open frontier` scenario reaches its endpoint (`measurements.json`) | ✅ PASS (no `Requirement` trait; cited by behaviour) |
| GCPC-018 interface/abstract → one candidate per concrete impl, no confirmed to the interface | exactly that shape | `CertificationCorpusInvocationTests.cs:27` class; live corpus `relations/candidates.json` holds the candidate and `relations/confirmed/invokes.json` holds no edge to the interface member | ✅ PASS |

### P1: Proven entry capability (GCPC-019..025)

Live evidence for this whole story: the corpus package's merged `facts/architecture.*.json` shards hold
exactly **four** `EntryPoint` facts — `GetWidget`, `Index`, `HandleAsync`, `GetOrderStatus` — and
`ChangeUriPlaceholder` is in none of them.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-019 `EntryPoint` only with positive entry evidence | promotion predicate requires proven capability | `tests/Csharp2Md.Analysis.Tests/Classification/EntryPointPassTests.cs:133`; `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusEntryPointTests.cs:19`. Live as above | ✅ PASS |
| GCPC-020 not externally reachable → no `EntryPoint` | regardless of declaring type | `EntryPointPassTests.cs:133` `Execute_PrivateHelperOnRecognizedController_DoesNotCreateEntryPoint`. Live: `ChangeUriPlaceholder` absent from every published entry point | ✅ PASS |
| GCPC-021 framework-type helper with no entry evidence → no `EntryPoint` | not published | same citations as GCPC-020 | ✅ PASS (no trait) |
| GCPC-022 conventional action, no route → `EntryPoint` + missing-route diagnostic | both present | `CertificationCorpusEntryPointTests.cs:54` `…IndexConventionalActionStaysPublishedWithMissingRouteDiagnostic`. Live: `Index` is an entry point and `diagnostics.json` carries `missing-route-declaration` | ✅ PASS |
| GCPC-023 publish the evidence, cited by artifact key and ordinal | citation resolves in the same publication | `CertificationCorpusEntryPointTests.cs:76` `…EveryEntryPointCitesEvidenceResolvableInThePublishedPackage` | ✅ PASS |
| GCPC-024 undeterminable capability → candidate/unresolved, never confirmed | unresolved record instead | `EntryPointPassTests.cs:163` `…PublishesUnresolvedNotEntryPoint` | ✅ PASS |
| GCPC-025 regression reproduced in the versioned corpus, no eShop clone needed | corpus test, CI-runnable | `CertificationCorpusEntryPointTests.cs:19`; the class carries no `LocalCorpus` trait and the gate ran green with both clones absent | ✅ PASS |

### P1: Supported-document policy (GCPC-026..035)

Live evidence: the corpus package's merged `facts/structural.*.json` hold 1 solution, 4 projects, **10
documents**, 42 symbols; not one document path carries `.ts`, `.js`, `.map`, an image extension, `.zip`,
`package-lock` or `.pfx`; `source/` holds exactly those same 10 artifacts; `diagnostics.json` holds
exactly one `unsupported-document` record.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-026 explicit policy naming accepted/conditional/excluded classes | every class decided by category | `tests/Csharp2Md.Analysis.Tests/Inventory/SupportedDocumentPolicyTests.cs` (one test per category); `tests/Csharp2Md.Analysis.Tests/Inventory/DocumentInventoryTests.cs:269` | ✅ PASS |
| GCPC-027 matching document → inventoried, fact, `source/` artifact | all three | `DocumentInventoryTests.cs:88`; `tests/Csharp2Md.Analysis.Tests/Inventory/InventoryStageTests.cs:130`. Live as above | ✅ PASS |
| GCPC-028 non-matching → no fact, no `source/`, no relation, no individual diagnostic | all four absent | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusExcludedAssetTests.cs:39` `…ExcludesEveryExcludedAssetFromStructuralFacts`. Live as above | ✅ PASS |
| GCPC-029 conditional extension admitted only on a declared consumer | admitted iff declared | `tests/Csharp2Md.Analysis.Tests/Classification/ClassifierCapabilityRegistryTests.cs:8` and siblings | ✅ PASS |
| GCPC-030 allowlist admits the listed documents only | no other excluded class admitted | `tests/Csharp2Md.Analysis.Tests/AnalysisRequestAllowlistTests.cs:17` `…AllowlistedTypeScriptFile_IsInventoriedWhileSiblingJavaScriptStaysExcluded` | ✅ PASS |
| GCPC-031 "every analyzed document" means "every accepted document" | a definition, not an observable outcome | no test carries this ID and none can: the AC states a reading convention, not a predicate. Its observable consequences are GCPC-027/028/032, each proven above and live | ⚠️ Spec-precision gap (definitional; unchanged from iteration 1) |
| GCPC-032 `source/` for every accepted, none for any excluded | exact set equality | `CertificationCorpusExcludedAssetTests.cs:56` `…PublishesNoSourceArtifactForAnyExcludedAsset`. Live: `source/` set == document-fact set, 10 each | ✅ PASS |
| GCPC-033 at most one aggregated exclusion diagnostic | one record naming count and extensions | `CertificationCorpusExcludedAssetTests.cs:93` `…PublishesExactlyOneAggregatedExclusionDiagnostic`. Live: exactly one `unsupported-document` record in a 5-record `diagnostics.json` | ✅ PASS |
| GCPC-034 accepted/excluded count and bytes per category | per-category totals | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusDocumentPolicyReportTests.cs:13` `…ReportsKnownCountsAndBytesPerCategory` | ✅ PASS |
| GCPC-035 no consumed evidence removed by the policy | bindings still promoted | `tests/Csharp2Md.Analysis.Tests/Extraction/ConfigurationDocumentReaderTests.cs:288` and siblings each assert a surviving binding; live: `.cs` and `.csproj` retained for all four projects | ✅ PASS (no trait; cited by behaviour) |

### P1: Bounded payloads and measured budgets (GCPC-036..045)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-036 declared budget and ceiling published | both published | `tests/Csharp2Md.Cli.Tests/AnalyzeBudgetAndAllowlistTests.cs:63`, `:112`. Live: provenance carries `artifact_ceiling_bytes: 32768` and `token_estimator_id: csharp2md.tokens.bytes-per-token-v1` | ✅ PASS |
| GCPC-037 ceiling derived from budget × measured ratio, calculation published | derivation reproducible | `tests/Csharp2Md.Storage.Tests/Mapping/CeilingCalculatorTests.cs`; `AnalyzeBudgetAndAllowlistTests.cs:112` `Analyze_SuppliedReadingBudget_ReachesThePublishedProvenance` | ✅ PASS |
| GCPC-038 **no artifact exceeds the declared ceiling** | zero artifacts over 32,768 B | **Still violated.** Corpus: `manifest.json` = 57,903 B (up from iteration 1's 47,273 — F1's sharding raised the entry count the manifest enumerates, 304 entries × ~190 B). `fixtures/SyntheticSolution/Acme.Orders`: `manifest.json` 174,166 B, `markdown/component/70fa37fb…ce0.md` 71,495 B, `facts/architecture.70fa.json` 45,354 B. Three separate exclusion lists still skip `manifest.json`: `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusCeilingTests.cs:20-28`, `tests/Csharp2Md.Analysis.Tests/Readiness/LlmReadinessChecklist.cs:161-168`, `tests/Csharp2Md.Storage.Tests/Scale/ScaleInputGenerator.cs:219-226`. Markdown pages are in no exclusion list and are never sharded. `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs` plans no split for the manifest at all | ❌ GAP (Fix 6) |
| GCPC-039 facts, observations, relations, candidates, unresolved, frontiers, catalogs, postings each split within the ceiling | every named family splits | **Closed by F1.** Live corpus: `facts/structural` splits into 51 shards (455–3,500 B), `facts/architecture` into 13 (1,192–8,731 B), `relations/confirmed/contains` into 45, `observations/type-usage` into 47 — each within 32,768. `tests/Csharp2Md.Storage.Tests/Mapping/CompoundFamilyShardingTests.cs:14`; `tests/Csharp2Md.Storage.Tests/Mapping/LayoutPlannerShardingTests.cs`. `manifest.json` is not in GCPC-039's family list, so it does not bear on this ID | ✅ PASS — **upgrade from iteration 1's partial** |
| GCPC-040 split preserves identity, ordering, hash, citation resolvability | all four | `tests/Csharp2Md.Storage.Tests/Mapping/LayoutPlannerTests.cs:14`; `tests/Csharp2Md.Storage.Tests/Mapping/PackagePublisherPlanDrivenTests.cs:14`. Live: `validate` over the corpus package re-resolves every citation and exits on certification status, not on a defect | ✅ PASS |
| GCPC-041 cited ordinal in a split artifact yields the claimed record | citation resolves post-split | `tests/Csharp2Md.Storage.Tests/Mapping/PublishedPackageViewShardAwareTests.cs:13` | ✅ PASS |
| GCPC-042 same input → same shard assignment | identical across runs | `ScaleInputGenerator.cs:292` `Plan_ScaleInputGeneratedTwice_AssignsEveryRecordToTheSameShard`. Live: two full corpus analyses produced identical file sets and **zero byte-differing files** across 305 files | ✅ PASS |
| GCPC-043 no directory keyed by a high-cardinality fact identity | shards are flat files | `ScaleInputGenerator.cs:251-257`. Live: every shard is a flat `family.<hex>.json` inside its family directory | ✅ PASS |
| GCPC-044 scale input splits `contains`, `belongs-to`, invocation **and publishes no artifact over the ceiling** | both clauses | Split clause ✅ (`ScaleInputGenerator.cs:243-249`). Ceiling clause ❌ — the same test's per-file loop still skips `manifest.json` (`ScaleInputGenerator.cs:219-226`), the artifact whose size grows directly with the shard count this requirement creates | ❌ GAP, partial (Fix 6) |
| GCPC-045 byte size and token estimate of the largest artifact of each role | both published | `ScaleInputGenerator.cs:269-282` reads `ByteSize` from the manifest and derives the estimate through the published estimator. Two caveats: the token estimate is derivable rather than stored, and every manifest entry carries `role: "payload"`, so "each role" collapses to one. Live: on `Acme.Orders` the genuinely largest artifact is `manifest.json`, which is not a manifest entry, so its size is published nowhere | ⚠️ PASS on the letter, weakened by Gap 1 |

### P1: Executable retrieval guide (GCPC-046..055)

Live evidence: the corpus `retrieval.md` is **3,606 bytes** (within ceiling), names all seven relation
kinds, and `measurements.json` records every scenario with reads/hops/bytes/tokens/relevant/noise — the
costliest being `locate-an-identity` at 4 reads / 20,499 B / 2,502 tokens, far inside the declared 25
reads / 100,000 tokens.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-046 locating an identity directs to a catalog, never a canonical payload | no payload key in the locate section | `tests/Csharp2Md.Projection.Tests/Guides/RetrievalGuideProjectorTests.cs:30` `Project_LocateSection_NamesACatalogAndNeverACanonicalPayloadArtifact` | ✅ PASS |
| GCPC-047 documents bucket selection and ordinal resolution | both, without whole-payload reads | `RetrievalGuideProjectorTests.cs:58`; `:165` `Project_PostingsSection_ShardedOutgoingFamily_DescribesBucketingOnceNotOncePerShard` (40 sharded relations produce exactly one `postings/outgoing` line) | ✅ PASS |
| GCPC-048 a path for each of the seven relation kinds, naming the holding artifact | all seven | **Closed by F5.** `RetrievalGuideProjectorTests.cs:78` `Project_RelationsSection_DocumentsAllSevenKinds`, plus `:126` `Project_RelationsSection_ShardedInvokesFamily_IsRecognizedNotReportedAbsent`. I independently judged the substitute unit tests faithful, not shallow: `ShardedInvokesView` (`RetrievalGuideProjectorTests.cs:416-436`) drives a **real `LayoutPlanner.Plan`** split of eight `Invokes` relations rather than a hand-built fixture, then asserts both the negative ("no such relation is recognized" absent) and the positive prose, and that no exact absent key is named. Live corpus: all seven kinds listed; the four absent kinds correctly report absence because the corpus genuinely holds no confirmed relation of those kinds | ✅ PASS — **upgrade from iteration 1's documented hole** |
| GCPC-049 separate paths for candidates, unresolved, frontiers | each with its own artifact | `RetrievalGuideProjectorTests.cs:148` `Project_DispositionsSection_ShardedUnresolvedFamily_IsRecognizedNotReportedAbsent`; `:182` | ✅ PASS |
| GCPC-050 never instruct a full read when an index addresses the record | no full-read instruction | `src/Csharp2Md.Projection/Guides/RetrievalGuideProjector.cs:89` and `:144` emit "without reading a canonical payload in full" and "never the whole canonical payload"; `RetrievalGuideProjectorTests.cs:58`. No test asserts the negative across the whole guide, and "open in full" has no precise testable predicate in the spec | ⚠️ Spec-precision gap (unchanged from iteration 1) |
| GCPC-051 stopping rule for all five conditions | five rules | `RetrievalGuideProjectorTests.cs:230` `Project_StoppingRules_DocumentsAllFive` | ✅ PASS |
| GCPC-052 executed scenario publishes reads/hops/bytes/tokens/relevant/noise | all six | `tests/Csharp2Md.Storage.Tests/Retrieval/RetrievalScenarioRunnerTests.cs:152`; `tests/Csharp2Md.Cli.Tests/ValidateCommandTests.cs:147`. Live: all six per record in `measurements.json` | ✅ PASS |
| GCPC-053 scenario reaches its endpoint within budget | within 100k tokens / 25 reads | `RetrievalScenarioRunnerTests.cs:32` `Run_FullyPopulatedPackage_EveryDocumentedScenarioIsExercisedAndReachesItsEndpointWithinBudget`. Live as above | ✅ PASS |
| GCPC-054 every documented path executed automatically, run fails on non-resolution | failure names the offender | `RetrievalScenarioRunnerTests.cs:102` `Run_BothArtifactSources_ProduceTheSameReportForTheSamePackage` and siblings. Live: `validate` printed every scenario's verdict, with the four unexercised kinds reported "not exercised", never "passed" | ✅ PASS |
| GCPC-055 guide naming an absent key aborts, naming it | abort + offender named | `RetrievalGuideProjectorTests.cs:248` `ValidateNoAbsentKeys_TextNamingAKeyNotInThePublication_AbortsNamingTheOffender` | ✅ PASS |

### P1: Provenance and manifest cardinality (GCPC-056..062)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-056 generator version + reproducible build identity | both present | `tests/Csharp2Md.Storage.Tests/Wire/ProvenanceDtoTests.cs:7` class. Live: `generator_version: "4.0.0.0"`, `build_identity: "bf1b165f-…"` | ✅ PASS |
| GCPC-057 all five version axes carried | five axes, correct values | Provenance is correct and now matches the user-confirmed spec row exactly — live: `schema 2, taxonomy 2, observation 1, extractor 2, classifier 2` (F3's target; `src/Csharp2Md.Domain/Registry/TaxonomyVersions.cs:30-36`). **But the same manifest also publishes a contradicting copy**: its top-level `schema_version: 1, taxonomy_version: 1, observation_schema_version: 1`, because `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs:65` and `src/Csharp2Md.Storage/Mapping/DomainMapper.cs:120` both use `TaxonomyVersions.Initial` (1,1,1,1,1) rather than `TaxonomyTables.Default.Versions`. A consumer reading `manifest.schema_version` still cannot tell a post-feature package from a pre-feature one — the exact distinguishability F3 was routed to restore | ⚠️ PASS on the AC's letter; package is self-contradicting (Fix 8) |
| GCPC-058 deterministic parameters: ceiling, policy version, allowlist digest | all three | `tests/Csharp2Md.Analysis.Tests/Inventory/SupportedDocumentPolicyTests.cs:158` `Version_IsANonEmptyStringCarriedIntoProvenance`. Live: `artifact_ceiling_bytes: 32768`, `document_policy_version: "supported-document-policy/1"`, `allowlist_digest: "e3b0c442…"` | ✅ PASS |
| GCPC-059 no timestamps or durations in manifest/provenance | none | `ProvenanceDtoTests.cs` reflection assertions; `tests/Csharp2Md.Cli.Tests/ValidateCommandTests.cs:147`. Live: the published provenance block carries no timestamp field; `measurements.json` carries `timestamp`/`duration_milliseconds` | ✅ PASS |
| GCPC-060 byte-identical provenance across two runs of the same build | byte-identical | `ProvenanceDtoTests.cs:7` class; `tests/Csharp2Md.Analysis.Tests/Determinism/WholePackageDeterminismTests.cs:37`. Live: two corpus runs, 305 files each, **0 byte-differing files** | ✅ PASS |
| GCPC-061 manifest entry carries the artifact's real count and byte size | equal to real content | `tests/Csharp2Md.Storage.Tests/Mapping/ManifestRealCardinalityTests.cs:15`; `tests/Csharp2Md.Storage.Tests/Validation/PackageValidatorManifestChecksTests.cs:12`. **Violated for every deferred artifact.** Live corpus: 10 of 304 entries publish `count: 0, byte_size: 0` while the file on disk is 207–2,357 B — all ten are `source/…` artifacts. On `Acme.Orders`, 29 of 938. Root cause: `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs:43-46` hardcodes `byteSize = 0` for `fragment.IsDeferred`. This is audit finding I5 closed for catalogs, postings and Markdown but left open on `source/`, the largest artifact class by count in the audited package. `PackageValidator` does not reject it, so `validate` returns success on a manifest that misreports ten artifacts | ❌ GAP (Fix 7) |
| GCPC-062 every file reachable from the manifest | exact set equality | `PackageValidatorManifestChecksTests.cs:12`. Live: 305 files on disk, 304 entries + `manifest.json`, **zero in either set difference** | ✅ PASS |

### P1: Final CLI surface (GCPC-063..073)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-063 exactly three subcommands | `analyze`, `validate`, `compose` | `tests/Csharp2Md.Cli.Tests/AnalyzeCommandTreeTests.cs:10` `RootCommand_ExposesExactlyTheThreeCertificationVerbs` | ✅ PASS |
| GCPC-064 `validate` opens no solution, loads no project, runs no semantic analysis | dependency-level proof | `tests/Csharp2Md.Cli.Tests/ValidateCommandTests.cs:16` `Validate_DependsOnlyOnStorageAndProjection_NeitherReferencesRoslynOrMSBuildDirectly`. Live: `validate` ran against a package copied outside the repo with no solution present | ✅ PASS |
| GCPC-065 detects all seven defect classes | each class detected | `tests/Csharp2Md.Storage.Tests/Corruption/CorruptedPackageTests.cs:10` (one corrupted package per class); `ValidateCommandTests.cs:170` | ✅ PASS |
| GCPC-066 names artifact key, defect class and offending value | all three | `ValidateCommandTests.cs:171` `Validate_PackageWithMismatchedManifestByteSize_Exits5AndNamesTheDefect`. Live: a mutated provenance produced `csharp2md: incompatible-provenance: Package schema version 99 is newer than the running generator's schema version 2.` | ✅ PASS |
| GCPC-067 reports the published status, recomputes nothing | reads, never recomputes | `ValidateCommandTests.cs:60` `Validate_PublishedPackage_ReportsPublishedCertificationStatusAndMatchingExitCode` | ✅ PASS |
| GCPC-068 `compose` reproduces batch artifacts with no solution present | byte-for-byte equal | `tests/Csharp2Md.Cli.Tests/ComposeCommandTests.cs:12` `Compose_WithNoSolutionPresent_ReproducesTheAnalyzeBatchArtifactsByteForByte`. Live: the corpus `analyze` produced `batch-manifest.json` and `composition/` alongside the package | ✅ PASS |
| GCPC-069 passed → exit `0` | `0` only when `passed` | `tests/Csharp2Md.Cli.Tests/ExitCodeTests.cs:15` `Certification_Passed_MapsToZero` — but that test drives **`validate`**, not `analyze`. **`analyze` returns `0` unconditionally for any non-partial run**: `src/Csharp2Md.Cli/CommandFactory.cs:147-149` returns only `PartialComposition` or `Success` and never reads the certification status. Live: `analyze` of `fixtures/CertificationCorpus` published `status: degraded` and exited **0**; `analyze` of `fixtures/SyntheticSolution/Acme.Orders` published `status: degraded` and exited **0** | ❌ GAP (Fix 7) |
| GCPC-070 degraded → `3`, failed → `4` | both, for a completing command | `ExitCodeTests.cs:35` `Certification_Degraded_MapsToThree` and `:57` both rewrite `run-certification.json` and then run **`validate`**. No test drives `analyze`'s own status mapping, and `ExitCodeTests.cs:258` actively asserts `analyze` of `Acme.Orders` exits `0` — a run whose published status is `degraded`. Spec's failure-semantics table maps "Run `degraded`; exit `3`; **package published**" — publishing is `analyze`, not `validate` | ❌ GAP (Fix 7) |
| GCPC-071 structural corruption → `5`; incompatible provenance or contract version → `6` | both halves | **Closed by F3.** `5`: `ExitCodeTests.cs:75` `StructuralCorruption_MapsToFive`. `6`: `src/Csharp2Md.Storage/Validation/PackageValidator.cs:152-179` now compares `GeneratorVersion`, `SchemaVersion` **and** `TaxonomyVersion`. Verified live, independent of any test: mutating only `provenance.schema_version` to 99 → exit `6` with a named defect; mutating only `provenance.taxonomy_version` to 99 → exit `6`; the pre-existing newer-generator case still exits `6` | ✅ PASS — **upgrade from iteration 1's partial** |
| GCPC-072 one unpublished solution → `2`, committed packages unmodified | both | `ExitCodeTests.cs:177` `PartialComposition_UnpublishedSolutionInABatch_MapsToTwo`; `ComposeCommandTests.cs:72` `…LeavesTheCommittedPackageUntouched` | ✅ PASS |
| GCPC-073 invalid invocation → `1`, publishes nothing | both | `ExitCodeTests.cs:155` `InvalidInvocation_MapsToOneAndPublishesNothing` | ✅ PASS |

### P1: Engine certification on labeled corpora (GCPC-074..081)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-074 corpora record source, expected state and rationale | all three per item | `tests/Csharp2Md.Analysis.Tests/Certification/LabeledCorpusIntegrityTests.cs:29` `Read_EveryLabeledItem_CarriesASourceReferenceAndAWrittenRationale`; `fixtures/CertificationCorpus/labels/*.json` | ✅ PASS |
| GCPC-075 labels authored independently of classifier output | resolver never reads `Expected` | `LabeledCorpusIntegrityTests.cs:50` `Read_EveryLabeledItemsConstruct_StillExistsInTheFixtureSource`; `tests/Csharp2Md.Analysis.Tests/Certification/EngineCertificationRunner.cs` `Resolve` takes only the label id and the publication | ✅ PASS |
| GCPC-076 positives, negatives and lookalikes per classifier | all three kinds | `LabeledCorpusIntegrityTests.cs:17` `Read_EveryCertifiedArea_CarriesPositiveNegativeAndLookalikeItems` | ✅ PASS |
| GCPC-077 precision and recall per certified area | computed from ground truth | `tests/Csharp2Md.Analysis.Tests/Certification/EngineCertificationRunnerTests.cs:14` `RunAsync_EntryPointArea_MatchesEveryLabelAgainstRealClassifierOutput`. **Strengthened by F2**: `EngineCertificationRunner.ResolveContract` now reads `relations/unresolved.json` / `relations/candidates.json` and matches the record's own evidence, instead of inferring `Unresolved` from the presence of a message-operation observation. I confirmed the strengthened resolver still resolves `contract-ordershipped` to `Unresolved`, now backed by a real published record | ✅ PASS |
| GCPC-078 thresholds 99/95, 99/90, 99/95, 99/90 | verbatim | `tests/Csharp2Md.Analysis.Tests/Certification/EngineThresholdTests.cs:69` `Evaluate_EveryCertifiedArea_MeetsItsNormativeThreshold` (theory over all four areas) | ✅ PASS |
| GCPC-079 below threshold → fail, naming area, value and failing items | all three named | `EngineThresholdTests.cs:87` `Evaluate_FlippedEntryPointLabel_FailsCertificationAndNamesTheFlippedItem` | ✅ PASS |
| GCPC-080 separate from run certification, no precision/recall in a package | no such field | `EngineCertificationRunnerTests.cs:93` `AnalyzeAsync_CertificationCorpus_PublishesNoPrecisionOrRecallValueInAnyArtifact`. Live: neither string appears in the corpus package | ✅ PASS |
| GCPC-081 versioned repository artifact, not a CLI subcommand | report file, three verbs only | `tests/Csharp2Md.Analysis.Tests/Certification/EngineCertificationReportTests.cs:13` `Generate_FromAFreshMeasurement_MatchesTheCommittedReportExactly`; `AnalyzeCommandTreeTests.cs:10` | ✅ PASS |

### P1: Security and fidelity under new projections (GCPC-082..086)

Unaffected by F1–F5's blast radius; re-checked anyway. All five are proven against a real published
package built from `fixtures/SyntheticSolution/Acme.Orders`, whose `appsettings.json` carries the
fixture secret — a substitution for the spec's "place in the certification corpus a configuration
document whose secret value would become a label", documented at `SecretAbsenceTests.cs:12-28` and
faithful to the AC's shape.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-082 redaction model preserved | sidecar, both hashes, ordinal-sorted spans | `tests/Csharp2Md.Projection.Tests/Security/SecretAbsenceTests.cs:72` `TheRedactedSidecar_StillDeclaresRedactedTheOrdinalSortedSpansAndBothHashes` | ✅ PASS |
| GCPC-083 no secret literal or individual hash in any published byte | absent everywhere | `SecretAbsenceTests.cs:39` fixture + the literal and SHA-256 sweeps, `Assert.All` over every published file | ✅ PASS |
| GCPC-084 label proven outside a redacted span before publication | no secret-derived label | `tests/Csharp2Md.Projection.Tests/Labels/LabelProjectorTests.cs:140` `For_EntryPoint_WhenSymbolDeclarationIsInsideARedactedSpan_OmitsTypeAndMethodLabels` | ✅ PASS |
| GCPC-085 separate consent for generators, analyzers never run | both | `tests/Csharp2Md.Analysis.Tests/Semantics/TrustBoundaryTests.cs:20` `Strip_ThenGetCompilationAsync_NeverExecutesTheAttachedSourceGenerator` | ✅ PASS |
| GCPC-086 no credential in a coverage/provenance/accounting envelope | absent | `SecretAbsenceTests.cs:111` `CoverageProvenanceAndAccountingEnvelopes_CarryNoCredentialConnectionStringOrToken` | ✅ PASS |

### P2: Contract and message-operation accounting (GCPC-087..092)

**F2's fix verified end to end by live `analyze`, not by its own test.** The corpus package's
`relations/unresolved.json` holds 9 records, two of kind `uses-contract` with cause `NoCandidateFound`:
one whose source is the `outbound␀messaging␀global::Certification.Messaging.OrderShipped` boundary
operation, evidenced by the `message-operation` observation on `OrderPublisher.PublishOrderShippedAsync`;
the other the equivalent for `OrderCreated`. These are records the classifier actually produced.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-087 every recognized message operation / payload slot reaches exactly one of four outcomes | one discrete record per item | **Closed by F2.** `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusContractTests.cs:16` and `:35`; the invariant test `…EveryRecognizedPublishOperationReachesExactlyOneOutcome`. Live as above | ✅ PASS — **upgrade from iteration 1's GAP** |
| GCPC-088 recognized counts vs per-outcome counts sum to the total | exact sum | `tests/Csharp2Md.Analysis.Tests/Pipeline/InvocationAccountingTests.cs:104` `…ContractAccountingTotalsSumToRecognizedTotal`. The `unresolved` bucket is now reconcilable against discrete published records rather than being a subtraction | ✅ PASS |
| GCPC-089 absence of a contract never presented as absence of messaging | no such claim | `InvocationAccountingTests.cs:105`; `tests/Csharp2Md.Projection.Tests/Postings/MessagingContractPostingTests.cs`. Live: `contract_coverage` publishes `denominator: 3, unknowns: 3`, never a silent zero | ✅ PASS |
| GCPC-090 never create a contract from name/structural similarity, path or prefix | no shared contract for the lookalike pair | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusContractTests.cs:132` `…SameNamedPayloadTypesAreDistinctSymbolsInDifferentProjects`; `fixtures/CertificationCorpus/labels/contracts.json` → `contract-receipt-not-merged` (lookalike, expected `absent`); `EngineThresholdTests.cs:69` would drop Contract precision below 0.99 on a name-similarity merge. Live: the corpus publishes **no** `facts/contract*.json` at all, so no lookalike merge occurred. Regression check after F1–F5: clean | ✅ PASS — confirmed still Verified |
| GCPC-091 proven shared contract → producers and consumers in one posting hop | both in one hop | `tests/Csharp2Md.Projection.Tests/Postings/MessagingContractPostingTests.cs:41` `Project_MessagingContractWithBothDirectionsBoundAsRequest_ReachesProducerAndConsumerInOneHop`. Proven on a purpose-built messaging fixture rather than T4's `OrderCreated`, because pre-existing `ContractPass.IsSharedAcrossProjects` (EBC-25) never promotes a same-project handled event — confirmed live: `OrderCreated` also lands as `uses-contract` unresolved. Substitution documented in `context.md` and faithful to the AC's shape | ✅ PASS (substitute fixture accepted) |
| GCPC-092 unproven contract identity → candidate or unresolved with a declared cause | an actual published record | **Closed by F2.** `CertificationCorpusContractTests.cs:35` `AnalyzeAsync_CertificationCorpus_OrderShippedReachesExactlyOneOfTheFourOutcomes`; live record above carries `cause: NoCandidateFound`. The hand-built `MessagingContractPostingTests` records remain as isolated projector proofs, now supplemented by the classifier-driven corpus assertions | ✅ PASS — **upgrade from iteration 1's GAP** |

### P2: Legible catalogs and pages (GCPC-093..098)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-093 catalogs and pages carry compact labels for proven values | component/type/method/protocol/verb/route | `tests/Csharp2Md.Projection.Tests/Catalogs/CatalogProjectorTests.cs:555` and siblings | ✅ PASS |
| GCPC-094 every label derived from the payload, citing key and ordinal | citation present and correct | `CatalogProjectorTests.cs:481` `Project_EntryPoints_EntryLabelsResolveToTheCitationsOfTheirProvenValues` | ✅ PASS |
| GCPC-095 canonical identity stays the authority | label never authoritative | `CatalogProjectorTests.cs:606` `Project_EntryPoints_OrderingStaysKeyedOnFactIdAndCanonicalIdRemainsTheIdentity` | ✅ PASS |
| GCPC-096 no page titled by fact type + encoded identity alone | title carries a label | `tests/Csharp2Md.Projection.Tests/Markdown/MarkdownTitleTests.cs:99` `Project_AllSevenPageFamilies_NoTitleConsistsSolelyOfFactTypeAndEncodedIdentity` | ✅ PASS |
| GCPC-097 disagreeing label/link/ordinal/locator aborts naming the offender | abort + offender named | `tests/Csharp2Md.Storage.Tests/Validation/ProjectionValidatorLabelTests.cs:27` `Commit_LabelValueAlteredByOneCharacter_AbortsNamingTheOffenderAndLeavesThePriorPackageByteIdentical` | ✅ PASS |
| GCPC-098 unproven value → omit the label, never infer | omitted | `CatalogProjectorTests.cs:536` and siblings | ✅ PASS |

### P2: HTTP verb and route as first-class fields (GCPC-099..102)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-099 both proven → published in their own fields | `http_method` and `route` set | `tests/Csharp2Md.Analysis.Tests/Classification/BoundaryPassTests.cs:49` `Execute_RouteDeclarationWithVerbAndTemplate_PublishesBothHttpMethodAndRouteFields` | ✅ PASS |
| GCPC-100 the protocol key is not the only means of discovery | fields populated independently | `BoundaryPassTests.cs:50` | ✅ PASS |
| GCPC-101 pages and catalogs present the published values, never synthesized | same values as the payload | `MarkdownTitleTests.cs:43`, `:73` | ✅ PASS |
| GCPC-102 only one proven → publish it, leave the other unset, record the unproven part | verb set, route unresolved | `BoundaryPassTests.cs:82` `Execute_RouteDeclarationWithVerbButNoTemplate_PublishesTheVerbAndRecordsRouteAsUnresolved` | ✅ PASS |

### P2: Configuration and inventory triage (GCPC-103..107)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-103 solution folder never diagnosed as a missing project | no such diagnostic | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusConfigurationTests.cs:27` `ReadProjectPaths_ConfigurationShapesSln_ExcludesTheSolutionFolderButListsBothProjects` | ✅ PASS |
| GCPC-104 genuinely absent project still diagnosed, naming both identities | both named | `CertificationCorpusConfigurationTests.cs:45` `…DoesNotDiagnoseTheSolutionFolderButStillDiagnosesTheGenuinelyMissingProject` | ✅ PASS |
| GCPC-105 explicit parsing policy matching the .NET provider | comments, trailing comma, BOM accepted | `tests/Csharp2Md.Analysis.Tests/Extraction/ConfigurationDocumentReaderTests.cs:288` `Emit_LineAndBlockComments_YieldsBindingAndNoMalformedDiagnostic` and siblings | ✅ PASS |
| GCPC-106 outside the policy → still diagnosed malformed | diagnosed, no binding | `ConfigurationDocumentReaderTests.cs:347` `Emit_UnterminatedObject_IsStillDiagnosedAndYieldsNoBinding` | ✅ PASS |
| GCPC-107 ambiguous structure → no binding promoted | duplicate key rejected | `ConfigurationDocumentReaderTests.cs:365` `Emit_DuplicateKey_IsDiagnosedAsMalformedAndYieldsNoBinding` | ✅ PASS |

### P2: Determinism, isolation and batch certification (GCPC-108..114)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-108 two runs → byte-identical artifacts | byte-identical | `tests/Csharp2Md.Analysis.Tests/Determinism/WholePackageDeterminismTests.cs:37`. **Independently reproduced live**: two `analyze` runs of the corpus, identical 305-file sets, 0 byte-differing files | ✅ PASS |
| GCPC-109 two absolute paths → byte-identical | byte-identical | `WholePackageDeterminismTests.cs:63` `Analyze_CertificationCorpusFromTwoAbsolutePaths_PublishesByteIdenticalPackage` | ✅ PASS |
| GCPC-110 reversed input / enumeration order → byte-identical | byte-identical | `WholePackageDeterminismTests.cs:97` `Analyze_ReversedSolutionOrder_PublishesByteIdenticalBatchAndComposition` | ✅ PASS |
| GCPC-111 batch keeps each solution's semantics isolated | no cross-contamination | `tests/Csharp2Md.Analysis.Tests/Determinism/BatchIsolationTests.cs:21` `…EveryFactCarriesOnlyItsOwnSolutionIdentity` | ✅ PASS |
| GCPC-112 all required solutions published, compatible, certifiable → certify | certified | `tests/Csharp2Md.Storage.Tests/Validation/BatchValidatorTests.cs:14` `Certify_EveryRequiredSolutionPublishedCompatibleAndCertifiable_ReturnsCertified` | ✅ PASS |
| GCPC-113 otherwise → incomplete scope, reason named, not certified | all three | `BatchValidatorTests.cs:42` and siblings, one test per reason | ✅ PASS |
| GCPC-114 committed packages left unmodified and valid | untouched | `ComposeCommandTests.cs:72` | ✅ PASS |

### P1: Migration completion gate (GCPC-115..120)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-115 objective checklist matching the audit's readiness matrix | six criteria | `tests/Csharp2Md.Analysis.Tests/Readiness/LlmReadinessChecklist.cs:13`, `:342` | ✅ PASS |
| GCPC-116 every criterion PASS on a corpus-built package | six PASS | `LlmReadinessChecklist.cs:342` `Evaluate_CertificationCorpusPackage_EveryPartialOrFailCriterionReportsPassWithEvidence`. The Scale criterion reaches PASS only because `LlmReadinessChecklist.cs:161-168` skips `manifest.json`, which is the **only** over-ceiling artifact in the corpus package (57,903 B) and its largest file. On `fixtures/SyntheticSolution/Acme.Orders` the same evaluator would report FAIL, because two of that package's three over-ceiling artifacts are in no exclusion list | ⚠️ PASS only under Gap 1's exclusion |
| GCPC-117 every audit regression in a versioned fixture under CI | all seven cases | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusTests.cs:10` plus the `CertificationCorpus*Tests` family (entry point, invocation, contract, configuration, excluded assets, ceiling) | ✅ PASS |
| GCPC-118 no mandatory gate requires the eShop clone | gate runs without it | `tests/Csharp2Md.Analysis.Tests/Readiness/LocalCorpusReadinessTests.cs:9` (`Category=LocalCorpus`, filter-excluded). The full gate ran green this session with both clones absent | ✅ PASS |
| GCPC-119 clone present → same checklist evaluated and reported | reported, never a build failure on absence | `LocalCorpusReadinessTests.cs:57` and the dynamic-skip convention above it | ✅ PASS |
| GCPC-120 report migration complete when scenarios, provenance, budgets and validation all hold | all conditions | `LlmReadinessChecklist.cs:344`. The budgets condition is the one Gap 1 falsifies | ⚠️ PASS, conditional on Gap 1 |

**Status**: ❌ Gaps present.

- **114 ✅ PASS** (including GCPC-039, GCPC-048, GCPC-071, GCPC-087 and GCPC-092, all upgraded from
  iteration 1 by F1/F2/F3/F5)
- **4 ❌ GAP**: GCPC-038, GCPC-044 (ceiling clause), GCPC-061, GCPC-069/GCPC-070
- **4 ⚠️ weakened or precision**: GCPC-004 (producer misses the case that fires), GCPC-057
  (self-contradicting manifest), GCPC-031 and GCPC-050 (spec-precision, definitional, unchanged)
- **3 ⚠️ conditional on Gap 1**: GCPC-045, GCPC-116, GCPC-120

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| GCPC-004 | ⚠️ Verified (vacuous) | ⚠️ Partial — live producer exists, unrouted for the family where oversized records occur |
| GCPC-038 | ❌ Needs Fix | ❌ Needs Fix (narrowed: 1 artifact on the corpus, 3 on `Acme.Orders`) |
| GCPC-039 | ⚠️ Partial | ✅ Verified |
| GCPC-044 | ⚠️ Partial | ⚠️ Partial (split clause verified; ceiling clause still exempts `manifest.json`) |
| GCPC-048 | ⚠️ PASS with a documented hole | ✅ Verified |
| GCPC-057 | Verified | ⚠️ Partial — provenance correct, manifest's own top-level axes stale |
| GCPC-061 | Verified | ❌ Needs Fix (deferred `source/` entries publish `byte_size: 0`) |
| GCPC-069 | Verified | ❌ Needs Fix (`analyze` exits `0` regardless of status) |
| GCPC-070 | Verified | ❌ Needs Fix (`analyze` never maps `degraded`/`failed`) |
| GCPC-071 | ⚠️ Partial | ✅ Verified |
| GCPC-087 | ❌ Needs Fix | ✅ Verified |
| GCPC-090 | ✅ Verified | ✅ Verified (regression check clean) |
| GCPC-092 | ❌ Needs Fix | ✅ Verified |
| GCPC-116 | ⚠️ | ⚠️ Partial (PASS only under Gap 1's exclusion) |

---

## Discrimination Sensor

**Skipped — standing project policy** (the user runs Stryker manually and does not want the sensor's
fault-injection pass run by the agent). Recorded in `CLAUDE.md`, `.specs/STATE.md`'s standing
engineering constraints, and every batch's task instructions; skipped for every feature from
`knowledge-taxonomy-contract` onward. No scratch worktree was created and no mutation was injected.

---

## Gate Check

- **Gate command** (Build level, from tasks.md's Gate Check Commands table):
  `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- **Build**: 0 warnings, 0 errors (`TreatWarningsAsErrors` on)
- **Result**: **2063 passed, 0 failed, 0 skipped**
  - Domain 563, Analysis 826, Storage 386, Cli 59, Projection 229
- **Test count before feature** (`524d73e`): 1723
- **Test count at iteration 1**: 2050
- **Test count now**: 2063 — **Delta +13 from F1–F5**, matching the fix-implementers' self-reports
  (F1 +5, F2 +2, F3 +1, F4 +2, F5 +3)
- **Test integrity**: no project decreased. Domain 563 → 563, Analysis 823 → 826, Storage 380 → 386,
  Cli 58 → 59, Projection 226 → 229. No test was deleted or skipped to absorb a fix.
- **Skipped tests**: none. `Category=LocalCorpus` is filter-excluded by the gate itself, as designed
  (GCPC-118); both eShop clones are absent, so those tests would report a named skip.
- **Failures**: none

---

## Edge Cases

- [x] Two dispositions not required by the taxonomy → run `failed`, occurrence and both named —
      `InvocationDispositionTests.cs:12` family, `RunCertifierTests.cs:41`
- [x] Every document in a project excluded → project fact with no documents, counted in the aggregate —
      `CertificationCorpusExcludedAssetTests.cs:39`, `CertificationCorpusDocumentPolicyReportTests.cs:13`
- [ ] **Record larger than the ceiling → own shard, degradation reason recorded, never truncated** —
      own-shard ✅ and never-truncate ✅, proven live: `facts/architecture.70fa.json` on `Acme.Orders`
      holds exactly one Component record at 45,354 B in a shard of its own. **The recorded reason half
      still fails**: that package's `coverage.json` publishes `degradation_reasons: []` on all four
      metrics, because F4 routes only `invokes`/`accesses-data`/`uses-contract` confirmed-relation
      families and an architecture *fact* is unrouted (Fix 9)
- [x] Scenario with no instance → marked not exercised with a reason, never passed —
      `RetrievalScenarioRunnerTests.cs:32` family; live: four relation scenarios report
      `"exercised": false, "reached": false` in `measurements.json`
- [x] `validate` target with no manifest → exit `1`, directory unchanged — `ValidateCommandTests.cs`,
      `PackageValidator.cs:191-194` (`not-a-package`)
- [x] Provenance naming a newer generator → exit `6`, no status reported — `ExitCodeTests.cs:104`;
      verified live alongside the new schema and taxonomy cases
- [x] Unreachable labeled-corpus item → certification fails naming it —
      `LabeledCorpusIntegrityTests.cs:50`
- [x] Solution with no facts → manifest, registry, provenance still published; every metric
      `not_applicable`; status `degraded` — `RunCertifierTests.cs:78`,
      `tests/Csharp2Md.Storage.Tests/Mapping/EmptySnapshotMappingTests.cs`
- [x] Accepted document unreadable → degradation reason, never a silent policy exclusion —
      `DocumentInventoryTests.cs`
- [x] Route proven only by convention → verb published, route unresolved, entry point still published —
      `CertificationCorpusEntryPointTests.cs:54`, `BoundaryPassTests.cs:82`; live: `Index` published as
      an entry point with `missing-route-declaration`

---

## Code Quality

Sampled F1–F5's changed files specifically (new since iteration 1's review):
`src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs`, `Mapping/PublicationPipeline.cs`,
`Mapping/DomainMapper.cs`, `Validation/PackageValidator.cs`,
`src/Csharp2Md.Domain/Registry/TaxonomyVersions.cs`,
`src/Csharp2Md.Analysis/Classification/Passes/RelationPass.cs`,
`src/Csharp2Md.Projection/Guides/RetrievalGuideProjector.cs`,
plus re-spot-checks of `Mapping/ManifestBuilder.cs`, `Mapping/PackagePublisher.cs`,
`src/Csharp2Md.Cli/CommandFactory.cs` and `tests/.../Scale/ScaleInputGenerator.cs`.

| Principle | Status |
| --- | --- |
| Minimum code — no features beyond what was asked | ✅ |
| No abstractions for single-use code | ✅ |
| No unnecessary flexibility | ✅ |
| Only touched files required for the task | ✅ — F4's `PublicationPipeline` touch is outside its stated `Where`, but its Deviation note explains why no listed file could perform the merge, and the applied shape is the narrower of the two it tried |
| Didn't "improve" unrelated code | ✅ — F1's ~30-file test rewrite is genuine fallout from sharding, done through one shared `ShardedFactsReader` helper rather than duplicated merge logic |
| Matches existing patterns/style | ✅ — `EnsureProvenanceCompatible`'s new axis checks reuse the existing `PublicationRejectedException("incompatible-provenance", …)` shape; `RetrievalGuideProjector`'s `HasFamily` fallback runs only after the original exact-match branch, so unsplit wording is byte-unchanged |
| Would a senior engineer approve? | ⚠️ — for the code, yes. Not for keeping `manifest.json` inside three "no artifact over the ceiling" exclusion lists while it is the one artifact over the ceiling (Gap 1), and not for `ManifestBuilder.cs:46`'s `byteSize = 0` placeholder standing in for a real byte size the store already knows (Gap 3) |
| Tests map to acceptance criteria and are non-shallow | ✅ — F5's two substitute unit tests were scrutinised specifically and are faithful: they drive a real `LayoutPlanner.Plan` split, assert the negative and the positive, and assert no absent key is named |
| Spec-anchored outcome check | ⚠️ — three exceptions, all listed as gaps: the `manifest.json` exemption in `CertificationCorpusCeilingTests.cs:20-28`, `LlmReadinessChecklist.cs:161-168` and `ScaleInputGenerator.cs:219-226`; and `ExitCodeTests.cs:258`'s `Assert.True(exitCode == 0)` on an `analyze` whose published status is `degraded` |
| Per-layer Coverage Expectation met | ✅ — domain/unit tests are 1:1 with ACs; the CLI covers every subcommand and every exit code constant, though not every verb's own status mapping (Gap 2) |
| Every test maps to a spec AC, edge case or Done-when | ✅ — no unclaimed tests found in the F1–F5 diff |
| Documented guidelines followed | ✅ — `CLAUDE.md` routing, `TreatWarningsAsErrors` (0 warnings), the multi-csproj gate convention, and the LocalCorpus fixture rule are all honoured |

**Quality observations that are not gaps:**

- F1's `ContributionReader` correction (re-planning with the ceiling published in the package's own
  provenance rather than the unsplit default) is a genuine production fix its deviation note discloses
  honestly, and `compose`'s byte-for-byte reproduction still holds.
- `InvokesPass.ConcreteImplementors` over-matching remains a documented pre-existing deferral; GCPC-018's
  text is satisfied.

---

## Deferred Ideas — independent assessment

| Deferred item | Spec requires closing it here? | Verdict |
| --- | --- | --- |
| `manifest.json` exempted from the ceiling (F1's "state and test their exemption" branch) | **Yes.** GCPC-038 admits no exemption, and the story's Independent Test says "walk every file in the package". The exemption's own stated justification — bounded "by this run's own shard count" — is not a bound: 304 entries → 57,903 B on the corpus, 938 → 174,166 B on `Acme.Orders`, ~190 B per entry, growing with exactly the sharding GCPC-039 mandates | **Disagree with the exemption.** Fix 6 |
| The other five envelope exemptions (registry, coverage, diagnostics, measurements, run-certification) | No — each is genuinely bounded by a fixed metric/reason count, and all five were measured under ceiling on both packages examined | Deferral legitimate |
| `entry_point_coverage` deliberately left unrouted (F4's Deviation) | **Yes, as scoped.** Not routing an architecture-family degradation to `entry_point_coverage` specifically is honest — the planner cannot distinguish `EntryPoint` from `Component` inside the compound family. But the conclusion drawn from it was wrong: rather than being an acceptable corner, that family is precisely where the only oversized record on a versioned fixture lives, so the spec's edge case ("SHALL record a degradation reason") fails in the one case that actually fires | **Disagree with the limitation as scoped.** Fix 9 |
| F5's substitute unit tests in place of a `ScaleInputGenerator` recognition case | No — the substitution is disclosed, and the two tests drive a real planner split rather than a hand-built fixture. They are stronger evidence for the recognition half than the scale fixture would have been, since that fixture's sharded families fall outside GCPC-048's seven kinds | Substitution accepted |
| `ShardWriter` fixed 256-bucket depth | No — no current AC requires a single high-fan-in posting group to split | Deferral legitimate; worth a roadmap note |
| Markdown pages never sharded | **Yes, newly.** Not previously deferred because it was not previously observed: `markdown/component/70fa…md` at 71,495 B is a GCPC-038 violation with no mitigating edge case, and it is a file the guide directs readers to | New; Fix 6 |

---

## Fix Plans

### Fix 6 — The published ceiling is still violated: manifest, Markdown pages, and a solitary oversized fact (Blocker)

- **Requirements**: GCPC-038 (❌), GCPC-044 ceiling clause (partial); weakens GCPC-045, GCPC-116, GCPC-120
- **Root cause**: three separate causes, one requirement.
  1. `manifest.json` is planned by `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs` as a single
     artifact and is never split. Its size is linear in the artifact count (~190 B/entry), which F1's
     sharding multiplied. Live: 57,903 B on `fixtures/CertificationCorpus`, 174,166 B on
     `fixtures/SyntheticSolution/Acme.Orders`, both against `artifact_ceiling_bytes: 32768`.
  2. Markdown pages are never sharded and are in no exclusion list. Live:
     `markdown/component/70fa37fb…ce0.md` = 71,495 B on `Acme.Orders`.
  3. `facts/architecture.70fa.json` = 45,354 B holds exactly one Component record. GCPC-038's edge case
     permits its own shard but requires a recorded degradation reason — see Fix 9.
- **Fix task**:
  - *What*: bound `manifest.json` (for example, a small root index carrying `provenance`, `solution_key`
    and a pointer to sharded manifest parts, so the root stays fixed-size and the parts shard like any
    other family) and bound Markdown page size (split a page, or cap the enumerated members and link to
    the posting that holds the rest). If any artifact genuinely cannot be bounded, amend `spec.md`'s
    GCPC-038 and its Independent Test to state the exemption explicitly rather than encoding it in three
    test files.
  - *Where*: `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs`,
    `src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs`, `src/Csharp2Md.Storage/FactualPackageReader.cs`,
    `src/Csharp2Md.Projection/Markdown/MarkdownProjector.cs`.
  - *Verify*: delete `manifest.json` from the exclusion sets at
    `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusCeilingTests.cs:20-28`,
    `tests/Csharp2Md.Analysis.Tests/Readiness/LlmReadinessChecklist.cs:161-168` and
    `tests/Csharp2Md.Storage.Tests/Scale/ScaleInputGenerator.cs:219-226`; watch all three fail against
    current code. Add a second corpus-level ceiling test over
    `fixtures/SyntheticSolution/Acme.Orders` — it reproduces all three causes today and the corpus
    reproduces only one.
  - *Done when*: a real `analyze` of `fixtures/CertificationCorpus`, of
    `fixtures/SyntheticSolution/Acme.Orders`, and of the `ScaleInputGenerator` input each publish zero
    files over `artifact_ceiling_bytes`, with no exclusion list anywhere.
- **Priority**: Blocker

### Fix 7 — `analyze` exits `0` while publishing a degraded certification (Major)

- **Requirements**: GCPC-069 (❌), GCPC-070 (❌)
- **Root cause**: `src/Csharp2Md.Cli/CommandFactory.cs:147-149` returns only `ExitCodes.PartialComposition`
  or `ExitCodes.Success` and never reads the run-certification status the same invocation just published.
  Every certification exit-code test drives `validate` after rewriting `run-certification.json`
  (`tests/Csharp2Md.Cli.Tests/ExitCodeTests.cs:35`, `:57`), so the `analyze` path is untested — and
  `ExitCodeTests.cs:258` positively asserts `analyze` exits `0` for `Acme.Orders`, whose published status
  is `degraded`.
- **Evidence**: live `analyze` of `fixtures/CertificationCorpus` → `run-certification.json`
  `{"status":"degraded", …}`, process exit **0**. Live `analyze` of
  `fixtures/SyntheticSolution/Acme.Orders` → `degraded`, exit **0**. `validate` over the same corpus
  package correctly exits `3`.
- **Fix task**:
  - *What*: map the published status onto the exit code in `analyze` the way `validate` already does
    (`CommandFactory.cs:300`), with `PartialComposition` taking precedence per GCPC-072.
  - *Where*: `src/Csharp2Md.Cli/CommandFactory.cs`, `tests/Csharp2Md.Cli.Tests/ExitCodeTests.cs`.
  - *Verify*: add exit-code tests that run **`analyze`** (not `validate`) over a fixture whose status is
    `degraded` and assert `3`, and over one whose status is `passed` and assert `0`. Update
    `ExitCodeTests.cs:258`'s helper, which currently pins the defect; that is a correction to an
    assertion that encodes wrong behaviour, not a weakening.
  - *Done when*: no `analyze` invocation returns `0` unless the package it published carries
    `status: passed`.
- **Priority**: Major

### Fix 8 — The manifest misreports cardinality for every deferred artifact, and contradicts itself on version axes (Major)

- **Requirements**: GCPC-061 (❌); GCPC-057 (⚠️)
- **Root cause (a)**: `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs:43-46` sets `byteSize = 0` for any
  `fragment.IsDeferred`, because the transactional store owns the single read of a deferred payload. Live:
  10 of 304 corpus entries and 29 of 938 `Acme.Orders` entries publish `byte_size: 0` for `source/` files
  of 207–2,357 B. `PackageValidator` does not reject it, so `validate` returns success on a manifest that
  misreports ten artifacts. This is audit finding I5 closed for catalogs, postings and Markdown but left
  open on the largest artifact class by count.
- **Root cause (b)**: `ManifestBuilder.cs:65` and `src/Csharp2Md.Storage/Mapping/DomainMapper.cs:120`
  build the manifest's own top-level version axes from `TaxonomyVersions.Initial` (1,1,1,1,1), while
  `ProvenanceDto.Current()` uses `TaxonomyTables.Default.Versions` (2,2,1,2,2). Live: the same
  `manifest.json` declares `schema_version: 1` at the top level and `schema_version: 2` inside
  `provenance`.
- **Fix task**:
  - *What*: (a) take a deferred fragment's byte size from the store after it writes the file, or have the
    store report the written length back to `ManifestBuilder`; (b) source the manifest's top-level axes
    from `TaxonomyTables.Default.Versions`, or remove the duplicated block entirely and let provenance be
    the single declaration.
  - *Where*: `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs`,
    `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`, `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs`,
    `src/Csharp2Md.Storage/Validation/PackageValidator.cs`.
  - *Verify*: extend `tests/Csharp2Md.Storage.Tests/Mapping/ManifestRealCardinalityTests.cs` to walk
    **every** manifest entry of a real corpus package and assert `byte_size` equals the file's length on
    disk, including `source/` entries; add a `PackageValidator` check so a zero byte size on a non-empty
    file is rejected. Add an assertion that the manifest's top-level axes equal its provenance's.
  - *Done when*: no manifest entry publishes a byte size that disagrees with its file, and the manifest
    declares one consistent set of version axes.
- **Priority**: Major

### Fix 9 — The degradation reason for an oversized record is never published (Major)

- **Requirements**: GCPC-004 (⚠️), the GCPC-038/GCPC-039 edge case ("SHALL record a degradation reason")
- **Root cause**: `src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs:234-240`
  (`CoverageMetricForRelation`) routes a `record-exceeds-ceiling` reason only for the confirmed-relation
  families `invokes`, `accesses-data` and `uses-contract`. F4's Deviation note documents
  `entry_point_coverage` as deliberately unrouted because the planner cannot distinguish `EntryPoint`
  from sibling facts inside `facts/architecture`. That is exactly the family where the only oversized
  record on a versioned fixture lives.
- **Evidence**: live `analyze` of `fixtures/SyntheticSolution/Acme.Orders` publishes
  `facts/architecture.70fa.json` = 45,354 B holding **one** Component record — a genuine irreducible
  over-ceiling record — while that package's `coverage.json` publishes `degradation_reasons: []` on all
  four metrics and `run-certification.json` names only unknown-occurrence reasons.
- **Fix task**:
  - *What*: publish a `record-exceeds-ceiling` degradation wherever it occurs. If a compound fact
    family's degradation cannot be attributed to one metric without guessing, attach it to every metric
    the family can feed, or publish it as a run-level reason in `run-certification.json` with its
    affected count — either satisfies the edge case; silence does not.
  - *Where*: `src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs`,
    `src/Csharp2Md.Storage/Mapping/PublicationPipeline.cs`,
    `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`.
  - *Verify*: an end-to-end test over `fixtures/SyntheticSolution/Acme.Orders` (which reproduces the
    oversized Component record today, under the real default ceiling, with no artificial 8-byte ceiling)
    asserting the published package carries a non-empty reason naming `record-exceeds-ceiling` with a
    correct affected count.
  - *Done when*: every artifact published in its own shard because it is irreducible has a published
    degradation reason a consumer can read.
- **Priority**: Major

---

## Summary

**Overall**: ❌ Not Ready

**Spec-anchored check**: 114/120 ACs matched the spec-defined outcome. 4 ❌ GAPs (GCPC-038,
GCPC-044 ceiling clause, GCPC-061, GCPC-069/070), 2 ⚠️ weakened (GCPC-004, GCPC-057), 2 ⚠️
spec-precision gaps (GCPC-031, GCPC-050), 3 ⚠️ conditional on Gap 1 (GCPC-045, GCPC-116, GCPC-120).
**Sensor**: Skipped — standing project policy (user runs Stryker manually); see `CLAUDE.md` and `.specs/STATE.md`
**Gate**: 2063 passed, 0 failed, 0 skipped, 0 warnings

**What F1–F5 closed**, each confirmed by live run rather than by its own test:

- **F2** — a real `analyze` of the corpus now publishes a discrete
  `UnresolvedRecord(kind: uses-contract, cause: NoCandidateFound)` for `OrderShipped`, evidenced by its
  own message-operation observation, and `EngineCertificationRunner.ResolveContract` now reads that
  record instead of inferring from an observation's presence. GCPC-087 and GCPC-092 close.
- **F3** — published provenance now reads `schema 2, taxonomy 2, observation 1, extractor 2,
  classifier 2`, exactly the user-confirmed spec row, and `validate` exits `6` on a newer
  `schema_version` and on a newer `taxonomy_version`, not only a newer `GeneratorVersion`. GCPC-071
  closes.
- **F5** — the guide names all seven relation kinds, recognises a sharded family instead of reporting it
  absent, describes a posting family's bucketing once, and is 3,606 bytes on the corpus. Its two
  substitute unit tests drive a real `LayoutPlanner` split and are faithful proof, not a weaker stand-in.
  GCPC-048 closes.
- **F1** — compound fact families now shard: `facts/structural` into 51 shards, `facts/architecture` into
  13, each within the ceiling. GCPC-039 closes. The two artifacts iteration 1 measured at 83,371 and
  33,684 bytes are gone.
- **F4** — a real production path now routes a `record-exceeds-ceiling` reason with an affected count
  onto a coverage metric, which did not exist before. It does not yet cover the family where the
  oversized record actually occurs.

**No regression was introduced by F1–F5.** The gate rose from 2050 to 2063 with no project decreasing,
no test deleted and none skipped. The corpus package is byte-identical across two runs, every manifest
entry is reachable both ways, `validate` and `compose` still work over a package with no solution
present, and GCPC-090 re-checks clean. The corpus `manifest.json` grew from 47,273 to 57,903 bytes, but
that is the arithmetic consequence of F1's sharding on a file that was already over the ceiling, not a
new defect.

**Issues found**:

1. The published ceiling is still violated — `manifest.json` on both packages examined, plus an
   unsharded 71 KB Markdown page and a 45 KB single-record fact shard on `Acme.Orders`. Three test
   exclusion lists still skip the one artifact that is over (Fix 6).
2. `analyze` exits `0` while publishing certification `degraded`, on both fixtures. Every
   certification exit-code test drives `validate` instead, and one test actively pins the defect
   (Fix 7).
3. Every `source/` manifest entry publishes `byte_size: 0` for a real file, and the manifest declares
   two contradicting sets of version axes (Fix 8).
4. The degradation reason the ceiling edge case requires is never published for the oversized record
   that actually occurs (Fix 9).

**Next steps**: route Fix 6 first — it is the feature's own MVP blocker and the only one that needs
design judgment (how to bound a manifest that must enumerate its own shards). Fix 7 is a three-line
change plus its tests. Fix 8 and Fix 9 are mechanical. Re-verify as iteration 3, the final allowed
round; if gaps remain after it, the loop escalates to the user.

`.specs/STATE.md` is deliberately **not** updated by this report: the verdict is FAIL.
