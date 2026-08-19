# RelationCollector Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and
Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the
full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**User-confirmed deviation from the standard flow:** the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by explicit user request — the user will run Stryker
manually afterward. The Verifier's spec-anchored outcome check, per-AC evidence, and `validation.md` report
still run as normal; only the injected-fault/mutation-kill sub-step is omitted.

---

**Design**: `.specs/features/relation-collector/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/Csharp2Md.Core.Tests/Detection/**`, `Analysis/Syntax/SyntaxFactExtractorTests.cs`, `Analysis/V3DeterminismTests.cs`, `Facts/Validation/FactValidatorTests.cs`) and project guidelines. Guidelines found: `AGENTS.md`/`CLAUDE.md` (routes testing quality to `dotnet-test:*` skills — assertion-quality, test-anti-patterns, crap-analysis — as post-hoc gates, not a coverage-threshold config). No coverage-threshold tool config found (no `.runsettings`/coverlet threshold); strong defaults applied for the Coverage Expectation column.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| ---------- | ------------------- | --------------------- | ----------------- | ------------ |
| Facts/model enum extensions (`FactResolution`, `RelationPartition`) + their direct consumers (`FactMerger.ResolutionRank`, `RelationProjector.Wire`) | unit | Every new enum member exercised in at least one test per consumer switch (no `ArgumentOutOfRangeException` path left silently untested) | `tests/Csharp2Md.Core.Tests/Facts/**/*Tests.cs`, `tests/Csharp2Md.Core.Tests/Projection/Aggregates/*Tests.cs` | `dotnet test csharp2md.slnx` |
| Validation logic (`FactValidator.ValidateRelation`) | unit | All branches; 1:1 to RELC-10 + the existing C2M-FV-005/006/007 rules, including the "no regression for currently-emitted kinds" case | `tests/Csharp2Md.Core.Tests/Facts/Validation/FactValidatorTests.cs` | `dotnet test csharp2md.slnx` |
| Syntax-only candidate detection (`SyntaxFactExtractor` extensions, `RelationNoiseFilter`) | unit | All branches; 1:1 to spec ACs RELC-01..05, RELC-12..16; every listed Edge Case in spec.md covered | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/*Tests.cs`, `tests/Csharp2Md.Core.Tests/Analysis/Relations/*Tests.cs` | `dotnet test csharp2md.slnx` |
| Candidate→fact materialization (`RelationCollector.CreateFacts`/`.Refine`) | unit | All branches; ordinal/claim-fingerprint reproducibility between `CreateFacts` and `Refine` explicitly asserted (design.md's named risk); 1:1 to RELC-07..11 | `tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationCollectorTests.cs` | `dotnet test csharp2md.slnx` |
| Pipeline wiring (`AnalysisEngine.AnalyzeProjectAsync`, `TrustedSemanticProjectProcessor.BindDocuments`) | integration | End-to-end against `fixtures/SyntheticSolution`: syntax-only mode (RELC-06) and trusted mode (RELC-07/08/09) both produce the relations named in spec.md's Independent Tests | `tests/Csharp2Md.Core.Tests/Analysis/*Tests.cs` (pattern of `V3DeterminismTests.cs`) | `dotnet test csharp2md.slnx --filter "Category=Integration"` |
| Retired detectors (`MessagingRelationDetector`, `HttpRelationDetector`) | none (deletion) | n/a — removed, not tested | n/a | build gate only |

## Gate Check Commands

> Generated from the project's established commands (consistent across every prior feature's `STATE.md` handoff — no CI workflow file exists in this repo to source them from instead).

| Gate Level | When to Use | Command |
| ---------- | ------------ | -------- |
| Quick | After tasks with unit tests only | `dotnet test csharp2md.slnx` |
| Full | After tasks with integration tests (pipeline wiring, fixture-level) | `dotnet test csharp2md.slnx` (integration tests are `[Trait("Category","Integration")]` in the same suite, no separate command exists in this repo) |
| Build | After phase completion or the retirement task | `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx` |

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a phase execute in order.

### Phase 1: Data model & validation foundation

T1, T2, T3 have no dependencies on each other or on any other task - run in the listed order (see the complete
dependency graph in **Phase Execution Map** below for the authoritative edge set).

### Phase 2: Shared candidate model & noise filter

T4, T5 have no dependencies on each other or on any other task - run in the listed order.

### Phase 3: Syntax-only candidate detection (all 10 kinds)

T6, T7, T8, T9, T10 all depend on Phase 2's T4 (and some on T5) - see **Phase Execution Map** for the exact
edges; run in the listed order.

### Phase 4: Materialization & pipeline wiring

T11, T12, T13, T14 depend on Phases 1-3's foundation and each other - see **Phase Execution Map** for the exact
edges; run in the listed order.

### Phase 5: Retirement & fixture-level proof

T15, T16 depend on Phase 4's pipeline wiring - see **Phase Execution Map** for the exact edges; run in the
listed order.

---

## Task Breakdown

### T1: Extend `FactResolution` with `Heuristic`/`Candidate` and update `FactMerger.ResolutionRank`

**What**: Add `Heuristic` and `Candidate` to the `FactResolution` enum in descending-confidence declaration
order (`Exact, Partial, Syntactic, Heuristic, Candidate, Unresolved, NotApplicable`), and add the two matching
cases to `FactMerger.ResolutionRank`'s ladder (`Unresolved=1 < Candidate=2 < Heuristic=3 < Syntactic=4`).
**Where**: `src/Csharp2Md.Core/Facts/Model/FactResolution.cs`, `src/Csharp2Md.Core/Facts/Composition/FactMerger.cs`
**Depends on**: None
**Reuses**: Existing enum/rank pattern (no new abstraction).
**Requirement**: RELC-06 (resolution vocabulary), design.md's `FactResolution` Data Model

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards` (enum/switch style)

**Done when**:
- [x] `FactResolution` declares the 7 members in the documented order
- [x] `FactMerger.ResolutionRank` handles all 7 without an `ArgumentOutOfRangeException` path being reachable for a real merge
- [x] `FactResolutionAlgebra.AggregateDocument` still treats `NotApplicable` as skip-worthy (unchanged behavior, regression-tested)
- [x] A merge test asserts a `Heuristic`-resolution fact loses to a `Syntactic`-resolution fact sharing the same identity, and a `Candidate` loses to a `Heuristic`
- [x] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T2: Add `RelationPartition.Structural` and wire it through `RelationProjector`

**What**: Add `Structural` to the `RelationPartition` enum; add it to `RelationProjector.Partitions` and to the
`Wire()` switch (`"structural"` label, matching the existing kebab-case convention).
**Where**: `src/Csharp2Md.Core/Facts/Model/RelationFact.cs`, `src/Csharp2Md.Core/Projection/Aggregates/RelationProjector.cs`
**Depends on**: None
**Reuses**: The existing `Partitions` array / `Wire()` switch pattern (already handles `Inheritance`, unused today).
**Requirement**: RELC-12, RELC-13, RELC-15 (design.md's `RelationPartition` Data Model)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `RelationPartition.Structural` exists
- [ ] `RelationProjector.Project` groups a `Structural`-partition `RelationFact` under its own partition bucket instead of throwing `InvalidOperationException`
- [ ] `RelationProjector`'s Mermaid `Wire()` renders a `structural:`-prefixed edge label for a `Structural` relation with both endpoints resolved to components
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T3: Loosen `FactValidator.ValidateRelation`'s evidence/provenance gate from `IsRuntime` to compile-time-only-kind exclusion

**What**: Change the two `relation.IsRuntime &&` guards (evidence-non-empty, provenance-has-detector-id) in
`ValidateRelation` to instead trigger whenever `!CompileTimeOnlyRelationKinds.Contains(relation.RelationKind)` —
i.e., every relation kind requires evidence + detector provenance except the two already-carved-out compile-time
kinds (`project-reference`, `package-reference`).
**Where**: `src/Csharp2Md.Core/Facts/Validation/FactValidator.cs`
**Depends on**: None
**Reuses**: The existing `CompileTimeOnlyRelationKinds` frozen set and `ValidateRelation` structure — only the gating condition changes, not the rule shape.
**Requirement**: RELC-10 (unconditional evidence)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `Inheritance`-partition relation without evidence now fails `C2M-FV-005` (previously exempt because `Inheritance` isn't in `IsRuntime`) — new test proves the tightened rule
- [ ] Existing `project-reference`/`package-reference` relations (evidence-free, from `CompileTimeReferenceDetector`, still untouched code) still validate cleanly — regression test
- [ ] Existing `FactValidatorTests` for `Http`/`Events`/`DependencyInjection`/`Grpc` partitions still pass unmodified (no behavior change for kinds already requiring evidence)
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T4: Extend `SyntacticRelationCandidate` with evidence span + resolution, migrate the existing `base-or-interface` producer

**What**: Change `SyntacticRelationCandidate`'s shape to `(FactId OwnerId, string RelationKind, string
ObservedTarget, FactResolution ShapeConfidence, int StartLine, int StartColumn, int EndLine, int EndColumn)`
(broadening `OwnerId` from `SymbolFactId` to `FactId`, adding the span+confidence fields). Update the existing
base-list-walk in `SyntaxFactExtractor.Extract` to populate the new fields (span computed via
`root.SyntaxTree.GetLineSpan(type.Type.Span)` before the tree is discarded); keep emitting `"base-or-interface"`
for now — kind-splitting is T6.
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: None
**Reuses**: The existing base-list walk and `NormalizeNode` helper.
**Requirement**: design.md's `SyntacticRelationCandidate` Data Model (foundation for RELC-01..05, RELC-12..16)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `SyntacticRelationCandidate` carries a real evidence span for every candidate (no candidate has a zero/default span)
- [ ] Existing `SyntaxFactExtractorTests` covering `base-or-interface` pass unmodified in assertion intent (span assertions added, not replacing existing ones)
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T5: `RelationNoiseFilter` — shared BCL/framework exclusion predicate

**What**: New static class with `IsLikelyFrameworkType(string simpleName): bool` (syntax-only denylist: `List`,
`Dictionary`, `HashSet`, `Queue`, `Stack`, `StringBuilder`, `Guid`, `Uri`, `TimeSpan`, `DateTime`,
`DateTimeOffset`, `Task`, `CancellationTokenSource`, any `*Exception` suffix) and `IsFrameworkType(ITypeSymbol
type): bool` (semantic path: `ContainingNamespace` starts with `System` or `Microsoft.Extensions`).
**Where**: `src/Csharp2Md.Core/Analysis/Relations/RelationNoiseFilter.cs` (new file, new folder)
**Depends on**: None
**Reuses**: n/a (new, intentionally minimal per spec.md's Assumptions table).
**Requirement**: RELC-16 (shared exclusion rule)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Both methods exist with the documented denylist/namespace rule
- [ ] Unit tests cover every listed denylist entry plus at least 2 non-matching (application-shaped) names for each method
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T6: Split `inherits`/`implements` classification (position + `I`-prefix heuristic)

**What**: Replace the single `"base-or-interface"` kind with `"inherits"`/`"implements"` classification per
design.md's rule: for `interface`/`struct`/`record struct` declarations, every base-list entry is `implements`
(`Syntactic` confidence — this is a certain rule, not a guess); for `class`/`record`/`record class`, the first
base-list entry is `inherits` unless it matches the `I`+uppercase naming convention (in which case
`implements`), every non-first entry is always `implements` (`Syntactic`); the first-entry naming-convention
branch specifically gets `Heuristic` confidence. A base-list entry that can't be classified even heuristically
(e.g., unresolved generic constraint) defaults to `implements`/`Unresolved` (spec.md Edge Case).
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T4
**Reuses**: T4's extended candidate shape and the existing base-list walk.
**Requirement**: RELC-14

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `interface I2 : I1` → `implements`, `Syntactic`
- [ ] `struct S : IDisposable` → `implements`, `Syntactic`
- [ ] `class D : Base, IDisposable` → first entry `inherits`/`Syntactic` (not `I`-prefixed), second `implements`/`Syntactic`
- [ ] `class PaymentsService : Payments.PaymentsBase` (the fixture case from spec.md's P2 Independent Test) → `inherits`, `target_text` `PaymentsBase` or `Payments.PaymentsBase`
- [ ] `class C : IRepository` (single entry, `I`-prefixed) → `implements`, `Heuristic`
- [ ] Unresolvable base-list entry defaults to `implements`/`Unresolved`, never `inherits` (spec.md Edge Case)
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T7: Syntax-only `publishes`/`subscribes`/`handles` candidate detection

**What**: New extraction pass in `SyntaxFactExtractor.Extract`: an invocation named `Publish`/`PublishAsync`
with no explicit generic type argument and whose first argument is an object-creation expression → `publishes`
(`Syntactic`, target_text = created type's simple name); an invocation named `Publish`/`PublishAsync` *with* an
explicit generic type argument → `publishes` (`Syntactic`, target_text = that type argument), regardless of the
argument's own shape (spec.md Edge Case: `PublishAsync<OrderPlaced>(existingInstance)`); an invocation named
`Subscribe`/`SubscribeAsync` with exactly one explicit generic type argument → `subscribes` (`Syntactic`,
target_text = the type argument), and additionally, only when the argument is a bare identifier/method-group
naming an existing method (not an inline lambda) → `handles` with `OwnerId` = that handler method's own nearest
enclosing `SymbolFactId` (walk to the referenced method's own declaration if it's in the same document;
otherwise no `handles` candidate — cross-document handler resolution is semantic-refinement territory, T13).
Neither explicit type argument nor object-creation argument present → no candidate (spec.md Edge Case).
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T4
**Reuses**: T4's candidate shape; member-name/argument-shape rules ported from `MessagingRelationDetector`
(retired in T15).
**Requirement**: RELC-01, RELC-02, RELC-03, RELC-04

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `_eventBus.PublishAsync(new PaymentProcessed(...))` (no explicit `<T>`) → `publishes`, `target_text=PaymentProcessed` (the fixture case)
- [ ] `_bus.Publish<OrderPlaced>(new OrderPlaced(...))` (explicit `<T>`) → `publishes`, `target_text=OrderPlaced`
- [ ] `_eventBus.Subscribe<OrderPlaced>(HandleOrderPlacedAsync)` (the fixture case) → both `subscribes` (target_text=OrderPlaced) and `handles` (`OwnerId` = `HandleOrderPlacedAsync`'s own `SymbolFactId`, target_text=OrderPlaced)
- [ ] `_bus.Subscribe<OrderPlaced>(msg => { })` (inline lambda) → `subscribes` only, no `handles` (spec.md Edge Case)
- [ ] `_bus.PublishAsync(existingVariable)` (no explicit `<T>`, not an object-creation argument) → no candidate emitted
- [ ] All candidates carry `Syntactic` confidence and a real evidence span
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T8: Syntax-only `http-client`/`http-call` candidate detection

**What**: New extraction pass: an invocation named `CreateClient` with a single string-literal argument →
`http-client` (`Syntactic`, target_text = the literal); an invocation whose member name matches
`GetAsync`/`GetStringAsync`/`PostAsync`/`PutAsync`/`DeleteAsync`/`PatchAsync` on a receiver that is either
syntactically typed as `HttpClient` (a locally-declared/parameter type name literally `HttpClient`) or,
conservatively, any receiver when no type information is syntactically available → `http-call` (`Syntactic` when
the receiver type is textually confirmed, `Unresolved` otherwise), carrying `http_method` and `route` (literal
value or expression text) as additional candidate metadata folded into `ObservedTarget`/details at
materialization time (T11).
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T4
**Reuses**: T4's candidate shape; member-name/route-extraction rules ported from `HttpRelationDetector` (retired
in T15).
**Requirement**: RELC-05

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `httpClientFactory.CreateClient("PaymentService")` (the fixture case) → `http-client`, `target_text=PaymentService`
- [ ] `paymentClient.PostAsJsonAsync("payments/authorize", ...)`-shaped call (an HTTP-verb-named invocation on an `HttpClient`-typed receiver) → `http-call` with `http_method=POST` and a route detail
- [ ] A same-named method call on a receiver that is NOT `HttpClient`-shaped (e.g. a custom `PostAsync` on an unrelated type) does not falsely emit `http-call` when the receiver type is syntactically determinable
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T9: Syntax-only `calls`/`creates` candidate detection using `RelationNoiseFilter`

**What**: New extraction pass: a member-access invocation on a field/property/parameter/local receiver, not
already claimed by T7/T8's kinds, whose receiver's syntactic type name (when a type is known) is not
`RelationNoiseFilter.IsLikelyFrameworkType` → `calls` (`Syntactic`, target_text =
`{receiver-expression}.{member-name}`); an object-creation expression (explicit or target-typed `new`) for a
type not on the same denylist, and not already the argument of a claimed `publishes` call → `creates`
(`Syntactic`, target_text = created type's simple name).
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T4, T5, T7 (needs to know which invocations T7 already claimed, to avoid double-emitting), T8 (same, for `http-call`)
**Reuses**: `RelationNoiseFilter` from T5.
**Requirement**: RELC-12, RELC-13, RELC-16

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `paymentsClient.AuthorizePayment(...)`-shaped call (the fixture case, `Acme.Orders/OrderService.cs`) → `calls`, `target_text=paymentsClient.AuthorizePayment`
- [ ] `new List<int>()` does not emit `creates` (denylisted)
- [ ] `new PaymentAuthorizer()` (application type) emits `creates`
- [ ] A call already classified as `http-call`/`publishes` by T7/T8 is not also emitted as `calls`
- [ ] `new PaymentProcessed(...)` inside a `PublishAsync(new PaymentProcessed(...))` call is not double-emitted as both `publishes`-target and `creates`
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T10: Syntax-only `references` candidate detection from `RelevantTypeReferences`

**What**: For each `SymbolFact` produced during extraction, route its already-computed `RelevantTypeReferences`
entries through `RelationNoiseFilter` and a same-document-type check; emit a `references` candidate
(`Syntactic`, `OwnerId` = that symbol's own `SymbolFactId`, target_text = the referenced type name) for each
survivor not already claimed by `calls`/`creates`/`inherits`/`implements` for that same symbol.
**Where**: `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T4, T5, T6, T9 (needs the claimed-kind sets to dedupe against)
**Reuses**: `ReferencedTypes`/`RelevantTypeReferences` (already computed, per design.md's Code Reuse Analysis).
**Requirement**: RELC-15

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] A property typed as an application-defined type not otherwise referenced by a call/creation on that member emits a `references` candidate
- [ ] A parameter typed `CancellationToken`/`string`/other denylisted or primitive type does not emit `references`
- [ ] A type already surfaced via `creates` on the same member is not also emitted as `references`
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T11: `RelationCollector.CreateFacts` — candidate→fact materialization

**What**: New static class `RelationCollector` with `CreateFacts(DocumentFactId documentId, string
relativePath, ImmutableArray<SyntacticRelationCandidate> candidates): ImmutableArray<RelationFact>`. Ports the
`EvidenceFor`/ordinal-`Dictionary<string,int>`/`RelationFactId.Create` pattern from the (soon-retired)
`MessagingRelationDetector`/`HttpRelationDetector`: builds `Evidence` from the candidate's span, wraps
`ObservedTarget` as `new RelationDetail("target_text", ...)`, sets `TargetId = null` and a populated
`UnresolvedReason` unconditionally (RELC-11), maps `RelationKind` to the correct `RelationPartition`
(`inherits`/`implements` → `Inheritance`; `publishes`/`subscribes`/`handles` → `Events`; `http-client`/
`http-call` → `Http`; `calls`/`creates`/`references` → `Structural`), and attaches `FactProvenance` with a real
`DetectorId` (e.g. `io.csharp2md.relation-collector`) so `FactValidator`'s provenance rule (T3) is satisfied
without relying on `DetectorHost`.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/RelationCollector.cs`
**Depends on**: T1, T2, T3 (model/validation must accept the output), T4 (candidate shape)
**Reuses**: The `CreateFact`/`EvidenceFor`/ordinal pattern from the detectors being retired.
**Requirement**: RELC-09, RELC-10, RELC-11

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [ ] Given a synthetic set of candidates covering all 10 kinds, produces one `RelationFact` per candidate with the correct `Partition` mapping
- [ ] Every produced fact has non-empty `Header.Evidence`, a `FactProvenance` with a non-null `DetectorId`, `TargetId = null`, and a non-empty `UnresolvedReason`
- [ ] Two candidates with the same kind + target_text in one document get distinct `RelationFactId`s via ordinal disambiguation (existing pattern, ported)
- [ ] Output passes `FactValidator.Validate` cleanly for a realistic document-level input
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T12: Wire `RelationCollector.CreateFacts` into `AnalysisEngine.AnalyzeProjectAsync`

**What**: In the per-document loop (near where `baseline` is assembled from `documentFact`/sections/symbols),
call `RelationCollector.CreateFacts(extraction.Document.DocumentId, relativePath, extraction.RelationCandidates)`
and append the result to `baseline` before `FactMerger.Merge`. This is the first point a `RelationFact` reaches
`FactStore`/`relations.json` in this codebase's live pipeline (RELC-06/RELC-17: works with zero `SemanticModel`
dependency, since `extraction` comes from `SyntaxFactExtractor.Extract` which already ran unconditionally
earlier in the same method).
**Where**: `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs`
**Depends on**: T6, T7, T8, T9, T10 (real candidates must exist), T11 (materializer)
**Reuses**: The existing `baseline`/`FactMerger.Merge`/`_validate`/`store.Persist` flow — no new plumbing shape, just one more fact source feeding it.
**Requirement**: RELC-06, RELC-17 (this is the P1/P2 Independent Test's actual delivery mechanism)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Running `AnalysisEngine.AnalyzeAsync` against `fixtures/SyntheticSolution` in **syntax-only mode** (default) produces non-empty `subscribes`/`handles`/`publishes`/`http-client`/`http-call`/`calls`/`inherits` relations in the persisted fragments for `Acme.Payments/PaymentsService.cs` and `Acme.Orders/OrderService.cs` — a new integration test, following the `V3DeterminismTests.cs` fixture-driven pattern, asserts this directly (this is literally spec.md's P1 and P2 Independent Tests)
- [ ] The existing `V3DeterminismTests` (byte-identical output across two roots, manifest hash integrity) still pass unmodified — relation facts must be as deterministic as every other fact kind
- [ ] Gate check passes: `dotnet test csharp2md.slnx --filter "Category=Integration"` then full `dotnet test csharp2md.slnx`

**Tests**: integration
**Gate**: full

---

### T13: `RelationCollector.Refine` — semantic refinement sharing `RelationFactId`

**What**: New method `Refine(DocumentFactId documentId, string relativePath,
ImmutableArray<SyntacticRelationCandidate> candidates, SemanticModel model):
ImmutableArray<RelationFact>` on `RelationCollector`. Iterates the **same canonically-ordered candidate list**
`CreateFacts` would receive (shared private ordinal-assignment helper — design.md's named risk) and, per kind:
resolves `inherits`/`implements` via `TypeKind` instead of the naming heuristic (upgrades `Heuristic`/`Syntactic`
→ `Syntactic` with certainty); applies `RelationNoiseFilter.IsFrameworkType(ITypeSymbol)` to `calls`/`creates`
using the real resolved type; for `publishes`/`subscribes`, additionally resolves `target_text` via the
invoked method's constructed generic type argument (`method.TypeArguments[0]`) when the syntax-only pass
found none (the "inference through a variable" case `MessagingRelationDetectorTests.cs` already covers today —
RELC-09's no-regression requirement). Produces enrichment facts with **identical `RelationFactId`s** to their
`CreateFacts` counterparts wherever a matching candidate exists, so `FactMerger` picks the winner by
`ResolutionRank`.
**Where**: `src/Csharp2Md.Core/Analysis/Relations/RelationCollector.cs`
**Depends on**: T11
**Reuses**: T11's `EvidenceFor`/`RelationFactId` construction (shared private helper, not duplicated); `method.TypeArguments[0]` resolution logic ported from `MessagingRelationDetector.IsPublishShape`/`IsSubscribeShape`.
**Requirement**: RELC-08, RELC-09 (no regression vs. today's fully-resolved cases)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `var message = new PaymentProcessed(...); await _bus.PublishAsync(message);` (semantic-only resolvable target, today's `MessagingRelationDetectorTests.cs` case) → `Refine` produces a `publishes` fact with `target_text=PaymentProcessed`, even though `CreateFacts` alone found no object-creation argument to read
- [ ] A dedicated test asserts `CreateFacts` and `Refine`, run over the identical candidate list, mint identical `RelationFactId`s for every kind (the reproducibility risk from design.md)
- [ ] An error/candidate symbol at the invocation site produces no enrichment (or an enrichment that doesn't outrank the baseline) rather than throwing
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T14: Wire `RelationCollector.Refine` into `TrustedSemanticProjectProcessor.BindDocuments`

**What**: Add `EnrichedRelations: ImmutableArray<RelationFact>` to `SemanticProcessedDocument` and a matching
builder to `DocumentState` (mirroring `EnrichedSymbols`). In `BindDocuments`, after
`SymbolFactEnricher().Enrich(...)`, call `RelationCollector.Refine(...)` with the document's
`RelationCandidates` and the bound `SemanticModel`, store the result on the state. In
`AnalysisEngine.AnalyzeProjectAsync`, fold `document.EnrichedRelations` into the `enrichment` argument of the
`FactMerger.Merge` call alongside `document.EnrichedSymbols`.
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/TrustedSemanticProjectProcessor.cs`, `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs`
**Depends on**: T12, T13
**Reuses**: The exact `EnrichedSymbols` plumbing shape already in `DocumentState`/`SemanticProcessedDocument`/`AnalysisEngine`.
**Requirement**: RELC-07, RELC-08, RELC-09

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Running `AnalysisEngine.AnalyzeAsync` against `fixtures/SyntheticSolution` in **trusted mode** (`--trust trusted-solution`) upgrades the same relations T12 proved in syntax-only mode to at least `Syntactic` resolution wherever semantic binding succeeds, and the `PublishAsync(message)`-through-a-variable case (T13) now appears with a real `target_text` in the persisted output
- [ ] An unresolved cross-project type in trusted mode (the originally-reported defect scenario) still yields the syntax-only baseline relation rather than nothing (RELC-08 proven end-to-end, not just at the `RelationCollector` unit level)
- [ ] Gate check passes: `dotnet test csharp2md.slnx --filter "Category=Integration"` then full `dotnet test csharp2md.slnx`

**Tests**: integration
**Gate**: full

---

### T15: Retire `MessagingRelationDetector` and `HttpRelationDetector`

**What**: Delete `src/Csharp2Md.Core/Detection/Messaging/MessagingRelationDetector.cs`,
`src/Csharp2Md.Core/Detection/Http/HttpRelationDetector.cs`, and their test files
(`tests/Csharp2Md.Core.Tests/Detection/Messaging/MessagingRelationDetectorTests.cs`,
`tests/Csharp2Md.Core.Tests/Detection/Http/HttpRelationDetectorTests.cs`). Confirm `DetectorHostTests.cs` and
the remaining 4 detectors (`Grpc`/`CompileTime`/`DependencyInjection`/`AspNetCore`) don't reference either
removed type (already verified during Design — no matches).
**Where**: deletions only, listed above
**Depends on**: T12, T14 (the new path must already be proven equivalent-or-better before the old one is removed)
**Reuses**: n/a
**Requirement**: RELC-09 (replace, no dangling duplicate coverage)

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:slopwatch` (confirm no orphaned references left behind after deletion)

**Done when**:
- [ ] Both files and both test files are deleted
- [ ] `DetectorHost`/`DetectorHostTests` and the 4 remaining detectors still build and their own tests still pass unmodified
- [ ] `dotnet build csharp2md.slnx -c Release` has zero unused-`using`/dangling-reference warnings from the deletion
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` → `dotnet format csharp2md.slnx --verify-no-changes` → `dotnet test csharp2md.slnx`

**Tests**: none (deletion; covered by the Build gate)
**Gate**: build

---

### T16: End-to-end fixture proof (spec.md Independent Tests, both stories)

**What**: New integration test file asserting, in one place, exactly what spec.md's P1 and P2 Independent Tests
describe: run `AnalysisEngine.AnalyzeAsync` against `fixtures/SyntheticSolution` in default (syntax-only) mode
and assert the persisted `relations` for `PaymentsService.cs` include `subscribes`/`handles`/`publishes` (with
correct `target_text`s) and `inherits` (`PaymentsBase`), and `OrderService.cs` includes `http-client`,
`http-call`, and `calls`. Assert `relations: []` is no longer the outcome for these documents (the literal
reported defect, closed).
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/RelationCollectorEndToEndTests.cs` (new)
**Depends on**: T12, T15 (final shape of the pipeline, old detectors gone)
**Reuses**: `V3DeterminismTests.cs`'s fixture-driven `AnalysisEngine.AnalyzeAsync` invocation pattern.
**Requirement**: All of RELC-01..17 (traceability closure)

**Tools**:
- MCP: NONE
- Skill: `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns` (per `AGENTS.md`'s quality-gate table, before declaring the suite done)

**Done when**:
- [ ] Every P1/P2 Independent Test scenario from spec.md has a direct, named assertion in this file
- [ ] `spec.md`'s Requirement Traceability table is updated to `Status: Verified` for all 17 IDs once this passes
- [ ] Gate check passes: full Build gate (`dotnet build csharp2md.slnx -c Release` → `dotnet format csharp2md.slnx --verify-no-changes` → `dotnet test csharp2md.slnx`)

**Tests**: integration
**Gate**: build

**Commit**: `feat(relations): prove RelationCollector against the reported relations:[] fixture scenario`

---

## Phase Execution Map

Complete dependency graph - every arrow below corresponds to a task's declared `Depends on`, and every declared
`Depends on` has its arrow below (this is the authoritative edge set the Diagram-Definition Cross-Check verifies
against):

```
T4 -> T6
T4 -> T7
T4 -> T8
T4 -> T9
T5 -> T9
T7 -> T9
T8 -> T9
T4 -> T10
T5 -> T10
T6 -> T10
T9 -> T10
T1 -> T11
T2 -> T11
T3 -> T11
T4 -> T11
T6 -> T12
T7 -> T12
T8 -> T12
T9 -> T12
T10 -> T12
T11 -> T12
T11 -> T13
T12 -> T14
T13 -> T14
T12 -> T15
T14 -> T15
T12 -> T16
T15 -> T16
```

T1, T2, T3, T4, T5 have no incoming edges - they are the roots, sequenced only by phase/listed order (Phase 1:
T1, T2, T3; Phase 2: T4, T5). Execution is strictly sequential within and across phases - there is no
intra-phase parallelism.

---

## Task Granularity Check

| Task | Scope | Status |
| ---- | ----- | ------ |
| T1: Extend FactResolution + ResolutionRank | 1 enum + its 1 direct consumer function | ✅ Granular (cohesive pair, consumer throws if not updated together) |
| T2: Add RelationPartition.Structural + RelationProjector wiring | 1 enum + its 1 direct consumer | ✅ Granular (same reasoning) |
| T3: FactValidator evidence/provenance gate change | 1 function | ✅ Granular |
| T4: Extend SyntacticRelationCandidate shape | 1 type + its 1 existing producer | ✅ Granular |
| T5: RelationNoiseFilter | 1 new class | ✅ Granular |
| T6: inherits/implements split | 1 extraction pass in 1 file | ✅ Granular |
| T7: publishes/subscribes/handles detection | 1 extraction pass in 1 file | ✅ Granular |
| T8: http-client/http-call detection | 1 extraction pass in 1 file | ✅ Granular |
| T9: calls/creates detection | 1 extraction pass in 1 file | ✅ Granular |
| T10: references detection | 1 extraction pass in 1 file | ✅ Granular |
| T11: RelationCollector.CreateFacts | 1 function, 1 new file | ✅ Granular |
| T12: Wire CreateFacts into AnalysisEngine | 1 integration point, 1 file | ✅ Granular |
| T13: RelationCollector.Refine | 1 function, same file as T11 | ✅ Granular |
| T14: Wire Refine into TrustedSemanticProjectProcessor | 1 integration point, 2 files (processor + the one-line fold in AnalysisEngine) | ⚠️ OK if cohesive — both edits are the same wiring change, inseparable |
| T15: Retire 2 detectors | Deletion only, 4 files | ⚠️ OK if cohesive — one atomic "remove the superseded pair" action |
| T16: End-to-end fixture proof | 1 new test file | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| ---- | ----------------------- | --------------- | ------ |
| T1 | None | (Phase 1 start) | ✅ Match |
| T2 | None | T1 → T2 | ✅ Match (sequential within phase, no data dependency, order is presentation-only) |
| T3 | None | T2 → T3 | ✅ Match |
| T4 | None | (Phase 2 start) | ✅ Match |
| T5 | None | T4 → T5 | ✅ Match |
| T6 | T4 | T5 → T6 (phase boundary) | ✅ Match — T6 depends on T4 (Phase 2), diagram shows Phase 2 → Phase 3 |
| T7 | T4 | T6 → T7 | ✅ Match |
| T8 | T4 | T7 → T8 | ✅ Match |
| T9 | T4, T5, T7, T8 | T8 → T9 | ✅ Match — all of T9's deps (T4,T5,T7,T8) are in earlier phases/tasks |
| T10 | T4, T5, T6, T9 | T9 → T10 | ✅ Match |
| T11 | T1, T2, T3, T4 | (Phase 4 start) | ✅ Match — all deps in Phases 1-2 |
| T12 | T6, T7, T8, T9, T10, T11 | T11 → T12 | ✅ Match — remaining deps (T6-T10) are in Phase 3 |
| T13 | T11 | T12 → T13 | ✅ Match |
| T14 | T12, T13 | T13 → T14 | ✅ Match |
| T15 | T12, T14 | (Phase 5 start) | ✅ Match |
| T16 | T12, T15 | T15 → T16 | ✅ Match |

No dependency points to a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| ---- | ----------------------------- | ------------------ | ----------- | ------ |
| T1 | Facts/model enum extensions | unit | unit | ✅ OK |
| T2 | Facts/model enum extensions | unit | unit | ✅ OK |
| T3 | Validation logic | unit | unit | ✅ OK |
| T4 | Syntax-only candidate detection | unit | unit | ✅ OK |
| T5 | Syntax-only candidate detection | unit | unit | ✅ OK |
| T6 | Syntax-only candidate detection | unit | unit | ✅ OK |
| T7 | Syntax-only candidate detection | unit | unit | ✅ OK |
| T8 | Syntax-only candidate detection | unit | unit | ✅ OK |
| T9 | Syntax-only candidate detection | unit | unit | ✅ OK |
| T10 | Syntax-only candidate detection | unit | unit | ✅ OK |
| T11 | Candidate→fact materialization | unit | unit | ✅ OK |
| T12 | Pipeline wiring | integration | integration | ✅ OK |
| T13 | Candidate→fact materialization | unit | unit | ✅ OK |
| T14 | Pipeline wiring | integration | integration | ✅ OK |
| T15 | Retired detectors | none (deletion) | none | ✅ OK |
| T16 | Pipeline wiring (fixture proof) | integration | integration | ✅ OK |

No violations.
