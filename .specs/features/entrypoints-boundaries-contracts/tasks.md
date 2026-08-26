# Entry Points, Boundaries, and Contracts Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**Standing skip — discrimination sensor**: the user runs Stryker manually; do not run the sensor's fault-injection pass. Every other Verifier step (spec-anchored coverage check, gate check, code-quality check) still runs as documented.

---

**Design**: `.specs/features/entrypoints-boundaries-contracts/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase sampling and project guidelines. Guidelines found: [`AGENTS.md`](file:///d:/workspace/csharp2md/AGENTS.md) (retrieval-led reasoning, Roslyn API verification, multi-csproj test execution note).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Project wiring / interfaces | none | Build gate only (interface definitions, declarative code) | `src/Csharp2Md.Analysis/Classification/IClassifierPass.cs` | build gate only |
| Domain facts / relations / guards | unit | All branches; 1:1 to spec ACs; all listed edge cases | `tests/Csharp2Md.Domain.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Classification passes (classifier logic) | unit + integration | All branches; 1:1 to spec ACs; fixture-backed assertions | `tests/Csharp2Md.Analysis.Tests/Classification/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Pipeline stage / orchestrator | integration | Stage wiring, count reporting, composability | `tests/Csharp2Md.Analysis.Tests/Pipeline/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Storage mapping (classifier facts) | unit | Classifier-produced facts round-trip correctly | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| CLI surface | unit | No new flags; CLI→Domain isolation | `tests/Csharp2Md.Cli.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj` |
| End-to-end (fixture analyze) | integration | Full pipeline with classifier output assertions | `tests/Csharp2Md.Analysis.Tests/Classification/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |

## Gate Check Commands

> Generated from codebase — confirm before Execute. Multi-csproj `dotnet test` hits MSB1008; run specific test projects.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After unit-test-only tasks (Domain, isolated classifier) | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Full | After Analysis classifier/pipeline tasks | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj` |
| Build | After phase completion | `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |

---

## Execution Plan

Phases are ordered and run sequentially — each phase completes before the next begins, and tasks within a phase execute in order.

### Phase 1: Infrastructure

Foundation: extend `SnapshotAccumulator`, create `IClassifierPass` interface, create `ClassifierContext`, wire `ClassificationAndPromotionStage`.

```
T1 → T2 → T3 → T4 → T5
```

### Phase 2: Component and entry point classification

Create `ComponentPass`, `EntryPointPass`, and their tests against the fixture.

```
T6 → T7 → T8
```

### Phase 3: Boundary classification

Create `BoundaryPass` (HTTP inbound, HTTP outbound, messaging inbound, messaging outbound) and tests.

```
T9 → T10 → T11 → T12
```

### Phase 4: Contract classification and relation emission

Create `ContractPass`, `RelationPass`, and integration tests.

```
T13 → T14 → T15
```

### Phase 5: End-to-end integration, determinism, and fixture

Full pipeline integration tests, determinism assertions, fixture verification, storage round-trip.

```
T16 → T17 → T18 → T19 → T20
```

---

## Task Breakdown

### Phase 1: Infrastructure

#### T1: Extend SnapshotAccumulator with AddCandidate and AddUnresolved

**What**: Add `AddCandidate(CandidateLink)` and `AddUnresolved(UnresolvedRecord)` methods to `SnapshotAccumulator` so `ToSnapshot()` populates those collections instead of hardcoding empty arrays.
**Where**: `src/Csharp2Md.Analysis/Pipeline/SnapshotAccumulator.cs`
**Depends on**: None
**Reuses**: Existing `AddFact`, `AddRelation`, `AddDiagnostic` patterns
**Requirement**: EBC-38

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `AddCandidate(CandidateLink)` method adds to internal list
- [x] `AddUnresolved(UnresolvedRecord)` method adds to internal list
- [x] `ToSnapshot()` populates `Candidates` and `Unresolved` from those lists
- [x] Unit tests assert both methods and snapshot population
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T2: Create IClassifierPass interface and ClassifierPassResult

**What**: Define the `IClassifierPass` internal interface and `ClassifierPassResult` record struct in a new `Classification/` directory under Analysis.
**Where**: `src/Csharp2Md.Analysis/Classification/IClassifierPass.cs`
**Depends on**: T1
**Reuses**: `IRegisteredContextDetector` pattern (simple interface, one method)
**Requirement**: EBC-27

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `IClassifierPass` interface defined with `Name` and `Execute(ClassifierContext, CancellationToken)`
- [x] `ClassifierPassResult` record struct with `FactCount`, `RelationCount`, `CandidateCount`, `UnresolvedCount`
- [x] Compiles without errors
- [x] Gate check passes: `dotnet build`

**Tests**: none
**Gate**: build

---

#### T3: Create ClassifierContext

**What**: Create `ClassifierContext` that provides classifier passes with a read-only snapshot view plus the accumulator for writes, and computed indexes (facts by type, observations by kind, observations by owner, symbols by signature key).
**Where**: `src/Csharp2Md.Analysis/Classification/ClassifierContext.cs`
**Depends on**: T2
**Reuses**: Index patterns from `ContainsRelationEmitter` (facts by type, symbols by signature)
**Requirement**: EBC-28

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `ClassifierContext` constructed from `PipelineContext`
- [x] `FactsByType<T>()` returns facts of that type from the current snapshot
- [x] `ObservationsByKind(ObservationKind)` returns observations filtered by kind
- [x] `ObservationsByOwner(FactReference)` returns observations for a given owner
- [x] `SymbolsBySignatureKey()` returns symbols indexed by `projectId + signature`
- [x] `Refresh()` method rebuilds indexes from current accumulator state
- [x] Unit tests assert index correctness
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T4: Create ClassificationAndPromotionStage

**What**: Create the `ClassificationAndPromotionStage` that implements `IPipelineStage`, accepts `ImmutableArray<IClassifierPass>`, runs passes in order, refreshes `ClassifierContext` between passes, and reports aggregate counts.
**Where**: `src/Csharp2Md.Analysis/Classification/ClassificationAndPromotionStage.cs`
**Depends on**: T3
**Reuses**: `ObservationExtractionStage` pattern (build context, delegate to internal workers, report counts)
**Requirement**: EBC-27, EBC-29, EBC-30, EBC-31

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `Name` returns `"Classification and Promotion"`
- [x] Executes all passes in registration order
- [x] Refreshes `ClassifierContext` between passes
- [x] Reports aggregate `StageResult` (sum of pass counts)
- [x] Sets `HasUnknownsOrCandidatesOrFrontiers` when candidates/unresolved exist
- [x] Skips gracefully when no passes are registered (returns zero counts)
- [x] Unit tests with a mock no-op pass and a counting test pass
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T5: Wire ClassificationAndPromotionStage into PipelineStages

**What**: Replace `ClassificationAndPromotionStub` at index 3 in `PipelineStages.CreateDefault()` with the new `ClassificationAndPromotionStage` carrying 5A's classifier passes.
**Where**: `src/Csharp2Md.Analysis/Pipeline/PipelineStages.cs`
**Depends on**: T4
**Reuses**: Existing `.SetItem(N, stage)` pattern
**Requirement**: EBC-27

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `PipelineStages.CreateDefault()` sets index 3 to `ClassificationAndPromotionStage` with an initially empty pass list
- [x] Existing pipeline tests still pass (stage name matches, zero output from empty passes)
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

### Phase 2: Component and entry point classification

#### T6: Create ComponentPass

**What**: Create `ComponentPass` that pre-scans observations to identify boundary-candidate symbols, then creates one `Component` fact per project that has at least one candidate.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/ComponentPass.cs`
**Depends on**: T5
**Reuses**: `Component.Create`, `ClassifierContext.FactsByType<Project>()`, `ClassifierContext.ObservationsByKind()`
**Requirement**: EBC-01, EBC-02, EBC-03, EBC-04

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Pre-scans `BaseType`, `AttributeUsage`, `RouteDeclaration`, `MessageOperation` observations to identify boundary-candidate symbols
- [x] Creates `Component` for each project with candidates; skips projects without
- [x] `Component.Name` is the project's logical relative path
- [x] `Component.Owners` contains references to candidate symbols
- [x] Unit tests with synthetic observations assert component creation/skip
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T7: Create EntryPointPass

**What**: Create `EntryPointPass` that classifies callable symbols as entry points from `ControllerBase` action methods and `IIntegrationEventHandler<T>.HandleAsync`.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/EntryPointPass.cs`
**Depends on**: T6
**Reuses**: `EntryPoint.Create`, `ClassifierContext`, `ClassifierIdentity.Create`
**Requirement**: EBC-05, EBC-08, EBC-09, EBC-19

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Identifies `ControllerBase` descendants via `BaseType` observations on declaring types
- [x] Creates `EntryPoint` for each callable action method on a `ControllerBase` descendant
- [x] Creates `EntryPoint` for `HandleAsync` on `IIntegrationEventHandler<T>` implementors
- [x] Skips non-callable symbols and non-controller/handler types
- [x] Records diagnostic for controller actions without route declarations
- [x] Classifier identity: `csharp2md.classifier.entrypoint` version 1
- [x] Unit tests with synthetic facts/observations
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T8: Register ComponentPass and EntryPointPass; fixture integration test

**What**: Register `ComponentPass` and `EntryPointPass` in the classifier pass list within `PipelineStages.CreateDefault()`. Add an integration test that analyzes `Acme.Orders.slnx` and asserts components and entry points exist.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/ComponentEntryPointIntegrationTests.cs`
**Depends on**: T7
**Reuses**: Existing `AnalysisEngine` test infrastructure
**Requirement**: EBC-01, EBC-02, EBC-05, EBC-35

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `PipelineStages.CreateDefault()` includes `ComponentPass` and `EntryPointPass`
- [x] Integration test analyzes `Acme.Orders.slnx` and asserts `Acme.Orders` has a `Component`
- [x] Integration test asserts `OrdersController.GetOrderStatus` is an `EntryPoint`
- [x] Integration test asserts `OrderPlacedEventHandler.HandleAsync` is an `EntryPoint`
- [x] Integration test asserts `Acme.Shared.Contracts` has no `Component`
- [x] Classification stage reports non-zero fact count
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: integration
**Gate**: full

---

### Phase 3: Boundary classification

#### T9: Create BoundaryPass — HTTP inbound sub-classifier

**What**: Implement the HTTP inbound sub-classifier within `BoundaryPass`: for each entry point from a `ControllerBase` descendant, create an inbound HTTP `BoundaryOperation` from its `RouteDeclaration` observation.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/BoundaryPass.cs`
**Depends on**: T8
**Reuses**: `BoundaryOperation.Create(Inbound, Http, ...)`, `ClassifierContext`
**Requirement**: EBC-06, EBC-33

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Creates inbound HTTP `BoundaryOperation` with protocol `http`, direction `inbound`
- [x] `protocolOperationKey` carries the route template from `RouteDeclaration`
- [x] Skips controller actions with no `RouteDeclaration` (already diagnosed by `EntryPointPass`)
- [x] Classifier identity: `csharp2md.classifier.http-inbound` version 1
- [x] Unit tests with synthetic observations
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T10: BoundaryPass — HTTP outbound sub-classifier

**What**: Implement the HTTP outbound sub-classifier: detect `IHttpClientFactory.CreateClient(name)` followed by HTTP method invocations, create outbound HTTP `BoundaryOperation`, candidate `ExternalSystem`, and `CandidateLink`.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/BoundaryPass.cs`
**Depends on**: T9
**Reuses**: `BoundaryOperation.Create(Outbound, Http, ...)`, `ExternalSystem.Create`, `CandidateLink.Create`
**Requirement**: EBC-10, EBC-11, EBC-12, EBC-13, EBC-14, EBC-15, EBC-16

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Detects `CreateClient(name)` invocations from observation payloads
- [x] Extracts client name literal, HTTP method, route literal
- [x] Creates outbound `BoundaryOperation` with `destinationScope`, `httpMethod`, `route`
- [x] Creates candidate `ExternalSystem` with `ClientName` literal role
- [x] Creates `CandidateLink` of kind `Targets`
- [x] Handles multiple outbound calls per callable (distinct tuples)
- [x] Classifier identity: `csharp2md.classifier.http-outbound` version 1
- [x] Unit tests with synthetic observations for `PlaceOrderAsync`, `NotifyOrderPlacedAsync`, `RequestShippingAsync` patterns
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T11: BoundaryPass — messaging outbound sub-classifier

**What**: Implement the messaging outbound sub-classifier: detect `PublishAsync`/`Publish` invocations on `IEventBus`, create outbound messaging `BoundaryOperation`.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/BoundaryPass.cs`
**Depends on**: T10
**Reuses**: `BoundaryOperation.Create(Outbound, Messaging, ...)`, `ClassifierContext.ObservationsByKind(MessageOperation)`
**Requirement**: EBC-17

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Detects `MessageOperation` observations with method `PublishAsync` or `Publish`
- [x] Extracts `TEvent` fully qualified type name from observation payload
- [x] Creates outbound `BoundaryOperation` with protocol `messaging`, direction `outbound`, `protocolOperationKey` = event FQN
- [x] Classifier identity: `csharp2md.classifier.messaging` version 1
- [x] Unit tests with synthetic `MessageOperation` observations
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T12: BoundaryPass — messaging inbound sub-classifier and fixture integration

**What**: Implement the messaging inbound sub-classifier: detect `IIntegrationEventHandler<T>` implementations, create inbound messaging `BoundaryOperation`. Register `BoundaryPass` in the pipeline and add fixture integration tests for all four sub-classifiers.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/BoundaryIntegrationTests.cs`
**Depends on**: T11
**Reuses**: `BoundaryOperation.Create(Inbound, Messaging, ...)`, existing `AnalysisEngine` test infrastructure
**Requirement**: EBC-18, EBC-20

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Detects `BaseType` observations for `IIntegrationEventHandler<TEvent>`
- [x] Extracts `TEvent` FQN and creates inbound messaging `BoundaryOperation`
- [x] `BoundaryPass` registered in `PipelineStages.CreateDefault()`
- [x] Integration test: `OrdersController.GetOrderStatus` → inbound HTTP boundary with route `orders/{id}`
- [x] Integration test: `PlaceOrderAsync` → outbound HTTP boundaries for `PaymentService`, outbound messaging for `OrderPlaced`
- [x] Integration test: `NotifyOrderPlacedAsync` → outbound HTTP for `NotificationService`
- [x] Integration test: `RequestShippingAsync` → outbound HTTP for `ShippingService`
- [x] Integration test: `OrderPlacedEventHandler.HandleAsync` → inbound messaging boundary
- [x] Integration test: candidate `ExternalSystem` facts exist for all three HTTP client names
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: integration
**Gate**: full

---

### Phase 4: Contract classification and relation emission

#### T13: Create ContractPass

**What**: Create `ContractPass` that groups messaging boundary operations by event type FQN, verifies cross-project sharing, and creates `Contract`, `ContractBinding`, and `ContractRevision` facts.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/ContractPass.cs`
**Depends on**: T12
**Reuses**: `Contract.Create`, `ContractBinding.Create`, `ContractRevision.Create`, `ClassifierContext`
**Requirement**: EBC-21, EBC-22, EBC-24, EBC-25, EBC-26

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Groups messaging boundaries by `TEvent` FQN
- [x] Creates `Contract` only when both outbound and inbound exist AND `TEvent` is in a shared project
- [x] Creates `ContractBinding` for each boundary operation with `PayloadRole = "request"`
- [x] Creates `ContractRevision` with structural fingerprint from event type's public properties
- [x] Skips unpaired publish-only events (e.g., `PaymentProcessed`)
- [x] Skips intra-project events (publisher and handler in same project)
- [x] Classifier identity: `csharp2md.classifier.contract-messaging` version 1
- [x] Unit tests with synthetic boundary operations
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T14: Create RelationPass

**What**: Create `RelationPass` that emits confirmed `implements-operation` and `uses-contract` relations for all boundary operations and contract bindings.
**Where**: `src/Csharp2Md.Analysis/Classification/Passes/RelationPass.cs`
**Depends on**: T13
**Reuses**: `ConfirmedRelation.Create`, `FacetBinding.Create` with `payload-role`, `EvidenceChain.Create`
**Requirement**: EBC-07, EBC-14, EBC-20, EBC-23, EBC-34

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Creates `implements-operation` for each `EntryPoint`/`BoundaryOperation` pair (source = callable Symbol, target = BoundaryOperation)
- [x] `implements-operation` supplies `sourceFact` as materialized `Symbol` with `Callable`
- [x] Creates `uses-contract` for each `ContractBinding` (source = BoundaryOperation, target = Contract, facets include `payload-role`)
- [x] All relations carry evidence method `Semantic`, non-empty `derived_from`, analysis variants
- [x] Records `UnresolvedRecord` for unresolvable messaging types (EBC-34)
- [x] Unit tests assert relation shape guards pass
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T15: Register ContractPass and RelationPass; contract integration test

**What**: Register `ContractPass` and `RelationPass` in the pipeline. Add integration tests that analyze `Acme.Orders.slnx` and assert contract and relation output.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/ContractRelationIntegrationTests.cs`
**Depends on**: T14
**Reuses**: Existing `AnalysisEngine` test infrastructure
**Requirement**: EBC-07, EBC-21, EBC-23, EBC-24

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `PipelineStages.CreateDefault()` includes all five passes in order
- [ ] Integration test: `Contract` exists for `Acme.Shared.Contracts.OrderPlaced`
- [ ] Integration test: `ContractBinding` links both publish and handler boundaries to the contract
- [ ] Integration test: `uses-contract` confirmed relations exist with `payload-role` facet
- [ ] Integration test: `PaymentProcessed` does NOT produce a `Contract` (no handler)
- [ ] Integration test: `implements-operation` relations exist for HTTP and messaging entry points
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: integration
**Gate**: full

---

### Phase 5: End-to-end integration, determinism, and fixture

#### T16: End-to-end pipeline integration — full classifier output

**What**: Add integration tests that analyze `Acme.Orders.slnx` through the full pipeline and assert complete classifier output: fact counts, relation counts, candidates, unresolved, stage reports.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/FullClassifierPipelineTests.cs`
**Depends on**: T15
**Reuses**: Existing `PackageContentsTests` patterns
**Requirement**: EBC-35, EBC-36, EBC-37

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Classification stage reports non-zero facts and relations
- [ ] Validation, Retrieval, Batch stubs still report zeros (EBC-36)
- [ ] Persistence stages the snapshot including classifier output (EBC-37)
- [ ] Package contains `CandidateLink` records for outbound HTTP destinations
- [ ] Package contains `UnresolvedRecord` if any messaging type was unresolvable
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: integration
**Gate**: full

---

#### T17: Determinism and clone-path independence tests

**What**: Add tests that verify classifier output is deterministic across re-runs and clone-path-independent.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/ClassifierDeterminismTests.cs`
**Depends on**: T16
**Reuses**: Existing `ClonePathIndependenceTests` and `CanonicalResultOrderTests` patterns
**Requirement**: EBC-32, EBC-39, EBC-40, EBC-41

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Two analysis runs produce identical classifier fact identities
- [ ] Two analysis runs produce byte-identical canonical payload files
- [ ] No absolute filesystem path in classifier-produced facts, relations, candidates, or diagnostics
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: integration
**Gate**: full

---

#### T18: Analysis surface isolation and invariant tests

**What**: Add tests that verify Analysis public surface does not expose Roslyn types from classifiers, CLI has no Domain reference, in-memory adapter produces classifier output without filesystem writes.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/ClassifierIsolationTests.cs`
**Depends on**: T17
**Reuses**: Existing `AnalysisPublicSurfaceTests`, `NoFilesystemWriteTests` patterns
**Requirement**: EBC-42, EBC-43, EBC-44, EBC-45

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Analysis public surface has no `Microsoft.CodeAnalysis` types from classifier paths
- [ ] `Csharp2Md.Cli` has no project reference to `Csharp2Md.Domain`
- [ ] In-memory adapter run produces classifier facts and relations, no filesystem writes
- [ ] No secrets in classifier-produced content
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj`

**Tests**: unit
**Gate**: full

---

#### T19: Storage round-trip for classifier facts

**What**: Verify that classifier-produced `Component`, `EntryPoint`, `BoundaryOperation`, `ExternalSystem`, `Contract`, `ContractBinding`, `ContractRevision` facts, plus `CandidateLink` and `UnresolvedRecord`, survive Storage write-read round-trip.
**Where**: `tests/Csharp2Md.Storage.Tests/ClassifierFactMappingTests.cs`
**Depends on**: T18
**Reuses**: Existing Storage mapping test patterns
**Requirement**: EBC-37

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Each classifier fact type survives JSON serialization/deserialization through `DomainMapper`
- [ ] `CandidateLink` and `UnresolvedRecord` survive round-trip
- [ ] `ConfirmedRelation` with `ImplementsOperation` and `UsesContract` kinds survive round-trip
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj`

**Tests**: unit
**Gate**: build

---

#### T20: Composability test — external classifier pass registration

**What**: Add a test that registers a test-only no-op classifier pass alongside the real 5A passes and verifies the stage executes all passes and reports combined counts without interference.
**Where**: `tests/Csharp2Md.Analysis.Tests/Classification/ComposabilityTests.cs`
**Depends on**: T19
**Reuses**: `ClassificationAndPromotionStage` constructor
**Requirement**: EBC-27, EBC-28, EBC-29, EBC-30

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Test-only `CountingClassifierPass` adds a dummy fact and reports count
- [ ] Stage executes all passes (5A + test pass) in registration order
- [ ] Aggregate counts include both 5A and test pass output
- [ ] Test pass sees facts from earlier 5A passes in the context
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj`

**Tests**: integration
**Gate**: full

---

## Phase Execution Map

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5

Phase 1:  T1 → T2 → T3 → T4 → T5
Phase 2:  T6 → T7 → T8
Phase 3:  T9 → T10 → T11 → T12
Phase 4:  T13 → T14 → T15
Phase 5:  T16 → T17 → T18 → T19 → T20
```

Execution is strictly sequential — there is no intra-phase parallelism. A single agent (or batch worker) works one task at a time, in order.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: Extend SnapshotAccumulator | 1 file, 2 methods | ✅ Granular |
| T2: Create IClassifierPass | 1 file, 1 interface + 1 struct | ✅ Granular |
| T3: Create ClassifierContext | 1 file, 1 class | ✅ Granular |
| T4: Create ClassificationAndPromotionStage | 1 file, 1 class | ✅ Granular |
| T5: Wire stage into pipeline | 1 file modification | ✅ Granular |
| T6: Create ComponentPass | 1 file, 1 class | ✅ Granular |
| T7: Create EntryPointPass | 1 file, 1 class | ✅ Granular |
| T8: Register + integration test | 1 test file | ✅ Granular |
| T9: BoundaryPass HTTP inbound | 1 file, 1 sub-classifier | ✅ Granular |
| T10: BoundaryPass HTTP outbound | 1 file extension | ✅ Granular |
| T11: BoundaryPass messaging outbound | 1 file extension | ✅ Granular |
| T12: BoundaryPass messaging inbound + reg | 1 test file | ✅ Granular |
| T13: Create ContractPass | 1 file, 1 class | ✅ Granular |
| T14: Create RelationPass | 1 file, 1 class | ✅ Granular |
| T15: Register + contract integration test | 1 test file | ✅ Granular |
| T16: Full pipeline integration test | 1 test file | ✅ Granular |
| T17: Determinism tests | 1 test file | ✅ Granular |
| T18: Isolation tests | 1 test file | ✅ Granular |
| T19: Storage round-trip tests | 1 test file | ✅ Granular |
| T20: Composability test | 1 test file | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None (first in Phase 1) | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | First in Phase 2, after Phase 1 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T7 | T7 → T8 | ✅ Match |
| T9 | T8 | First in Phase 3, after Phase 2 | ✅ Match |
| T10 | T9 | T9 → T10 | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T12 | First in Phase 4, after Phase 3 | ✅ Match |
| T14 | T13 | T13 → T14 | ✅ Match |
| T15 | T14 | T14 → T15 | ✅ Match |
| T16 | T15 | First in Phase 5, after Phase 4 | ✅ Match |
| T17 | T16 | T16 → T17 | ✅ Match |
| T18 | T17 | T17 → T18 | ✅ Match |
| T19 | T18 | T18 → T19 | ✅ Match |
| T20 | T19 | T19 → T20 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1: Extend SnapshotAccumulator | Pipeline (accumulator) | unit | unit | ✅ OK |
| T2: Create IClassifierPass | Project wiring / interfaces | none | none | ✅ OK |
| T3: Create ClassifierContext | Classification (context) | unit | unit | ✅ OK |
| T4: Create ClassificationAndPromotionStage | Classification (stage) | unit | unit | ✅ OK |
| T5: Wire stage into pipeline | Pipeline (wiring) | unit | unit | ✅ OK |
| T6: Create ComponentPass | Classification passes | unit | unit | ✅ OK |
| T7: Create EntryPointPass | Classification passes | unit | unit | ✅ OK |
| T8: Register + integration test | Classification passes | integration | integration | ✅ OK |
| T9: BoundaryPass HTTP inbound | Classification passes | unit | unit | ✅ OK |
| T10: BoundaryPass HTTP outbound | Classification passes | unit | unit | ✅ OK |
| T11: BoundaryPass messaging outbound | Classification passes | unit | unit | ✅ OK |
| T12: BoundaryPass messaging inbound + reg | Classification passes | integration | integration | ✅ OK |
| T13: Create ContractPass | Classification passes | unit | unit | ✅ OK |
| T14: Create RelationPass | Classification passes | unit | unit | ✅ OK |
| T15: Register + contract integration | Classification passes | integration | integration | ✅ OK |
| T16: Full pipeline integration | End-to-end pipeline | integration | integration | ✅ OK |
| T17: Determinism tests | End-to-end pipeline | integration | integration | ✅ OK |
| T18: Isolation tests | CLI / Surface | unit | unit | ✅ OK |
| T19: Storage round-trip | Storage mapping | unit | unit | ✅ OK |
| T20: Composability test | Pipeline stage / orchestrator | integration | integration | ✅ OK |
