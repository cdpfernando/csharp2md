# Pacote de conhecimento útil e confiável — Full-Scope Validation

**Date**: 2026-09-17
**Spec**: `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`
**Diff range**: full feature — entire current state of the implementation at `6ac4b02` (branch `feature/simplif`), not a diff
**Verifier**: independent sub-agent (author ≠ verifier), read-only over the implementation

## Validation Verdict — FAIL ❌

**Spec-anchored check**: 70 of 71 ACs + 5 of 5 edge cases matched their spec-defined outcome. **1 AC (DEP-01) fails** at two of its four scopes, measured on the real corpus. 6 spec-precision / trait-precision gaps flagged.

---

## This report supersedes the Phase 11 report, and why

The previous `validation.md` was **scoped to Phase 11 only** — 4 of 71 ACs (DEP-01, DEP-02, DEP-03,
PKG-05), anchored to `fixtures/ArchitectureDependencyLab`'s 87-edge oracle. It said so explicitly and
did not renew the other 67 rows. Before it, a full 71/71 PASS had been anchored almost entirely to
`fixtures/SyntheticSolution`, and was later shown worthless for dependency correctness: at two entities
a complete graph and a correct graph are the same graph.

This report is the full-scope re-verification the user requested (`.specs/STATE.md`, 2026-09-17): all 71
ACs plus EDG-01..05, evidence-or-zero, and deliberately **not** leaning on `fixtures/SyntheticSolution`
for any claim it cannot discriminate.

**Doing that turned up a defect neither prior run could see.** Phase 11 is real and holds — but it closed
the *BCL-symbol* cause of the complete component graph, not the *shared in-solution type* cause that
`STATE.md` recorded as an unmeasured residual. Measured directly on `fixtures/eShop` for this report,
that residual reproduces **95.8% of the complete Component-scope graph**. Details in Gap 1.

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1–T64, T69–T78 | ✅ Done | All Done-when boxes checked |
| T65 | ⚠️ Complete with recorded shortfall | 1 of 3 boxes explicitly unchecked and annotated "Not met as written" — this is PUB-08's `Partial` |
| T66 | ✅ Done | — |
| T67 | ⚠️ Complete, boxes stale | Status reads `Complete` and `STATE.md` lists it complete, but **all 4 Done-when boxes are unchecked**. The gate note shows the work was done differently (the authoritative check moved to publication time; one non-discriminating case was deleted rather than kept green). The boxes were never reconciled to what was actually built. Bookkeeping only. |
| T68 | ⏸️ Parked | User deprioritised file-size work, 2026-09-17. 4 unchecked boxes are expected. |

Counts: 309 checked boxes, 9 unchecked (1 × T65 annotated, 4 × T67 stale, 4 × T68 parked), 78 task headers.

---

## Spec-Anchored Acceptance Criteria

Evidence-or-zero. Every row cites `file:line` in the real test tree plus the assertion, and the asserted
value is compared against the spec's own EARS outcome. Paths are relative to `D:/workspace/csharp2md`.
Where a claim is about dependency *correctness* rather than schema or separation, only corpus-grade
evidence (`fixtures/ArchitectureDependencyLab`, `fixtures/eShop`) is accepted.

### PKG — Publicar somente conhecimento útil (10 ACs)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| PKG-01 | One manifest grouped by solution; each group declares SolutionId, logical path, roots, indexes, four journeys | `Publication/SolutionManifestContractTests.cs:14-27` — `Assert.IsType<SolutionId>(solution.Id)`, `Assert.Equal("src/App.sln", solution.LogicalRelativePath)`, `Assert.Equal(8, solution.Indexes.Length)`, `Assert.Equal(4, solution.Journeys.Length)`, plus `Assert.DoesNotContain(…"Roots" or "Indexes" or "Journeys")` on the package-level manifest | ✅ PASS |
| PKG-02 | Retained graph starts at proven Component, Deployment Unit, Entry Point, Boundary Operation | `PackageBuilding/RetainedGraphBuilderTests.cs:8-17` — theory over `EntityKind` 5–8, `Assert.Contains(result.Entities, e => e.Kind == kind)`; `Analysis/ArchitectureFactExtractorTests.cs:63,94,139,167` each require project-file or host evidence | ✅ PASS |
| PKG-03 | Retain only facts supporting a retained journey or explaining its gap | `RetainedGraphBuilderTests.cs:19-33` — `Assert.DoesNotContain(…"orphan")`, `Assert.Equal("relation:root-terminal", Assert.Single(result.Relations).CanonicalKey)` | ✅ PASS |
| PKG-04 | Candidate/Unknown/Open Frontier only when they can alter a retained journey | `RetentionPolicyTests.cs:10-14` — `Assert.Equal("gap:relevant", Assert.Single(Apply().Gaps).CanonicalKey)` and `Assert.DoesNotContain(…"gap:orphan")` | ✅ PASS |
| PKG-05 | Exclude tests, unpromoted observations, non-retained type uses, uncited sources, raw config values, per-record files | Corpus-grade: `Cli.Tests/OracleProjectReferenceScoreTests.cs:156-169` — `Assert.True(leaked.Length == 0)` for `symbol:`/`callable:` keys across all six solutions; `:181-193` same for `.Testes`. Unit: `CausalRelationExtractorTests.cs:59-75`, `RetainedGraphBuilderTests.cs:42-47`, `RetentionPolicyTests.cs:22,24,34`, `ScopePairingTests.cs:68-74` | ✅ PASS |
| PKG-06 | Source content only for documents cited by retained evidence | `MarkdownRendererTests.cs:59-64` — `Assert.Equal(["document:src/Billing.cs","document:src/Orders.cs"], index.Documents…)` + `Assert.DoesNotContain(…"/documents/2.md")`; `RetentionPolicyTests.cs:20` | ✅ PASS |
| PKG-07 | Config by keys/sections/links/category/safe location — no env values, credentials, secrets, absolute paths | `ConfigurationPersistenceExtractorTests.cs:14-22` — `Assert.Contains(…DisplayName=="key:Orders:ConnectionString")` **and** `Assert.DoesNotContain(…Contains("hunter2"))` across Entities, CanonicalKeys, Occurrences; `IdentityPrimitivesTests.cs:154` | ✅ PASS |
| PKG-08 | Explicit test inclusion recorded in manifest and run identity | `PackageBuilderTests.cs:14` — `Assert.True(PackageBuilder.Build(Model(), true).Manifest.IncludeTests)`; `SourceInventoryTests.cs:140` identity flip; `PackageContractTests.cs:44` | ✅ PASS |
| PKG-09 | Only observable facts and relations; no business-rule interpretation | `ArchitectureFactExtractorTests.cs:233`, `FactualGraphContractTests.cs:124`; `CausalRelationExtractorTests.cs:169-177` — unresolved call yields `GapKind.Unknown`/`"unresolved-invocation"`, not a confirmed relation | ✅ PASS |
| PKG-10 | Repository contains only the current contract — no version dispatch, legacy reader, converter, compat route | `Surface/CoreTopologyTests.cs:43,62,67,78,94,109,123,138,156,161` — ten structural assertions over the real solution and source tree | ✅ PASS |

### DEP — Navegar e medir dependências (8 ACs)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| **DEP-01** | **Aggregate Confirmed Relations at Document, Project, Component and Deployment Unit scope using only proven membership** | **Project scope ✅** — `Cli.Tests/OracleProjectReferenceScoreTests.cs:86-90` and `:133-143`, scoring 60 correct / 0 false positives / 0 leaks against the 87-edge oracle (re-derived independently, below). **Document scope ✅** — `ScopePairingTests.cs:19-29`. **Component + Deployment Unit scope ❌** — only evidence is `ScopePairingTests.cs:54-62`, which asserts a **self-edge** (`entity:component → entity:component`) as correct on a 4-entity graph. Measured on `fixtures/eShop`: **138 of 144 possible component pairs (95.8%), 34 self-pairs**. See Gap 1. | ❌ **GAP** (2 of 4 scopes) |
| DEP-02 | Preserve Project Reference, internal invocation, structural type use, HTTP, gRPC, messaging, contract and persistence **separately** | `PackageBuilding/RetrievalContractTests.cs:23-40` — `Assert.Equal([ProjectReference…Persistence], Enum.GetValues<DependencyCategory>())`; `DependencyAggregatorTests.cs:8-9` theory over all 8 ordinals; `CausalRelationExtractorTests.cs:11,23,46,113,136,147,157` emit each category discretely; `OracleProjectReferenceScoreTests.cs:426-427` decodes `category == 0` alone off the committed wire | ✅ PASS (separation, not recall — see Gap 4) |
| DEP-03 | Aggregated dependency declares origin, target, scope, categories, occurrence count, variants, direct/transitive nature, and references to source relations and evidence | `RetrievalContractTests.cs:56-69` — all eight members asserted with exact values (`Assert.Equal(3, d.OccurrenceCount)`, `Assert.Equal("var-1", Assert.Single(d.Variants).Value)`, `Assert.Equal("rel-1", …)`, `Assert.Equal("ev-1", …)`); `:41-49` Direct vs Transitive | ✅ PASS |
| DEP-04 | Same origin/target/scope/category → one aggregated edge with total count and deduplicated evidence | `DependencyAggregatorTests.cs:10-11` — `Assert.Equal(2, …OccurrenceCount)` and `Assert.Single(edge.Evidence)`, `Assert.Single(edge.Relations)`; `ScopePairingTests.cs:51-52` | ✅ PASS |
| DEP-05 | A low-level relation contributing to more than one scope reuses its reference without duplicating its factual payload | `ScopePairingTests.cs:88-98` — `Assert.Equal([Document, Project, Component, DeploymentUnit], carrying.Select(e => e.Scope).Distinct().Order())`; `:103-110` — `Assert.Equal(1, occurrences)` of the canonical key across **all** plan artifacts | ✅ PASS |
| DEP-06 | Confirmed count excludes Candidate, Unknown and Open Frontier | `DependencyAggregatorTests.cs:12` — `Assert.Empty(DependencyAggregator.Aggregate([Item(confirmed:false)]))` | ⚠️ **Spec-precision gap** — asserts one `IsConfirmed` boolean; the three named kinds are never distinguished. Already recorded in `tasks.md:1846`, still open. |
| DEP-07 | Opening an aggregated dependency yields resolvable references to its Confirmed Relations and evidence | `CompactDependencyReferenceTests.cs:35-49` — handle resolves to the confirmed fact and to the same ordinal in the evidence index; `:70,97` reject unknown handles; `NavigationPayloadIndexTests.cs:36-61,107-146` | ✅ PASS (resolvability; not semantic support — see Gap 1) |
| DEP-08 | A name alone suggesting a service keeps scope Project — no Service or Deployment Unit | `ArchitectureFactExtractorTests.cs:16,43,320` — `…_WithoutExeOrHost_DoesNotCreateDeploymentUnit`, `…_DirectoryOrAssemblyNameAlone_NeverCreatesDeploymentUnit`. **Corroborated on the corpus by this Verifier**: across all six ArchitectureDependencyLab solutions only the `*.Api` project becomes a Component/DeploymentUnit; `*.Aplicacao`, `*.Infraestrutura`, `*.Nucleo`, `*.Formalizacao` correctly produce none. | ✅ PASS |

**Non-vacuity check on the oracle (re-derived independently, not copied from the prior report).** I parsed
`fixtures/ArchitectureDependencyLab/oracle/project-references.json` myself: **87 edges total**; excluding any
edge whose source or target ends `.Testes` gives per-solution reachable counts of SistemaA 3, SistemaB 12,
SistemaC 0, SistemaD 5, SistemaE 20, SistemaE.Copia 20 — **total 60 reachable, 27 excluded**. These are
*exactly* the `CorrectToday` values in `RecordedBaselines:59-66`. So the conjunction at `:86-90` asserts set
equality (`produced == reachable`), not a threshold: an empty projection scores 0 and fails; any extra edge
scores a false positive or leak and fails. Non-vacuous.

### MET — Publicar medidas explicáveis (8 ACs)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| MET-01 | Fan-out = count of distinct targets reached by retained edges in the requested scope | `DirectMeasureCalculatorTests.cs:7-8` — `Assert.Equal(2, For("a",[Edge("a","b"),Edge("a","c")]).FanOut)` and `Assert.Equal(1, …[Edge("a","b"),Edge("a","b")]…)` | ✅ PASS |
| MET-02 | Fan-in = count of distinct origins reaching the entity in the requested scope | `DirectMeasureCalculatorTests.cs:9-10` — `Assert.Equal(2, For("b",[Edge("a","b"),Edge("c","b")]).FanIn)` and the dedup counterpart | ✅ PASS |
| MET-03 | Occurrence count counts confirmed contributions **before** aggregated-edge deduplication | `DirectMeasureCalculatorTests.cs:11-17` — asserts `OccurrenceCount == 3` from three contributions, computes fan-out, then re-asserts `== 3` (proves the measure pass does not mutate it) | ✅ PASS |
| MET-04 | Cross-component counts edges whose aggregated entities belong to distinct proven components | `DirectMeasureCalculatorTests.cs:18-19` — `Assert.Equal(1, …Edge("a","b",Component).CrossComponentEdges)` and `Assert.Equal(0, …Edge("a","a",…))` | ✅ PASS |
| MET-05 | Cycle participation computed over the retained directed graph in the requested scope | `CycleCalculatorTests.cs:6-13` — two-node cycle members `["a","b"]`, self-cycle, disconnected acyclic excluded, permutation determinism, canonical member order, scope separation theory | ✅ PASS |
| MET-06 | Reverse impact lists the set reachable by the entry index and declares the depth traversed | `ImpactCalculatorTests.cs:5-8` — `Assert.Equal(1, …Depth)` (minimum depth), `Assert.Single(…)` (listed once), `Assert.Equal(3, …Count())` (diamond), cycle case | ✅ PASS |
| MET-07 | Relevant Candidate/Unknown/Open Frontier counts shown separately from confirmed measures | `ImpactCalculatorTests.cs:9-12` — one case per kind asserting `Gaps.Candidate`/`.Unknown`/`.OpenFrontier == 1`, plus `Assert.Equal(0, …)` for an unrelated gap; `RetrievalContractTests.cs:84` | ✅ PASS |
| MET-08 | Omit any composite score or automatic quality/risk/coupling label | `RetrievalModelBuilderTests.cs:19-34` — reflects over **every** type in the PackageBuilding and Publication namespaces and asserts zero properties matching Score/Quality/Risk/Coupling | ✅ PASS |

### NAV — Recuperar respostas diretamente (10 ACs)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| NAV-01 | Exactly one index each of identity, roots, outgoing, incoming, contract, persistence, evidence, measures; each journey references its entry index; no shard choice | `SolutionManifestContractTests.cs:31-45` — `Assert.Equal([Identity, Roots, Outgoing, Incoming, Contracts, Persistence, Evidence, Measures], Enum.GetValues<NavigationIndexKind>())`; `:24-25` `Assert.Equal(8, …Indexes.Length)`; `MachineArtifactWriterTests.cs:14-20`; `NavigationPayloadIndexTests.cs:30`; end-to-end `Cli.Tests/KnowledgePackageJourneyTests.cs:17-18` — `Assert.Equal(8, …GetProperty("indexes").GetArrayLength())` | ✅ PASS |
| NAV-02 | Summary presents components, Deployment Units, cycles, top fan-in/fan-out, four journeys | `MarkdownRendererTests.cs:11,14-23,39,40,41` — including `Assert.Contains("## Components and Deployment Units")` and a regex proving the deployment row is a link that **resolves to a written artifact** | ✅ PASS |
| NAV-03 | Component/service/document pages present outgoing, incoming, measures, effects, gaps with existing Markdown links | `MarkdownRendererTests.cs:42-45` — exact strings `"- [component:billing](0.md) (Http)"`, `"fan-out: 1"`, `"- impact: … at depth 1"`, `"unknown gaps: 1"`; `:75-82` every link in every page resolves to a written artifact | ✅ PASS |
| NAV-04 | Normal Markdown journey needs no directory enumeration, shard choice or ID decoding | `Cli.Tests/KnowledgePackageJourneyTests.cs:13-14` — `Assert.Equal(["manifest.json"], _run.InitialReads)`; `NavigationIndexBuilderTests.cs:6`; `RootsIndexRoutingTests.cs:43,60` | ✅ PASS |
| NAV-05 | Markdown and machine indexes derive from the same retained set and present equivalent dependencies and measures | `RetrievalModelBuilderTests.cs:10-16`; `RetrievalModelReaderTests.cs:11-18` round-trips + `VerifyMarkdown_AcceptsEquivalentBytes`; `RetrievalContractTests.cs:120` | ✅ PASS |
| NAV-06 | Locating a component completes in ≤ 5 reads | `RootsIndexRoutingTests.cs:148-158` — `Assert.Equal(3, measurement.Reads)` at both 3 and 400 roots; `JourneyCertifierTests.cs:10`; end-to-end `KnowledgePackageJourneyTests.cs:73-74` — `AssertBudget("locate", maximumReads: 5, …)` | ✅ PASS |
| NAV-07 | Locating another supported root completes in ≤ 8 reads and 12,000 tokens | `RootsIndexRoutingTests.cs:171-181` — theory over `deployment`/`entrypoint`/`boundary` × {3, 400} roots, `Assert.InRange(measurement.Reads, 1, 8)`, `Assert.InRange(measurement.Tokens, 1, 12_000)` | ✅ PASS |
| NAV-08 | Causal flow to contracts, external effects and persistence completes in ≤ 32 reads and 125,000 tokens | `KnowledgePackageJourneyTests.cs:77-78` — `AssertBudget("follow_flow", 32, 125_000)` with `Assert.InRange` on the parsed detail; `GraphJourneyCertifierTests.cs:11,98,99,111` | ✅ PASS |
| NAV-09 | Reverse impact from file/project/component/DU/contract/data completes in ≤ 32 reads and 125,000 tokens | `KnowledgePackageJourneyTests.cs:81-82` — `AssertBudget("reverse_impact", 32, 125_000)`; `GraphJourneyCertifierTests.cs:12,36,100,112` | ✅ PASS |
| NAV-10 | Inspecting evidence or disposition completes in ≤ 12 reads and 25,000 tokens | `KnowledgePackageJourneyTests.cs:85-86` — `AssertBudget("evidence_disposition", 12, 25_000)`; `EvidenceEntryIndexTests.cs:115`; `JourneyCertifierTests.cs:19` — token ceiling formula `Math.Ceiling(bytes / 4.0)` matches the manifest's declared estimator | ✅ PASS |

### VAR — Isolar projetos, variantes e soluções (6 ACs)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| VAR-01 | Only Analysis Variants resolved by the project's real evaluation are created | `ProjectVariantWorkspaceTests.cs:16-29` — `Assert.Equal("net10.0", workspace.Variant.TargetFramework)` on the real fixture; `:64-77` two variants use isolated workspaces; `ProjectVariantPlannerTests.cs:8,54,119` | ✅ PASS |
| VAR-02 | Target frameworks collected from one project are never applied to another | `ProjectVariantWorkspaceTests.cs:16` (only the requested TFM property is applied); `ProjectVariantPlannerTests.cs:35,71,87,100,161` — each failure mode fails the plan rather than guessing | ✅ PASS |
| VAR-03 | Compatible occurrences across variants produce one logical identity | `LogicalEntityAccumulatorTests.cs:10-24` — `Assert.Single(accumulator.Entities())` with `Assert.Equal(2, …Occurrences().Length)` across net8.0/net10.0 | ✅ PASS |
| VAR-04 | An occurrence's locator and evidence declare the Analysis Variant that produced them | `LogicalEntityAccumulatorTests.cs:28-41` — `Assert.Equal(["shape-net10","shape-net8"], …ShapeDigest…)`; `FactualGraphContractTests.cs:24`; `CausalRelationExtractorTests.cs:194-200` | ✅ PASS |
| VAR-05 | Two incompatible occurrences inside one Analysis Variant are rejected as a structural collision | `LogicalEntityAccumulatorTests.cs:46-59` — `Assert.Equal("occurrence-collision", exception.Code)` plus entity key and variant key asserted; `:63-74` identical shape is idempotent | ✅ PASS |
| VAR-06 | Batch analysis keeps identities, variants, dedup, handles, roots, dependencies and measures structurally isolated per solution, even when handles and index kinds coincide | `SolutionScopedRetrievalTests.cs:33` (`Write_TwoSolutionsWithSameHandlesAndIndexKinds_EmitsIsolatedArtifacts`), `:100`, `:123`; `LogicalEntityAccumulatorTests.cs:77,96`; `KnowledgeEngineWorkflowTests.cs:39,47,55,64`. **Corroborated by this Verifier**: six solutions analysed in one batch produced six isolated solution directories with independent entity tables, and `SistemaE.Copia` stayed separate from `SistemaE`. | ✅ PASS |

### STO — Persistir identidades compactas e shards estáveis (7 ACs)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STO-01 | `^[a-z]{3}_[0-9a-v]{16}$`, exactly 20 chars, leading 80 bits of SHA-256 of the canonical identity in lowercase base32hex after a unique type prefix | `PublicIdRegistryTests.cs:39-44` — two literals computed **outside** the codebase (`component:orders → ent_i0k15e1lv4fo5h8n`); `:51-69` recomputes the same value from `System.Security` SHA-256 and a locally written base32hex; `:20-24` grammar theory; `:92` rejects non-canonical prefixes | ✅ PASS |
| STO-02 | Two different canonical entries producing one digest → explicit diagnostic failure before publication | `PublicIdRegistryTests.cs:74-85` — forces the collision via reflection, then `Assert.Equal(first, exception.Id)`, `Assert.Equal("entity:other", exception.FirstCategory)`, `Assert.Equal("entity:first", exception.SecondCategory)` | ✅ PASS |
| STO-03 | `^[0-9a-z]{1,6}$`, base36 ordinal from `0` after canonical-key ordering, resolvable through a declared index | `LocalTableBuilderTests.cs:15,21,25`; `CompactDependencyReferenceTests.cs:10,18`; `ArtifactWireFormTests.cs:11,24,34` | ✅ PASS |
| STO-04 | One stored entry per repeated identity, document, string and evidence within the solution package | `CanonicalKeyTableTests.cs:10,17`; `CompactDependencyReferenceTests.cs:26,85`; `IdentityPrimitivesTests.cs:127`; `LocalTableBuilderTests.cs:22` | ✅ PASS |
| STO-05 | Projection records use handles without repeating public ID, path or signature already in the local table | `MachineArtifactWriterTests.cs:13,22`; `CanonicalKeyTableTests.cs:65,82,95,105` reject handles with no table row | ✅ PASS |
| STO-06 | Sharding groups records deterministically by family and real byte range, without one file per common record | `ShardPackerTests.cs:8,12,15,17` — `Pack_DoesNotEmitOneFilePerNormalRecord`, `Pack_NeverExceedsHardCeilingForAllowedRecord`, `Pack_UsesStableOrdinalPaths` | ✅ PASS |
| STO-07 | Same evaluated input and policy reprocessed → byte-identical solution package | `PackageBuilderTests.cs:16` — `Assert.Equal(first.PackageDigest, second.PackageDigest)` **and** payload-by-payload equality; `CanonicalJsonTests.cs:14,24,45`; `MachineArtifactWriterTests.cs:23`; `SolutionScopedRetrievalTests.cs:79` permuted solutions | ✅ PASS (see Gap 5 — determinism is proven against a fixed model, not against build state) |

### PUB — Comprometer somente pacotes válidos (8 ACs)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| PUB-01 | Immediate and deferred fragments go through the same materialization, normalization and validator collection | `PackageContractTests.cs:31` — `PackagePlan_HasNoDeferredFragmentContract` (there is one path by construction); `PackagePublicationTests.cs:10` — every planned artifact materialized in its generation | ✅ PASS |
| PUB-02 | Staged plan is rehydrated and validated in full before the atomic swap | `PackagePublicationTests.cs:11` — `Assert.True(PackagePublication.Validate(output.Path).Succeeded)`; `:104` `Assert.Empty(…Failures)`; `:111` four journeys recorded | ✅ PASS |
| PUB-03 | Validate command reuses the same reader and rules applied before commit | `KnowledgeEngineWorkflowTests.cs:97,107,125` — including `Validate_WhenSourceSolutionIsOffline_ReadsOnlyThePackage` and `Validate_DoesNotMutateTheCommittedPackage`; `PackageValidatorTests.cs:11` | ✅ PASS |
| PUB-04 | A package announced as committed passes immediate validation with no interpretation difference | `PackagePublicationTests.cs:112` — `Publish_ValidationHasNoInterpretationDifference`; end-to-end `Cli.Tests/KnowledgePackageJourneyTests.cs:89-96` — real `validate --package` on the freshly analysed fixture, `Assert.Equal(0, exitCode)`, `Assert.Equal(string.Empty, stderr)` | ✅ PASS |
| PUB-05 | On variant/retention/safety/size/materialization/rehydration/validation failure, the last valid package is preserved byte for byte | `Cli.Tests/KnowledgePackageFailureTests.cs:155-183` — `AssertRejectedWithoutMutationAsync` takes a full byte snapshot of every file and re-compares after the rejection, across five real failure classes; `:114-137` lock-held case preserves the prior committed package and leaves no `.staging-*` | ✅ PASS |
| PUB-06 | A C# `//` comment must not be lexically classified as a UNC path | `PublicationSafetyScannerTests.cs:44-50` — `Assert.Equal(PublicationSafetyDisposition.Retained, result.Disposition)` and `Assert.Equal(source, result.Value)` for `"// C:/not/a/path…"`; contrasted at `:57-63` where the same `//` **inside a string literal** is redacted | ✅ PASS (trait-precision gap — see Gap 6) |
| PUB-07 | A real absolute path reaching the materialized plan is removed, redacted or the plan rejected, so the value never appears in the committed package | `PublicationSafetyScannerTests.cs:21-26` — `Assert.NotEqual(Retained, …)` + `Assert.DoesNotContain(value, result.Value)` for `C:/secrets/config.json` and `\\server\share\secret.txt`; `PackagePublicationTests.cs:31-49` real publication rejection; end-to-end `KnowledgePackageJourneyTests.cs:99-107` — `Assert.DoesNotContain("C:\\fixture\\synthetic-output", text)` over all declared artifacts | ✅ PASS |
| PUB-08 | A rejected publication's diagnostic states the applicable project, variant, family and cause | Engine side: `PackagePublicationTests.cs:16-49` — two **real** rejections asserting `Family`/`Artifact` (`"certification"`/`"certification.json"`, `"markdown"`/`"markdown/index.md"`). CLI side: `Cli.Tests/KnowledgePackageFailureTests.cs:23-52` — nine classes asserting `project=`, `variant=`, `family=`, `cause=` — but from a **hand-built `EngineDiagnostic`** (`:28-36`) injected through a stub (`:40`). | ⚠️ **Partial — confirmed accurate, not stale.** No test drives `project` **and** `variant` from a real engine rejection. The five real pipeline failures assert code/stage/cause/family/artifact only. |

### CRT — Certificar utilidade no seam da CLI (9 ACs)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| CRT-01 | An applicable journey is exercised per solution and fails if any solution misses the expected answer | `SolutionCertificationTests.cs:12,31,66` — including `Certify_CorruptionOnlyInSecondSolution_FailsThatSolutionWithoutMaskingIt`; `JourneyCertifierTests.cs:15,16,21`; `GraphJourneyCertifierTests.cs:76-97,113` | ✅ PASS |
| CRT-02 | A non-applicable journey is recorded not-applicable with a reason, never as passed; FollowFlow/ReverseImpact need HTTP/gRPC/Messaging/Contract/Persistence; persistence-only → FollowFlow `no-causal-root` while ReverseImpact stays applicable | `GraphJourneyCertifierTests.cs:29-35` — `Assert.Equal("not_applicable:no-causal-root", result.Detail)`; `:36-40` `Assert.Equal(Passed, Impact(package).Status)` for the same persistence-only package; `:41-75` each absent terminal named exactly | ✅ PASS |
| CRT-03 | Separate extraction and publication metrics, items filtered by reason, measures by family/solution/journey/corpus; each journey budget starts at zero | `PackageBuilderTests.cs:17,18,35-53,106-156,179`; `SolutionCertificationTests.cs:31` — `Certify_EachJourney_RestartsReadAndTokenMeasurements` | ✅ PASS |
| CRT-04 | eShopOnContainers present → committed package ≤ 1,500 files and 64 MiB | `PackageBuilderTests.cs:59-64` — `Assert.Equal(1_500, …)`, `Assert.Equal(67_108_864L, …)` (spec figures converted by hand); end-to-end `LocalCorpusAnalyzeTests.cs:33-46` — `AssertCommittedCeiling(package, 1_500, 64*MiB)` | ⚠️ **Unverified** — clone absent (confirmed: `fixtures/eShopOnContainers` is empty). The case **skipped by name**, which CRT-07 requires. |
| CRT-05 | Pitstop present → ≤ 750 files and 25 MiB | `PackageBuilderTests.cs:66-71` — `Assert.Equal(750, …)`, `Assert.Equal(26_214_400L, …)`; `LocalCorpusAnalyzeTests.cs:50-63` | ⚠️ **Unverified** — clone absent, skipped by name. |
| CRT-06 | eShop present → analysis completes with no collision from applying one project's variant to another | `LocalCorpusAnalyzeTests.cs:16-29` — `Assert.DoesNotContain("variant-collision", stderr)` + exit 0 + `"committed and certified"`. **`fixtures/eShop` IS present; this Verifier ran the LocalCorpus filter and the case passed.** Independently re-run: a full `analyze` on `eShop.slnx` exited 0 and committed. | ✅ PASS (on the real clone) |
| CRT-07 | Where clones exist the Verifier runs the matching acceptance; their absence must not fail CI | `LocalCorpusAnalyzeTests.cs:67-74` — asserts the **exact** skip string naming the clone and the path searched; `:78` `Assert.Null(new LocalCorpusFactAttribute("..","csharp2md.slnx").Skip)` for a present clone; `:83-96` gitignore rules. Observed: filter run = **4 passed, 2 skipped by name, 0 failed**. | ✅ PASS (no `Requirement` trait — see Gap 6) |
| CRT-08 | Versioned fixture contains multi-target, prod+test code, `//` comment, absolute config path, ProjectReference, cross-document call, repeated calls, component dependency, runtime integration, cycle, unconfirmed gap | `Cli.Tests/SyntheticSolutionFixtureTests.cs` — multi-target `:54-59`; prod+test `:62-70`; `//` comment `:109`; absolute config path `:105-107` (`Assert.Equal("C:\\fixture\\synthetic-output", …)`); repeated calls `:73-78`; component dependency + runtime integration `:81-87`; cycle `:90-96`; unconfirmed gap `:44-45`; cross-document boundary edges `:22-31`. ProjectReference: 5 real ones in the fixture, exercised through Roslyn at `ProjectVariantWorkspaceTests.cs:33-45`. | ✅ PASS |
| CRT-09 | CLI analyses the fixture, publishes, rehydrates, validates and completes the four journeys in the same acceptance seam | `Cli.Tests/KnowledgePackageJourneyTests.cs` — one real `analyze` run (`:148-167`), then `:21-24` four journeys declared, `:73-86` all four budgets met, `:89-96` immediate `validate` returns exit 0 | ✅ PASS (no `Requirement` trait — see Gap 6) |

### Edge Cases (5)

| Edge case | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| EDG-01 | A relation without resolvable evidence is omitted as Confirmed or the plan is rejected before commit | `RetainedGraphBuilderTests.cs:57-63` — four cases, each `Assert.Equal("invalid-confirmed-relation", Assert.Throws<RetentionException>(…).Cause)`; `CompactDependencyReferenceTests.cs:108,114`; `CausalRelationExtractorTests.cs:181-190` | ✅ PASS |
| EDG-02 | An applicable journey over budget fails, naming the journey and the exceeded measure | `GraphJourneyCertifierTests.cs:101-110` — `Assert.StartsWith("reads-exceeded:", …)` and `Assert.EndsWith($":{package.Paths.Count}>32", …)`; `JourneyCertifierTests.cs:17` — `Assert.Contains("tokens-exceeded", …)` | ✅ PASS |
| EDG-03 | A package over the corpus-applicable limit fails before the atomic swap | `PackagePublicationTests.cs:75-96` — `Assert.Contains("package-budget:bytes:", …)` plus `Assert.False(File.Exists(manifest.json))` **and** `Assert.False(Directory.Exists(generations))`; `PackageBuilderTests.cs:19,20,74,83,94,158` | ✅ PASS |
| EDG-04 | A dependency existing in only one Analysis Variant keeps that qualification without duplicating source/target identities | Qualification: `DependencyAggregatorTests.cs:13` — `Assert.Equal("v", Assert.Single(…Variants).Value)`. No duplication: `LogicalEntityAccumulatorTests.cs:21` — `Assert.Single(accumulator.Entities())` across two TFMs. Variant propagation: `CausalRelationExtractorTests.cs:194-200`. | ⚠️ **Covered by composition** — both halves asserted, but no single case sets up the "exists in only one variant" precondition and checks the projection. |
| EDG-05 | Markdown/machine divergence → package classified corrupt, commit prevented | `PackagePublicationTests.cs:108` — `Assert.Throws<PackagePublicationException>` + no manifest; `RetrievalModelReaderTests.cs:19-20`; real end-to-end `Cli.Tests/KnowledgePackageFailureTests.cs:101-111` — divergent markdown rejected with `family=markdown`, package byte-identical afterwards | ✅ PASS |

**Status**: ❌ 1 AC gap (DEP-01, 2 of 4 scopes). 70/71 ACs and 5/5 edge cases otherwise matched their
spec-defined outcome; 2 recorded as Partial/Unverified by the spec itself and confirmed accurate.

---

## Ranked Gaps

### Gap 1 — BLOCKER: DEP-01's Component and Deployment Unit scopes publish a near-complete graph

**Measured by this Verifier, read-only, on `fixtures/eShop` (present locally).** I ran
`analyze --solution fixtures/eShop/eShop.slnx` into a scratch directory, decoded the committed shards
outside `Csharp2Md.Core`, and deleted the output. The decoder was first validated against a known-good
signal: Project-scope `ProjectReference` edges out of `src/Ordering.API` decode to exactly its real
references (EventBus, EventBusRabbitMQ, IntegrationEventLogEF, Ordering.Domain, Ordering.Infrastructure,
eShop.ServiceDefaults). So the Component-scope numbers below are read correctly.

| Measure | Value |
| --- | --- |
| Components | 12 |
| Distinct Component-scope pairs published | **138 of 144 possible (95.8%)** |
| Self-pairs | 34 |
| Deployment-Unit pairs | 138 of 144 (95.8%), 34 self-pairs |
| Component-scope edges by category | `InternalInvocation` 138, `StructuralTypeUse` 112, `Persistence` 44, `Http` 2, `Grpc` 2, `Messaging` 1, `Contract` 1 |
| Bare `symbol:`/`callable:` entities | 0 ✅ (T74 holds) |

`src/Ordering.API` publishes `internal-invocation` to **all 12 components including itself**, and
`structural-type-use` to all 12 including itself. Ordering.API does not invoke ClientApp, HybridApp,
WebhookClient, PaymentProcessor, OrderProcessor or WebApp. Its five real project references
(EventBusRabbitMQ, IntegrationEventLogEF, eShop.ServiceDefaults, Ordering.Domain,
Ordering.Infrastructure) are libraries and correctly **not** components — so the correct Component-scope
outgoing set is near-empty, not 12.

**Root cause is the residual `STATE.md` named and left open**: Component-scope lifting still emits
`source.Components × target.Components`. Phase 11 removed the BCL/framework symbols (T74) and the test
projects (T75/T77) from that product, which cut it from 17 components / 289 of 289 pairs to 12 components
/ 138 of 144 — a reduction in magnitude, not in kind. The remaining driver is exactly what `STATE.md`
predicted: a legitimately shared in-solution type (here `eShop.ServiceDefaults`, `EventBus`, the shared
`Ordering.Domain` types) occurs in every host project, so lifting produces every pair. `STATE.md` says
"no measurement showed that as a live defect" — **this measurement does.**

**Why the suite is green.** No test anywhere measures Component-scope density.
`ScopePairingTests.cs:54-62`, DEP-01's only Component/Deployment-Unit evidence, asserts a **self-edge**
(`entity:component → entity:component`) as correct behaviour on a 4-entity graph.
`fixtures/ArchitectureDependencyLab` cannot catch it either: I measured all six solutions and **each has
exactly one Component and one DeploymentUnit** (only the `*.Api` project is an executable host — which is
DEP-08 behaving correctly), so the only Component-scope edges the corpus can produce are self-loops. At
one node, a complete graph and a correct graph are again the same graph. The oracle discriminates Project
scope and nothing above it.

**Which AC this fails.** DEP-01 requires the projection to *"agregar Confirmed Relations"* at Component
and Deployment Unit scope. An edge `Ordering.API → ClientApp (internal-invocation)` aggregates **no
confirmed relation between those two components** — what is confirmed is that both call into a common
third library. Each endpoint's membership is individually proven, but the dependency the edge asserts is
not. DEP-07 is weakened for the same reason: the relations referenced by such an edge do not sustain the
pair the edge states, although DEP-07's own tests only prove handle resolvability.

**Fix task**: define and implement a Component/Deployment-Unit lifting policy — at minimum, do not lift a
relation to a component pair unless the relation's own endpoints are attributable to those components
(e.g. lift only where source and target occurrences are in the pair's own projects, or suppress lifting
through entities whose membership exceeds a stated threshold). Then add a corpus-level density assertion
so this cannot regress unseen. **Priority: Blocker.**

### Gap 2 — The oracle corpus cannot discriminate any scope above Project

`fixtures/ArchitectureDependencyLab` is the project's accuracy instrument, but every one of its six
solutions yields 1 Component and 1 DeploymentUnit. Its `oracle/scenarios.json` (63 scenarios across
`confirmed`/`absent`/`candidate`/`open-frontier`/`unknown`) has **no test consumer at all** — I grepped
the tree; only `oracle/project-references.json` is scored. So the corpus currently certifies one category
(`project-reference`) at one scope (Project), and nothing else. **Priority: Major** (it is what allowed
Gap 1 to survive two verification passes).

### Gap 3 — `ScopePairingTests` encodes the defect shape as expected behaviour

`ScopePairingTests.cs:54-62` asserts that a Component-scope self-edge and a Deployment-Unit-scope
self-edge are present and correct. `STATE.md` already flagged this case as the reason the complete-graph
defect went unnoticed, and it is unchanged after Phase 11. It is now the only thing standing behind
DEP-01's Component arm. **Priority: Major.**

### Gap 4 — Seven of DEP-02's eight categories have no corpus-level accuracy measurement

DEP-02 is about *separation*, and separation is fully proven (closed enum, per-category theory, discrete
decoding off the wire) — so the AC passes. But accuracy for `internal-invocation`, `structural-type-use`,
HTTP, gRPC, messaging, contract and persistence is proven only on hand-built compilations.
`STATE.md`'s own graphify benchmark records 3 of 7 HTTP scenarios detected. No AC pins detection recall,
so this is **not** an AC failure — it is a spec-precision gap: the spec never states a recall obligation
for any category. **Priority: Minor** (flag for spec, not for code).

### Gap 5 — STO-07's determinism is proven against a fixed model, not against build state

`STATE.md` records that analysis is **not** deterministic against build state (SistemaA cold yields 80
aggregated dependencies, warm yields 115 — 44% more). Every STO-07 test feeds a fixed in-memory model, so
byte-stability is proven for the package *builder* and never for the *analysis*. STO-07's wording — "a
mesma entrada avaliada" (the same *evaluated* input) — arguably scopes it to the builder, which is why
this is a spec-precision gap rather than a failure. **Priority: Minor.**

### Gap 6 — Trait and traceability precision (unchanged from the first Verifier's deferred Fix 8)

78 `[Fact]`/`[Theory]` cases carry no `Requirement` trait (was 80), including the CLI suites that solely
own **CRT-07 and CRT-09** — the only two ACs with no trait anywhere in the tree. Both have real evidence
(cited above), so neither fails. Additional misplacements found this pass:

- `PackageBuilderTests.cs:11` is traited **PKG-04** but asserts `measurements.json` exists; `:12` is traited **PKG-05** but asserts `certification.json` exists; `:13` is traited **PKG-06** but asserts a Markdown artifact family exists. None of the three targets its AC's outcome; each AC's real evidence lives elsewhere.
- **PUB-06**'s discriminating case (`PublicationSafetyScannerTests.cs:44`) is traited `CRT-08`; **PUB-07**'s absolute-path arm is traited `PUB-06`.
- `PackageValidatorTests.cs:18` (`Validate_AbsolutePathPayloadReportsArtifact`) is traited **STO-06**; it is PUB-07 evidence.
- `OracleProjectReferenceScoreTests.ProjectReferences_HoldTheRecordedDefectBaseline` still names a "defect baseline" for a state that is correct at Project scope, and its `>=`/`<=` lines at `:83-85` are redundant beside the exact-equality conjunction at `:86-90`.

**Priority: Minor.** Costs no coverage; makes trait-based reports misleading.

---

## Previously-Known Open Items — status after this pass

| Item | Recorded status | After this full-scope pass |
| --- | --- | --- |
| PUB-08 `Partial` | Partial | **Unchanged and confirmed accurate.** Nine classes prove CLI rendering from a fabricated `EngineDiagnostic`; five real pipeline failures prove family+artifact. Neither side proves `project` **and** `variant` from a real engine rejection. Not a stale label. |
| CRT-04 `Unverified` | Unverified | **Unchanged.** `fixtures/eShopOnContainers` confirmed absent; case skipped by name, as CRT-07 requires. |
| CRT-05 `Unverified` | Unverified | **Unchanged.** `fixtures/Pitstop` confirmed absent; case skipped by name. |
| Residual: BCL-generic-wrapped user type (`List<Foo>`) yields no `structural-type-use` for `Foo` | Recorded, not closed | **Unchanged.** Still recorded, still not closed. No AC pins detection recall, so no AC fails on it. |
| Residual: a legitimately shared in-solution type fans its Component-scope membership to every using component | Recorded, not closed, "not measured as a live defect" | **CHANGED — now measured as a live defect.** 138 of 144 component pairs (95.8%) on `fixtures/eShop`. This is Gap 1 and it is the reason for the FAIL verdict. |

---

## Discrimination Sensor

**Skipped, per AGENTS.md.** The automated mutation/fault-injection sensor is a permanent project-level
skip; the user runs Stryker manually. This was not re-litigated and no mutation was applied to the
working tree.

In its place, Phase 7–11 tasks each record a hand-run fault injection in their `tasks.md` gate notes. This
pass performed an equivalent, stronger check for the highest-risk claim instead: rather than injecting a
fault, I **measured the shipped output directly against reality** on both corpora — which is what
uncovered Gap 1, something no mutation of the current tests could have found, since no test measures the
quantity in question.

---

## Payload / Conjunction Rule

Spot-checked across the suite. The assertions are, with the exceptions noted in Gap 6, strong:

- `PublicIdRegistryTests.cs:39-69` pins STO-01 with literals computed outside the codebase **and** an independent in-test recomputation — a grammar-only check would have been the shallow version.
- `RetrievalModelBuilderTests.cs:19-34` proves MET-08 by reflecting over every type in two namespaces rather than checking one known class.
- Every PKG-05 default-exclusion case has a matching PKG-08 opt-in case asserting the *same named item* is present, ruling out an implementation that drops everything.
- `MarkdownRendererTests.cs:75-82` resolves every Markdown link to a written artifact rather than pattern-matching link syntax.
- `KnowledgePackageFailureTests` compares full byte snapshots of every file before and after each rejection, not just an exit code.
- Counter-example: `DependencyAggregatorTests.cs:12` (DEP-06) asserts a single boolean where the spec names three kinds — Gap listed above.

---

## Code Quality

| Principle | Status | Note |
| --- | --- | --- |
| Minimum code | ✅ | Four projects, no speculative abstraction; `CoreTopologyTests` enforces the topology |
| Surgical changes | ✅ | — |
| No scope creep | ✅ | Out-of-scope table in `spec.md` is respected; no query engine, embeddings or wiki code in the tree |
| Matches patterns | ✅ | — |
| Spec-anchored outcome check | ⚠️ | 70/71 ACs match; DEP-01's Component/DU arms assert a shape the corpus contradicts |
| Per-layer coverage expectation | ⚠️ | Domain logic maps 1:1 to ACs; **no layer asserts aggregate graph shape**, which is where the defect lives |
| Every test maps to a requirement | ⚠️ | 78 untraited cases; CRT-07 and CRT-09 have no trait at all |
| Documented guidelines followed | ✅ | `AGENTS.md`: `net10.0` confirmed, no `Microsoft.Build.*` reference, no `MSBuildLocator.RegisterDefaults()` — each enforced by a test (`CoreTopologyTests.cs:109,123,138`); sensor skip honoured |

---

## Gate Check

Run independently by this Verifier, in the real working tree, at `6ac4b02`.

- **Build**: `dotnet build csharp2md.slnx --configuration Release` → exit 0, **0 warnings, 0 errors**.
- **Full**: `dotnet test csharp2md.slnx --configuration Release --no-build` → exit 0.
  - `Csharp2Md.Core.Tests`: **637 passed, 0 failed, 0 skipped** (637 total)
  - `Csharp2Md.Cli.Tests`: **96 passed, 0 failed, 2 skipped** (98 total)
  - **Total: 733 passed, 0 failed, 2 skipped**
- **Oracle filter**: `--filter "Category=OracleCorpus"` → **11 passed, 0 failed**. Score held at 60 correct / 0 false positives / 0 test-policy leaks.
- **LocalCorpus filter**: `--filter "Category=LocalCorpus"` → **4 passed, 2 skipped, 0 failed**. The two skips are `Analyze_eShopOnContainers_CommitsWithinFileAndByteCeilings` and `Analyze_Pitstop_…`, skipped **by name** with the clone path in the reason. `fixtures/eShop` is present and its case ran and passed.
- **Comparison to the last recorded gate (Phase 11)**: Core 637/637 and Cli 96 + 2 skipped — **identical**. No count moved. No test deleted or weakened.
- **Failures**: none.

**The gate is fully green and the feature nonetheless fails.** That is the finding: the defect in Gap 1
is invisible to every test in the tree.

---

## Fix Plans

### Fix 1 — Bound Component and Deployment Unit scope lifting (Blocker)

- **Root cause**: `PackageBuilder`'s scope pairing emits `source.Components × target.Components` for every retained relation. Phase 11 shrank the *inputs* to that product (no BCL symbols, no test projects) but left the product itself unbounded, so any symbol shared across host projects still fans out to every pair.
- **Fix task**: state a lifting policy in `design.md` and implement it — lift a relation to a component pair only when the relation's own source and target occurrences are attributable to those two components, rather than crossing full membership sets. Decide explicitly whether a Component-scope self-edge is ever publishable.
- **Verify**: on `fixtures/eShop`, Component-scope density must fall far below the 95.8% measured here, and `Ordering.API`'s outgoing component set must not contain ClientApp, HybridApp, WebhookClient, PaymentProcessor, OrderProcessor or WebApp.
- **Done when**: a committed test measures Component-scope pair density on a multi-component corpus and fails if it exceeds a stated bound.

### Fix 2 — Give the corpus a multi-component solution, and score `scenarios.json` (Major)

- **Root cause**: every ArchitectureDependencyLab solution has exactly one host project, so no committed fixture can discriminate Component scope; `oracle/scenarios.json`'s 63 scenarios are unused.
- **Fix task**: add a solution with several executable hosts and a known component graph, and score the retained projection against `scenarios.json`'s five expected states the way `project-references.json` is scored today.

### Fix 3 — Re-point `ScopePairingTests`' Component/DU cases (Major)

- **Root cause**: `ScopePairingTests.cs:54-62` asserts the defect's shape as correct.
- **Fix task**: rewrite both cases against the policy Fix 1 establishes.

### Fix 4 — Traceability hygiene (Minor)

- Add `Requirement` traits to the CRT-07/CRT-09 CLI suites; correct the misplacements listed in Gap 6; rename `ProjectReferences_HoldTheRecordedDefectBaseline`; reconcile T67's four stale Done-when boxes with what was built.

---

## Requirement Traceability Update

Proposed for `spec.md` — **not applied by this Verifier** (author ≠ verifier; this is a follow-up for the orchestrator).

| Requirement | Current status in spec.md | Proposed status |
| --- | --- | --- |
| DEP-01 | `Complete — measured: OracleProjectReferenceScoreTests…` | **`Partial`** — Document and Project scope verified (60/60 oracle); Component and Deployment Unit scope measured wrong on `fixtures/eShop` (138 of 144 pairs). The existing citation is true but covers Project scope only; it should say so. |
| DEP-06 | `Complete` | **`Partial`** — asserts one `IsConfirmed` boolean; Candidate/Unknown/Open Frontier never distinguished. |
| DEP-07 | `Complete` | `Complete` — keep, with a note that resolvability is proven and semantic support is not. |
| EDG-04 | `Complete` | `Complete` — keep; note that the two halves are asserted separately, never as one scenario. |
| PUB-08 | `Partial` | `Partial` — **unchanged, confirmed accurate.** |
| CRT-04, CRT-05 | `Unverified` | `Unverified` — **unchanged**, clones still absent. |
| All others (65) | `Complete` | `Complete` — confirmed by this pass with the `file:line` evidence tabled above. |

---

## Notes on fixtures

- `fixtures/CertificationCorpus` and `fixtures/PublicationResilience` are committed and **still have no test consumer anywhere in the tree** (grepped `tests/` and `src/` for both paths: zero hits). Confirmed as a fact, not raised as a gap — no AC references them. `AGENTS.md` already records this and asks that they be re-pointed or deleted deliberately.
- `fixtures/eShop` present; `fixtures/eShopOnContainers` and `fixtures/Pitstop` absent (empty directories).
- Working tree confirmed unchanged by this verification: `git status --porcelain` before and after is identical (`M AGENTS.md`, `M docs/specs/pacote-conhecimento-util-e-confiavel.md` — both pre-existing and untouched). All scratch analysis output was written outside the repository and deleted.

---

## Summary

**Overall**: ❌ Not Ready

**Spec-anchored check**: 70/71 ACs + 5/5 edge cases matched their spec outcome; **1 AC gap (DEP-01)**;
6 spec-precision / trait-precision gaps flagged.
**Sensor**: skipped per `AGENTS.md`; replaced by direct measurement of the shipped output against both corpora.
**Gate**: 733 passed, 0 failed, 2 justified skips; Release build 0 warnings / 0 errors.

**What works.** Phase 11 is real and holds on a second, larger corpus: the Project-scope `ProjectReference`
projection reproduces the oracle exactly (60 of 60 reachable edges, 0 false positives, 0 test-policy
leaks), and on `fixtures/eShop` it decodes `Ordering.API`'s references correctly. Zero BCL/framework
symbol entities and zero `.Testes` entities survive retention anywhere. Identity, sharding, determinism,
publication atomicity, safety redaction, journey budgets and the four-journey CLI seam are all covered by
strong, non-shallow assertions, several of which derive their expectations outside the code under test.

**What fails.** DEP-01's Component and Deployment Unit arms. The complete-graph defect that Phase 11 was
opened to fix is **reduced but not closed**: 138 of 144 possible component pairs (95.8%) on the real
eShop corpus, with `Ordering.API` claiming internal invocation and structural type use against all twelve
components including itself. The cause is precisely the residual `STATE.md` named and left open for want
of a measurement. No test in the tree measures this quantity, and neither versioned fixture can — which is
why a fully green gate coexists with it.

**Next steps**: route Fix 1 (blocker) and Fixes 2–3 (major) back to an implementer, then re-verify. Fix 4
is hygiene and can follow. PUB-08 stays `Partial`, CRT-04/CRT-05 stay `Unverified` until the clones return.
