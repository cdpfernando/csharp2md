# Generator CLI, Projections and Certification Validation

**Date**: 2026-09-11
**Spec**: `.specs/features/generator-cli-projections-certification/spec.md`
**Diff range**: `524d73e..32b938e` (82 commits: T1–T66, hygiene and docs commits, fix commits F1–F5
(iteration 1) and F6–F9 (iteration 2), plus one follow-up test commit)
**Verifier**: independent sub-agent (author ≠ verifier). Five batch workers authored T1–T66; separate
fix-implementer workers authored F1–F9; none of them wrote this report.
**Iteration**: 3 of a bounded 3-iteration fix→re-verify loop (final)

---

## Verdict

**Result**: FAIL — **the fix loop's 3-iteration bound is now exhausted; this escalates to the user.**

**All four gaps iteration 2 routed are genuinely closed.** I re-derived each from a live `analyze` rather
than from the fix tasks' own tests, and each holds:

| Iteration-2 gap | Iteration-3 evidence | Status |
| --- | --- | --- |
| GCPC-038 — artifacts over the declared ceiling | `fixtures/CertificationCorpus`: **0 of 307** files over `artifact_ceiling_bytes: 32768`, with `manifest.json` inside the walk (it is now a root with 2 `manifest-part` pointers). `fixtures/SyntheticSolution/Acme.Orders`: **1 of 944** — `facts/architecture.70fa.json`, 45,354 B, which I parsed and confirmed holds **exactly 1 record**: the amended AC's indivisible-record exemption | ✅ Closed |
| GCPC-057 — self-contradicting version axes | Both packages: manifest top level `schema 2 / taxonomy 2 / observation 1` **equals** its own provenance | ✅ Closed |
| GCPC-061 — `byte_size: 0` on deferred entries | **0 byte-size mismatches and 0 declared-zero entries** across 306 corpus and 943 Acme.Orders entries, `source/` included (10 and 29 respectively) | ✅ Closed |
| GCPC-069 / GCPC-070 — `analyze` exits 0 on `degraded` | `analyze` exits **3** on both fixtures, both publishing `status: degraded` | ✅ Closed |
| GCPC-004 — unrouted degradation reason | Acme.Orders `run-certification.json` carries `record-exceeds-ceiling; affected_count=1; Record 'id1:component;…' (45354 bytes)` | ✅ Closed |

**But re-deriving the full spec independently this round surfaced two further gaps that neither prior
iteration examined.** Both are Major, both are pre-existing (T11/T23/T29, not F6–F9 regressions), and both
fail the evidence-or-zero rule, because the only evidence offered for them asserts an in-memory object or
filters the published set before asserting on it.

1. **Three computed envelopes never reach the package.** `InvocationAccountingReport`,
   `ContractAccountingReport` and `DocumentPolicyReport` are built, carried on `FactualSnapshot`, and
   consumed by `RunCertifier` — then dropped. `src/Csharp2Md.Storage/Mapping/DomainMapper.cs` contains
   **zero** references to any of them, and a live package contains zero hits for
   `recognized_occurrences`, `invocation_accounting`, `contract_accounting`, `exclusion_category`,
   `accepted_count`, `excluded_bytes` or `policy_category`. The exclusion-category vocabulary
   (`external-framework-callable`, `duplicate-edge`) appears in **no published byte**. `spec.md`'s
   "Observability and envelopes" table names all three as envelopes, and GCPC-012, GCPC-016, GCPC-034 and
   GCPC-088 all say **publish**.

2. **A fabricated candidate `invokes` edge.** On the corpus, `relations/candidates.json` holds **two**
   candidates from `OrderQueriesController.GetOrderStatus`: one to the real implementor
   `Certification.Queries.OrderQueries.GetOrderStatus`, and one **to the caller itself**.
   `InvokesPass.ConcreteImplementors` (`src/Csharp2Md.Analysis/Classification/Passes/InvokesPass.cs:303-317`)
   matches on `metadata` + `parameters` + `arity` only — never on whether the candidate's declaring type
   implements the abstract target's type, and never on the return type — so the controller's own
   `GetOrderStatus(Guid) : object` is published as a "concrete implementor" of
   `IOrderQueries.GetOrderStatus(Guid) : string?`. GCPC-018 says one candidate *per concrete implementing
   symbol*; the story's Independent Test says *exactly one* candidate to the concrete implementation. This
   is the same failure class the feature exists to close — an invented edge — on the mirror side of B2's
   fabricated entry point.

Everything else re-verified clean, including a live byte-identical re-run and a live `validate` over a
sharded-manifest package.

---

## Task Completion

| Task range | Status | Notes |
| --- | --- | --- |
| T1–T7 (fixtures) | ✅ Done | See Fix 3 for a reproducibility defect in T7's digest guard |
| T8–T14 (document + config policy) | ⚠️ Partial | T11's own `What` says the policy report is carried "to the package"; it stops at `FactualSnapshot` (Gap 1) |
| T15–T24 (facet, entry capability, dispositions, HTTP) | ⚠️ Partial | T23's candidate resolution publishes a non-implementing symbol as an implementor (Gap 2) |
| T25–T31 (coverage + certification) | ⚠️ Partial | T29's two accounting ledgers are computed but never mapped to the wire (Gap 1) |
| T32–T41 (wire v2, layout, manifest, provenance) | ✅ Done | F6/F8 closed the manifest bound, the real byte sizes and the version axes |
| T42–T47 (labels, guide, scenario runner) | ✅ Done | - |
| T48–T52 (validate, compose, exit codes, CLI options) | ✅ Done | F7 closed `analyze`'s status mapping |
| T53–T56 (engine certification) | ✅ Done | - |
| T57–T62 (postings, security, determinism, batch) | ✅ Done | - |
| T63–T66 (scale, readiness, LocalCorpus, decisions) | ✅ Done | F6 removed the last `manifest.json` exemption from all three ceiling loops |
| F1–F5 (iteration 1) | ✅ Done | Re-confirmed still holding after F6–F9 |
| F6 — bound every artifact under the ceiling | ✅ Done | Verified live on two fixtures, not by its own tests |
| F7 — map certification onto `analyze`'s exit code | ✅ Done | Verified live: exit 3 on both fixtures |
| F8 — real byte sizes + consistent axes | ✅ Done | Verified live over 1,249 manifest entries |
| F9 — degradation reason for an irreducible record | ✅ Done | Verified live in `run-certification.json` |

All 66 T-tasks and all 9 F-tasks carry fully checked `Done when` lists. The only unchecked boxes in
`tasks.md` are the two Definition-of-Done bullets at lines 2151 and 2153, deliberately left for this
Verifier.

**Spec amendment recorded, not silent.** T72's Deviation amended GCPC-038 and its Independent Test to
state the indivisible-single-record exemption explicitly (`git diff 5bacb39..HEAD -- spec.md`). That
exemption already existed in the spec's own Edge Cases list (`spec.md:436`); the amendment made the AC and
the Independent Test agree with it. I accept it as a legitimate, declared deviation rather than an
exemption encoded in test files — which is what iteration 2 objected to and what F6 actually removed.

---

## Spec-Anchored Acceptance Criteria

All 120 GCPC IDs re-derived from `spec.md` this session. Every `file:line` below was located and read in
this session; iteration 2's report was read as context only and **no citation was inherited** — one of its
citations (GCPC-110) proved wrong and is corrected below. Empirical claims come from live runs performed
this session in this worktree:

- `analyze --solution fixtures/CertificationCorpus/CertificationCorpus.slnx` → package
  `s-7e968f867df278de81a0529e094b2729`, 307 files, 306 resolved manifest entries, **exit 3**
- the same corpus analyzed a second time into a separate output root → `diff -r` reports **0 differences**
- `analyze --solution fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx` → package
  `s-4fa15a7cd543552f0b7f8134f9a5beb7`, 944 files, 943 resolved entries, **exit 3**
- `validate --package <corpus package>` → `Certification: degraded`, **exit 3**, a verdict per scenario

### P1: Certified execution with verifiable denominators (GCPC-001..010)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-001 status exactly passed/degraded/failed, never `not_evaluated` | the literal never published | `tests/Csharp2Md.Analysis.Tests/Pipeline/RunCertifierTests.cs:109` `RunCertificationStatus_HasNoNotEvaluatedMember`. Live: both packages publish `"status":"degraded"`; the literal `not_evaluated` occurs in no published byte | ✅ PASS |
| GCPC-002 each metric publishes numerator/denominator/exclusions/unknowns/reasons | all five fields per metric | `tests/Csharp2Md.Analysis.Tests/Pipeline/ValidationAndCoverageStageTests.cs:32` `…EntryPointNumeratorMatchesPublishedEntryPointFacts`. Live corpus `coverage.json`: all four metrics carry `state`, `not_applicable_reason`, `numerator`, `denominator`, `exclusions`, `unknowns`, `degradation_reasons` | ✅ PASS |
| GCPC-003 denominator is the named population, independently enumerable | test re-derives, never reads back | `ValidationAndCoverageStageTests.cs:19` `ExecuteAsync_CertificationCorpus_EntryPointDenominatorMatchesIndependentRecount` (+ 4 sibling recounts). **I re-derived one myself**: `linked_call_coverage.denominator = 14` equals the 10 `invocation` + 4 `object-creation` observation records I counted across the package's 65 observation shards | ✅ PASS |
| GCPC-004 per degradation reason, the affected denominator count | a real reason with a real count | `tests/Csharp2Md.Storage.Tests/Mapping/CoverageDegradationRoutingTests.cs:28` (metric-level route, `affected_count` asserted) and `:96` `Publish_LayoutDegradation_PreservesAnAlreadyFailedCertification`; `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusCeilingTests.cs:122-146`. Live Acme.Orders: `record-exceeds-ceiling; affected_count=1; Record 'id1:component;…' (45354 bytes)` | ✅ PASS — **upgrade from iteration 2** |
| GCPC-005 no recall/precision/derived ratio in the run envelopes | no such field | `ValidationAndCoverageStageTests.cs:101` `CoverageMetric_PublishesNoRecallOrPrecisionField` (reflection over the record). Live: `coverage.json`'s only top-level keys are the four metrics | ✅ PASS |
| GCPC-006 all evaluated, nothing quarantined, no reason → `passed` | `passed` | `RunCertifierTests.cs:16` `Certify_AllMetricsEvaluatedWithNoDegradationNoQuarantineNoUnaccounted_IsPassed`; `CoverageDegradationRoutingTests.cs:78` keeps `passed` when no layout reason fires | ✅ PASS |
| GCPC-007 reason/unknown/unsupported capability → `degraded` | `degraded` | `RunCertifierTests.cs:29` `Certify_AMetricCarriesAnUnknownOccurrence_IsDegraded`. Live corpus: `degraded`, with reasons naming `linked_call_coverage` (4 unknowns), `contract_coverage` (3) and `persistence_coverage` not_applicable | ✅ PASS |
| GCPC-008 quarantine / conflict / undisposed → `failed` | `failed` | `RunCertifierTests.cs:42` `Certify_QuarantinedDerivedFact_IsFailed`; `CoverageDegradationRoutingTests.cs:96` proves a layout degradation never downgrades an already-`failed` run | ✅ PASS |
| GCPC-009 empty population → `not_applicable` with a reason | never a satisfied ratio | `tests/Csharp2Md.Analysis.Tests/Storage/FactualSnapshotTests.cs:195` `CoverageMetric_NotApplicable_RequiresAReason`. Live corpus: `persistence_coverage.state == "not_applicable"` with `not_applicable_reason: "No recognized data-access occurrences were found in the analyzed variants."` | ✅ PASS |
| GCPC-010 every metric `not_applicable` → `degraded`, never `passed` | `degraded` | `RunCertifierTests.cs:79` `Certify_LibraryOnlySolutionWithNoEntryPoints_IsDegradedNeverPassed` | ✅ PASS |

### P1: Complete invocation accounting (GCPC-011..018)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-011 exactly one disposition per occurrence | one of five, never zero or two | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusInvocationTests.cs:134` `…EveryRecognizedInvocationOccurrenceCarriesExactlyOneDisposition` — re-derives the denominator from observations, asserts `ledger.Dispositions.Count` equals it and `ledger.Duplicates` is empty | ✅ PASS |
| GCPC-012 **publish** an invocation-accounting record whose per-disposition totals sum to recognized occurrences | a published record, per disposition | `tests/Csharp2Md.Analysis.Tests/Pipeline/InvocationAccountingTests.cs:86` asserts on `snapshot.InvocationAccounting` — an **in-memory** object. No such record is published: `src/Csharp2Md.Storage/Mapping/DomainMapper.cs` has 0 occurrences of `Accounting`, and the live package has 0 hits for `recognized_occurrences`/`invocation_accounting`. The arithmetic *does* close in a published artifact (`coverage.json` linked_call: 3 + 7 + 4 = 14 = the 14 invocation/object-creation observations I counted), but only as a 3-way collapse, not the five dispositions GCPC-011 names | ❌ GAP (Fix 1) |
| GCPC-013 undisposed occurrence → `failed`, occurrence named | named in the reasons | `InvocationAccountingTests.cs:71` `Build_OccurrenceWithNoDisposition_IsNamedAsUnaccounted` | ✅ PASS |
| GCPC-014 unresolved + frontier overlap counted once | counted exactly once | `InvocationAccountingTests.cs:50` `Build_OccurrenceWithBothUnresolvedRecordAndOpenFrontier_IsCountedOnceInTheExclusiveTotal` | ✅ PASS |
| GCPC-015 no occurrence in two totals | disjoint buckets | `InvocationAccountingTests.cs:24` `Build_OnePerDispositionOccurrenceEach_TotalsSumToRecognizedOccurrences`. Live: coverage's three buckets sum exactly to the denominator, so no occurrence is double counted | ✅ PASS |
| GCPC-016 out-of-scope callable → counted exclusion **carrying a declared exclusion category**, no per-occurrence diagnostic | category present in the publication | Counted ✅ (live `linked_call_coverage.exclusions == 7`) and no per-occurrence diagnostic ✅ (`CertificationCorpusInvocationTests.cs:119-129`; live `diagnostics.json` holds 5 records, none per-occurrence). **The category is not published**: the wire names `external-framework-callable` and `duplicate-edge` (`src/Csharp2Md.Analysis/Pipeline/InvocationAccounting.cs:88-92`) appear in **zero** published files. The cited test `CertificationCorpusInvocationTests.cs:95` asserts the category on the in-memory `ledger.Dispositions`, never on the package; the story's Independent Test requires reading it from published artifacts | ❌ GAP, partial (Fix 1) |
| GCPC-017 non-demonstrable continuation → terminal effect with declared cause | frontier carrying a cause, reachable from the occurrence | `tests/Csharp2Md.Analysis.Tests/Classification/InvokesPassTests.cs:217` `Execute_ReflectionDispatch_EmitsOpenFrontier` and siblings asserting `Frontiers` with a cause. Live: `relations/frontiers.json` holds 3 records and the `disposition:open frontier` scenario reaches its endpoint in `measurements.json` | ✅ PASS (no `Requirement` trait; cited by behaviour) |
| GCPC-018 interface/abstract → **one** candidate per concrete implementing symbol, no confirmed to the interface | exactly one candidate here (one implementor exists) | No-confirmed half ✅ (`CertificationCorpusInvocationTests.cs:86-90`). **Count half fails.** Live `relations/candidates.json` holds **2** `invokes` candidates from `OrderQueriesController.GetOrderStatus`: one to `Certification.Queries.OrderQueries.GetOrderStatus` (correct) and one **to the caller itself** (`container=global::Certification.Api.OrderQueriesController; metadata=GetOrderStatus; type=object`), which implements nothing. Root cause `src/Csharp2Md.Analysis/Classification/Passes/InvokesPass.cs:303-317` — `ConcreteImplementors` matches `metadata`+`parameters`+`arity` only. The cited test (`CertificationCorpusInvocationTests.cs:78-84`) filters candidates to `ProposedTarget.Id.Contains("Certification.Queries")` **before** `Assert.Single`, so it is structurally blind to it; no test anywhere asserts the total published candidate count | ❌ GAP (Fix 2) |

### P1: Proven entry capability (GCPC-019..025)

Live evidence for this whole story: the corpus package's `facts/architecture.*.json` shards hold exactly
**four** `entry_points` records, in `architecture.41/51/55/5c.json`. `ChangeUriPlaceholder` appears in
`facts/structural.0c.json` (as a `Symbol`, correct), in `contains`/`belongs-to`/`executes`/`invokes`
relations and in `catalogs/unknowns.json` — and in **no** `entry_points` array.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-019 `EntryPoint` only with positive entry evidence | promotion predicate requires proven capability | `tests/Csharp2Md.Analysis.Tests/Classification/EntryPointPassTests.cs:134`; `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusEntryPointTests.cs:19`. Live as above | ✅ PASS |
| GCPC-020 not externally reachable → no `EntryPoint` | regardless of declaring type | `EntryPointPassTests.cs:134` `Execute_PrivateHelperOnRecognizedController_DoesNotCreateEntryPoint` — `Assert.Equal(0, result.FactCount)` on a private helper of a `ControllerBase` descendant. Live: the helper is in no `entry_points` array | ✅ PASS |
| GCPC-021 framework-type helper with no entry evidence → no `EntryPoint` | not published | `EntryPointPassTests.cs:134` (the private helper on a recognized controller — the spec's own named case), plus `:91` `Execute_ControllerBaseDescendantWithNoCallableActions_DoesNotCreateEntryPoint` and `:74` `Execute_CallableOnNonControllerNonHandler_DoesNotCreateEntryPoint` | ✅ PASS (no trait; cited by behaviour) |
| GCPC-022 conventional action, no route → `EntryPoint` + missing-route diagnostic | both present | `CertificationCorpusEntryPointTests.cs:54`. Live: `Index` is published and `diagnostics.json` carries `missing-route-declaration` naming it | ✅ PASS |
| GCPC-023 publish the evidence, cited by artifact key and ordinal | citation resolves in the same publication | `CertificationCorpusEntryPointTests.cs:77` `…EveryEntryPointCitesEvidenceResolvableInThePublishedPackage` — for every entry point it resolves the `executes` relation's `derived_from` citations to a real observation by owner + kind + occurrence ordinal in the artifact that kind maps to. Note the citation is an observation-identity citation (kind → artifact key, `occurrence_ordinal` → ordinal), not a literal `{artifact_key, ordinal}` pair on the fact itself | ✅ PASS |
| GCPC-024 undeterminable capability → candidate/unresolved, never confirmed | unresolved record instead | `EntryPointPassTests.cs:165` `Execute_ControllerActionWithNoResolvableOwningComponent_PublishesUnresolvedNotEntryPoint` | ✅ PASS |
| GCPC-025 regression reproduced in the versioned corpus, no eShop clone needed | corpus test, CI-runnable | `CertificationCorpusEntryPointTests.cs:19`; the class carries no `LocalCorpus` trait and the gate ran green this session with both clones absent | ✅ PASS |

### P1: Supported-document policy (GCPC-026..035)

Live evidence: the corpus package holds **10** `source/` artifacts and 10 document facts; no path carries
`.ts`, `.js`, `.map`, an image extension, `.zip`, `package-lock` or `.pfx`; `diagnostics.json` holds
exactly one `unsupported-document` record: *"7 document(s) excluded by the supported-document policy: .js,
.json, .map, .pfx, .png, .ts, .zip."*

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-026 explicit policy naming accepted/conditional/excluded classes | every class decided by category | `tests/Csharp2Md.Analysis.Tests/Inventory/SupportedDocumentPolicyTests.cs` (one test per category); `tests/Csharp2Md.Analysis.Tests/Inventory/DocumentInventoryTests.cs:269` | ✅ PASS |
| GCPC-027 matching document → inventoried, fact, `source/` artifact | all three | `DocumentInventoryTests.cs:88`; `tests/Csharp2Md.Analysis.Tests/Inventory/InventoryStageTests.cs:130`. Live as above | ✅ PASS |
| GCPC-028 non-matching → no fact, no `source/`, no relation, no individual diagnostic | all four absent | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusExcludedAssetTests.cs:39` `…ExcludesEveryExcludedAssetFromStructuralFacts`. Live as above | ✅ PASS |
| GCPC-029 conditional extension admitted only on a declared consumer | admitted iff declared | `tests/Csharp2Md.Analysis.Tests/Classification/ClassifierCapabilityRegistryTests.cs:8` and siblings | ✅ PASS |
| GCPC-030 allowlist admits the listed documents only | no other excluded class admitted | `tests/Csharp2Md.Analysis.Tests/AnalysisRequestAllowlistTests.cs:17` `…AllowlistedTypeScriptFile_IsInventoriedWhileSiblingJavaScriptStaysExcluded` | ✅ PASS |
| GCPC-031 "every analyzed document" means "every accepted document" | a definition, not an observable outcome | no test carries this ID and none can: the AC states a reading convention, not a predicate. Its observable consequences are GCPC-027/028/032, each proven above and live | ⚠️ Spec-precision gap (definitional; unchanged from iterations 1 and 2) |
| GCPC-032 `source/` for every accepted, none for any excluded | exact set equality | `CertificationCorpusExcludedAssetTests.cs:56` `…PublishesNoSourceArtifactForAnyExcludedAsset`. Live: the `source/` set equals the document-fact set, 10 each | ✅ PASS |
| GCPC-033 at most one aggregated exclusion diagnostic | one record naming count and extensions | `CertificationCorpusExcludedAssetTests.cs:94` `…PublishesExactlyOneAggregatedExclusionDiagnostic`. Live: exactly one `unsupported-document` record in a 5-record `diagnostics.json`, naming 7 documents and all 7 extensions | ✅ PASS |
| GCPC-034 publish accepted and excluded count **and byte total per policy category** | per-category counts and bytes in the publication | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusDocumentPolicyReportTests.cs:14` asserts on `CollectWebAssetsPolicyReport()` — an **in-memory** `DocumentPolicyReport`. `src/Csharp2Md.Analysis/Inventory/DocumentPolicyReport.cs:19` reaches `FactualSnapshot.DocumentPolicy` (`Storage/FactualSnapshot.cs:182`) and stops; no Storage mapper reads it. Live: no `accepted_count`, `excluded_bytes` or `policy_category` in any published file. The aggregated diagnostic publishes the **excluded count and extensions only** — no accepted count, no byte totals, no per-category split. T11's own `What` says "carried on the snapshot **to the package**" | ❌ GAP (Fix 1) |
| GCPC-035 no consumed evidence removed by the policy | bindings still promoted | `tests/Csharp2Md.Analysis.Tests/Extraction/ConfigurationDocumentReaderTests.cs:288` and siblings each assert a surviving binding. Live: `.cs`, `.csproj` and `appsettings*.json` retained for all four corpus projects | ✅ PASS (no trait; cited by behaviour) |

### P1: Bounded payloads and measured budgets (GCPC-036..045)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-036 declared budget (tokens and file reads) and byte ceiling published | all three published | `tests/Csharp2Md.Cli.Tests/AnalyzeBudgetAndAllowlistTests.cs:63`, `:112`. Live: the published `retrieval.md:59` reads *"the declared reading budget is exhausted — 100,000 read tokens or 25 file reads for the scenario"*, and provenance publishes `artifact_ceiling_bytes: 32768` | ✅ PASS |
| GCPC-037 ceiling derived from budget × measured ratio, calculation published | derivation reproducible | `tests/Csharp2Md.Storage.Tests/Mapping/CeilingCalculatorTests.cs`; `src/Csharp2Md.Storage/Mapping/CeilingCalculator.cs:50-52` — `round(100000 × 8.192 / 25) = 32768`, matching the live value exactly. Published inputs: the budget (retrieval.md), the ceiling and `token_estimator_id: csharp2md.tokens.bytes-per-token-v1` (provenance). The 8.192 ratio is not stored as a field but is algebraically recoverable from those three | ✅ PASS (ratio derivable, not stored — see Fix 4) |
| GCPC-038 no artifact over the ceiling, except an indivisible single record published alone with a degradation reason | zero avoidable offenders | **Closed by F6+F9.** Corpus: I walked all **307** files against the published `32768` — **0 over**, `manifest.json` included in the walk (now a root with 2 `manifest-part` pointers over `manifest/parts.l00.*`). Acme.Orders: all **944** files walked, **exactly 1 over** — `facts/architecture.70fa.json`, 45,354 B, which I parsed and confirmed holds **exactly 1 record**, with a matching run-level degradation reason. `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusCeilingTests.cs:42-62` (corpus, no manifest exemption) and `:107-146` (Acme.Orders, **no exclusion list at all**, `Assert.Single(offenders)` + `IsSingleRecordShard`) | ✅ PASS — **upgrade from iteration 2's blocker** |
| GCPC-039 every named family splits within the ceiling | each family splits | Live corpus: `facts/structural` → 51 shards, `facts/architecture` → 13, `relations/confirmed/contains` → 45, observations → 65 files, every one under 32,768. `tests/Csharp2Md.Storage.Tests/Mapping/CompoundFamilyShardingTests.cs:14`; `LayoutPlannerShardingTests.cs` | ✅ PASS |
| GCPC-040 split preserves identity, ordering, hash, citation resolvability | all four | `tests/Csharp2Md.Storage.Tests/Mapping/LayoutPlannerTests.cs:14`; `PackagePublisherPlanDrivenTests.cs:14`. Live: `validate` over the corpus package re-resolves every citation and exits on certification status, not on a defect | ✅ PASS |
| GCPC-041 cited ordinal in a split artifact yields the claimed record | citation resolves post-split | `tests/Csharp2Md.Storage.Tests/Mapping/PublishedPackageViewShardAwareTests.cs:13`. Live: every catalog entry carries `{artifact_key, ordinal}` resolving into a shard (e.g. `components-and-deployment-units.json` → `facts/architecture.14.json` ordinal 0) | ✅ PASS |
| GCPC-042 same input → same shard assignment | identical across runs | `tests/Csharp2Md.Storage.Tests/Scale/ScaleInputGenerator.cs:292` `Plan_ScaleInputGeneratedTwice_AssignsEveryRecordToTheSameShard`. **Live: two full corpus analyses, `diff -r` = 0 differences across 307 files**, shard names included | ✅ PASS |
| GCPC-043 no directory keyed by a high-cardinality fact identity | shards are flat files | `ScaleInputGenerator.cs:251-257`. Live: every shard is a flat `family.<hex>.json` inside its family directory; the package's only directories are `catalogs`, `contracts`, `facts`, `manifest`, `markdown`, `observations`, `postings`, `relations`, `source` | ✅ PASS |
| GCPC-044 scale input splits `contains`, `belongs-to`, invocation **and** publishes no artifact over the ceiling | both clauses | Split clause ✅ (`ScaleInputGenerator.cs:243-249`). Ceiling clause ✅ — F6 removed `manifest.json` from that test's exclusion set (`ScaleInputGenerator.cs:217-226`) and the test now resolves the sharded manifest through `ManifestSharder.Resolve` before checking | ✅ PASS — **upgrade from iteration 2** |
| GCPC-045 byte size and token estimate of the largest artifact of each role | both published | `ScaleInputGenerator.cs:269-282` derives both from the manifest and the published estimator. Live: every manifest entry carries `role` + `byte_size`, so the largest per role is present in the publication (`payload` → `postings/outgoing.json` 30,922 B; `manifest-part` → `manifest/parts.l00.0000.json` 32,657 B) and the token estimate follows from the published `token_estimator_id` — but neither is published as a dedicated field, and only two roles exist | ⚠️ Spec-precision gap (derivable, not published as such; unchanged from iteration 2 — see Fix 4) |

### P1: Executable retrieval guide (GCPC-046..055)

Live evidence: the corpus `retrieval.md` is **3,606 bytes**, names all seven relation kinds, and
`measurements.json` records **11** scenarios each carrying `exercised`, `reached`, `files_read`, `hops`,
`bytes`, `tokens`, `relevant_facts` and `noise_records_read` — the costliest being `locate-an-identity` at
4 reads / 20,499 B / 2,502 tokens, far inside the declared 25 reads / 100,000 tokens.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-046 locating an identity directs to a catalog, never a canonical payload | no payload key in the locate section | `tests/Csharp2Md.Projection.Tests/Guides/RetrievalGuideProjectorTests.cs:30` `Project_LocateSection_NamesACatalogAndNeverACanonicalPayloadArtifact` | ✅ PASS |
| GCPC-047 documents bucket selection and ordinal resolution | both, without whole-payload reads | `RetrievalGuideProjectorTests.cs:58`; `:165` `Project_PostingsSection_ShardedOutgoingFamily_DescribesBucketingOnceNotOncePerShard` | ✅ PASS |
| GCPC-048 a path for each of the seven relation kinds, naming the holding artifact | all seven | `RetrievalGuideProjectorTests.cs:79` `Project_RelationsSection_DocumentsAllSevenKinds`, plus `:126` `Project_RelationsSection_ShardedInvokesFamily_IsRecognizedNotReportedAbsent`. Live: all seven kinds listed in the published guide | ✅ PASS |
| GCPC-049 separate paths for candidates, unresolved, frontiers | each with its own artifact | `RetrievalGuideProjectorTests.cs:148`, `:182`. Live: the three `disposition:*` scenarios each reach their endpoint | ✅ PASS |
| GCPC-050 never instruct a full read when an index addresses the record | no full-read instruction | `src/Csharp2Md.Projection/Guides/RetrievalGuideProjector.cs:89`, `:144` emit "without reading a canonical payload in full" and "never the whole canonical payload"; `RetrievalGuideProjectorTests.cs:58`. No test asserts the negative across the whole guide, and "open in full" has no precise testable predicate in the spec | ⚠️ Spec-precision gap (unchanged from iterations 1 and 2) |
| GCPC-051 stopping rule for all five conditions | five rules | `RetrievalGuideProjectorTests.cs:231` `Project_StoppingRules_DocumentsAllFive`. Live: `retrieval.md:59` is rule 5, the exhausted-budget rule | ✅ PASS |
| GCPC-052 executed scenario publishes reads/hops/bytes/tokens/relevant/noise | all six | `tests/Csharp2Md.Storage.Tests/Retrieval/RetrievalScenarioRunnerTests.cs:152`; `tests/Csharp2Md.Cli.Tests/ValidateCommandTests.cs:147`. Live: all six fields present on all 11 `measurements.json` records | ✅ PASS |
| GCPC-053 scenario reaches its endpoint within budget | within 100k tokens / 25 reads | `RetrievalScenarioRunnerTests.cs:33` `Run_FullyPopulatedPackage_EveryDocumentedScenarioIsExercisedAndReachesItsEndpointWithinBudget`. Live: max observed 4 reads / 3,161 tokens | ✅ PASS |
| GCPC-054 every documented path executed automatically, run fails on non-resolution | failure names the offender | `RetrievalScenarioRunnerTests.cs:102` and siblings. Live `validate` printed a verdict for every scenario; the four relation kinds with no corpus instance report `not exercised`, never `reached` | ✅ PASS |
| GCPC-055 guide naming an absent key aborts, naming it | abort + offender named | `RetrievalGuideProjectorTests.cs:248` `ValidateNoAbsentKeys_TextNamingAKeyNotInThePublication_AbortsNamingTheOffender` | ✅ PASS |

### P1: Provenance and manifest cardinality (GCPC-056..062)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-056 generator version + reproducible build identity | both present | `tests/Csharp2Md.Storage.Tests/Wire/ProvenanceDtoTests.cs:7`. Live (both packages, identical): `generator_version: "4.0.0.0"`, `build_identity: "faba0e8a-dcc6-4c…"` | ✅ PASS |
| GCPC-057 all five version axes carried, consistently | five axes, one consistent set | **Closed by F8.** Live (both packages): provenance `schema 2, taxonomy 2, observation 1, extractor 2, classifier 2`, **and the manifest's own top-level `schema_version`/`taxonomy_version`/`observation_schema_version` equal them**. `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs:64-72` now derives both from one `effectiveProvenance`; `DomainMapper.cs:120` uses `TaxonomyTables.Default.Versions`. `tests/Csharp2Md.Storage.Tests/Mapping/ManifestRealCardinalityTests.cs:53-56` asserts the equality on a real corpus package | ✅ PASS — **upgrade from iteration 2** |
| GCPC-058 deterministic parameters: ceiling, policy version, allowlist digest | all three | `tests/Csharp2Md.Analysis.Tests/Inventory/SupportedDocumentPolicyTests.cs:158`. Live: `artifact_ceiling_bytes: 32768`, `document_policy_version: "supported-document-policy/1"`, `allowlist_digest: "e3b0c442…"` | ✅ PASS |
| GCPC-059 no timestamps or durations in manifest/provenance | none | `ProvenanceDtoTests.cs` reflection assertions. Live: provenance's 11 keys carry no timestamp; `measurements.json` records carry `timestamp` and `duration_milliseconds` | ✅ PASS |
| GCPC-060 byte-identical provenance across two runs of the same build | byte-identical | `tests/Csharp2Md.Analysis.Tests/Determinism/WholePackageDeterminismTests.cs:37`. **Live: two corpus runs, `diff -r` reports 0 differences across 307 files** | ✅ PASS |
| GCPC-061 manifest entry carries the artifact's real count and byte size | equal to real content | **Closed by F8.** Live: I compared **every** resolved entry's `byte_size` to the file's real length — **0 mismatches and 0 declared-zero entries** across 306 corpus entries (10 `source/`) and 943 Acme.Orders entries (29 `source/`). `src/Csharp2Md.Storage/FilesystemTransactionalStore.cs:485-511` rebuilds the manifest from written file lengths inside staging before the atomic swap; `src/Csharp2Md.Storage/Validation/PackageValidator.cs:218` now validates on-disk packages **with no deferred exemption**. `ManifestRealCardinalityTests.cs:23`; `tests/Csharp2Md.Cli.Tests/ValidateCommandTests.cs` `Validate_NonEmptySourceDeclaredAsZeroBytes_Exits5AndNamesBothSizes` | ✅ PASS — **upgrade from iteration 2** |
| GCPC-062 every file reachable from the manifest | exact set equality | `tests/Csharp2Md.Storage.Tests/Validation/PackageValidatorManifestChecksTests.cs:12`; `tests/Csharp2Md.Storage.Tests/Mapping/ManifestSharderTests.cs:12`. Live: corpus 307 files = 306 entries + `manifest.json`, **both set differences empty**; Acme.Orders 944 = 943 + 1, likewise. The `manifest-part` shards are themselves manifest entries, so sharding created no unreachable file | ✅ PASS |

### P1: Final CLI surface (GCPC-063..073)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-063 exactly three subcommands | `analyze`, `validate`, `compose` | `tests/Csharp2Md.Cli.Tests/AnalyzeCommandTreeTests.cs:11` `RootCommand_ExposesExactlyTheThreeCertificationVerbs` | ✅ PASS |
| GCPC-064 `validate` opens no solution, loads no project, runs no semantic analysis | dependency-level proof | `tests/Csharp2Md.Cli.Tests/ValidateCommandTests.cs:17` `Validate_DependsOnlyOnStorageAndProjection_NeitherReferencesRoslynOrMSBuildDirectly`; `ComposeCommandTests.cs:113` for the compose half | ✅ PASS |
| GCPC-065 detects all seven defect classes | each class detected | `tests/Csharp2Md.Storage.Tests/Corruption/CorruptedPackageTests.cs:10` (one corrupted package per class); `ValidateCommandTests.cs:170` | ✅ PASS |
| GCPC-066 names artifact key, defect class and offending value | all three | `src/Csharp2Md.Storage/Validation/PackageValidator.cs:104-107` — `"manifest-size-mismatch"` + `$"{entry.Path}: manifest declares {entry.ByteSize} bytes, the artifact is {bytes.Length} bytes."`; `ValidateCommandTests.cs` `Validate_NonEmptySourceDeclaredAsZeroBytes_Exits5AndNamesBothSizes` | ✅ PASS |
| GCPC-067 reports the published status, recomputes nothing | reads, never recomputes | `ValidateCommandTests.cs:60`. **Live**: `validate` printed `Certification: degraded` for a package whose `run-certification.json` says `degraded`, with no solution in reach | ✅ PASS |
| GCPC-068 `compose` reproduces batch artifacts with no solution present | byte-for-byte equal | `tests/Csharp2Md.Cli.Tests/ComposeCommandTests.cs:13` `Compose_WithNoSolutionPresent_ReproducesTheAnalyzeBatchArtifactsByteForByte` — re-verified against the sharded-manifest package by F6 | ✅ PASS |
| GCPC-069 passed → exit `0` | `0` only when `passed` | **Closed by F7.** `tests/Csharp2Md.Cli.Tests/ExitCodeTests.cs:22` `Analyze_PublishedCertification_MapsToItsExitCode("passed", 0)` — drives **`analyze`**, not `validate`; `:74` covers `validate`. `src/Csharp2Md.Cli/CommandFactory.cs:347-394` `PublishedCertificationExitCode` reads the just-published `run-certification.json` through the batch manifest | ✅ PASS — **upgrade from iteration 2** |
| GCPC-070 degraded → `3`, failed → `4` | both, for a completing command | **Closed by F7.** `ExitCodeTests.cs:50` `Analyze_RealDegradedFixture_MapsToThree` runs real `analyze` over `Acme.Orders` and asserts exit 3 plus the published `"degraded"`; the `:22` theory covers `("failed", 4)` and asserts `manifest.json` still exists — the package is still published. **Live, independent of any test: `analyze` exits 3 on both `fixtures/CertificationCorpus` and `fixtures/SyntheticSolution/Acme.Orders`, both publishing `degraded`** | ✅ PASS — **upgrade from iteration 2** |
| GCPC-071 structural corruption → `5`; incompatible provenance or contract version → `6` | both halves | `ExitCodeTests.cs:75` `StructuralCorruption_MapsToFive`; `src/Csharp2Md.Storage/Validation/PackageValidator.cs:152-179` compares `GeneratorVersion`, `SchemaVersion` and `TaxonomyVersion`; `ExitCodeTests.cs:104` for the newer-generator case | ✅ PASS |
| GCPC-072 one unpublished solution → `2`, committed packages unmodified, `2` takes precedence | all three | `ExitCodeTests.cs:177`; `ExitCodeTests.cs:262` `PartialComposition_TakesPrecedenceOverPublishedDegradedStatus` proves the collision directly; `ComposeCommandTests.cs:77` `…LeavesTheCommittedPackageUntouched`. `CommandFactory.cs:147-152` returns `PartialComposition` before consulting the status | ✅ PASS |
| GCPC-073 invalid invocation → `1`, publishes nothing | both | `ExitCodeTests.cs:155` `InvalidInvocation_MapsToOneAndPublishesNothing`; `ExitCodes_ExtendRatherThanRenumberTheExistingThree` (`:292`) pins all seven constants | ✅ PASS |

### P1: Engine certification on labeled corpora (GCPC-074..081)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-074 corpora record source, expected state and rationale | all three per item | `tests/Csharp2Md.Analysis.Tests/Certification/LabeledCorpusIntegrityTests.cs:29` `Read_EveryLabeledItem_CarriesASourceReferenceAndAWrittenRationale`; `fixtures/CertificationCorpus/labels/*.json` | ✅ PASS |
| GCPC-075 labels authored independently of classifier output | resolver never reads `Expected` | `LabeledCorpusIntegrityTests.cs:50`; `tests/Csharp2Md.Analysis.Tests/Certification/EngineCertificationRunner.cs` `Resolve` takes only the label id and the publication | ✅ PASS |
| GCPC-076 positives, negatives and lookalikes per classifier | all three kinds | `LabeledCorpusIntegrityTests.cs:17` `Read_EveryCertifiedArea_CarriesPositiveNegativeAndLookalikeItems` | ✅ PASS |
| GCPC-077 precision and recall per certified area | computed from ground truth | `tests/Csharp2Md.Analysis.Tests/Certification/EngineCertificationRunnerTests.cs:14` `RunAsync_EntryPointArea_MatchesEveryLabelAgainstRealClassifierOutput` | ✅ PASS |
| GCPC-078 thresholds 99/95, 99/90, 99/95, 99/90 | verbatim | `tests/Csharp2Md.Analysis.Tests/Certification/EngineThresholdTests.cs:69` `Evaluate_EveryCertifiedArea_MeetsItsNormativeThreshold` (theory over all four areas) | ✅ PASS |
| GCPC-079 below threshold → fail, naming area, value and failing items | all three named | `EngineThresholdTests.cs:88` `Evaluate_FlippedEntryPointLabel_FailsCertificationAndNamesTheFlippedItem` | ✅ PASS |
| GCPC-080 separate from run certification, no precision/recall in a package | no such field | `EngineCertificationRunnerTests.cs:93` `AnalyzeAsync_CertificationCorpus_PublishesNoPrecisionOrRecallValueInAnyArtifact`. Live: neither string occurs in either published package | ✅ PASS |
| GCPC-081 versioned repository artifact, not a CLI subcommand | report file, three verbs only | `tests/Csharp2Md.Analysis.Tests/Certification/EngineCertificationReportTests.cs:13` `Generate_FromAFreshMeasurement_MatchesTheCommittedReportExactly`; `AnalyzeCommandTreeTests.cs:11` | ✅ PASS |

**Caveat worth recording**: GCPC-018's defect (Fix 2) means the linked-call area's ground truth is being
measured against a classifier that emits a false-positive candidate. `EngineThresholdTests.cs:69` still
passes, so the labeled corpus does not currently contain an item that would catch it — see Fix 2's
`Verify` step.

### P1: Security and fidelity under new projections (GCPC-082..086)

Re-checked after F6's Markdown-truncation and manifest-sharding changes, since both create new published
surfaces.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-082 redaction model preserved | sidecar, both hashes, ordinal-sorted spans | `tests/Csharp2Md.Projection.Tests/Security/SecretAbsenceTests.cs:72` `TheRedactedSidecar_StillDeclaresRedactedTheOrdinalSortedSpansAndBothHashes` | ✅ PASS |
| GCPC-083 no secret literal or individual hash in any published byte | absent everywhere | `SecretAbsenceTests.cs:39` fixture plus the literal and SHA-256 sweeps, with `Assert.All` over **every** published file — so the new `manifest/parts.*` shards and the truncated Markdown pages fall inside the sweep by construction | ✅ PASS |
| GCPC-084 label proven outside a redacted span before publication | no secret-derived label | `tests/Csharp2Md.Projection.Tests/Labels/LabelProjectorTests.cs:140` `For_EntryPoint_WhenSymbolDeclarationIsInsideARedactedSpan_OmitsTypeAndMethodLabels` | ✅ PASS |
| GCPC-085 separate consent for generators, analyzers never run | both | `tests/Csharp2Md.Analysis.Tests/Semantics/TrustBoundaryTests.cs:20` `Strip_ThenGetCompilationAsync_NeverExecutesTheAttachedSourceGenerator` | ✅ PASS |
| GCPC-086 no credential in a coverage/provenance/accounting envelope | absent | `SecretAbsenceTests.cs:111` `CoverageProvenanceAndAccountingEnvelopes_CarryNoCredentialConnectionStringOrToken`. Live: the corpus's provenance, coverage and certification envelopes carry no such value | ✅ PASS |

### P2: Contract and message-operation accounting (GCPC-087..092)

Live: `relations/unresolved.json` holds 9 records, including the two `uses-contract` records with cause
`NoCandidateFound` for `OrderShipped` and `OrderCreated`; `contract_coverage` publishes
`denominator: 3, unknowns: 3`, never a silent zero.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-087 every recognized message operation / payload slot reaches exactly one of four outcomes | one discrete record per item | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusContractTests.cs:16`, `:35`. Live as above | ✅ PASS |
| GCPC-088 **publish** the recognized count alongside the per-outcome counts | a published record whose parts sum | `tests/Csharp2Md.Analysis.Tests/Pipeline/InvocationAccountingTests.cs:105` asserts on `snapshot.ContractAccounting` — an **in-memory** object. `ContractAccountingReport` (`Storage/FactualSnapshot.cs:149-154`) is never mapped by `DomainMapper`, and the live package has 0 hits for `contract_accounting`/`recognized_total`. Mitigated only in part: `contract_coverage` publishes `num 0 + excl 0 + unk 3 = den 3`, which closes arithmetically, but that is the coverage metric — not the per-outcome record with its exclusion categories that the Observability table names | ❌ GAP (Fix 1) |
| GCPC-089 absence of a contract never presented as absence of messaging | no such claim | `tests/Csharp2Md.Projection.Tests/Postings/MessagingContractPostingTests.cs`; `InvocationAccountingTests.cs:105`. Live: `contract_coverage` publishes `denominator: 3, unknowns: 3` and `facts/architecture.*.json` publish 3 messaging `boundary_operations` — messaging stays visible even though no `Contract` fact exists | ✅ PASS |
| GCPC-090 never create a contract from name/structural similarity, path or prefix | no shared contract for the lookalike pair | `CertificationCorpusContractTests.cs:132` `…SameNamedPayloadTypesAreDistinctSymbolsInDifferentProjects`; `fixtures/CertificationCorpus/labels/contracts.json` → `contract-receipt-not-merged`. Live: the corpus publishes **no** `Contract` fact at all, so no lookalike merge occurred | ✅ PASS |
| GCPC-091 proven shared contract → producers and consumers in one posting hop | both in one hop | `MessagingContractPostingTests.cs:41` `Project_MessagingContractWithBothDirectionsBoundAsRequest_ReachesProducerAndConsumerInOneHop`, on a purpose-built messaging fixture because `ContractPass.IsSharedAcrossProjects` (EBC-25) never promotes a same-project handled event. Substitution documented in `context.md` and faithful to the AC's shape | ✅ PASS (substitute fixture accepted) |
| GCPC-092 unproven contract identity → candidate or unresolved with a declared cause | an actual published record | `CertificationCorpusContractTests.cs:35`. Live: two `uses-contract` unresolved records with `cause: NoCandidateFound`, each evidenced by the `message-operation` observation on its publisher | ✅ PASS |

### P2: Legible catalogs and pages (GCPC-093..098)

Live: the corpus publishes 4 catalogs (`entry-points` 4, `boundary-operations` 5,
`components-and-deployment-units` 4, `unknowns` 9) and 13 Markdown pages, every title carrying a linked
label with an `artifact_key` and an ordinal comment — e.g.
`# [GET](facts/architecture.8a.json) <!-- 0 --> [orders/{id}/status](facts/architecture.8a.json) <!-- 0 --> [GetOrderStatus]…`.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-093 catalogs and pages carry compact labels for proven values | component/type/method/protocol/verb/route | `tests/Csharp2Md.Projection.Tests/Catalogs/CatalogProjectorTests.cs:555` and siblings. Live as above | ✅ PASS |
| GCPC-094 every label derived from the payload, citing key and ordinal | citation present and correct | `CatalogProjectorTests.cs:481` `Project_EntryPoints_EntryLabelsResolveToTheCitationsOfTheirProvenValues`. Live: every catalog entry carries `artifact_key` + `ordinal` | ✅ PASS |
| GCPC-095 canonical identity stays the authority | label never authoritative | `CatalogProjectorTests.cs:606` `Project_EntryPoints_OrderingStaysKeyedOnFactIdAndCanonicalIdRemainsTheIdentity`. F6's page truncation preserves this: an over-budget page defers to the posting artifact rather than claiming completeness | ✅ PASS |
| GCPC-096 no page titled by fact type + encoded identity alone | title carries a label | `tests/Csharp2Md.Projection.Tests/Markdown/MarkdownTitleTests.cs:100` `Project_AllSevenPageFamilies_NoTitleConsistsSolelyOfFactTypeAndEncodedIdentity`. Live: all 13 titles are labelled | ✅ PASS |
| GCPC-097 disagreeing label/link/ordinal/locator aborts naming the offender | abort + offender named | `tests/Csharp2Md.Storage.Tests/Validation/ProjectionValidatorLabelTests.cs:28` `Commit_LabelValueAlteredByOneCharacter_AbortsNamingTheOffenderAndLeavesThePriorPackageByteIdentical` | ✅ PASS |
| GCPC-098 unproven value → omit the label, never infer | omitted | `CatalogProjectorTests.cs:536` and siblings. Live: the three messaging boundary operations publish `http_method: null` and `route: null` rather than an inferred verb | ✅ PASS |

### P2: HTTP verb and route as first-class fields (GCPC-099..102)

Live: the corpus's five `boundary_operations` carry `http_method` and `route` as their own fields — the two
HTTP ones publish `"GET"` with `{"value":"widgets/{id}","role":"Route"}` and
`{"value":"orders/{id}/status","role":"Route"}`; the three messaging ones publish `null` for both.

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-099 both proven → published in their own fields | `http_method` and `route` set | `tests/Csharp2Md.Analysis.Tests/Classification/BoundaryPassTests.cs:49` `Execute_RouteDeclarationWithVerbAndTemplate_PublishesBothHttpMethodAndRouteFields`. Live as above | ✅ PASS |
| GCPC-100 the protocol key is not the only means of discovery | fields populated independently | `BoundaryPassTests.cs:50`. Live: `http_method` and `route` sit alongside, not inside, `protocol_operation_key` | ✅ PASS |
| GCPC-101 pages and catalogs present the published values, never synthesized | same values as the payload | `MarkdownTitleTests.cs:43`, `:73`. Live: the two HTTP pages' titles show exactly the `GET` and the route template the payload holds, each linked to `facts/architecture.*.json` at an ordinal | ✅ PASS |
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
| GCPC-108 two runs → byte-identical artifacts | byte-identical | `tests/Csharp2Md.Analysis.Tests/Determinism/WholePackageDeterminismTests.cs:37`. **Independently reproduced live**: two `analyze` runs of the corpus into separate output roots, `diff -r` → **0 differences**, 307 files each | ✅ PASS |
| GCPC-109 two absolute paths → byte-identical | byte-identical | `WholePackageDeterminismTests.cs:64` `Analyze_CertificationCorpusFromTwoAbsolutePaths_PublishesByteIdenticalPackage` | ✅ PASS |
| GCPC-110 reversed input / enumeration order → byte-identical | byte-identical | `tests/Csharp2Md.Analysis.Tests/Composition/BatchDeterminismTests.cs:41` `Analyze_ReversedSolutionOrder_PublishesByteIdenticalBatchAndComposition`. **(Iteration 2 cited `WholePackageDeterminismTests.cs:97`; that file has no such method — citation corrected.)** | ✅ PASS |
| GCPC-111 batch keeps each solution's semantics isolated | no cross-contamination | `tests/Csharp2Md.Analysis.Tests/Determinism/BatchIsolationTests.cs:21` `…EveryFactCarriesOnlyItsOwnSolutionIdentity` | ✅ PASS |
| GCPC-112 all required solutions published, compatible, certifiable → certify | certified | `tests/Csharp2Md.Storage.Tests/Validation/BatchValidatorTests.cs:14` `Certify_EveryRequiredSolutionPublishedCompatibleAndCertifiable_ReturnsCertified` | ✅ PASS |
| GCPC-113 otherwise → incomplete scope, reason named, not certified | all three | `BatchValidatorTests.cs:42` and siblings, one test per reason; `ComposeCommandTests.cs:77` | ✅ PASS |
| GCPC-114 committed packages left unmodified and valid | untouched | `ComposeCommandTests.cs:77` `Compose_PackageMissingFromTheOutputRoot_YieldsIncompleteScopeAndLeavesTheCommittedPackageUntouched` | ✅ PASS |

### P1: Migration completion gate (GCPC-115..120)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| GCPC-115 objective checklist matching the audit's readiness matrix | six criteria | `tests/Csharp2Md.Analysis.Tests/Readiness/LlmReadinessChecklist.cs:18-26` (the six `ReadinessCriterion` values), `:342` | ✅ PASS |
| GCPC-116 every criterion PASS on a corpus-built package | six PASS | `LlmReadinessChecklist.cs:342` `Evaluate_CertificationCorpusPackage_EveryPartialOrFailCriterionReportsPassWithEvidence`, green in this session's gate. **The Scale criterion no longer depends on an exemption**: F6 removed `manifest.json` from `LlmReadinessChecklist.cs:158-165`; its five remaining exclusions are fixed one-per-package envelopes, and I confirmed live that the largest of them (`contracts/taxonomy-registry.json`, 10,184 B) is comfortably under the ceiling and that **0 of 307** corpus files exceed it. The evaluator allows **no** indivisible-record exemption, so it is stricter than the amended GCPC-038 | ✅ PASS — **upgrade from iteration 2's conditional** |
| GCPC-117 every audit regression in a versioned fixture under CI | all seven cases | `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusTests.cs:10` plus the `CertificationCorpus*Tests` family (entry point, invocation, contract, configuration, excluded assets, ceiling, document policy). See Gate Check and Fix 3 for a reproducibility defect in `SyntheticSolutionImmutabilityTests` that does not affect the regression cases themselves | ✅ PASS (with the fixture-digest caveat) |
| GCPC-118 no mandatory gate requires the eShop clone | gate runs without it | `tests/Csharp2Md.Analysis.Tests/Readiness/LocalCorpusReadinessTests.cs:9` carries `Category=LocalCorpus` and is filter-excluded. The full gate ran green this session with both clones absent | ✅ PASS |
| GCPC-119 clone present → same checklist evaluated and reported | reported, never a build failure on absence | `LocalCorpusReadinessTests.cs:57` and the dynamic-skip convention above it. The clone-present half is not exercisable here — both clones are absent from this worktree, which per `.specs/STATE.md:245` is the specified and expected state | ✅ PASS (absence half exercised; presence half not reachable here) |
| GCPC-120 report migration complete when scenarios, provenance, budgets and validation all hold | all conditions | `LlmReadinessChecklist.cs:344`. The budgets condition iteration 2 falsified is now satisfied (GCPC-038 above). The completion claim nevertheless rests on stories with open gaps — GCPC-012/016/018/034/088 | ⚠️ Partial — budgets condition resolved; conditional on Fixes 1 and 2 |

**Status**: ❌ Gaps present.

- **111 ✅ PASS**, including GCPC-004, GCPC-038, GCPC-044, GCPC-057, GCPC-061, GCPC-069, GCPC-070 and
  GCPC-116 — all upgraded from iteration 2 by F6–F9, each re-derived here from a live run
- **5 ❌ GAP**: GCPC-012, GCPC-016 (partial), GCPC-034 and GCPC-088 share one root cause (Fix 1);
  GCPC-018 is separate (Fix 2)
- **4 ⚠️ precision / partial**: GCPC-031 and GCPC-050 (definitional, unchanged from both prior
  iterations), GCPC-045 (derivable, not published as such), GCPC-120 (conditional on Fixes 1 and 2)

---

## Discrimination Sensor

**Skipped — standing project rule, user runs Stryker manually.**

Recorded in `CLAUDE.md`, in `.specs/STATE.md:244` ("The tlc-spec-driven discrimination sensor remains
skipped; the user runs Stryker manually. All other verifier steps remain required when feature execution
begins."), and in this feature's own `tasks.md:9` and `tasks.md:2307-2308`. It has been skipped for every
feature from `knowledge-taxonomy-contract` onward.

No scratch worktree was created and no mutation was injected. **No mutation result is reported, because
none was attempted** — this is a deliberate skip, not a zero-mutation run. Every other Verifier step
(spec-anchored coverage check, gate check, code-quality check, edge cases) ran as documented.

**Sensor depth**: n/a (skipped)
**Result**: n/a (skipped)

Worth recording for the user's manual Stryker pass: both gaps found this round are exactly the kind a
mutation run would surface. Gap 2 survives because the assertion filters the population before counting
it; Gap 1 survives because nothing asserts on the published bytes at all.

---

## Interactive UAT Results

Not performed. This is a backend generator and a scripted CLI with no interactive surface; per
`validate.md` step 3, automated checks are sufficient for infrastructure work. The CLI's observable
contract (exit codes, stdout summary) is covered by `tests/Csharp2Md.Cli.Tests` and by the four live
invocations recorded above.

---

## Code Quality

Files reviewed this round: the full F6–F9 production diff —
`src/Csharp2Md.Storage/Mapping/ManifestSharder.cs` (new), `Mapping/ManifestBuilder.cs`,
`Mapping/LayoutPlanner.cs`, `Mapping/PublicationPipeline.cs`, `Mapping/DomainMapper.cs`,
`Mapping/PackagePublisher.cs`, `FilesystemTransactionalStore.cs`, `FactualPackageReader.cs`,
`Validation/PackageValidator.cs`, `src/Csharp2Md.Projection/Markdown/MarkdownProjector.cs`,
`PackageProjector.cs`, `src/Csharp2Md.Cli/CommandFactory.cs` — plus the new and changed tests
(`CertificationCorpusCeilingTests.cs`, `ManifestSharderTests.cs`, `ManifestRealCardinalityTests.cs`,
`CoverageDegradationRoutingTests.cs`, `ExitCodeTests.cs`, `ScaleInputGenerator.cs`,
`LlmReadinessChecklist.cs`), and the pre-existing
`src/Csharp2Md.Analysis/Classification/Passes/InvokesPass.cs` and
`src/Csharp2Md.Analysis/Pipeline/InvocationAccounting.cs` reached through the two gaps.

| Principle | Status |
| --- | --- |
| Minimum code — no features beyond what was asked | ✅ `ManifestSharder` is 173 lines and does one thing; F9 reused `plan.DegradationReasons` instead of changing `LayoutPlanner`'s routing, as its Deviation states |
| No abstractions for single-use code | ✅ `LayoutPlan.CeilingBytes` is a carried value, not a new abstraction; `ManifestSharder.Resolve` is shared by all four readers of a published manifest rather than copied into each |
| No unnecessary flexibility | ✅ |
| Only touched files required for the task | ✅ F6 touched `PackageProjector.cs` and `PackagePublisher.cs` outside its stated `Where`; both are one-line call-site changes made necessary by the new parameters, not scope creep |
| Didn't "improve" unrelated code | ✅ `ManifestBuilder.CountTopLevelEntries` was restructured, but the added `ArgumentException` catch is a real fix (`JsonNode` defers duplicate-property validation to enumeration) and behaviour for every other input is unchanged |
| Matches existing patterns/style | ✅ The `PublicationRejectedException(code, offender)` shape is reused for `record-exceeds-ceiling`, `manifest-part-cycle` and `manifest-file-missing`; `ManifestSharder.ToFragments` returns the byte-identical unsplit fragment when the manifest fits, so every small package's bytes are unchanged |
| Would a senior engineer approve? | ⚠️ For F6–F9, yes — this round's production changes are careful, well-commented and conservative. **Two reservations, both outside the fix scope**: `InvokesPass.ConcreteImplementors` calls a name-and-parameter match "concrete implementors" and publishes the result as fact (Gap 2); and three envelopes are computed, carried on the snapshot and then silently dropped at the Storage boundary with no compile-time or test-time signal (Gap 1). One minor note on `ManifestSharder.cs:45-48`: when the manifest's fixed envelope alone exceeds the ceiling it falls back to the unsplit form, which can publish an over-ceiling manifest under an artificially tiny test ceiling. The comment discloses this and the real derived ceiling never triggers it |
| Tests map to acceptance criteria and are non-shallow | ⚠️ Mostly yes — `CertificationCorpusCeilingTests.AnalyzeAsync_AcmeOrders_…` is a model strong assertion: no exclusion list, walks every file, `Assert.Single`, then parses the offender to prove it is an irreducible singleton and requires a run-level reason naming its family. **But `CertificationCorpusInvocationTests.cs:78-84` narrows the candidate set by target project before `Assert.Single`**, which is what let Gap 2 stand through two Verifier rounds |
| Spec-anchored outcome check: each test's asserted value matches the spec-defined outcome | ❌ Four exceptions, all listed as gaps: `InvocationAccountingTests.cs:86` and `:105` and `CertificationCorpusDocumentPolicyReportTests.cs:14` assert on in-memory objects where the AC says *publish*; `CertificationCorpusInvocationTests.cs:78-84` asserts over a filtered subset where the AC bounds the whole set |
| Per-layer Coverage Expectation met | ✅ Domain/unit tests are 1:1 with ACs; the CLI now covers every subcommand **and every verb's own status mapping** (F7's gap closed) |
| Every test maps to a spec requirement — no unclaimed tests | ✅ No unclaimed tests found in the F6–F9 diff. 115 of the 120 IDs are referenced by name in `tests/`; the 5 that are not (GCPC-017, 021, 031, 035, 050) are each cited by behaviour or flagged as definitional above |
| Documented guidelines followed | ✅ `CLAUDE.md` routing, `TreatWarningsAsErrors` (0 warnings), the multi-csproj gate convention, `net10.0`, no `Microsoft.Build.*` reference and no `MSBuildLocator.RegisterDefaults()` (AD-003), and the LocalCorpus fixture rule are all honoured |

---

## Edge Cases

- [x] Two dispositions not required by the taxonomy → run `failed`, occurrence and both named —
      `InvocationDispositionTests.cs:12` family; `InvocationAccountingTests.cs:71`
- [x] Every document in a project excluded → project fact with no documents, counted in the aggregate —
      `CertificationCorpusExcludedAssetTests.cs:39`; live: `Certification.WebAssets` publishes its project
      fact with 2 accepted documents, its 7 excluded ones folded into the single aggregated diagnostic
- [x] **Record larger than the ceiling → own shard, degradation reason recorded, never truncated** — all
      three halves now hold. Live on `Acme.Orders`: `facts/architecture.70fa.json` is 45,354 B, I parsed
      it and it holds **exactly one** record, it is untruncated, and `run-certification.json` carries
      `record-exceeds-ceiling; affected_count=1; Record 'id1:component;…' (45354 bytes)`.
      `CertificationCorpusCeilingTests.cs:122-146`; `CoverageDegradationRoutingTests.cs:56-65`
- [x] Scenario with no instance → marked not exercised with a reason, never passed —
      `RetrievalScenarioRunnerTests.cs:33` family; live `validate` printed `not exercised` for the four
      relation kinds the corpus has no instance of, and `measurements.json` records
      `"exercised": false, "reached": false` for each
- [x] `validate` target with no manifest → exit `1`, directory unchanged — `ValidateCommandTests.cs`;
      `PackageValidator.cs:189-193` (`not-a-package`)
- [x] Provenance naming a newer generator → exit `6`, no status reported — `ExitCodeTests.cs:104`;
      `PackageValidator.cs:152-179`
- [x] Unreachable labeled-corpus item → certification fails naming it —
      `LabeledCorpusIntegrityTests.cs:50`
- [x] Solution with no facts → manifest, registry and provenance still published; every metric
      `not_applicable`; status `degraded` — `RunCertifierTests.cs:79`;
      `tests/Csharp2Md.Storage.Tests/Mapping/EmptySnapshotMappingTests.cs`
- [x] Accepted document unreadable → degradation reason, never a silent policy exclusion —
      `DocumentInventoryTests.cs`
- [x] Route proven only by convention → verb published, route unresolved, entry point still published —
      `CertificationCorpusEntryPointTests.cs:54`; `BoundaryPassTests.cs:82`; live: `Index` is one of the
      four published entry points and `diagnostics.json` carries `missing-route-declaration` for it

---

## Gate Check

- **Gate command** (Build level, from `tasks.md`'s Gate Check Commands table, run per-project because
  multi-csproj `dotnet test` hits MSB1008):

  ```
  dotnet build csharp2md.slnx -clp:ErrorsOnly -v:quiet
  dotnet test tests/Csharp2Md.<Domain|Analysis|Storage|Cli|Projection>.Tests/… --filter "Category!=LocalCorpus" -v:quiet
  ```

- **Build**: 0 warnings, 0 errors (`TreatWarningsAsErrors` on)
- **Result**: **2072 passed, 0 failed, 0 skipped**
  - Domain 563, Analysis 827, Storage 389, Cli 64, Projection 229
- **Test count before feature** (`524d73e`): 1723 — **Delta +349**
- **Test count at iteration 1**: 2050 · **at iteration 2**: 2063 · **now**: 2072 (**+9 from F6–F9**,
  matching the fix-implementers' self-reports: F6 +2, F7 +4, F8 +2, F9 +1)
- **Test integrity**: no project decreased. Domain 563 → 563, Analysis 826 → 827, Storage 386 → 389,
  Cli 59 → 64, Projection 229 → 229. No test was deleted or skipped to absorb a fix. Three assertions were
  **strengthened**: `CertificationCorpusCeilingTests` and `ScaleInputGenerator` both lost their
  `manifest.json` exemption, and `LlmReadinessChecklist.EvaluateScale` lost it too
- **Skipped tests**: none. `Category=LocalCorpus` is filter-excluded by the gate itself, as designed
  (GCPC-118); both eShop clones are absent from this worktree, so per `.specs/STATE.md:245` those tests
  are correctly not run and correctly not failed
- **Failures**: none

**Environment note — a real reproducibility defect, not a feature defect (see Fix 3).** My first gate run
in this fresh worktree reported 11 failures. Both causes were environmental, and I neutralised both before
the recorded run:

1. The fixture projects were not NuGet-restored (no `obj/project.assets.json`), so MSBuildWorkspace
   resolved no metadata references and six Acme.Orders classification tests found no facts. Fixed by
   `dotnet restore` per fixture csproj. `Acme.Broken` and the `Acme.Orders.slnx` entry point cannot
   restore **by design** (a deliberately broken SDK reference and a deliberately missing project), which
   is correct fixture behaviour.
2. `SyntheticSolutionImmutabilityTests` (T7, GCPC-117) hashes raw file bytes. The committed
   `SyntheticSolutionManifest.json` is a **mixed CRLF/LF snapshot** of one particular working tree: 17 of
   its 41 entries are the CRLF form and 24 are the LF form. With `core.autocrlf=true` (this machine's
   setting) a fresh checkout produced 24 mismatches; with `core.autocrlf=false`, the complementary 17.
   **Neither setting reproduces the committed digest from a clean checkout**, so this guard passes only in
   a tree whose line endings happen to match the author's. I reconstructed the exact expected bytes from
   the committed manifest — the digest then matched
   `40dd18b021d265ff9a94f1b99523770848a9cd6dc10bc6fccf321fd65d078c86` exactly — and ran the recorded gate
   against that state.

---

## Fix Plans

Ranked. **The 3-iteration fix→re-verify bound is now exhausted**, so per `validate.md` step 8 these are
escalated to the user rather than routed automatically: the user decides whether to authorise a fourth
round, accept the gaps with a recorded spec amendment, or split them into a follow-up feature.

### Fix 1 (Major): publish the three computed envelopes

- **Requirements**: GCPC-012 (❌), GCPC-016 (⚠️ partial), GCPC-034 (❌), GCPC-088 (❌); unblocks GCPC-120
- **Root cause**: `InvocationAccountingReport` and `ContractAccountingReport`
  (`src/Csharp2Md.Analysis/Storage/FactualSnapshot.cs:135-154`) and `DocumentPolicyReport`
  (`src/Csharp2Md.Analysis/Inventory/DocumentPolicyReport.cs:19`) are built, carried on `FactualSnapshot`
  and consumed only by `RunCertifier`. `src/Csharp2Md.Storage/Mapping/DomainMapper.cs` contains zero
  references to any of them, so nothing crosses the Storage boundary. The exclusion-category vocabulary
  (`external-framework-callable`, `duplicate-edge`,
  `src/Csharp2Md.Analysis/Pipeline/InvocationAccounting.cs:88-92`) therefore appears in no published byte.
- **Fix task**:
  - *What*: map all three onto the wire and publish them — either as their own envelopes
    (`invocation-accounting.json`, `contract-accounting.json`, `document-policy.json`) or as named blocks
    inside `coverage.json`. Each must carry the recognized total and the per-disposition / per-outcome
    totals; the two accounting reports must carry the exclusion **categories** with their counts; the
    document-policy report must carry accepted and excluded **counts and byte totals per category**.
  - *Where*: `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`,
    `src/Csharp2Md.Storage/Mapping/PublicationPipeline.cs`, `src/Csharp2Md.Storage/Wire/` (new DTOs),
    `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs` (so the new artifacts become manifest entries and
    stay under the ceiling).
  - *Verify*: a test that runs a real `analyze` of `fixtures/CertificationCorpus`, reads the **published**
    artifacts from disk, and asserts (a) the invocation-accounting totals equal the recognized occurrence
    count re-derived from the published observation shards — live today that is `10 invocation +
    4 object-creation = 14`; (b) both framework calls appear as counted exclusions carrying
    `external-framework-callable`; (c) the contract-accounting totals close against the published
    message-operation observations and messaging boundary operations; (d) the document-policy report's
    accepted and excluded counts and bytes match the corpus's known contents (2 accepted / 988 B and
    7 excluded for `Certification.WebAssets`, per `CertificationCorpusDocumentPolicyReportTests.cs:14-64`).
    Replace the in-memory assertions at `InvocationAccountingTests.cs:86`, `:105` and
    `CertificationCorpusDocumentPolicyReportTests.cs:14` with published-artifact assertions, or add
    published-artifact siblings.
  - *Done when*: GCPC-012, GCPC-016, GCPC-034 and GCPC-088 each trace to a `file:line` assertion over
    bytes read back from a published package; `grep -r external-framework-callable <package>` returns a
    hit; every new artifact is a manifest entry with a real count and byte size and is under the ceiling;
    the full gate stays green with the count reported.
- **Priority**: Major

### Fix 2 (Major): stop publishing a non-implementing symbol as a concrete implementor

- **Requirements**: GCPC-018 (❌); touches the engine-certification linked-call area (GCPC-077, GCPC-078)
- **Root cause**: `src/Csharp2Md.Analysis/Classification/Passes/InvokesPass.cs:303-317`.
  `ConcreteImplementors` selects any callable, non-abstract symbol in the solution whose signature
  `metadata`, `parameters` and `arity` fields match the abstract target's. It never checks that the
  candidate's declaring type implements or derives from the abstract target's declaring type, and it
  ignores the return type. On the corpus, `Certification.Api.OrderQueriesController.GetOrderStatus(Guid)
  : object` therefore matches `Certification.Api.IOrderQueries.GetOrderStatus(Guid) : string?` and is
  published as an implementor — a candidate `invokes` edge from the action **to itself**.
- **Evidence**: live `relations/candidates.json` holds 2 `invokes` candidates from that action; the second
  has source id == proposed-target id. `fixtures/CertificationCorpus/Certification.Api/InvocationChain.cs:44-63`
  shows `OrderQueriesController` implements nothing.
- **Fix task**:
  - *What*: require positive evidence of the implements/overrides relationship before proposing a
    candidate — gate on a published `base-type` observation (the corpus already emits 5) or on an
    implements relation linking the candidate's declaring type to the abstract target's declaring type —
    and include the return type in the signature match. Where the relationship cannot be proven, the
    occurrence must fall to an unresolved record or an open frontier with a declared cause (CLLF-08,
    CLLF-11), never to a candidate — GCPC-024's "never publish it as confirmed" discipline applied to
    candidates.
  - *Where*: `src/Csharp2Md.Analysis/Classification/Passes/InvokesPass.cs`,
    `tests/Csharp2Md.Analysis.Tests/Fixtures/CertificationCorpusInvocationTests.cs`,
    `tests/Csharp2Md.Analysis.Tests/Classification/InvokesPassTests.cs`.
  - *Verify*: remove the `ProposedTarget.Id.Contains("Certification.Queries")` pre-filter at
    `CertificationCorpusInvocationTests.cs:78-82` and assert over the **whole** published
    `relations/candidates.json`: exactly one `invokes` candidate whose source is
    `OrderQueriesController.GetOrderStatus`, targeting `Certification.Queries.OrderQueries.GetOrderStatus`.
    Add a unit test in `InvokesPassTests` for a same-named, same-parameter method on a type that does
    **not** implement the interface, asserting no candidate is produced. Consider adding a same-named
    lookalike to `fixtures/CertificationCorpus` and a matching labeled item, mirroring the
    `contract-receipt-not-merged` lookalike that already guards GCPC-090.
  - *Done when*: the corpus publishes exactly one candidate from that action; no test filters the
    candidate population before counting it; GCPC-018 traces to an assertion over the full published set;
    the linked-call precision measurement (`EngineThresholdTests.cs:69`) still meets its 0.99 threshold;
    the full gate is green with the count reported.
- **Priority**: Major

### Fix 3 (Minor): make the SyntheticSolution immutability digest reproducible from a clean checkout

- **Requirements**: GCPC-117 (guard reliability), and the standing determinism constraint
- **Root cause**: `tests/Csharp2Md.Analysis.Tests/Fixtures/SyntheticSolutionImmutabilityTests.cs:64`
  hashes raw file bytes, and the committed `SyntheticSolutionManifest.json` was generated from a working
  tree with mixed line endings (17 CRLF entries, 24 LF). With `core.autocrlf=true` a fresh checkout gives
  24 mismatches; with `core.autocrlf=false`, 17. The guard therefore passes only where it was authored —
  it would fail in CI and in any fresh clone or worktree, which is the opposite of the reproducibility
  GCPC-108..110 exist to guarantee.
- **Fix task**:
  - *What*: either (a) normalize line endings before hashing (`bytes.Replace("\r\n", "\n")`) and
    regenerate the manifest and digest, or (b) pin the fixture tree's line endings with a `.gitattributes`
    rule (`fixtures/SyntheticSolution/** text eol=lf`, matching the existing `contracts/` rules) and
    regenerate. Option (b) also stabilises every locator span asserted against those bytes across
    platforms, which (a) does not.
  - *Where*: `.gitattributes`,
    `tests/Csharp2Md.Analysis.Tests/Fixtures/SyntheticSolutionImmutabilityTests.cs`,
    `tests/Csharp2Md.Analysis.Tests/Fixtures/SyntheticSolutionManifest.json`.
  - *Verify*: `git worktree add` into a clean directory under both `core.autocrlf=true` and
    `core.autocrlf=false`, `dotnet restore` the fixture projects, then run the Analysis suite — green in
    both. Consider a short "restore the fixtures first" note in `tasks.md`'s Gate Check section, since six
    other tests silently depend on it.
  - *Done when*: the digest test passes from a clean checkout under either `core.autocrlf` setting, and
    the six Acme.Orders classification tests that depend on those fixture bytes pass with it.
- **Priority**: Minor (test-infrastructure reproducibility; no published-package behaviour is affected)

### Fix 4 (Cosmetic): publish the ceiling ratio and the largest-artifact-per-role measurement explicitly

- **Requirements**: GCPC-037 (⚠️), GCPC-045 (⚠️)
- **Root cause**: GCPC-037's `bytesPerToken` (8.192,
  `src/Csharp2Md.Storage/Mapping/CeilingCalculator.cs:39`) and GCPC-045's largest-artifact byte size and
  token estimate are recoverable from the publication but never written as fields; `measurements.json`
  holds only the 11 retrieval-scenario records.
- **Fix task**:
  - *What*: add `bytes_per_token`, `reading_budget_tokens` and `max_file_reads_per_scenario` to
    `ProvenanceDto`, and add one `largest-artifact:<role>` measurement record per role carrying its byte
    size and token estimate.
  - *Where*: `src/Csharp2Md.Storage/Wire/ProvenanceDto.cs`,
    `src/Csharp2Md.Storage/Mapping/PublicationPipeline.cs`.
  - *Verify*: a live-package test asserting the ceiling re-derives exactly from the three published
    inputs, and that a `largest-artifact:payload` record exists whose byte size equals the largest payload
    entry's `byte_size` (live today: `postings/outgoing.json`, 30,922 B).
  - *Done when*: GCPC-037 and GCPC-045 trace to published fields rather than to derivations.
- **Priority**: Cosmetic

---

## Requirement Traceability Update

| Requirement | Previous Status (in `spec.md` at `32b938e`) | New Status |
| --- | --- | --- |
| GCPC-004 | Verified | ✅ Verified — confirmed live, `affected_count=1` on the real oversized record |
| GCPC-012 | Verified | ❌ Needs Fix — no invocation-accounting record is published (Fix 1) |
| GCPC-016 | Verified | ⚠️ Partial — counted and undiagnosed ✅; the declared exclusion category is not published (Fix 1) |
| GCPC-018 | Verified | ❌ Needs Fix — a second, non-implementing candidate is published (Fix 2) |
| GCPC-034 | Verified | ❌ Needs Fix — per-category counts and bytes are not published (Fix 1) |
| GCPC-038 | Verified | ✅ Verified — 0 of 307 corpus files over ceiling; 1 of 944 on Acme.Orders, an irreducible singleton with its reason |
| GCPC-044 | Verified | ✅ Verified — both clauses; the last `manifest.json` exemption is gone |
| GCPC-045 | Verified | ⚠️ Spec-precision gap — derivable from the manifest, not published as such (Fix 4) |
| GCPC-057 | Verified | ✅ Verified — manifest top-level axes equal provenance on both fixtures |
| GCPC-061 | Verified | ✅ Verified — 0 mismatches across 1,249 entries, `source/` included |
| GCPC-069 | Verified | ✅ Verified — `analyze` maps the published status; live exit 3 |
| GCPC-070 | Verified | ✅ Verified — `degraded`→3 and `failed`→4 both asserted on `analyze` |
| GCPC-088 | Verified | ❌ Needs Fix — no contract-accounting record is published (Fix 1) |
| GCPC-110 | Verified | ✅ Verified — citation corrected to `BatchDeterminismTests.cs:41` |
| GCPC-116 | ⚠️ Partial (Scale criterion exempts the over-ceiling artifact) | ✅ Verified — F6 removed the exemption and the criterion passes on its own terms |
| GCPC-117 | Verified | ✅ Verified — with the fixture-digest reproducibility caveat in Fix 3 |
| GCPC-120 | ⚠️ Partial (budgets condition) | ⚠️ Partial — the budgets condition is satisfied; the completion claim still rests on GCPC-012/016/018/034/088 |

`spec.md`'s table should also drop the now-stale parenthetical on GCPC-116 and update GCPC-120's reason.
I have **not** edited `spec.md` — the Verifier writes no code or spec changes; this table is the record.

`.specs/STATE.md` is deliberately **not** updated by this report: the verdict is FAIL.

---

## Summary

**Overall**: ❌ Not Ready — and the bounded fix loop is exhausted, so this escalates to the user.

**Spec-anchored check**: 111/120 ACs matched the spec-defined outcome; 5 gaps; 4 precision/partial
**Sensor**: Skipped — standing project rule (user runs Stryker manually); see `CLAUDE.md` and `.specs/STATE.md:244`
**Gate**: 2072 passed, 0 failed, 0 skipped; build clean, 0 warnings

**What works**: all four iteration-2 gaps are genuinely closed, and I confirmed each from a live run
rather than from its fix task's own tests. The corpus package now has **zero** artifacts over its own
published ceiling with `manifest.json` inside the walk; Acme.Orders has exactly one, an irreducible
single-record shard carrying its degradation reason with an affected count. Every manifest entry on both
packages declares its real byte size, `source/` included. The manifest's top-level version axes agree with
its provenance. `analyze` maps the certification status onto its exit code and returned 3 on both
fixtures. Two runs of the corpus are byte-identical across all 307 files, and `validate` reads a
sharded-manifest package and reports the published status without touching a solution. Entry capability,
the document policy, the retrieval guide, labels, HTTP verb and route fields, configuration triage,
determinism, batch certification, engine certification and the security sweep all hold.

**Issues found**:

1. *(Major)* Three computed envelopes — invocation accounting, contract accounting and the document-policy
   report — never cross the Storage boundary, so GCPC-012, GCPC-016, GCPC-034 and GCPC-088 are satisfied
   only in memory. The exclusion-category vocabulary appears in no published byte. → Fix 1.
2. *(Major)* `InvokesPass.ConcreteImplementors` matches implementors by name, parameters and arity alone,
   publishing a candidate `invokes` edge from `OrderQueriesController.GetOrderStatus` to itself. The test
   that should have caught it filters the candidate set by target project before asserting `Single`. → Fix 2.
3. *(Minor)* The SyntheticSolution immutability digest is a mixed CRLF/LF snapshot and does not reproduce
   from a clean checkout under either `core.autocrlf` setting. → Fix 3.
4. *(Cosmetic)* GCPC-037's bytes-per-token ratio and GCPC-045's largest-artifact measurement are derivable
   but not published as fields. → Fix 4.

**Next steps**: escalate to the user with the four ranked fixes. Fixes 1 and 2 are the substantive ones
and are independent of each other; Fix 2 is the smaller change and closes a fabricated-fact defect, so it
is the better first move if only one further round is authorised. Fix 3 should be taken regardless — it is
cheap, and it currently makes the repository's own regression guard unreproducible in CI.
