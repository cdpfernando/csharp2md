# Knowledge Taxonomy Contract Specification

## Problem Statement

Seven downstream workstreams — from `engine-bootstrap` to `generator-cli-projections-certification` — are blocked because csharp2md has no machine-readable taxonomy. The legacy implementation mixes structure, protocol, lifecycle and confidence into one flat relation model, so extraction, classification, storage and projection each carry their own private notion of what a fact is. Until identities, fact families, facets, the relation matrix, proof states and the version axes exist as one enforced contract, every later workstream would re-invent them and drift apart.

## Goals

- [ ] Ship `Csharp2Md.Domain` as a dependency-free assembly that defines all five fact families, the observation contract, the closed facet vocabularies, the twelve canonical relations, the three proof-state axes, the identity grammar and the five version axes.
- [ ] Make illegal taxonomy states unrepresentable: every closed vocabulary, every relation triple and every required identity component is enforced at construction, with a named rejection.
- [ ] Emit a committed machine-readable registry from the domain, guarded by a byte-comparison drift gate so code and registry can never disagree.
- [ ] Keep `csharp2md.slnx` building green with legacy `Csharp2Md.Core` still in place, so no downstream workstream inherits a broken tree.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| New `Csharp2Md.Analysis`, `Csharp2Md.Storage`, `Csharp2Md.Projection` assemblies and the pipeline skeleton | Owned by workstream 2 `engine-bootstrap` |
| Removal of legacy `Csharp2Md.Core`, legacy CLI contracts and legacy taxonomic code | Owned by workstream 2 `engine-bootstrap` |
| Wire schemas, JSON serialization, sharding, staging and transactional commit | Owned by workstream 3 `factual-storage` |
| Roslyn binding, extractors, inventory and the populated observation ledger | Owned by workstream 4 `roslyn-observation-extraction` |
| Concrete classifier rule instances and framework support declarations | Owned by workstreams 5A–5D; this feature defines only the registry surface a rule must declare against |
| Discovery and enumeration of analysis variants from MSBuild | Owned by workstream 4; this feature defines only the analysis-variant identity |
| Catalogs, postings, source locators, Markdown and any human-readable rendering of the registry | Owned by workstream 6 `retrieval-projections` |
| Coverage metric and certification record shapes | Owned by workstreams 3 and 8; they measure a run, not the taxonomy |
| The override mechanism that classifies or associates existing identities | Deferred; only the logical-key hook that stabilises identity is in scope here |
| Detection of secrets in source spans | Owned by workstream 4; this feature defines the suspected-secret evidence shape and the literal allowlist |
| Cross-solution composition semantics | Owned by workstream 7 `multi-solution-composition` |
| Packing `Csharp2Md.Domain` as a published NuGet package | No external consumer exists; AD-002 removes any compatibility obligation |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here — nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Registry authority direction | Hand-author the C# domain; a test-owned emitter writes the registry file and a drift gate fails the build on mismatch | Confirmed with the user; keeps one editable source and satisfies AD-006, which forbids JSON inside the domain | y |
| Legacy coexistence | Add `Csharp2Md.Domain` to `csharp2md.slnx` beside legacy `Csharp2Md.Core`, with no reference in either direction | Confirmed with the user; legacy removal stays in workstream 2 while the tree stays buildable | y |
| Enforcement strength | The domain rejects invalid relation triples and unregistered facet values at construction | Confirmed with the user; a contract that only advertises rules would let workstreams 3–5 diverge | y |
| Story slicing | One atomic P1 covering the whole contract, no P2 or P3 tier | Confirmed with the user; a partial taxonomy unblocks none of the seven dependent workstreams | y |
| Registry file location and format | `contracts/taxonomy-registry.json`, UTF-8 without BOM, LF endings, two-space indent, keys in a declared stable order | The legacy `schemas/` folder is scheduled for deletion in workstream 2, so a new path avoids inheriting a doomed location; stable ordering is what makes the drift gate a byte comparison | n |
| Test project | New `tests/Csharp2Md.Domain.Tests` using the repository's existing xUnit and Verify conventions | The legacy `Csharp2Md.Core.Tests` project is removed in workstream 2, so tests placed there would be deleted with it | n |
| Initial version-axis values | All five axes start at `1` as monotonic integers | No contract has ever been released, and AD-002 removes any compatibility obligation | n |
| How rejection is surfaced | Construction fails; whether that is an exception or a result type is a `design.md` decision | The spec must fix the observable outcome without pre-empting the API shape | n |
| Determinism evidence | Identity determinism is proven by unit tests over synthetic inputs, including a simulated clone-path change and a shuffled input order | No pipeline exists yet to run end to end, and the domain has no filesystem dependency | n |
| Meaning of "exactly" in vocabulary criteria | The registered set equals the documented set with no extra and no missing member, asserted by set equality rather than by membership spot checks | A membership-only assertion would pass while extra values silently leak into a closed axis | n |
| `contains` relation scope | Restricted to structural owner and structural child, matching `docs/architecture/taxonomy.md` | Widening it would recreate the generic containment edge the taxonomy principles reject | n |

**Open questions:** none — all resolved or logged above.

---

## Implicit-requirement dimensions sweep

Large scope, so every dimension resolves to a requirement or an explicit exclusion.

| Dimension | Coverage |
| --- | --- |
| Input validation and bounds | TAX-22, TAX-34, TAX-39, TAX-43, TAX-68, TAX-71 — closed vocabularies, required components and the literal allowlist all reject at construction |
| Failure and partial-failure states | TAX-57, TAX-58 — unresolved records and open frontiers are first-class outcomes. Partial-write and staging failure is N/A because persistence is workstream 3 |
| Idempotency, retry, duplicate handling | TAX-31, TAX-32, TAX-69, TAX-81 — identity is a pure function of its components, so re-derivation is idempotent, and collisions and duplicate registrations fail loudly |
| Auth boundaries and rate limits | N/A because the domain is an in-process library with no network surface, no callers outside the engine and no I/O |
| Concurrency and ordering | TAX-63 — identities are order-independent. Thread safety follows from immutability, which TAX-83 requires |
| Data lifecycle and expiry | TAX-79, TAX-80 — taxonomy entries version additively and are never expired. Retention of generated packages is N/A because it belongs to workstreams 3 and 8 |
| Observability | TAX-33, TAX-57, TAX-59 — binding diagnostics, unresolved causes and rejection causes are carried on the records. Logging, metrics and tracing are N/A because AD-006 forbids I/O in the domain |
| External-dependency failure | N/A because TAX-02 gives the domain no external dependency to fail |
| State-transition integrity | TAX-54, TAX-55, TAX-56, TAX-58 — a confirmed relation is fixed at `confirmed`, candidates never cross into the confirmed set, and a new continuation never weakens an existing edge |

---

## User Stories

### P1: Isolated domain assembly ⭐ MVP

**User Story**: As an engine contributor, I want a dependency-free `Csharp2Md.Domain` assembly in the solution so that every later workstream can reference one taxonomy without inheriting Roslyn, MSBuild, JSON, filesystem or CLI concerns.

**Why P1**: Nothing else in this feature can be authored until the assembly exists, and AD-006 makes its isolation a structural invariant rather than a convention.

**Acceptance Criteria**:
1. The `Csharp2Md.Domain` project SHALL target `net10.0`. (TAX-01)
2. The `Csharp2Md.Domain` project SHALL declare no package reference and no project reference beyond the .NET base class library. (TAX-02)
3. IF any public or internal type in `Csharp2Md.Domain` exposes a type from `Microsoft.CodeAnalysis`, `Microsoft.Build`, `System.Text.Json` or `System.IO` THEN the dependency test SHALL fail naming the offending type. (TAX-03)
4. WHEN `csharp2md.slnx` is built with `TreatWarningsAsErrors` enabled THEN the build SHALL succeed while legacy `Csharp2Md.Core` remains present in the solution. (TAX-04)
5. The `Csharp2Md.Domain` project SHALL be listed in `csharp2md.slnx` under the `src` folder. (TAX-05)
6. The `Csharp2Md.Domain` project SHALL hold no reference to legacy `Csharp2Md.Core`, and legacy `Csharp2Md.Core` SHALL hold no reference to it. (TAX-06)

**Independent Test**: Build the solution and run a dependency test that asserts the domain assembly's referenced-assembly set and public surface contain no forbidden namespace.

---

### P1: Fact families and typed identities ⭐ MVP

**User Story**: As a classifier author, I want five closed fact families with exactly the documented public types so that roles like controller or repository cannot be smuggled in as new top-level fact types.

**Why P1**: AD-001 and the taxonomy principles depend on the family boundary; an open family set would let workstreams 5A–5D each invent facts.

**Acceptance Criteria**:
1. The domain SHALL define exactly five fact families named `StructuralFact`, `ArchitectureFact`, `ContractFact`, `PersistenceFact` and `ConfigurationFact`. (TAX-07)
2. The domain SHALL define the `StructuralFact` family with exactly the types `Solution`, `Project`, `Document` and `Symbol`. (TAX-08)
3. The domain SHALL define the `ArchitectureFact` family with exactly the types `Component`, `DeploymentUnit`, `EntryPoint`, `BoundaryOperation` and `ExternalSystem`. (TAX-09)
4. The domain SHALL define the `ContractFact` family with exactly the types `Contract`, `ContractBinding` and `ContractRevision`. (TAX-10)
5. The domain SHALL define the `PersistenceFact` family with exactly the types `DataStore`, `DataObject`, `DataField` and `DataOperation`. (TAX-11)
6. The domain SHALL define the `ConfigurationFact` family with exactly the type `ConfigurationBinding`. (TAX-12)
7. The domain SHALL model controller, handler, repository, client and service as facet values on a symbol or an architecture fact and SHALL NOT define any of them as a fact type. (TAX-13)
8. The domain SHALL model callable as a facet of `Symbol` rather than as a separate fact type. (TAX-14)
9. The domain SHALL NOT define a business-rule fact type or any fact type whose payload is a functional explanation. (TAX-15)
10. The domain SHALL define a contract only for a proven boundary payload, and a CLR type alone SHALL NOT constitute a contract. (TAX-16)
11. The domain SHALL require a proven protocol or schema key before two owners share one contract identity, and structural or name similarity SHALL NOT merge contracts across owners. (TAX-17)

**Independent Test**: Assert set equality between each family's registered type set and the documented set, and assert that no fact type name matches a facet-only role.

---

### P1: Closed facet vocabularies ⭐ MVP

**User Story**: As a classifier author, I want facets modelled as independent closed axes so that structure, protocol, lifecycle and direction never collapse into one ambiguous `kind`.

**Why P1**: The flat-kind collapse is the specific legacy defect AD-001 exists to correct.

**Acceptance Criteria**:
1. The domain SHALL define the boundary-operation protocol axis with exactly the values `http`, `grpc`, `messaging`, `cli`, `scheduler` and `function`. (TAX-18)
2. The domain SHALL define the boundary-operation direction axis with exactly the values `inbound` and `outbound`. (TAX-19)
3. The domain SHALL define the boundary-operation role axis with exactly the values `command`, `query`, `event`, `stream` and `lifecycle`. (TAX-20)
4. The domain SHALL expose protocol, direction and role as three independently valued axes rather than as one composite kind. (TAX-21)
5. The domain SHALL define `DataStore.technology` with exactly the values `relational`, `document`, `key-value`, `cache` and `unknown`. (TAX-22)
6. The domain SHALL define `DataObject.form` with exactly the values `table`, `view`, `collection`, `key-space`, `cache-region` and `unknown`. (TAX-23)
7. The domain SHALL define `DataOperation.operation` with exactly the values `read`, `insert`, `update`, `delete`, `execute` and `unknown`. (TAX-24)
8. IF a facet value outside a closed axis is supplied THEN the domain SHALL reject the construction naming the axis and the rejected value. (TAX-25)
9. WHEN a facet axis is given the value `unknown` THEN the domain SHALL treat it as a registered value rather than as a missing value. (TAX-26)
10. The domain SHALL define `EntryPoint` and `BoundaryOperation` as distinct types where neither is derivable from the other. (TAX-27)
11. WHEN one callable both starts an execution and crosses a boundary THEN the domain SHALL permit it to participate in an entry-point fact and a boundary-operation fact at the same time. (TAX-28)
12. The domain SHALL derive outbound HTTP boundary-operation identity from the owning component, direction, protocol, destination scope, HTTP method and normalized route. (TAX-29)
13. The domain SHALL derive inbound boundary-operation identity from the owning component and the protocol operation key. (TAX-30)
14. The domain SHALL close the CLR-to-physical mapping states to explicit confirmation, conventional candidate and unresolved. (TAX-31)

**Independent Test**: For every closed axis, assert set equality against the documented values and assert that one out-of-vocabulary value is rejected with the axis and value named.

---

### P1: Observation contract ⭐ MVP

**User Story**: As an extractor author, I want the observation kinds, required metadata and identity rule fixed in the domain so that observations stay immutable evidence and never leak Roslyn types across the extractor interface.

**Why P1**: AD-004 makes observations the only input to promotion; workstream 4 cannot start against an undefined observation.

**Acceptance Criteria**:
1. The domain SHALL define exactly the observation kinds `Invocation`, `ObjectCreation`, `TypeUsage`, `BaseType`, `AttributeUsage`, `Assignment`, `Configuration`, `RouteDeclaration`, `MessageOperation` and `DataAccess`. (TAX-32)
2. The domain SHALL mark `Invocation`, `ObjectCreation`, `TypeUsage`, `BaseType` and `AttributeUsage` as always emitted when bindable. (TAX-33)
3. The domain SHALL mark `Assignment`, `Configuration`, `RouteDeclaration`, `MessageOperation` and `DataAccess` as emitted only in a registered context. (TAX-34)
4. The domain SHALL derive observation identity from the owner, the observation kind, the normalized semantic payload and the structural occurrence ordinal only. (TAX-35)
5. WHEN two observations agree on owner, kind, normalized payload and ordinal but differ on evidence locator THEN the domain SHALL report one identity for both. (TAX-36)
6. WHEN two observations agree on owner, kind and normalized payload but differ on structural occurrence ordinal THEN the domain SHALL report distinct identities. (TAX-37)
7. The domain SHALL require every observation to carry an owner, a kind, a normalized payload, an evidence locator, an extraction method, a binding diagnostic, a document hash and an extractor version. (TAX-38)
8. IF an observation is constructed without a document hash or without an extractor version THEN the domain SHALL reject it naming the missing component. (TAX-39)
9. The domain SHALL NOT expose any Roslyn syntax, symbol or compilation type on the observation contract. (TAX-40)
10. The domain SHALL treat an observation as immutable once constructed, offering no mutating member. (TAX-41)

**Independent Test**: Assert the observation-kind set equality and its emission tiers, then assert the identity rule by constructing pairs that differ only by locator and only by ordinal.

---

### P1: Relation matrix enforced at construction ⭐ MVP

**User Story**: As a classifier author, I want the twelve canonical relations to reject unregistered source-target combinations at construction so that no broad or ambiguous edge can reach the confirmed graph.

**Why P1**: The legacy generic relation model is the defect this contract replaces; enforcement is the confirmed decision, not documentation.

**Acceptance Criteria**:
1. The domain SHALL define exactly twelve canonical relations named `contains`, `belongs-to`, `included-in`, `executes`, `invokes`, `implements-operation`, `targets`, `uses-contract`, `accesses-data`, `operates-on`, `maps-to` and `configured-by`. (TAX-42)
2. The domain SHALL NOT define a generic `references` relation or a generic `depends-on` relation. (TAX-43)
3. WHEN a relation is constructed THEN the domain SHALL validate the source fact type, the relation and the target fact type against the registered matrix. (TAX-44)
4. IF a source-relation-target triple is absent from the registered matrix THEN the domain SHALL reject the construction naming all three. (TAX-45)
5. WHEN a relation shape requires a callable THEN the domain SHALL accept only a `Symbol` carrying the callable facet. (TAX-46)
6. The domain SHALL define each relation in exactly one canonical direction and SHALL NOT define an inverse relation type. (TAX-47)
7. The domain SHALL close the `maps-to` mapping role to `contract-implementation`, `data-object-mapping`, `data-field-mapping` and `serialization-binding`. (TAX-48)
8. IF a `uses-contract` relation is constructed without a registered payload role THEN the domain SHALL reject it. (TAX-49)
9. WHEN a `targets` relation is constructed THEN the domain SHALL accept only an inbound boundary operation, a deployment unit or an external system as its target. (TAX-50)
10. WHEN an `operates-on` relation is constructed THEN the domain SHALL accept only a data object or a data field as its target. (TAX-51)
11. The registry SHALL declare a minimum accepted evidence method for every relation. (TAX-52)
12. IF a promotion supplies syntactic evidence where the registry requires semantic evidence THEN the domain SHALL reject the promotion. (TAX-53)
13. The domain SHALL NOT define a name-similarity, prefix-similarity or path-similarity evidence method. (TAX-54)

**Independent Test**: For each of the twelve relations, assert one registered triple is accepted and one unregistered triple is rejected with source, relation and target named.

---

### P1: Proof states and confirmed-relation invariants ⭐ MVP

**User Story**: As a downstream consumer, I want proof state expressed as three independent axes with no numeric confidence so that I can tell how something was observed, whether its identity closed, and whether continuations remain open.

**Why P1**: AD-010 rejects numeric confidence outright; the confirmed graph's integrity rests on these invariants.

**Acceptance Criteria**:
1. The domain SHALL define `evidence_method` with exactly the values `semantic`, `syntactic` and `configured`. (TAX-55)
2. The domain SHALL define `resolution` with exactly the values `confirmed`, `candidate` and `unresolved`. (TAX-56)
3. The domain SHALL define `frontier` with exactly the values `closed` and `open`. (TAX-57)
4. The domain SHALL expose evidence method, resolution and frontier as three independently valued axes. (TAX-58)
5. The domain SHALL NOT define a numeric confidence field on any fact, observation, relation or candidate. (TAX-59)
6. WHEN a confirmed relation is constructed THEN the domain SHALL require a typed source, a typed target, registered facets, a `derived_from` evidence chain, a classifier identifier, a classifier version and at least one analysis variant. (TAX-60)
7. The domain SHALL fix `resolution` to `confirmed` for every confirmed relation. (TAX-61)
8. IF a record whose resolution is `candidate` or `unresolved` is added to the confirmed relation set THEN the domain SHALL reject it. (TAX-62)
9. IF a confirmed relation is constructed with an absent target identity THEN the domain SHALL reject it. (TAX-63)
10. IF a resolution attempt cannot close an identity THEN the domain SHALL represent the outcome as an unresolved record carrying its cause and its available evidence. (TAX-64)
11. WHEN a further continuation exists at an occurrence that already carries a confirmed relation THEN the domain SHALL record a separate open frontier on that occurrence and SHALL leave the confirmed relation unchanged. (TAX-65)
12. The domain SHALL require every rejected candidate recorded by a promotion to carry its rejection cause. (TAX-66)
13. The domain SHALL require every promotion record to declare its required observations, accepted evidence methods, negative conditions, produced facts, produced relations, produced facets, classifier identifier and classifier version. (TAX-67)

**Independent Test**: Construct a confirmed relation missing each required component in turn and assert rejection; assert by reflection that no public member on the fact, observation, relation or candidate surface is a numeric confidence.

---

### P1: Identity grammar and determinism ⭐ MVP

**User Story**: As a consumer comparing two runs, I want identities that exclude absolute paths, locations, labels and timestamps so that moving a clone or reordering inputs never changes an ID.

**Why P1**: Determinism independent of clone path and input order is a standing engineering constraint, and identity collisions are a zero-tolerance gate.

**Acceptance Criteria**:
1. The domain SHALL exclude absolute paths from every identity. (TAX-68)
2. The domain SHALL exclude source locations, translated labels and timestamps from every identity. (TAX-69)
3. WHEN the same inputs are analyzed from two different absolute clone paths THEN the domain SHALL produce identical identities. (TAX-70)
4. WHEN the same inputs are supplied in two different orders THEN the domain SHALL produce identical identities. (TAX-71)
5. The domain SHALL scope globally composable identities by workspace identity. (TAX-72)
6. The domain SHALL derive solution and project identities from logical relative paths. (TAX-73)
7. WHEN a project moves to a different logical relative path THEN the domain SHALL produce a different default project identity. (TAX-74)
8. WHERE an explicit logical key is supplied for a project THEN the domain SHALL preserve its identity across a change of logical relative path. (TAX-75)
9. IF a required identity component is missing THEN the domain SHALL fail naming the missing component rather than emitting a partial identity. (TAX-76)
10. IF two distinct facts resolve to one identity string THEN the domain SHALL fail naming both facts. (TAX-77)
11. The domain SHALL qualify every semantic fact identity by its solution and its analysis variant. (TAX-78)

**Independent Test**: Derive identities twice from synthetic inputs under a simulated clone-path change and a shuffled input order, and assert byte equality; then assert a collision and a missing component each fail with the expected names.

---

### P1: Literal allowlist and secret exclusion ⭐ MVP

**User Story**: As a security reviewer, I want the taxonomy itself to bound which literals may enter facts and observations so that credentials cannot reach factual output through a payload field.

**Why P1**: "No secret duplicated into facts, observations, indexes or diagnostics" is a zero-tolerance gate, and a contract added later cannot retroactively clean emitted payloads.

**Acceptance Criteria**:
1. The domain SHALL restrict fact and observation literal values to a registered structural allowlist covering routes, protocol names, channels, schema names, table names, field names, configuration keys and client names. (TAX-79)
2. IF a literal outside the allowlist is supplied to a fact or observation payload THEN the domain SHALL reject the construction naming the field. (TAX-80)
3. The domain SHALL model suspected-secret evidence with a document identity, a span, a document hash and a redacted excerpt. (TAX-81)
4. The domain SHALL NOT define a field that carries an individual secret value or a hash of an individual secret value. (TAX-82)

**Independent Test**: Supply an out-of-allowlist literal and assert a named rejection; assert by reflection that the suspected-secret evidence type exposes no original-literal and no secret-hash member.

---

### P1: Version axes and registry drift gate ⭐ MVP

**User Story**: As a maintainer changing the taxonomy, I want five independent version axes and a committed registry guarded by a drift gate so that code and published contract can never disagree.

**Why P1**: The registry is the artifact every later workstream validates against; without the gate it silently rots.

**Acceptance Criteria**:
1. The domain SHALL expose five independent version axes named `schema_version`, `taxonomy_version`, `observation_schema_version`, `extractor_set_version` and `classifier_set_version`. (TAX-83)
2. The registry SHALL enumerate every fact family, fact type, observation kind, facet axis with its closed values, relation with its registered triples and minimum evidence method, mapping role, proof-state axis and version axis. (TAX-84)
3. WHEN the registry emitter runs twice against an unchanged domain THEN it SHALL produce byte-identical output. (TAX-85)
4. IF the committed registry file differs from the emitter output THEN the drift gate test SHALL fail naming the differing entries. (TAX-86)
5. The registry emitter SHALL live outside `Csharp2Md.Domain`. (TAX-87)
6. WHEN a taxonomy change only adds entries THEN the domain SHALL keep every previously valid identity valid. (TAX-88)
7. IF a change alters the meaning of an existing identity or removes a value from a closed axis THEN the taxonomy SHALL require a new identity namespace or grammar instead of an additive version increment. (TAX-89)
8. IF one relation triple is registered twice THEN the registry emitter SHALL fail naming the duplicate. (TAX-90)
9. The domain SHALL expose every taxonomy type as an immutable value with no mutating member. (TAX-91)

**Independent Test**: Run the emitter twice and compare bytes, hand-edit the committed registry and assert the drift gate fails with the differing entry named.

---

## Edge Cases

- IF a closed axis receives an out-of-vocabulary value THEN the domain SHALL reject the construction naming the axis and the value (TAX-25).
- IF an observation lacks a document hash or extractor version THEN the domain SHALL reject it naming the missing component (TAX-39).
- IF a relation triple is unregistered THEN the domain SHALL reject it naming source, relation and target (TAX-45).
- IF a confirmed relation has no target identity THEN the domain SHALL reject it rather than emit a dangling edge (TAX-63).
- IF two distinct facts collide on one identity string THEN the domain SHALL fail naming both (TAX-77).
- WHEN a facet axis value is `unknown` THEN the domain SHALL treat it as registered, not as absent (TAX-26).
- WHEN a project moves and carries an explicit logical key THEN the domain SHALL preserve its identity (TAX-75).
- IF a duplicate relation triple is registered THEN the emitter SHALL fail naming the duplicate (TAX-90).
- IF the committed registry is hand-edited THEN the drift gate SHALL fail (TAX-86).

---

## Requirement Traceability

Each requirement gets a unique ID for tracking across design, tasks, and validation.

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| TAX-01 | P1: Isolated domain assembly | Tasks | Verified |
| TAX-02 | P1: Isolated domain assembly | Tasks | Verified |
| TAX-03 | P1: Isolated domain assembly | Tasks | Verified |
| TAX-04 | P1: Isolated domain assembly | Tasks | Verified |
| TAX-05 | P1: Isolated domain assembly | Tasks | Verified |
| TAX-06 | P1: Isolated domain assembly | Tasks | Verified |
| TAX-07 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-08 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-09 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-10 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-11 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-12 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-13 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-14 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-15 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-16 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-17 | P1: Fact families and typed identities | Tasks | Verified |
| TAX-18 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-19 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-20 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-21 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-22 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-23 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-24 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-25 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-26 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-27 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-28 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-29 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-30 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-31 | P1: Closed facet vocabularies | Tasks | Verified |
| TAX-32 | P1: Observation contract | Tasks | Verified |
| TAX-33 | P1: Observation contract | Tasks | Verified |
| TAX-34 | P1: Observation contract | Tasks | Verified |
| TAX-35 | P1: Observation contract | Tasks | Verified |
| TAX-36 | P1: Observation contract | Tasks | Verified |
| TAX-37 | P1: Observation contract | Tasks | Verified |
| TAX-38 | P1: Observation contract | Tasks | Verified |
| TAX-39 | P1: Observation contract | Tasks | Verified |
| TAX-40 | P1: Observation contract | Tasks | Verified |
| TAX-41 | P1: Observation contract | Tasks | Verified |
| TAX-42 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-43 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-44 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-45 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-46 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-47 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-48 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-49 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-50 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-51 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-52 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-53 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-54 | P1: Relation matrix enforced at construction | Tasks | Verified |
| TAX-55 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-56 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-57 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-58 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-59 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-60 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-61 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-62 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-63 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-64 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-65 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-66 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-67 | P1: Proof states and confirmed-relation invariants | Tasks | Verified |
| TAX-68 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-69 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-70 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-71 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-72 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-73 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-74 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-75 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-76 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-77 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-78 | P1: Identity grammar and determinism | Tasks | Verified |
| TAX-79 | P1: Literal allowlist and secret exclusion | Tasks | Verified |
| TAX-80 | P1: Literal allowlist and secret exclusion | Tasks | Verified |
| TAX-81 | P1: Literal allowlist and secret exclusion | Tasks | Verified |
| TAX-82 | P1: Literal allowlist and secret exclusion | Tasks | Verified |
| TAX-83 | P1: Version axes and registry drift gate | Tasks | Verified |
| TAX-84 | P1: Version axes and registry drift gate | Tasks | Verified |
| TAX-85 | P1: Version axes and registry drift gate | Tasks | Verified |
| TAX-86 | P1: Version axes and registry drift gate | Tasks | Verified |
| TAX-87 | P1: Version axes and registry drift gate | Tasks | Verified |
| TAX-88 | P1: Version axes and registry drift gate | Tasks | Verified |
| TAX-89 | P1: Version axes and registry drift gate | Tasks | Verified |
| TAX-90 | P1: Version axes and registry drift gate | Tasks | Verified |
| TAX-91 | P1: Version axes and registry drift gate | Tasks | Verified |

**ID format:** `TAX-[NUMBER]`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 91 total, 91 mapped to tasks, 91 covered by tests, 0 unmapped

---

## Success Criteria

- [ ] `dotnet build csharp2md.slnx` succeeds with `TreatWarningsAsErrors` while legacy `Csharp2Md.Core` is still present.
- [ ] Every closed vocabulary in the taxonomy has a set-equality test against its documented values plus a rejection test for one out-of-vocabulary value.
- [ ] Every one of the twelve relations has at least one accepted registered triple and one rejected unregistered triple.
- [ ] Identity derivation is byte-identical across a simulated clone-path change and a shuffled input order.
- [ ] `contracts/taxonomy-registry.json` is committed, reproduced byte-identically by the emitter, and a hand edit fails the drift gate.
- [ ] A reflection test proves the public taxonomy surface exposes no numeric confidence member, no Roslyn or JSON or filesystem type, and no secret-carrying member.
- [ ] Every requirement ID TAX-01 through TAX-91 has at least one test asserting a spec-defined outcome.
