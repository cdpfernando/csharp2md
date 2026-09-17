# Pacote de conhecimento útil e confiável Validation

**Date**: 2026-09-17
**Spec**: `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`
**Diff range**: `ff42c35..71e7094` (64 commits on `feature/simplif`; 731 files changed, +15476 / -85212)
**Verifier**: independent sub-agent (author ≠ verifier), evidence-or-zero
**Result**: ❌ FAIL
**Sensor**: skipped per AGENTS.md standing rule (user runs Stryker manually)

---

## Task Completion

All 59 tasks (T1-T59) are marked Complete in `tasks.md`. No task is blocked or partial. Task status was
not taken as evidence; every acceptance criterion below was re-derived from the test sources.

---

## Spec-Anchored Acceptance Criteria

Legend: ✅ PASS = a located assertion targets the spec-defined value/state · ❌ GAP = no evidence, or the
assertion is weaker/different than the spec text · ⚠️ = spec-precision gap (the spec does not pin a
precise outcome).

### P1: Publicar somente conhecimento útil

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| PKG-01 | one manifest grouped by solution; each group declares SolutionId, logical path, roots, indexes, four journeys | `tests/Csharp2Md.Core.Tests/Publication/SolutionManifestContractTests.cs:14` `PackageManifest_GroupsRootsIndexesAndJourneysUnderTypedSolutionId`; `tests/Csharp2Md.Core.Tests/PackageBuilding/MachineArtifactWriterTests.cs:11` `Write_ListsSolutionLogicalPath`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:20` `Assert.Equal(8, _run.Solution.GetProperty("indexes").GetArrayLength())`; `:24` `Assert.Equal(["evidence_disposition","follow_flow","locate","reverse_impact"], …)` | ✅ PASS |
| PKG-02 | retained graph starts at proven Component, Deployment Unit, Entry Point, Boundary Operation | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetainedGraphBuilderTests.cs:11` `Build_StartsClosureAtEveryProvenRootKind`; `tests/Csharp2Md.Core.Tests/Analysis/ArchitectureFactExtractorTests.cs:64,96,140,168` (`Extract_OutputTypeExe_CreatesDeploymentUnitWithProjectFileEvidence`, `Extract_ControllerHttpAction_EmitsEntryPointAndBoundaryOperation`, …) | ✅ PASS |
| PKG-03 | retain only facts/relations/observations/evidence that support a retained journey or explain a relevant gap | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetainedGraphBuilderTests.cs:20` `Build_ExcludesDisconnectedInventory`; `:24` `Build_RetainsReachableConfirmedRelationAndEvidence`; `tests/Csharp2Md.Core.Tests/PackageBuilding/RetentionPolicyTests.cs:16` `Assert.Contains(Apply().Relations, x => x.CanonicalKey == "relation:caller-root")` | ✅ PASS |
| PKG-04 | Candidate/Unknown/Open Frontier only when they can alter or interrupt a retained journey | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetentionPolicyTests.cs:10` `Assert.Equal("gap:relevant", Assert.Single(Apply().Gaps).CanonicalKey)`; `:12` `Apply_ExcludesGapOutsideJourney`; `:14` `Apply_OrdersGapsByAffectedRootsThenCanonicalKey` | ✅ PASS |
| PKG-05 | exclude tests, unpromoted observations, unretained type uses, uncited sources, raw config values, per-record common files | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetentionPolicyTests.cs:22` `Apply_ExcludesUncitedSource`; `:24` `Assert.Empty(Apply(includeTests:false, testEvidence:true).CitedSources)`; `tests/Csharp2Md.Core.Tests/Analysis/SourceInventoryTests.cs:81,102`; `tests/Csharp2Md.Core.Tests/PackageBuilding/ShardPackerTests.cs:15` `Pack_DoesNotEmitOneFilePerNormalRecord`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:104` `Assert.DoesNotContain("Acme.Shipping.Tests", text)` | ✅ PASS |
| PKG-06 | source content only for documents cited by retained evidence | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetentionPolicyTests.cs:20` `Assert.Equal("document:prod", Assert.Single(Apply().CitedSources).CanonicalKey)`; `tests/Csharp2Md.Core.Tests/PackageBuilding/MarkdownRendererTests.cs:32` `Render_UncitedDocumentGetsNoPage` asserts `["document:src/Billing.cs","document:src/Orders.cs"]` and no `documents/2.md` | ✅ PASS |
| PKG-07 | config by keys/sections/bindings/category/safe location; no env values, credentials, secrets, absolute paths | `tests/Csharp2Md.Core.Tests/Analysis/ConfigurationPersistenceExtractorTests.cs:14` `Extract_ConfigValueNeverEntersGraph`; `tests/Csharp2Md.Core.Tests/Analysis/IdentityPrimitivesTests.cs:96,114,155`; `tests/Csharp2Md.Core.Tests/Publication/PublicationSafetyScannerTests.cs:12` `Assert.Equal(PublicationSafetyDisposition.Retained, …)`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:107` `Assert.DoesNotContain("C:\\fixture\\synthetic-output", text)` | ✅ PASS |
| PKG-08 | when tests are explicitly enabled, record the choice in the manifest and in the run identity | `tests/Csharp2Md.Core.Tests/PackageBuilding/PackageBuilderTests.cs:14` `Assert.True(PackageBuilder.Build(Model(), true).Manifest.IncludeTests)`; `tests/Csharp2Md.Core.Tests/Analysis/SourceInventoryTests.cs:140` `AnalysisPolicy_IncludeTestsFlip_ChangesIdentityOnly`; `tests/Csharp2Md.Core.Tests/Surface/KnowledgeEngineWorkflowTests.cs:75` `AnalyzeAsync_ExplicitIncludeTestsPolicy_ReachesTheManifest`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:110` `Assert.False(_run.Manifest.GetProperty("include_tests").GetBoolean())` | ✅ PASS |
| PKG-09 | publish only observable facts and relations; no business-rule interpretation | `tests/Csharp2Md.Core.Tests/Analysis/ArchitectureFactExtractorTests.cs:234` `Extract_DoesNotEmitBusinessRuleOrQualityLabels`; `tests/Csharp2Md.Core.Tests/Analysis/FactualGraphContractTests.cs:125` `FactualGraphTypes_DoNotExposeBusinessRuleOrQualityInterpretation`; `tests/Csharp2Md.Core.Tests/Analysis/CausalRelationExtractorTests.cs:71,115` (unresolved → Candidate/Unknown, never Confirmed) | ✅ PASS |
| PKG-10 | after cutover the repo holds only the current contract — no version dispatch, legacy reader, converter, compatibility route | `tests/Csharp2Md.Core.Tests/Surface/CoreTopologyTests.cs:44` `Solution_ListsExactlyTheFourCurrentProjects`; `:63,68,79,95` `ProductSources_CarryNoVersionDispatchOrCompatibilityPath`; `:110` `ProductSources_NeverCallMSBuildLocatorRegisterDefaults`; `:124` `NoProject_ReferencesMicrosoftBuildPackages`; `:139` `Core_TargetsNet10AndReferencesRoslynWorkspaces560` | ✅ PASS |

### P1: Navegar e medir dependências

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| DEP-01 | aggregate Confirmed Relations in Document, Project, Component and Deployment Unit scopes using proven membership only | `tests/Csharp2Md.Core.Tests/PackageBuilding/ScopePairingTests.cs:17` `Assert.Equal(EvidenceDocument, Assert.Single(EdgesInto(TargetDocument)).Source.Value)`; `:29,35` project-scope source/target equal the proven owning project; `:41` `Build_ProjectEdgeIsAbsentWhenEvidenceDocumentHasNoProvenOwner`; `:52,57` component/deployment membership; `tests/.../DependencyAggregatorTests.cs:7` `Assert.Equal((AggregationScope)scope, …)` over all four scopes | ✅ PASS |
| DEP-02 | keep Project Reference, internal invocation, structural type use, HTTP, gRPC, messaging, contract and persistence separate | `tests/Csharp2Md.Core.Tests/PackageBuilding/DependencyAggregatorTests.cs:9` `Assert.Equal((DependencyCategory)category, …)` over all 8 values; `tests/Csharp2Md.Core.Tests/Analysis/CausalRelationExtractorTests.cs:12,24,47,60,83,94,104` one extractor case per category; `tests/.../RetrievalContractTests.cs:24` `DependencyCategory_IsTheClosedSetOfEightRelationCategories` | ✅ PASS |
| DEP-03 | aggregated dependency declares source, target, scope, categories, occurrence count, variants, direct/transitive nature, relation + evidence references | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetrievalContractTests.cs:57` `AggregatedDependency_DeclaresSourceTargetScopeCategoryCountVariantsAndEvidence`; `:42` `DirectDependencyAndTransitiveImpact_AreDistinctNatures`; `:139` `AggregatedDependency_NonPositiveOccurrenceCount_IsRejected`; `tests/.../DependencyAggregatorTests.cs:13` `Assert.Equal("v", …Variants).Value)` | ✅ PASS |
| DEP-04 | same source/target/scope/category → one aggregated edge with total count and deduplicated evidence | `tests/Csharp2Md.Core.Tests/PackageBuilding/DependencyAggregatorTests.cs:10` `Assert.Equal(2, Assert.Single(Aggregate([Item(),Item()])).OccurrenceCount)`; `:11` `Assert.Single(edge.Evidence); Assert.Single(edge.Relations)`; `tests/.../ScopePairingTests.cs:48` `Assert.Equal(1, …OccurrenceCount)` | ✅ PASS |
| DEP-05 | a low-level relation contributing to more than one scope reuses its reference without duplicating its factual payload | `tests/Csharp2Md.Core.Tests/PackageBuilding/CompactDependencyReferenceTests.cs:27` `Assert.Equal(2, payload.Relations.Length); Assert.Equal(2, payload.Evidence.Length); Assert.Equal(3, payload.Dependencies.Length)` (same-scope repetition); `tests/.../NavigationPayloadIndexTests.cs:11,22` single dependency/measure array path. No assertion pins the **multi-scope** case: `ScopePairingTests` builds one relation into Document/Project/Component/DeploymentUnit scopes but never asserts the four edges share one relation/evidence table row. | ❌ GAP (weaker than spec) |
| DEP-06 | confirmed count excludes Candidate, Unknown, Open Frontier | `tests/Csharp2Md.Core.Tests/PackageBuilding/DependencyAggregatorTests.cs:12` `Assert.Empty(DependencyAggregator.Aggregate([Item(confirmed:false)]))`; upstream `tests/.../CausalRelationExtractorTests.cs:71,115` prove unresolved input never becomes a Confirmed Relation | ✅ PASS |
| DEP-07 | an opened aggregated dependency yields resolvable references to its Confirmed Relations and evidence | `tests/Csharp2Md.Core.Tests/PackageBuilding/CompactDependencyReferenceTests.cs:36` `Assert.Equal("relation:a", relation.CanonicalKey)` + `Assert.Equal("0", relation.SourceCanonicalKey)` + `Assert.Equal("contract", relation.Category)`; `:50` `Assert.Equal("digest-b", evidence[0].ContentDigest)`; `:61` `Reader_RestoresCanonicalRelationAndEvidenceReferences`; `:71,98` unresolvable handle → `PackageCorruptionException` naming the artifact; `tests/.../NavigationPayloadIndexTests.cs:37,48,59,62,82` index-to-record resolution | ✅ PASS |
| DEP-08 | a name alone (project/assembly/directory) keeps scope at Project — no Service or Deployment Unit | `tests/Csharp2Md.Core.Tests/Analysis/ArchitectureFactExtractorTests.cs:17` `Extract_ProjectNameSuggestingService_WithoutExeOrHost_DoesNotCreateDeploymentUnit`; `:44` `Extract_DirectoryOrAssemblyNameAlone_NeverCreatesDeploymentUnit`; `:321` `Extract_AcmeSharedContracts_DoesNotCreateDeploymentUnitFromLibraryName` | ✅ PASS |

### P1: Publicar medidas explicáveis

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| MET-01 | fan-out = count of distinct targets reached by retained edges in the requested scope | `tests/Csharp2Md.Core.Tests/PackageBuilding/DirectMeasureCalculatorTests.cs:7` `Assert.Equal(2, For("a",[Edge("a","b"),Edge("a","c")]).FanOut)`; `:8` `Assert.Equal(1, …FanOut)` on duplicate target; `:21` `Calculate_KeepsScopesSeparate` over all four scopes | ✅ PASS |
| MET-02 | fan-in = count of distinct origins reaching the entity in the requested scope | `tests/Csharp2Md.Core.Tests/PackageBuilding/DirectMeasureCalculatorTests.cs:9` `Assert.Equal(2, For("b",[Edge("a","b"),Edge("c","b")]).FanIn)`; `:10` `Assert.Equal(1, …FanIn)` on duplicate origin | ✅ PASS |
| MET-03 | occurrence count counts confirmed contributions before edge deduplication | `tests/Csharp2Md.Core.Tests/PackageBuilding/DirectMeasureCalculatorTests.cs:11` `Assert.Equal(3, …OccurrenceCount)` before and after `Calculate`, with `Assert.Equal(1, For("a",aggregated).FanOut)` in between | ✅ PASS |
| MET-04 | cross-component counts edges whose aggregated entities belong to distinct proven components | `tests/Csharp2Md.Core.Tests/PackageBuilding/DirectMeasureCalculatorTests.cs:18` `Assert.Equal(1, For("a",[Edge("a","b",scope:Component)]).CrossComponentEdges)`; `:19` `Assert.Equal(0, …)` for a self edge | ✅ PASS |
| MET-05 | cycle participation computed over the retained directed graph in the requested scope | `tests/Csharp2Md.Core.Tests/PackageBuilding/CycleCalculatorTests.cs:6,7,8,9` acyclic/two-node/self/disconnected cases; `:10` `Calculate_IsPermutationDeterministic`; `:12` `Calculate_DoesNotMixScopes`; `:13` `Calculate_OrdersMembersCanonically` | ✅ PASS |
| MET-06 | reverse impact lists the reachable set from the entry index and declares the depth walked | `tests/Csharp2Md.Core.Tests/PackageBuilding/ImpactCalculatorTests.cs:5` `Calculate_UsesMinimumDepth`; `:6` `Calculate_ListsReachableSourceOnce`; `:7,8` diamond and cycle; `:13` `Calculate_SeparatesScopes`; `tests/.../RetrievalContractTests.cs:100` cycle + reverse impact carried separately | ✅ PASS |
| MET-07 | show relevant Candidate/Unknown/Open Frontier counts separately from confirmed measures | `tests/Csharp2Md.Core.Tests/PackageBuilding/ImpactCalculatorTests.cs:9,10,11` one case per gap kind; `:12` `Calculate_OmitsUnrelatedGap`; `tests/.../RetentionPolicyTests.cs:32` `Assert.Equal(GapKind.Unknown, Assert.Single(Apply().Gaps).Kind)`; `tests/.../RetrievalContractTests.cs:85` `ScopeMeasures_KeepGapCountsSeparateFromConfirmedFanInAndFanOut` | ✅ PASS |
| MET-08 | omit any composite score or automatic quality/risk/coupling label | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetrievalModelBuilderTests.cs:19` `Build_OmitsCompositeQualityProperty` — reflects over every `PackageBuilding*`/`Publication*` type and asserts no property name contains Score/Quality/Risk/Coupling; `:13` `Assert.Equal(1, …ReverseImpact.Single().Depth)` (raw depth, not a score) | ✅ PASS |

### P1: Recuperar respostas diretamente

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| NAV-01 | per solution exactly one identity, roots, outgoing, incoming, contract, persistence, evidence/disposition and measures index; each journey names its entry index; each index points at its logical entry with no shard choice | `tests/Csharp2Md.Core.Tests/PackageBuilding/NavigationPayloadIndexTests.cs:31` `Assert.Equal(Enum.GetValues<NavigationIndexKind>().Order(), …Indexes.Select(i => i.Kind).Order())`; `tests/.../MachineArtifactWriterTests.cs:14-21`; `tests/.../RootsIndexRoutingTests.cs:16,30,73`; `tests/.../EvidenceEntryIndexTests.cs:32,52`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:20` `Assert.Equal(8, …indexes…GetArrayLength())` | ✅ PASS |
| NAV-02 | summary presents components, **Deployment Units**, cycles, top fan-in/fan-out and the four journeys | `tests/Csharp2Md.Core.Tests/PackageBuilding/MarkdownRendererTests.cs:11` `Assert.Contains("component:orders", …)`; `:12` `Assert.Contains("fan-in 1, fan-out 1", …)`; `:13` `Assert.Contains("cycle:orders", …)`; `:14` asserts every `journey.Kind via journey.EntryIndex`. The renderer emits one combined `## Components and Deployment Units` section (`src/Csharp2Md.Core/PackageBuilding/Rendering/MarkdownRenderer.cs:49`); **no test asserts a Deployment Unit appears in the summary**. | ❌ GAP (element unasserted) |
| NAV-03 | component/service/document pages present outgoing, incoming, measures, effects and gaps with existing Markdown links | `tests/Csharp2Md.Core.Tests/PackageBuilding/MarkdownRendererTests.cs:15` `Assert.Contains("- [component:billing](0.md) (Http)", …)`; `:16,17,18`; `:23` `Render_CitedDocumentGetsItsOwnPage`; `:39,41` link vs plain text; `:48` `Render_EveryLinkResolvesToAWrittenArtifact`; `:57` `Render_SummaryReachesEveryDocumentPage` | ✅ PASS |
| NAV-04 | a normal Markdown journey needs no directory enumeration, shard choice or ID decoding | `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:13` `Assert.Equal(["manifest.json"], _run.InitialReads)`; `tests/.../NavigationIndexBuilderTests.cs:6` `Resolve_MissingKeyRejectsWithoutShardChoice`; `:11` `Resolve_DoesNotDecodeStartKey`; `tests/.../RootsIndexRoutingTests.cs:44,61` | ✅ PASS |
| NAV-05 | Markdown and machine indexes derive from the same retained set and present equivalent dependencies and measures | `tests/Csharp2Md.Core.Tests/Publication/RetrievalModelReaderTests.cs:18` `VerifyMarkdown_AcceptsEquivalentBytes`; `:19,20` divergence/absence rejected with the path; `tests/.../RetrievalModelBuilderTests.cs:10,11` single retained graph owns both writers; `tests/.../MarkdownRendererTests.cs:20` `Render_UsesMachineManifestRoots` | ✅ PASS |
| NAV-06 | locating a component completes in at most 5 reads | `tests/Csharp2Md.Core.Tests/PackageBuilding/RootsIndexRoutingTests.cs:148` `Assert.Equal(3, measurement.Reads)` at 3 and 400 component roots; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:71` `AssertBudget("locate", maximumReads: 5, …)` → `Assert.InRange(values["reads"], 1, 5)`; production bound at `src/Csharp2Md.Core/Publication/Certification/JourneyCertifier.cs:99` | ✅ PASS |
| NAV-07 | locating another supported root completes in at most 8 reads and 12,000 estimated tokens | `tests/Csharp2Md.Core.Tests/Publication/JourneyCertifierTests.cs:11` `Certify_LocateOtherRootPassesWithinEightReads` asserts **only** `Assert.Equal(JourneyCertificationStatus.Passed, Locate(package).Status)` for root `service:orders`. `RootsIndexRoutingTests.cs:148` also carries the NAV-07 trait but its fixture emits only `component:NNNN` roots (`:242`), so it exercises the 5-read branch, not the 8-read one. No assertion pins reads or tokens for a non-component root. | ❌ GAP (weaker than spec) |
| NAV-08 | causal flow to contracts, external effects and persistence completes in ≤32 reads and ≤125,000 tokens | `tests/Csharp2Md.Core.Tests/Publication/GraphJourneyCertifierTests.cs:54` `Assert.StartsWith("reads-exceeded:", result.Detail)` + `Assert.EndsWith($":{package.Paths.Count}>32", …)`; `:64` detail carries `tokens:`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:74` `AssertBudget("follow_flow", 32, 125_000)` | ✅ PASS |
| NAV-09 | reverse impact from file/project/component/Deployment Unit/contract/data completes in ≤32 reads and ≤125,000 tokens | `tests/Csharp2Md.Core.Tests/Publication/GraphJourneyCertifierTests.cs:11,35,53,65`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:77` `AssertBudget("reverse_impact", 32, 125_000)`; production bound at `src/Csharp2Md.Core/Publication/Certification/GraphJourneyCertifier.cs:57-58` | ✅ PASS |
| NAV-10 | inspecting evidence or disposition completes in ≤12 reads and ≤25,000 tokens | `tests/Csharp2Md.Core.Tests/PackageBuilding/EvidenceEntryIndexTests.cs:116` `Assert.Equal(3, measurement.Reads); Assert.InRange(measurement.Tokens, 1, 25_000)` at 3 and 400 evidence records; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:80` `AssertBudget("evidence_disposition", 12, 25_000)`; `tests/.../JourneyCertifierTests.cs:19` token estimator = `ceil(bytes/4.0)` | ✅ PASS |

### P1: Isolar projetos, variantes e soluções

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| VAR-01 | only Analysis Variants resolved by the project's own evaluation are created | `tests/Csharp2Md.Core.Tests/Analysis/ProjectVariantPlannerTests.cs:11` asserts each evaluated project/TFM pair, `Assert.Equal(plan.Length, plan.Distinct().Count())`; `:55` `DiscoverAsync_MultiTargetTempProject_DeduplicatesPerSolutionAndKeepsBothTfms`; `tests/.../ProjectVariantWorkspaceTests.cs:48` `Assert.NotEqual(left.RootProject.Id, right.RootProject.Id)` | ✅ PASS |
| VAR-02 | target frameworks collected from one project must not be applied to another project of the solution | `tests/Csharp2Md.Core.Tests/Analysis/ProjectVariantWorkspaceTests.cs:16` `Assert.Equal("net10.0", workspace.Variant.TargetFramework)` for a single opened project; `tests/.../ProjectVariantPlannerTests.cs:72,88,101,162` reject missing/ambiguous TFMs rather than guessing. The direct contamination case (project A's `net8.0` leaking to project B) is proven only indirectly, by the live eShop acceptance (CRT-06). | ✅ PASS (see CRT-06) |
| VAR-03 | compatible occurrences across variants produce one logical identity | `tests/Csharp2Md.Core.Tests/Analysis/LogicalEntityAccumulatorTests.cs:10` `Add_CompatibleOccurrencesAcrossTfms_ShareOneLogicalEntity`; `tests/.../IdentityPrimitivesTests.cs:51` `EntityKey_IsLogicalAndSharedAcrossVariants`; `:64` `EntityKey_DifferentSolutions_DoNotShareIdentity` | ✅ PASS |
| VAR-04 | locator and evidence of an occurrence declare the producing Analysis Variant | `tests/Csharp2Md.Core.Tests/Analysis/FactualGraphContractTests.cs:25` `OccurrenceAndEvidence_DeclareTheAnalysisVariantThatProducedThem`; `tests/.../ArchitectureFactExtractorTests.cs:346` `Extract_OccurrencesAreVariantQualified`; `tests/.../LogicalEntityAccumulatorTests.cs:131` `Add_MismatchedOccurrenceEntityKey_IsRejected` | ✅ PASS |
| VAR-05 | two incompatible occurrences inside the same Analysis Variant → reject the structural collision | `tests/Csharp2Md.Core.Tests/Analysis/LogicalEntityAccumulatorTests.cs:46` `Add_IncompatibleShapesWithinSameVariant_Collides`; `:63` `Add_IdenticalShapeWithinSameVariant_IsIdempotent`; `:161` `Add_ChangingLogicalMetadataAcrossVariants_IsRejected` | ✅ PASS |
| VAR-06 | batch of several solutions keeps identities, variants, dedup, handles, roots, dependencies and measures structurally isolated, even when handles and index kinds coincide | `tests/Csharp2Md.Core.Tests/PackageBuilding/SolutionScopedRetrievalTests.cs:35` `Write_TwoSolutionsWithSameHandlesAndIndexKinds_EmitsIsolatedArtifacts`; `:102` `Read_TwoSolutionsWithDifferentCausalData_DoesNotLeakAcrossSolutions`; `:125` corruption in solution 2 reports solution 2's artifact; `tests/.../LogicalEntityAccumulatorTests.cs:78,97`; `tests/Csharp2Md.Core.Tests/Surface/KnowledgeEngineWorkflowTests.cs:48,56,65` | ✅ PASS |

### P1: Persistir identidades compactas e shards estáveis

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STO-01 | public ID matches `^[a-z]{3}_[0-9a-v]{16}$`, is exactly 20 chars, and is **the first 80 bits of SHA-256 of the canonical identity in lowercase base32hex** after a unique type prefix | `tests/Csharp2Md.Core.Tests/PackageBuilding/PublicIdRegistryTests.cs:20` `Assert.Matches("^[a-z]{3}_[0-9a-v]{16}$", id)` (grammar + implied 20 chars); `:28` determinism + `Assert.StartsWith("rel_", …)`; `:54` non-canonical prefixes rejected. **No test derives the expected ID independently** (SHA-256 → first 80 bits → base32hex); the digest formula is never compared against an oracle, so a wrong hash, wrong bit slice, or wrong alphabet ordering would still match the grammar. | ❌ GAP (weaker than spec) |
| STO-02 | two different canonical inputs producing the same digest → builder fails with an explicit diagnostic before publication | `tests/Csharp2Md.Core.Tests/PackageBuilding/PublicIdRegistryTests.cs:36` `Assert.Throws<PublicIdCollisionException>`; asserts `exception.Id`, `exception.FirstCategory == "entity:other"`, `exception.SecondCategory == "entity:first"` | ✅ PASS |
| STO-03 | local handle matches `^[0-9a-z]{1,6}$`, is the base36 ordinal from `0` after canonical-key ordering, and resolves through a manifest-declared index | `tests/Csharp2Md.Core.Tests/PackageBuilding/LocalTableBuilderTests.cs:16` `Build_AssignsLowercaseBase36HandlesInCanonicalOrder`; `:25` `LocalHandle_IsLimitedToSixCharacters`; `tests/.../CompactDependencyReferenceTests.cs:11` `Assert.Equal(["0","1"], …)` after sorting `["relation:a","relation:z"]`; `tests/.../CanonicalKeyTableTests.cs:26,34,43` | ✅ PASS |
| STO-04 | one stored entry per repeated identity, document, string and evidence within the solution package | `tests/Csharp2Md.Core.Tests/PackageBuilding/CompactDependencyReferenceTests.cs:27` `Assert.Equal(2, payload.Relations.Length)` / `Assert.Equal(3, payload.Dependencies.Length)`; `tests/.../CanonicalKeyTableTests.cs:11,18`; `:86` duplicate table keys rejected; `tests/.../LocalTableBuilderTests.cs:21,22` | ✅ PASS |
| STO-05 | projection records use handles and do not repeat public ID, path or signature already in the local table | `tests/Csharp2Md.Core.Tests/PackageBuilding/RootsIndexRoutingTests.cs:24` `Assert.DoesNotContain("component:", manifestText)`; `:84,98,115,132` reader rejects row-count / ordering / handle / missing-index violations; `tests/.../CanonicalKeyTableTests.cs:66,83,96,106`; `tests/.../MachineArtifactWriterTests.cs:22` `Write_UsesOnlyRelativeNormalizedArtifactPaths` | ✅ PASS |
| STO-06 | sharding groups records deterministically by family and real byte range, never one file per common record | `tests/Csharp2Md.Core.Tests/PackageBuilding/ShardPackerTests.cs:9,13,15,17` (`Pack_DoesNotEmitOneFilePerNormalRecord`, `Pack_NeverExceedsHardCeilingForAllowedRecord`, `Pack_UsesStableOrdinalPaths`); `tests/.../EvidenceEntryIndexTests.cs:52` `Write_ShardsPartitionTheEvidenceTableIntoContiguousOrdinalRanges`; `tests/.../PackageBuilderTests.cs:15` canonical artifact order | ✅ PASS |
| STO-07 | reprocessing the same evaluated input and policy produces byte-identical solution package bytes | `tests/Csharp2Md.Core.Tests/PackageBuilding/PackageBuilderTests.cs:16` `Assert.Equal(first.PackageDigest, second.PackageDigest)` + payload equality; `tests/.../CanonicalJsonTests.cs:15,25,47`; `tests/.../MachineArtifactWriterTests.cs:23`; `tests/.../RootsIndexRoutingTests.cs:177`; `tests/.../SolutionScopedRetrievalTests.cs:80` `Write_PermutedSolutions_ProducesIdenticalOrderPathsAndBytes` | ✅ PASS |

### P1: Comprometer somente pacotes válidos

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| PUB-01 | immediate and deferred fragments go through the same materialization, normalization and validator collection | `tests/Csharp2Md.Core.Tests/Publication/PackageContractTests.cs:32` `PackagePlan_HasNoDeferredFragmentContract` (there is only one path); `:24` plan without root manifest rejected; `tests/.../PackagePublicationTests.cs:10` `Publish_MaterializesEveryPlannedArtifactInItsImmutableGeneration`; `:26` `Publish_PreservesPlanDigest` | ✅ PASS |
| PUB-02 | a staged plan is rehydrated and validated in full before the atomic swap | `tests/Csharp2Md.Core.Tests/Publication/PackagePublicationTests.cs:11` `Publish_RehydratesAndValidatesBeforeCommit`; `:20` `Validate_UsesPublishedPackageReader`; `:27` `Publish_RecordsAllFourJourneys`; `tests/.../PackageReaderTests.cs:12,17` | ✅ PASS |
| PUB-03 | the validate command reuses the same reader and rules applied before commit | `tests/Csharp2Md.Core.Tests/Publication/PackageValidatorTests.cs:11` `Validate_UsesManifestReader`; `tests/Csharp2Md.Core.Tests/Surface/KnowledgeEngineWorkflowTests.cs:98` `Validate_CommittedPackage_UsesThePublicationInterpretation`; `:108` `Validate_WhenSourceSolutionIsOffline_ReadsOnlyThePackage`; `:126` `Validate_DoesNotMutateTheCommittedPackage` | ✅ PASS |
| PUB-04 | a package announced as committed passes an immediate validation with no interpretation difference | `tests/Csharp2Md.Core.Tests/Publication/PackagePublicationTests.cs:28` `Publish_ValidationHasNoInterpretationDifference`; `:12` `Publish_ReturnsCommittedPackageOnlyAfterValidation`; `tests/Csharp2Md.Core.Tests/Surface/KnowledgeEngineWorkflowTests.cs:24` `AnalyzeAsync_CommittedResult_HasAnImmediatelyValidRootManifest`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:89` `ImmediateValidate_ReusesTheCommittedPackageInterpretation` → `Assert.Equal(0, exitCode)` + `Assert.Equal(string.Empty, stderr)` after a real `analyze` | ✅ PASS |
| PUB-05 | if variant, retention, safety, size, materialization, rehydration or validation fails, the last valid package is preserved byte for byte | `tests/Csharp2Md.Cli.Tests/KnowledgePackageFailureTests.cs:172` `AssertSnapshotEqual(before, PackageSnapshot.Read(...))` compares every file's bytes, applied by `:56,66,74,88,101` (missing manifest, invalid manifest, missing declared index, absolute path in Markdown, Markdown/machine divergence) and `:117,140` (held lock, cancellation) with `Assert.Empty(Directory.GetDirectories(..., ".staging-*"))`; `tests/.../PackagePublicationTests.cs:14,17,21` | ✅ PASS |
| PUB-06 | a C# comment beginning `//` must not be lexically classified as a UNC path | `tests/Csharp2Md.Core.Tests/Publication/PublicationSafetyScannerTests.cs:44` `RedactCSharpSource_DoesNotClassifyCommentTriviaAsPath` → `Assert.Equal(PublicationSafetyDisposition.Retained, result.Disposition)` and `Assert.Equal(source, result.Value)` for `"// C:/not/a/path…"`; `:21` unsafe paths (including `\\server\share`) are not retained and never echoed | ✅ PASS |
| PUB-07 | a real absolute path reaching the materialized plan is removed, redacted or the plan is rejected, so the value never appears in the committed package | `tests/Csharp2Md.Core.Tests/Publication/PublicationSafetyScannerTests.cs:34` `Assert.Equal("[REDACTED]", result.Value)`; `tests/Csharp2Md.Core.Tests/Publication/PackageValidatorTests.cs:18` `Validate_AbsolutePathPayloadReportsArtifact`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageFailureTests.cs:88` real `validate` on an injected `C:/forbidden/source.cs` → `publication-safety` / `absolute-path` / `family=markdown`; `tests/.../KnowledgePackageJourneyTests.cs:107` committed fixture package contains no `C:\fixture\synthetic-output` | ✅ PASS |
| PUB-08 | a rejected publication's diagnostic states the applicable project, variant, family and cause | Real-pipeline evidence: `tests/Csharp2Md.Cli.Tests/KnowledgePackageFailureTests.cs:165-171` asserts `code=`, `stage=`, `cause=`, `family=`, `artifact=` from a genuine `validate` run. But `src/Csharp2Md.Core/KnowledgeEngine.cs:84-88` builds the **publication-rejection** diagnostic with code/stage/cause only — no project, variant or family; `family` is populated only on the validate path (`:120`). The CLI theory `KnowledgePackageFailureTests.cs:22` that asserts `variant=` and `family=` for all nine rejection classes injects a **fabricated** `EngineDiagnostic` through a stub, so it proves CLI formatting, not engine production. Spec says "aplicáveis" without defining which coordinates are applicable. | ⚠️ Spec-precision gap + weak evidence |

### P1: Certificar utilidade no seam da CLI

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| CRT-01 | an applicable journey is exercised per solution and fails if any solution misses the expected answer | `tests/Csharp2Md.Core.Tests/Publication/SolutionCertificationTests.cs:14` `Certify_TwoSolutions_ProducesFourJourneysForEachSolution`; `:68` `Certify_CorruptionOnlyInSecondSolution_FailsThatSolutionWithoutMaskingIt`; `tests/.../GraphJourneyCertifierTests.cs:40` `Assert.Equal("missing-terminal:contracts", result.Detail)`; `:47,48,49,50,66`; `tests/.../JourneyCertifierTests.cs:15,16,21`; `tests/.../RootsIndexRoutingTests.cs:162` `Assert.Equal("missing-terminal", journey.Detail)` | ✅ PASS |
| CRT-02 | a non-applicable journey is recorded as not applicable with a reason, never as passed; FollowFlow/ReverseImpact applicable only with HTTP/gRPC/Messaging/Contract/Persistence | `tests/Csharp2Md.Core.Tests/Publication/GraphJourneyCertifierTests.cs:12,13` empty → NotApplicable; `:14,21` internal-invocation-only → `Assert.StartsWith("not_applicable:", result.Detail)`; `tests/.../JourneyCertifierTests.cs:13` `Assert.StartsWith("not_applicable:", journey.Detail)`; `:14` graph journeys not applicable until their certifier runs; `tests/.../EvidenceEntryIndexTests.cs:144` `Assert.Equal("not_applicable:no-evidence-index", …)`. **However** `GraphJourneyCertifierTests.cs:28` asserts `Assert.Equal("not_applicable:no-causal-root", result.Detail)` for a Persistence-only solution — the implementation adds a causal-root precondition the spec never states, and the spec's "só são aplicáveis quando …" reads as necessary, not sufficient. | ⚠️ Spec-precision gap |
| CRT-03 | record separate extraction and publication metrics, items filtered by reason, and measures **by family, solution, journey and corpus**; every journey budget starts from a zeroed measurement | Family: `tests/Csharp2Md.Core.Tests/PackageBuilding/PackageBuilderTests.cs:35` `Assert.Equal([(Table,4),(Graph,1),(Index,9),(Measure,2),(Markdown,2)], …ByFamily…)`; `:38` byte sums; `:41,47,48`. Extraction vs publication: `:17` `Assert.Equal(0, measurements.Extraction.ExtractedCount)` + `Assert.Equal(Plan().Artifacts.Length, measurements.PublishedArtifactCount)`. Filtered reason: `:18` `Assert.Equal("retention", …Reason)`. Zeroed journey budget: `tests/.../SolutionCertificationTests.cs:33` `Certify_EachJourney_RestartsReadAndTokenMeasurements`. **Missing:** `src/Csharp2Md.Core/Publication/PackageContracts.cs:227-236` shows `PublicationMeasurements` carries only `Extraction`, `PublishedArtifactCount`, `FilteredByReason`, `ByFamily` — there is **no per-solution and no per-corpus breakdown**, and `grep -i corpus src/Csharp2Md.Core` returns only two code comments. | ❌ GAP (two of four dimensions absent) |
| CRT-04 | when eShopOnContainers is present, the committed package holds at most 1,500 files and 64 MiB | Test exists at `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs:32` `AssertCommittedCeiling(package, maximumFiles: 1_500, maximumBytes: 64 * MiB)` but **SKIPPED** in this gate run (clone absent) → zero execution evidence. **Adverse evidence:** `src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs:13-16` raised the default byte budget to 96 MiB with the comment *"96 MiB, not the 64 MiB that CRT-04 pins for eShopOnContainers: rendering one Markdown page per cited document took eShop from 49.21 to 78.60 MiB"*. I re-measured this independently: `analyze` over the present `fixtures/eShop` clone committed **966 files / 82,424,721 bytes = 78.61 MiB**, i.e. 22.8% over the CRT-04 ceiling on a corpus of comparable size. | ❌ GAP (zero evidence + adverse measurement) |
| CRT-05 | when Pitstop is present, the committed package holds at most 750 files and 25 MiB | `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs:49` `AssertCommittedCeiling(package, maximumFiles: 750, maximumBytes: 25 * MiB)` — **SKIPPED** (clone absent). No execution evidence. Given the measured eShop result, the 25 MiB ceiling is at high risk. | ❌ GAP (zero evidence) |
| CRT-06 | when eShop is present, analysis completes with no collision caused by applying one project's variant to another | `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs:23` `Assert.DoesNotContain("variant-collision", stderr)` — **this case RAN and PASSED** in the gate (the eShop clone is present); I also reproduced a clean `analyze` over `fixtures/eShop/eShop.slnx` (exit 0, "committed and certified"). Supporting: `tests/Csharp2Md.Core.Tests/Analysis/ProjectVariantPlannerTests.cs:11`, `:141` | ✅ PASS |
| CRT-07 | where a clone is present the Verifier runs the matching acceptance; an absent clone must not fail CI | `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs:65` `Assert.Equal("local eShop clone is not present at '…absent-clone.slnx'.", absent.Skip)`; `:76` `Assert.Null(new LocalCorpusFactAttribute("..","csharp2md.slnx").Skip)`; `:80` gitignore + out-of-repo output. Gate observation: the two absent clones produced **named skips, 0 failures**; the present eShop case ran. | ✅ PASS |
| CRT-08 | versioned fixture contains multi-target per project, production and test code, `//` comment, absolute config path, Project Reference, cross-document call, repeated calls, cross-component dependency, runtime integration, cycle and one unconfirmed gap | `tests/Csharp2Md.Cli.Tests/SyntheticSolutionFixtureTests.cs:57` `Assert.Contains("<TargetFrameworks>net8.0;net10.0</TargetFrameworks>", …)`; `:63` test project; `:75` `Assert.True(Count(source,"CreateClient(") >= 3)`; `:83` `IEventBus` + `PaymentClient`; `:91` cycle probe; `:99` absolute path `C:\fixture\synthetic-output` + `// Hand-written SQL`; `:44` `Assert.Equal("ShippingService is unresolved", Value("expectedGap"))`; `tests/Csharp2Md.Cli.Tests/Isolation/FixtureRetentionTests.cs:7,26` | ✅ PASS |
| CRT-09 | the CLI analyzes the fixture, publishes, rehydrates, validates and completes the four journeys in the same acceptance seam | `tests/Csharp2Md.Cli.Tests/KnowledgePackageJourneyTests.cs:152` (`PackageRun.InitializeAsync` runs a real `analyze` → `Assert.Equal(0, exitCode)`), then `:13` `Assert.Equal(["manifest.json"], _run.InitialReads)`, `:71,74,77,80` the four `AssertBudget` calls asserting `Assert.Equal(0, journey.GetProperty("status").GetInt32())` plus `Assert.InRange(values["reads"], 1, max)`, and `:89` immediate `validate` → exit 0. (No `Requirement` trait; mapped by content.) | ✅ PASS |

### Edge Cases

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| EDG-01 | a relation with no resolvable evidence is omitted as a Confirmed Relation or the plan is rejected before commit | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetainedGraphBuilderTests.cs:43,45,47,49` (missing source / target / no evidence / unknown evidence all rejected); `tests/.../CompactDependencyReferenceTests.cs:109,115` `Assert.Throws<InvalidOperationException>`; `tests/Csharp2Md.Core.Tests/Analysis/CausalRelationExtractorTests.cs:127` `Extract_EveryConfirmedRelation_ResolvesEvidenceChain` | ✅ PASS |
| EDG-02 | an applicable journey over budget fails with the journey and the exceeded measure in the diagnostic | `tests/Csharp2Md.Core.Tests/Publication/GraphJourneyCertifierTests.cs:54` `Assert.StartsWith("reads-exceeded:", result.Detail)` + `Assert.EndsWith($":{package.Paths.Count}>32", result.Detail)`; `tests/.../JourneyCertifierTests.cs:17` `Assert.Contains("tokens-exceeded", Locate(package).Detail)` | ✅ PASS |
| EDG-03 | if the package exceeds the structural limit **applicable to the corpus**, publication fails before the atomic swap | `tests/Csharp2Md.Core.Tests/PackageBuilding/PackageBuilderTests.cs:19,20` `Assert.StartsWith("package-budget: 'artifacts'…"` / `'bytes'…` for an injected `PackageBudget(1, …)` / `(…, 1)`; `:21` diagnostic quotes the family breakdown; `tests/.../PackagePublicationTests.cs:23` `Publish_BudgetFailureOccursBeforePublication`. **But** the only real limit is a single global `PackageBudget.Default = (1_500, 96 MiB)` (`src/Csharp2Md.Core/PackageBuilding/PackageBuilder.cs:16`), which sits **above every per-corpus ceiling the spec names** (64 MiB / 25 MiB). No per-corpus limit exists in code or test, so the mechanism is proven only against limits the tests invent. | ❌ GAP (corpus-applicable limit not implemented) |
| EDG-04 | a dependency present in only one Analysis Variant keeps that qualification without duplicating source and target identities | `tests/Csharp2Md.Core.Tests/Analysis/CausalRelationExtractorTests.cs:140` `Extract_UsesTheInputVariantForEveryOccurrenceAndEvidence`; `tests/.../LogicalEntityAccumulatorTests.cs:46` `Add_IncompatibleShapesWithinSameVariant_Collides`; `:28` `Add_ShapeDifferenceAcrossVariants_KeepsQualifiedOccurrences`; `tests/.../DependencyAggregatorTests.cs:13` variant reference retained on the aggregated edge | ✅ PASS |
| EDG-05 | if Markdown and the machine representation diverge, validation classifies the package as corrupted and blocks the commit | `tests/Csharp2Md.Core.Tests/Publication/RetrievalModelReaderTests.cs:19,20` `VerifyMarkdown_RejectsMutatedMarkdownWithPath` / `RejectsMissingMarkdownWithPath`; `tests/.../PackagePublicationTests.cs:24` `Publish_InvalidMarkdownPlanIsRejectedBeforeManifestSwap`; `tests/Csharp2Md.Cli.Tests/KnowledgePackageFailureTests.cs:101` real `validate` → `package-corruption` / `invalid-artifact` / `family=markdown`, package byte-identical afterwards | ✅ PASS |

**Status**: ❌ Gaps present — **64 / 71** ACs matched the spec-defined outcome; **5** are gaps
(CRT-04, CRT-05, CRT-03, EDG-03, STO-01) plus **2** weaker-than-spec assertions (DEP-05, NAV-02,
NAV-07 — see ranked list); **2** spec-precision gaps flagged (CRT-02, PUB-08).

---

## Traceability Audit (T59's table in `spec.md`, 71 rows)

T59's table was derived from per-task gate notes, not from a Verifier report. Judged against the
evidence above, these rows carry a status my evidence does not support:

| Row | T59 status | Verifier finding |
| --- | --- | --- |
| CRT-03 | Complete | **Not supported.** `PublicationMeasurements` has no per-solution and no per-corpus dimension; two of the four dimensions the AC names are absent from the contract. Should read `Partial`. |
| EDG-03 | Complete | **Not supported.** No corpus-applicable structural limit exists; the default ceiling (96 MiB) is above both corpus ceilings the spec pins. Should read `Partial`. |
| STO-01 | Complete | **Not supported.** Grammar and determinism are asserted; the SHA-256 / first-80-bits / base32hex derivation is never checked against an independent oracle. Should read `Partial`. |
| DEP-05 | Complete | **Weak.** The multi-scope reuse case named by the AC is never asserted. Should read `Partial`. |
| NAV-02 | Complete | **Weak.** "Deployment Units" — one of five named summary elements — is unasserted. Should read `Partial`. |
| NAV-07 | Complete | **Weak.** The 8-read / 12,000-token budget for a non-component root is never pinned; the test tagged NAV-07 in `RootsIndexRoutingTests` only emits `component:` roots. Should read `Partial`. |
| CRT-04 | Unverified | **Correct label, understated.** Beyond "not run", there is measured counter-evidence: eShop commits at 78.61 MiB against a 64 MiB ceiling, and the default budget was deliberately raised above the spec ceiling to keep the build green. Should read `Needs Fix`, not `Unverified`. |
| CRT-05 | Unverified | **Correct** — skipped clone, zero evidence. Keep. |
| PUB-08 | Complete | **Partially supported.** Real-pipeline evidence covers the validate path only; the analyze/publication-rejection diagnostic carries no project, variant or family, and the CLI theory that asserts them feeds a fabricated diagnostic through a stub. |

The other 62 rows are supported by the evidence I located.

---

## Discrimination Sensor

**Sensor: skipped per AGENTS.md standing rule (user runs Stryker manually).**

No fault was injected, no scratch worktree was created, and nothing in the working tree was mutated.
This standing project override supersedes `validate.md`'s "never optional" wording for this repository
and is not re-decided per feature.

---

## Code Quality

| Principle | Status |
| --- | --- |
| Minimum code | ✅ |
| Surgical changes | ✅ — the diff removes 85,212 lines of superseded implementation and adds 15,476; only `src/Csharp2Md.Core`, `src/Csharp2Md.Cli`, their two test projects and `fixtures/SyntheticSolution` remain |
| No scope creep | ✅ |
| Matches patterns | ✅ — xunit + Verify stack, `[Trait("Requirement", …)]` convention, canonical JSON contracts |
| Spec-anchored outcome check (asserted values match spec) | ⚠️ — 5 gaps + 3 weak assertions, listed above |
| Per-layer Coverage Expectation met (domain 1:1 ACs; CLI happy+edge+error) | ⚠️ — domain layers map 1:1; the CLI seam covers happy, edge and error paths, but the optional-corpus layer (CRT-04/05) has no executed coverage |
| Every test maps to a spec requirement — no unclaimed tests | ❌ — 80 of 552 `[Fact]`/`[Theory]` attributes carry no `Requirement` trait: `KnowledgePackageJourneyTests` (16), `KnowledgeAnalyzeCommandTests` (15), `SyntheticSolutionFixtureTests` (13), `SolutionAnalyzerTests` (10), `KnowledgePackageFailureTests` (8), `KnowledgeValidateCommandTests` (8), `LocalCorpusAnalyzeTests` (6), `ConfigurationPersistenceExtractorTests` (4). All map to requirements by content — including the sole CRT-04/05/07/09 evidence — but the machine-readable traceability is missing exactly where it matters most. |
| Documented guidelines followed | ✅ — `AGENTS.md` (net10.0, no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults()`, versioned fixture only), verified by `tests/Csharp2Md.Core.Tests/Surface/CoreTopologyTests.cs:110,124,139` |

Additional observations (non-blocking):

- `tests/Csharp2Md.Core.Tests/Publication/PublicationSafetyScannerTests.cs:57`
  `RedactCSharpSource_OnlyRedactsUnsafeLiteralValues` asserts that the harmless literal
  `"// harmless text"` is **Redacted**. The behaviour is safe-by-default but contradicts the test's own
  name, and the over-redaction of `//`-bearing string literals is locked in by the assertion.
- `tests/Csharp2Md.Core.Tests/PackageBuilding/PackageBuilderTests.cs:11,12,10,13` carry PKG-04, PKG-05,
  PKG-03 and PKG-06 traits on assertions about `measurements.json` / `certification.json` presence and
  non-empty payloads, which have nothing to do with those criteria. The real evidence for all four
  lives in `RetentionPolicyTests` / `RetainedGraphBuilderTests`; the misplaced traits inflate the
  apparent coverage of the trait index.

---

## Gate Check

- **Build gate**: `dotnet build csharp2md.slnx --configuration Release`
  - Result: **exit 0** — `Compilação com êxito. 0 Aviso(s), 0 Erro(s)`, 6.52 s.
    Outputs: `Csharp2Md.Core.dll`, `Csharp2Md.Cli.dll`, `Csharp2Md.Core.Tests.dll`, `Csharp2Md.Cli.Tests.dll`.
- **Test gate**: `dotnet test csharp2md.slnx --configuration Release --no-build`
  - Result: **exit 0**
  - `Csharp2Md.Core.Tests.dll (net10.0)`: **588 passed, 0 failed, 0 skipped, 588 total**, 14 s
  - `Csharp2Md.Cli.Tests.dll (net10.0)`: **81 passed, 0 failed, 2 skipped, 83 total**, 2 m 8 s
  - **Total: 669 passed, 0 failed, 2 skipped, 671 total**
- **Skipped tests (each justified)**:
  - `LocalCorpusAnalyzeTests.Analyze_eShopOnContainers_CommitsWithinFileAndByteCeilings` — clone absent
    (`fixtures/eShopOnContainers`). Permitted by CRT-07 and `AGENTS.md`; does not fail CI. But it leaves
    CRT-04 with zero execution evidence.
  - `LocalCorpusAnalyzeTests.Analyze_Pitstop_CommitsWithinFileAndByteCeilings` — clone absent
    (`fixtures/Pitstop`). Same justification; leaves CRT-05 with zero execution evidence.
  - `LocalCorpusAnalyzeTests.Analyze_eShop_CompletesWithoutCrossProjectVariantCollision` **was not
    skipped** — the eShop clone is present, the case ran and passed (CRT-06).
- **Test count before feature**: not comparable — the diff replaces the entire suite (85,212 lines
  deleted, whole legacy test projects removed). **After: 671 collected.** No assertion weakening was
  found in the retained tests; the new suite asserts values, not call occurrence, in every case I read.
- **Failures**: none.
- **Out-of-gate measurement (Verifier, read-only)**: `analyze --solution fixtures/eShop/eShop.slnx`
  into a temp directory outside the repository → exit 0, "Knowledge package committed and certified",
  committed package **966 files / 82,424,721 bytes / 78.61 MiB**. Temp output removed afterwards;
  `git status --porcelain` unchanged (only the pre-existing `AGENTS.md` and
  `docs/specs/pacote-conhecimento-util-e-confiavel.md` modifications, which are not mine).

---

## Fix Plans

### Fix 1: CRT-04 / CRT-05 — the committed package exceeds the spec's corpus ceilings

- **Priority**: Blocker
- **Root cause**: T58 ("render document pages and link entity rows", `8295dd0`) added one Markdown page
  per cited document. That grew the eShop package from 49.21 MiB to the 78.61 MiB I measured. Rather
  than reducing the output, `PackageBuilder.cs:13-16` raised the default byte budget to 96 MiB with a
  comment acknowledging it is "not the 64 MiB that CRT-04 pins". The ceilings are now enforced nowhere
  in production code, and the only tests that would catch the breach are skipped.
- **Fix task**: Bring the committed package back under 64 MiB for an eShopOnContainers-class corpus and
  25 MiB for Pitstop — by bounding or sharding per-document Markdown, or by making document pages
  derivable rather than materialized — and restore `PackageBudget.Default` to a value at or below the
  tightest corpus ceiling the spec pins, with a per-corpus limit where the corpus is known.
- **Verify**: with the clones present, `dotnet test tests/Csharp2Md.Cli.Tests --filter "Category=LocalCorpus"`
  passes without skips; and a fresh `analyze` over `fixtures/eShop` commits under 64 MiB.
- **Done when**: no default budget exceeds a spec-pinned corpus ceiling; CRT-04 and CRT-05 have
  executed evidence.

### Fix 2: EDG-03 — no corpus-applicable structural limit exists

- **Priority**: Major
- **Root cause**: `PackageBudget` is a single global pair. The tests only prove that *an injected*
  budget causes a pre-publication failure; nothing ties a limit to a corpus.
- **Fix task**: Introduce the corpus-applicable limit EDG-03 names and assert that exceeding it fails
  before the atomic swap, using the spec's own numbers rather than test-invented ones.

### Fix 3: CRT-03 — measures are not broken down by solution or corpus

- **Priority**: Major
- **Root cause**: `PublicationMeasurements` (`PackageContracts.cs:227`) carries only extraction,
  published count, filtered-by-reason and by-family. The AC names four dimensions.
- **Fix task**: Add the per-solution and per-corpus breakdowns (or amend the AC if the product intends
  only family + journey), then assert each dimension's values, not just its presence.

### Fix 4: STO-01 — the public ID derivation is unverified

- **Priority**: Major
- **Root cause**: Tests pin the grammar and determinism but never compute the expected ID
  independently, so a wrong digest, bit slice or alphabet would pass.
- **Fix task**: Add one case that computes SHA-256 of a known canonical key in the test, takes the first
  80 bits, encodes lowercase base32hex, and asserts `PublicIdRegistry.Register` returns exactly that.

### Fix 5: NAV-07 — the non-component locate budget is never pinned

- **Priority**: Minor
- **Fix task**: Extend `RootsIndexRoutingTests` (or `JourneyCertifierTests:11`) with a non-`component:`
  root and assert the measured reads and tokens against the 8 / 12,000 bounds, as NAV-06 and NAV-10 do.

### Fix 6: DEP-05 and NAV-02 — weaker-than-spec assertions

- **Priority**: Minor
- **Fix task**: (a) assert that the four scope-paired edges built from one relation in
  `ScopePairingTests` share a single relation and evidence table row; (b) assert the Markdown summary
  lists a Deployment Unit, not only a component.

### Fix 7: PUB-08 — publication-rejection diagnostics lack coordinates; CLI theory uses a stub

- **Priority**: Minor
- **Fix task**: Populate project / variant / family on `PackagePublicationException`-derived diagnostics
  in `KnowledgeEngine.AnalyzeAsync`, and replace at least the variant and family cases in
  `KnowledgePackageFailureTests.cs:22` with real pipeline failures rather than a fabricated
  `EngineDiagnostic`.

### Fix 8: Traceability hygiene

- **Priority**: Minor
- **Fix task**: Add `[Trait("Requirement", …)]` to the 80 untagged cases (especially the CLI acceptance
  suite that solely owns CRT-04/05/07/09), and move the four misplaced PKG-03/04/05/06 traits in
  `PackageBuilderTests.cs:10-13` onto assertions that actually target those criteria.

---

## Requirement Traceability Update

| Requirement | Previous status (T59) | New status |
| --- | --- | --- |
| CRT-04 | Unverified | ❌ Needs Fix (measured counter-evidence) |
| CRT-05 | Unverified | ❌ Needs Fix (no execution evidence) |
| CRT-03 | Complete | ⚠️ Partial |
| EDG-03 | Complete | ⚠️ Partial |
| STO-01 | Complete | ⚠️ Partial |
| DEP-05 | Complete | ⚠️ Partial |
| NAV-02 | Complete | ⚠️ Partial |
| NAV-07 | Complete | ⚠️ Partial |
| PUB-08 | Complete | ⚠️ Partial (spec-precision) |
| CRT-02 | Complete | ⚠️ Spec-precision gap |
| all other 61 | Complete | ✅ Verified |

---

## Summary

**Overall**: ❌ Not Ready

**Spec-anchored check**: 64 / 71 ACs matched the spec outcome · 2 spec-precision gaps flagged
**Sensor**: skipped per AGENTS.md standing rule (user runs Stryker manually)
**Gate**: 669 passed, 0 failed, 2 skipped (absent optional clones — not a failure)

**What works**: the contract cutover is complete and enforced by topology tests; retention, scope
pairing, aggregation, measures, cycles and reverse impact are asserted against hand-built oracles that
never call production code to form expectations; identity, handles, sharding and byte determinism are
pinned; atomic publication preserves the prior package byte for byte across seven distinct failure
classes; the four journeys certify end-to-end through the real CLI on the versioned fixture; and the
eShop variant-isolation acceptance ran live and passed.

**Issues found**: the package no longer fits the size contract it promises. T58's per-document Markdown
pages took the eShop package to 78.61 MiB — 22.8% over CRT-04's 64 MiB ceiling — and the response was
to raise the builder's default budget to 96 MiB, above every ceiling the spec pins, instead of shrinking
the output. Because both corpus tests skip for absent clones, nothing in the green gate reveals this.
Alongside it: CRT-03 is missing two of its four measurement dimensions, EDG-03's corpus-applicable limit
does not exist in code, and STO-01's digest derivation is asserted only by grammar.

**Next steps**: route Fixes 1-4 to an implementer as blocking/major work; Fixes 5-8 can follow. Re-verify
with the eShopOnContainers and Pitstop clones present so CRT-04 and CRT-05 produce real evidence.
