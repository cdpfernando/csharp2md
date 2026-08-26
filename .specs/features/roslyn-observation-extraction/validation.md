# Roslyn Observation Extraction Validation

**Date**: 2026-08-25
**Spec**: `.specs/features/roslyn-observation-extraction/spec.md`
**Diff range**: `174fbee^..65fa91a`
**Verifier**: independent sub-agent (author ≠ verifier)
**Result**: PASS

Post-T63 commits in range: `52ada04` (ROSE-21 abort-on-collision), `65fa91a` (ROSE-23 bound observations after compile errors).

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1–T63 | Done | Every Done-when box in `tasks.md` is checked. Header still says “Execute in progress”; the checkboxes are the completion evidence. |
| post-T63 | Done | `52ada04` abort-on-collision; `65fa91a` extract bound observations after compile errors. |

No blocked or partial tasks.

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| WHEN Inventory runs THEN authorized root is the smallest directory containing the solution file and every existing listed project (ROSE-01) | Root is `fixtures/SyntheticSolution` for Acme.Orders plus siblings; missing listed path does not change it; nested-only projects keep the solution directory | `tests/Csharp2Md.Analysis.Tests/Inventory/AuthorizedRootTests.cs:16` `AssertEqualPaths(SyntheticSolutionRoot(), root)`; `:31-32` missing path does not change root; `:51` nested tree keeps `appDirectory` | PASS |
| IF a symlink’s resolved target lies outside the authorized root THEN abort that solution, name the symlink, and do not follow the target (ROSE-02) | Unpublished, not structural corruption, Detail contains the symlink path | `tests/Csharp2Md.Analysis.Tests/Inventory/InventorySymlinkAbortTests.cs:53-56` `PublicationStatus.Unpublished`, `Assert.Contains(symlink, outcome.Detail)`; `tests/Csharp2Md.Analysis.Tests/Inventory/PathGuardTests.cs:24-26` `Assert.Throws<InvalidOperationException>` and `Assert.Contains(symlink, exception.Message)` | PASS |
| WHEN a listed project path exists inside the root THEN emit one Project fact with a `/`-separated logical path relative to that root (ROSE-03) | `Acme.Shared.Contracts/Acme.Shared.Contracts.csproj`; no `..` in the identity | `tests/Csharp2Md.Analysis.Tests/Inventory/InventoryFactsTests.cs:42-46` `Assert.Contains(facts.Projects, project => project.Id.Equals(expectedId))` | PASS |
| WHEN a file is a project item or sits in the project directory inside the root THEN emit one Document with `/` relative path and no drive prefix (ROSE-04) | `Acme.Orders/Api/OrdersController.cs`; not rooted; no `\` | `tests/Csharp2Md.Analysis.Tests/Inventory/DocumentInventoryTests.cs:28-32` `Assert.Equal("Acme.Orders/Api/OrdersController.cs", controller.RelativePath)`, `Assert.False(Path.IsPathRooted(...))`, `Assert.False(HasDrivePrefix(...))` | PASS |
| WHEN a non-C# document is inventoried THEN keep it as a Document and do not run a C# extractor against it (ROSE-05) | JSON is a Document, absent from `CSharpDocuments`, no observations for `.json` | `tests/Csharp2Md.Analysis.Tests/Inventory/DocumentInventoryTests.cs:118-129` Document plus not in `CSharpDocuments`; `tests/Csharp2Md.Analysis.Tests/Extraction/ObservationExtractionStageTests.cs:45-47` `Assert.DoesNotContain(... EndsWith(".json"))` | PASS |
| WHEN a non-C# document is inventoried THEN record support outcome `unsupported` (ROSE-06) | Diagnostic code `unsupported-document` | `tests/Csharp2Md.Analysis.Tests/Inventory/DocumentInventoryTests.cs:122-125` `Assert.Equal("unsupported-document", unsupported.Code)` | PASS |
| Inventory SHALL NOT read, hash, or copy any file whose resolved path lies outside the authorized root (ROSE-07) | Escape rejected by name; solution unpublished | `tests/Csharp2Md.Analysis.Tests/Inventory/PathGuardTests.cs:24` throw on escaped symlink; `tests/Csharp2Md.Analysis.Tests/Inventory/InventorySymlinkAbortTests.cs:53` unpublished | PASS |
| WHEN two requested solutions would produce the same `SolutionId` THEN reject before analysis and name both input paths (ROSE-08) | `ArgumentException` names both paths; store `Open` not called | `tests/Csharp2Md.Analysis.Tests/DuplicateSolutionIdTests.cs:22-27` `Assert.ThrowsAsync<ArgumentException>`, `Assert.Contains(first/second, exception.Message)`, `Assert.Equal(0, store.OpenCount)`; `tests/Csharp2Md.Cli.Tests/AnalyzeInvocationErrorTests.cs:89-91` CLI exit 1 names both paths | PASS |
| Workspace identity in `SolutionId` SHALL be logical name `default` (ROSE-09) | `WorkspaceIdentity.Create("default")` | `tests/Csharp2Md.Analysis.Tests/Inventory/InventoryFactsTests.cs:21-24` `Assert.Equal(expected.Id, facts.Solution.Id)`, `Assert.Contains("default", facts.Solution.Id.Value)` | PASS |
| Solution logical relative path SHALL be the solution file name including extension (ROSE-10) | `Acme.Orders.slnx` | `tests/Csharp2Md.Analysis.Tests/Inventory/InventoryFactsTests.cs:21-25` `SolutionId.Create(..., "Acme.Orders.slnx")`, `Assert.Contains("Acme.Orders.slnx", facts.Solution.Id.Value)` | PASS |
| IF a listed project path does not exist THEN record a diagnostic naming that path and do not abort (ROSE-11) | `missing-project` with `Acme.DoesNotExist/Acme.DoesNotExist.csproj`; `AbortPublication` false | `tests/Csharp2Md.Analysis.Tests/Inventory/InventoryStageTests.cs:20-27` `Assert.False(result.AbortPublication)`, `Assert.Equal("Acme.DoesNotExist/Acme.DoesNotExist.csproj", missing.IdentityOrKey)` | PASS |
| IF a listed project cannot be loaded because its SDK is unresolvable THEN record a diagnostic naming that project and do not abort (ROSE-12) | `unresolvable-sdk` for `Acme.Broken/Acme.Broken.csproj`; solution continues | `tests/Csharp2Md.Analysis.Tests/Semantics/SemanticAnalysisStageTests.cs:126-135` `Assert.False(result.AbortPublication)`, `Assert.Equal("Acme.Broken/Acme.Broken.csproj", sdk.IdentityOrKey)` | PASS |
| WHEN Inventory completes for a loadable solution THEN the package contains exactly one Solution fact (ROSE-13) | `Assert.Single` of `Solution` facts | `tests/Csharp2Md.Analysis.Tests/Inventory/InventoryStageTests.cs:56-58` `Assert.Single(...Facts.OfType<Solution>())` | PASS |
| WHEN Semantic Analysis binds a C# document THEN emit a Symbol fact for each declared type, method, property, field, event, constructor, local function, and identifiable lambda (ROSE-14) | All eight shapes present in signatures | `tests/Csharp2Md.Analysis.Tests/Semantics/SymbolFactEmitterTests.cs:85-100` `Assert.Contains` for namedtype/method/property/field/event/.ctor/Local/identifiable | PASS |
| WHEN a symbol is a method, constructor, local function, or identifiable lambda THEN facet set includes `Callable` (ROSE-15) | `SymbolFacet.Callable` on those shapes; not on types | `tests/Csharp2Md.Analysis.Tests/Semantics/SymbolFactEmitterTests.cs:128` `Assert.Contains(SymbolFacet.Callable, placeOrder.Facets.Facets)`; `:175-178` Callable on Method/.ctor/Local/identifiable | PASS |
| WHEN a symbol is a type, property, field, or event THEN facet set SHALL NOT include Controller/Handler/Repository/Client/Service (ROSE-16) | Those facets absent | `tests/Csharp2Md.Analysis.Tests/Semantics/SymbolFactEmitterTests.cs:211-215` `Assert.DoesNotContain` each architecture facet; `:277-279` `Assert.All` structural kinds omit them | PASS |
| WHEN a C# document yields at least one observation THEN Project `contains` Document with evidence method `syntactic`, classifier `csharp2md.inventory.contains` v1, non-empty `derived_from` (ROSE-17) | Project→Document `contains`; classifier id/version; non-empty chain; syntactic minimum | `tests/Csharp2Md.Analysis.Tests/Extraction/ContainsRelationEmitterTests.cs:31-58` Project→Document, `Assert.Equal("csharp2md.inventory.contains", relation.Classifier.Id)`, `Assert.Equal(1, relation.Classifier.Version)`, `Assert.NotEmpty(relation.DerivedFrom.DerivedFrom)`; `:59` `Assert.Equal(EvidenceMethod.Syntactic, ContainsMinimumEvidence)` (`ConfirmedRelation.Create` takes `EvidenceMethod.Syntactic` and does not persist the enum) | PASS |
| WHEN a Symbol is declared in a C# document that yields at least one observation THEN Document `contains` Symbol with the same classifier and non-empty `derived_from` (ROSE-18) | Document→Symbol `contains` | `tests/Csharp2Md.Analysis.Tests/Extraction/ContainsRelationEmitterTests.cs:36-40` Document→Symbol `contains`; `:50-57` same classifier and non-empty `derived_from` | PASS |
| The package SHALL NOT contain Solution `contains` Project (ROSE-19) | No such triple | `tests/Csharp2Md.Analysis.Tests/Extraction/ContainsRelationEmitterTests.cs:41-45` `Assert.DoesNotContain` Solution→Project | PASS |
| The package SHALL NOT contain a confirmed relation whose kind is not `contains` (ROSE-20) | Every confirmed relation is `contains` | `tests/Csharp2Md.Analysis.Tests/Extraction/ContainsRelationEmitterTests.cs:46-49` `Assert.DoesNotContain(... Kind is not Contains)`, `Assert.All(... Assert.Equal(RelationKind.Contains, relation.Kind))` | PASS |
| IF two structural facts in one solution would share one identity THEN commit SHALL abort as structural corruption naming the identity (ROSE-21) | Unpublished; `StructuralCorruption` true; Detail contains the identity; no publication | `tests/Csharp2Md.Analysis.Tests/Pipeline/SnapshotAccumulatorTests.cs:42-44` `Assert.True(accumulator.StructuralCorruption)`, `Assert.Equal(identity.Id.Value, accumulator.CollidingIdentity)`; `:58-64` `PublicationStatus.Unpublished`, `Assert.True(outcome.StructuralCorruption)`, `Assert.Contains(identity.Id.Value, outcome.Detail)`, `Assert.False(store.TryGetPublication(...))` | PASS |
| IF `MSBuildWorkspace` cannot open a requested solution THEN abort that solution, name the failure, continue remaining solutions (ROSE-22) | First unpublished with open-failure Detail; second Committed | `tests/Csharp2Md.Analysis.Tests/Semantics/SemanticAnalysisStageTests.cs:72-82` unpublished Detail contains `MSBuildWorkspace could not open`; second `PublicationStatus.Committed`; `tests/Csharp2Md.Analysis.Tests/Pipeline/StageSubstitutionTests.cs:49-53` unpublished, not structural corruption, Detail copied | PASS |
| IF a project produces compilation diagnostics of error severity THEN record a diagnostic naming that project, extract observations for occurrences that still bind, and do not abort (ROSE-23) | `compilation-error` names `Broken/Broken.csproj`; `AbortPublication` false; bound observation from `Good.cs` | `tests/Csharp2Md.Analysis.Tests/Semantics/SemanticAnalysisStageTests.cs:166-201` `Assert.False(result.AbortPublication)`, `Assert.Equal("Broken/Broken.csproj", compileError.IdentityOrKey)`, extraction `Assert.Contains` observation with `Good.cs` and `Diagnostic.Code == "bound"` | PASS |
| Semantic Analysis SHALL compile every declared `TargetFramework` under configuration `Debug` when unspecified (ROSE-24) | Open uses Debug + declared TFM; variants include `configuration=Debug` and `tfm=net10.0` | `tests/Csharp2Md.Analysis.Tests/Semantics/MsBuildWorkspaceFactoryTests.cs:14` `Open(..., "Debug", "net10.0", ...)`; `tests/Csharp2Md.Analysis.Tests/Semantics/AnalysisVariantFactoryTests.cs:57-68` `Assert.Contains("configuration=Debug", variant.Value)`, `Assert.Contains(... "tfm=net10.0")` | PASS |
| Each compiled TFM×configuration pair is an `AnalysisVariantId` with `environment` `local` (ROSE-25) | `environment=local` | `tests/Csharp2Md.Analysis.Tests/Semantics/AnalysisVariantFactoryTests.cs:36-41` `Assert.Contains("environment=local", variant.Value)`, equal to `AnalysisVariantId.Create(..., "local")` | PASS |
| `Csharp2Md.Analysis` SHALL reference `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0 (ROSE-26) | PackageReference present; central version `5.6.0` | `tests/Csharp2Md.Analysis.Tests/Isolation/PackageHygieneTests.cs:41-57` `referenced.Contains(packageId)`, `Assert.True(version == "5.6.0", ...)` | PASS |
| `Csharp2Md.Analysis` SHALL NOT reference any `Microsoft.Build.*` package (ROSE-27) | No PackageReference with prefix `Microsoft.Build` | `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisIsolationTests.cs:70-75` `offending is null` for `Microsoft.Build*` | PASS |
| Production assemblies SHALL NOT call `MSBuildLocator.RegisterDefaults` (ROSE-28) | No `MSBuildLocator` or `RegisterDefaults` token in `src/**/*.cs` | `tests/Csharp2Md.Analysis.Tests/Isolation/MsBuildLocatorAbsenceTests.cs:18-20` `Assert.True(offending is null, ...)` | PASS |
| WHEN a compilation is created THEN analyzer assemblies SHALL be absent (ROSE-29) | `AnalyzerReferences` empty after Strip | `tests/Csharp2Md.Analysis.Tests/Semantics/CompilationSanitizerTests.cs:21` `Assert.Empty(stripped.AnalyzerReferences)`; `:105-108` remaining analyzers length 0 | PASS |
| WHEN a compilation is created THEN source-generator assemblies SHALL be absent (ROSE-30) | Remaining generator names empty after Strip | `tests/Csharp2Md.Analysis.Tests/Semantics/CompilationSanitizerTests.cs:105-108` `remainingGenerators.Length == 0`; `:83-91` fixture compilations sanitized | PASS |
| Public surface of `Csharp2Md.Analysis` SHALL NOT expose any type from `Microsoft.CodeAnalysis` (ROSE-31) | No exported CodeAnalysis types or signatures | `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisIsolationTests.cs:109-118` `Assert.DoesNotContain` exported types in `Microsoft.CodeAnalysis`; no offending public member | PASS |
| WHEN a C# invocation binds to a method or delegate THEN emit `Invocation` owned by the containing callable (ROSE-32) | Invocation kind present; OrdersController invocations owned by `GetOrderStatus` | `tests/Csharp2Md.Analysis.Tests/Extraction/AlwaysWhenBindableWalkerTests.cs:26` Invocation kind; `:71-74` `Assert.Contains("GetOrderStatus", invocation.Identity.Owner.Id.Value)` | PASS |
| WHEN a C# object-creation expression binds THEN emit `ObjectCreation` (ROSE-33) | Kind present | `tests/Csharp2Md.Analysis.Tests/Extraction/AlwaysWhenBindableWalkerTests.cs:27` `observation.Identity.Kind is ObservationKind.ObjectCreation` | PASS |
| WHEN a C# type reference binds THEN emit `TypeUsage` (ROSE-34) | Kind present | `tests/Csharp2Md.Analysis.Tests/Extraction/AlwaysWhenBindableWalkerTests.cs:28` `ObservationKind.TypeUsage` | PASS |
| WHEN a C# type declaration has a base type or interface in its base list THEN emit `BaseType` (ROSE-35) | BaseType on `OrdersController` | `tests/Csharp2Md.Analysis.Tests/Extraction/AlwaysWhenBindableWalkerTests.cs:51-56` BaseType, path ends with `OrdersController.cs`, owner contains `OrdersController` | PASS |
| WHEN a C# attribute is applied THEN emit `AttributeUsage` (ROSE-36) | Kind present | `tests/Csharp2Md.Analysis.Tests/Extraction/AlwaysWhenBindableWalkerTests.cs:30` `ObservationKind.AttributeUsage` | PASS |
| WHEN a C# assignment writes a member of an entity instance used through `DbSet<T>` or `DbContext` THEN emit `Assignment` (ROSE-37) | `order.Status` in `OrderDbContext.cs` | `tests/Csharp2Md.Analysis.Tests/Extraction/AssignmentDetectorTests.cs:25-33` `ObservationKind.Assignment`, spanned lines contain `order.Status` | PASS |
| WHEN C# code invokes `IConfiguration` (`this[]`, `GetSection`, `GetValue`), `IOptions<T>`, or `Configure<T>` THEN emit `Configuration` whose payload carries the key and SHALL NOT carry the bound value (ROSE-38) | Payload is only `ConfigurationKey` `Logging:Level` | `tests/Csharp2Md.Analysis.Tests/Extraction/ConfigurationDetectorTests.cs:34-46` `Assert.Equal(LiteralRole.ConfigurationKey, entry.Value.Role)`, `Assert.Equal("Logging:Level", entry.Value.Value)`, no other roles/values | PASS |
| WHEN C# applies `[HttpGet]`/`[HttpPost]`/`[HttpPut]`/`[HttpDelete]`/`[HttpPatch]`/`[Route]`, or invokes `MapGet`/`MapPost`/`MapPut`/`MapDelete` THEN emit `RouteDeclaration` whose payload carries the route literal when present (ROSE-39) | Route literal `orders/{id}` | `tests/Csharp2Md.Analysis.Tests/Extraction/RouteDeclarationDetectorTests.cs:33-35` `Assert.Equal(LiteralRole.Route, entry.Value.Role)`, `Assert.Equal("orders/{id}", entry.Value.Value)` | PASS |
| WHEN C# invokes `Publish`/`PublishAsync`/`Send`/`SendAsync`/`Subscribe` on a bound receiver THEN emit `MessageOperation` (ROSE-40) | `PublishAsync` in `OrderService.cs` | `tests/Csharp2Md.Analysis.Tests/Extraction/MessageOperationDetectorTests.cs:24-32` `ObservationKind.MessageOperation`, spanned lines contain `PublishAsync` | PASS |
| WHEN C# accesses `DbSet<T>`, invokes SaveChanges/Add/FromSql/ExecuteSql on DbContext/DbSet, or applies a LINQ operator whose source binds to `DbSet<T>` THEN emit `DataAccess` (ROSE-41) | `_context.SaveChanges` | `tests/Csharp2Md.Analysis.Tests/Extraction/DataAccessDetectorTests.cs:25-33` `ObservationKind.DataAccess`, spanned lines contain `_context.SaveChanges` | PASS |
| IF an occurrence of a registered-context kind does not match ROSE-37..41 THEN do not emit that kind (ROSE-42) | No Assignment on ReceiverShapes/OrderRepository; no Configuration on OrderRepository; Find/Add not MessageOperation; no DataAccess on OrderRepository | `tests/Csharp2Md.Analysis.Tests/Extraction/AssignmentDetectorTests.cs:42-51`; `ConfigurationDetectorTests.cs:56-60`; `MessageOperationDetectorTests.cs:56-67`; `DataAccessDetectorTests.cs:43-47` each `Assert.DoesNotContain` | PASS |
| WHEN two observations share owner, kind, and payload but differ in ordinal THEN they SHALL have distinct identities (ROSE-43) | Ordinals 1 and 2; identities not equal | `tests/Csharp2Md.Analysis.Tests/Extraction/OccurrenceOrdinalAssignerTests.cs:21-29` `Assert.Equal(1/2, ...OccurrenceOrdinal)`, `Assert.NotEqual(assigned[0].Identity, assigned[1].Identity)` | PASS |
| WHEN two observations share owner, kind, payload, and ordinal but differ only in source span THEN they SHALL have the same identity (ROSE-44) | Same identity; accumulator keeps one | `tests/Csharp2Md.Analysis.Tests/Extraction/ObservationIdentityUnionTests.cs:29-33` same identity across spans, `Assert.Single(observations)`; `tests/Csharp2Md.Analysis.Tests/Pipeline/SnapshotAccumulatorTests.cs:78-80` `Assert.Equal(first.Identity, second.Identity)`, `Assert.Single` | PASS |
| IF an occurrence cannot bind THEN still emit the always-when-bindable kind, attach a binding diagnostic naming the failure, and do not invent a target fact identity (ROSE-45) | Kind Invocation; code `unbound`; empty payload; no `id1:` | `tests/Csharp2Md.Analysis.Tests/Extraction/AlwaysWhenBindableWalkerTests.cs:119-125` `Assert.Equal("unbound", unbound.Diagnostic.Code)`, `Assert.Empty(unbound.Identity.Payload.Entries)`, `Assert.DoesNotContain(... "id1:")` | PASS |
| Every emitted observation SHALL carry owner, kind, payload, positive ordinal, locator, extraction method, binding diagnostic, document hash, and extractor version (ROSE-46) | All fields asserted non-empty/valid; ordinal ≥ 1; extractor version 1 | `tests/Csharp2Md.Analysis.Tests/Extraction/ObservationMaterializerTests.cs:21-41` `Assert.All` owner/kind/ordinal/locator/method/diagnostic/hash/`ExtractorVersion.Value == 1` | PASS |
| Observation payloads SHALL contain only `StructuralLiteral` values (ROSE-47) | Every payload entry is `StructuralLiteral` | `tests/Csharp2Md.Analysis.Tests/Extraction/AlwaysWhenBindableWalkerTests.cs:38-42` `Assert.IsType<StructuralLiteral>(entry.Value)`; `ConfigurationDetectorTests.cs:35` same | PASS |
| Observation Extraction SHALL NOT expose a Roslyn syntax, symbol, or compilation type on the Analysis public surface (ROSE-48) | Stage is internal; no CodeAnalysis parameter/return types | `tests/Csharp2Md.Analysis.Tests/Extraction/ObservationExtractionStageTests.cs:97-103` `Assert.False(typeof(ObservationExtractionStage).IsPublic)`, no `Microsoft.CodeAnalysis` on declared methods; `AnalysisIsolationTests.cs:109-118` public surface | PASS |
| Versioned fixture SHALL contain at least one positive occurrence of each of the ten observation kinds (ROSE-49) | No missing `ObservationKind` | `tests/Csharp2Md.Analysis.Tests/Extraction/DataAccessDetectorTests.cs:55-64` `missing.Length == 0`, `Assert.Contains` for every kind | PASS |
| Every observation evidence locator SHALL use a relative path with forward slashes and a one-based source span (ROSE-50) | `/` present, no `\`, not rooted, start line ≥ 1 | `tests/Csharp2Md.Analysis.Tests/Extraction/ObservationMaterializerTests.cs:28-34` those assertions | PASS |
| Every observation extracted from a document SHALL carry the SHA-256 digest of that document’s file bytes (ROSE-51) | Hash equals `SHA256` of on-disk bytes | `tests/Csharp2Md.Analysis.Tests/Extraction/ObservationMaterializerTests.cs:61-63` `Assert.Equal(expected, observation.DocumentHash.Value)` | PASS |
| IF two clones of the same tree are analyzed THEN every structural fact identity and every observation identity SHALL be equal (ROSE-52) | Equal sorted id arrays; clone path absent from ids | `tests/Csharp2Md.Analysis.Tests/Pipeline/ClonePathIndependenceTests.cs:46-49` `Assert.Equal(factIdsA, factIdsB)`, `Assert.Equal(observationIdsA, observationIdsB)` | PASS |
| IF the same solution is analyzed twice THEN every canonical payload file SHALL be byte-identical (ROSE-53) | Payload spans sequence-equal per key | `tests/Csharp2Md.Analysis.Tests/Pipeline/RetryCanonicalBytesTests.cs:37-39` `firstPayloads[index].Payload.AsSpan().SequenceEqual(second...)` | PASS |
| IF document enumeration order is shuffled THEN structural fact identities and observation identities SHALL be unchanged (ROSE-54) | Equal sorted identity arrays after reverse document groups | `tests/Csharp2Md.Analysis.Tests/Extraction/DocumentOrderIndependenceTests.cs:44` `Assert.Equal(forwardIds, shuffledIds)`; `:47` structural ids unchanged after reverse | PASS |
| IF an extractor would copy a connection string, password, token, certificate, or authorization value THEN omit it, record `SuspectedSecretEvidence` with document, span, document hash, and a redacted excerpt, and SHALL NOT hash the secret value itself (ROSE-55) | Planted `Password=secret` absent from payloads; suspected-secret with mask; document hash is file hash not secret hash | `tests/Csharp2Md.Analysis.Tests/Extraction/SuspectedSecretExtractionTests.cs:68-94` `Assert.DoesNotContain(PlantedLiteral/PlantedSecret)`, suspected-secret with `***`/`[REDACTED]`, `Assert.Equal(fileHash, dto.DocumentHash)`, hash ≠ secret hash; `ObservationRedactionTests.cs:34-49` excerpt/document/span/hash | PASS |
| Canonical package payloads SHALL NOT contain an absolute filesystem path (ROSE-56) | No clone path, no rooted JSON strings, no `\` | `tests/Csharp2Md.Analysis.Tests/Pipeline/NoAbsolutePathTests.cs:70-73` clone path absent; `:81-85` `IsAbsoluteFilesystemPath` false | PASS |
| Committed package SHALL NOT contain source-projection files, Markdown pages, catalogs, or postings (ROSE-57) | No `.md`, `catalogs/`, `postings/`, `source-projection/` keys | `tests/Csharp2Md.Analysis.Tests/Pipeline/PackageContentsTests.cs:31-43` those `Assert.DoesNotContain` / `Assert.All` | PASS |
| WHEN `analyze` completes without structural corruption THEN Persistence SHALL stage the accumulated Solution/Project/Document/Symbol, observations, and `contains` records, not an empty snapshot (ROSE-58) | Non-empty structural shard, observation keys, contains relations | `tests/Csharp2Md.Analysis.Tests/Pipeline/PersistenceManifestTests.cs:36-45` `Assert.NotEmpty` solutions/projects/documents/symbols; observations key present; contains array not empty | PASS |
| WHEN Inventory, Semantic Analysis, or Observation Extraction runs on `Acme.Orders.slnx` THEN that stage’s reported count SHALL be greater than zero for the metric appropriate to the stage (ROSE-59) | Inventory facts > 0; Semantic facts > 0; Extraction observations and relations > 0 | `tests/Csharp2Md.Analysis.Tests/Pipeline/DefaultPipelineZerosTests.cs:39-53` those `Assert.True(... > 0)` | PASS |
| WHEN Classification and Promotion, Retrieval Projection, or Batch Composition runs THEN that stage SHALL still report 0/0/0 of its own production (ROSE-60) | Those stages 0 facts, 0 observations, 0 relations | `tests/Csharp2Md.Analysis.Tests/Pipeline/DefaultPipelineZerosTests.cs:58-61` `AssertZeroProduction` for Classification, Retrieval Projection, Batch Composition | PASS |
| CLI SHALL still require `--output` and at least one `--solution`, and SHALL NOT expose `--trust`, `--include-source-generators`, or `--analysis-timeout` (ROSE-61) | Product options exactly those two; removed flags absent; missing option exits 1 | `tests/Csharp2Md.Cli.Tests/AnalyzeOptionSurfaceTests.cs:33-44` `Assert.DoesNotContain` removed names, `Assert.Equal(["--output", "--solution"], analyzeProductOptions)`; `tests/Csharp2Md.Cli.Tests/AnalyzeCommandTreeTests.cs:26` `Assert.True(solution.Required)`; `:38` `Assert.True(output.Required)`; `tests/Csharp2Md.Cli.Tests/AnalyzeInvocationErrorTests.cs:15-16` and `:33-34` exit 1 names the missing option | PASS |
| `Csharp2Md.Cli` SHALL continue to declare no project reference to `Csharp2Md.Domain` (ROSE-62) | Domain absent from CLI ProjectReferences | `tests/Csharp2Md.Cli.Tests/Isolation/CliIsolationTests.cs:24` `Assert.DoesNotContain("Csharp2Md.Domain", names)` | PASS |
| WHEN a solution is unpublished because of a symlink escape or an MSBuild open failure THEN the last valid package SHALL remain byte-identical (ROSE-63) | After symlink abort, prior payload bytes unchanged | `tests/Csharp2Md.Analysis.Tests/Pipeline/AbortPreservesPackageTests.cs:55-67` role/key equal and `Payload.AsSpan().SequenceEqual`; `InventorySymlinkAbortTests.cs:57-68` same | PASS |
| WHERE the in-memory adapter is used the engine SHALL still produce structural facts and observations and SHALL create no files (ROSE-64) | Working-tree hash unchanged; structural and observation artifacts present | `tests/Csharp2Md.Analysis.Tests/Pipeline/NoFilesystemWriteTests.cs:40-41` `Assert.Equal(before, after)`; `:52-62` non-empty structural facts and observation keys | PASS |

**Status**: All 64 ACs covered. 0 spec-precision gaps.

Adversarial re-read of the two previously contested criteria:

- ROSE-21 is not only an accumulator flag. `AnalyzeAsync_UnequalFactsWithOneIdentity_UnpublishesNamingTheIdentity` drives unequal facts with one identity through `AnalysisEngine`, asserts `PublicationStatus.Unpublished`, `StructuralCorruption`, Detail containing `identity.Id.Value`, and `TryGetPublication` false. Production sets `CollidingIdentity`, orchestrator returns `PipelineCompletion.StructuralCorruption`, engine `session.Abort()`s.
- ROSE-23 is not only a `compilation-error` diagnostic. `ExecuteAsync_CompileErrorProject_RecordsCompilationErrorAndKeepsLoadableCompilation` runs `ObservationExtractionStage` after the error project and asserts a `bound` observation whose locator path contains `Good.cs`, plus `AbortPublication == false`.

---

## Discrimination Sensor

| Mutation | File:line | Description | Killed? |
| -------- | --------- | ----------- | ------- |
| — | — | Not run | SKIPPED |

**Sensor depth**: skipped
**Result**: SKIPPED (standing user request for csharp2md, same as `symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`, `knowledge-taxonomy-contract`, `engine-bootstrap`, and `factual-storage`). No git worktree, no file mutation, no Stryker.

---

## Interactive UAT Results

Not performed. This feature is backend/CLI infrastructure; automated checks are sufficient per validate.md.

---

## Code Quality

| Principle | Status |
| --------- | ------ |
| Minimum code | PASS |
| Surgical changes | PASS |
| No scope creep | PASS |
| Matches patterns | PASS |
| Spec-anchored outcome check (asserted values match spec) | PASS |
| Per-layer Coverage Expectation met (domain 1:1 ACs; analysis inventory/semantics/extraction happy+edge+error) | PASS |
| Every test maps to a spec requirement - no unclaimed tests | PASS |
| Documented guidelines followed: `AGENTS.md` / `CLAUDE.md` (net10.0, Workspaces.MSBuild 5.6.0, no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults`, SyntheticSolution-only versioned fixture, LocalCorpus skip when clones absent) | PASS |

Diff surface `174fbee^..65fa91a` stays on Analysis inventory/semantics/extraction, Storage diagnostics mapping, CLI Detail/option surface, the versioned fixture, and matching tests. No unrelated “improvements”. Extra ROSE-21-tagged accumulator tests (`ToSnapshot` contents, empty `PipelineContext`, no Roslyn types on `SnapshotAccumulator`) map to T6 Done-when, not to a second AC.

Spot-check (P1 Observation ledger): ROSE-32..49 assertions name kinds, owners, payloads (empty vs key vs route literal), and negative registered-context cases. Not trait-only.

---

## Edge Cases

- [x] Symlink escapes the authorized root: unpublished and named (ROSE-02) — `InventorySymlinkAbortTests.cs:53-56`
- [x] Listed project missing: continue with named diagnostic (ROSE-11) — `InventoryStageTests.cs:20-27`
- [x] Project SDK cannot resolve: continue with named diagnostic (ROSE-12) — `SemanticAnalysisStageTests.cs:126-135`
- [x] `MSBuildWorkspace` cannot open: only that solution unpublished (ROSE-22) — `SemanticAnalysisStageTests.cs:66-82`
- [x] Compilation has errors: bindable observations still emitted (ROSE-23) — `SemanticAnalysisStageTests.cs:194-198`
- [x] Two solutions would share one `SolutionId`: rejected before analysis (ROSE-08) — `DuplicateSolutionIdTests.cs:22-27`
- [x] Invocation cannot bind: binding diagnostic, no invented target (ROSE-45) — `AlwaysWhenBindableWalkerTests.cs:119-125`
- [x] Configuration occurrence has a secret-shaped value: payload key only / value omitted (ROSE-38, ROSE-55) — `ConfigurationDetectorTests.cs:34-46`, `SuspectedSecretExtractionTests.cs:68-82`
- [x] N = 1 still occupies a child directory under `--output` (STOR-19, unchanged) — prior STOR coverage; this feature does not alter that contract
- [x] In-memory adapter creates no files (ROSE-64) — `NoFilesystemWriteTests.cs:40-41`

---

## Gate Check

- **Gate command**: `dotnet test csharp2md.slnx --filter "Category!=LocalCorpus"`
- **Result**: 930 passed, 0 failed, 0 skipped among selected tests
- **Per-project passed counts**:
  - `Csharp2Md.Projection.Tests`: 3 passed
  - `Csharp2Md.Domain.Tests`: 544 passed
  - `Csharp2Md.Storage.Tests`: 143 passed
  - `Csharp2Md.Cli.Tests`: 27 passed
  - `Csharp2Md.Analysis.Tests`: 213 passed
- **Test count before feature** (`174fbee^`): 590 `[Fact]`/`[Theory]` attributes under `tests/`
- **Test count after feature** (`HEAD`): 718 `[Fact]`/`[Theory]` attributes under `tests/`
- **Delta**: +128 attributes (increase; no silent deletions)
- **Skipped tests**: `LocalCorpusAnalyzeTests` (2 theory cases, `[Trait("Category", "LocalCorpus")]`) excluded by the filter. `fixtures/eShop` and `fixtures/eShopOnContainers` are absent. Documented skip, not a feature failure.
- **Failures**: none

---

## Fix Plans

None.

---

## Requirement Traceability Update

Applied: ROSE-01..ROSE-64 status is Verified in `spec.md`.

---

## Summary

**Overall**: Ready

**Spec-anchored check**: 64/64 ACs matched spec outcome | 0 spec-precision gaps
**Sensor**: SKIPPED (standing user request)
**Gate**: 930 passed

**What works**: Inventory bounds the authorized root and aborts symlink escapes by name. Semantic binding compiles declared TFMs under Debug, strips analyzers/generators, and keeps bindable observations when a project has compile errors. The ledger emits all ten kinds from `fixtures/SyntheticSolution`, unions identities without a span axis, redacts secrets, and Persistence stages a filled snapshot. Structural identity collision aborts commit and names the identity.

**Issues found**: none

**Next steps**: Feature is verified. Classifier workstreams 5A–5D wait on an explicit start.
