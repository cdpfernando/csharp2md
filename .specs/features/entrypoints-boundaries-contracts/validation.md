# Entry Points, Boundaries, and Contracts Validation

**Date**: 2026-08-26
**Spec**: `.specs/features/entrypoints-boundaries-contracts/spec.md`
**Diff range**: `d5f64ee^..b9846cf`
**Verifier**: independent sub-agent (author ≠ verifier)
**Result**: PASS

T1–T20 each have a Conventional Commit on `feat/entrypoints-boundaries-contracts` (`d5f64ee` … `b9846cf`). `tasks.md` Done-when boxes are all checked. Header still says “Draft”; the checkboxes and commits are the completion evidence.

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1 | Done | `d5f64ee` SnapshotAccumulator candidates/unresolved |
| T2 | Done | `57bd572` IClassifierPass |
| T3 | Done | `f5d8819` ClassifierContext |
| T4 | Done | `5c3f355` ClassificationAndPromotionStage |
| T5 | Done | `ade3fd0` replace stub at index 3 |
| T6 | Done | `1defb8c` ComponentPass |
| T7 | Done | `2a587be` EntryPointPass |
| T8 | Done | `03480b7` register + fixture integration |
| T9 | Done | `d54af77` inbound HTTP |
| T10 | Done | `07a87c7` outbound HTTP |
| T11 | Done | `7e09598` outbound messaging |
| T12 | Done | `e9e910e` inbound messaging + register |
| T13 | Done | `e01e331` ContractPass |
| T14 | Done | `72a4a3c` RelationPass |
| T15 | Done | `0303419` register contract/relation |
| T16 | Done | `d065691` full pipeline |
| T17 | Done | `64dbf6a` determinism |
| T18 | Done | `ea068bd` isolation |
| T19 | Done | `cc72d5c` storage round-trip |
| T20 | Done | `b9846cf` composability |

No blocked or partial tasks.

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| WHEN a project contains a promoted entry point or boundary THEN a `Component` named for the project's logical relative path (EBC-01) | Name `Acme.Orders/Acme.Orders.csproj`; fixture `Acme.Orders` has a Component | `tests/Csharp2Md.Analysis.Tests/Classification/ComponentPassTests.cs:37` `Assert.Equal("Acme.Orders/Acme.Orders.csproj", component.Name)`; `ComponentEntryPointIntegrationTests.cs:47-50` `Assert.Contains(... Name.Contains("Acme.Orders.csproj"))` | PASS |
| WHEN a project has no entry points and no boundary operations THEN no Component (EBC-02) | `Acme.Shared.Contracts` and `Acme.Broken` absent | `ComponentPassTests.cs:94-95` `Assert.Empty(...OfType<Component>())`; `ComponentEntryPointIntegrationTests.cs:51-56` `Assert.DoesNotContain` Shared.Contracts and Broken | PASS |
| Component `Owners` SHALL contain every promoted symbol in that project (EBC-03) | Owners equal the candidate symbol references | `ComponentPassTests.cs:39` `Assert.Equal(action.Reference, Assert.Single(component.Owners))`; `:67-71` ordered owners match attributed, publisher, controller | PASS |
| `Component.Create` SHALL use the SolutionId from the existing Solution fact (EBC-04) | `component.Solution == solution.Id` | `ComponentPassTests.cs:38` `Assert.Equal(solution.Id, component.Solution)` | PASS |
| WHEN Callable on a `ControllerBase` descendant THEN EntryPoint owned by the project component (EBC-05) | `GetOrderStatus` is an EntryPoint; owning component is the project component | `EntryPointPassTests.cs:30-35` `Assert.Equal(action.Reference, entry.Symbol)` and owning component reference; `ComponentEntryPointIntegrationTests.cs:58-61` `GetOrderStatus` + `OrdersController` | PASS |
| WHEN RouteDeclaration exists on a ControllerBase callable THEN inbound HTTP BoundaryOperation with the route template as `protocolOperationKey` (EBC-06) | protocol `http`, direction `inbound`, key `orders/{id}` | `BoundaryPassTests.cs:38-44` `Assert.Equal(BoundaryDirection.Inbound/Http)`, `Assert.Equal("orders/{id}", operation.ProtocolOperationKey.Value.Value)`; `BoundaryIntegrationTests.cs:51-56` `Assert.Equal("orders/{id}", getOrderStatus.ProtocolOperationKey?.Value)` | PASS |
| WHEN a callable is an entry point THEN confirmed `implements-operation` with evidence method `semantic`, classifier `csharp2md.classifier.http-inbound` v1, non-empty `derived_from` citing RouteDeclaration and AttributeUsage (EBC-07) | Kind ImplementsOperation; classifier id/version 1; derived_from contains both observation kinds. EvidenceMethod is a Create-time guard (not a persisted property; taxonomy minimum is Semantic, same ROSE-17 pattern) | `RelationPassTests.cs:36-42` `Assert.Equal(RelationKind.ImplementsOperation)`, `Assert.Equal(BoundaryPass.HttpInboundIdentity, relation.Classifier)`, `Assert.Contains(... RouteDeclaration)` and `AttributeUsage`; `ContractRelationIntegrationTests.cs:107-114` classifier id/version, `DerivedFrom.Length > 0` | PASS |
| WHEN a ControllerBase callable has no RouteDeclaration THEN still EntryPoint, diagnostic naming the symbol, no boundary (EBC-08) | EntryPoint present; diagnostic `missing-route-declaration`; no BoundaryOperation | `EntryPointPassTests.cs:61-68` `Assert.Equal(action.Reference, entry.Symbol)`, `Assert.Empty(...BoundaryOperation)`, `Assert.Equal("missing-route-declaration", diagnostic.Code)`, message contains `GetOrderStatus` and symbol id | PASS |
| WHEN Callable but not ControllerBase and not a minimal-API delegate THEN no EntryPoint (EBC-09) | `PlaceOrderAsync` is not an entry point; empty controller has none | `EntryPointPassTests.cs:84-85` `Assert.Empty(...EntryPoint)`; `:108-109` empty controller; `ComponentEntryPointIntegrationTests.cs:66-68` `Assert.DoesNotContain(... PlaceOrderAsync)` | PASS |
| WHEN CreateClient(name) then HTTP method THEN outbound HTTP BoundaryOperation with destinationScope, method, route (EBC-10) | `PaymentService`, `POST`, `payments/authorize` | `BoundaryPassTests.cs:158-164` those three fields; `BoundaryIntegrationTests.cs:59-66` same tuple on PlaceOrderAsync | PASS |
| WHEN client name is a constant string THEN destinationScope is that string (EBC-11) | `PaymentService` / `NotificationService` / `ShippingService` | `BoundaryPassTests.cs:160` `Assert.Equal("PaymentService", operation.DestinationScope)`; `:194-195` NotificationService; `:215` ShippingService | PASS |
| WHEN route argument is a constant string THEN Route literal role `Route` (EBC-12) | Role Route, value `payments/authorize` | `BoundaryPassTests.cs:163-164` `Assert.Equal(LiteralRole.Route, operation.Route.Value.Role)`, `Assert.Equal("payments/authorize", operation.Route.Value.Value)` | PASS |
| WHEN multiple outbound HTTP calls to different clients THEN one BoundaryOperation per `(clientName, httpMethod, route)` (EBC-13) | Two operations; PaymentService+payments/authorize and NotificationService+notifications/order-placed | `BoundaryPassTests.cs:239-244` `Assert.Equal(2, operations.Length)` plus `Assert.Contains` each tuple; `Assert.Empty` confirmed relations | PASS |
| Outbound BoundaryOperation SHALL carry classifier `csharp2md.classifier.http-outbound` v1 on implements-operation (EBC-14) | Classifier identity equals HttpOutboundIdentity | `BoundaryPassTests.cs:277-278` id/version; `RelationPassTests.cs:61-64` `Assert.Equal(BoundaryPass.HttpOutboundIdentity, relation.Classifier)` | PASS |
| WHEN CreateClient then HTTP method THEN CandidateLink kind `targets` to candidate ExternalSystem (EBC-15) | Kind Targets; source is the outbound operation; no confirmed `targets` | `BoundaryPassTests.cs:169-173` `Assert.Equal(RelationKind.Targets, link.Kind)`, source/target, `Assert.Empty` confirmed; `BoundaryIntegrationTests.cs:95-99` three `targets` candidates, no `relations/confirmed/targets.json` | PASS |
| Candidate ExternalSystem SHALL carry ClientName literal role (EBC-16) | Role ClientName, value is the client name | `BoundaryPassTests.cs:167-168` `Assert.Equal(LiteralRole.ClientName, external.Name.Role)`; `BoundaryIntegrationTests.cs:91-93` Payment/Notification/Shipping with `Name.Role == "ClientName"` | PASS |
| WHEN PublishAsync/Publish on IEventBus THEN outbound messaging BoundaryOperation keyed by TEvent FQN (EBC-17) | protocol messaging, outbound, key contains OrderPlaced FQN | `BoundaryPassTests.cs:296-301` `Assert.Equal("global::Acme.Shared.Contracts.OrderPlaced", ...)`; `:306-320` Publish; `BoundaryIntegrationTests.cs:67-73` PlaceOrderAsync outbound messaging contains `OrderPlaced` | PASS |
| WHEN type implements IIntegrationEventHandler\<TEvent\> THEN inbound messaging BoundaryOperation keyed by TEvent FQN (EBC-18) | inbound, messaging, key OrderPlaced FQN | `BoundaryPassTests.cs:408-411` direction/protocol/key; `BoundaryIntegrationTests.cs:82-89` HandleAsync + OrderPlacedEventHandler inbound messaging | PASS |
| WHEN HandleAsync on that handler carries Callable THEN EntryPoint (EBC-19) | HandleAsync is an EntryPoint | `EntryPointPassTests.cs:125-127` `Assert.Equal(method.Reference, entry.Symbol)`; `ComponentEntryPointIntegrationTests.cs:62-65` HandleAsync + OrderPlacedEventHandler | PASS |
| Messaging boundary operations SHALL carry classifier `csharp2md.classifier.messaging` v1 on implements-operation (EBC-20) | Classifier MessagingIdentity | `BoundaryPassTests.cs:369-370` id/version; `RelationPassTests.cs:83-85` `Assert.Equal(BoundaryPass.MessagingIdentity, relation.Classifier)`; `ContractRelationIntegrationTests.cs:115-122` HandleAsync implements-operation classifier messaging v1 | PASS |
| WHEN outbound and inbound messaging share a named TEvent declared in a project used by both sides THEN Contract with FQN schema key (EBC-21) | Contract proof contains `Acme.Shared.Contracts.OrderPlaced` | `ContractPassTests.cs:33-35` `Assert.Equal(OrderPlacedFqn, contract.Proof.Value)`; `ContractRelationIntegrationTests.cs:54-56` `Assert.Single` OrderPlaced | PASS |
| WHEN a messaging Contract is created THEN ContractBinding per boundary with PayloadRole `request` (EBC-22) | Two bindings, both `request` | `ContractPassTests.cs:36-41` `Assert.Equal(2, bindings.Length)`, `PayloadRole == "request"`; `ContractRelationIntegrationTests.cs:72-81` publish and handler bindings | PASS |
| WHEN ContractBinding is created THEN confirmed `uses-contract` with evidence method semantic, payload-role facet, non-empty derived_from (EBC-23) | Kind UsesContract; facet payload-role=request; classifier contract-messaging v1 | `RelationPassTests.cs:109-116` kind, source/target, `Assert.Contains(... AxisName == "payload-role" && WireValue == "request")`, `Assert.NotEmpty` derived_from; `ContractRelationIntegrationTests.cs:87-95` those field values | PASS |
| WHEN event is published but no handler in the solution THEN no Contract (EBC-24) | PaymentProcessed absent | `ContractPassTests.cs:67-70` `Assert.Empty` Contract/Binding/Revision; `ContractRelationIntegrationTests.cs:57-59` `Assert.DoesNotContain(... PaymentProcessed)` | PASS |
| WHEN publisher and handler and type are in the same project THEN no Contract (EBC-25) | No Contract for intra-project LocalEvent | `ContractPassTests.cs:88` `Assert.Empty(...OfType<Contract>())` | PASS |
| WHEN a Contract is created THEN ContractRevision with fingerprint from public properties (EBC-26) | Fingerprint contains `Amount:decimal` and `OrderId:global::System.Guid` | `ContractPassTests.cs:42-45` `Assert.Contains("Amount:decimal")`, `Assert.Contains("OrderId:global::System.Guid")` | PASS |
| ClassificationAndPromotionStub at index 3 SHALL be replaced with ClassificationAndPromotionStage accepting ImmutableArray of passes (EBC-27) | Index 3 is ClassificationAndPromotionStage, not stub; default includes the five 5A passes | `PipelineStagesTests.cs:56-65` `Assert.IsType<ClassificationAndPromotionStage>`, `Assert.IsNotType<ClassificationAndPromotionStub>`, source contains each `new *Pass()`; `ClassificationAndPromotionStageTests.cs:21` Name; `ComposabilityTests.cs:47-50` six-pass order including test pass | PASS |
| Each pass SHALL receive prior facts/observations and add to SnapshotAccumulator (EBC-28) | Later pass sees facts from earlier pass; counting pass sees 5A components/entry points | `ClassificationAndPromotionStageTests.cs:75-76` `Assert.Equal(1, Assert.Single(seen))`; `ClassifierContextTests.cs:26-28` accumulator/variants/solution; `ComposabilityTests.cs:52-53` `counting.ComponentCount > 0` and `EntryPointCount > 0` | PASS |
| Stage SHALL execute passes in registration order (EBC-29) | `["first","second","third"]`; composed 5A order then Counting | `ClassificationAndPromotionStageTests.cs:56` `Assert.Equal(["first", "second", "third"], order)`; `ComposabilityTests.cs:47-49` Components → Entry points → Boundaries → Contracts → Relations → Counting | PASS |
| Stage SHALL report aggregate fact/observation/relation counts (EBC-30) | 2+3 facts = 5, 1+4 relations = 5; fixture classification counts > 0 | `ClassificationAndPromotionStageTests.cs:93-95` `Assert.Equal(5, result.FactCount/RelationCount)`; `FullClassifierPipelineTests.cs:21-22` fact and relation counts > 0 | PASS |
| IF a pass cannot classify an observation THEN skip without aborting the stage (EBC-31) | Later passes still run; no structural corruption | `ClassificationAndPromotionStageTests.cs:144-147` order includes after-skip, fact count 1, `Assert.False(StructuralCorruption)`; `ComponentPassTests.cs:140-142` orphan owner skipped, `Assert.False(StructuralCorruption)` | PASS |
| Re-running the classifier on the same observation set produces identical facts, relations, candidates, unresolved (EBC-32) | Identity arrays equal; candidates non-empty on retry | `ClassifierDeterminismTests.cs:49-53` `Assert.NotEmpty` facts/relations/candidates, `AssertEqualIdentities` | PASS |
| Route templates with `{id}` SHALL be used as-is (EBC-33) | Key still `orders/{id}`; contains `{id}`; does not contain `resolved` | `BoundaryPassTests.cs:68-70` those three asserts; `BoundaryIntegrationTests.cs:56-57` `Assert.Contains("{id}", ...)` | PASS |
| Unresolvable messaging type → UnresolvedRecord kind `uses-contract` cause `NoCandidateFound` (EBC-34) | Kind UsesContract, cause NoCandidateFound. Non-constant CreateClient: kind Targets, cause InsufficientEvidence | `RelationPassTests.cs:137-141` anonymous PublishAsync; `:157-161` non-constant CreateClient; `BoundaryPassTests.cs:265-270` BoundaryPass also records Targets/InsufficientEvidence | PASS |
| WHEN Classification completes for Acme.Orders.slnx THEN StageResult reports at least one fact and at least one relation (EBC-35) | FactCount > 0 and RelationCount > 0 | `FullClassifierPipelineTests.cs:21-22`; `DefaultPipelineZerosTests.cs:65-67` `Assert.True(classification.FactCount > 0)` and `RelationCount > 0` | PASS |
| WHEN Classification completes THEN Validation, Retrieval Projection, Batch Composition stubs still report 0/0/0 (EBC-36) | Those three stages zero production | `FullClassifierPipelineTests.cs:24-26` `AssertZeroProduction` stages 4, 6, 7; `DefaultPipelineZerosTests.cs:59-61` same | PASS |
| WHEN analyze completes THEN Persistence SHALL stage classifier facts, relations, candidates, and unresolved records (EBC-37) | Architecture/contract shards non-empty; implements-operation and uses-contract present; candidates non-empty; unresolved section present (count may be 0 when none produced). Round-trip reconstitutes implements-operation with materialized callable Symbol and uses-contract with payload-role | `FullClassifierPipelineTests.cs:41-112` components/entry points/boundaries/contracts/implements/uses-contract/candidates; `:118-130` unresolved manifest entry; `ClassifierFactMappingTests.cs:49-51` fact equality; `:70-73` CandidateLink; `:89-91` UnresolvedRecord; `:114-118` implements-operation + callable; `:140-146` uses-contract payload-role | PASS |
| SnapshotAccumulator SHALL support AddCandidate and AddUnresolved (EBC-38) | ToSnapshot populates those collections | `SnapshotAccumulatorTests.cs:147-148` `Assert.Equal(candidate, Assert.Single(snapshot.Candidates))`; `:160-161` unresolved; `:179-183` both, kind/cause values | PASS |
| IF two clones of the same tree are analyzed THEN classifier fact, relation, candidate, and unresolved identities SHALL be equal (EBC-39) | Equal identity arrays; clone path absent from payloads | `ClassifierDeterminismTests.cs:99-104` `AssertEqualIdentities`, `AssertNoAbsolutePath`. Retry path also asserts `Assert.NotEmpty` candidates (`:52`); clone path asserts equality only (static gap, not an AC mismatch) | PASS |
| IF the same solution is analyzed twice THEN every canonical payload file SHALL be byte-identical (EBC-40) | Payload spans sequence-equal per key | `ClassifierDeterminismTests.cs:61-64` `SequenceEqual` per canonical key | PASS |
| Package SHALL NOT contain an absolute filesystem path in classifier-produced content (EBC-41) | Clone root / slash / JSON-escaped forms absent | `ClassifierDeterminismTests.cs:67-69` and `:103-104` `AssertNoAbsolutePath` | PASS |
| Public surface of Csharp2Md.Analysis SHALL NOT expose Microsoft.CodeAnalysis through classifier interfaces (EBC-42) | Classifier types not exported; no CodeAnalysis member types | `ClassifierIsolationTests.cs:52-60` `Assert.DoesNotContain` IClassifierPass and pass types; `:73-75` `offending is null` | PASS |
| Csharp2Md.Cli SHALL continue to declare no project reference to Csharp2Md.Domain (EBC-43) | Domain absent from CLI ProjectReferences | `ClassifierIsolationTests.cs:98` `Assert.DoesNotContain("Csharp2Md.Domain", references)` | PASS |
| WHERE in-memory adapter is used the classifier SHALL still produce facts and relations and SHALL create no files (EBC-44) | Working-tree hash unchanged; classification counts > 0; architecture present | `ClassifierIsolationTests.cs:121-139` `Assert.Equal(before, after)`, fact/relation counts > 0, implements-operation and uses-contract keys | PASS |
| Canonical payloads SHALL NOT contain connection string, password, token, certificate, or authorization values from classifier content (EBC-45) | Secret tokens absent from classifier payload keys | `ClassifierIsolationTests.cs:166-173` `Assert.DoesNotContain` each SecretTokens entry | PASS |

**Status**: All 45 ACs covered. 1 spec-precision gap flagged (empty-route edge of EBC-06; see Edge Cases). `// SPEC_DEVIATION` in `BoundaryPass.cs:85`.

EvidenceMethod.Semantic on EBC-07/EBC-23 is a Create-time guard. `ConfirmedRelation` does not persist the enum (same as ROSE-17). Taxonomy minimum for `implements-operation` and `uses-contract` is Semantic. Tests assert classifier identity, facets, and `derived_from`, not a stored evidence-method field.

---

## Discrimination Sensor

| Mutation | File:line | Description | Killed? |
| -------- | --------- | ----------- | ------- |
| — | — | Not run | SKIPPED |

**Sensor depth**: skipped
**Result**: SKIPPED (standing user request for csharp2md, same as `symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`, `knowledge-taxonomy-contract`, `engine-bootstrap`, `factual-storage`, and `roslyn-observation-extraction`). No git worktree, no file mutation, no Stryker.

Static gap analysis only (`dotnet-test:test-gap-analysis` step 4 without 4b live mutation):

- Flipping `EvidenceMethod.Semantic` to `Syntactic` on implements-operation would fail Domain `RequireSufficientEvidence` (minimum Semantic). Construction-killed.
- `IsSharedAcrossProjects` OR→AND would drop the OrderPlaced contract; `ContractRelationIntegrationTests` would fail.
- Clone-path equality without `Assert.NotEmpty(candidates)` (EBC-39) would not catch both clones producing empty candidate lists. Retry (EBC-32/40) does assert non-empty candidates. Unverified (static reasoning); not treated as an uncovered AC.

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
| Spec-anchored outcome check (asserted values match spec) | PASS (empty-route edge flagged) |
| Per-layer Coverage Expectation met (domain 1:1 ACs; analysis classifiers happy+edge+error; storage round-trip) | PASS |
| Every test maps to a spec requirement - no unclaimed tests | PASS |
| Documented guidelines followed: `AGENTS.md` / `CLAUDE.md` (net10.0, Workspaces.MSBuild 5.6.0, no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults`, SyntheticSolution-only versioned fixture, LocalCorpus skip when clones absent) | PASS |

Diff `d5f64ee^..b9846cf` stays on Analysis classification/pipeline, Storage relation reconstitution (needed so `implements-operation`/`uses-contract` survive Domain shape guards), one CLI STOR-49 assertion update, and matching tests. No unrelated “improvements”.

STOR-49 `Candidates.IsEmpty` was replaced with `Assert.Contains(... RelationKind.Targets)` in `AnalyzePackageWriteTests.cs:62-67`. STOR-49 itself requires a schema-valid package and exit 0, not empty candidates. The update matches 5A outbound HTTP candidates. Not an EBC SPEC_DEVIATION.

PackageValidator/DomainMapper now pass materialized facts into `ConfirmedRelation.Create` (`sourceFact`/`targetFact`) and allow `payload-role` on FacetBinding. `ClassifierFactMappingTests.cs:96-146` asserts those field values, not only that mapping was called.

Spot-check (P1 HTTP/messaging): assertions name protocol, direction, destinationScope, route literals, classifier id/version, payload-role, and negative cases (PaymentProcessed, PlaceOrderAsync not an entry point). Not trait-only.

---

## Edge Cases

- [x] ControllerBase descendant with no Callable actions: no EntryPoint (EBC-09) — `EntryPointPassTests.cs:108-109`
- [ ] RouteDeclaration with empty route template: spec says empty-string `protocolOperationKey` (EBC-06). **⚠️ Spec-precision gap / SPEC_DEVIATION**: `StructuralLiteral.Create` rejects empty canonical text (`StructuralLiteralTests.cs:66-71`). Production records `missing-route-template` and creates no boundary (`BoundaryPass.cs:85-93`; `BoundaryPassTests.cs:106-111` `Assert.Equal(0, result.FactCount)`, `Assert.Empty(...BoundaryOperation)`, `Assert.Equal("missing-route-template", diagnostic.Code)`). Asserted outcome matches the Domain-feasible diagnostic path, not the spec's empty-string key.
- [x] CreateClient with non-constant argument: UnresolvedRecord cause InsufficientEvidence (EBC-34) — `BoundaryPassTests.cs:265-270`; `RelationPassTests.cs:157-161`
- [x] Anonymous messaging TEvent: no Contract; UnresolvedRecord uses-contract NoCandidateFound (EBC-34) — `ContractPassTests.cs:106`; `RelationPassTests.cs:137-141`
- [x] Project with both controller actions and PublishAsync: HTTP and messaging boundaries on the same component (EBC-01, EBC-06, EBC-17) — `BoundaryIntegrationTests.cs:51-73`
- [x] PaymentProcessed published with no handler: no Contract (EBC-24) — `ContractRelationIntegrationTests.cs:57-59`
- [x] Two callables that would share an EntryPoint identity: SnapshotAccumulator structural corruption (ROSE-21, unchanged) — `SnapshotAccumulatorTests.cs` ROSE-21 cases

Independent Test wording that unresolved sections are non-empty is stronger than EBC-37 and the fixture: Acme.Orders produces no unresolvable messaging types, so `FullClassifierPipelineTests.cs:121-124` allows unresolved count 0. Candidates are asserted non-empty. Not scored as an uncovered AC.

---

## Gate Check

- **Gate command**: `dotnet build` then per-csproj `dotnet test` (multi-csproj `dotnet test` hits MSB1008). CLI used VSTest `--filter "Category!=LocalCorpus"` (xUnit 2.9.3 + `xunit.runner.visualstudio`, not xUnit v3 MTP `--filter-not-trait`).
- **Result**: 1019 passed, 0 failed, 0 skipped among selected tests (serial re-run of Analysis after a parallel CLI/Analysis collision on a leftover `fixtures/csharp2md-analyze-out-*` lock file; that failure is not a product defect)
- **Per-project passed counts**:
  - `Csharp2Md.Domain.Tests`: 545 passed
  - `Csharp2Md.Analysis.Tests`: 283 passed
  - `Csharp2Md.Storage.Tests`: 161 passed
  - `Csharp2Md.Cli.Tests`: 27 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Projection.Tests`: 3 passed
- **Test count before feature** (ROSE, `Category!=LocalCorpus`): 930
- **Test count after feature**: 1019
- **Delta**: +89 executed tests. `[Fact]`/`[Theory]` attributes under `tests/`: 800 (ROSE was 718; +82 attributes). Increase; no silent deletions.
- **Skipped tests**: `LocalCorpusAnalyzeTests` (2 theory cases, `[Trait("Category", "LocalCorpus")]`) excluded by the CLI filter. `fixtures/eShop` and `fixtures/eShopOnContainers` are absent. Documented skip, not a feature failure.
- **Failures**: none on the serial gate.

---

## Fix Plans

None. The empty-route SPEC_DEVIATION is Domain-constrained (TAX-80) and recorded as a spec-precision gap, not an uncovered AC.

---

## Requirement Traceability Update

Applied: EBC-01..EBC-45 status is Verified in `spec.md`.

| Requirement | Previous Status | New Status |
| ----------- | --------------- | ---------- |
| EBC-01..EBC-45 | Implementing | ✅ Verified |

---

## LocalCorpus

`fixtures/eShop` and `fixtures/eShopOnContainers` are absent. LocalCorpus tests were not run. Clones were not added to git.

---

## Summary

**Overall**: Ready

**Spec-anchored check**: 45/45 ACs matched spec outcome | 1 spec-precision gap
**Sensor**: SKIPPED (standing user request)
**Gate**: 1019 passed

**What works**: Classification replaces the stub with ordered classifier passes. Acme.Orders yields project-as-component, HTTP and messaging entry points and boundaries, OrderPlaced contract plus bindings, confirmed `implements-operation`/`uses-contract`, and `targets` candidates (not confirmed `targets`). Retry bytes and clone identities are stable. Storage reconstitutes classifier facts and relations. In-memory adapter writes no files.

**Issues found**: Empty route template cannot be a StructuralLiteral; implementation diagnoses `missing-route-template` instead of an empty-string protocol key (`BoundaryPass.cs:85`).

**Next steps**: Feature is verified. Workstreams 5B–5D wait on an explicit start.
