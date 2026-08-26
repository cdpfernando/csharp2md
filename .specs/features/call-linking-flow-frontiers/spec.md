# Call Linking, Flow Frontiers — Specification

## Problem Statement

After `roslyn-observation-extraction` (WS4) the pipeline accumulates a complete observation ledger — `Invocation`, `TypeUsage`, `BaseType`, `ObjectCreation`, `AttributeUsage`, `MessageOperation`, and others — but the confirmed-relation graph contains only `contains` edges. A downstream LLM cannot ask "which callables does this entry point traverse?" or "why was this link kept as a candidate rather than confirmed?" because no `invokes` or `executes` relations, no candidates, and no open frontiers have been produced yet.

Workstream 5B closes that gap: it classifies `Invocation` observations (and the closely related `ObjectCreation`, `BaseType`, and `TypeUsage` observations that qualify a receiver) into `invokes` confirmed relations, `executes` relations from entry points to their first direct callables, `CandidateLink` records for polymorphic and unresolvable dispatch, and `OpenFrontier` records for calls whose continuation cannot be statically demonstrated.

## Goals

- [ ] Every `Invocation` observation whose owner and target are both inventoried `Symbol` facts with the `Callable` facet produces an `invokes` confirmed relation with `EvidenceMethod.Semantic`.
- [ ] Every `EntryPoint` fact produced by WS5A produces an `executes` relation to the direct callable it names, as its primary confirmed call edge.
- [ ] Polymorphic dispatch (interface method, abstract/virtual override) with one or more implementing candidates produces `CandidateLink` records instead of a confirmed `invokes`.
- [ ] An `Invocation` observation that cannot be resolved to a known `Symbol` fact in any solution-scoped project produces an `UnresolvedRecord` with a declared cause.
- [ ] An invocation whose target is resolvable but whose continuation through dynamic dispatch, reflection, or an external boundary cannot be statically closed produces an `OpenFrontier` on that occurrence.
- [ ] All five record types (`ConfirmedRelation`, `CandidateLink`, `UnresolvedRecord`, `OpenFrontier`, diagnostic) are written to the committed package through the existing transactional port.
- [ ] The `Invocation` observation payload problem is resolved: either the extractor is extended to carry the target symbol's canonical signature, or the classifier has a deterministic way to re-derive it without re-running Roslyn (see Assumptions & Open Questions).

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Cross-solution call linking | WS7 (`multi-solution-composition`) owns global correlations; 5B produces only solution-scoped `invokes` edges |
| MediatR / domain-event in-process dispatch | 5B scope covers direct static calls; MediatR-style send/publish that passes through a mediator is a separate pattern and may be addressed by 5D (config/DI) or left as an open frontier |
| Business-rule or flow-path interpretation | AD-005: downstream LLM responsibility; 5B produces raw edges, not annotated paths |
| Call-graph completeness guarantees | AD-009: run certification is a separate concern; unknown continuation remains an open frontier, not an error |
| Recursive cycle detection or call-graph traversal | The `invokes` relation is one direct edge per occurrence; traversal is a retrieval projection (WS6) |
| gRPC client calls (cross-service) | Covered by WS5A (`BoundaryOperation` outbound gRPC); 5B focuses on in-process `Symbol→Symbol` calls |
| Messaging dispatch through `IEventBus.PublishAsync` / `Subscribe` | Those `MessageOperation` observations are WS5A boundary concerns; 5B does not re-promote them |
| Test code call paths | Analysis already excludes test projects from classification; 5B inherits that scope |
| `executes` edges from entry points to the full reachable subgraph | `executes` is defined as the direct callable the entry point names; depth-first reachability is a projection |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here — nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| **Invocation payload gap** — `Invocation` observations carry empty payloads (ROSE design:380); the target symbol identity is not stored. 5B classifiers cannot lookup `observation.payload["target"]`. Path (A): extend the extractor to store the bound target callable's canonical signature in the `BindingDiagnostic.Message` as a structured suffix `bound::<canonical-signature>` without touching the payload or `ObservationIdentity`. Path (B): classifier re-runs Roslyn (violates AD-006). | **Path A chosen.** The extractor already holds the bound `IMethodSymbol` at walk time; storing its canonical signature (computed by `SymbolFactEmitter.TrySignature`) as a message suffix is a purely additive annotation. WS4 identity invariants are preserved because `Message` is not part of `ObservationIdentity`. | y |
| WS4 identity invariants: adding a key to a formerly-empty payload changes `ObservationIdentity`; WS4 ordinal tests would break. | Store target signature in `BindingDiagnostic.Message` suffix only — message is not part of identity. WS4 tests are unaffected. | Separation of identity from annotation is the existing pattern for diagnostic codes. | y |
| `executes` relation between `EntryPoint` and its direct callable: an `EntryPoint` fact (from WS5A) already carries `Symbol` (the callable it points to). The `executes` relation (`EntryPoint → Symbol`) can be emitted directly from the `EntryPoint` fact without any additional observation — purely structural. | Emit `executes` directly from `EntryPoint.Symbol` during 5B's `InvokesPass`. No new observation needed. | `EntryPoint.Symbol` is the canonical reference; `EvidenceMethod.Semantic` is justified because `EntryPoint.Create` itself required semantic evidence (WS5A). | n |
| Polymorphism: the registry defines `invokes` as `Symbol → Symbol` with `EvidenceMethod.Semantic`. If a method call binds to an interface or abstract member, the declared target is the interface/abstract symbol, but the runtime target is unknown. | For a call that binds to an interface or abstract symbol where `Symbol` facts exist for 1+ implementing overrides in the same solution, produce `CandidateLink` records for each candidate override rather than a confirmed `invokes` to the abstract member. If no concrete override is in scope, produce `UnresolvedRecord` with cause `NoCandidateFound`. | AD-010: candidates never enter the confirmed graph; a numeric confidence score is rejected; `CandidateLink` is the correct representation. | n |
| Lambda and local-function invocations: `Invocation` observations whose target is a lambda or local function may not have a corresponding `Symbol` fact (WS4 may not inventory lambdas as separate `Symbol` facts). | If the target signature cannot be matched to a `Symbol` fact, produce `UnresolvedRecord` with cause `NoCandidateFound` and leave the occurrence with an `OpenFrontier` if further continuation is observed. Lambdas that are inventoried as `Symbol` facts follow the normal confirmed path. | WS4 inventories named symbols; anonymous lambdas are not named; the boundary is the `Symbol` fact, not Roslyn's ISymbol universe. | n |
| Delegate invocations (`func.Invoke(...)`, `action()`): the target is determined at runtime. | Always `OpenFrontier` + `UnresolvedRecord(NoCandidateFound)`. No confirmed `invokes` edge. | AD-010: cannot confirm causal target when alternatives remain. | n |
| Self-calls (a callable calling itself recursively): the source and target `Symbol` facts are the same. | Produce a confirmed `invokes` relation with source = target; the registry allows `Symbol → Symbol` with no restriction against self-reference. | No architectural reason to exclude self-calls; a confirmed self-invokes edge is valid evidence of recursion. | n |
| Calls where the owner `Symbol` fact cannot be determined (fallback owner used in WS4): the owner reference is the fallback `Symbol` for the project. | If the owner resolves to a fallback symbol, produce `UnresolvedRecord(InsufficientEvidence)` rather than a confirmed `invokes`, because the source identity is imprecise. | Fallback owner means the extractor could not pin the containing callable; a confirmed causal edge from an imprecise source would be misleading. | n |
| `ObjectCreation` observations: a constructor call is a callable invocation. | `ObjectCreation` whose target matches a `Symbol` fact with the `Callable` facet (constructor) produces a confirmed `invokes`. If the constructed type is abstract or an interface (not typical for constructor calls), treat as unresolved. | Constructors are `Callable` per CONTEXT.md definition. | n |
| `SnapshotAccumulator` missing `AddCandidate`, `AddUnresolved`, and `AddOpenFrontier`: WS5A design already notes that `CandidateLink.Empty` and `UnresolvedRecord.Empty` are placeholders in `ToSnapshot()`. | 5B extends `SnapshotAccumulator` with `AddCandidate(CandidateLink)`, `AddUnresolved(UnresolvedRecord)`, and `AddOpenFrontier(OpenFrontier)`. These may be added by WS5A first; 5B must not duplicate them. | The extension is already designed in WS5A's design.md; 5B reuses whatever WS5A adds rather than adding again. | n |
| Observation on `IClassifierPass` interface: WS5A introduces `IClassifierPass`, `ClassifierContext`, and `ClassificationAndPromotionStage`. 5B reuses that interface unchanged. | 5B adds one or more `IClassifierPass` implementations into the same `Classification/Passes/` folder. `ClassificationAndPromotionStage.CreateDefault()` (or equivalent wiring) is extended to include 5B's passes after WS5A's passes. | WS5A design.md declares this a de facto multi-workstream standard. | n |

**Open questions:** none — all resolved or logged above.

---

## User Stories

### P1: Confirmed in-process call edges ⭐ MVP

**User Story**: As a downstream LLM consumer of the analysis package, I want to see `invokes` confirmed relations between callable symbols so I can trace which callables a given entry point reaches.

**Why P1**: Without confirmed call edges, the package has structural facts (symbols, documents) and architectural facts (entry points, boundary operations from WS5A) but no flow graph. The primary value of WS5B is those edges.

**Acceptance Criteria**:

1. WHEN an `Invocation` observation's owner `FactReference` matches a `Symbol` fact with the `Callable` facet AND the observation carries a resolved target signature matching a `Symbol` fact with the `Callable` facet in the same solution THEN Classification and Promotion SHALL emit a `ConfirmedRelation` of kind `Invokes` with `EvidenceMethod.Semantic`, with the owning symbol as source and the target symbol as target. (CLLF-01)
2. WHEN an `ObjectCreation` observation's owner matches a callable `Symbol` fact AND the constructed type's constructor resolves to a `Symbol` fact with the `Callable` facet THEN Classification and Promotion SHALL emit a `ConfirmedRelation` of kind `Invokes` with `EvidenceMethod.Semantic`. (CLLF-02)
3. WHEN an `EntryPoint` fact exists from WS5A with a non-default `Symbol` reference THEN Classification and Promotion SHALL emit a `ConfirmedRelation` of kind `Executes` from the `EntryPoint` to the referenced `Symbol`, with `EvidenceMethod.Semantic`. (CLLF-03)
4. The system SHALL NOT emit a confirmed `Invokes` relation when the source or target `FactReference` cannot be matched to a known `Symbol` fact in the accumulated snapshot. (CLLF-04)
5. The system SHALL NOT emit duplicate `ConfirmedRelation` records for the same `(Kind, Source, Target, Facets)` tuple within a single analysis run. (CLLF-05)
6. WHEN the invocation target signature is stored as a structured annotation on the observation (per the chosen payload-gap resolution), THEN Classification and Promotion SHALL use that annotation and SHALL NOT re-invoke Roslyn APIs. (CLLF-06)

**Independent Test**: Load the `Acme.Orders.slnx` fixture. After full pipeline execution, assert that `OrderService.PlaceOrderAsync` → `IEventBus.PublishAsync` produces a confirmed `Invokes` relation, and that `OrdersController.GetOrderStatus` produces an `Executes` relation from its `EntryPoint` to the controller action callable.

---

### P1: Polymorphic call candidates ⭐ MVP

**User Story**: As a downstream LLM consumer, I want `CandidateLink` records for calls through interface or abstract members so I can see what concrete implementations exist without the system fabricating a confirmed edge it cannot prove.

**Why P1**: Polymorphism is ubiquitous in C# services. Without candidates, every interface call would be silently dropped, hiding major portions of the call graph.

**Acceptance Criteria**:

1. WHEN an invocation observation resolves to an interface or abstract method symbol AND one or more concrete implementing `Symbol` facts exist in the same solution THEN Classification and Promotion SHALL emit a `CandidateLink` of kind `Invokes` for each concrete implementing symbol, not a confirmed `ConfirmedRelation`. (CLLF-07)
2. WHEN an invocation observation resolves to an interface or abstract method AND no concrete implementing `Symbol` fact exists in the accumulated solution scope THEN Classification and Promotion SHALL emit an `UnresolvedRecord` with cause `NoCandidateFound` referencing the observation identity. (CLLF-08)
3. The system SHALL NOT emit a confirmed `Invokes` relation whose target is an interface member or abstract method. (CLLF-09)
4. WHEN a concrete override exists in the solution AND the calling callable's declared receiver type is the concrete type (not the interface), THEN Classification and Promotion SHALL emit a confirmed `Invokes` to the concrete `Symbol` directly, not a candidate. (CLLF-10)

**Independent Test**: In `ReceiverShapes.cs`, `ViaField()` and `ViaProperty()` call `PaymentClient.Authorize` — a concrete method, not an interface. Assert confirmed `Invokes`. Add a new fixture method calling a declared interface method (`IEventBus.PublishAsync`) and assert that `CandidateLink` records (not a confirmed relation) are produced for each observable concrete implementation in scope.

---

### P1: Open frontiers for unresolvable continuations ⭐ MVP

**User Story**: As a downstream LLM consumer, I want `OpenFrontier` records at call sites where static analysis cannot determine the continuation so I know the call graph is incomplete at those points rather than silently missing data.

**Why P1**: AD-010 and CONTEXT.md both define Open Frontier as a first-class output. Without it, the graph appears complete when it is not.

**Acceptance Criteria**:

1. WHEN an `Invocation` observation targets a delegate, local function with no matching `Symbol` fact, or a reflection-based dispatch (identified by target type `System.Reflection.*`) THEN Classification and Promotion SHALL emit an `OpenFrontier` on that observation identity with cause `FurtherContinuationObserved`. (CLLF-11)
2. WHEN an invocation is through a generic type parameter call (e.g., `where T : IHandler`) and no concrete `Symbol` can be confirmed THEN Classification and Promotion SHALL emit a `CandidateLink` if candidates exist, else an `UnresolvedRecord` with cause `NoCandidateFound`, and an `OpenFrontier` with cause `FurtherContinuationObserved`. (CLLF-12)
3. WHEN a call site produces an `UnresolvedRecord` THEN Classification and Promotion SHALL also emit an `OpenFrontier` on that same observation identity, unless the unresolved cause is `InsufficientEvidence` (imprecise owner — the frontier cannot be anchored to a specific occurrence). (CLLF-13)
4. IF an `Invocation` observation's owner is the project-level fallback symbol THEN Classification and Promotion SHALL emit an `UnresolvedRecord` with cause `InsufficientEvidence` and SHALL NOT emit an `OpenFrontier` for that occurrence. (CLLF-14)

**Independent Test**: In `OrderService.cs`, the `httpClientFactory.CreateClient(...)` call returns an `HttpClient` whose subsequent `PostAsJsonAsync` chain is an outbound boundary (5A scope). Verify that a fixture method calling a delegate or `Func<T>` produces an `OpenFrontier`. (The SyntheticSolution may need a small delta fixture method for this scenario; see design phase.)

---

### P2: Multi-receiver-shape resolution

**User Story**: As a downstream LLM consumer, I want confirmed `invokes` edges regardless of how the receiver is declared (field, property, parameter, pattern variable), so call graph edges are not missing due to syntactic differences.

**Why P2**: `ReceiverShapes.cs` exercises all four shapes. If only one shape resolves, the call graph is systematically incomplete for certain coding patterns.

**Acceptance Criteria**:

1. WHEN an `Invocation` observation's owner symbol is established via a field-stored receiver, property receiver, constructor-parameter receiver, or pattern-match variable receiver THEN Classification and Promotion SHALL produce the same confirmed `Invokes` relation as if the receiver were a simple local variable. (CLLF-15)
2. The system SHALL NOT produce duplicate `invokes` edges for the same `(source, target)` pair across different receiver shapes within the same callable. (CLLF-16)

**Independent Test**: Load `Acme.Orders.slnx`. Assert that `ReceiverShapes.ViaField`, `ReceiverShapes.ViaProperty`, `ReceiverShapes.ViaPatternVariable`, and the constructor-parameter call in `ReceiverShapes..ctor` each produce exactly one confirmed `Invokes` to `PaymentClient.Authorize`.

---

### P2: Cross-project invocation resolution

**User Story**: As a downstream LLM consumer, I want confirmed `invokes` edges when the calling symbol and target symbol are in different projects within the same solution, so flows crossing project boundaries are visible.

**Why P2**: The fixture's `OrderService.AuthorizeViaPaymentClientAsync` calls `PaymentClient.Authorize` which is declared in `Acme.Shared.Contracts` — a different project. Without cross-project resolution, service-to-library call edges are missing.

**Acceptance Criteria**:

1. WHEN an `Invocation` observation's resolved target signature matches a `Symbol` fact in a different project than the owner symbol's project THEN Classification and Promotion SHALL emit a confirmed `Invokes` relation, provided both projects are in the same solution scope. (CLLF-17)
2. WHEN the target symbol's project is not in the accumulated solution scope (external package or assembly) THEN Classification and Promotion SHALL emit an `UnresolvedRecord` with cause `NoCandidateFound`. (CLLF-18)

**Independent Test**: Load `Acme.Orders.slnx`. Assert that `OrderService.AuthorizeViaPaymentClientAsync` → `PaymentClient.Authorize` (declared in `Acme.Shared.Contracts`) produces a confirmed `Invokes` across the project boundary.

---

### P3: Diagnostic coverage for skipped observations

**User Story**: As a package consumer validating run coverage, I want a diagnostic record for every `Invocation` or `ObjectCreation` observation that was not promoted to any confirmed relation, candidate, or unresolved record, so I can see what was intentionally skipped.

**Why P3**: Nice-to-have for run certification (AD-009 coverage awareness); not needed for downstream LLM use cases.

**Acceptance Criteria**:

1. WHEN an `Invocation` or `ObjectCreation` observation is skipped by all passes (e.g., the owner is a fallback symbol and cause `InsufficientEvidence` was already recorded) THEN Classification and Promotion SHALL NOT emit a diagnostic redundantly — the `UnresolvedRecord` is sufficient. (CLLF-19)
2. WHEN an `Invocation` observation binds to a framework method outside the solution scope (e.g., `string.Format`, `Task.FromResult`) THEN Classification and Promotion SHALL silently skip it (no `UnresolvedRecord`, no diagnostic) because external-symbol non-promotion is expected behavior. (CLLF-20)

**Independent Test**: Run against the `Acme.Orders.slnx` fixture. Verify that `string.Format` and `Task.FromResult` calls produce no `UnresolvedRecord` and no diagnostic. Verify that calls to unknown method references (if any) produce `UnresolvedRecord`, not a diagnostic.

---

## Edge Cases

- IF the `Invocation` observation's target annotation resolves to a signature that matches multiple `Symbol` facts (ID collision from WS4's accumulator) THEN Classification and Promotion SHALL emit `UnresolvedRecord(InsufficientEvidence)` — a collision in WS4 would have set `StructuralCorruption`; if the accumulator is non-corrupt the match is unique by WS4's identity guarantee.
- IF an `EntryPoint` fact's `Symbol` reference does not match any `Symbol` fact in the snapshot (stale reference from WS5A) THEN Classification and Promotion SHALL emit a diagnostic naming the missing reference and SHALL NOT emit an `Executes` relation.
- WHEN the analysis variant list is empty or default THEN `ConfirmedRelation.Create` will throw; the pass SHALL validate `AnalysisVariants` from `ClassifierContext` before attempting relation construction.
- IF `SnapshotAccumulator.AddCandidate` or `AddUnresolved` or `AddOpenFrontier` do not yet exist (WS5A not yet merged) THEN 5B's implementation MUST add them, consistent with WS5A's design, without breaking WS5A's expectations.
- WHEN a solution has zero entry points (WS5A produced none) THEN no `Executes` relations are produced; zero confirmed relations is a valid outcome for a library project.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| CLLF-01 | P1: Confirmed call edges | Design | Implementing |
| CLLF-02 | P1: Confirmed call edges | Design | Implementing |
| CLLF-03 | P1: Confirmed call edges | Design | Implementing |
| CLLF-04 | P1: Confirmed call edges | Design | Implementing |
| CLLF-05 | P1: Confirmed call edges | Design | Implementing |
| CLLF-06 | P1: Confirmed call edges | Design | Implementing |
| CLLF-07 | P1: Polymorphic candidates | Design | Implementing |
| CLLF-08 | P1: Polymorphic candidates | Design | Implementing |
| CLLF-09 | P1: Polymorphic candidates | Design | Implementing |
| CLLF-10 | P1: Polymorphic candidates | Design | Implementing |
| CLLF-11 | P1: Open frontiers | Design | Implementing |
| CLLF-12 | P1: Open frontiers | Design | Implementing |
| CLLF-13 | P1: Open frontiers | Design | Implementing |
| CLLF-14 | P1: Open frontiers | Design | Implementing |
| CLLF-15 | P2: Multi-receiver shapes | - | Implementing |
| CLLF-16 | P2: Multi-receiver shapes | - | Implementing |
| CLLF-17 | P2: Cross-project resolution | - | Implementing |
| CLLF-18 | P2: Cross-project resolution | - | Implementing |
| CLLF-19 | P3: Diagnostic coverage | - | Implementing |
| CLLF-20 | P3: Diagnostic coverage | - | Implementing |

**ID format:** `CLLF-NN`
**Status values:** Pending → In Design → In Tasks → Implementing → Verified
**Coverage:** 20 total. CLLF-19 Implementing (Verifier fix iteration 1: empty-diagnostics asserts). Status not Verified until Verifier re-runs.

---

## Success Criteria

- [ ] Every inventoried same-solution callable-to-callable invocation produces exactly one confirmed `invokes` or exactly one `CandidateLink` set or exactly one `UnresolvedRecord` — never both a confirmed edge and a candidate for the same occurrence.
- [ ] Every `EntryPoint` fact produces exactly one `executes` relation to its named callable.
- [ ] Every call to a delegate or fully-unresolvable target produces an `OpenFrontier`; no call is silently dropped.
- [ ] `ReceiverShapes` fixture methods each produce confirmed `invokes` to `PaymentClient.Authorize`.
- [ ] `OrderService.AuthorizeViaPaymentClientAsync` → `PaymentClient.Authorize` (cross-project) produces a confirmed `invokes`.
- [ ] Framework/BCL calls (e.g., `Task.FromResult`, `string.IsNullOrEmpty`) produce no `UnresolvedRecord`.
- [ ] `dotnet test` passes on all five test projects (excluding `Category=LocalCorpus`) after the feature is complete.
