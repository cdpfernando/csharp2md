# Entry Points, Boundaries, and Contracts Specification

## Problem Statement

Workstream 4 fills the observation ledger with the ten registry observation kinds and commits structural facts. But the package contains no architecture or contract facts and only `contains` relations. An LLM reading the package cannot answer "which entry points start execution, which boundary operations cross a protocol boundary, or which contracts are shared" — those require promotion from observations. This workstream introduces the first versioned classifiers: they consume the immutable ledger and promote observations into architecture and contract facts with confirmed relations, candidates, and unresolved records.

## Goals

- [ ] Classify entry points from symbols that carry `Callable` and are reachable from a recognized ASP.NET Core host or framework bootstrap pattern.
- [ ] Classify inbound and outbound HTTP boundary operations from route declarations, `HttpClient` invocations, and controller action methods.
- [ ] Classify inbound and outbound messaging boundary operations from `IEventBus.PublishAsync` invocations and `IIntegrationEventHandler<T>` implementations.
- [ ] Create project-as-component facts for projects that contain at least one entry point or boundary symbol, as a structural placeholder that workstream 5D refines.
- [ ] Create `Contract` facts for messaging boundaries when the event type is a named CLR type shared across projects within the same solution.
- [ ] Emit confirmed `implements-operation` and `uses-contract` relations with full evidence chains.
- [ ] Record candidate external-system links and unresolved records for outbound HTTP destinations that lack configuration evidence.
- [ ] Replace `ClassificationAndPromotionStub` with a composable stage that accepts classifier passes, so workstreams 5B–5D can add theirs without modifying 5A's code.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| gRPC boundary operations and protobuf-name contracts | Deferred to post-roadmap; no classifier rules or fixture coverage in this workstream |
| CLI, scheduler, and function entry points / boundary operations | Deferred to 5D which owns host configuration and deployment units |
| `invokes` confirmed relations (call linking) | Owned by workstream 5B |
| `accesses-data`, `operates-on` relations and persistence facts | Owned by workstream 5C |
| `belongs-to`, `included-in`, component merging, deployment units, DI-based grouping | Owned by workstream 5D; this workstream creates minimal project-as-component only |
| `configured-by` relations and `ConfigurationBinding` facts | Owned by workstream 5D |
| `targets` confirmed relation (outbound → inbound resolution) | Requires configuration evidence from 5D; this workstream records candidates/unresolved only |
| HTTP contracts (no schema key without OpenAPI/protobuf) | Deferred to workstream 6 which parses non-C# specification files |
| `maps-to` with `contract-implementation` or `serialization-binding` | Deferred to workstreams 5D and 6 |
| Source-projection files, catalogs, postings, Markdown | Owned by workstream 6 |
| Batch manifest and cross-solution composition | Owned by workstream 7 |
| Changes to Domain descriptor tables or `contracts/taxonomy-registry.json` bytes | Workstream 1 is closed |
| Reading `appsettings.json` values to confirm external-system destinations | Non-C# adapter owned by 5D; 5A uses only the client name literal from observations |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here. Nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Protocol coverage | HTTP and messaging classifiers only; gRPC, CLI, scheduler, function deferred | User decision: gRPC deferred to post-roadmap; CLI/scheduler/function to 5D | y |
| Component identity | 5A creates one `Component` per project that contains at least one entry point or boundary symbol, using the project's logical relative path as the component name | `EntryPoint.Create` and `BoundaryOperation.Create` require `FactReference owningComponent`; 5D refines components (merging, DI grouping, deployment units) | y |
| Contract proof paths | Messaging: shared CLR type across projects within same solution. No HTTP or gRPC contracts in 5A | A contract requires a proven schema key; HTTP has none without OpenAPI; gRPC deferred | y |
| Outbound HTTP external-system resolution | Client name literal from `IHttpClientFactory.CreateClient("name")` invocation observation as `destinationScope`; candidate external system using the name; no confirmed `targets` relation | Config-backed resolution requires reading `appsettings.json` values, which is a non-C# adapter owned by 5D | y |
| Classifier pass ordering | Single `ClassificationAndPromotion` stage with composable classifier passes; internal order: components → entry points → boundary operations → contracts/bindings → relations | The DAG between 5A and 5B/5C/5D is: 5A has no dependency on the others; they may depend on 5A's facts | y |
| Entry point detection | A callable is an entry point when it is an action method on a class deriving from `ControllerBase`, or a delegate passed to `MapGet`/`MapPost`/`MapPut`/`MapDelete` | These are the two ASP.NET Core patterns the fixture exercises; further patterns (e.g., `BackgroundService.ExecuteAsync`) belong to 5D | n |
| Inbound HTTP identity | The entry point's symbol plus its route declaration observation form the inbound boundary operation's `protocolOperationKey` | The taxonomy says inbound identities use the owning component plus their protocol operation key | n |
| Outbound HTTP identity | Owner component, direction `outbound`, protocol `http`, destination scope (client name), HTTP method, and route literal | Matches `BoundaryOperation.CreateOutboundHttp` signature; the factory pattern identifies the client name | n |
| Messaging inbound identity | A class implementing `IIntegrationEventHandler<TEvent>` produces an inbound messaging boundary operation keyed by the handler's fully qualified type name | The interface pattern is the fixture's messaging subscription mechanism | n |
| Messaging outbound identity | An `IEventBus.PublishAsync<TEvent>` invocation produces an outbound messaging boundary operation keyed by the event type's fully qualified name | `MessageOperation` observations capture the method name and type argument | n |
| Messaging contract schema key | The fully qualified CLR type name of the event type shared across projects | The type's project membership proves cross-project sharing; the FQN is a stable schema key | n |
| Classifier identity format | `csharp2md.classifier.{protocol}-{direction}` version 1, evidence method `semantic` | Matches the pattern from WS4's `csharp2md.inventory.contains` v1; classifiers are versioned independently | n |
| Stub stage replacement | `ClassificationAndPromotionStub` at index 3 is replaced with a real `ClassificationAndPromotionStage` that accepts `ImmutableArray<IClassifierPass>` | Mirrors WS4's pattern where `PipelineStages.CreateDefault()` replaces stub items by index | n |
| Snapshot accumulator usage | Classifiers add facts, relations, candidates, and unresolved records to the existing `SnapshotAccumulator` | The accumulator already exposes `AddFact`, `AddRelation`; candidates and unresolved need new methods or direct snapshot composition | n |
| `StageResult` counts | The Classification and Promotion stage reports its own fact, observation, and relation counts | `StageResult` already has `FactCount`, `ObservationCount`, `RelationCount`; classifier produces facts and relations, not new observations | n |
| Existing WS4 tests unchanged | No modification to existing WS4 tests that assert `ROSE-20` (no non-`contains` confirmed relations) or `ROSE-60` (Classification stub reports zeros) | Those tests run against the stub pipeline; 5A's pipeline replaces the stub, so integration tests use the new stage and assert non-zero classifier output | n |

**Open questions:** none — all resolved or logged above.

---

## Implicit-requirement dimensions sweep

Large scope, so every dimension resolves to a requirement or an explicit exclusion.

| Dimension | Coverage |
| --- | --- |
| Input validation and bounds | EBC-01, EBC-09, EBC-33 — classifier rules validate observation payloads structurally; a fact with an empty component reference is a construction-time rejection (Domain guards); a symbol without `Callable` cannot become an entry point |
| Failure and partial-failure states | EBC-28, EBC-29, EBC-30, EBC-31 — unresolvable receiver type produces an unresolved record; missing route literal produces a diagnostic; a classifier that cannot promote stays a candidate or unresolved, never corrupts the graph |
| Idempotency, retry, duplicate handling | EBC-32 — re-running the classifier on the same observation set produces identical facts, relations, candidates, and unresolved records; deterministic identity from Domain guards |
| Auth boundaries and rate limits | N/A because the engine is a local in-process tool with no network surface |
| Concurrency and ordering | EBC-32, EBC-33 — observation processing order does not affect classifier output; identity is payload-derived, not order-derived |
| Data lifecycle and expiry | N/A because generated packages are operator-owned files |
| Observability | EBC-28, EBC-29, EBC-30, EBC-31, EBC-34 — unresolved HTTP destinations, unresolvable messaging types, and missing route literals produce named diagnostics or unresolved records |
| External-dependency failure | N/A because classifiers consume the in-memory accumulator, not Roslyn or filesystem |
| State-transition integrity | EBC-35, EBC-36, EBC-37 — the Classification stage reports non-zero counts; later stubs still report zeros; Persistence stages the accumulated snapshot including classifier output |

---

## User Stories

### P1: Project-as-component promotion ⭐ MVP

**User Story**: As a classifier, I want a `Component` fact for each project that has boundary symbols so that entry points and boundary operations have a legal owning component reference.

**Why P1**: `EntryPoint.Create` and `BoundaryOperation.Create` require `FactReference owningComponent`. No component = no boundary facts.

**Acceptance Criteria**:

1. WHEN a project contains at least one symbol that a classifier in this workstream promotes to an entry point or boundary operation THEN the package SHALL contain a `Component` fact whose name is the project's logical relative path. (EBC-01)
2. WHEN a project contains no entry points and no boundary operations THEN the classifier SHALL NOT create a `Component` for that project. (EBC-02)
3. The `Component` fact's `Owners` collection SHALL contain the `FactReference` of every `Symbol` fact in that project that was promoted to an entry point or boundary operation. (EBC-03)
4. The `Component.Create` call SHALL use the `SolutionId` from the existing `Solution` structural fact. (EBC-04)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert `Acme.Orders` has a `Component` fact; assert `Acme.Shared.Contracts` has no `Component` fact (it has no boundary symbols); assert `Acme.Broken` has no `Component` fact.

---

### P1: HTTP entry point classification ⭐ MVP

**User Story**: As an LLM reading the package, I want entry points classified from controller action methods and minimal-API route delegates so that I can find where HTTP execution begins.

**Why P1**: Entry points answer "where does execution start?" — the most basic architecture question.

**Acceptance Criteria**:

1. WHEN a symbol carries the `Callable` facet and its declaring type derives from `Microsoft.AspNetCore.Mvc.ControllerBase` THEN the classifier SHALL create an `EntryPoint` fact for that symbol with the project-as-component as its owning component. (EBC-05)
2. WHEN a `RouteDeclaration` observation exists for a callable on a `ControllerBase` descendant THEN the classifier SHALL create an inbound `BoundaryOperation` with protocol `http`, direction `inbound`, and the route template as the `protocolOperationKey`. (EBC-06)
3. WHEN a callable is an entry point THEN the package SHALL contain a confirmed `implements-operation` relation from that symbol to the inbound boundary operation with evidence method `semantic`, classifier identity `csharp2md.classifier.http-inbound` version 1, and a non-empty `derived_from` chain citing the `RouteDeclaration` and `AttributeUsage` observations. (EBC-07)
4. WHEN a callable on a `ControllerBase` descendant has no `RouteDeclaration` observation THEN the classifier SHALL still create the `EntryPoint` but SHALL record a diagnostic naming the symbol and SHALL NOT create a boundary operation for it. (EBC-08)
5. WHEN a callable has `Callable` but its declaring type does not derive from `ControllerBase` and is not a minimal-API delegate THEN the classifier SHALL NOT create an entry point for it. (EBC-09)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert `OrdersController.GetOrderStatus` is an `EntryPoint`; assert an inbound HTTP `BoundaryOperation` exists with route `orders/{id}`; assert `OrderService.PlaceOrderAsync` is NOT an entry point; assert an `implements-operation` relation links the symbol to the boundary operation.

---

### P1: Outbound HTTP boundary classification ⭐ MVP

**User Story**: As an LLM, I want outbound HTTP calls classified as boundary operations with their destination and route so that I can trace which external services a flow contacts.

**Why P1**: Outbound HTTP is the most common cross-boundary interaction. Without it, the package cannot show service-to-service communication.

**Acceptance Criteria**:

1. WHEN a callable invokes `IHttpClientFactory.CreateClient(name)` followed by an HTTP method (`PostAsJsonAsync`, `GetAsync`, `PutAsJsonAsync`, `DeleteAsync`, `SendAsync`) THEN the classifier SHALL create an outbound `BoundaryOperation` with protocol `http`, direction `outbound`, `destinationScope` set to the client name literal, the HTTP method, and the route literal from the invocation. (EBC-10)
2. WHEN the client name literal is a constant string THEN the `destinationScope` SHALL use that string value. (EBC-11)
3. WHEN the route argument is a constant string THEN the boundary operation's `Route` SHALL carry that literal with role `Route`. (EBC-12)
4. WHEN a callable creates multiple outbound HTTP calls to different client names THEN the classifier SHALL create one outbound `BoundaryOperation` per distinct `(clientName, httpMethod, route)` tuple. (EBC-13)
5. The outbound `BoundaryOperation` SHALL carry the classifier identity `csharp2md.classifier.http-outbound` version 1 in its confirmed `implements-operation` relation. (EBC-14)
6. WHEN a callable invokes an HTTP method on a client created by `IHttpClientFactory.CreateClient(name)` THEN the classifier SHALL create a `CandidateLink` of kind `targets` from the outbound boundary operation to a candidate `ExternalSystem` fact whose name is the client name literal. (EBC-15)
7. The candidate `ExternalSystem` SHALL carry the `ClientName` literal role. (EBC-16)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert `OrderService.PlaceOrderAsync` produces an outbound HTTP `BoundaryOperation` with `destinationScope="PaymentService"`, method `POST`, route `payments/authorize`; assert `NotifyOrderPlacedAsync` produces a second outbound HTTP operation with `destinationScope="NotificationService"`; assert `RequestShippingAsync` produces a third with `destinationScope="ShippingService"`; assert each has a `CandidateLink` of kind `targets` to an `ExternalSystem`; assert no confirmed `targets` relation exists.

---

### P1: Messaging boundary classification ⭐ MVP

**User Story**: As an LLM, I want publish and subscribe boundaries classified so that I can trace event-driven flows across components.

**Why P1**: The fixture exercises both publish and handler patterns. Without messaging classification, event-driven architecture is invisible.

**Acceptance Criteria**:

1. WHEN a callable invokes `PublishAsync` or `Publish` on a receiver whose type is or implements `IEventBus` THEN the classifier SHALL create an outbound `BoundaryOperation` with protocol `messaging`, direction `outbound`, and the fully qualified type name of the `TEvent` type argument as the `protocolOperationKey`. (EBC-17)
2. WHEN a type implements `IIntegrationEventHandler<TEvent>` THEN the classifier SHALL create an inbound `BoundaryOperation` with protocol `messaging`, direction `inbound`, and the fully qualified type name of `TEvent` as the `protocolOperationKey`. (EBC-18)
3. WHEN a type implements `IIntegrationEventHandler<TEvent>` and the `HandleAsync` method carries the `Callable` facet THEN the classifier SHALL create an `EntryPoint` fact for that method. (EBC-19)
4. The messaging boundary operations SHALL carry the classifier identity `csharp2md.classifier.messaging` version 1 in their confirmed `implements-operation` relation. (EBC-20)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert `OrderService.PlaceOrderAsync` produces an outbound messaging `BoundaryOperation` with `protocolOperationKey` containing `OrderPlaced`; assert `OrderPlacedEventHandler.HandleAsync` produces an inbound messaging `BoundaryOperation` and an `EntryPoint`; assert `implements-operation` relations exist for both.

---

### P1: Messaging contract classification ⭐ MVP

**User Story**: As an LLM, I want a `Contract` fact when the same event type is published and subscribed across projects so that I can see shared protocol identity.

**Why P1**: Contracts prove shared payload identity. Without them, messaging boundaries are isolated stubs.

**Acceptance Criteria**:

1. WHEN both an outbound and an inbound messaging boundary operation reference the same `TEvent` fully qualified type name, and `TEvent` is a named type (not anonymous) declared in a project that is referenced by both the publishing and subscribing projects THEN the classifier SHALL create a `Contract` fact with the fully qualified type name as the schema key. (EBC-21)
2. WHEN a `Contract` is created for a messaging boundary THEN the classifier SHALL create a `ContractBinding` for each boundary operation that references that event type, with the `PayloadRole` set to `request`. (EBC-22)
3. WHEN a `ContractBinding` is created THEN the package SHALL contain a confirmed `uses-contract` relation from the boundary operation to the contract, with evidence method `semantic`, a registered `payload-role` facet, and a non-empty `derived_from` chain. (EBC-23)
4. WHEN a messaging event type is published but no `IIntegrationEventHandler<TEvent>` exists in the same solution THEN the classifier SHALL NOT create a `Contract` for that type. (EBC-24)
5. WHEN a messaging event type is both published and handled but the type is declared in the same project as both publisher and handler THEN the classifier SHALL NOT create a `Contract` (intra-project, not cross-boundary). (EBC-25)
6. WHEN a `Contract` is created THEN the classifier SHALL create a `ContractRevision` with a structural fingerprint derived from the event type's public properties. (EBC-26)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert a `Contract` exists for `Acme.Shared.Contracts.OrderPlaced` (published by `OrderService`, handled by `OrderPlacedEventHandler`, type declared in `Acme.Shared.Contracts`); assert `ContractBinding` facts link both boundary operations to the contract; assert `uses-contract` relations exist; assert `PaymentProcessed` does NOT produce a `Contract` (no handler in this fixture).

---

### P1: Composable classification stage ⭐ MVP

**User Story**: As a later workstream author, I want the Classification and Promotion stage to accept a list of classifier passes so that 5B–5D can add their classifiers without modifying 5A's code.

**Why P1**: The roadmap allows the four classifier workstreams to proceed in parallel. A hardcoded stage would force serial coupling.

**Acceptance Criteria**:

1. The `ClassificationAndPromotionStub` at pipeline index 3 SHALL be replaced with a `ClassificationAndPromotionStage` that accepts an `ImmutableArray` of classifier passes. (EBC-27)
2. Each classifier pass SHALL receive the accumulated facts and observations from prior stages and earlier passes, and SHALL add its promoted facts, relations, candidates, and unresolved records to the shared `SnapshotAccumulator`. (EBC-28)
3. The `ClassificationAndPromotionStage` SHALL execute its passes in registration order. (EBC-29)
4. The `ClassificationAndPromotionStage` SHALL report the aggregate fact, observation, and relation counts from all classifier passes. (EBC-30)
5. IF a classifier pass encounters an observation it cannot classify THEN it SHALL skip that observation without aborting the stage. (EBC-31)

**Independent Test**: Register a test-only no-op classifier pass alongside the real 5A passes; assert the stage executes all passes and reports combined counts; assert the no-op pass does not interfere with 5A output.

---

### P1: Candidates, unresolved, and diagnostics ⭐ MVP

**User Story**: As an LLM, I want outbound HTTP destinations recorded as candidates and unresolved records so that I can distinguish confirmed from uncertain links.

**Why P1**: The taxonomy requires candidates and unknowns as first-class records. Without them, the package overstates certainty or silently drops uncertain boundaries.

**Acceptance Criteria**:

1. WHEN an outbound HTTP boundary operation is created THEN the classifier SHALL create a `CandidateLink` of kind `targets` from the boundary operation to an `ExternalSystem` fact created from the client name literal. (EBC-15, restated)
2. WHEN a `RouteDeclaration` observation references a route template but the template contains dynamic segments (e.g., `{id}`) THEN the classifier SHALL use the template as-is and SHALL NOT attempt to resolve dynamic segments. (EBC-33)
3. WHEN a messaging `PublishAsync` invocation has a type argument that cannot be resolved to a named type THEN the classifier SHALL record an `UnresolvedRecord` of kind `uses-contract` with cause `NoCandidateFound`. (EBC-34)
4. WHEN the Classification and Promotion stage completes for `Acme.Orders.slnx` THEN the stage's `StageResult` SHALL report at least one fact and at least one relation. (EBC-35)
5. WHEN Classification and Promotion completes THEN Validation and Coverage, Retrieval Projection, and Batch Composition stubs SHALL still report 0 facts, 0 observations, and 0 relations. (EBC-36)
6. WHEN `analyze` completes THEN Persistence SHALL stage the accumulated snapshot including classifier-produced facts, relations, candidates, and unresolved records. (EBC-37)
7. The `SnapshotAccumulator` SHALL support adding `CandidateLink` and `UnresolvedRecord` entries. (EBC-38)

**Independent Test**: Analyze `Acme.Orders.slnx`; read the committed package; assert `CandidateLink` records exist for outbound HTTP destinations; assert the Classification stage reports non-zero counts; assert later stubs still report zeros; assert the package's candidates and unresolved sections are non-empty.

---

### P1: Determinism and invariants ⭐ MVP

**User Story**: As an operator, I want classifier output to be deterministic and to preserve all workstream 4 invariants.

**Why P1**: Non-deterministic classification would violate ROSE-52/53/54 guarantees that packages are clone-path-independent and byte-identical on re-run.

**Acceptance Criteria**:

1. IF two clones of the same tree are analyzed THEN every classifier-produced fact identity, relation, candidate, and unresolved record SHALL be equal. (EBC-39)
2. IF the same solution is analyzed twice THEN every canonical payload file SHALL be byte-identical. (EBC-40)
3. The package SHALL NOT contain an absolute filesystem path in any classifier-produced fact, relation, candidate, or diagnostic. (EBC-41)
4. The public surface of `Csharp2Md.Analysis` SHALL NOT expose any type from `Microsoft.CodeAnalysis` through classifier interfaces. (EBC-42)
5. The `Csharp2Md.Cli` project SHALL continue to declare no project reference to `Csharp2Md.Domain`. (EBC-43)
6. WHERE the in-memory adapter is used the classifier SHALL still produce facts and relations and SHALL create no files. (EBC-44)
7. Canonical package payloads SHALL NOT contain a connection string, password, token, certificate, or authorization value from classifier-produced content. (EBC-45)

**Independent Test**: Analyze the fixture from two working directories; assert identity equality on classifier output. Commit twice; assert canonical bytes. Run with in-memory adapter; assert no filesystem writes. Assert no absolute paths in classifier-produced JSON.

---

## Edge Cases

- IF a `ControllerBase` descendant has no action methods with `Callable` THEN no entry point SHALL be created for that class (EBC-09).
- IF a `RouteDeclaration` has an empty route template THEN the boundary operation SHALL use the empty string as the protocol operation key (EBC-06).
- IF `CreateClient` is called with a non-constant argument THEN the classifier SHALL record an `UnresolvedRecord` for the outbound boundary with cause `InsufficientEvidence` and the observation as available evidence (EBC-34).
- IF a messaging `TEvent` type is an anonymous type THEN the classifier SHALL skip contract creation for that publish boundary and record an `UnresolvedRecord` (EBC-34).
- IF a project has both controller actions and `PublishAsync` calls THEN both HTTP and messaging boundary operations SHALL be created for that project's component (EBC-01, EBC-06, EBC-17).
- WHEN the fixture's `PaymentProcessed` event is published but has no handler THEN no `Contract` SHALL be created (EBC-24).
- IF two callable symbols in different documents would produce the same `EntryPoint` identity THEN the `SnapshotAccumulator` SHALL detect structural corruption (existing WS4 invariant from ROSE-21).

---

## Requirement Traceability

Each requirement gets a unique ID for tracking across design, tasks, and validation.

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| EBC-01 | P1: Project-as-component promotion | Phase 2 (T6, T8) | Implementing |
| EBC-02 | P1: Project-as-component promotion | Phase 2 (T6, T8) | Implementing |
| EBC-03 | P1: Project-as-component promotion | Phase 2 (T6) | Implementing |
| EBC-04 | P1: Project-as-component promotion | Phase 2 (T6) | Implementing |
| EBC-05 | P1: HTTP entry point classification | Phase 2 (T7, T8) | Implementing |
| EBC-06 | P1: HTTP entry point classification | Phase 3 (T9) | Implementing |
| EBC-07 | P1: HTTP entry point classification | Phase 4 (T14, T15) | In Tasks |
| EBC-08 | P1: HTTP entry point classification | Phase 2 (T7) | Implementing |
| EBC-09 | P1: HTTP entry point classification | Phase 2 (T7) | Implementing |
| EBC-10 | P1: Outbound HTTP boundary classification | Phase 3 (T10) | Implementing |
| EBC-11 | P1: Outbound HTTP boundary classification | Phase 3 (T10) | Implementing |
| EBC-12 | P1: Outbound HTTP boundary classification | Phase 3 (T10) | Implementing |
| EBC-13 | P1: Outbound HTTP boundary classification | Phase 3 (T10) | Implementing |
| EBC-14 | P1: Outbound HTTP boundary classification | Phase 4 (T14) | In Tasks |
| EBC-15 | P1: Outbound HTTP boundary classification | Phase 3 (T10) | Implementing |
| EBC-16 | P1: Outbound HTTP boundary classification | Phase 3 (T10) | Implementing |
| EBC-17 | P1: Messaging boundary classification | Phase 3 (T11) | Implementing |
| EBC-18 | P1: Messaging boundary classification | Phase 3 (T12) | Implementing |
| EBC-19 | P1: Messaging boundary classification | Phase 2 (T7) | Implementing |
| EBC-20 | P1: Messaging boundary classification | Phase 3 (T12), Phase 4 (T14) | Implementing |
| EBC-21 | P1: Messaging contract classification | Phase 4 (T13, T15) | In Tasks |
| EBC-22 | P1: Messaging contract classification | Phase 4 (T13) | In Tasks |
| EBC-23 | P1: Messaging contract classification | Phase 4 (T14, T15) | In Tasks |
| EBC-24 | P1: Messaging contract classification | Phase 4 (T13, T15) | In Tasks |
| EBC-25 | P1: Messaging contract classification | Phase 4 (T13) | In Tasks |
| EBC-26 | P1: Messaging contract classification | Phase 4 (T13) | In Tasks |
| EBC-27 | P1: Composable classification stage | Phase 1 (T2, T4, T5), Phase 5 (T20) | Implementing |
| EBC-28 | P1: Composable classification stage | Phase 1 (T3), Phase 5 (T20) | Implementing |
| EBC-29 | P1: Composable classification stage | Phase 1 (T4), Phase 5 (T20) | Implementing |
| EBC-30 | P1: Composable classification stage | Phase 1 (T4), Phase 5 (T20) | Implementing |
| EBC-31 | P1: Composable classification stage | Phase 1 (T4) | Implementing |
| EBC-32 | P1: Candidates, unresolved, and diagnostics | Phase 5 (T17) | In Tasks |
| EBC-33 | P1: Candidates, unresolved, and diagnostics | Phase 3 (T9) | Implementing |
| EBC-34 | P1: Candidates, unresolved, and diagnostics | Phase 4 (T14) | In Tasks |
| EBC-35 | P1: Candidates, unresolved, and diagnostics | Phase 2 (T8), Phase 5 (T16) | Implementing |
| EBC-36 | P1: Candidates, unresolved, and diagnostics | Phase 5 (T16) | In Tasks |
| EBC-37 | P1: Candidates, unresolved, and diagnostics | Phase 5 (T16, T19) | In Tasks |
| EBC-38 | P1: Candidates, unresolved, and diagnostics | Phase 1 (T1) | Implementing |
| EBC-39 | P1: Determinism and invariants | Phase 5 (T17) | In Tasks |
| EBC-40 | P1: Determinism and invariants | Phase 5 (T17) | In Tasks |
| EBC-41 | P1: Determinism and invariants | Phase 5 (T17) | In Tasks |
| EBC-42 | P1: Determinism and invariants | Phase 5 (T18) | In Tasks |
| EBC-43 | P1: Determinism and invariants | Phase 5 (T18) | In Tasks |
| EBC-44 | P1: Determinism and invariants | Phase 5 (T18) | In Tasks |
| EBC-45 | P1: Determinism and invariants | Phase 5 (T18) | In Tasks |

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 45 total, 45 mapped to tasks, 0 unmapped ✅

---

## Success Criteria

How we know the feature is successful:

- [ ] `analyze --solution fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx --output <dir>` writes a package with non-zero architecture and contract facts, non-zero non-`contains` confirmed relations, and non-empty candidates
- [ ] `OrdersController.GetOrderStatus` is an `EntryPoint` with an inbound HTTP `BoundaryOperation`
- [ ] `OrderService.PlaceOrderAsync` produces outbound HTTP and outbound messaging `BoundaryOperation` facts
- [ ] `OrderPlacedEventHandler.HandleAsync` is an `EntryPoint` with an inbound messaging `BoundaryOperation`
- [ ] A `Contract` for `OrderPlaced` exists with `ContractBinding` and `uses-contract` relations
- [ ] Outbound HTTP destinations are `CandidateLink` records, not confirmed `targets` relations
- [ ] Two clone paths produce identical classifier output and canonical payload bytes
- [ ] `ClassificationAndPromotionStage` is composable: a test-only pass can be registered alongside 5A passes
- [ ] After the Verifier, `LocalCorpus` tests run when `fixtures/eShop` or `fixtures/eShopOnContainers` exist, and are skipped when they do not
