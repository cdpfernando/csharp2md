# RelationCollector Specification

## Problem Statement

`MessagingRelationDetector` and `HttpRelationDetector` only run when Roslyn's `SemanticModel` fully resolves the
invocation being inspected (`if (context.SemanticDocument is not { } semantic) return DetectorResult.Create();`,
and a hard `GetOperation(syntax) is not IInvocationOperation` guard per invocation). Under the default
syntax-only trust mode (AD-012) there is no `SemanticModel` at all, so these detectors never run; even in
trusted mode, a single unresolved cross-project type silently drops the relation instead of degrading it. The
result is real, syntactically obvious relations (`PaymentsService` subscribing to `OrderPlaced`,
`AuthorizePayment` publishing `PaymentProcessed`, `OrderService` calling a named `PaymentService` HTTP client)
never reaching `relations.json` — `relations: []` even though the source plainly contains them.

## Goals

- [x] Detecting a relation's existence no longer depends on Roslyn fully resolving its target — the collector
      records the relation from syntax first and leaves target resolution to a separate, future stage.
- [x] `Acme.Payments/PaymentsService.cs`'s subscribe/publish and `Acme.Orders/OrderService.cs`'s named-HTTP-client
      scenarios produce relations under the default (syntax-only, untrusted) analysis mode, which they do not
      today.
- [x] Every collected relation carries `target_text` (the observed name, reusing the existing `RelationDetail`
      key/value shape), a resolution state describing how confidently the *shape* was recognized, and `Evidence`
      pointing at the exact source span — unconditionally, not gated on the relation's partition.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature                                                                    | Reason                                                                                                    |
| --------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `grpc-call` / `GrpcRelationDetector`                                       | User deprioritized gRPC explicitly for this pass; detector stays exactly as-is, untouched.                |
| `RelationResolver` (turning `target_text` into a proven `target_id`)       | User confirmed this feature only has to shape the output so a resolver can attach later without re-running collection — it does not build that resolver. |
| `DependencyInjectionDetector`, `AspNetCoreDetector`, `CompileTimeReferenceDetector` | Not part of the reported bug (`CompileTimeReferenceDetector` already resolves eagerly from evaluated MSBuild data with no SemanticModel dependency); DI/AspNetCore relation kinds are untouched. |
| New relation kinds beyond the initial 10 (`reads`, `writes`, `configured-by`, `registered-as`, `resolves-to`, `depends-on`) | Named by the user as "posteriormente" — future work, not this feature's acceptance criteria.              |
| Discrimination-sensor / mutation validation (Stryker) at the Verifier step | User will run Stryker manually afterward; the automated Verifier's mutation-testing sub-step is skipped for this feature by explicit request. |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here - nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --------------------- | --------------- | --------- | ---------- |
| BCL/framework exclusion list for `calls`/`creates` | A documented, shared denylist predicate (well-known `System.*`/collection/LINQ shapes), applied when a receiver/created type's identity is knowable; used to bound `calls`/`creates` volume per the user's explicit choice to restrict rather than emit unfiltered | Exact enumeration is an implementation detail, not a product decision; user already chose "restrict, don't emit everything" | n — finalized in Design |
| `inherits` vs `implements` split for a base-list entry | When `SemanticModel` is available: split by `TypeKind` (`Class` → `inherits`, `Interface` → `implements`), resolution `Syntactic`. When unavailable: first base-list entry → `inherits` unless it matches the `I`+uppercase interface-naming convention, every non-first entry → `implements` (guaranteed by C#'s single-inheritance rule); resolution `Heuristic` in that branch | No syntax-only rule can distinguish a base class from an interface with certainty in C#; the naming-convention fallback is exactly what the `Heuristic` resolution state exists to describe | y |
| `references` scope | Field/property/parameter type names and generic type arguments that are not already claimed by `calls`/`creates`/`inherits`/`implements`, and are not a type declared in the current document | Unscoped, "any type name anywhere" would duplicate most of `calls`/`creates`; matches the user's "referência direta" / "integração via pacote" framing | n — finalized in Design |
| `FactResolution` enum insertion position for the two new members (`Candidate`, `Heuristic`) | Inserted so the `markdown-cleanup` feature's `## Analysis` YAML block (which groups by "fixed enum-declaration order") still reads sensibly; exact position is a Design call, but any approved Verify snapshot whose grouping shifts gets re-approved as part of this feature's tasks | Enum order is cosmetic for that projector, but re-approving now (not silently) keeps the ripple visible instead of surprising | y |
| `handles` relation's `SourceId` | The handler method's symbol/syntactic-declaration id (or, when that can't be resolved, the declaring document's id) — never a synthesized id for the unresolved event-type text itself | `RelationFact.SourceId` is a mandatory `FactId`; AD-014's grammar and AD-011 both forbid minting an identity from unproven text, so "OrderPlaced --handled-by--> Handler" has to be modeled from the handler's side, not the event's side | y |
| `PublishAsync`/`Subscribe` target-name extraction without semantic help | Explicit generic type-argument list (`Subscribe<OrderPlaced>(...)`) is read directly when present; otherwise (`PublishAsync(new PaymentProcessed(...))`, no explicit `<T>`) the first argument's object-creation-expression type name is used; if neither syntactic form yields a name, no relation is emitted for that call | Matches both shapes in the fixture code exactly (`Subscribe<OrderPlaced>` has an explicit type argument; `PublishAsync(new PaymentProcessed(...))` does not) without inventing a third inference path | y |

**Open questions:** none - all resolved or logged above.

---

## User Stories

### P1: Messaging and HTTP relations survive incomplete semantic binding ⭐ MVP

**User Story**: As a csharp2md user analyzing a real multi-project solution, I want `publishes`, `subscribes`,
`handles`, `http-client`, and `http-call` relations recorded even when Roslyn can't fully resolve the target
type or method, so that `relations.json` reflects what the source code plainly shows instead of going empty.

**Why P1**: This is the exact reported defect (`relations: []` on the fixture solution) and the highest-value
slice — it directly replaces the two detectors most responsible for it.

**Acceptance Criteria**:

1. WHEN a document contains an invocation whose member name is `Subscribe` or `SubscribeAsync` with exactly one
   explicit generic type argument THEN the system SHALL emit a `subscribes` RelationFact (source = the
   subscribing document or containing type) whose `target_text` RelationDetail equals that type argument's
   simple name.
2. WHEN that same `Subscribe`/`SubscribeAsync` invocation's argument resolves to a named method (a method-group
   or a lambda naming an existing method) THEN the system SHALL additionally emit a `handles` RelationFact whose
   `SourceId` is the handler method's own symbol/declaration id and whose `target_text` equals the same type
   argument's simple name.
3. WHEN a document contains an invocation whose member name is `Publish` or `PublishAsync`, has no explicit
   generic type argument, and whose first argument is an object-creation expression THEN the system SHALL emit
   a `publishes` RelationFact whose `target_text` equals the created type's simple name.
4. WHEN a document contains an invocation whose member name is `CreateClient` with a single string-literal
   argument THEN the system SHALL emit an `http-client` RelationFact whose `target_text` equals the literal.
5. WHEN a document contains an invocation recognized as an HTTP request method (`GetAsync`, `GetStringAsync`,
   `PostAsync`, `PutAsync`, `DeleteAsync`, `PatchAsync`) on a receiver syntactically or semantically shaped like
   an `HttpClient` THEN the system SHALL emit an `http-call` RelationFact carrying the HTTP method and the route
   argument (literal value, or expression text when not a literal) as RelationDetails.
6. WHILE Roslyn's `SemanticModel` for a document is unavailable (syntax-only analysis, the default per AD-012)
   the system SHALL still produce every relation in criteria 1-5 from syntax alone, each with
   `FactResolution.Unresolved` or `FactResolution.Syntactic` (never `Exact`) and a populated `UnresolvedReason`.
7. IF Roslyn's `SemanticModel` is available but the invocation's target member or receiver type does not fully
   resolve (an error/candidate symbol) THEN the system SHALL still emit the relation from the syntactic shape
   rather than silently dropping it, closing the reported `relations: []` defect.
8. The system SHALL replace `MessagingRelationDetector` and `HttpRelationDetector`'s registrations with the new
   collector; any scenario the two prior detectors already resolved fully (SemanticModel available and
   confirming the shape) SHALL keep at least the same resolution confidence as before.
9. The system SHALL attach `Evidence` (document id, relative path, one-based line/column span) to every relation
   the collector emits, regardless of the relation's runtime/compile-time partition.
10. The system SHALL leave every collected relation's `TargetId` as `null` and populate `UnresolvedReason` — this
    feature never promotes `target_text` to a proven `target_id` (that is `RelationResolver`'s job, out of
    scope here).

**Independent Test**: Run csharp2md against `fixtures/SyntheticSolution` in the default (syntax-only) mode and
confirm `Acme.Payments/PaymentsService.cs`'s document facts include `subscribes`/`handles`/`publishes`
relations for `OrderPlaced`/`PaymentProcessed`, and `Acme.Orders/OrderService.cs`'s document facts include an
`http-client` relation for the named client and an `http-call` relation for its request — all with `target_text`
set and `TargetId` null. Today's build produces zero relations for these documents in this mode.

---

### P2: Structural relations (calls, creates, inherits, implements, references)

**User Story**: As a csharp2md user, I want method calls, object creations, type inheritance/implementation, and
direct type references collected from syntax — even unresolved — so the tool surfaces internal component
dependencies useful for reverse engineering, not just messaging/HTTP edges.

**Why P2**: Builds on the same collector/evidence/target_text contract P1 establishes; broadens coverage beyond
the two kinds directly named in the bug report, at lower priority since it's new capability rather than a fix.

**Acceptance Criteria**:

1. WHEN a document contains a member-access invocation on a field/property/parameter/local receiver, AND that
   invocation isn't already claimed by `http-call`/`publishes`/`subscribes`, AND the receiver's type (when
   knowable) is not on the shared BCL/framework exclusion list THEN the system SHALL emit a `calls` RelationFact
   whose `target_text` is `{receiver-expression}.{member-name}`.
2. WHEN a document contains an object-creation expression (explicit or target-typed `new`) for a type not on the
   shared BCL/framework exclusion list, and not already captured as a `publishes` target THEN the system SHALL
   emit a `creates` RelationFact whose `target_text` is the created type's simple name.
3. WHEN a `class`/`record`/`record class`/`struct`/`record struct`/`interface` declaration has a base list THEN
   the system SHALL emit one `inherits` or `implements` RelationFact per base-list entry, classified per the
   Assumptions table's rule, with `target_text` equal to that entry's simple name.
4. WHEN a field, property, or parameter declaration's type (or a generic type argument within it) names a type
   not declared in the current document and not already claimed by `calls`/`creates`/`inherits`/`implements`
   THEN the system SHALL emit a `references` RelationFact whose `target_text` is that type's simple name.
5. The system SHALL apply the exact same BCL/framework exclusion predicate to both `calls` and `creates` — one
   shared rule, not two divergent implementations.
6. WHILE Roslyn's `SemanticModel` is unavailable, the system SHALL still emit `calls`/`creates`/`inherits`/
   `implements`/`references` relations from syntax alone (consistent with P1's criterion 6).

**Independent Test**: Run csharp2md against `fixtures/SyntheticSolution` and confirm `Acme.Orders/OrderService.cs`
produces at least one `calls` relation for its `paymentsClient.AuthorizePayment(...)`-shaped invocation, and that
`Acme.Payments/PaymentsService.cs` (`sealed class PaymentsService : Payments.PaymentsBase`) produces an
`inherits` relation with `target_text` `PaymentsBase` (or `Payments.PaymentsBase`).

---

## Edge Cases

- IF an invocation named `Publish`/`PublishAsync` has an explicit generic type argument instead of an
  object-creation argument (e.g., `PublishAsync<OrderPlaced>(existingInstance)`) THEN the system SHALL read
  `target_text` from the explicit type argument instead of requiring an object-creation shape.
- IF neither an explicit generic type argument nor an object-creation-expression argument is present for a
  `Publish`/`PublishAsync`/`Subscribe`/`SubscribeAsync` invocation THEN the system SHALL emit no relation for
  that call (no fabricated `target_text`).
- IF a `Subscribe<T>` call's handler argument is an inline lambda rather than a named method THEN the system
  SHALL still emit the `subscribes` relation (criterion P1-1) but SHALL NOT emit a `handles` relation (there is
  no handler symbol/declaration to be its source).
- WHEN the same relation kind + target_text pair occurs more than once in a document THEN the system SHALL
  reuse the existing occurrence-ordinal disambiguation already used by `RelationFactId.Create` (no duplicate
  IDs).
- IF a base-list entry's simple name cannot be classified with even the naming-convention heuristic (e.g., an
  unresolved generic type parameter used as a constraint-only bound) THEN the system SHALL default to
  `implements` with `FactResolution.Unresolved` rather than guessing `inherits`.

---

## Requirement Traceability

| Requirement ID | Story                    | Phase   | Status  |
| --------------- | ------------------------- | ------- | ------- |
| RELC-01         | P1: Messaging/HTTP survives incomplete binding | Execute | Verified |
| RELC-02         | P1 (subscribes)            | Execute | Verified |
| RELC-03         | P1 (handles)                | Execute | Verified |
| RELC-04         | P1 (publishes)              | Execute | Verified |
| RELC-05         | P1 (http-client)            | Execute | Verified |
| RELC-06         | P1 (http-call)               | Execute | Verified |
| RELC-07         | P1 (syntax-only mode)        | Execute | Verified |
| RELC-08         | P1 (error/candidate symbol tolerance) | Execute | Verified |
| RELC-09         | P1 (detector replacement, no regression) | Execute | Verified |
| RELC-10         | P1 (unconditional evidence)  | Execute | Verified |
| RELC-11         | P1 (target always null, unresolved reason) | Execute | Verified |
| RELC-12         | P2 (calls)                   | Execute | Verified |
| RELC-13         | P2 (creates)                 | Execute | Verified |
| RELC-14         | P2 (inherits/implements)     | Execute | Verified |
| RELC-15         | P2 (references)              | Execute | Verified |
| RELC-16         | P2 (shared BCL exclusion)    | Execute | Verified |
| RELC-17         | P2 (syntax-only mode)        | Execute | Verified |

**ID format:** `RELC-[NUMBER]`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 17 total, 17 mapped to tasks, 0 unmapped.

**Evidence (T1-T16, branch `feat/relation-collector`):**

- RELC-01 (P1 umbrella): `RelationCollector.CreateFacts`/`.Refine` (T11, T13) wired into `AnalysisEngine.AnalyzeProjectAsync` (T12) and `TrustedSemanticProjectProcessor.BindDocuments` (T14); proven end-to-end by `RelationCollectorEndToEndTests.cs` (T16) against `fixtures/SyntheticSolution`.
- RELC-02/03/04 (subscribes/handles/publishes): detection in `SyntaxFactExtractor` (T7), materialization (T11), syntax-only wiring (T12), semantic variable-inference for `publishes` (T13), trusted wiring (T14), spec-literal assertions (`RelationCollectorEndToEndTests.P1_PaymentsServiceDocument_...`, T16).
- RELC-05/06 (http-client/http-call): detection (T8), `http-call`'s pipe-delimited target split into `http_method`/`route` details (T11), syntax-only + spec-literal proof (T12, T16).
- RELC-07/17 (syntax-only mode, P1 and P2): `RelationCollectorWiringTests.cs` (T12) proves every in-scope kind is produced with zero `SemanticModel` dependency.
- RELC-08 (error/candidate symbol tolerance): `Refine`'s error-symbol guard never throws (T13 unit test); the real fixture's unresolved `Payments.PaymentsBase` (gRPC-generated, never actually codegen'd) still yields its syntax-only baseline relation in trusted mode (`RelationCollectorTrustedWiringTests.cs`, T14).
- RELC-09 (detector replacement, no regression): `MessagingRelationDetector`'s `IsPublishShape`/`QualifyingCandidates` shape-matching ported into `RelationCollector.Refine` (T13) before both detectors and their tests were deleted (T15).
- RELC-10 (unconditional evidence): `FactValidator.ValidateRelation`'s evidence/provenance gate keyed off `CompileTimeOnlyRelationKinds` instead of `IsRuntime` (T3); every `RelationCollector`-produced fact carries non-empty `Header.Evidence` (T11 unit test).
- RELC-11 (target always null, unresolved reason): `RelationCollector.CreateFacts`/`.Refine` set `TargetId = null` and a populated `UnresolvedReason` unconditionally (T11 unit test, reasserted end-to-end by T16).
- RELC-12/13/16 (calls, creates, shared BCL exclusion): `RelationNoiseFilter` (T5) applied identically by both kinds' detection passes (T9).
- RELC-14 (inherits/implements): position + `I`-prefix heuristic (T6), upgraded to a certain `TypeKind`-based classification in trusted mode (T13); the real fixture's `PaymentsService : Payments.PaymentsBase` proves the `inherits` case end-to-end (T16).
- RELC-15 (references): `RelevantTypeReferences` routed through `RelationNoiseFilter` and same-document/already-claimed dedup (T10).

---

## Success Criteria

- [x] Running csharp2md against `fixtures/SyntheticSolution` in default (syntax-only) mode produces non-empty
      `subscribes`/`handles`/`publishes`/`http-client`/`http-call` relations for `PaymentsService.cs` and
      `OrderService.cs`, where the current build produces zero.
- [x] `MessagingRelationDetector` and `HttpRelationDetector` are retired; `RelationCollector` is the sole
      producer of their former relation kinds plus the five new structural kinds, with no duplicate coverage.
- [x] `FactValidator`, `FactStore`, `RelationProjector`, `RelationPartition`, and the JSON schema/contracts
      accept the 10 in-scope relation kinds and the extended `FactResolution` vocabulary (`Candidate`,
      `Heuristic`) without special-casing.
- [x] Full test suite green; `dotnet build -c Release` and `dotnet format --verify-no-changes` both clean.
