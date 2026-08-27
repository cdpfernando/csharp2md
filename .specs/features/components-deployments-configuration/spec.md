# Components, Deployments and Configuration Specification

## Problem Statement

Workstreams 5A, 5B and 5C filled the architecture, contract and persistence fact families, but three registry slots still have no producer at all: `DeploymentUnit` and `ConfigurationBinding` facts, and the `belongs-to`, `included-in` and `configured-by` relations. The wire layer already maps all three — `WireFactMapping.ToDto(DeploymentUnit)` and `ToDto(ConfigurationBinding)` exist, `PackagePublisher` writes a `ConfigurationFactsShard` — but `DomainMapper` never receives one because no classifier mints them. An LLM reading the package cannot answer "which deployable does this code ship in", "which components are shared between deployables" or "which configuration key supplies this store's address".

`Component` has a producer, but it is a placeholder. `ComponentPass` mints one component per project that happens to carry one of four candidate observation kinds, named by the project's logical path. That is a per-project label, not the "agrupamento lógico de código com responsabilidade técnica coesa, formado a partir de evidência de deploy, uso privado, compartilhamento ou configuração explícita" that `CONTEXT.md` defines. Nothing in the package says which projects deploy together.

The evidence for all of it is missing from the ledger. MSBuild's `OutputType` and each project's `ProjectReference` set live only on the Roslyn `Project` objects the semantic stage holds, and AD-004 forbids a classifier from re-entering Roslyn. `appsettings.json` is inventoried as a `Document` and immediately written off with an `unsupported-document` diagnostic, so `ConnectionStrings:OrdersDb` and `Services:PaymentService` never reach a classifier — which is why 5A can only leave `targets` as a candidate against a client name it cannot resolve. Closing both evidence gaps is part of this workstream.

## Goals

- [ ] Publish MSBuild project metadata — output kind and in-solution project references — into the immutable ledger as `Configuration` observations owned by `Project` facts, located in the `.csproj` document.
- [ ] Read `appsettings*.json` through a new configuration document adapter, emitting one key-path `Configuration` observation per leaf value, never the value itself.
- [ ] Replace `ComponentPass`'s per-project label with the evidence-based grouping rule: deployable, private use, sharing.
- [ ] Mint one `DeploymentUnit` per application project and emit confirmed `included-in` relations from every component that ships inside it.
- [ ] Emit confirmed `belongs-to` relations from every evidence-backed symbol to its component.
- [ ] Mint `ConfigurationBinding` facts from declared configuration keys and emit `configured-by` relations across all four registered triples — `Component`, `Symbol`, `DataStore` and `BoundaryOperation`.
- [ ] Promote 5A's candidate `targets` links to confirmed relations when configuration proves the destination address, keep the candidate and record an open frontier when the address is an environment-variable indirection, and leave it untouched when nothing matches.
- [ ] Record suspected-secret evidence for credential-bearing configuration values without copying the value into any fact, observation, relation or diagnostic.
- [ ] Extend `fixtures/SyntheticSolution` with one new deployable project so the grouping rule has deployable, shared and private-use coverage inside versioned fixtures.
- [ ] Publish component and configuration run-coverage counts into the snapshot diagnostics envelope.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Operator override files that associate identities or supply logical keys | User decision: "secure overrides" in the roadmap row is read as configuration published securely, not as an override input surface. An override file needs its own CLI option, schema and contradiction rules |
| Dockerfile, Docker Compose and Kubernetes manifests | User decision: deployment evidence comes from MSBuild `OutputType`. A YAML adapter needs a new dependency and new fixture files, and adds no fact type this workstream does not already produce |
| `.env`, `web.config`, `app.config`, user secrets, environment variables at analysis time | Same adapter cost with no fixture coverage; `appsettings*.json` is the .NET configuration source the architecture names first |
| Resolving an environment-variable indirection to its runtime value | The generator analyzes statically and never reads the operator's environment. The indirection is published as an open frontier, which is what it is |
| DI lifetime facets (`singleton`, `scoped`, `transient`) | Not representable in the registry: lifetime is not a declared facet axis, and workstream 1 is closed |
| Promoting DI registration into confirmed `invokes` or interface-to-implementation binding | Owned by workstream 5B, which already publishes `invokes` candidates for abstract targets |
| `ExternalSystem` facts not already minted by 5A | 5D confirms or refuses an existing candidate; minting a new external identity from configuration alone would promote a name into a destination |
| Grouping components by namespace, assembly name, folder or name prefix | The taxonomy forbids promotion from name, prefix or path similarity |
| `maps-to` with `contract-implementation` or `serialization-binding` roles | Owned by workstream 6 |
| Catalogs, postings, Markdown or component/deployment projections | Owned by workstream 6 |
| Cross-solution component or deployment correlation | Owned by workstream 7; every fact here stays scoped to one solution (AD-008) |
| The `run_certification` document and coverage gate thresholds | Owned by workstream 8; 5D publishes raw counts only |
| Changes to Domain descriptor tables or `contracts/taxonomy-registry.json` bytes | Workstream 1 is closed; every fact, relation, facet and observation kind this workstream needs is already registered |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here. Nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Component formation rule | Evidence-based grouping replaces the per-project label: an application project is its own component; a library reached by exactly one application is grouped into that application's component; a library reached by two or more applications is its own shared component; a library reached by none is its own component with no deployment unit | User decision. Matches `CONTEXT.md`'s deploy / private-use / sharing evidence, and is the only rule under which `included-in` carries information | y |
| Deployment evidence source | Roslyn `OutputKind` derived from MSBuild `OutputType`, plus `appsettings*.json` for configuration. No container or orchestrator files | User decision | y |
| Fixture extension | One new project, `Acme.Orders.Worker`, added as a second application to `Acme.Orders.slnx`; `Acme.Orders` and `Acme.Payments` become applications | User decision ("add a new acme project to ensure the tests"). One project is sufficient for full coverage: in `Acme.Orders.slnx` two applications make `Acme.Shared.Contracts` shared, and in `Acme.Payments.slnx` the same project is privately used by the single application — which also proves AD-008 isolation | y |
| "Secure overrides" | Secret handling only: allowlisted structural keys in facts, suspected-secret evidence with a redacted excerpt for credential-bearing values, no override input surface | User decision | y |
| `targets` promotion | A candidate `targets` link is promoted to a confirmed relation when configuration proves a literal address for its client name, and the superseded candidate is removed; an environment-variable indirection keeps the candidate and adds an open frontier; no match leaves the candidate untouched | User decision. `targets` declares `configured` as its minimum evidence method precisely for this | y |
| How MSBuild metadata reaches classifiers | As `Configuration` observations owned by the `Project` fact, located in that project's `.csproj` document, with `evidence_method = configured` | A `.csproj` is configuration and `Configuration` is a registered observation kind emitted "only in a registered context". Putting it in the ledger — instead of a side channel on `PipelineContext` — gives `included-in` and the component rule a real evidence chain, which `EvidenceChain.Create` requires and a `PipelineContext` property cannot provide | n |
| Component naming grammar | `Component.Name` is always some project's logical path: the application's path for a deployable or private-use grouping, the library's own path for a shared or unreached library | Keeps the existing identity grammar, so the `Acme.Orders` component id is unchanged and 5A/5B/5C assertions against it keep holding. Only grouping changes | n |
| `DeploymentUnit` naming | The application project's logical path | Same grammar as `Component`; the `deployment-unit:` and `component:` id prefixes keep the two identities distinct without inventing an assembly-name axis | n |
| Component membership evidence | A symbol is an owner of its component, and gets a `belongs-to` relation, only when it owns at least one observation of any kind | `EvidenceChain.Create` throws on an empty chain, so an observation-free symbol has nothing to derive `belongs-to` from. Widening from today's four candidate kinds to any kind is strictly more evidence, not less | n |
| Observation-free symbols | Recorded by the structural `contains` relation only; no `belongs-to`, no `UnresolvedRecord` | One unresolved record per unobserved symbol would flood the package with a fact the structural family already states | n |
| Configuration key grammar | The colon-joined JSON key path, matching .NET's own configuration key convention (`ConnectionStrings:OrdersDb`, `Services:PaymentService`) | It is the key an operator and `IConfiguration` both use, and it survives `FactIdGrammar.RequireCanonicalText` | n |
| Configuration values in the ledger | Never carried on the observation. A leaf value contributes only a `resolution` axis (`literal`, `dynamic`, `unknown`) and, when it is a well-formed absolute URI and not a suspected secret, an `address` entry | The security rules allowlist structural values; an arbitrary configuration value is not one. An absolute URI is the one shape `targets` promotion needs, and it is a client address, not a credential | n |
| Multiple `appsettings*.json` files | Each file is read independently and each contributes its own observations; keys are never merged or overridden across files | Override precedence is a runtime host behaviour that depends on the environment name; asserting a winner statically would invent it. Two files declaring one key produce two observations and one `ConfigurationBinding` | n |
| `ConfigurationBinding` bound fact | The `Component` that owns the document's project, so one binding exists per (component, key) pair | `ConfigurationBinding.Create` takes a single bound fact and derives the id from it. The component is the narrowest fact that owns the file and is stable across the grouping rule | n |
| Client-name matching | Exact ordinal comparison between the boundary operation's client name and the last segment of a `Services:*` key | Anything looser is name similarity, which the taxonomy forbids as a promotion basis | n |
| Pass registration | `ComponentPass` keeps its first position and grows to emit `Component`, `DeploymentUnit`, `belongs-to` and `included-in`; one new `ConfigurationPass` is appended after `PersistencePass` and before `RelationPass` | Components must exist before every later pass that reads them; `configured-by` on a `DataStore` needs 5C's output, and `targets` promotion needs 5A's candidates | n |
| Superseded candidate removal | `SnapshotAccumulator` gains a `RemoveCandidate` method used only when a candidate is promoted to a confirmed relation | Leaving both records would publish a candidate and a confirmed edge for the same link, which contradicts "candidates never enter the confirmed graph" | n |
| Classifier identities | `csharp2md.classifier.component-topology` and `csharp2md.classifier.configuration`, both version 1 | Matches the `csharp2md.classifier.{area}` pattern 5A established; the two rule sets version independently | n |
| Coverage publication | Component and configuration coverage are published as `DiagnosticRecord`s in the snapshot diagnostics envelope with numerator, denominator and each unresolved occurrence's owner id, never a percentage | AD-017 makes the diagnostics envelope the pipeline's operational channel; the certification document is workstream 8's | n |

**Open questions:** none — all resolved or logged above.

---

## Implicit-requirement dimensions sweep

Large scope, so every dimension resolves to a requirement or an explicit exclusion.

| Dimension | Coverage |
| --- | --- |
| Input validation and bounds | CDC-26, CDC-31, CDC-33, CDC-34 — the JSON adapter accepts object/array/leaf shapes and rejects everything else with a diagnostic; key paths and literals pass `FactIdGrammar` and `StructuralLiteral` construction guards before entering the ledger |
| Failure and partial-failure states | CDC-05, CDC-07, CDC-22, CDC-31, CDC-42, CDC-45 — an uncompilable project, an out-of-solution reference, an unreached component, a malformed document, an unbacked key and an unmatched client each yield a diagnostic or an unresolved record, never a confirmed edge with an invented target |
| Idempotency, retry, duplicate handling | CDC-04, CDC-51, CDC-52 — re-running the classifier over the same ledger produces identical facts, relations, candidates, frontiers and unresolved records; a project reference analyzed under several target frameworks yields one observation |
| Auth boundaries and rate limits | N/A because the engine is a local in-process tool with no network surface and reads no remote configuration |
| Concurrency and ordering | CDC-06, CDC-33, CDC-51 — observation ordinals derive from ordinal-sorted key paths and reference paths, so MSBuild's and the filesystem's return order cannot affect identity or emitted bytes |
| Data lifecycle and expiry | N/A because generated packages are operator-owned files |
| Observability | CDC-56, CDC-57, CDC-58 — component grouping counts, configuration key counts and every unresolved `included-in` / `configured-by` / `targets` occurrence reach the diagnostics envelope |
| External-dependency failure | CDC-34 — the adapter reads only inventoried documents inside the authorized root and never follows a path that escapes it |
| State-transition integrity | CDC-43, CDC-46, CDC-49, CDC-53 — a candidate is removed only in the same pass that emits its confirmed replacement, promotion never mints a new `ExternalSystem`, and the taxonomy registry bytes are unchanged |

---

## User Stories

### P1: Project metadata reaches the ledger ⭐ MVP

**User Story**: As a component classifier, I want each project's output kind and in-solution references in the immutable ledger so that I can group components and mint deployment units without re-reading Roslyn.

**Why P1**: `OutputType` and `ProjectReference` live only on Roslyn `Project` objects. Without them in the ledger there is no deployment evidence, no grouping rule and no evidence chain for `included-in`.

**Acceptance Criteria**:

1. WHEN the pipeline compiles a project THEN it SHALL emit one `Configuration` observation owned by that project's `Project` fact, located in that project's `.csproj` document, carrying an `output-kind` payload entry whose value is `application` or `library`. (CDC-01)
2. WHEN a compiled project declares a direct project reference to another project analyzed in the same solution THEN the pipeline SHALL emit one `Configuration` observation owned by the referencing `Project` fact carrying a `project-reference` payload entry holding the referenced project's logical path. (CDC-02)
3. Every project-metadata observation SHALL carry `evidence_method = configured`. (CDC-03)
4. WHEN a project is compiled under more than one target framework or analysis variant THEN each distinct output-kind and project-reference pair SHALL still yield exactly one observation. (CDC-04)
5. WHEN a project listed in the solution produces no compilation THEN no project-metadata observation SHALL be emitted for it, and the existing missing-project and compile-failure diagnostics SHALL be unchanged. (CDC-05)
6. The occurrence ordinals of a project's `project-reference` observations SHALL derive from the ordinal-sorted referenced logical paths, independently of the order MSBuild returns them. (CDC-06)
7. WHEN a compiled project references a project that is not analyzed in this solution THEN no `project-reference` observation SHALL be emitted for it and the pipeline SHALL record a diagnostic naming the referencing project and the unanalyzed reference. (CDC-07)
8. No type under `Csharp2Md.Analysis.Classification` SHALL reference `Microsoft.CodeAnalysis` to obtain project topology. (CDC-08)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert the `Project` fact for `Acme.Orders/Acme.Orders.csproj` owns a `Configuration` observation with `output-kind=application` and one with `project-reference=Acme.Shared.Contracts/Acme.Shared.Contracts.csproj`; assert `Acme.Shared.Contracts` owns an `output-kind=library` observation and no `project-reference` observation; assert `Acme.Broken` owns no project-metadata observation.

---

### P1: Evidence-based component formation ⭐ MVP

**User Story**: As an LLM, I want components that group code by how it actually ships so that "which components exist" is an architectural answer rather than a restatement of the project list.

**Why P1**: The current pass labels projects. Every later relation that names a component — `belongs-to`, `included-in`, `configured-by`, boundary ownership — inherits that label's meaning.

**Acceptance Criteria**:

1. WHEN a project's `output-kind` observation holds `application` THEN the classifier SHALL create exactly one `Component` named by that project's logical path. (CDC-09)
2. WHEN a library project is reached through `project-reference` observations by exactly one application project THEN the classifier SHALL group that library's symbols into that application's `Component` and SHALL NOT create a separate `Component` for it. (CDC-10)
3. WHEN a library project is reached by two or more application projects THEN the classifier SHALL create exactly one `Component` named by that library's own logical path. (CDC-11)
4. WHEN a library project is reached by no application project THEN the classifier SHALL create exactly one `Component` named by that library's own logical path. (CDC-12)
5. Reachability SHALL be transitive across `project-reference` observations. (CDC-13)
6. A `Component`'s `Owners` SHALL be exactly the `Symbol` facts that own at least one observation and whose owning project is grouped into that component, ordered by fact id using ordinal comparison. (CDC-14)
7. WHEN a symbol is an owner of a component THEN the classifier SHALL emit one confirmed `belongs-to` relation from that `Symbol` to that `Component` with `evidence_method = semantic`, derived from that symbol's own observations. (CDC-15)
8. WHEN a symbol owns no observation THEN it SHALL NOT appear in any `Component.Owners` and SHALL NOT produce a `belongs-to` relation. (CDC-16)
9. Each `Symbol` SHALL be the source of at most one `belongs-to` relation. (CDC-17)
10. WHERE the ledger holds no project-metadata observation, the classifier SHALL emit no `Component`, `DeploymentUnit`, `belongs-to` or `included-in` record and SHALL NOT throw. (CDC-18)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert exactly three components exist — `Acme.Orders/Acme.Orders.csproj`, `Acme.Orders.Worker/Acme.Orders.Worker.csproj` and `Acme.Shared.Contracts/Acme.Shared.Contracts.csproj`. Analyze `Acme.Payments.slnx`; assert exactly one component, `Acme.Payments/Acme.Payments.csproj`, and that a symbol declared in `Acme.Shared.Contracts` has a `belongs-to` relation targeting it.

---

### P1: Deployment units and inclusion ⭐ MVP

**User Story**: As an LLM, I want to know which deployable each component ships in so that I can tell a shared library from a service's private code.

**Why P1**: `DeploymentUnit` and `included-in` have no producer. Without them the package cannot answer any deployment question.

**Acceptance Criteria**:

1. WHEN a project's `output-kind` observation holds `application` THEN the classifier SHALL create exactly one `DeploymentUnit` named by that project's logical path. (CDC-19)
2. WHEN a component's code is reached by an application project — as that application itself, as a private-use grouping, or through transitive references — THEN the classifier SHALL emit a confirmed `included-in` relation from that `Component` to that application's `DeploymentUnit` with `evidence_method = configured`, derived from the `output-kind` and `project-reference` observations that prove the reach. (CDC-20)
3. WHEN a shared component is reached by two or more applications THEN it SHALL be the source of one `included-in` relation per reaching application. (CDC-21)
4. WHEN a component is reached by no application project THEN the classifier SHALL emit an `UnresolvedRecord` for `included-in` with cause `insufficient evidence` and SHALL NOT emit a confirmed `included-in` relation for it. (CDC-22)
5. WHEN a solution contains no application project THEN no `DeploymentUnit` SHALL be created and every component SHALL produce the unresolved record of CDC-22. (CDC-23)
6. A `DeploymentUnit` SHALL be created only from an `output-kind` observation, never from a component, a folder, an assembly name or a name prefix. (CDC-24)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert exactly two deployment units exist; assert the `Acme.Shared.Contracts` component is the source of exactly two confirmed `included-in` relations, one per deployment unit; assert each application's own component is `included-in` its own deployment unit only.

---

### P1: Configuration documents reach the ledger ⭐ MVP

**User Story**: As a configuration classifier, I want `appsettings*.json` keys in the immutable ledger so that I can mint configuration facts without a classifier touching the filesystem.

**Why P1**: `appsettings.json` is currently written off as an unsupported document, so no configuration evidence exists outside C# call sites.

**Acceptance Criteria**:

1. WHEN an inventoried document's file name matches `appsettings*.json` and lies inside the authorized root THEN the pipeline SHALL treat it as a supported configuration document and SHALL NOT record an `unsupported-document` diagnostic for it. (CDC-25)
2. WHEN the configuration adapter reads a supported configuration document THEN for every leaf value it SHALL emit one `Configuration` observation owned by that `Document` fact carrying a `key` payload entry holding the colon-joined key path. (CDC-26)
3. A configuration observation SHALL NOT carry the leaf value, and SHALL NOT carry any entry derived from it other than those CDC-29 and CDC-30 permit. (CDC-27)
4. WHEN a leaf value is a suspected secret THEN the adapter SHALL record `SuspectedSecretEvidence` holding the document, span, hash and a redacted excerpt, and the value SHALL NOT appear in any fact, observation, relation, candidate, frontier or diagnostic. (CDC-28)
5. WHEN a leaf value is an environment-variable indirection of the form `${VAR}`, `$VAR` or `%VAR%` THEN the observation SHALL carry a `resolution` entry with value `dynamic`; WHEN it is any other non-empty value the entry SHALL be `literal`; WHEN it is empty or null the entry SHALL be `unknown`. (CDC-29)
6. WHEN a leaf value is a well-formed absolute URI and is not a suspected secret THEN the observation SHALL carry an `address` entry holding that URI; otherwise no `address` entry SHALL be present. (CDC-30)
7. WHEN a supported configuration document cannot be parsed THEN the adapter SHALL record a `malformed-configuration-document` diagnostic naming the document, SHALL emit no observation for it, and SHALL NOT abort the run. (CDC-31)
8. Every configuration document observation SHALL carry `evidence_method = configured` and the document's content hash. (CDC-32)
9. The occurrence ordinals of a document's configuration observations SHALL derive from the ordinal-sorted key paths, independently of the order the keys appear in the file. (CDC-33)
10. The adapter SHALL read only documents already present in the inventory and SHALL NOT resolve any path that escapes the authorized root. (CDC-34)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert `appsettings.json` produces no `unsupported-document` diagnostic; assert observations exist for `ConnectionStrings:OrdersDb`, `Services:PaymentService` and `Services:NotificationService`; assert the `PaymentService` observation carries `resolution=literal` and an `address` entry, the `NotificationService` observation carries `resolution=dynamic` and no `address` entry, and the `OrdersDb` observation carries neither an `address` entry nor the connection string.

---

### P1: Configuration bindings and configured-by ⭐ MVP

**User Story**: As an LLM, I want to see which configuration key supplies a component, a symbol, a data store or a boundary operation so that I can follow a value from code to its declared source.

**Why P1**: `ConfigurationBinding` and `configured-by` have no producer; the `ConfigurationFactsShard` is never written.

**Acceptance Criteria**:

1. WHEN a configuration document declares a key THEN the classifier SHALL create exactly one `ConfigurationBinding` bound to the `Component` grouping the document's owning project, carrying that key as a `ConfigurationKey` structural literal. (CDC-35)
2. WHEN a `ConfigurationBinding` is created THEN the classifier SHALL emit a confirmed `configured-by` relation from that `Component` to that binding with `evidence_method = configured`. (CDC-36)
3. WHEN a `Configuration` observation owned by a `Symbol` carries a key equal to a declared configuration key THEN the classifier SHALL emit a confirmed `configured-by` relation from that `Symbol` to that binding. (CDC-37)
4. WHEN a `DataStore` fact's name equals the last segment of a declared key under `ConnectionStrings` THEN the classifier SHALL emit a confirmed `configured-by` relation from that `DataStore` to that binding. (CDC-38)
5. WHEN an outbound `BoundaryOperation`'s client name equals the last segment of a declared key under `Services` THEN the classifier SHALL emit a confirmed `configured-by` relation from that operation to that binding. (CDC-39)
6. Every `configured-by` relation SHALL carry `evidence_method = configured` and an evidence chain containing the configuration observation that declared the key. (CDC-40)
7. Key matching in CDC-37, CDC-38 and CDC-39 SHALL be exact ordinal comparison; no `configured-by` relation SHALL be emitted from a prefix, suffix or case-insensitive match. (CDC-41)
8. WHEN a `Configuration` observation owned by a `Symbol` carries a key that no configuration document declares THEN the classifier SHALL emit an `UnresolvedRecord` for `configured-by` with cause `insufficient evidence` and SHALL NOT emit a confirmed relation. (CDC-42)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert a `ConfigurationBinding` exists for `ConnectionStrings:OrdersDb` and for `Services:PaymentService`; assert the `OrdersDb` `DataStore` 5C mints is the source of a confirmed `configured-by` relation to the `ConnectionStrings:OrdersDb` binding; assert the outbound HTTP operation whose client name is `PaymentService` is the source of a confirmed `configured-by` relation; assert `Program.ConfigureHost` is the source of a confirmed `configured-by` relation for the key it reads.

---

### P1: Client address resolution and targets promotion ⭐ MVP

**User Story**: As an LLM, I want an outbound call's destination confirmed when configuration proves its address, and honestly left open when it does not, so that I can distinguish a proven external dependency from a name.

**Why P1**: 5A can only publish a candidate, because the client name alone proves nothing. Configuration is the evidence `targets` was registered to accept.

**Acceptance Criteria**:

1. WHEN a candidate `targets` link joins an outbound `BoundaryOperation` to an `ExternalSystem` whose client name matches a declared `Services` key whose observation carries `resolution=literal` and an `address` entry THEN the classifier SHALL emit a confirmed `targets` relation with `evidence_method = configured` and SHALL remove the superseded candidate. (CDC-43)
2. WHEN the matching observation carries `resolution=dynamic` THEN the candidate SHALL remain, no confirmed `targets` relation SHALL be emitted, and the classifier SHALL record an `OpenFrontier` on the originating occurrence. (CDC-44)
3. WHEN no declared `Services` key matches the client name THEN the candidate SHALL remain unchanged, no confirmed `targets` relation SHALL be emitted, and no `OpenFrontier` SHALL be recorded. (CDC-45)
4. A confirmed `targets` relation SHALL carry an evidence chain containing both the C# observation that produced the candidate and the configuration observation that proved the address. (CDC-46)
5. The classifier SHALL NOT create an `ExternalSystem` fact; it SHALL only confirm, keep or refuse a link to one that already exists. (CDC-47)
6. No confirmed `targets` relation SHALL be emitted from a client name and a key matching by prefix, suffix, case-insensitive comparison or path similarity. (CDC-48)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert the `PaymentService` outbound operation is the source of a confirmed `targets` relation and that no candidate `targets` link remains for it; assert the `NotificationService` operation still has its candidate plus an open frontier; assert the `ShippingService` operation still has its candidate, no confirmed relation and no open frontier.

---

### P1: Determinism, security and invariants ⭐ MVP

**User Story**: As an operator, I want this workstream's output to be reproducible and free of credentials so that the package can be committed and diffed.

**Why P1**: This workstream is the first to read a file that contains a password.

**Acceptance Criteria**:

1. WHEN the same solution is analyzed from two different absolute clone paths THEN the emitted components, deployment units, configuration bindings, relations, candidates, frontiers and unresolved records SHALL be identical and the canonical payload bytes SHALL be byte-identical. (CDC-49)
2. No fact, observation, relation, candidate, frontier or diagnostic emitted by this workstream SHALL contain an absolute path. (CDC-50)
3. WHEN the classifier runs twice over the same ledger THEN it SHALL produce identical facts, relations, candidates, frontiers and unresolved records. (CDC-51)
4. The fixture's connection-string password and every other suspected-secret value SHALL be absent from every committed package artifact. (CDC-52)
5. `contracts/taxonomy-registry.json` SHALL remain byte-identical, and the registry drift gate SHALL pass. (CDC-53)
6. `Csharp2Md.Analysis` SHALL NOT reference `Csharp2Md.Storage`, `Csharp2Md.Projection` or `Csharp2Md.Cli`, and `Csharp2Md.Cli` SHALL NOT reference `Csharp2Md.Domain`. (CDC-54)
7. WHEN this workstream's passes are registered THEN `ClassificationAndPromotionStage` SHALL run them in the order `Components`, `Entry points`, `Boundaries`, `Contracts`, `Persistence`, `Configuration`, `Relations`, `Invokes`, `Executes`, and its aggregate counts SHALL include their output. (CDC-55)

**Independent Test**: Analyze the fixture from two clone paths and compare canonical bytes; grep every package artifact for the fixture password and for the drive-letter prefix; run the registry drift gate; assert the stage's pass-name order.

---

### P2: Component and configuration run coverage

**User Story**: As an operator, I want to see how much of the solution was grouped and how many configuration keys were bound so that I can judge a run's completeness.

**Why P2**: The counts explain a degraded run, but the package is usable without them and the certification document is workstream 8's.

**Acceptance Criteria**:

1. WHEN classification completes THEN the snapshot diagnostics envelope SHALL carry a component-coverage record holding the number of projects grouped, the number of applications found and the number of components with no deployment unit. (CDC-56)
2. WHEN classification completes THEN the snapshot diagnostics envelope SHALL carry a configuration-coverage record holding the number of declared keys, the number bound by a `configured-by` relation and the number of keys read in C# but not declared. (CDC-57)
3. The coverage records SHALL publish counts only and SHALL NOT publish a percentage or a recall figure. (CDC-58)

**Independent Test**: Analyze `Acme.Orders.slnx`; assert `diagnostics.json` carries both coverage records with non-zero numerators and no `%` character in either payload.

---

## Requirement Traceability

| ID | Story | Task | Status |
| --- | --- | --- | --- |
| CDC-01 | P1: Project metadata reaches the ledger | Phase 1 (T4, T5, T6, T7), Phase 4 (T15, T17) | In Tasks |
| CDC-02 | P1: Project metadata reaches the ledger | Phase 2 (T9), Phase 4 (T15) | In Tasks |
| CDC-03 | P1: Project metadata reaches the ledger | Phase 4 (T15) | In Tasks |
| CDC-04 | P1: Project metadata reaches the ledger | Phase 4 (T15) | In Tasks |
| CDC-05 | P1: Project metadata reaches the ledger | Phase 4 (T15) | In Tasks |
| CDC-06 | P1: Project metadata reaches the ledger | Phase 4 (T15) | In Tasks |
| CDC-07 | P1: Project metadata reaches the ledger | Phase 4 (T15) | In Tasks |
| CDC-08 | P1: Project metadata reaches the ledger | Phase 10 (T40) | In Tasks |
| CDC-09 | P1: Evidence-based component formation | Phase 1 (T4, T5, T6), Phase 2 (T8), Phase 5 (T19), Phase 9 (T37) | In Tasks |
| CDC-10 | P1: Evidence-based component formation | Phase 1 (T7), Phase 5 (T20), Phase 9 (T38) | In Tasks |
| CDC-11 | P1: Evidence-based component formation | Phase 2 (T8, T9, T10), Phase 5 (T20), Phase 9 (T37) | In Tasks |
| CDC-12 | P1: Evidence-based component formation | Phase 5 (T20) | In Tasks |
| CDC-13 | P1: Evidence-based component formation | Phase 5 (T19), Phase 9 (T38) | In Tasks |
| CDC-14 | P1: Evidence-based component formation | Phase 5 (T21), Phase 6 (T23, T24, T25) | In Tasks |
| CDC-15 | P1: Evidence-based component formation | Phase 5 (T21) | In Tasks |
| CDC-16 | P1: Evidence-based component formation | Phase 5 (T21) | In Tasks |
| CDC-17 | P1: Evidence-based component formation | Phase 5 (T21) | In Tasks |
| CDC-18 | P1: Evidence-based component formation | Phase 5 (T19, T22) | In Tasks |
| CDC-19 | P1: Deployment units and inclusion | Phase 1 (T4, T5, T6, T7), Phase 2 (T8, T9), Phase 5 (T20), Phase 9 (T37) | In Tasks |
| CDC-20 | P1: Deployment units and inclusion | Phase 5 (T18, T20, T21), Phase 9 (T37) | In Tasks |
| CDC-21 | P1: Deployment units and inclusion | Phase 2 (T10), Phase 5 (T20, T21), Phase 9 (T37) | In Tasks |
| CDC-22 | P1: Deployment units and inclusion | Phase 5 (T20, T21) | In Tasks |
| CDC-23 | P1: Deployment units and inclusion | Phase 5 (T20) | In Tasks |
| CDC-24 | P1: Deployment units and inclusion | Phase 5 (T20) | In Tasks |
| CDC-25 | P1: Configuration documents reach the ledger | Phase 2 (T11), Phase 3 (T12, T13, T14) | In Tasks |
| CDC-26 | P1: Configuration documents reach the ledger | Phase 2 (T11), Phase 4 (T16) | In Tasks |
| CDC-27 | P1: Configuration documents reach the ledger | Phase 4 (T16) | In Tasks |
| CDC-28 | P1: Configuration documents reach the ledger | Phase 4 (T16) | In Tasks |
| CDC-29 | P1: Configuration documents reach the ledger | Phase 4 (T16) | In Tasks |
| CDC-30 | P1: Configuration documents reach the ledger | Phase 4 (T16) | In Tasks |
| CDC-31 | P1: Configuration documents reach the ledger | Phase 4 (T16) | In Tasks |
| CDC-32 | P1: Configuration documents reach the ledger | Phase 4 (T16, T17) | In Tasks |
| CDC-33 | P1: Configuration documents reach the ledger | Phase 4 (T16) | In Tasks |
| CDC-34 | P1: Configuration documents reach the ledger | Phase 3 (T13, T14), Phase 4 (T16) | In Tasks |
| CDC-35 | P1: Configuration bindings and configured-by | Phase 7 (T26, T27), Phase 8 (T31), Phase 9 (T37) | In Tasks |
| CDC-36 | P1: Configuration bindings and configured-by | Phase 7 (T28), Phase 8 (T31), Phase 9 (T37) | In Tasks |
| CDC-37 | P1: Configuration bindings and configured-by | Phase 7 (T28), Phase 8 (T31), Phase 9 (T37) | In Tasks |
| CDC-38 | P1: Configuration bindings and configured-by | Phase 7 (T28), Phase 8 (T31), Phase 9 (T37) | In Tasks |
| CDC-39 | P1: Configuration bindings and configured-by | Phase 7 (T28), Phase 8 (T31), Phase 9 (T37) | In Tasks |
| CDC-40 | P1: Configuration bindings and configured-by | Phase 7 (T28), Phase 8 (T31) | In Tasks |
| CDC-41 | P1: Configuration bindings and configured-by | Phase 7 (T28) | In Tasks |
| CDC-42 | P1: Configuration bindings and configured-by | Phase 7 (T28), Phase 8 (T31) | In Tasks |
| CDC-43 | P1: Client address resolution and targets promotion | Phase 7 (T26, T29), Phase 8 (T30, T32), Phase 9 (T37) | In Tasks |
| CDC-44 | P1: Client address resolution and targets promotion | Phase 7 (T29), Phase 8 (T32), Phase 9 (T37) | In Tasks |
| CDC-45 | P1: Client address resolution and targets promotion | Phase 7 (T29), Phase 8 (T32), Phase 9 (T37) | In Tasks |
| CDC-46 | P1: Client address resolution and targets promotion | Phase 7 (T29), Phase 8 (T32) | In Tasks |
| CDC-47 | P1: Client address resolution and targets promotion | Phase 7 (T29), Phase 8 (T32) | In Tasks |
| CDC-48 | P1: Client address resolution and targets promotion | Phase 7 (T29) | In Tasks |
| CDC-49 | P1: Determinism, security and invariants | Phase 10 (T39, T42) | In Tasks |
| CDC-50 | P1: Determinism, security and invariants | Phase 10 (T40) | In Tasks |
| CDC-51 | P1: Determinism, security and invariants | Phase 10 (T39) | In Tasks |
| CDC-52 | P1: Determinism, security and invariants | Phase 10 (T40) | In Tasks |
| CDC-53 | P1: Determinism, security and invariants | Phase 10 (T40, T41) | In Tasks |
| CDC-54 | P1: Determinism, security and invariants | Phase 10 (T40) | In Tasks |
| CDC-55 | P1: Determinism, security and invariants | Phase 5 (T22), Phase 8 (T33), Phase 9 (T34), Phase 10 (T42) | In Tasks |
| CDC-56 | P2: Component and configuration run coverage | Phase 9 (T35) | In Tasks |
| CDC-57 | P2: Component and configuration run coverage | Phase 9 (T36) | In Tasks |
| CDC-58 | P2: Component and configuration run coverage | Phase 9 (T35, T36) | In Tasks |

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 58 total, 58 mapped to tasks, 0 unmapped ✅

---

## Fixture changes

`fixtures/SyntheticSolution` is the only versioned analysis fixture and this workstream extends it. Four changes, each required by an approved decision:

1. **Applications.** `Acme.Orders.csproj` and `Acme.Payments.csproj` gain `<OutputType>Exe</OutputType>`, and each gains the `Main` entry point that `OutputType=Exe` requires to compile. `Program.Main` delegates to the existing `ConfigureHost`; the Payments equivalent delegates to its existing service surface. Nothing else in those files changes.
2. **Second deployable.** A new project `Acme.Orders.Worker` is added to `Acme.Orders.slnx`. It is an application, references only `Acme.Shared.Contracts`, and consumes `IEventBus` so its symbols carry real observations. Its purpose is to make `Acme.Shared.Contracts` a shared component in `Acme.Orders.slnx` while the same project stays privately used in `Acme.Payments.slnx`.
3. **Second configuration file.** `Acme.Orders/appsettings.Development.json` is added, declaring one key already present in `appsettings.json` and one new key, so the `appsettings*.json` glob and the no-merge rule both have coverage.
4. **Configuration content.** `Acme.Orders/appsettings.json` is unchanged. Its `ConnectionStrings:OrdersDb` password is the suspected-secret probe, `Services:PaymentService` is the literal-address probe and `Services:NotificationService` is the environment-indirection probe.

`Acme.Broken`, `Acme.DoesNotExist`, `Acme.Shared.Contracts` and every persistence, contract and boundary fixture file are unchanged. Superseded legacy comments in the touched files are rewritten to describe the current contract.

---

## Success Criteria

How we know the feature is successful:

- [ ] `analyze --solution fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx --output <dir>` writes a package with non-zero `DeploymentUnit` and `ConfigurationBinding` facts and non-zero `belongs-to`, `included-in` and `configured-by` relations
- [ ] `Acme.Orders.slnx` yields exactly three components and two deployment units; `Acme.Payments.slnx` yields exactly one component and one deployment unit
- [ ] `Acme.Shared.Contracts` is a shared component in `Acme.Orders.slnx` and is grouped into the `Acme.Payments` component in `Acme.Payments.slnx`
- [ ] All four registered `configured-by` triples — `Component`, `Symbol`, `DataStore`, `BoundaryOperation` — are exercised by the fixture
- [ ] `PaymentService` produces a confirmed `targets` relation with no surviving candidate; `NotificationService` keeps its candidate and gains an open frontier; `ShippingService` keeps its candidate unchanged
- [ ] `appsettings.json` produces no `unsupported-document` diagnostic
- [ ] The fixture connection-string password appears in no package artifact
- [ ] Two clone paths produce byte-identical canonical payloads
- [ ] `contracts/taxonomy-registry.json` is byte-identical and the drift gate passes
- [ ] `diagnostics.json` carries component and configuration coverage counts with no percentage
- [ ] After the Verifier, `LocalCorpus` tests run when `fixtures/eShop` or `fixtures/eShopOnContainers` exist, and are skipped when they do not
