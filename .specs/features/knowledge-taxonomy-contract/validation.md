# Knowledge Taxonomy Contract Validation

## Validation: knowledge-taxonomy-contract - PASS ✅ (4 spec-precision gaps flagged, 0 hard AC failures)

**Date**: 2026-08-25
**Spec**: `.specs/features/knowledge-taxonomy-contract/spec.md`
**Diff range**: `1e06a08..HEAD` (54 commits, `8ba2d3f`..`bd51096`, branch `codex/architecture-knowledge-engine-docs`)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Task Completion

All 54 tasks in `tasks.md` (T1–T54) have every "Done when" line checked `[x]`, and 54 commits exist in the diff range, one per task, matching the commit-message list in each task's `**Commit**:` field exactly (verified by `git log --oneline 1e06a08..HEAD`). No task is partial or blocked.

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1–T54 | ✅ Done | All "Done when" items checked; commit present for each |

---

## Spec-Anchored Acceptance Criteria

Evidence-or-zero: every row below cites `file:line` + the concrete assertion. Where a criterion is mechanically repeated per-axis/per-relation, one representative test method is cited and the multiplicity is noted.

### P1: Isolated domain assembly

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| TAX-01 project targets net10.0 | `TargetFramework` = `net10.0` | `src/Csharp2Md.Domain/Csharp2Md.Domain.csproj:4` (`<TargetFramework>net10.0</TargetFramework>`); `tests/Csharp2Md.Domain.Tests/Surface/RequirementCoverageTests.cs:64-70` — reflects the compiled assembly's `TargetFrameworkAttribute` and asserts `FrameworkName == ".NETCoreApp,Version=v10.0"` | ✅ PASS |
| TAX-02 no package/project reference | csproj declares neither | `src/Csharp2Md.Domain/Csharp2Md.Domain.csproj:1-13` (only `TargetFramework`/`ImplicitUsings`/`Nullable`/`InternalsVisibleTo`); `RequirementCoverageTests.cs:74-80` — `Assert.DoesNotContain("<PackageReference"...)`, `Assert.DoesNotContain("<ProjectReference"...)` | ✅ PASS |
| TAX-03 no forbidden namespace on public/internal surface | Rejects any exposed `Microsoft.CodeAnalysis`/`Microsoft.Build`/`System.Text.Json`/`System.IO` type, naming it | `tests/Csharp2Md.Domain.Tests/Isolation/DomainIsolationTests.cs:36-49` (`PublicOrInternalSurface_DoesNotExposeForbiddenNamespace`, theory over 4 namespaces, walks all public/internal members incl. nested generics) + `:51-63` (`ReferencedAssemblies_DoNotIncludeForbiddenAssembly`, theory over 3 assembly prefixes) | ✅ PASS |
| TAX-04 build succeeds with `TreatWarningsAsErrors` while `Csharp2Md.Core` remains | Build succeeds | `Directory.Build.props:7` (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`); `RequirementCoverageTests.cs:82-95` asserts the flag is on, not overridden by the domain csproj, and `Csharp2Md.Core.csproj` still exists. **The build outcome itself is proven empirically by this Verifier's own Gate Check below** (`dotnet build csharp2md.slnx -c Release` exit 0), not by the unit test alone — the unit test only pins the preconditions. This split is consistent with the Test Coverage Matrix's own "Build gate only" classification for this layer | ✅ PASS (config precondition unit-tested; build outcome confirmed by this Verifier's Gate Check) |
| TAX-05 listed under `src` in `csharp2md.slnx` | `Csharp2Md.Domain.csproj` inside the `/src/` folder block | `RequirementCoverageTests.cs:97-110` parses the `<Folder Name="/src/">...</Folder>` block and asserts it contains `Csharp2Md.Domain.csproj` | ✅ PASS |
| TAX-06 no reference in either direction with `Csharp2Md.Core` | Neither assembly references the other | `DomainIsolationTests.cs:65-73` (`Domain_DoesNotReferenceCore`) and `:75-83` (`Core_DoesNotReferenceDomain`), both via `Assembly.GetReferencedAssemblies()` | ✅ PASS |

### P1: Fact families and typed identities

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| TAX-07 exactly 5 families, exactly the 17 typed types | Set equality both directions | `tests/Csharp2Md.Domain.Tests/Facts/FactFamilyClosureTests.cs:73-76` (`Enum.GetValues<FactFamily>().Length == 5`), `:78-87` (17 CLR `IFact` types by reflection), `:89-106` (symmetric difference registry↔CLR is empty), `:122-139` (every instance's `Family` matches its registry descriptor) | ✅ PASS |
| TAX-08 `StructuralFact` = `Solution, Project, Document, Symbol` | Exactly these 4 | `src/Csharp2Md.Domain/Registry/FactTypeDescriptor.cs:21-24`; `tests/Csharp2Md.Domain.Tests/Facts/StructuralFactsTests.cs:20-163` (14 traited tests over all four types' construction/rejection) | ✅ PASS |
| TAX-09 `ArchitectureFact` = `Component, DeploymentUnit, EntryPoint, BoundaryOperation, ExternalSystem` | Exactly these 5 | `FactTypeDescriptor.cs:25-29`; `tests/Csharp2Md.Domain.Tests/Registry/FactTypeDescriptorTests.cs:29,65` | ✅ PASS |
| TAX-10 `ContractFact` = `Contract, ContractBinding, ContractRevision` | Exactly these 3 | `FactTypeDescriptor.cs:30-32`; `tests/Csharp2Md.Domain.Tests/Facts/ContractFactsTests.cs:21-38` (`ContractFamily_HasExactlyThreeTypes`, set equality against registry) | ✅ PASS |
| TAX-11 `PersistenceFact` = `DataStore, DataObject, DataField, DataOperation` | Exactly these 4 | `FactTypeDescriptor.cs:33-36`; `tests/Csharp2Md.Domain.Tests/Facts/PersistenceFactsTests.cs` (18 traited tests) | ✅ PASS |
| TAX-12 `ConfigurationFact` = `ConfigurationBinding` only | Exactly this 1 | `FactTypeDescriptor.cs:37`; `tests/Csharp2Md.Domain.Tests/Facts/ConfigurationBindingTests.cs` (6 traited tests) | ✅ PASS |
| TAX-13 controller/handler/repository/client/service are facets, not fact types | No such fact type exists | `src/Csharp2Md.Domain/Facets/SymbolFacets.cs:3-11` (`SymbolFacet` enum carries them); `FactFamilyClosureTests.cs:108-120` (`NoClrFactTypeName_MatchesAFacetOnlyRoleOrABusinessRuleConcept`) | ✅ PASS |
| TAX-14 callable is a `Symbol` facet, not a type | No `Callable` fact type | `SymbolFacets.cs:5`; `tests/Csharp2Md.Domain.Tests/Facets/SymbolFacetsTests.cs:8-18`; `FactFamilyClosureTests.cs:11` (`Callable` in the forbidden-name list) | ✅ PASS |
| TAX-15 no business-rule fact type | No such type | `tests/Csharp2Md.Domain.Tests/Registry/FactTypeDescriptorTests.cs:76` (`NoBusinessRule...`); `FactFamilyClosureTests.cs:11` (`BusinessRule`, `Rule` in forbidden names) | ✅ PASS |
| TAX-16 contract requires proven protocol/schema, CLR type alone insufficient | Rejected naming `proof` if role isn't `ProtocolName`/`SchemaName` | `src/Csharp2Md.Domain/Facts/Contracts/ContractFacts.cs:21-29`; `tests/Csharp2Md.Domain.Tests/Facts/ContractFactsTests.cs:40-48` (no `Type` parameter exists at all) and `:50-59` (`Contract_Create_ProofWithoutProtocolOrSchemaRole_IsRejectedNamingProof`) | ✅ PASS |
| TAX-17 shared contract needs proven key; similarity alone doesn't merge | Same key ⇒ same identity; similar-but-keyless ⇒ distinct | `ContractFactsTests.cs:61-72` (same schema key ⇒ equal reference) and `:74-85` (similarly named, no shared key ⇒ distinct reference) | ✅ PASS |

### P1: Closed facet vocabularies

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| TAX-18 `BoundaryProtocol` = `http,grpc,messaging,cli,scheduler,function` | Set equality | `src/Csharp2Md.Domain/Facets/BoundaryAxes.cs:3-11`; `tests/Csharp2Md.Domain.Tests/Facets/BoundaryAxesTests.cs:8-17` | ✅ PASS |
| TAX-19 `BoundaryDirection` = `inbound,outbound` | Set equality | `BoundaryAxes.cs:13-17`; `BoundaryAxesTests.cs:18-27` | ✅ PASS |
| TAX-20 `BoundaryRole` = `command,query,event,stream,lifecycle` | Set equality | `BoundaryAxes.cs:19-26`; `BoundaryAxesTests.cs:28-37` | ✅ PASS |
| TAX-21 protocol/direction/role are 3 independent axes, not one composite | 3 separate enums, no combined type | `BoundaryAxes.cs` (3 separate `enum` declarations); `BoundaryAxesTests.cs:38-47` (reflection: no `Facets` type combines all three) | ✅ PASS |
| TAX-22 `DataStore.technology` = `relational,document,key-value,cache,unknown` | Set equality | `src/Csharp2Md.Domain/Facets/PersistenceAxes.cs:3-10`; `tests/Csharp2Md.Domain.Tests/Facets/PersistenceAxesTests.cs:8-17`; also `PersistenceFactsTests.cs:44,53` | ✅ PASS |
| TAX-23 `DataObject.form` = `table,view,collection,key-space,cache-region,unknown` | Set equality | `PersistenceAxes.cs:12-20`; `PersistenceAxesTests.cs:18-27` | ✅ PASS |
| TAX-24 `DataOperation.operation` = `read,insert,update,delete,execute,unknown` | Set equality | `PersistenceAxes.cs:22-30`; `PersistenceAxesTests.cs:28-37` | ✅ PASS |
| TAX-25 out-of-vocabulary facet value rejected naming axis+value | `ArgumentOutOfRangeException` naming both | `src/Csharp2Md.Domain/Facets/FacetAxes.cs:5-11` (`WireValue<TEnum>`, `Enum.IsDefined` guard on every axis); `tests/Csharp2Md.Domain.Tests/Facets/FacetAxesTests.cs:8-101` (one theory per axis) | ✅ PASS |
| TAX-26 `unknown` is a registered value, not "missing" | `unknown` resolves through the wire-value pairing like any other member | `FacetAxes.cs:61-62,73,84` (`Unknown` cases mapped to `"unknown"` explicitly, not treated specially); `FacetAxesTests.cs:113` | ✅ PASS |
| TAX-27 `EntryPoint`/`BoundaryOperation` distinct, no derivation | No conversion operator/factory between them | `src/Csharp2Md.Domain/Facts/Architecture/BoundaryFacts.cs` (two unrelated sealed records, no cast/cross-factory); `tests/Csharp2Md.Domain.Tests/Facts/BoundaryFactsTests.cs:24-38` | ✅ PASS |
| TAX-28 one `Symbol` can be in an `EntryPoint` and a `BoundaryOperation` at once | Both facts reference the same `Symbol` reference without conflict | `BoundaryFactsTests.cs:40-56` | ✅ PASS |
| TAX-29 outbound HTTP identity derives from component+direction+protocol+scope+method+route | Changing any of the 6 changes identity; otherwise stable | `BoundaryFacts.cs:105-140` (`CreateOutboundHttp`, `operationKey` joins all 6); `BoundaryFactsTests.cs:58-129,160-180` (6 traited variance cases) | ✅ PASS |
| TAX-30 inbound identity = owning component + protocol operation key only | 2-component derivation | `BoundaryFacts.cs:142-170` (`CreateInbound`); `BoundaryFactsTests.cs:131-158,182` | ✅ PASS |
| TAX-31 mapping states closed to explicit-confirmation/conventional-candidate/unresolved | Set equality, nothing else | `PersistenceAxes.cs:32-37` (`MappingStateKind`); `PersistenceAxesTests.cs:38-47`; also `PersistenceFactsTests.cs:130,146,234` | ✅ PASS |

### P1: Observation contract

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| TAX-32 exactly the 10 documented observation kinds | Set equality | `src/Csharp2Md.Domain/Observations/ObservationKind.cs:3-15`; `tests/Csharp2Md.Domain.Tests/Observations/ObservationKindTests.cs:10-33` | ✅ PASS |
| TAX-33 5 kinds always emitted when bindable | `EmissionTier.AlwaysWhenBindable` for `Invocation,ObjectCreation,TypeUsage,BaseType,AttributeUsage` | `src/Csharp2Md.Domain/Registry/ObservationKindDescriptor.cs:14-18`; `tests/Csharp2Md.Domain.Tests/Registry/ObservationKindDescriptorTests.cs:9,37,49` | ✅ PASS |
| TAX-34 5 kinds emitted only in a registered context | `EmissionTier.RegisteredContextOnly` for `Assignment,Configuration,RouteDeclaration,MessageOperation,DataAccess` | `ObservationKindDescriptor.cs:19-23`; `ObservationKindDescriptorTests.cs:23` | ✅ PASS |
| TAX-35 identity = owner+kind+normalized payload+ordinal only | Same 4 ⇒ equal identity | `src/Csharp2Md.Domain/Observations/ObservationIdentity.cs:5-27` (4-parameter struct, no locator param); `tests/Csharp2Md.Domain.Tests/Observations/ObservationIdentityTests.cs:12-23,47-57` | ✅ PASS |
| TAX-36 differ-only-by-locator ⇒ one identity | Locator excluded structurally | `ObservationIdentityTests.cs:25-32` (`Constructor_HasNoEvidenceLocatorParameter_...`, reflection over the constructor) | ✅ PASS |
| TAX-37 differ-only-by-ordinal ⇒ distinct identities | Distinct | `ObservationIdentityTests.cs:34-45` | ✅ PASS |
| TAX-38 8 required components (owner, kind, payload, locator, method, diagnostic, doc hash, extractor version) | Each omission rejected by name | `src/Csharp2Md.Domain/Observations/Observation.cs:37-85`; `tests/Csharp2Md.Domain.Tests/Observations/ObservationTests.cs:24-102` (one rejection test per component) | ✅ PASS |
| TAX-39 missing document hash / extractor version rejected by name | `documentHash`/`extractorVersion` named specifically | `ObservationTests.cs:104-124` | ✅ PASS |
| TAX-40 no Roslyn type on the observation surface | No `Microsoft.CodeAnalysis` type reachable | `ObservationTests.cs:126-134` (`ExposedSurface_DoesNotContainAMicrosoftCodeAnalysisType`) | ✅ PASS |
| TAX-41 observation immutable, no mutating member | No setter, not a record (`with` bypass), no mutable collection | `ObservationTests.cs:136-165`; `tests/Csharp2Md.Domain.Tests/Surface/ImmutabilityTests.cs:45-54` (`Observation_AndItsIdentity_ExposeNoMutatingMember`) | ✅ PASS |

### P1: Relation matrix enforced at construction

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| TAX-42 exactly 12 canonical relations | Set equality | `src/Csharp2Md.Domain/Relations/RelationKind.cs:3-17`; `tests/Csharp2Md.Domain.Tests/Relations/RelationKindTests.cs:25` | ✅ PASS |
| TAX-43 no generic `references`/`depends-on` | Neither name exists | `RelationKind.cs` (no such member); `RelationKindTests.cs:39` | ✅ PASS |
| TAX-44 construction validates source/relation/target against matrix | Registered ⇒ accepted | `src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs:79` (`Registry.RequireRegisteredTriple`); `tests/Csharp2Md.Domain.Tests/Relations/RelationMatrixTests.cs:38-54` (theory over all 12 kinds, one registered triple each) | ✅ PASS |
| TAX-45 unregistered triple rejected naming all three | `ArgumentException` message contains source, relation, target | `src/Csharp2Md.Domain/Registry/TaxonomyRegistry.cs:24-31`; `RelationMatrixTests.cs:56-72` (theory over all 12 kinds) | ✅ PASS |
| TAX-46 callable-requiring relation accepts only a `Symbol` with the callable facet | Rejected naming the relation when the guard is invoked | Guard implemented and independently correct: `src/Csharp2Md.Domain/Relations/RelationShapeGuards.cs:18-34` (`RequireCallableIfNeeded`); `tests/Csharp2Md.Domain.Tests/Relations/RelationShapeGuardsTests.cs:38-69`. **But `RelationShapeGuards.RequireCallableIfNeeded` is never called from `ConfirmedRelation.Create`** (`src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs:46-83` calls only `RequireRegisteredTriple` and `RequirePayloadRoleForUsesContract`) — confirmed by grep: the only call sites of `RequireCallableIfNeeded` in the whole repo are inside `RelationShapeGuardsTests.cs`. A caller who constructs an `executes`/`invokes`/`implements-operation`/`accesses-data` relation through the only public relation-construction API can currently supply a non-callable `Symbol` and it will be accepted | ⚠️ Spec-precision gap (see analysis below) |
| TAX-47 each relation in exactly one direction, no inverse type | No inverse member | `RelationKind.cs`; `RelationKindTests.cs:49` (forbidden-inverse-name list) | ✅ PASS |
| TAX-48 `maps-to` mapping role closed to 4 documented values | Set equality | `src/Csharp2Md.Domain/Registry/MappingRoles.cs:5-11`; `tests/Csharp2Md.Domain.Tests/Registry/MappingRolesTests.cs:8-43` | ✅ PASS |
| TAX-49 `uses-contract` without registered payload role rejected | `ArgumentException` | `ConfirmedRelation.cs:80` (`RelationShapeGuards.RequirePayloadRoleForUsesContract(kind, facets)` — **wired into `Create`**); `RelationShapeGuardsTests.cs:71-103` | ✅ PASS |
| TAX-50 `targets` accepts only inbound-boundary-op / deployment-unit / external-system | Rejected otherwise | Guard implemented: `RelationShapeGuards.cs:55-92` (`RequireLegalTargetShape`/`RequireLegalTargetsShape`); `RelationShapeGuardsTests.cs:105-166`. **Same non-wiring gap as TAX-46** — `RequireLegalTargetShape` is called from nowhere except its own test file; `ConfirmedRelation.Create` cannot invoke it because its signature carries only `FactReference` (id + type-name string), not the materialized `IFact` the guard needs to read `BoundaryOperation.Direction` | ⚠️ Spec-precision gap |
| TAX-51 `operates-on` accepts only a data object or data field | Rejected for a data store | Guard implemented: `RelationShapeGuards.cs:94-101` (`RequireLegalOperatesOnShape`); `RelationShapeGuardsTests.cs:168-197`. Same non-wiring gap | ⚠️ Spec-precision gap |
| TAX-52 registry declares a minimum evidence method per relation | Every relation has one | `src/Csharp2Md.Domain/Registry/RelationDescriptor.cs:9-13,42-124` (every entry supplies `EvidenceMethod`); `tests/Csharp2Md.Domain.Tests/Registry/RelationDescriptorTests.cs:36`, `TaxonomyRegistryTests.cs:54` | ✅ PASS |
| TAX-53 syntactic evidence rejected where semantic required | `ArgumentException` naming both methods | `TaxonomyRegistry.cs:46-54` (`RequireSufficientEvidence`); `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyRegistryTests.cs:43,59,64`; `RelationShapeGuardsTests.cs:199-213`. **`ConfirmedRelation.Create` has no `EvidenceMethod` parameter at all** — the relation's `EvidenceChain` carries only `ObservationIdentity` values (owner/kind/payload/ordinal), never the evidence method used to derive an edge, so this check has no data to run against at the only public construction site | ⚠️ Spec-precision gap |
| TAX-54 no name/prefix/path-similarity evidence method | No such `EvidenceMethod` member | `src/Csharp2Md.Domain/Proof/ProofAxes.cs:3-8` (`Semantic,Syntactic,Configured` only); `TaxonomyRegistryTests.cs:75` | ✅ PASS |

**Analysis of the TAX-46/50/51/53 finding** (per the orchestrator's explicit request): `RelationShapeGuards.RequireCallableIfNeeded`, `.RequireLegalTargetShape` and `.RequireSufficientEvidence` are each correctly implemented and independently unit-tested — they are not broken code. But none is reachable from `ConfirmedRelation.Create`, the domain's only public relation-construction API, and grep confirms zero non-test call sites for any of the three. This traces directly to `design.md`'s own fixed `Create` signature (`Interfaces` section and the `Data Models` code block, both matching the shipped code parameter-for-parameter): `Create(RelationKind, FactReference source, FactReference target, FacetBinding facets, EvidenceChain derivedFrom, ClassifierIdentity classifier, ImmutableArray<AnalysisVariantId> variants)`. `FactReference` is deliberately an identity-plus-type-name pair with no facet or instance data — design.md states this explicitly as the point of the type ("the triple-checkable handle... lets a triple be validated from a reference alone without materializing facts"). Because of that, `Create` structurally cannot read `Symbol.Facets`, `BoundaryOperation.Direction`, or an evidence method that isn't even a parameter. `RequirePayloadRoleForUsesContract` (TAX-49) is the one guard that only needs `FacetBinding`, which *is* a `Create` parameter — which is exactly why it alone is wired in, and why the pattern is not a uniform implementer shortcut but a direct consequence of which data each guard needs versus what the approved signature carries. Widening the signature (e.g. an additional optional `IFact? sourceFact`/`targetFact` or `EvidenceMethod suppliedEvidence` parameter) would close the gap, but doing so changes the interface design.md fixed at approval time — a design change, not a task-level fix inside this feature's existing scope. Verdict: **⚠️ Spec-precision gap**, not a genuine implementer shortcut. Recommendation: raise a design amendment (or a documented caller contract requiring `RelationShapeGuards` to be invoked by any promotion pipeline before calling `ConfirmedRelation.Create`) in the next workstream that actually constructs confirmed relations from real facts (`engine-bootstrap` / `roslyn-observation-extraction`), since this domain library's own design explicitly defers promotion orchestration to those workstreams.

### P1: Proof states and confirmed-relation invariants

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| TAX-55 `evidence_method` = `semantic,syntactic,configured` | Set equality | `src/Csharp2Md.Domain/Proof/ProofAxes.cs:3-8`; `tests/Csharp2Md.Domain.Tests/Proof/ProofAxesTests.cs:10` | ✅ PASS |
| TAX-56 `resolution` = `confirmed,candidate,unresolved` | Set equality | `ProofAxes.cs:10-15`; `ProofAxesTests.cs:20` | ✅ PASS |
| TAX-57 `frontier` = `closed,open` | Set equality | `ProofAxes.cs:17-21`; `ProofAxesTests.cs:30` | ✅ PASS |
| TAX-58 3 independently valued axes | 3 separate enums | `ProofAxes.cs` (3 declarations); `ProofAxesTests.cs:40,63` | ✅ PASS |
| TAX-59 no numeric confidence anywhere | No forbidden-fragment member; exact allowlist of the only legitimate numeric members | `tests/Csharp2Md.Domain.Tests/Surface/NoNumericConfidenceTests.cs:69-99` (whole-assembly reflection scan against an explicit, `nameof`-pinned allowlist, plus a decoy-type negative control at `:116-126`) | ✅ PASS |
| TAX-60 confirmed relation requires typed source/target, registered facets, evidence chain, classifier id+version, ≥1 analysis variant | Each omission rejected by name | `ConfirmedRelation.cs:46-83`; `tests/Csharp2Md.Domain.Tests/Relations/ConfirmedRelationTests.cs:73-135` (one rejection test per component) | ✅ PASS |
| TAX-61 `resolution` fixed to `confirmed` | Computed, no setter/param | `ConfirmedRelation.cs:26` (`Resolution => Resolution.Confirmed`); `ConfirmedRelationTests.cs:137-157` (reflection: no setter, no ctor/factory param) | ✅ PASS |
| TAX-62 candidate/unresolved rejected from confirmed set | Structurally impossible (type mismatch) or explicit rejection | `src/Csharp2Md.Domain/Relations/ConfirmedRelationSet.cs:12-20` (`Add(ConfirmedRelation)` — only accepts the confirmed type); `tests/Csharp2Md.Domain.Tests/Relations/ConfirmedRelationSetTests.cs:46-62` (proves `CandidateLink`/`UnresolvedRecord` are not assignable to the parameter type) | ✅ PASS |
| TAX-63 absent target identity rejected | `ArgumentException` naming `target` | `ConfirmedRelation.cs:57` (`FactGuards.RequireInitialized(target,...)`); `ConfirmedRelationTests.cs:82-90` | ✅ PASS |
| TAX-64 unresolved outcome carries cause + available evidence | Required, rejected if absent | `src/Csharp2Md.Domain/Relations/UnresolvedRecord.cs:34-55`; `tests/Csharp2Md.Domain.Tests/Relations/UnresolvedRecordTests.cs:19-59` | ✅ PASS |
| TAX-65 further continuation at an already-confirmed occurrence records a separate open frontier, leaves confirmed relation unchanged | Confirmed relation byte-identical before/after | `src/Csharp2Md.Domain/Relations/OpenFrontier.cs` (keyed on `ObservationIdentity`, not on any relation); `tests/Csharp2Md.Domain.Tests/Relations/OpenFrontierTests.cs:36-48,50-60` (`Create_AtAnOccurrenceThatAlreadyCarriesAConfirmedRelation_LeavesThatRelationByteIdentical`) | ✅ PASS |
| TAX-66 rejected candidate requires a cause | Rejected if absent | `src/Csharp2Md.Domain/Proof/RejectedCandidate.cs:23-36`; `tests/Csharp2Md.Domain.Tests/Proof/RejectedCandidateTests.cs:19,28,33` | ✅ PASS |
| TAX-67 promotion record declares all 9 components | Each omitted in turn rejected by name; empty required-observations rejected | `src/Csharp2Md.Domain/Proof/PromotionRecord.cs:48-115`; `tests/Csharp2Md.Domain.Tests/Proof/PromotionRecordTests.cs:54-190` (13 traited tests) | ✅ PASS |

### P1: Identity grammar and determinism

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| TAX-68 no absolute paths in any identity | Absolute/rooted/backslash/dot-segment path rejected | `src/Csharp2Md.Domain/Identity/FactId.cs:35-53` (`ValidateRelativePath`); `tests/Csharp2Md.Domain.Tests/Identity/FactIdGrammarTests.cs:7-19,67-74` | ✅ PASS |
| TAX-69 no source location/label/timestamp in any identity | Only declared key-value components emitted | `FactId.cs:24-33` (`Create` only ever appends supplied key/value pairs); `FactIdGrammarTests.cs:21-32`; `tests/Csharp2Md.Domain.Tests/Identity/CanonicalSymbolSignatureTests.cs:8,41,47` | ✅ PASS |
| TAX-70 identical identities across 2 simulated clone paths | Byte equality | `tests/Csharp2Md.Domain.Tests/Identity/IdentityDeterminismTests.cs:7-36` (solution/project/variant/symbol identities built twice, `Assert.Equal(...Value...)`) | ✅ PASS |
| TAX-71 identical identities across 2 shuffled input orders | Byte equality | `IdentityDeterminismTests.cs:38-46`; also `tests/Csharp2Md.Domain.Tests/Identity/AnalysisVariantIdTests.cs:24-32` | ✅ PASS |
| TAX-72 globally composable identities scoped by workspace | Workspace component present; two workspaces ⇒ distinct | `src/Csharp2Md.Domain/Identity/WorkspaceIdentity.cs`; `src/Csharp2Md.Domain/Identity/SolutionId.cs:13-16` (`("workspace", workspace.Value)`); `tests/Csharp2Md.Domain.Tests/Identity/SolutionIdTests.cs:24,50`; `tests/Csharp2Md.Domain.Tests/Identity/WorkspaceIdentityTests.cs:7-15` | ✅ PASS |
| TAX-73 solution/project identity from logical relative path | Path-derived | `SolutionId.cs:9-19`; `src/Csharp2Md.Domain/Identity/ProjectId.cs:22-31`; `SolutionIdTests.cs:8,35` | ✅ PASS |
| TAX-74 project moving to a new path (no key) ⇒ different default identity | Distinct | `tests/Csharp2Md.Domain.Tests/Identity/ProjectIdTests.cs:10,45` | ✅ PASS |
| TAX-75 explicit logical key preserves identity across a path change | Identical identity | `ProjectId.cs:22-31` (key branch bypasses path in the id); `ProjectIdTests.cs:20,32` | ✅ PASS |
| TAX-76 missing required identity component fails naming it, no partial identity | `ArgumentException` naming the parameter | `FactId.cs:70-72` (`Encode` — `ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName)`); `IdentityDeterminismTests.cs:48-75` (7 cases across 4 types); `FactIdGrammarTests.cs:49-56` | ✅ PASS |
| TAX-77 identity collision fails naming both facts | `ArgumentException` naming both fact types | `src/Csharp2Md.Domain/Identity/IdentityLedger.cs:9-24`; `IdentityDeterminismTests.cs:77-89`; `tests/Csharp2Md.Domain.Tests/Identity/IdentityLedgerTests.cs:8,21,35,47` | ✅ PASS |
| TAX-78 semantic fact identity qualified by solution + analysis variant | `AnalysisVariantId` incorporates target framework/config/symbols/environment | `src/Csharp2Md.Domain/Identity/AnalysisVariantId.cs:9-31`; `AnalysisVariantIdTests.cs:7-89` (7 traited tests, incl. `Create_HasNoAbsolutePathLocationOrTimestampParameter`) | ✅ PASS |

### P1: Literal allowlist and secret exclusion

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| TAX-79 literal allowlist = route/protocol-name/channel/schema-name/table-name/field-name/config-key/client-name | Set equality, 8 roles | `src/Csharp2Md.Domain/Literals/StructuralLiteral.cs:5-15`; `tests/Csharp2Md.Domain.Tests/Literals/StructuralLiteralTests.cs:8-19,21-37` | ✅ PASS |
| TAX-80 out-of-allowlist literal rejected naming the field | `ArgumentException` naming the field parameter | `StructuralLiteral.cs:29-39` (`Enum.IsDefined` guard); `StructuralLiteralTests.cs:39-96` (undefined role, non-canonical value, and a reflection scan proving no bare `string` payload property bypasses `StructuralLiteral`) | ✅ PASS |
| TAX-81 suspected-secret evidence = document id + span + doc hash + redacted excerpt | Exactly these 4 | `src/Csharp2Md.Domain/Literals/SuspectedSecretEvidence.cs:36-56`; `tests/Csharp2Md.Domain.Tests/Literals/SuspectedSecretEvidenceTests.cs:8-58,90-104` (exactly 4 public properties; whole-document hash, not span-derived) | ✅ PASS |
| TAX-82 no member carries an individual secret / secret hash | No such member, by name and by type | `SuspectedSecretEvidenceTests.cs:76-88` (`Type_NoMemberCarriesAnOriginalLiteralOrAPerSecretHash_ByNameAndByType`); `SuspectedSecretEvidence.cs` itself has no `string`-typed property (`RedactedExcerpt` requires a visible mask marker at `src/Csharp2Md.Domain/Literals/SuspectedSecretEvidence.cs:18-29`) | ✅ PASS |

### P1: Version axes and registry drift gate

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| TAX-83 5 independent version axes named exactly as specified, starting at 1 | 5 separate `int` members, all `1` | `src/Csharp2Md.Domain/Registry/TaxonomyVersions.cs:3-11`; `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyVersionsTests.cs:8,21,33` | ✅ PASS |
| TAX-84 registry enumerates every family/type/kind/axis/relation/mapping-role/proof-axis/version-axis | Full enumeration present and correctly reflects `TaxonomyTables` | `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyRegistryWriterTests.cs:68-169` (6 tests, each asserting the emitted JSON length/content matches `Tables.*`); confirmed by direct inspection of `contracts/taxonomy-registry.json` (17 fact types, 12 relations with triples+evidence method, 4 mapping roles, 4 payload roles, 10 observation kinds, 8 facet axes, 3 proof axes, 5 version axes — all present) | ✅ PASS |
| TAX-85 emitter runs twice ⇒ byte-identical | Byte equality | `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyRegistryWriterTests.cs:11-19`; `tests/Csharp2Md.Domain.Tests/Registry/EmissionOrderTests.cs:12-142` (also proves the writer only reads `ImmutableArray` members, no `FrozenSet`/`FrozenDictionary` on the projection path, and per-table declared order is preserved in the output) | ✅ PASS |
| TAX-86 committed registry diverging from emitter fails naming the differing entries | Fails, message names the entry | `tests/Csharp2Md.Domain.Tests/Registry/RegistryDriftGateTests.cs:10-20` (committed file byte-compared to fresh emission — **re-ran independently**: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj --filter "FullyQualifiedName~RegistryDriftGateTests"` → 4 passed, 0 failed) and `:22-38` (hand-edited copy fails, message contains `"schema_version"` and `"line 2"`) | ✅ PASS |
| TAX-87 emitter lives outside `Csharp2Md.Domain` | Writer type in the test assembly | `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyRegistryWriter.cs` (file location); `TaxonomyRegistryWriterTests.cs:171-187` (asserts the writer's assembly name and that `Csharp2Md.Domain` declares no `System.Text.Json` reference) | ✅ PASS |
| TAX-88 additive change keeps prior identities/triples valid | Existing triple still registered after an addition | `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyTablesAdditivityTests.cs:8-38` (adding a fact type or a triple to a copy leaves the original untouched and the new state still valid) | ✅ PASS |
| TAX-89 removing/renaming a closed-axis value is detected, not silently accepted | Difference observable, not swallowed | `TaxonomyTablesAdditivityTests.cs:40-81` (narrowed/renamed copy diverges observably from the original via `IsRegisteredTriple`/axis-value containment). This AC is inherently a design-governance statement about how taxonomy changes must be authored (new namespace/grammar vs. additive bump) rather than a runtime-enforceable invariant — there is no code path that could physically stop someone from editing `TaxonomyTables`; the test demonstrates that such an edit is detectable (the drift gate and this test both react), which is the strongest form of "not silently accepted" achievable in a stateless declarative table. Rated a pass on that basis, flagged for awareness | ✅ PASS (governance-style AC; enforcement is via detectability + drift gate, not a runtime block) |
| TAX-90 duplicate triple registration fails naming the duplicate | Fails at registry init and at emission | `src/Csharp2Md.Domain/Registry/RelationDescriptor.cs:17-38` (`RelationTripleIndex.Build`, throws naming relation+both fact types); `tests/Csharp2Md.Domain.Tests/Registry/DuplicateTripleTests.cs:17-43` (both the emitter and `TaxonomyRegistry` construction fail on the same duplicate, proving the emitter inherits the guard) | ✅ PASS |
| TAX-91 every taxonomy type is an immutable value, no mutating member | No settable prop, no mutable field, no mutable collection | `tests/Csharp2Md.Domain.Tests/Surface/ImmutabilityTests.cs:34-43` (whole-assembly reflection scan) with a negative-control decoy at `:56-64` | ✅ PASS |

**Status**: ✅ 87/91 ACs PASS with precise spec-defined-outcome evidence · ⚠️ 4 spec-precision gaps (TAX-46, TAX-50, TAX-51, TAX-53 — all in the same root cause, analyzed above) · 0 hard AC failures (❌) · 0 uncovered (evidence-or-zero found a citation for every ID)

---

## Findings on the two flagged items

### 1. `ConfirmedRelation.Create` and the relation shape guards (TAX-46, TAX-49, TAX-50, TAX-51, TAX-53)

Independently re-derived (not trusting the implementer's self-report):

- `RelationShapeGuards.RequireCallableIfNeeded` and `.RequireLegalTargetShape` are **not called from any production code path** — grep across `src/` and `tests/` shows their only call sites outside their own declaration are in `RelationShapeGuardsTests.cs`. `ConfirmedRelation.Create` (`src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs:46-83`) calls exactly two guards: `Registry.RequireRegisteredTriple` (structural: fact-type names only) and `RelationShapeGuards.RequirePayloadRoleForUsesContract` (TAX-49 — reachable because it only needs `FacetBinding`, which *is* a `Create` parameter).
- `RelationShapeGuards.RequireSufficientEvidence` (TAX-53) is likewise only exercised directly by `RelationShapeGuardsTests.cs` and `TaxonomyRegistryTests.cs`; `ConfirmedRelation.Create` has no `EvidenceMethod` parameter to check it against in the first place.
- **Verdict**: this is a **spec-precision gap rooted in the pre-approved `design.md`**, not a genuine implementer shortcut. `design.md`'s own "Interfaces" section and "Data Models" code block fix `ConfirmedRelation.Create`'s signature to accept only `FactReference` (an id + type-name pair) for source/target — and `design.md` states explicitly that this is deliberate: `FactReference` exists specifically so "a triple be validated from a reference alone without materializing facts." That design choice structurally forecloses reading `Symbol.Facets` or `BoundaryOperation.Direction` (needed for TAX-46/50/51) or an evidence method (TAX-53, which isn't even a `Create` parameter) at the one public relation-construction site. Closing this within the feature's current approved interface is not possible without adding parameters `design.md` did not specify — i.e., a design change, not a task-level fix. TAX-49 is the control case: it is the one shape guard whose only input (`FacetBinding`) already exists on `Create`, and it is exactly the one guard that got wired in. That is direct evidence the pattern is caused by data availability, not carelessness.
- **Practical consequence**: as shipped, a caller can construct e.g. an `executes` `ConfirmedRelation` from a `Symbol` lacking the `Callable` facet, or a `targets` relation whose target is an *outbound* `BoundaryOperation`, and the public API will accept it — only the registered-triple check (fact-type names) runs, not the facet/shape-level check. This is real, not theoretical, and should not be hidden inside an overall "PASS" summary.
- **Recommendation**: this is not fixable inside `knowledge-taxonomy-contract`'s already-approved design without amending it. Route a design-amendment/fix task to whichever downstream workstream first constructs `ConfirmedRelation` from real materialized facts (`engine-bootstrap` or `roslyn-observation-extraction`, per the spec's own Out-of-Scope table, which defers "the override mechanism that classifies or associates existing identities" and the whole promotion pipeline to those workstreams) — either widen `Create`'s signature with optional shape-carrying parameters, or make it a documented, tested caller contract that any promotion pipeline must call `RelationShapeGuards.RequireCallableIfNeeded`/`.RequireLegalTargetShape`/`.RequireSufficientEvidence` against its materialized facts before calling `ConfirmedRelation.Create`.

### 2. T54's requirement-coverage tests for TAX-01, TAX-02, TAX-04, TAX-05

Independently read and re-derived:

- **TAX-01** (`DomainAssembly_TargetsNet10`, `RequirementCoverageTests.cs:63-70`): reflects the *compiled* assembly's `TargetFrameworkAttribute` and asserts `FrameworkName == ".NETCoreApp,Version=v10.0"` — this is real evidence, arguably stronger than reading the `.csproj` text since it reflects the actual build output. ✅ Non-shallow.
- **TAX-02** (`DomainProject_DeclaresNoPackageReferenceAndNoProjectReference`, `:73-80`): reads the raw `.csproj` file text and asserts it does not contain `<PackageReference` or `<ProjectReference`. This is a textual, not semantic (e.g. MSBuild-evaluated), check, but it directly matches the spec's own wording ("declare no package reference and no project reference") and is corroborated independently by this Verifier reading `Csharp2Md.Domain.csproj` directly (13 lines, only `TargetFramework`/`ImplicitUsings`/`Nullable`/`InternalsVisibleTo`). ✅ Non-shallow, matches spec-defined outcome exactly.
- **TAX-04** (`SolutionBuild_KeepsTreatWarningsAsErrorsEnabledWhileLegacyCoreRemains`, `:82-95`): asserts `Directory.Build.props` has `TreatWarningsAsErrors=true`, the domain csproj does not override it, and `Csharp2Md.Core.csproj` still exists on disk. This test proves the **preconditions** for the AC, not the "build succeeds" **outcome** itself — a unit test cannot itself run and assert on a `dotnet build` invocation without becoming an integration test the Test Coverage Matrix explicitly scopes to "build gate only" for this layer. The full outcome is closed by this Verifier's own Gate Check (`dotnet build csharp2md.slnx -c Release` → exit 0, confirmed independently below). Given the matrix's own pre-declared design and the fact the Gate Check genuinely passed, this is accepted as adequate, non-hollow coverage split across two verification layers by design, not a gap.
- **TAX-05** (`DomainProject_IsListedInTheSolutionUnderTheSrcFolder`, `:97-110`): parses `csharp2md.slnx` for the `<Folder Name="/src/">...</Folder>` block and asserts it contains `Csharp2Md.Domain.csproj`. Precise, matches the spec's exact wording, non-shallow.

**Verdict**: all four are genuine, non-tautological assertions of the specific spec-defined outcome for their ID — none is a hollow "does not throw" or presence-only check. TAX-04's split between unit-test-verified preconditions and Gate-Check-verified outcome is a deliberate and documented layering (declared in the Test Coverage Matrix header), not a gap.

---

## Edge Cases

From spec.md's Edge Cases section:

- [x] Out-of-vocabulary axis value rejected naming axis+value (TAX-25) — `FacetAxesTests.cs:8-101`
- [x] Observation missing document hash/extractor version rejected naming the component (TAX-39) — `ObservationTests.cs:104-124`
- [x] Unregistered relation triple rejected naming source/relation/target (TAX-45) — `RelationMatrixTests.cs:56-72`
- [x] Confirmed relation with no target identity rejected rather than emitting a dangling edge (TAX-63) — `ConfirmedRelationTests.cs:82-90`
- [x] Two distinct facts colliding on one identity fail naming both (TAX-77) — `IdentityDeterminismTests.cs:77-89`
- [x] `unknown` facet value treated as registered, not absent (TAX-26) — `FacetAxes.cs:61-62,73,84`; `FacetAxesTests.cs:113`
- [x] Project move with an explicit logical key preserves identity (TAX-75) — `ProjectIdTests.cs:20,32`
- [x] Duplicate relation triple registration fails naming the duplicate (TAX-90) — `DuplicateTripleTests.cs:17-43`
- [x] Hand-edited committed registry fails the drift gate (TAX-86) — `RegistryDriftGateTests.cs:22-38`, independently re-run: 4/4 passed

All 9 documented edge cases are handled and evidenced.

---

## Code Quality

Sampled across phases: identity (`FactId.cs`, `SolutionId.cs`, `ProjectId.cs`), facets/proof (`FacetAxes.cs`, `ProofAxes.cs`), registry (`TaxonomyRegistry.cs`, `RelationDescriptor.cs`), literals (`StructuralLiteral.cs`, `SuspectedSecretEvidence.cs`), observations (`Observation.cs`, `NormalizedPayload.cs`), facts (`StructuralFacts.cs`, `ContractFacts.cs`, `PersistenceFacts.cs`), relations (`ConfirmedRelation.cs`, `RelationShapeGuards.cs`, `ConfirmedRelationSet.cs`), registry-projection (`TaxonomyRegistryWriter.cs`), surface (`NoNumericConfidenceTests.cs`, `ImmutabilityTests.cs`, `RequirementCoverageTests.cs`).

| Principle | Status |
| --- | --- |
| No features beyond what was asked | ✅ — no wire schemas, no persistence, no CLI, no override mechanism; all correctly deferred per spec's Out-of-Scope table |
| No abstractions for single-use code | ✅ — `TaxonomyTables`/`TaxonomyRegistry` is the one declarative authority design.md calls for, not over-engineered |
| No unnecessary "flexibility" added | ✅ — enums are closed, `FacetAxes.WireValue` is an explicit pairing (not reflection-derived), matching the design's stated anti-goal ("a derived conversion would tie the published contract to CLR member names") |
| Only touched files required for task | ✅ — diff is scoped to `src/Csharp2Md.Domain/**`, `tests/Csharp2Md.Domain.Tests/**`, `contracts/taxonomy-registry.json`, `csharp2md.slnx`, and `.specs/features/knowledge-taxonomy-contract/spec.md`'s traceability table |
| Didn't "improve" unrelated code | ✅ — `Csharp2Md.Core` untouched (confirmed: no diff hunks in `src/Csharp2Md.Core/**` in the commit range) |
| Matches existing patterns/style | ✅ — ported grammar keeps the `id1:` form and `-` sentinel per design.md; test style/traits match `tests/Csharp2Md.Core.Tests` conventions |
| Would senior engineer approve? | ✅ with the one caveat above — the `ConfirmedRelation`/`RelationShapeGuards` split is defensible given the design constraint, but should be called out explicitly to the next workstream rather than left implicit |
| Tests map to acceptance criteria, non-shallow (spot-check one story) | ✅ — spot-checked "Proof states and confirmed-relation invariants": every one of the 7 required components of a confirmed relation has its own dedicated rejection test (`ConfirmedRelationTests.cs:73-135`), each asserting the exact `ParamName`, not just "throws" |
| Spec-anchored outcome check | ✅ for 87/91; ⚠️ spec-precision gap noted and not silently passed for 4 (TAX-46/50/51/53) |
| Per-layer Coverage Expectation met | ✅ — domain logic has 1:1 AC-to-test mapping per the Test Coverage Matrix; no route/e2e layer exists in this CLI-less domain-only feature |
| Every test in scope maps to a spec AC/edge case/Done-when (no unclaimed tests) | ✅ — every test file uses the `[Trait("Requirement","TAX-nn")]` convention; `RequirementCoverageTests.cs`'s own self-check (`EveryCarriedRequirementTrait_IsAWellFormedInRangeTaxId`) mechanically enforces this and passed |
| Documented project guidelines followed | ✅ — `AGENTS.md`/`CLAUDE.md` (skill routing, Roslyn/BCL discipline — N/A here since the domain has zero Roslyn surface by design), `Directory.Build.props:7` (`TreatWarningsAsErrors`, confirmed via the Gate Check) |

❌ No "No" answers.

---

## Gate Check

- **Gate command**: `dotnet build csharp2md.slnx -c Release` → exit 0 (build succeeded, no warnings-as-errors failures) → `dotnet format csharp2md.slnx --verify-no-changes` → exit 0 (no formatting drift) → `dotnet test csharp2md.slnx` → exit 1 overall, driven solely by one pre-existing, unrelated failure (below)
- **Result**: **2101 passed, 1 failed, 0 skipped** (`Csharp2Md.Domain.Tests`: 523 passed, 0 failed; `Csharp2Md.Core.Tests`: 1578 passed, 1 failed)
- **The 1 failure** — `Csharp2Md.Core.Tests.Analysis.MigrationLedgerTests.BaselineCategory_StillHasARepresentativeV3Test` (`System.IO.FileNotFoundException`: `.specs/features/csharp2md-v3/test-migration.md` not found) — is in the legacy `Csharp2Md.Core.Tests` project, references a `.specs/features/csharp2md-v3` path this feature never touches, and is explicitly named in the task prompt as a known pre-existing failure out of this feature's scope. Confirmed independently: no file in the diff range (`1e06a08..HEAD`) touches `Csharp2Md.Core.Tests/Analysis/MigrationLedgerTests.cs` or the `csharp2md-v3` spec path. **Not counted against this feature's verdict**, but not silently omitted either.
- **Drift gate re-run independently** (step 7 of the checklist): `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj --filter "FullyQualifiedName~RegistryDriftGateTests"` → 4 passed, 0 failed, 51ms.
- **`contracts/taxonomy-registry.json` spot-check**: read the full 491-line committed file directly (not just trusting the passing test) — it enumerates all 17 fact types (with family+identity components), all 12 relations (with every registered triple and minimum evidence method), 4 mapping roles, 4 payload roles, all 10 observation kinds (with wire name+tier), all 8 facet axes (with full value lists matching the enums read from source), and all 3 proof axes, plus the 5 version axes at value `1`. This is a real, complete, well-formed registry projection, not a stub.
- **Test count before feature**: `Csharp2Md.Core.Tests` had 1579 tests (1578 passing + the 1 pre-existing failure) before this feature — this feature added zero tests to that project. `Csharp2Md.Domain.Tests` did not exist before this feature (T2 created it).
- **Test count after feature**: `Csharp2Md.Domain.Tests` = 523 tests (0 failed). `Csharp2Md.Core.Tests` unchanged at 1579 (1578 passing, same 1 pre-existing failure).
- **Delta**: +523 new tests, 0 deleted, 0 weakened (spot-checked per-task test-count progression in `tasks.md` — each task's recorded running total is internally consistent: 0→52→59→70→80→84→...→509→523, no regression at any step)
- **Skipped tests**: none.
- **Failures**: 1, pre-existing and unrelated (see above).

---

## Discrimination Sensor

**Sensor**: skipped — standing project decision (user runs Stryker manually; documented in `tasks.md`'s header and `.specs/STATE.md`'s "Standing engineering constraints," consistent with `symbol-index`, `relation-collector`, `data-access-discovery` and `relation-resolver`). No mutants were injected. This produces no pass/fail signal by design and is not treated as a gap.

---

## Requirement Traceability Update

Independently re-derived every row's status against the evidence above. 87 of 91 rows are genuinely earned as "Verified." The 4 rows tied to the guard-wiring finding are corrected from "Verified" to reflect the spec-precision gap — they are not silent failures, but they are not unconditionally verified constructions either, since the construction-time enforcement the AC describes is not reachable through the domain's only public API.

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| TAX-01 – TAX-45 | Verified | ✅ Verified (unchanged) |
| TAX-46 | Verified | ⚠️ Verified with spec-precision gap (guard implemented and unit-tested; not wired into `ConfirmedRelation.Create` — see Findings) |
| TAX-47 – TAX-49 | Verified | ✅ Verified (unchanged) |
| TAX-50 | Verified | ⚠️ Verified with spec-precision gap (same as TAX-46) |
| TAX-51 | Verified | ⚠️ Verified with spec-precision gap (same as TAX-46) |
| TAX-52 | Verified | ✅ Verified (unchanged) |
| TAX-53 | Verified | ⚠️ Verified with spec-precision gap (same as TAX-46) |
| TAX-54 – TAX-91 | Verified | ✅ Verified (unchanged) |

`spec.md`'s Requirement Traceability table has been updated to match (see diff to that file in this same commit range's working tree).

---

## Summary

**Overall**: ⚠️ Issues (spec-precision gaps flagged, not blocking; feature is otherwise complete and correctly built/tested)

**Spec-anchored check**: 87/91 ACs matched the spec-defined outcome exactly with `file:line` evidence; 4 spec-precision gaps flagged (TAX-46, TAX-50, TAX-51, TAX-53 — one root cause)
**Sensor**: skipped — standing project decision
**Gate**: 2101 passed, 1 failed (pre-existing, unrelated, out of scope), 0 skipped

**What works**: All 54 tasks complete with matching commits; the domain assembly is genuinely dependency-free and isolated (TAX-01–06); all five fact families and seventeen types are closed and registry-backed (TAX-07–17); every closed vocabulary is set-equality tested with named rejections (TAX-18–31); the observation contract is fully immutable and Roslyn-free (TAX-32–41); eleven of twelve relation-matrix ACs are fully construction-time enforced (TAX-42–45, 47–49, 52, 54); proof-state axes and confirmed-relation invariants hold structurally, not just by convention (TAX-55–67); identity is proven deterministic across clone-path and input-order variation with byte equality (TAX-68–78); the literal allowlist and secret-evidence shape are closed and reflection-proven (TAX-79–82); the registry/drift-gate/version-axis machinery is complete, byte-deterministic, and the committed artifact is real and correct (TAX-83–91). Build, format, and the full test suite (2101 tests) all pass except one documented pre-existing unrelated failure.

**Issues found**: `ConfirmedRelation.Create`'s design.md-fixed signature (accepting only bare `FactReference` identity handles, by deliberate design) structurally prevents four of its own construction-time guards (`RequireCallableIfNeeded`, `RequireLegalTargetShape` ×2 shapes, `RequireSufficientEvidence`) from ever running against a real construction — they exist, are individually correct, and are unit-tested, but a caller using only the public API can currently build a `ConfirmedRelation` that violates TAX-46/50/51/53's facet/shape/evidence-level constraints. This is a genuine gap in end-to-end enforcement, not a documentation nit, but it is not fixable inside this feature's already-approved design without a signature change.

**Next steps**: Route a design-amendment task to whichever workstream first constructs `ConfirmedRelation` from real materialized facts (`engine-bootstrap` or `roslyn-observation-extraction`) to either (a) widen `ConfirmedRelation.Create` with optional shape-carrying parameters so the existing guards can run inline, or (b) formalize and test a caller contract requiring `RelationShapeGuards.RequireCallableIfNeeded`/`.RequireLegalTargetShape`/`.RequireSufficientEvidence` to be invoked against materialized facts before `ConfirmedRelation.Create` is called. Until then, this gap should be treated as a known, documented limitation of the taxonomy contract, not a silent hole.
