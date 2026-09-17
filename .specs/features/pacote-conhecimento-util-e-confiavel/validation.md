# Pacote de conhecimento útil e confiável Validation

**Date**: 2026-09-17
**Spec**: `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`
**Diff range**: `ff42c35..6d0a3b3` (full feature); `71e7094..6d0a3b3` (Phase 8 remediation: `10ceb90`, `db16a8a` T60, `a55f831` T61, `03a0814` T62, `bf0bb86` T63, `98f53fc` T64, `e8f3ac1` T65, `df84bac` T66, `6d0a3b3`)
**Verifier**: independent sub-agent, iteration 2 of the bounded fix→re-verify loop (author ≠ verifier)
**Verdict**: **PASS ✅**

---

## Task Completion

T1–T66 all read `Status: Complete` in `tasks.md`. T65 carries a self-recorded shortfall
(`- [ ] Not met as written` on its second Done-when criterion); the shortfall is accurate and is
scored below under PUB-08. No other task carries an unchecked box.

| Task group | Status | Notes |
| ---------- | ------ | ----- |
| T1–T59 (Phases 1–7) | ✅ Done | Re-verified through the AC table below |
| T60 corpus budget | ✅ Done | `PackageBudget.ForCorpus` real and wired into `Build` |
| T61 solution/corpus measures | ✅ Done | Both dimensions round-trip through `measurements.json` |
| T62 independent ID derivation | ✅ Done | Literals independently recomputed by this Verifier — they match |
| T63 non-component locate bound | ✅ Done | The `: 8` arm of the budget selector is now executed |
| T64 multi-scope reuse + deployment nav | ✅ Done | Both halves of DEP-05 asserted |
| T65 publication-rejection coordinates | ⚠️ Done with recorded shortfall | Engine populates; nine-class CLI theory still stubbed |
| T66 spec amendment + traceability | ✅ Done | Amendment judged honest — see CRT-02 below |

---

## Spec-Anchored Acceptance Criteria

Evidence-or-zero. Every row cites `file:line` in the real test tree plus the assertion expression.
Paths are relative to `D:/workspace/csharp2md`.

### P1: Publicar somente conhecimento útil

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| PKG-01 | exactly one entry manifest, grouped by solution, each group declaring SolutionId, logical path, roots, indexes and the four journeys | `tests/Csharp2Md.Core.Tests/PackageBuilding/MachineArtifactWriterTests.cs:10` — `Assert.Single(Write().Artifacts.Where(a => a.Path.Value == "manifest.json"))`; `:11` — `Assert.Equal("src/App.sln", Assert.Single(Write().Manifest.Solutions).LogicalRelativePath)`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:17` — `Assert.Equal(8, _run.Solution.GetProperty("indexes").GetArrayLength())` | ✅ PASS |
| PKG-02 | retained graph rooted at Component, Deployment Unit, Entry Point and Boundary Operation | `tests/Csharp2Md.Core.Tests/Analysis/ArchitectureFactExtractorTests.cs:63,94,139,167` — `Extract_OutputTypeExe_CreatesDeploymentUnitWithProjectFileEvidence`, `…EmitsEntryPointAndBoundaryOperation`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:35-36` — `Assert.Contains(roots, r => r.Contains(":component:"))` / `":deploymentunit:"` | ✅ PASS |
| PKG-03 | retain only what sustains a retained journey or explains its gap | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetainedGraphBuilderTests.cs:19` — `Build_ExcludesDisconnectedInventory`; `PackageBuilderTests.cs:10` — `Build_ContainsAllMachineAndMarkdownBytes` | ✅ PASS |
| PKG-04 | Candidate/Unknown/Open Frontier published only when they can alter or interrupt a retained journey | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetentionPolicyTests.cs:9` — `Apply_RetainsGapAffectingJourney`; `:11` — `Apply_ExcludesGapOutsideJourney` | ✅ PASS |
| PKG-05 | exclude tests, unpromoted observations, unretained type uses, uncited sources, raw config values, per-record files | `tests/Csharp2Md.Core.Tests/Analysis/SourceInventoryTests.cs:53,80,100`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:103` — `Assert.DoesNotContain("Acme.Shipping.Tests", text)` | ✅ PASS |
| PKG-06 | source content only for documents cited by retained evidence | `tests/Csharp2Md.Core.Tests/PackageBuilding/MarkdownRendererTests.cs:59` — `Render_UncitedDocumentGetsNoPage`; `SourceInventoryTests.cs:196` | ✅ PASS |
| PKG-07 | config as keys/sections/links/category/safe location, never values, credentials, secrets or absolute paths | `tests/Csharp2Md.Core.Tests/Analysis/ConfigurationPersistenceExtractorTests.cs:14` — `Extract_ConfigValueNeverEntersGraph`; `tests/Csharp2Md.Core.Tests/Publication/PublicationSafetyScannerTests.cs:12` — `Assert.Equal(Retained, ScanStructuredValue(value).Disposition)` for safe values only | ✅ PASS |
| PKG-08 | explicit test inclusion recorded in manifest and run identity | `tests/Csharp2Md.Core.Tests/Analysis/SourceInventoryTests.cs:119` — `Collect_ExplicitIncludeTests_AdmitsTestDocumentsAndChangesPolicyIdentity`; `:139` — `AnalysisPolicy_IncludeTestsFlip_ChangesIdentityOnly`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:111` — `Assert.False(_run.Manifest.GetProperty("include_tests").GetBoolean())` | ✅ PASS |
| PKG-09 | observable facts and relations only, no business-rule interpretation | `tests/Csharp2Md.Core.Tests/Analysis/ArchitectureFactExtractorTests.cs:233` — `Extract_DoesNotEmitBusinessRuleOrQualityLabels`; `CausalRelationExtractorTests.cs:70,114` — unresolved call ⇒ gap, not a confirmed relation | ✅ PASS |
| PKG-10 | repository contains only the current contract: no version dispatch, legacy reader, converter or compatibility route | `tests/Csharp2Md.Core.Tests/Surface/CoreTopologyTests.cs:63` — `Assert.Equal(CurrentProjects, ProjectFiles())`; `:95` — `ProductSources_CarryNoVersionDispatchOrCompatibilityPath`; `:110` — `ProductSources_NeverCallMSBuildLocatorRegisterDefaults`; `:124` — `NoProject_ReferencesMicrosoftBuildPackages`; `:144` — `Assert.Equal("net10.0", …)` and `Assert.Equal("5.6.0", PackageVersion(packages, "Microsoft.CodeAnalysis.Workspaces.MSBuild"))` | ✅ PASS |

### P1: Navegar e medir dependências

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| DEP-01 | aggregate confirmed relations at Document, Project, Component and Deployment Unit using proven membership only | `tests/Csharp2Md.Core.Tests/PackageBuilding/DependencyAggregatorTests.cs:7` — `Assert.Equal((AggregationScope)scope, …Scope)` over all four; `ScopePairingTests.cs:17` — `Assert.Equal(EvidenceDocument, Assert.Single(EdgesInto(TargetDocument)).Source.Value)`; `:41` — project edge absent when the evidence document has no proven owner | ✅ PASS |
| DEP-02 | keep the eight categories separate | `tests/Csharp2Md.Core.Tests/PackageBuilding/DependencyAggregatorTests.cs:9` — `Assert.Equal((DependencyCategory)category, …Category)` over all 8 rows; `Analysis/CausalRelationExtractorTests.cs:11,23,46,59` | ✅ PASS |
| DEP-03 | aggregated edge declares source, target, scope, categories, occurrence count, variants, direct/transitive nature and references | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetrievalContractTests.cs:56` — asserts each of Scope/Source/Target/Category/OccurrenceCount(3)/Variants/Relations/Evidence on value; `:42` — `Direct` vs `Transitive` distinct natures | ✅ PASS |
| DEP-04 | same source+target+scope+category ⇒ one aggregated edge with the total count and deduplicated evidence | `DependencyAggregatorTests.cs:10` — `Assert.Equal(2, …OccurrenceCount)`; `:11` — `Assert.Single(edge.Evidence); Assert.Single(edge.Relations)` | ✅ PASS |
| DEP-05 | a low-level relation contributing to more than one scope is reused by reference without duplicating its factual payload | **T64.** `tests/Csharp2Md.Core.Tests/PackageBuilding/ScopePairingTests.cs:67` — `Assert.Equal([Document, Project, Component, DeploymentUnit], carrying.Select(e => e.Scope).Distinct().Order())` + `Assert.All(carrying, e => Assert.Single(e.Relations, r => r.Value == "relation:source-target"))`; `:82` — `Assert.Equal(1, occurrences)` counting `"relation:source-target"` across **every** artifact payload in the plan | ✅ PASS |
| DEP-06 | confirmed count excludes Candidate, Unknown and Open Frontier | `DependencyAggregatorTests.cs:12` — `Assert.Empty(DependencyAggregator.Aggregate([Item(confirmed:false)]))` | ⚠️ Spec-precision gap (see note 3) |
| DEP-07 | opening an aggregated dependency yields resolvable references to its relations and evidence | `tests/Csharp2Md.Core.Tests/PackageBuilding/CompactDependencyReferenceTests.cs:35,49` — handle resolves to the confirmed fact / same ordinal in the evidence index; `:70` — unknown handle rejected | ✅ PASS |
| DEP-08 | name-only service hints keep scope at Project; no Service or Deployment Unit | `tests/Csharp2Md.Core.Tests/Analysis/ArchitectureFactExtractorTests.cs:16` — `Extract_ProjectNameSuggestingService_WithoutExeOrHost_DoesNotCreateDeploymentUnit`; `:43`; `:320` | ✅ PASS |

### P1: Publicar medidas explicáveis

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| MET-01 | fan-out = distinct targets reached by retained edges in the requested scope | `DirectMeasureCalculatorTests.cs:7` — `Assert.Equal(2, For("a",[Edge("a","b"),Edge("a","c")]).FanOut)`; `:8` — `Assert.Equal(1, …)` on a repeated target; `:21` — scopes kept separate over all four | ✅ PASS |
| MET-02 | fan-in = distinct origins reaching the entity | `DirectMeasureCalculatorTests.cs:9` — `Assert.Equal(2, For("b",[Edge("a","b"),Edge("c","b")]).FanIn)`; `:10` — `Assert.Equal(1, …)` | ✅ PASS |
| MET-03 | occurrence count counts confirmed contributions *before* edge deduplication | `DirectMeasureCalculatorTests.cs:11` — three distinct contributions collapse to one edge, `Assert.Equal(3, …OccurrenceCount)` while `Assert.Equal(1, For("a",aggregated).FanOut)` | ✅ PASS |
| MET-04 | cross-component count = edges whose aggregated entities belong to distinct proven components | `DirectMeasureCalculatorTests.cs:18` — `Assert.Equal(1, …CrossComponentEdges)`; `:19` — self-edge `Assert.Equal(0, …)` | ✅ PASS |
| MET-05 | cycle participation computed over the retained directed graph in scope | `CycleCalculatorTests.cs:7` — `Assert.Equal(["a","b"], Assert.Single(Calculate([E("a","b"),E("b","a")])).Members…)`; `:6,8,9` | ✅ PASS |
| MET-06 | reverse impact lists the reachable set and declares the traversed depth | `ImpactCalculatorTests.cs:5` — `Assert.Equal(1, For("c",…).ReverseImpact.Single(x => x.Entity.Value=="a").Depth)`; `:6,7,8` — once-only, diamond, cycle | ✅ PASS |
| MET-07 | Candidate/Unknown/Open Frontier counts shown separately from confirmed measures | `ImpactCalculatorTests.cs:9,10,11` — `Assert.Equal(1, …Gaps.Candidate / .Unknown / .Frontier)`; `:12` — unrelated gap omitted | ✅ PASS |
| MET-08 | no composite score or automatic quality/risk/coupling label | `RetrievalModelBuilderTests.cs:19` — reflection over every `PackageBuilding*`/`Publication*` type asserting no property name contains Score/Quality/Risk/Coupling; `:13` — reverse impact merged without a score | ✅ PASS |

### P1: Recuperar respostas diretamente

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| NAV-01 | exactly one index of each of the eight kinds per solution; each journey names its entry index; each index points at its logical entry with no shard choice | `tests/Csharp2Md.Core.Tests/Publication/SolutionManifestContractTests.cs:31` — `Assert.Equal([Identity,Roots,Outgoing,Incoming,Contracts,Persistence,Evidence,Measures], Enum.GetValues<NavigationIndexKind>())`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:18` — `Assert.Equal(8, …indexes…GetArrayLength())`; `PackageBuilding/NavigationIndexBuilderTests.cs:5` — `Assert.Equal("indexes/"+key+".json", index.Resolve(key).ArtifactPath)` for all seven start keys | ✅ PASS |
| NAV-02 | summary presents components, **Deployment Units**, cycles, top fan-in/fan-out and the four journeys | **T64.** `tests/Csharp2Md.Core.Tests/PackageBuilding/MarkdownRendererTests.cs:14` — `Assert.Contains("## Components and Deployment Units", text)`, `Assert.Contains("deployment:orders-api", text)`, regex-matched Markdown link, and `Assert.Contains(Resolve("markdown/index.md", link…), written)` proving the link reaches a written artifact; `:11,39,40,41` cover components, fan-in/out, cycles and the four journeys | ✅ PASS |
| NAV-03 | component/service/document pages present outgoing, incoming, measures, effects and gaps with existing Markdown links | `MarkdownRendererTests.cs:42,43,44,45` — `Render_EntityPageListsOutgoing/Incoming/Measures/ImpactAndGaps` | ✅ PASS |
| NAV-04 | a normal Markdown journey needs no directory enumeration, shard choice or ID decoding | `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:14` — `Assert.Equal(["manifest.json"], _run.InitialReads)`; `NavigationIndexBuilderTests.cs:6` — `Resolve_MissingKeyRejectsWithoutShardChoice`; `:11` — `Resolve_DoesNotDecodeStartKey`; `RootsIndexRoutingTests.cs:60` — every derived root page is a written artifact | ✅ PASS |
| NAV-05 | Markdown and machine indexes derive from the same retained set and present equivalent dependencies and measures | `MarkdownRendererTests.cs:47` — `Render_UsesMachineManifestRoots`; `Publication/RetrievalModelReaderTests.cs:19,20` — divergence/absence rejected with the path; `MachineArtifactWriterTests.cs:21` — `Assert.Equal(4, …Journeys.Length)` | ✅ PASS |
| NAV-06 | locating a component completes in ≤ 5 reads | `tests/Csharp2Md.Core.Tests/PackageBuilding/RootsIndexRoutingTests.cs:146` — `Assert.Equal(3, measurement.Reads)` at 3 and 400 roots; `Publication/JourneyCertifierTests.cs:10` — `Certify_LocateComponentPassesWithinFiveReads`; `SolutionCertificationTests.cs:52` — `Assert.Equal(3, Reads(solution, JourneyKind.Locate))` | ✅ PASS |
| NAV-07 | locating **another supported root** completes in ≤ 8 reads and ≤ 12,000 tokens | **T63.** `tests/Csharp2Md.Core.Tests/PackageBuilding/RootsIndexRoutingTests.cs:170` — theory over `deployment`/`entrypoint`/`boundary` at 3 and 400 roots: `Assert.Equal(Passed, journey.Status)`, `Assert.InRange(measurement.Reads, 1, 8)`, `Assert.InRange(measurement.Tokens, 1, 12_000)`. This is the first case to execute the `: 8` arm of `src/Csharp2Md.Core/Publication/Certification/JourneyCertifier.cs:99` | ✅ PASS |
| NAV-08 | causal flow to contracts, external effects and persistence in ≤ 32 reads and ≤ 125,000 tokens | `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:77` — `AssertBudget("follow_flow", maximumReads: 32, maximumTokens: 125_000)` with `Assert.InRange` on both measured values; `Publication/GraphJourneyCertifierTests.cs:10,51,52` | ✅ PASS |
| NAV-09 | reverse impact from file/project/component/deployment/contract/data in ≤ 32 reads and ≤ 125,000 tokens | `KnowledgePackageJourneyTests.cs:81` — `AssertBudget("reverse_impact", 32, 125_000)`; `GraphJourneyCertifierTests.cs:11,35,53` | ✅ PASS |
| NAV-10 | evidence/disposition inspection in ≤ 12 reads and ≤ 25,000 tokens | `tests/Csharp2Md.Core.Tests/PackageBuilding/EvidenceEntryIndexTests.cs:114` — `Certify_EvidenceJourney_ResolvesASelectedRecordWithinTwelveReadsAndTwentyFiveThousandTokens`; `KnowledgePackageJourneyTests.cs:85` — `AssertBudget("evidence_disposition", 12, 25_000)` | ✅ PASS |

### P1: Isolar projetos, variantes e soluções

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| VAR-01 | only the Analysis Variants resolved by the project's own evaluation | `tests/Csharp2Md.Core.Tests/Analysis/ProjectVariantPlannerTests.cs:8` — TFM pairs emitted without a global `TargetFramework`; `:54` — multi-target dedup keeping both TFMs; `ProjectVariantWorkspaceTests.cs:14` — `OpenAsync_AppliesOnlyTheRequestedTargetFrameworkProperty` | ✅ PASS |
| VAR-02 | target frameworks collected from one project never applied to another | `ProjectVariantPlannerTests.cs:9,71,87` — missing/unmatched TFM fails as a variant plan rather than borrowing one | ✅ PASS |
| VAR-03 | compatible occurrences across variants yield one logical identity | `Analysis/LogicalEntityAccumulatorTests.cs:9` — `Add_CompatibleOccurrencesAcrossTfms_ShareOneLogicalEntity`; `IdentityPrimitivesTests.cs:50` — `EntityKey_IsLogicalAndSharedAcrossVariants` | ✅ PASS |
| VAR-04 | locator and evidence declare the producing Analysis Variant | `Analysis/FactualGraphContractTests.cs:24` — `OccurrenceAndEvidence_DeclareTheAnalysisVariantThatProducedThem`; `ArchitectureFactExtractorTests.cs:345` — `Extract_OccurrencesAreVariantQualified` | ✅ PASS |
| VAR-05 | incompatible occurrences inside one variant reject the structural collision | `LogicalEntityAccumulatorTests.cs:44` — `Add_IncompatibleShapesWithinSameVariant_Collides`; `:62` — identical shape is idempotent (the discriminating negative) | ✅ PASS |
| VAR-06 | identities, variants, dedup, handles, roots, dependencies and measures stay structurally isolated per solution, even when handles and index kinds coincide | `PackageBuilding/SolutionScopedRetrievalTests.cs:100` — `Read_TwoSolutionsWithDifferentCausalData_DoesNotLeakAcrossSolutions`; `:123` — corruption in the second solution reports that solution's artifact; `IdentityPrimitivesTests.cs:34` — project key scoped to the solution | ✅ PASS |

### P1: Persistir identidades compactas e shards estáveis

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STO-01 | `^[a-z]{3}_[0-9a-v]{16}$`, exactly 20 chars, first 80 bits of SHA-256 of the canonical identity in lowercase base32hex after a unique type prefix | **T62.** `tests/Csharp2Md.Core.Tests/PackageBuilding/PublicIdRegistryTests.cs:42` — `Assert.Equal("ent_" + expected, Register("ent", category))` with literals `i0k15e1lv4fo5h8n` / `57qlrjjre54j61bv`; `:50` — recomputes the same value inside the test from `SHA256.HashData`, `AsSpan(0,10)` and a locally written base32hex alphabet; `:19` — `Assert.Matches("^[a-z]{3}_[0-9a-v]{16}$", id)` over 12 categories; `:27` — determinism + prefix. **This Verifier recomputed both literals independently (Python `hashlib.sha256`, first 10 bytes, base32hex) and they match exactly.** | ✅ PASS |
| STO-02 | two different canonical entries with the same digest ⇒ explicit diagnostic failure before publication | `PublicIdRegistryTests.cs:73` — `Assert.Throws<PublicIdCollisionException>(…)` then `Assert.Equal(first, exception.Id)`, `Assert.Equal("entity:other", exception.FirstCategory)`, `Assert.Equal("entity:first", exception.SecondCategory)` | ✅ PASS |
| STO-03 | `^[0-9a-z]{1,6}$` base36 ordinal from `0` after canonical-key ordering, resolvable through a manifest-declared index | `PackageBuilding/CanonicalKeyTableTests.cs:25,51`; `ArtifactWireFormTests.cs:11,24,34` — handles serialised as JSON strings and resolved back | ✅ PASS |
| STO-04 | one stored entry per repeated identity, document, string and evidence inside the solution package | `ArtifactWireFormTests.cs:45,62`; `IdentityPrimitivesTests.cs:127` — `RepeatedIdentitiesWithinASolution_CompareEqualForDeduplication`; `PackageBuilderTests.cs:52` — one artifact path per payload | ✅ PASS |
| STO-05 | projection records use handles and do not repeat public ID, path or signature already in the local table | `CanonicalKeyTableTests.cs:65,82,95,105` — handles with no table row, duplicate keys and missing tables all rejected | ✅ PASS |
| STO-06 | deterministic sharding by family and real byte range, never one file per common record | `ShardPackerTests.cs:15` — `Assert.Single(Pack("entities",[Record("a",100),Record("b",100)]))`; `:13` — `Assert.InRange(shard.ByteCount, 0, HardCeilingBytes)`; `:17` — `Assert.Equal(["entities.000000.json","entities.000001.json"], …)` | ✅ PASS |
| STO-07 | same evaluated input + policy ⇒ byte-identical solution package | `PackageBuilderTests.cs:16` — `Assert.Equal(first.PackageDigest, second.PackageDigest)` and payload-sequence equality; `CanonicalJsonTests.cs:14,24,46`; `ShardPackerTests.cs:16` | ✅ PASS |

### P1: Comprometer somente pacotes válidos

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| PUB-01 | immediate and deferred fragments pass the same materialization, normalization and validator collection | `tests/Csharp2Md.Core.Tests/Publication/PackagePublicationTests.cs:10` — every planned artifact exists in its immutable generation; `PackageContractTests.cs:31` — `PackagePlan_HasNoDeferredFragmentContract` | ✅ PASS |
| PUB-02 | staged plan is rehydrated and validated before the atomic swap | `PackagePublicationTests.cs:11` — `Assert.True(PackagePublication.Validate(output.Path).Succeeded)` after publish; `src/Csharp2Md.Core/Publication/PackagePublication.cs:38,42` calls `EnsureValid` twice before `Directory.Move` at `:46` | ✅ PASS |
| PUB-03 | the validate command reuses the same reader and rules applied before commit | `Publication/PackageValidatorTests.cs:11` — `Validate_UsesManifestReader`; `PackagePublicationTests.cs:56` — `Validate_UsesPublishedPackageReader`; `:64` — `Publish_ValidationHasNoInterpretationDifference` | ✅ PASS |
| PUB-04 | a package announced as committed passes immediate validation with no interpretation difference | `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:89` — real CLI `validate` on the just-committed package: `Assert.Equal(0, exitCode)`, `Assert.Contains("valid", stdout)`, `Assert.Equal(string.Empty, stderr)`; `PackagePublicationTests.cs:12,52,58` | ✅ PASS |
| PUB-05 | any failure preserves the last valid package byte for byte | `tests/Csharp2Md.Cli.Tests/KnowledgePackageFailureTests.cs:172` — `AssertSnapshotEqual(before, PackageSnapshot.Read(candidate.Directory))` comparing every file's bytes, plus `Assert.Empty(Directory.GetDirectories(…, ".staging-*"))`, used by five injected failure classes at `:60,69,79,97,110`; `PackagePublicationTests.cs:50` | ✅ PASS |
| PUB-06 | a C# `//` comment must not be lexically classified as a UNC path | `tests/Csharp2Md.Core.Tests/Publication/PublicationSafetyScannerTests.cs:44` — `var source = "// C:/not/a/path…"; Assert.Equal(PublicationSafetyDisposition.Retained, result.Disposition); Assert.Equal(source, result.Value)`; `:57` — the same text inside a string literal *is* redacted, which is the discriminating counterpart | ✅ PASS (trait mislabelled `CRT-08` — see note 4) |
| PUB-07 | a real absolute path reaching the materialized plan is removed, redacted or rejects the plan, so it never appears in the committed package | `PublicationSafetyScannerTests.cs:21` — `Assert.NotEqual(Retained, …); Assert.DoesNotContain(value, result.Value)` for `C:/…`, UNC, `../`; `Publication/PackagePublicationTests.cs:31` — planted `C:\Users\…` in the summary rejects before the swap, `Assert.False(File.Exists(Path.Combine(output.Path, "manifest.json")))`; `KnowledgePackageJourneyTests.cs:106` — `Assert.DoesNotContain("C:\\fixture\\synthetic-output", text)` over the committed package; `src/Csharp2Md.Core/Publication/PackageValidator.cs:39-42` | ✅ PASS |
| PUB-08 | a rejection reports the **applicable** project, variant, family and cause | **T65.** `tests/Csharp2Md.Core.Tests/Publication/PackagePublicationTests.cs:16` — real rejection, `Assert.Equal("certification", rejection.Family); Assert.Equal("certification.json", rejection.Artifact)`; `:31` — a *different* real cause, `Assert.Equal("markdown/index.md", rejection.Artifact); Assert.Equal("markdown", rejection.Family)`; five real end-to-end CLI rejections at `tests/Csharp2Md.Cli.Tests/KnowledgePackageFailureTests.cs:60,69,79,97,110` assert `family=` and `artifact=` on a genuinely corrupted package. **Project and variant are exercised only through the fabricated `EngineDiagnostic` at `KnowledgePackageFailureTests.cs:28-36`.** | ⚠️ PASS with a residual gap (see note 1) — `Partial` in the table is the honest call |

### P1: Certificar utilidade no seam da CLI

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| CRT-01 | an applicable journey is exercised per solution and fails if any solution misses the expected answer | `tests/Csharp2Md.Core.Tests/Publication/SolutionCertificationTests.cs:12` — four journeys for each of two solutions; `:66` — `Certify_CorruptionOnlyInSecondSolution_FailsThatSolutionWithoutMaskingIt`; `JourneyCertifierTests.cs:21` — `Certify_ApplicableJourneyNeverReportsNotApplicable`; `GraphJourneyCertifierTests.cs:40,47,48,49,50,66` | ✅ PASS |
| CRT-02 | a non-applicable journey is recorded not-applicable **with a reason**, never as passed; FollowFlow/ReverseImpact need HTTP/gRPC/Messaging/Contract/Persistence; FollowFlow additionally needs a causal root, so a Persistence-only solution records `no-causal-root` while ReverseImpact stays applicable | **T66 amendment.** `tests/Csharp2Md.Core.Tests/Publication/GraphJourneyCertifierTests.cs:33` — `Assert.Equal("not_applicable:no-causal-root", result.Detail)`; `:38` — `Assert.Equal(JourneyCertificationStatus.Passed, Impact(package).Status)` for the same Persistence-only package; `:12,13,14,21` — empty and internal-invocation-only cases with `Assert.StartsWith("not_applicable:", …)`. Amendment judged **honest** — see note 5 | ✅ PASS |
| CRT-03 | separate extraction and publication metrics, items filtered by reason, and measures by **family, solution, journey and corpus**; each journey budget starts from zero | **T61.** family — `PackageBuilderTests.cs:35,38`; solution — `:103` `Assert.Equal(plan.Measurements.ByFamily.Sum(f => f.ArtifactCount) - 1, solution.ArtifactCount)` + owned-byte equality, `:117` two solutions separated and canonically ordered; corpus — `:132` `Assert.Equal("pitstop.sln", corpus.Corpus); Assert.Equal(750, corpus.MaximumArtifacts); Assert.Equal(26_214_400L, corpus.MaximumBytes)`, `:146` unpinned, `:150` round-trip through `CanonicalJson`; journey — `Publication/SolutionCertificationTests.cs:52-55` `Assert.Equal(3/4/3/3, Reads(solution, …))` per journey plus exact byte and ceiling-divided token equality at `:56-58`; extraction vs publication — `PackageBuilderTests.cs:17`; filtered-by-reason — `:18` | ✅ PASS — all four dimensions present and asserted on values |
| CRT-04 | with eShopOnContainers present, the committed package holds ≤ 1,500 files and ≤ 64 MiB | **T60.** `PackageBuilderTests.cs:59` — `Assert.Equal(1_500, budget.MaximumArtifacts); Assert.Equal(67_108_864L, budget.MaximumBytes)` (the spec's own numbers, hand-converted); `src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs:97` — `budget ??= PackageBudget.ForCorpus(model)` on the path `KnowledgeEngine.cs:37` actually takes; `:127,132` refuse beyond it before the plan is returned. End-to-end measurement at `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs:40` — **skipped, clone absent** (CRT-07 requires exactly that) | ⚠️ Conditional PASS — enforcement verified, corpus measurement deferred. `Unverified` in the table is correct |
| CRT-05 | with Pitstop present, ≤ 750 files and ≤ 25 MiB | **T60.** `PackageBuilderTests.cs:66` — `Assert.Equal(750, …MaximumArtifacts); Assert.Equal(26_214_400L, …MaximumBytes)`; `:93` — `Build_RefusesAPinnedCorpusAtItsOwnCeilingRatherThanTheDefault`: the same 800-root model throws `package-budget: 'artifacts'. corpus: 'pitstop.sln'.` under Pitstop and is accepted (`Assert.InRange(accepted.Artifacts.Length, 751, 1_500)`) under an unpinned name. `LocalCorpusAnalyzeTests.cs:57` — skipped, clone absent | ⚠️ Conditional PASS — same as CRT-04. `Unverified` is correct |
| CRT-06 | eShop completes with no collision caused by applying one project's variant to another | `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs:23` — `Assert.DoesNotContain("variant-collision", stderr)` — **this case ran and passed in this Verifier's gate** (only the two other clones skipped); `Analysis/ProjectVariantPlannerTests.cs:10,140` | ✅ PASS |
| CRT-07 | present clones run their acceptance; absent clones do not fail CI | `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs:67` — `Assert.Equal($"local eShop clone is not present at '…'.", absent.Skip)`; `:78` — `Assert.Null(new LocalCorpusFactAttribute("..","csharp2md.slnx").Skip)`; `:89-91` — the three clone paths are gitignored. Observed in this Verifier's gate: exit 0 with exactly 2 named skips | ✅ PASS (no `Requirement` trait — see note 4) |
| CRT-08 | the versioned fixture carries multi-target, production + test code, a `//` comment, an absolute config path, a Project Reference, a cross-document call, repeated calls, a cross-component dependency, a runtime integration, a cycle and an unconfirmed gap | `tests/Csharp2Md.Cli.Tests/SyntheticSolutionFixtureTests.cs`; `Analysis/ArchitectureFactExtractorTests.cs:285` — `Extract_AcmeOrders_EmitsDeploymentComponentEntryAndBoundary`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:44` — `Assert.True(categories.IsSupersetOf(ExpectedCategories))` over the hand-authored edges; `:49` — `occurrence_count > 1` | ✅ PASS |
| CRT-09 | the CLI analyses the fixture, publishes, rehydrates, validates and completes the four journeys in the same acceptance seam | `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:148-160` — the fixture drives the real `analyze` CLI (`Assert.Equal(0, exitCode)`), then `:22` four journeys declared, `:73,77,81,85` all four certified inside their budgets, `:89` immediate `validate` succeeds | ✅ PASS (no `Requirement` trait — see note 4) |

### Edge Cases

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| EDG-01 | a relation with no resolvable evidence is omitted as Confirmed or rejects the plan before commit | `PackageBuilding/CompactDependencyReferenceTests.cs:108,114` — `Write_RejectsAReferenceWithNoRetainedConfirmedRelation` / `…NoRetainedEvidence`; `Analysis/CausalRelationExtractorTests.cs:126` — `Extract_EveryConfirmedRelation_ResolvesEvidenceChain` | ✅ PASS |
| EDG-02 | an applicable journey over budget fails with the journey and the exceeded measure in the diagnostic | `Publication/GraphJourneyCertifierTests.cs:54` — `Assert.StartsWith("reads-exceeded:", result.Detail)` and `Assert.EndsWith($":{package.Paths.Count}>32", result.Detail)`; `JourneyCertifierTests.cs:17` — `Certify_LargeComponentPageFailsTokenBudget` | ✅ PASS |
| EDG-03 | a package exceeding the limit **applicable to the corpus** fails before the atomic swap | **T60/T61.** `PackageBuilderTests.cs:19,20` — `Assert.StartsWith("package-budget: 'artifacts'. corpus: 'unpinned'. by-family: ", …)` / `'bytes'`; `:74` — unpinned corpus stays on `Assert.Equal(100_663_296L, budget.MaximumBytes)`; `:83` — componentwise minimum, `Assert.Equal(750, …); Assert.True(budget.MaximumBytes < PackageBudget.Default.MaximumBytes)`; `:93` — the selection reaches `Build`; `:156` — `Assert.Contains("corpus: 'pitstop.sln'.", …Message)`; `PackageBuilder.cs:127,132` throw before `PackagePublication.Publish` is ever called | ✅ PASS (boundary caveat in note 2) |
| EDG-04 | a dependency in only one Analysis Variant keeps that qualification without duplicating source/target identities | `Analysis/CausalRelationExtractorTests.cs:139` — `Extract_UsesTheInputVariantForEveryOccurrenceAndEvidence`; `DependencyAggregatorTests.cs:13` — `Assert.Equal("v", Assert.Single(…Variants).Value)` | ✅ PASS |
| EDG-05 | Markdown/machine divergence classifies the package corrupted and blocks the commit | `Publication/RetrievalModelReaderTests.cs:19,20` — divergence and absence rejected with the path; `PackagePublicationTests.cs:60` — `Publish_InvalidMarkdownPlanIsRejectedBeforeManifestSwap`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageFailureTests.cs:110` — real divergence ⇒ `code=package-corruption`, `cause=invalid-artifact`, `family=markdown`, `artifact=markdown/index.md`, package byte-identical afterwards | ✅ PASS |

**Status**: ✅ 71/71 ACs traced to a `file:line` assertion targeting the spec-defined outcome.
1 spec-precision gap flagged (DEP-06). 2 conditional passes (CRT-04, CRT-05 — corpus measurement
deferred by CRT-07's own rule). 1 residual coverage gap (PUB-08 project/variant).

---

## Findings

### Note 1 — PUB-08: `Partial` is the right status (residual, non-blocking)

The criterion says "projeto, variante, família e causa **aplicáveis**". Family, cause and artifact
are now proven on seven genuinely-failing pipelines (`PackagePublicationTests.cs:16,31` and five
end-to-end CLI rejections in `KnowledgePackageFailureTests.cs`). The production defect T65 names was
real: `PackagePublication.cs:58-66` used to collapse the validator's failure into its cause alone.

What is still unproven by a real failure is *project* and *variant*. Those coordinates are populated
for analysis-stage rejections (`src/Csharp2Md.Core/KnowledgeEngine.cs:61-67,71-76`), but the only test
asserting them feeds a hand-built `EngineDiagnostic` through a CLI stub
(`KnowledgePackageFailureTests.cs:28-36`). **Judgment: `Partial` is correct** — neither `Complete`
(project/variant never observed on a real rejection) nor `Failed` (family and cause are proven
end-to-end). Closing it needs the per-class fixture workstream T65 describes, not a diagnostic fix.

A smaller related gap T65 did not cover: `KnowledgeEngine.cs:83` folds the whole
`PackageBudgetExceededException` message into `Cause` and leaves `Family` and `Artifact` null. The
EDG-03 rejection class that T60 just made reachable therefore still reports no family field, even
though the message text carries the family breakdown and the corpus.

### Note 2 — CRT-04/CRT-05: the enforced measure is one file short of the stated measure

`PackageBuilder.cs:127` compares `ordered.Length` (the planned artifacts) against the ceiling, but the
*committed package* also contains the root `manifest.json` generation pointer written at
`PackagePublication.cs:77-82` — which `LocalCorpusAnalyzeTests.cs:118` correctly counts
(`.Append(rootManifest)`). At exactly 1,500 planned artifacts the builder accepts and the committed
package holds 1,501 files, violating CRT-04's stated ceiling. Boundary-only and far from current
sizes, but it is a real mismatch between what is enforced and what the criterion states.

### Note 3 — DEP-06 spec-precision gap

The criterion names Candidate, Unknown and Open Frontier; the only test
(`DependencyAggregatorTests.cs:12`) asserts the aggregator drops a contribution whose
`IsConfirmed` flag is false. The mapping from the three gap kinds to "not confirmed" is implicit in
the production code and asserted nowhere. MET-07 covers the three kinds separately
(`ImpactCalculatorTests.cs:9,10,11`), so the behaviour is almost certainly right — but DEP-06's own
wording is not pinned. Flagged rather than silently passed.

### Note 4 — Requirement-trait hygiene

The traceability table cannot be reproduced from the test tree by tooling:

- **CRT-07** and **CRT-09** carry no `[Trait("Requirement", …)]` anywhere. Their evidence exists
  (`LocalCorpusAnalyzeTests.cs:67,78`; `KnowledgePackageJourneyTests.cs:73-89`) but only by inspection.
- **PUB-06**'s own discriminating case is traited `CRT-08` (`PublicationSafetyScannerTests.cs:43`),
  while the two cases traited `PUB-06` cover path redaction instead.
- **PUB-07**'s pre-commit rejection evidence lives under `PUB-08` and `PKG-07` traits.
- Three whole CLI classes — `KnowledgePackageJourneyTests`, `KnowledgePackageFailureTests`,
  `LocalCorpusAnalyzeTests` — carry no requirement traits at all, yet they hold the strongest
  end-to-end evidence in the feature.

No AC is uncovered because of this; it is traceability debt, not a correctness gap.

### Note 5 — CRT-02 amendment (T66): honest, not a weakening

The amendment adds FollowFlow's causal-root precondition to the criterion. Judged against the code
and its history:

- `src/Csharp2Md.Core/Publication/Certification/GraphJourneyCertifier.cs:17-22` already excluded
  Persistence from the causal-root set, with the domain reasoning in the comment, since T51 — the
  spec was behind the code, not the other way round.
- The amendment **adds** two testable outcomes rather than removing one: the exact reason token
  `no-causal-root` (asserted at `GraphJourneyCertifierTests.cs:33` with `Assert.Equal`, not
  `StartsWith`) and ReverseImpact staying applicable on the same package (`:38`).
- It narrows *applicability*, and CRT-02's own rule is that a non-applicable journey is never marked
  passed. Nothing that previously had to pass now escapes; the Persistence-only package still cannot
  report `Passed` for FollowFlow.

**Process note, not a defect**: the spec is marked `Aprovada` and was amended by the implementer
mid-flight. The user should confirm the amended CRT-02 text.

### Note 6 — Design review of T60's corpus table (answering the brief's question directly)

`PackageBuilder.cs:20-25` keys the pinned ceilings on the solution **file name**:

```
"eShopOnContainers-ServicesAndWebApps.sln" -> (1_500, 64 MiB)
"pitstop.sln"                              -> (750, 25 MiB)
```

Verified correct on the real path: `SolutionAnalyzer.cs:35-38` builds the identity from
`PathGuard.ToLogicalPath(...)`, whose last segment is the real solution file name, so
`Path.GetFileName(...)` at `PackageBuilder.cs:37` does match a genuine eShopOnContainers or Pitstop
run, and `KnowledgeEngine.cs:37` passes no budget so `ForCorpus` really is consulted.

**But it is a fragile string match, not a sound design.** Three specific reasons:

1. The two literals are hand-copied into `PackageBuilder.cs:23-24` and again into
   `LocalCorpusAnalyzeTests.cs:132,134`, in different assemblies, with **no test asserting the two
   agree**. Change one and CRT-04/CRT-05 silently stop being enforced while the acceptance test keeps
   asserting them.
2. Failure is silent. A clone checked out under a differently-named solution, a `.slnx` variant, or a
   fork whose `.sln` was renamed falls back to the 96 MiB / 1,500 default and `DescribeCorpus` just
   reports `unpinned` — there is no diagnostic distinguishing "this corpus has no ceiling" from
   "this corpus has a ceiling we failed to recognise".
3. The corpus is a *deployment-time* fact about which repository is being analysed, but it is being
   inferred from a filename inside the production builder. A better seam would carry it as an
   explicit policy input (or at minimum share one constant with the acceptance test), leaving
   `PackageBuilder` to apply a ceiling it is given rather than to guess which corpus it is looking at.

The componentwise-minimum choice (`PackageBuilder.cs:40-42`) is sound and worth keeping: it makes a
pinned corpus strictly tighter than the default rather than merely different, which is exactly the
property iteration 1 was missing.

---

## Discrimination Sensor

**Sensor: skipped per AGENTS.md standing rule (user runs Stryker manually).**

No faults were injected, no scratch worktree was created, and nothing in the working tree was
mutated by this Verifier. `git status --porcelain` before and after this run is identical:
`M AGENTS.md` and `M docs/specs/pacote-conhecimento-util-e-confiavel.md`, both pre-existing and not
this Verifier's. The only file written by this Verifier is this report.

T60–T65 each record a hand-run fault-injection pass in their gate notes. Those are the implementer's
own claims and are **not** counted as Verifier evidence; the AC table above stands on located
assertions only.

---

## Code Quality

| Principle | Status |
| --------- | ------ |
| Minimum code | ✅ Phase 8 added 5 production methods/records and ~20 test cases for 9 named gaps |
| Surgical changes | ✅ `PackageBuilder`, `PackageContracts`, `PackagePublication`, `PackageValidator`, `KnowledgeEngine` — each edit traceable to a task |
| No scope creep | ✅ `FamilyFor`'s three added rows are the minimum PUB-08 needs and are declared in T65's note |
| No abstractions for single-use code | ⚠️ `PackageBudget.Pinned` is a two-row table behind a static lookup — see note 6 |
| Only touched files required for task | ✅ T61's `PackageBuilder.cs` / `CanonicalJson.cs` deviation is declared in the task, matching T57's precedent |
| Didn't "improve" unrelated code | ✅ |
| Matches existing patterns/style | ✅ `SolutionMeasurement`/`CorpusMeasurement` follow `FamilyMeasurement`'s validating-constructor shape; new optional ctor params keep the record backward-compatible |
| Would a senior engineer approve? | ✅ with note 6 raised in review |
| Tests map to ACs and are non-shallow | ✅ spot-checked STO-01 (literals recomputed independently — exact match) and CRT-03 (hand-computed `26_214_400`, `67_108_864`, `100_663_296`) |
| Spec-anchored outcome check | ✅ 71/71; 1 spec-precision gap flagged (DEP-06) |
| Per-layer coverage expectation | ✅ domain 1:1; CLI seam covers happy path (`KnowledgePackageJourneyTests`), edge (`LocalCorpusAnalyzeTests`) and 9 error classes (`KnowledgePackageFailureTests`) |
| Every test maps to a spec requirement | ⚠️ See note 4 — three CLI classes carry no requirement trait |
| Documented guidelines followed | ✅ `AGENTS.md`: `net10.0` asserted at `CoreTopologyTests.cs:144`; no `Microsoft.Build.*` at `:124`; no `MSBuildLocator.RegisterDefaults()` at `:110`; Roslyn pinned to `5.6.0` at `:149` |

---

## Edge Cases

- [x] EDG-01 — unresolvable evidence omitted or plan rejected
- [x] EDG-02 — over-budget journey fails naming journey and measure
- [x] EDG-03 — corpus-applicable limit now real and enforced before the swap (boundary caveat, note 2)
- [x] EDG-04 — single-variant dependency keeps its qualification without duplicating identities
- [x] EDG-05 — Markdown/machine divergence blocks the commit

---

## Gate Check

- **Build gate**: `dotnet build csharp2md.slnx --configuration Release` → exit 0, **0 Aviso(s), 0 Erro(s)**
- **Full gate**: `dotnet test csharp2md.slnx --configuration Release` → exit 0
  - `Csharp2Md.Core.Tests`: **612 passed, 0 failed, 0 skipped, 612 total** (15 s)
  - `Csharp2Md.Cli.Tests`: **81 passed, 0 failed, 2 skipped, 83 total** (2 m 10 s)
  - **Total: 693 passed, 0 failed, 2 skipped**
- **Skipped tests** (each justified):
  1. `LocalCorpusAnalyzeTests.Analyze_Pitstop_CommitsWithinFileAndByteCeilings` — Pitstop clone absent.
     CRT-07 requires the skip; it does not fail CI.
  2. `LocalCorpusAnalyzeTests.Analyze_eShopOnContainers_CommitsWithinFileAndByteCeilings` —
     eShopOnContainers clone absent. Same rule.
- **LocalCorpus gate**: `fixtures/eShop` is present and
  `Analyze_eShop_CompletesWithoutCrossProjectVariantCollision` **ran and passed** inside the full gate,
  satisfying CRT-06 and the present-clone half of CRT-07.
- **Test-count integrity**: 612 in Core matches T65's recorded count exactly; the Phase 8 progression
  588 → 593 → 599 → 602 → 608 → 611 → 612 is monotonic. No test was deleted; two pre-existing
  assertions (`Build_FailsBeforePublication…Artifact/ByteCeilingIsExceeded`) were made *stricter*, not
  weaker, by pinning the longer `corpus: '…'` message prefix.
- **Failures**: none.

---

## Requirement Traceability Audit

The table in `spec.md` reads **68 Complete / 1 Partial / 2 Unverified**. Audited row by row against
the evidence above:

| Row | Table status | Verifier's evidence supports | Verdict on the row |
| --- | --- | --- | --- |
| CRT-03 | Complete | All four dimensions asserted on values (family, solution, journey, corpus) | ✅ Supported |
| EDG-03 | Complete | Corpus-applicable limit real, wired into `Build`, and refused | ✅ Supported (boundary caveat, note 2) |
| STO-01 | Complete | Literals independently recomputed by this Verifier — exact match | ✅ Supported |
| DEP-05 | Complete | Both halves (reference reuse + single payload) asserted | ✅ Supported |
| NAV-02 | Complete | Deployment Unit row present, a link, and resolving to a written artifact | ✅ Supported |
| NAV-07 | Complete | The `: 8` arm executed for all three non-component root kinds | ✅ Supported |
| PUB-08 | Partial | Family/cause real; project/variant only via stub | ✅ Correct — `Complete` would overstate, `Failed` would understate |
| CRT-04 | Unverified | Ceiling enforced and unit-asserted; corpus run skipped per CRT-07 | ✅ Correct |
| CRT-05 | Unverified | Same | ✅ Correct |
| All other 62 rows | Complete | Located `file:line` assertion targeting the spec outcome | ✅ Supported |

**No row's status is contradicted by this Verifier's evidence.** Two documentation-level quibbles,
neither a status error: T66 lists itself as an owning task for `PKG-10` although it changed only
`spec.md`; and the `Coverage` legend contains a typo (`Incluíos` for `Incluídos`).

---

## Fix Plans (non-blocking — recommended follow-ups, not gate failures)

### Fix 1: Bind the corpus ceiling to the committed file count

- **Root cause**: `PackageBuilder.cs:127` measures the plan; CRT-04/CRT-05 measure the committed
  package, which carries one extra root `manifest.json`.
- **Fix task**: compare `ordered.Length + 1` (or state the measured set in the criterion) and add a
  boundary case at exactly the ceiling.
- **Priority**: Minor.

### Fix 2: Make the corpus key un-driftable

- **Root cause**: the same two solution-file literals live in `PackageBuilder.cs:23-24` and
  `LocalCorpusAnalyzeTests.cs:132,134` with nothing asserting they agree, and a miss is silent.
- **Fix task**: share one constant (or take the corpus as an explicit policy input) and add a case
  asserting the pinned key equals the one the acceptance test analyses through.
- **Priority**: Minor (Major if either clone is ever renamed).

### Fix 3: Carry family/corpus onto the budget rejection diagnostic

- **Root cause**: `KnowledgeEngine.cs:83` drops `Family`/`Artifact` for
  `PackageBudgetExceededException`.
- **Fix task**: give the exception structured `Family`/`Corpus` members and pass them through.
- **Priority**: Minor.

### Fix 4: Traceability traits

- **Root cause**: note 4 — two ACs untraited, two mis-traited, three CLI classes untraited.
- **Fix task**: add `[Trait("Requirement", …)]` to the CLI acceptance classes and correct PUB-06/PUB-07.
- **Priority**: Minor.

### Fix 5: Pin DEP-06 to the three named gap kinds

- **Root cause**: note 3 — the criterion names Candidate/Unknown/Open Frontier; the test asserts a
  boolean flag.
- **Fix task**: one case per gap kind proving it never reaches the confirmed count.
- **Priority**: Minor.

---

## Summary

**Overall**: ✅ Ready

**Spec-anchored check**: 71/71 ACs matched the spec-defined outcome; 1 spec-precision gap flagged
(DEP-06); 2 conditional passes (CRT-04, CRT-05 — corpus measurement deferred by CRT-07's own rule).
**Sensor**: skipped per AGENTS.md standing rule (user runs Stryker manually).
**Gate**: 693 passed, 0 failed, 2 justified skips; Release build 0 warnings / 0 errors.

**What works**: All nine gaps iteration 1 raised are genuinely closed, and I re-derived each rather
than trusting the claim. `PackageBudget.ForCorpus` is a real corpus-applicable limit that `Build`
actually consults on the production path, with the componentwise minimum making a pinned corpus
strictly tighter than the 96 MiB default. `PublicationMeasurements` now carries all four CRT-03
dimensions, asserted on values and round-tripped through `CanonicalJson`. STO-01's public-ID
derivation is pinned by literals I recomputed independently outside the codebase — they match
exactly. NAV-07's non-component arm, DEP-05's two halves, NAV-02's Deployment Unit row and PUB-08's
family/artifact coordinates are all now backed by assertions that would fail on a wrong value rather
than merely on a missing one. iteration 1's claim that eShop's 78.61 MiB violated CRT-04 does not
survive reading the spec: CRT-04 pins eShopOnContainers, and no criterion sets a size ceiling on
eShop — T60's reading is the correct one.

**Issues found**: six non-blocking items — PUB-08's project/variant coordinates still ride on a
fabricated diagnostic (note 1, correctly recorded as `Partial`); a one-file boundary mismatch between
the enforced ceiling and CRT-04/CRT-05's stated measure (note 2); DEP-06's spec-precision gap
(note 3); requirement-trait debt across the CLI acceptance classes (note 4); and the corpus table's
fragile duplicated string key (note 6). None of these invalidates a committed package or an asserted
outcome.

**Next steps**: route Fixes 1–5 as ordinary follow-up tasks rather than a fourth fix→re-verify
iteration; ask the user to confirm the T66 amendment to CRT-02; and re-run
`dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --configuration Release --filter "Category=LocalCorpus"`
when the eShopOnContainers and Pitstop clones return, to move CRT-04 and CRT-05 from `Unverified` to
`Complete`.
