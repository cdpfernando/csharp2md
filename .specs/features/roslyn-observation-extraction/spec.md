# Roslyn Observation Extraction Specification

## Problem Statement

Workstream 3 can commit a schema-valid package, but Inventory, Semantic Analysis, and Observation Extraction still report zeros. Workstreams 5A–5D cannot classify against an empty ledger, and AD-004 forbids them from reaching through Roslyn themselves. This workstream binds each requested solution with Roslyn, publishes structural facts, and fills the immutable observation ledger.

## Goals

- [ ] Inventory each requested solution inside an authorized root, reject symlink escapes, and emit `Solution`, `Project`, and `Document` facts with clone-path-independent identities.
- [ ] Load each solution with `MSBuildWorkspace`, compile every declared target framework under one configuration, strip generators and analyzers, and emit `Symbol` facts for declared C# symbols.
- [ ] Extract all ten registry observation kinds from C#, with registered-context kinds limited to the contexts this spec declares, and commit that graph through the existing storage port.
- [ ] Keep secrets, absolute paths, and Roslyn types out of the published package and off the Analysis public surface.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Architecture, contract, persistence, and configuration classification | Owned by workstreams 5A–5D; this feature emits observations and structural facts only |
| `belongs-to`, `included-in`, `executes`, `invokes`, and other non-`contains` confirmed relations | Those are classifier promotions (AD-004) |
| Non-C# observation adapters (appsettings, Docker, Kubernetes, protobuf, OpenAPI, `.sql`) | Inventoried as documents here; specialized extraction belongs with 5A/5C/5D |
| Source-projection files, catalogs, postings, Markdown, `retrieval.md` | Owned by workstream 6; STOR-46 forbids them until then |
| Batch manifest and proven cross-solution composition | Owned by workstream 7; per-solution isolation already exists |
| `validate` / `compose`, `--trust`, `--include-source-generators`, `--analysis-timeout`, syntax-only mode, labeled-corpus certification, performance baselines | Owned by workstream 8; STOR-52 keeps those flags absent |
| Query engines, `kb`, QMD, embeddings, wiki compilation | Deferred by AD-011 |
| Incremental analysis and snapshot diff | Explicit non-goals of the generator replacement |
| Changes to Domain descriptor tables or `contracts/taxonomy-registry.json` bytes | Workstream 1 is closed |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here. Nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Observation ledger completeness | All ten kinds from C#; non-C# files inventoried only | Confirmed in discuss; classifiers in 5A–5D must be promotion-only after this ledger is stable | y |
| Trust and generators | Semantic MSBuild is the default `analyze` path; generators stay stripped; analyzers never run; no new flags | Confirmed in discuss; `--solution` is the trust grant for a local tool; STOR-52 stays | y |
| Authorized root | Smallest directory that contains the solution file and every existing project path the solution lists | Discussed default was the solution-file directory; the versioned fixture lists sibling projects, so a directory-only root would reject declared projects. Security still rejects resolved paths outside that expanded root. Batch-wide common ancestor and `--root` stay rejected | n |
| Analysis variants | Every declared `TargetFramework` × one configuration (`Debug` when unspecified); `environment` is `local` | Confirmed TFM×configuration in discuss; `environment` has no CLI flag in this workstream | y |
| Workspace identity | Logical name `default` | No workspace option in this workstream; clone path must not enter IDs | n |
| Solution logical path | The solution file’s name, including extension (`Acme.Orders.slnx`) | Authorized-root expansion would otherwise put a relative folder into an identity that should survive moving the clone | n |
| Structural `contains` | Emit `Project contains Document` and `Document contains Symbol` when `derived_from` can cite at least one observation; do not emit `Solution contains Project` | `ConfirmedRelation` requires a non-empty observation evidence chain; solution/project nesting is already in `ProjectId` | n |
| Symbol facets | `Callable` on methods, constructors, local functions, and identifiable lambdas; no `Controller`/`Handler`/`Repository`/`Client`/`Service` | Those roles are classification (AD-004) | n |
| Source fidelity | Relative path, one-based spans, SHA-256 document digest on every observation; no source files in the package | STOR-46; workstream 6 owns byte-faithful projection | n |
| Multi-TFM observation identity | Union by observation identity; prefer a successful bind over a failed one; never emit two observations with one identity | Domain `ObservationIdentity` has no variant axis; Storage aborts on fact identity collision and duplicate observations would be an unreadable ledger | n |
| Configuration default | `Debug` when the request does not name a configuration | Confirmed as the unspecified case; no new CLI flag | y |
| Missing listed project | Named diagnostic, continue | `Acme.DoesNotExist` is a fixture case; a missing path is not structural corruption | n |
| Unresolvable SDK / compile errors | Named diagnostic, extract what still binds, do not abort the solution | `Acme.Broken` is a fixture case; unknowns are a valid commit (quality document) | n |
| Registered-context catalog | The five contexts listed in P1 Observation ledger | Empty “registered context” is untestable; the catalog is this workstream’s compiled capability, not a 5A–5D classifier | n |
| Inventory classifier identity for `contains` | `csharp2md.inventory.contains` version 1, evidence method `syntactic` | `ConfirmedRelation.Create` requires a classifier identity; this is inventory, not a 5A–5D promoter | n |
| Persistence staging | Persistence stages the accumulated structural/observation/`contains` snapshot instead of `FactualSnapshot.Empty` | Otherwise the package stays empty after real extraction | n |
| ENG-15 zeros | Superseded for Inventory, Semantic Analysis, and Observation Extraction; later stub stages still report 0/0/0 | Filling a stage falsifies the stub claim for that stage only | n |
| Domain and registry bytes | No descriptor-table or `taxonomy-registry.json` edits | Workstream 1 is closed; extractors consume existing `Observation.Create` | n |

**Open questions:** none - all resolved or logged above.

---

## Implicit-requirement dimensions sweep

Large scope, so every dimension resolves to a requirement or an explicit exclusion.

| Dimension | Coverage |
| --- | --- |
| Input validation and bounds | ROSE-08, ROSE-09, ROSE-10, ROSE-47 — duplicate solution identities rejected; authorized root bounds inventory; relative-path grammar already rejects absolute paths at Domain construction |
| Failure and partial-failure states | ROSE-02, ROSE-11, ROSE-12, ROSE-22, ROSE-23, ROSE-24, ROSE-48 — symlink escape aborts that solution; missing project and compile failure degrade; MSBuild open failure aborts that solution only |
| Idempotency, retry, duplicate handling | ROSE-08, ROSE-43, ROSE-44, ROSE-52, ROSE-53, ROSE-54 — duplicate solution identities are rejected; observation identity ignores span; clone path, retry, and document order do not change IDs or canonical bytes |
| Auth boundaries and rate limits | N/A because the engine is a local in-process tool with no network surface. The security boundary that does exist is authorized-root enforcement (ROSE-01, ROSE-02, ROSE-07) |
| Concurrency and ordering | ROSE-36, ROSE-49 — per-solution isolation from workstream 2 remains; document order does not affect identities; overlapping `Open` stays a Storage concern (STOR-59) |
| Data lifecycle and expiry | N/A because generated packages are operator-owned files. This feature does not add TTL, archival, or deletion |
| Observability | ROSE-06, ROSE-11, ROSE-12, ROSE-22, ROSE-23, ROSE-45, ROSE-55 — unsupported files, missing project, broken SDK, MSBuild open failure, bind failure, and suspected secrets are named; CLI exit 2 on structural abort remains STOR-55 |
| External-dependency failure | ROSE-22, ROSE-23 — MSBuild/Roslyn/BuildHost are the external dependency; open failure aborts that solution and preserves the last valid package. No network |
| State-transition integrity | ROSE-41, ROSE-42, ROSE-43 — filled stages produce non-zero counts; later stubs stay zero; Persistence commits the accumulated snapshot or aborts |

---

## User Stories

### P1: Authorized inventory ⭐ MVP

**User Story**: As an operator, I want each solution inventoried only inside an authorized root so that analysis cannot follow a symlink or copy a file from outside the tree I named.

**Why P1**: Inventory is the first pipeline stage. Without a root, Roslyn binding has no legal file set, and the quality document’s secret/path rules have nothing to enforce against.

**Acceptance Criteria**:

1. WHEN Inventory runs for a solution THEN the authorized root SHALL be the smallest directory that contains that solution file and every project path the solution lists that exists on disk. (ROSE-01)
2. IF a symlink’s resolved target lies outside the authorized root THEN Inventory SHALL abort publication for that solution, SHALL name the symlink path, and SHALL NOT follow the target. (ROSE-02)
3. WHEN a listed project path exists inside the authorized root THEN Inventory SHALL emit one `Project` fact whose logical relative path is relative to that root and uses forward slashes. (ROSE-03)
4. WHEN a file is a project item of an inventoried project, or sits in that project’s directory inside the authorized root, THEN Inventory SHALL emit one `Document` fact with a relative path that uses forward slashes and contains no drive prefix. (ROSE-04)
5. WHEN a non-C# document is inventoried THEN Inventory SHALL keep it as a `Document` and SHALL NOT run a C# observation extractor against it. (ROSE-05)
6. WHEN a non-C# document is inventoried THEN Inventory SHALL record the support outcome `unsupported` for that document. (ROSE-06)
7. The Inventory SHALL NOT read, hash, or copy any file whose resolved path lies outside the authorized root. (ROSE-07)
8. WHEN two requested solutions would produce the same `SolutionId` THEN the engine SHALL reject the request before analysis and SHALL name both input paths. (ROSE-08)
9. The workspace identity used in `SolutionId` SHALL be the logical name `default`. (ROSE-09)
10. The solution’s logical relative path SHALL be the solution file’s name including extension. (ROSE-10)
11. IF a listed project path does not exist on disk THEN Inventory SHALL record a diagnostic naming that path and SHALL NOT abort the solution. (ROSE-11)
12. IF a listed project cannot be loaded because its SDK is unresolvable THEN Inventory SHALL record a diagnostic naming that project and SHALL NOT abort the solution. (ROSE-12)

**Independent Test**: Analyze `fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx`; assert `Acme.Orders`, `Acme.Shared.Contracts`, and `Acme.Broken` become projects; assert `Acme.DoesNotExist` is a named diagnostic; plant a symlink to a file outside the expanded root and assert unpublished plus the symlink named; request two copies of the same solution file name from different folders and assert rejection.

---

### P1: Structural facts ⭐ MVP

**User Story**: As a later classifier author, I want `Solution`, `Project`, `Document`, and `Symbol` facts committed so that observations have legal owners and containment is navigable without Roslyn.

**Why P1**: Observation identity requires an owner `FactReference`. Without structural facts the ledger cannot be constructed.

**Acceptance Criteria**:

1. WHEN Inventory completes for a loadable solution THEN the package SHALL contain exactly one `Solution` fact for that solution. (ROSE-13)
2. WHEN Semantic Analysis binds a C# document THEN the package SHALL contain a `Symbol` fact for each declared type, method, property, field, event, constructor, local function, and identifiable lambda in that document. (ROSE-14)
3. WHEN a symbol is a method, constructor, local function, or identifiable lambda THEN its facet set SHALL include `Callable`. (ROSE-15)
4. WHEN a symbol is a type, property, field, or event THEN its facet set SHALL NOT include `Controller`, `Handler`, `Repository`, `Client`, or `Service`. (ROSE-16)
5. WHEN a C# document yields at least one observation THEN the package SHALL contain a `contains` relation from the owning `Project` to that `Document` with evidence method `syntactic`, classifier `csharp2md.inventory.contains` version 1, and a non-empty `derived_from` of observations from that document. (ROSE-17)
6. WHEN a `Symbol` is declared in a C# document that yields at least one observation THEN the package SHALL contain a `contains` relation from that `Document` to that `Symbol` with the same classifier identity and a non-empty `derived_from`. (ROSE-18)
7. The package SHALL NOT contain a `contains` relation whose source is `Solution` and whose target is `Project`. (ROSE-19)
8. The package SHALL NOT contain a confirmed relation whose kind is not `contains`. (ROSE-20)
9. IF two structural facts in one solution would share one identity THEN commit SHALL abort as structural corruption naming the identity. (ROSE-21)

**Independent Test**: Read the committed package for `Acme.Orders.slnx`; assert solution/project/document/symbol counts are non-zero; assert `OrderService.PlaceOrderAsync` is `Callable`; assert `OrdersController` is not tagged `Controller`; assert `contains` triples match the matrix; assert no `invokes` edge.

---

### P1: Semantic Roslyn binding ⭐ MVP

**User Story**: As an operator, I want `analyze` to load each solution with the real compiler so that observations are bound against actual symbols, not guessed names.

**Why P1**: AD-003 makes MSBuildWorkspace the only legal semantic path. A syntax-only stub would leave 5B without bindable invocations.

**Acceptance Criteria**:

1. IF `MSBuildWorkspace` cannot open a requested solution THEN the engine SHALL abort publication for that solution, SHALL name the failure, and SHALL continue the remaining solutions. (ROSE-22)
2. IF a project produces compilation diagnostics of error severity THEN the engine SHALL record a diagnostic naming that project, SHALL extract observations for occurrences that still bind, and SHALL NOT abort the solution. (ROSE-23)
3. The Semantic Analysis stage SHALL compile every `TargetFramework` declared by each inventoried project that supports compilation, under configuration `Debug` when the request does not name another configuration. (ROSE-24)
4. WHEN a project that supports compilation is analyzed THEN the engine SHALL represent each compiled `TargetFramework` and configuration pair as an `AnalysisVariantId` whose `environment` component is `local`. (ROSE-25)
5. The `Csharp2Md.Analysis` project SHALL reference `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0. (ROSE-26)
6. The `Csharp2Md.Analysis` project SHALL NOT reference any `Microsoft.Build.*` package. (ROSE-27)
7. The production assemblies SHALL NOT call `MSBuildLocator.RegisterDefaults`. (ROSE-28)
8. WHEN a compilation is created THEN analyzer assemblies SHALL be absent from that compilation. (ROSE-29)
9. WHEN a compilation is created THEN source-generator assemblies SHALL be absent from that compilation. (ROSE-30)
10. The public surface of `Csharp2Md.Analysis` SHALL NOT expose any type from `Microsoft.CodeAnalysis`. (ROSE-31)

**Independent Test**: Re-establish the sanitation probes against a compilation taken from the filled Semantic Analysis stage; assert `Microsoft.Build` is absent from Analysis’s package graph; open `Acme.Broken` as part of `Acme.Orders.slnx` and assert the package still commits with a named diagnostic; fail `OpenSolutionAsync` in a test double and assert only that solution is unpublished.

---

### P1: Observation ledger ⭐ MVP

**User Story**: As a workstream 5A–5D author, I want every registry observation kind extracted from C# so that classifiers consume a stable ledger instead of writing extractors.

**Why P1**: The roadmap lets the four classifier workstreams proceed only after this ledger exists.

**Acceptance Criteria**:

1. WHEN a C# invocation binds to a method or delegate THEN Observation Extraction SHALL emit an `Invocation` observation owned by the containing callable. (ROSE-32)
2. WHEN a C# object-creation expression binds THEN Observation Extraction SHALL emit an `ObjectCreation` observation. (ROSE-33)
3. WHEN a C# type reference binds THEN Observation Extraction SHALL emit a `TypeUsage` observation. (ROSE-34)
4. WHEN a C# type declaration has a base type or interface in its base list THEN Observation Extraction SHALL emit a `BaseType` observation. (ROSE-35)
5. WHEN a C# attribute is applied THEN Observation Extraction SHALL emit an `AttributeUsage` observation. (ROSE-36)
6. WHEN a C# assignment writes a member of an entity instance used through `DbSet<T>` or `DbContext` THEN Observation Extraction SHALL emit an `Assignment` observation. (ROSE-37)
7. WHEN C# code invokes `IConfiguration` (`this[]`, `GetSection`, `GetValue`), `IOptions<T>`, or `Configure<T>` THEN Observation Extraction SHALL emit a `Configuration` observation whose payload carries the configuration key and SHALL NOT carry the bound value. (ROSE-38)
8. WHEN C# code applies `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`, `[HttpPatch]`, or `[Route]`, or invokes `MapGet`, `MapPost`, `MapPut`, or `MapDelete` THEN Observation Extraction SHALL emit a `RouteDeclaration` observation whose payload carries the route literal when that literal is present. (ROSE-39)
9. WHEN C# code invokes `Publish`, `PublishAsync`, `Send`, `SendAsync`, or `Subscribe` on a bound receiver THEN Observation Extraction SHALL emit a `MessageOperation` observation. (ROSE-40)
10. WHEN C# code accesses a `DbSet<T>` member, invokes `SaveChanges`, `SaveChangesAsync`, `Add`, `AddAsync`, `FromSqlRaw`, `FromSqlInterpolated`, `ExecuteSqlRaw`, or `ExecuteSqlInterpolated` on a `DbContext` or `DbSet<T>`, or applies a LINQ operator whose source binds to a `DbSet<T>` THEN Observation Extraction SHALL emit a `DataAccess` observation. (ROSE-41)
11. IF an occurrence of a registered-context kind does not match a context in ROSE-37 through ROSE-41 THEN Observation Extraction SHALL NOT emit that kind for it. (ROSE-42)
12. WHEN two observations share owner, kind, and normalized payload but differ in structural occurrence ordinal THEN they SHALL have distinct identities. (ROSE-43)
13. WHEN two observations share owner, kind, normalized payload, and ordinal but differ only in source span THEN they SHALL have the same identity. (ROSE-44)
14. IF an occurrence cannot bind THEN Observation Extraction SHALL still emit the always-when-bindable kind when the syntax is present, SHALL attach a binding diagnostic naming the failure, and SHALL NOT invent a target fact identity. (ROSE-45)
15. Every emitted observation SHALL carry an owner, kind, normalized payload, positive occurrence ordinal, evidence locator, extraction method, binding diagnostic, document hash, and extractor version. (ROSE-46)
16. Observation payloads SHALL contain only `StructuralLiteral` values. (ROSE-47)
17. The Observation Extraction stage SHALL NOT expose a Roslyn syntax, symbol, or compilation type on the Analysis public surface. (ROSE-48)
18. The versioned fixture `fixtures/SyntheticSolution` SHALL contain at least one positive occurrence of each of the ten observation kinds under the contexts above. (ROSE-49)

**Independent Test**: Analyze `Acme.Orders.slnx`, read observations by kind, and assert a named fixture occurrence for each kind (`eventBus.PublishAsync` for `MessageOperation`, `_context.SaveChanges` for `DataAccess`, `order.Status =` for `Assignment`, `OrdersController : ControllerBase` for `BaseType`). Extend the fixture only where a kind has no occurrence today. Assert a duplicate ordinal/payload pair is impossible and a span-only difference does not fork identity.

---

### P1: Source fidelity, secrets, and determinism ⭐ MVP

**User Story**: As a downstream reader, I want locators and hashes that survive moving the clone, and I want secrets kept out of the ledger, so that workstream 6 can project source without republishing credentials.

**Why P1**: Absolute paths fail STOR-29 at commit. Unhashed documents make later source projection unverifiable. Secret copying is a zero-tolerance gate.

**Acceptance Criteria**:

1. Every observation evidence locator SHALL use a relative path with forward slashes and a one-based source span. (ROSE-50)
2. Every observation extracted from a document SHALL carry the SHA-256 digest of that document’s file bytes as its `DocumentHash`. (ROSE-51)
3. IF two clones of the same tree are analyzed THEN every structural fact identity and every observation identity SHALL be equal. (ROSE-52)
4. IF the same solution is analyzed twice THEN every canonical payload file SHALL be byte-identical. (ROSE-53)
5. IF document enumeration order is shuffled THEN structural fact identities and observation identities SHALL be unchanged. (ROSE-54)
6. IF an extractor would copy a connection string, password, token, certificate, or authorization value into a payload, diagnostic, or fact THEN it SHALL omit that value, SHALL record `SuspectedSecretEvidence` with document, span, document hash, and a redacted excerpt, and SHALL NOT hash the secret value itself. (ROSE-55)
7. Canonical package payloads SHALL NOT contain an absolute filesystem path. (ROSE-56)
8. The committed package SHALL NOT contain source-projection files, Markdown pages, catalogs, or postings. (ROSE-57)

**Independent Test**: Analyze the fixture from two working directories; assert identity equality. Commit twice; assert canonical bytes. Plant `Password=secret` in a C# string that a registered configuration context would otherwise capture; assert the value is absent from payload JSON and a redacted excerpt is present.

---

### P1: Commit the extracted graph ⭐ MVP

**User Story**: As an operator, I want `analyze` to write the extracted facts and observations into the package I passed as `--output` so that the walking skeleton stops publishing an empty ledger.

**Why P1**: A filled pipeline that still stages `FactualSnapshot.Empty` would hide extraction behind a lying commit.

**Acceptance Criteria**:

1. WHEN `analyze` completes without structural corruption THEN Persistence SHALL stage the accumulated `Solution`, `Project`, `Document`, `Symbol`, observation, and `contains` records, not an empty snapshot. (ROSE-58)
2. WHEN Inventory, Semantic Analysis, or Observation Extraction runs on `Acme.Orders.slnx` THEN that stage’s reported fact, observation, or relation count SHALL be greater than zero for at least one of those three metrics as appropriate to the stage. (ROSE-59)
3. WHEN Classification and Promotion, Retrieval Projection, or Batch Composition runs THEN that stage SHALL still report 0 facts, 0 observations, and 0 relations of its own production. (ROSE-60)
4. The CLI SHALL still require `--output` and at least one `--solution`, and SHALL NOT expose `--trust`, `--include-source-generators`, or `--analysis-timeout`. (ROSE-61)
5. The `Csharp2Md.Cli` project SHALL continue to declare no project reference to `Csharp2Md.Domain`. (ROSE-62)
6. WHEN a solution is unpublished because of a symlink escape or an MSBuild open failure THEN the last valid package for that solution SHALL remain byte-identical. (ROSE-63)
7. WHERE the in-memory adapter is used the engine SHALL still produce structural facts and observations and SHALL create no files. (ROSE-64)

**Independent Test**: Run `analyze` on `Acme.Orders.slnx` with `--output`; read the package and assert non-zero structural facts and observations; assert later stub stages still report zeros; rerun with the in-memory adapter in tests and assert no filesystem writes; abort on a symlink escape after a successful commit and assert the previous package bytes.

---

## Edge Cases

- IF a symlink escapes the authorized root THEN that solution SHALL be unpublished and the symlink SHALL be named (ROSE-02).
- IF a listed project is missing THEN analysis SHALL continue with a named diagnostic (ROSE-11).
- IF a project SDK cannot resolve THEN analysis SHALL continue with a named diagnostic (ROSE-12).
- IF `MSBuildWorkspace` cannot open the solution THEN only that solution SHALL be unpublished (ROSE-22).
- IF a compilation has errors THEN bindable observations SHALL still be emitted (ROSE-23).
- IF two solutions would share one `SolutionId` THEN the request SHALL be rejected before analysis (ROSE-08).
- IF an invocation cannot bind THEN the observation SHALL carry a binding diagnostic and no invented target (ROSE-45).
- IF a configuration occurrence has a secret-shaped value THEN the payload SHALL carry the key only (ROSE-38, ROSE-55).
- WHEN N = 1 THEN the package SHALL still occupy a child directory under `--output` (STOR-19, unchanged).
- WHERE the in-memory adapter is used no files SHALL be created (ROSE-64).

---

## Requirement Traceability

Each requirement gets a unique ID for tracking across design, tasks, and validation.

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| ROSE-01 | P1: Authorized inventory | Tasks (T14) | Implemented |
| ROSE-02 | P1: Authorized inventory | Tasks (T4, T16, T23, T24) | Implemented |
| ROSE-03 | P1: Authorized inventory | Tasks (T15, T17) | Implemented |
| ROSE-04 | P1: Authorized inventory | Tasks (T19) | Implemented |
| ROSE-05 | P1: Authorized inventory | Tasks (T20, T43) | Implemented |
| ROSE-06 | P1: Authorized inventory | Tasks (T20) | Implemented |
| ROSE-07 | P1: Authorized inventory | Tasks (T16, T23) | Implemented |
| ROSE-08 | P1: Authorized inventory | Tasks (T18) | Implemented |
| ROSE-09 | P1: Authorized inventory | Tasks (T17) | Implemented |
| ROSE-10 | P1: Authorized inventory | Tasks (T17) | Implemented |
| ROSE-11 | P1: Authorized inventory | Tasks (T21) | Implemented |
| ROSE-12 | P1: Authorized inventory | Tasks (T29) | Pending |
| ROSE-13 | P1: Structural facts | Tasks (T17, T22) | Implemented |
| ROSE-14 | P1: Structural facts | Tasks (T33, T37) | Pending |
| ROSE-15 | P1: Structural facts | Tasks (T34) | Pending |
| ROSE-16 | P1: Structural facts | Tasks (T35) | Pending |
| ROSE-17 | P1: Structural facts | Tasks (T51) | Pending |
| ROSE-18 | P1: Structural facts | Tasks (T51) | Pending |
| ROSE-19 | P1: Structural facts | Tasks (T51) | Pending |
| ROSE-20 | P1: Structural facts | Tasks (T51) | Pending |
| ROSE-21 | P1: Structural facts | Tasks (T6) | Implemented |
| ROSE-22 | P1: Semantic Roslyn binding | Tasks (T5, T25, T28) | Implemented |
| ROSE-23 | P1: Semantic Roslyn binding | Tasks (T30) | Pending |
| ROSE-24 | P1: Semantic Roslyn binding | Tasks (T25, T31) | Pending |
| ROSE-25 | P1: Semantic Roslyn binding | Tasks (T31) | Pending |
| ROSE-26 | P1: Semantic Roslyn binding | Tasks (T7, T8, T25) | Implemented |
| ROSE-27 | P1: Semantic Roslyn binding | Tasks (T8, T9) | Implemented |
| ROSE-28 | P1: Semantic Roslyn binding | Tasks (T10) | Implemented |
| ROSE-29 | P1: Semantic Roslyn binding | Tasks (T26, T27) | Implemented |
| ROSE-30 | P1: Semantic Roslyn binding | Tasks (T26, T27) | Implemented |
| ROSE-31 | P1: Semantic Roslyn binding | Tasks (T32, T36, T43) | Pending |
| ROSE-32 | P1: Observation ledger | Tasks (T39) | Pending |
| ROSE-33 | P1: Observation ledger | Tasks (T39) | Pending |
| ROSE-34 | P1: Observation ledger | Tasks (T39) | Pending |
| ROSE-35 | P1: Observation ledger | Tasks (T39) | Pending |
| ROSE-36 | P1: Observation ledger | Tasks (T39) | Pending |
| ROSE-37 | P1: Observation ledger | Tasks (T46) | Pending |
| ROSE-38 | P1: Observation ledger | Tasks (T47) | Pending |
| ROSE-39 | P1: Observation ledger | Tasks (T48) | Pending |
| ROSE-40 | P1: Observation ledger | Tasks (T49) | Pending |
| ROSE-41 | P1: Observation ledger | Tasks (T50) | Pending |
| ROSE-42 | P1: Observation ledger | Tasks (T46, T49, T50) | Pending |
| ROSE-43 | P1: Observation ledger | Tasks (T38, T44) | Pending |
| ROSE-44 | P1: Observation ledger | Tasks (T6, T38, T44) | Implemented |
| ROSE-45 | P1: Observation ledger | Tasks (T40) | Pending |
| ROSE-46 | P1: Observation ledger | Tasks (T38, T39, T41) | Pending |
| ROSE-47 | P1: Observation ledger | Tasks (T39, T47) | Pending |
| ROSE-48 | P1: Observation ledger | Tasks (T36, T43) | Pending |
| ROSE-49 | P1: Observation ledger | Tasks (T45, T50) | Pending |
| ROSE-50 | P1: Source fidelity, secrets, and determinism | Tasks (T41) | Pending |
| ROSE-51 | P1: Source fidelity, secrets, and determinism | Tasks (T41) | Pending |
| ROSE-52 | P1: Source fidelity, secrets, and determinism | Tasks (T57) | Pending |
| ROSE-53 | P1: Source fidelity, secrets, and determinism | Tasks (T19, T58) | Pending |
| ROSE-54 | P1: Source fidelity, secrets, and determinism | Tasks (T38, T44, T59) | Pending |
| ROSE-55 | P1: Source fidelity, secrets, and determinism | Tasks (T13, T42, T47, T61) | Pending |
| ROSE-56 | P1: Source fidelity, secrets, and determinism | Tasks (T1, T60) | Implemented |
| ROSE-57 | P1: Source fidelity, secrets, and determinism | Tasks (T54) | Pending |
| ROSE-58 | P1: Commit the extracted graph | Tasks (T2, T3, T52) | Implemented |
| ROSE-59 | P1: Commit the extracted graph | Tasks (T11, T12, T22, T37, T43, T53) | Implemented |
| ROSE-60 | P1: Commit the extracted graph | Tasks (T53) | Pending |
| ROSE-61 | P1: Commit the extracted graph | Tasks (T56) | Pending |
| ROSE-62 | P1: Commit the extracted graph | Tasks (T56) | Pending |
| ROSE-63 | P1: Commit the extracted graph | Tasks (T23, T62) | Implemented |
| ROSE-64 | P1: Commit the extracted graph | Tasks (T55, T63) | Pending |

**Coverage:** 64 total, 64 mapped to tasks, 0 unmapped

---

## Success Criteria

How we know the feature is successful:

- [ ] `analyze --solution fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx --output <dir>` writes a package with non-zero structural facts, non-zero observations, and only `contains` confirmed relations
- [ ] All ten observation kinds appear at least once from the versioned fixture
- [ ] A symlink escape unpublished that solution and leaves any previous package byte-identical
- [ ] Analyzer and generator assemblies are absent from the compilation
- [ ] Two clone paths produce identical identities and canonical payload bytes
- [ ] After the Verifier, `LocalCorpus` analyze tests run when `fixtures/eShop` or `fixtures/eShopOnContainers` exist, and are skipped when they do not
