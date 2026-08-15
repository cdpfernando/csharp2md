# csharp2md (v1) Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

---

**Design**: `.specs/features/csharp2md/design.md`
**Spec**: `.specs/features/csharp2md/spec.md` (39 acceptance criteria)
**Status**: Draft

---

## Test Coverage Matrix

> Generated from spec + design + user decisions - confirm before Execute. Guidelines found: **none** - greenfield repo, no `AGENTS.md`, `CONTRIBUTING.md`, test config, or CI. Strong defaults applied. Stack chosen by user: **xunit + Verify** (snapshot for text output); e2e runs in a **separate gate** via `Category=Integration`. C# style authority is `dotnet-skills` per `CLAUDE.md`.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain logic (detectors, classifiers, resolvers, discoverer, graph builder) | unit | All branches; 1:1 to spec ACs; every listed edge case has a test | `tests/Csharp2Md.Core.Tests/**/*Tests.cs` | `dotnet test --filter Category!=Integration` |
| Renderer (MarkdownRenderer + enrichment) | unit | All branches + **span-coverage invariant asserted** (AD-002); format via Verify snapshot | `tests/Csharp2Md.Core.Tests/Rendering/*Tests.cs` | `dotnet test --filter Category!=Integration` |
| Output writers (index, JSON, Mermaid) | unit | Every emitted artifact shape via Verify snapshot + explicit asserts on required fields | `tests/Csharp2Md.Core.Tests/Output/*Tests.cs` | `dotnet test --filter Category!=Integration` |
| Roslyn loading (SolutionLoader) | integration | All load-health paths: ok, degraded, missing-restore, unsupported-for-compilation | `tests/Csharp2Md.Core.Tests/Loading/*Tests.cs` | `dotnet test` |
| Pipeline orchestration | integration | Full run against fixture; sequential-workspace invariant asserted | `tests/Csharp2Md.Core.Tests/Pipeline/*Tests.cs` | `dotnet test` |
| CLI end-to-end | integration | Happy path + every error/exit-code path from P3 | `tests/Csharp2Md.Core.Tests/Cli/*Tests.cs` | `dotnet test` |
| Build config / scaffolding / fixture projects | none | - (build gate only) | - | build gate only |

**Verify snapshots** live beside their tests in `snapshots/` and are reviewed on approval - never auto-accepted. The span-coverage invariant is a **real assert**, not a snapshot, precisely so the AD-002 fidelity guarantee cannot be rubber-stamped.

## Gate Check Commands

> Generated from the chosen stack - confirm before Execute. Commands run from the repo root. All listed commands must pass, in order.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks with unit tests only | `dotnet test --filter "Category!=Integration"` |
| Full | After tasks with integration/e2e tests | `dotnet test` |
| Build | After phase completion or config/fixture-only tasks | `dotnet build -c Release` → `dotnet format --verify-no-changes` → `dotnet test` |

> ✅ **Quick-gate filter syntax confirmed by T1 (2026-08-14).** The scaffolded test project resolved xUnit **2.9.3** (VSTest runner, `xunit.runner.visualstudio` 3.1.4) — not xUnit v3/MTP. `dotnet test --filter "Category!=Integration"` was proven empirically: run against one `[Trait("Category","Integration")]`-tagged test and one untagged test, it executed exactly the untagged one. Integration tests in later tasks must carry `[Trait("Category", "Integration")]` for this filter to exclude them.

**Additional quality gates:**

| When | Skill |
| --- | --- |
| End of every phase (substantial new code) | `dotnet-skills:slopwatch` (per `CLAUDE.md`) |
| End of Phase 3 and Phase 4 (most complex logic) | `dotnet-test:crap-score`, `dotnet-skills:crap-analysis` |
| End of Phase 3 and Phase 5 | `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns` |
| Before the Verifier runs | `dotnet-test:test-gap-analysis` — complements the discrimination sensor with mutation reasoning |

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a phase execute in order.

### Phase 0: Foundation & BuildHost risk closure

The highest-risk unknown in the design (does `PackAsTool` package the Roslyn BuildHost correctly?) is closed here, before anything is built on top of it.

```
T1 → T2
T2 → T3
T3 → T4
```

### Phase 1: Inventory (Stage 1)

Cheap, no Roslyn. Must fully precede detection.

```
T1 → T5
T5 → T6
T6 → T7
T6 → T8
T8 → T9
```

### Phase 2: Rendering (Stage 2a)

```
T1 → T10
T10 → T11
T11 → T12
T11 → T13
```

### Phase 3: Detection (Stage 2b)

```
T10 → T14
T14 → T15
T14 → T16
T14 → T17
T14 → T18
```

### Phase 4: Aggregate (Stage 3)

```
T18 → T19
T19 → T20
T19 → T21
T13 → T22
```

### Phase 5: Integration

```
T11 → T23
T18 → T23
T23 → T24
T24 → T25
T25 → T26
```

---

## Task Breakdown

### T1: Scaffold repository and build configuration

**What**: Create the solution, build config, and three empty projects per `dotnet-skills:project-structure`.
**Where**: repo root (`csharp2md.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `NuGet.Config`) plus empty `src/Csharp2Md.Cli`, `src/Csharp2Md.Core`, `tests/Csharp2Md.Core.Tests`
**Depends on**: None
**Reuses**: None (greenfield)
**Requirement**: enables all

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:project-structure`, `dotnet-skills:package-management`, `dotnet-nuget:convert-to-cpm`, `dotnet-test:scaffold-dotnet-test-project`, `dotnet-msbuild:directory-build-organization`

**Done when**:
- [x] `.gitignore` created **before** any project is scaffolded (`dotnet new gitignore`), so `bin/`, `obj/`, and `nupkg/` are never tracked
- [x] `.slnx` created (never alongside a `.sln`), all three projects registered
- [x] `Directory.Build.props` sets `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `NetLibVersion=net10.0`
- [x] `Directory.Packages.props` has `ManagePackageVersionsCentrally=true`; all packages added via `dotnet add package`, never hand-edited XML
- [x] `global.json` pins SDK `10.0.300` with `rollForward: latestFeature`
- [x] **Quick-gate filter syntax verified empirically**: added one `Category=Integration`-tagged test and one untagged test (`tests/Csharp2Md.Core.Tests/FilterSyntaxProbeTests.cs`), ran `dotnet test --filter "Category!=Integration"`, confirmed it executed exactly 1 of 2 tests. The template-generated test stack is xUnit **v2.9.3** (VSTest), not v3/MTP, so the Gate Check Commands table's existing VSTest syntax is correct as written — no correction needed.
- [x] Gate check passes: `dotnet build -c Release` → `dotnet format --verify-no-changes` → `dotnet test`

**Tests**: none
**Gate**: build

**Commit**: `chore(build): scaffold solution, central package management, and build config`

---

### T2: Build the synthetic fixture solution

**What**: Create a 3-project .NET solution used as the TDD target, exercising every detector and both degradation paths.
**Where**: `fixtures/SyntheticSolution/`
**Depends on**: T1
**Reuses**: build config from T1
**Requirement**: Success Criteria (fixture), enables P1/P2 Independent Tests

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:project-structure`, `dotnet-msbuild:check-bin-obj-clash`

**Done when**:
- [x] `Acme.Orders` — calls `Acme.Payments` over HTTP via `IHttpClientFactory.CreateClient("PaymentService")`, publishes an `OrderPlaced` message, references `Acme.Shared.Contracts`
- [x] `Acme.Payments` — exposes a gRPC service (one unary + one streaming method), subscribes to `OrderPlaced`, references `Acme.Shared.Contracts`
- [x] `Acme.Shared.Contracts` — class library with an explicit `PackageId`, referenced by both (internal direct-reference signal)
- [x] `appsettings.json` maps `PaymentService` to a hard-coded address; a second logical name resolves via env var (dynamic); a third is referenced in code but absent from config (unresolved)
- [x] One project deliberately left without restore, to exercise P1-08 (`Acme.Payments` — real `Grpc.AspNetCore` + protobuf-codegen dependency, genuinely unresolved without restore; see fixture README)
- [x] One publish with no matching subscriber, to exercise P2-15 (`PaymentProcessed`, published by `Acme.Payments`, no subscriber anywhere in fixture)
- [x] Fixture is excluded from the main solution build so its intentional breakage never fails the repo gate (not added to `csharp2md.slnx`)
- [x] Gate check passes: `dotnet build -c Release` → `dotnet format --verify-no-changes` → `dotnet test`

> Extended in T3: added `Acme.Orders.slnx`/`Acme.Payments.slnx`, `Acme.Broken`
> (unresolvable-SDK project), a reference to a nonexistent `Acme.DoesNotExist`
> project, and a fixture-scoped `Directory.Build.props` disabling SourceLink.
> T2 had no `.sln` anywhere, but `SolutionLoader` needs one to call
> `OpenSolutionAsync` against. See fixture `README.md`.

**Tests**: none
**Gate**: build

**Commit**: `test(fixtures): add synthetic microservice solution for TDD`

---

### T3: Implement SolutionLoader with load-health classification

**What**: Wrap `MSBuildWorkspace` for one service and classify each project's load health.
**Where**: `src/Csharp2Md.Core/Loading/SolutionLoader.cs` (+ `LoadReport.cs`)
**Depends on**: T2
**Reuses**: fixture from T2 as the integration target
**Requirement**: P1-05, P1-06, P1-07, P1-08, P1-09

**Tools**:
- MCP: `binlog` (bundled with `dotnet-msbuild`, for diagnosing fixture load failures)
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`, `dotnet-msbuild:binlog-failure-analysis`

**Done when**:
- [x] `SkipUnrecognizedProjects = true` set before opening (P1-06) — empirically verified via a reference to a nonexistent project file: load succeeds and skips it rather than throwing
- [x] Exactly one `OpenSolutionAsync` call per service; no `OpenProjectAsync` loop (P1-05) — structural (single call site in `SolutionLoader.LoadAsync`); see design.md's signature-resolution note (every service arrives as a solution path, resolving the P1-03/P1-05 tension)
- [x] Load failures read from the **`workspace.Diagnostics` property**, not the `[Obsolete]` `WorkspaceFailed` event (AD-003 — the event would fail the build under `TreatWarningsAsErrors`)
- [x] `Kind == Failure` marks the project degraded and the run continues (P1-07)
- [x] Unresolved-type/namespace compilation errors mark *possible missing restore*, corroborated with workspace diagnostics rather than trusting `Kind` alone (P1-08) — precedence: missing-restore diagnostics checked before Kind==Failure, since a project can show both and the former is more specific
- [x] `GetCompilationAsync() == null` classified as *unsupported-for-compilation*, explicitly **not** as a failure (P1-09)
- [x] Integration tests cover all four load-health outcomes: Ok/Degraded/PossibleMissingRestore against the fixture, UnsupportedForCompilation via an isolated `AdhocWorkspace`-built project (see fixture README's "Not fixture-based" note — no low-effort authentic MSBuild trigger found)
- [x] Gate check passes: `dotnet test`
- [x] Test count: 9 tests in `Loading` namespace (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `feat(loading): add SolutionLoader with graceful load-health classification`

---

### T4: Package as dotnet global tool and close the BuildHost risk

**What**: Make the CLI packable and installable, and prove the **packed** tool can load a solution from an unrelated working directory.
**Where**: `src/Csharp2Md.Cli/Csharp2Md.Cli.csproj`, `src/Csharp2Md.Cli/Program.cs`, `tests/Csharp2Md.Core.Tests/Cli/PackagingSmokeTests.cs`
**Depends on**: T3
**Reuses**: `SolutionLoader` from T3
**Requirement**: P3-01, P3-02, P3-03, P3-04

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:project-structure`, `dotnet-skills:local-tools`

**Done when**:
- [x] `PackAsTool=true`, `OutputType=Exe`, `ToolCommandName=csharp2md`, `PackageOutputPath=./nupkg` (P3-01) — also added explicit `PackageId=csharp2md` (default would have been `Csharp2Md.Cli`, breaking `dotnet tool install --global csharp2md` per spec.md's own Independent Test)
- [x] `dotnet pack` produces a `.nupkg` (P3-02)
- [x] `System.CommandLine` 2.0.11 wired with `--manifest` and `--output`; missing/invalid args print usage and exit non-zero (P3-04) — built-in `Required=true` + `ParseResult.InvokeAsync()` behavior, no hand-rolled validation needed
- [x] Smoke test packs, installs globally, runs the tool **from a directory outside the repo** against the fixture, and asserts it loads the solution and reports a project count (P3-03)
- [x] **Risk closed explicitly**: BuildHost payload **is** packaged correctly — see design.md Risks (resolved) and Code Reuse Analysis (added `Microsoft.CodeAnalysis.CSharp.Workspaces`, empirically required)
- [x] Smoke test cleans up the globally installed tool afterwards (`IAsyncLifetime`, uninstall in both `InitializeAsync` and `DisposeAsync`; verified via `dotnet tool list --global` post-run)
- [x] Gate check passes: `dotnet test`
- [x] Test count: 5 tests across `PackagingSmokeTests` + `CliArgumentValidationTests` (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `feat(cli): package as dotnet global tool and verify BuildHost payload`

---

### T5: Implement manifest model and loader

**What**: Define `Manifest`/`ManifestEntry` and load them from JSON with typed failures.
**Where**: `src/Csharp2Md.Core/Manifest/` (`Manifest.cs`, `ManifestLoader.cs`)
**Depends on**: T1
**Reuses**: None
**Requirement**: P1-16

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:serialization`

**Done when**:
- [x] `sealed record` types; `System.Text.Json` with a source-generated `JsonSerializerContext` (no reflection-based serialization)
- [x] Returns a domain-specific `ManifestLoadResult` — **deviates from this doc's `Result<Manifest, ManifestError>` sketch.** `dotnet-skills:csharp-coding-standards` explicitly says "don't build a generic `Result<T>` — each operation knows what success and failure look like"; per CLAUDE.md's precedence rule, the community pack wins for authoring style. `ManifestError` kept its `(Code, Message)` shape but `Code` is now `ManifestErrorCode` (enum), not `string` — matches the skill's own `OrderErrorCode` example ("type-safe and switchable"). An invalid manifest is still an expected error, never an exception.
- [x] Distinct typed errors for: file missing, malformed JSON, zero entries (P1-16)
- [x] Unit tests cover each failure mode and the happy path
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 8 tests in `Manifest` namespace (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(manifest): add manifest model and JSON loader with typed failures`

---

### T6: Implement ServiceDiscoverer with boundary resolution

**What**: Expand manifest globs to roots and resolve each root to exactly one service boundary.
**Where**: `src/Csharp2Md.Core/Discovery/ServiceDiscoverer.cs` (+ `ServiceCatalog.cs`)
**Depends on**: T5
**Reuses**: `Manifest` from T5
**Requirement**: P1-01, P1-02, P1-03, P1-04, P1-17, P1-18

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`

**Done when**:
- [x] Wildcard patterns expand to concrete directories (P1-01) — native `Directory.GetDirectories(parent, pattern)` on the last path segment; not a full multi-segment glob engine (documented limitation, degrades safely to P1-17's zero-match path)
- [x] Boundary precedence implemented and tested: manifest override → single `.sln` → loose `.csproj` files (P1-04, P1-02, P1-03)
- [x] Glob matching zero directories warns and continues (P1-17)
- [x] Duplicate roots processed once, with a warning (P1-18)
- [x] Unit tests cover every branch and all three listed edge cases, plus two undocumented edge cases found while implementing: multiple `.sln` files (ambiguous — warn and skip, neither P1-02 nor P1-03 literally covers it) and zero `.sln`/`.csproj` (warn and skip)
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 10 tests in `Discovery` namespace (no silent deletions)

> `ServiceName`/`PackageId` value objects (design.md assigns these to T10, a later phase) were pulled forward into this task — `ServiceDescriptor` needs them and T6 has no forward dependency on T10 per the phase graph. Same resolution pattern as T3's `SolutionLoader` signature. Files: `src/Csharp2Md.Core/ServiceName.cs`, `src/Csharp2Md.Core/PackageId.cs`. T10 should skip redefining them.
>
> `src/Csharp2Md.Core/Manifest/` was renamed to `Manifests/` in this task (namespace/type collision) — see design.md.
>
> `ServiceDescriptor.ProjectPaths` is now populated for the `Solution` boundary kind too (previously only `SolutionPath` was set, `ProjectPaths` stayed empty) — added `SolutionProjectPaths.cs`, parsing both `.slnx` (XML) and classic `.sln` (regex on the well-known `Project(...)` line format) to extract referenced `.csproj` paths. Lets `ProjectIdentityReader` (T7) iterate `service.ProjectPaths` uniformly without boundary-kind-specific logic. Verified against real `.slnx` and classic `.sln` content in tests, not just the flat-file no-solution-info case.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(discovery): resolve service boundaries from manifest roots`

---

### T7: Implement ProjectIdentityReader

**What**: Read each project's effective `PackageId` without Roslyn or MSBuild evaluation.
**Where**: `src/Csharp2Md.Core/Discovery/ProjectIdentityReader.cs`
**Depends on**: T6
**Reuses**: `ServiceDescriptor` from T6
**Requirement**: enables P2-04, P2-05

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [x] Reads explicit `<PackageId>` from the `.csproj` via plain XML read — Stage 1 stays cheap, no MSBuild evaluation
- [x] Falls back to `AssemblyName`, then to the project file name, matching SDK default precedence
- [x] Unit tests cover explicit id, both fallbacks, and a malformed project file
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 6 tests in `ProjectIdentityReaderTests` (no silent deletions)

> Return type is `IReadOnlyList<PackageId>` (value object), not `IReadOnlyList<string>` as sketched in design.md's Interfaces list — matches `ServiceDescriptor.PackageIds`' own type in the same doc's Data Models section, and preserves the whole point of `PackageId` as a value object (P2-04's rationale: prevents raw strings from being compared silently).

**Tests**: unit
**Gate**: quick

**Commit**: `feat(discovery): read project PackageId for internal-reference matching`

---

### T8: Implement ConfigIndexer

**What**: Parse `appsettings*.json` and `docker-compose.yml` under discovered roots into a logical-name index.
**Where**: `src/Csharp2Md.Core/Configuration/ConfigIndexer.cs`
**Depends on**: T6
**Reuses**: `ServiceDescriptor` from T6
**Requirement**: P2-06

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:serialization`

**Done when**:
- [x] Discovers and parses all `appsettings*.json` files under each service root (recursive, excluding `bin/`/`obj/`)
- [x] Parses `docker-compose.yml` service definitions (YamlDotNet, added via `dotnet add package`)
- [x] Malformed config files are skipped with a warning, never aborting the run
- [x] Unit tests cover both formats, multiple `appsettings.*` variants, and malformed input
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 7 tests in `ConfigIndexerTests` (no silent deletions)

> Index shape (not specified in design.md beyond "parse into a logical-name index"): `ConfigIndex(IReadOnlyDictionary<string,string> LogicalNames, IReadOnlySet<string> DockerComposeServiceNames)`. `appsettings*.json` is read via a `"Services": { "Name": "value" }` convention matching the T2 fixture (no established external convention exists for this — spec.md leaves the schema open). `docker-compose.yml` service names (the `services:` mapping's keys) are indexed as a set, not name→value, since Docker's built-in DNS-based service discovery makes the *name itself* the resolvable target — feeds T9's `Dynamic` classification (registry/discovery indirection, P2-08).

**Tests**: unit
**Gate**: quick

**Commit**: `feat(config): index appsettings and docker-compose for name resolution`

---

### T9: Implement ServiceNameResolver with resolution classification

**What**: Resolve a logical service name to a concrete service and classify how it resolved.
**Where**: `src/Csharp2Md.Core/Configuration/ServiceNameResolver.cs`
**Depends on**: T8
**Reuses**: config index from T8
**Requirement**: P2-07, P2-08, P2-09

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`

**Done when**:
- [x] Literal address in config → `HardCoded` (P2-07)
- [x] Env-var reference or registry/discovery indirection → `Dynamic` (P2-08)
- [x] No match → returns `Unresolved` **with the raw name preserved** so the caller can still record an edge (P2-09)
- [x] Implemented as a static pure function over the index (no hidden state)
- [x] Unit tests map 1:1 to P2-07, P2-08, P2-09
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 7 tests in `ServiceNameResolverTests` (no silent deletions)

> `ResolutionKind` enum (design.md assigns it to T10, a later phase, alongside the Graph types) pulled forward for the same reason as T6's `ServiceName`/`PackageId` — placed at `src/Csharp2Md.Core/ResolutionKind.cs`. T10 should skip redefining it.
>
> Signature is `Resolve(string logicalName, ConfigIndex index)` — no `ServiceCatalog`/`ServiceDescriptor` parameter, and no target-service lookup, despite design.md's combined T8+T9 sketch showing `Resolve(string) → (ServiceDescriptor?, ResolutionKind)`. T9's own Done-when list and Reuses field ("config index from T8" only, not the catalog) don't ask for target-service resolution — and doing it well would need a real mapping from a logical name / resolved address to a catalog entry that neither spec.md nor design.md defines (my own T2 fixture uses non-matching names on purpose: appsettings key `"PaymentService"` vs. catalog service `"Acme.Payments"` — they don't line up by string equality). Classification only, as literally scoped. Flagging this for whoever picks up the detector tasks (T15/T16): matching a resolved name back to a specific `ServiceDescriptor` needs an explicit design decision, not a guess.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(config): resolve logical service names with resolution classification`

---

### T10: Define the core domain model

**What**: Add value objects, dependency model, and load-health types per the design's Data Models section.
**Where**: `src/Csharp2Md.Core/Graph/` (`DependencySignal.cs`, `DependencyEdge.cs`, enums) and `src/Csharp2Md.Core/ServiceName.cs`, `PackageId.cs`
**Depends on**: T1
**Reuses**: None
**Requirement**: P2-12

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`

**Done when**:
- [x] `ServiceName` uses the **explicit-property form** with validation — not the primary-constructor + validating-ctor form, which does not compile (CS0111) — already satisfied by T6's forward-pulled `src/Csharp2Md.Core/ServiceName.cs`; T10 added the missing tests rather than redefining it
- [x] `PackageId`, `SourceLocation` as `readonly record struct`; all records `sealed`; collections exposed as `IReadOnlyList<T>`
- [x] `CommunicationType` enumerates exactly the **five** amended values including `DirectReference` (P2-12)
- [x] `DependencySignal` carries `Role` for messaging half-edges; `DependencyEdge` carries evidence locations
- [x] Unit tests assert `ServiceName` rejects null/empty/whitespace and that value equality holds
- [x] Gate check passes: `dotnet test --filter Category!=Integration`
- [x] Test count: 11 tests in `Graph/DomainModelTests.cs` (suite 41 → 52; no silent deletions)

> `ServiceName`, `PackageId`, and `ResolutionKind` were already pulled forward into T6/T9 and were **not** redefined here (see the notes under those tasks). Load-health types (`ProjectLoadStatus`, `ProjectLoadResult`, `LoadReport`) already exist from T3 and were likewise left alone. T10's net-new files are all under `src/Csharp2Md.Core/Graph/`: `DependencyKind.cs`, `CommunicationType.cs`, `MessagingRole.cs`, `SourceLocation.cs`, `DependencySignal.cs`, `DependencyEdge.cs` (which also holds `DependencyGraph`).

**Tests**: unit
**Gate**: quick

**Commit**: `feat(core): add domain model for services and dependency edges`

---

### T11: Implement MarkdownRenderer structural core

**What**: Render a C# document to structural Markdown with full member bodies, guarded by the span-coverage invariant.
**Where**: `src/Csharp2Md.Core/Rendering/MarkdownRenderer.cs`
**Depends on**: T10
**Reuses**: domain model from T10
**Requirement**: P1-11, P1-12

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:snapshot-testing`

**Done when**:
- [x] Emits: file heading → namespace + index backlink → preamble section (usings, file-level attributes/comments) → one section per type → one subsection per member
- [x] Member bodies emitted **verbatim and complete**, never summarized or truncated (P1-12)
- [x] XML doc comments rendered as prose
- [x] **Span-coverage invariant asserted as a real test** (not a snapshot): every byte of the source file maps to exactly one emitted section. Covers usings, inter-member code, `#region`, and top-level statements (AD-002)
- [x] Verify snapshot covers the emitted Markdown shape
- [x] Renderer is syntax-driven and functions with a null `SemanticModel`
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 32 tests across `Rendering/MarkdownRendererTests.cs` + `Rendering/SpanCoverageTests.cs` (suite 52 → 84; no silent deletions)

> **Span coverage is structural, not incidental.** The renderer partitions the file with Roslyn
> `FullSpan` boundaries (contiguous by construction across a syntax list) and emits any span the
> walk does not claim under a neutral "Additional source" title, so source can never be dropped
> silently. The test asserts the invariant three ways on six sources: spans are contiguous from 0
> to `text.Length`, concatenating section text reproduces the file byte-for-byte, and every
> section's text survives verbatim into the assembled Markdown.
>
> Two sub-decisions the design did not specify: (1) a member's XML doc comment is emitted **both**
> as prose and inside its verbatim code fence — the prose is additive, and excluding the trivia
> from the fence would have broken byte-for-byte reconstruction; (2) the code fence length is
> computed per document as one backtick longer than the longest run in the source, so a file
> containing ``` cannot escape its own fence (tested).
>
> Files beyond the `Where` field, all in `src/Csharp2Md.Core/Rendering/`: `RenderContext.cs`,
> `RenderedDocument.cs` (also holds `RenderedSection`, and owns Markdown assembly so T12 can
> re-assemble additively), `XmlDocProse.cs`. Same pattern as T3's `SolutionLoader.cs (+ LoadReport.cs)`.
>
> **Not emitted here: design.md's "detected-dependency section".** design.md's MarkdownRenderer
> structure list includes it, but it is P2-10 and depends on detectors that do not exist until
> Phase 3; T11's own Done-when list omits it. Left for the task that wires detectors into rendering.
>
> `Verify.Xunit` 31.12.5 added via `dotnet add package` (CPM). `tests/Csharp2Md.Core.Tests/VerifySetup.cs`
> disables DiffEngine so a snapshot mismatch fails the gate instead of opening a diff tool. The
> baseline was reviewed by hand and promoted from `.received.txt`; auto-accept is never enabled.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(rendering): render documents to structural Markdown with span coverage`

---

### T12: Add semantic enrichment decorator

**What**: Layer resolved namespace, base types, and implemented interfaces onto the structural renderer, degrading safely.
**Where**: `src/Csharp2Md.Core/Rendering/SemanticEnricher.cs`
**Depends on**: T11
**Reuses**: `MarkdownRenderer` from T11
**Requirement**: P1-11 (enrichment half of AD-002)

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-nullable-reference-types`

**Done when**:
- [x] Implemented as an **additive decorator** over T11's renderer — never woven into it (AD-002)
- [x] With a null or error-laden `SemanticModel`, falls back to source-as-written and **never throws**
- [x] Unit tests cover: healthy semantic model, null semantic model, and a compilation with unresolved types
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 7 tests in `Rendering/SemanticEnricherTests.cs` (suite 84 → 91; no silent deletions)

> Shape is `static RenderedDocument Enrich(RenderedDocument, RenderContext)` — a pure function over
> T11's output, not an interface implementation. `MarkdownRenderer` contains no reference to this
> type, so the decorator relationship is one-directional and deleting the call degrades output to
> source-as-written, which is what AD-002 asks for. An interface seam was not introduced because
> nothing needs to substitute the renderer.
>
> Degradation is handled by two explicit guards rather than a blanket `try/catch`: a null model or
> a model built over a different `SyntaxTree` returns the document unchanged (the latter is what
> would otherwise throw from `GetDeclaredSymbol`), and any base type or interface that resolves to
> an error symbol is dropped rather than reported. Verified against a compilation built with **no**
> metadata references, where every base and interface is an error symbol.
>
> Compiler-supplied bases (`object`, `ValueType`, `Enum`, `Delegate`, `MulticastDelegate`) are not
> reported — they are noise the author never wrote. `Interfaces` (directly declared) is used rather
> than `AllInterfaces` (inherited closure).
>
> Enrichment appends to `RenderedSection.Notes` and never rewrites `Span` or `Text`, so T11's
> span-coverage invariant is preserved by construction and asserted again here.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(rendering): add degradable semantic enrichment decorator`

---

### T13: Implement OutputWriter

**What**: Map documents to mirrored output paths, apply exclusions, and fully overwrite prior runs.
**Where**: `src/Csharp2Md.Core/Output/OutputWriter.cs`
**Depends on**: T11
**Reuses**: `RenderedDocument` from T11
**Requirement**: P1-11, P1-15

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-msbuild:check-bin-obj-clash`

**Done when**:
- [x] Output path mirrors the document's relative path within its service source tree (P1-11)
- [x] Excludes `obj/`, `bin/`, `*.g.cs`, `*.designer.cs`
- [x] `PrepareRun` deletes prior generated content so output always reflects the current run (P1-15)
- [x] Unit tests cover path mirroring, each exclusion rule, and full-overwrite behavior on a pre-populated directory
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 17 tests in `Output/OutputWriterTests.cs` (suite 91 → 108; no silent deletions)

> `outputRoot` moved from design.md's `PrepareRun(string outputRoot)` parameter to the constructor,
> so `Write(RenderedDocument doc)` keeps design.md's exact signature and both methods agree on one
> root by construction rather than by the caller passing the same string twice. `PrepareRun` deletes
> everything under that root, so the instance must be rooted at the **run's** output directory and
> `PrepareRun` called once before any service is processed.
>
> `Write` returns `string?` rather than `string`: `null` means the document was excluded and nothing
> was written, which the index builder needs to distinguish from a written path. Exclusion is also
> exposed as `static bool IsExcluded(string)` so the rule is testable without touching the disk.
>
> Exclusion matches `obj`/`bin` as whole **path segments** (case-insensitive) and only in directory
> position, not the file name. Tested explicitly against `Objects/Registry.cs`, `Binder/Setup.cs`,
> and `Orders/Designer.cs`, which a naive `Contains`/`EndsWith` rule would wrongly exclude.
>
> Output file naming is `Foo.cs` → `Foo.cs.md`, matching spec.md's own link example `[Foo.cs](./Foo.cs.md)`.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(output): write mirrored Markdown tree with full-overwrite semantics`

---

### T14: Define detector interfaces and communication classifier

**What**: Add the two detector contracts and the static classifier implementing the design's classification table.
**Where**: `src/Csharp2Md.Core/Detection/IDocumentDependencyDetector.cs`, `IProjectDependencyDetector.cs`, `CommunicationClassifier.cs`
**Depends on**: T10
**Reuses**: domain model from T10
**Requirement**: P2-12

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`, `dotnet-skills:csharp-type-design-performance`

**Done when**:
- [x] Two interfaces defined, splitting document-level from project-level detection (AD-004)
- [x] `DocumentDetectionContext` exposes a **nullable** `SemanticModel` so detectors degrade with the renderer
- [x] `CommunicationClassifier` is a static pure function implementing every row of the design's classification table
- [x] Unit tests map 1:1 to each classification-table row
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 11 tests across `Detection/CommunicationClassifierTests.cs` + `Detection/DetectorContractTests.cs` (suite 108 → 119; no silent deletions)

> `Classify(DependencyKind kind, CallShape shape)` — design.md names the table but not the signature.
> `CallShape` enumerates exactly the discriminators the table itself uses (`ResultConsumed`,
> `ResultDiscarded`, `Unary`, `Streaming`, `NotApplicable`), so the six rows map to six switch arms
> with nothing invented. A `(kind, shape)` pair no row covers **throws** rather than falling through
> to a default: a fabricated communication type would land in `dependencies.json` looking authoritative.
>
> The two context records live beside their interfaces rather than in files of their own.
> `ProjectDetectionContext` carries the `ServiceCatalog` (T18 matches `PackageId`s against it);
> `DocumentDetectionContext` deliberately does **not** — see the resolution note under T15.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(detection): add detector contracts and communication classifier`

---

### T15: Implement HttpClientDetector

**What**: Detect HTTP client calls and classify them as blocking or fire-and-forget.
**Where**: `src/Csharp2Md.Core/Detection/HttpClientDetector.cs`
**Depends on**: T14
**Reuses**: `CommunicationClassifier` from T14, `ServiceNameResolver` from T9
**Requirement**: P2-01

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [x] Detects `IHttpClientFactory.CreateClient(...)` and typed `HttpClient` usage
- [x] Awaited / result-consumed → `sincrono-bloqueante`; not awaited / result discarded → `assincrono-fire-and-forget` (P2-01)
- [x] Logical target name passed through `ServiceNameResolver`; unresolved names still produce a signal
- [x] Unit tests cover both classifications, all three resolution outcomes, and a null `SemanticModel`
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 14 tests in `Detection/HttpClientDetectorTests.cs` (suite 119 → 133; no silent deletions)

> **Decision: `TargetService` stays `null` on HTTP signals; catalog correlation is T19's job.**
> This closes the gap T9 flagged. `ServiceNameResolver.Resolve(string, ConfigIndex)` returns a
> `NameResolution(LogicalName, Kind)` and nothing else, and **no rule for mapping a logical name or
> a resolved address back to a `ServiceDescriptor` exists anywhere** in spec.md or design.md. The T2
> fixture proves the obvious guess wrong on purpose: the config key is `"PaymentService"` while the
> catalog service is `"Acme.Payments"`, so string equality resolves nothing and a substring or fuzzy
> match would be an invention that silently fabricates graph edges. design.md's own data model calls
> `DependencySignal.TargetService` "null until correlated/resolved", so null is the modelled state,
> not a shortcut. The detector therefore records `RawTarget` (the logical name) plus `Resolution`
> (P2-07/08/09), and correlation is left to `GraphBuilder`, the only component that holds the whole
> catalog. A `SPEC_DEVIATION` block on `HttpClientDetector` states the same thing at the call site.
>
> **T19 does not describe that matching step either.** Its Done-when list covers messaging
> correlation and says non-messaging signals "pass through with target and classification intact".
> Somebody must decide the name→catalog rule before P2-11's `dependencies.json` can name a real
> target service for HTTP/gRPC edges. Flagged, not guessed.
>
> Two detection paths with deliberately different degradation. `CreateClient("Name")` is recognised
> **syntactically** by method name and string-literal argument, so it keeps working with a null
> `SemanticModel`. A typed `HttpClient` call requires the model to prove the receiver's type really
> is `System.Net.Http.HttpClient`; without the model that path reports nothing rather than guessing
> from identifier names. Both behaviours are tested.
>
> Call shape climbs the surrounding expression chain rather than looking only at the direct parent,
> so `ConfigureAwait(false)` still reads as awaited and `.Result`/`.Wait()` read as blocking even
> though nothing is awaited. `_ = call(...)` reads as discarded. Target for a typed-client call is
> the request-URI literal when present, since a typed client exposes no logical name;
> such a URI is simply `Unresolved` against the config index, which is the P2-09 path.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(detection): detect HTTP client dependencies`

---

### T16: Implement GrpcClientDetector

**What**: Detect gRPC client calls and distinguish unary from streaming.
**Where**: `src/Csharp2Md.Core/Detection/GrpcClientDetector.cs`
**Depends on**: T14
**Reuses**: `CommunicationClassifier` from T14, `ServiceNameResolver` from T9
**Requirement**: P2-02

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [x] Detects generated gRPC client invocations
- [x] Unary → `sincrono-bloqueante`; duplex/streaming → `streaming-bidirecional` (P2-02)
- [x] Target resolved through `ServiceNameResolver` with the same unresolved-still-recorded behavior
- [x] Unit tests cover unary, streaming, and unresolved-target cases
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 11 tests in `Detection/GrpcClientDetectorTests.cs` (suite 133 → 144; no silent deletions)

> Same `TargetService = null` decision as T15, for the same reason and with the same
> `SPEC_DEVIATION` block on the detector. See the note under T15 for the full rationale.
>
> Recognition is **semantic only**: the receiver's type must genuinely derive from
> `Grpc.Core.ClientBase`, and the call shape comes from the return type (`AsyncUnaryCall` versus
> `AsyncServerStreamingCall` / `AsyncClientStreamingCall` / `AsyncDuplexStreamingCall`). Unlike
> T15's `CreateClient` path there is no syntactic fallback, because "a generated gRPC client" has
> no reliable name-only signature; with a null `SemanticModel` the detector reports nothing rather
> than guessing. Tested.
>
> Server streaming and client streaming both classify as `streaming-bidirecional`, matching the
> design table's single "gRPC streaming / duplex call" row. A generated **blocking** overload
> returns the response type directly rather than a call handle and is still unary.
>
> Target name is the proto service behind the generated client (`PaymentsClient` → `Payments`),
> since a generated client carries no other logical name. It is then classified by
> `ServiceNameResolver` like any other logical name.
>
> Tests declare stand-in `Grpc.Core` call types and a generated-client shape inside the test
> compilation rather than referencing `Grpc.AspNetCore`. The detector matches on
> namespace-qualified type names, so this exercises the real rule; it also sidesteps the fixture
> gap T9 flagged (the fixture has no gRPC *client* anywhere, only Payments exposing a service).

**Tests**: unit
**Gate**: quick

**Commit**: `feat(detection): detect gRPC client dependencies`

---

### T17: Implement MessagingDetector

**What**: Detect pub/sub publish and subscribe calls, emitting half-edge signals carrying topic and role.
**Where**: `src/Csharp2Md.Core/Detection/MessagingDetector.cs`
**Depends on**: T14
**Reuses**: `CommunicationClassifier` from T14
**Requirement**: P2-03

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [x] Emits a signal carrying the topic/message type name and `MessagingRole` (publish or subscribe) — **not** a finished edge (P2-03 as amended)
- [x] Communication type is `pub-sub-evento`
- [x] Unit tests cover publish, subscribe, and a document containing both
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 10 tests in `Detection/MessagingDetectorTests.cs` (suite 144 → 153; no silent deletions)

> Recognised call shapes were taken from the T2 fixture rather than invented: `PublishAsync(new
> OrderPlaced(...))` and `Subscribe<OrderPlaced>(handler)`. Verbs matched are `Publish`/`PublishAsync`
> and `Subscribe`/`SubscribeAsync`. `Send`/`SendAsync` are deliberately **not** matched — those
> usually carry commands, not events, and P2-03 scopes this to pub/sub.
>
> Topic resolution has three tiers, in order: an explicit type argument, an explicit `new T(...)`
> argument, then the semantic type of the first argument. The first two need no `SemanticModel`, so
> both fixture shapes keep working degraded. When only the semantic tier could answer and no model
> is present, the detector records **nothing** rather than a guessed topic — a wrong topic name
> would produce a false correlation in T19, which is worse than a missing edge. Tested both ways.
>
> `Resolution` is `Unresolved` at detection time, not `NotApplicable`: the counterpart lives in
> another service and is genuinely uncorrelated until Stage 3, and this is exactly the value P2-15
> requires for a signal that never finds a pair, so T19's unpaired path is a pass-through rather
> than a rewrite. T19 decides what a **paired** edge's resolution becomes.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(detection): detect messaging publish and subscribe signals`

---

### T18: Implement DirectReferenceDetector

**What**: Detect project references and internal package references, excluding public libraries.
**Where**: `src/Csharp2Md.Core/Detection/DirectReferenceDetector.cs`
**Depends on**: T14
**Reuses**: `PackageId` list from T7, `ServiceCatalog` from T6
**Requirement**: P2-04, P2-05

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [x] Project reference to another manifest service → `direct-reference` edge (P2-04)
- [x] Package reference whose `PackageId` matches another manifest service's `PackageId` → `direct-reference` edge (P2-04)
- [x] Package reference with **no** matching manifest service produces **no** edge — explicitly tested with a well-known public package (P2-05)
- [x] Implements `IProjectDependencyDetector`, not the document-level contract
- [x] Unit tests map 1:1 to P2-04 and P2-05, including the public-library exclusion
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 9 tests in `Detection/DirectReferenceDetectorTests.cs` (suite 153 → 162; no silent deletions)

> **This detector *does* populate `TargetService`, unlike T15/T16** — and the difference is the
> point. P2-04 states the matching rule outright (the referenced project file belongs to another
> manifest service, or the referenced package id equals another service's declared `PackageId`), so
> the target is derived from a specified rule rather than guessed. Where the spec supplies a rule the
> detector correlates; where it supplies none it records the raw target and leaves correlation alone.
>
> Reads the `.csproj` as plain XML with `LoadOptions.SetLineInfo`, matching `ProjectIdentityReader`'s
> no-MSBuild approach, so evidence points at the exact project-file line declaring the reference.
> A malformed or unreadable project file yields no edges instead of throwing — load health is
> already `SolutionLoader`'s job to report.
>
> Two rules the Done-when list implies but does not spell out: a reference whose target service is
> the **source service itself** produces no edge (internal structure, not a service dependency —
> tested), and project-path matching is case-insensitive on full paths so `..\` relative includes
> resolve correctly.
>
> `Resolution` is `NotApplicable`: nothing was resolved through config, since a compile-time
> reference names its target directly. This is the one signal kind where `ResolutionKind`'s
> `NotApplicable` member earns its place.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(detection): detect internal project and package references`

---

### T19: Implement GraphBuilder with messaging correlation

**What**: Turn accumulated signals into service-to-service edges, correlating messaging half-edges.
**Where**: `src/Csharp2Md.Core/Graph/GraphBuilder.cs`
**Depends on**: T18
**Reuses**: signals from T15-T18, `ServiceCatalog` from T6
**Requirement**: P2-14, P2-15

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`

**Done when**:
- [x] Publish of topic `T` in service A pairs with subscribe of `T` in service B → one edge `A → B`, directed publisher-to-subscriber (P2-14)
- [x] Unpaired publish or subscribe retained as an edge targeting the topic name, resolution `Unresolved` — **never dropped** (P2-15)
- [x] Non-messaging signals pass through with target and classification intact — **per AD-005**: `RawTarget` wrapped directly as `ServiceName`, no `ServiceCatalog` cross-matching
- [x] Evidence locations aggregated per edge
- [x] Unit tests cover: matched pair, unpaired publish, unpaired subscribe, multiple publishers of one topic
- [x] Gate check passes: `dotnet test --filter Category!=Integration`
- [x] Test count: 14 tests in `Graph/GraphBuilderTests.cs` (suite 180 → 193 quick; no silent deletions). The 14th was added at the Phase 4 CRAP gate, which found the role-less-messaging-signal arm uncovered — a real P2-15 ("never dropped") path, now asserted. Phase 4 finishes at 100% sequence coverage and CRAP 0.00 across all four components.

> **AD-005 applied**: a non-messaging signal's target is its already-resolved `RawTarget` wrapped as
> `ServiceName`, never cross-matched against `ServiceCatalog`. Because that leaves the catalog with
> nothing to do here, the `catalog` parameter in design.md's `Build(signals, catalog)` sketch is
> **omitted** rather than accepted and ignored — a parameter no code reads is dead API surface
> implying a correlation step this component does not perform. Signature is
> `static DependencyGraph Build(IReadOnlyList<DependencySignal>)`. A signal that already carries a
> `TargetService` (T18's direct references) still wins over `RawTarget`.
>
> **Spec-precision gap, decided: a *correlated* pub/sub edge's resolution is `NotApplicable`.**
> P2-14 does not state one. P2-07/08/09 classify how a *logical name resolved against config*, and a
> correlated edge never went through config — it was matched on topic name, the same reason T18 uses
> `NotApplicable` for a compile-time reference. Specifically **not** `Unresolved`, which P2-15
> reserves for a signal that found no counterpart. Asserted, not left implicit.
>
> Two rules P2-14 implies but does not spell out: pairing requires the counterpart to be in
> **another** service, so a service that both publishes and subscribes to one topic produces no
> self-edge and both halves stay unpaired (tested); and only the publish half emits the correlated
> edge, since the subscribe half sees the same pairing from the other side and would duplicate it.
>
> Edges that describe the same relationship (same source, target, communication type, and
> resolution) merge into one carrying every evidence location, and the edge list is ordered by
> source → target → communication → resolution so T20/T21's artifacts are byte-stable across runs
> rather than dependent on document visit order.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(graph): build dependency graph with messaging correlation`

---

### T20: Implement DependencyJsonWriter

**What**: Serialize the graph to `dependencies.json`.
**Where**: `src/Csharp2Md.Core/Output/DependencyJsonWriter.cs`
**Depends on**: T19
**Reuses**: `DependencyGraph` from T19
**Requirement**: P2-11, P2-12

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:serialization`, `dotnet-skills:snapshot-testing`

**Done when**:
- [x] Every edge serialized with source, target, communication type, and resolution (P2-11) — plus `evidence`, per design.md's DependencyJsonWriter line
- [x] All five communication-type values round-trip correctly, `direct-reference` included (P2-12)
- [x] Source-generated `JsonSerializerContext`; no reflection-based serialization
- [x] Verify snapshot of the emitted JSON, plus explicit asserts on required fields
- [x] Gate check passes: `dotnet test --filter Category!=Integration`
- [x] Test count: 15 tests in `Output/DependencyJsonWriterTests.cs` (suite 192 → 207 quick; no silent deletions)

> Enum values are written as **spec.md's own spellings** (`sincrono-bloqueante`, `direct-reference`,
> `hard-coded`, …), not as C# identifiers and not as ordinals — `dependencies.json` is the tool's
> contract with whoever reads it, and an ordinal silently changes meaning the day a value is
> inserted into the enum. Achieved with `JsonStringEnumConverter<T>(JsonNamingPolicy.KebabCaseLower)`
> subclasses applied per property, so T10's enums stay untouched (out of this task's `Where`) and
> source generation still applies.
>
> `Deserialize` exists because the Done-when says values must **round-trip**, which needs a reader;
> it is the only production API here that no v1 caller uses yet. Evidence strings are split on the
> **last** colon so a Windows drive letter stays part of the path (tested).
>
> Field name is `communicationType`, matching P2-11's wording ("communication type") rather than the
> shorter `communication` the domain record uses.
>
> **Observed, not fixed (out of scope, T10's file):** `DependencyEdge`'s record equality compares
> `Evidence` by reference because it is an `IReadOnlyList`, so two edges with identical evidence
> are unequal. The round-trip test compares fields explicitly for this reason. Worth a look if a
> later task ever relies on `DependencyEdge` equality.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(output): serialize dependency graph to dependencies.json`

---

### T21: Implement MermaidWriter

**What**: Generate the Mermaid diagram from the same graph object.
**Where**: `src/Csharp2Md.Core/Output/MermaidWriter.cs`
**Depends on**: T19
**Reuses**: `DependencyGraph` from T19
**Requirement**: P2-13

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:snapshot-testing`

**Done when**:
- [x] Diagram derived from the graph object, **every** edge labeled with its communication type (P2-13)
- [x] Service names containing Mermaid-significant characters are escaped so the diagram stays valid
- [x] Verify snapshot covers a graph exercising all five communication types
- [x] Gate check passes: `dotnet test --filter Category!=Integration`
- [x] Test count: 13 tests in `Output/MermaidWriterTests.cs` (suite 207 → 220 quick; no silent deletions)

> Node identifiers are **generated** (`svc0`, `svc1`, …) rather than derived from service names.
> Under AD-005 a target can be a raw address or a topic name (`http://payments:8080/api`), whose
> dots, colons and slashes Mermaid will not accept as an identifier. The readable name lives in the
> quoted label, where it only has to survive escaping. Declaration order follows the edge list the
> `GraphBuilder` already sorted, so the diagram is byte-stable across runs.
>
> Escaping covers `"` (would close the label early) and `#` (opens a Mermaid entity), `#` first so
> the `"` replacement is not re-encoded; newlines are flattened. Both are asserted, including a
> negative assertion that the raw quoted text is gone.
>
> Labels come from `JsonNamingPolicy.KebabCaseLower` — the **same policy** T20 serializes with —
> rather than a second hand-written table, so the diagram and `dependencies.json` cannot drift into
> two vocabularies. Diagram file is `dependencies.mmd` (spec.md names no filename).

**Tests**: unit
**Gate**: quick

**Commit**: `feat(output): generate labeled Mermaid dependency diagram`

---

### T22: Implement IndexWriter

**What**: Write the per-service and root index files with relative links.
**Where**: `src/Csharp2Md.Core/Output/IndexWriter.cs`
**Depends on**: T13
**Reuses**: written-path list from T13
**Requirement**: P1-13, P1-14

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:snapshot-testing`

**Done when**:
- [x] One `index.md` per service linking every generated file for that service (P1-13)
- [x] One root `index.md` linking every per-service index (P1-14)
- [x] All links relative, so output stays portable on GitHub, in an IDE, or fed to an LLM
- [x] Verify snapshots for both index shapes
- [x] Gate check passes: `dotnet test --filter Category!=Integration`
- [x] Test count: 9 tests in `Output/IndexWriterTests.cs` (suite 220 → 229 quick; no silent deletions)

> The per-service index is written **at the service's own output root**, which is the location
> T11's `MarkdownRenderer.IndexLink` already points every document's backlink at (`../` per path
> depth). This pins the output layout the pipeline must use: one folder per service beneath the run
> output root, so an `OutputWriter` is constructed per service rooted at `<output>/<service>`.
>
> Link style follows spec.md's own example `[Foo.cs](./Foo.cs.md)`: link text is the **source** file
> path, the href keeps `.md`. Separators are normalized to `/` and every link is prefixed `./` —
> a Windows-style backslash link is dead on GitHub, which the portability assumption rules out.
> Entries are sorted (ordinal) so both indexes are byte-stable across runs.
>
> A service whose documents were all excluded still gets an index file, so the root index can never
> link at a file that does not exist.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(output): write per-service and root index files`

---

### T23: Render the detected-dependencies section into documents

**What**: Wire per-document signals into the rendered Markdown's "Dependências detectadas" section.
**Where**: `src/Csharp2Md.Core/Rendering/DependencySectionRenderer.cs` (+ `MarkdownRenderer` integration)
**Depends on**: T11, T18
**Reuses**: `MarkdownRenderer` from T11, detectors from T15-T18
**Requirement**: P2-10

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:snapshot-testing`

**Done when**:
- [x] Section lists communication type, target, and resolution for each dependency (P2-10)
- [x] Messaging signals use the **topic/message type name** as target, since correlation happens later in Stage 3 (P2-10 as amended)
- [x] Documents with no detected dependencies omit the section entirely
- [x] Span-coverage invariant from T11 still holds with the section present
- [x] Unit tests + Verify snapshot cover HTTP, gRPC, messaging, direct-reference, and the empty case
- [x] Gate check passes: `dotnet test --filter Category!=Integration`
- [x] Test count: 15 tests in `Rendering/DependencySectionRendererTests.cs` (suite 230 → 245 quick; no silent deletions)

> **The section is attached outside `RenderedDocument.Sections`, not as another `RenderedSection`.**
> A `RenderedSection` owns a span of the source file, and this section maps to **no source bytes at
> all**; a zero-span entry in that list would break AD-002's span-coverage invariant outright. It
> travels as a separate `string? DependencySection` property that `ToMarkdown` emits between the
> index backlink and the preamble — design.md's exact document order. The invariant is re-asserted
> here over all seven of T11's sources with the section present, reusing `SpanCoverageTests.Sources`.
>
> Shape is `Apply(RenderedDocument, IReadOnlyList<DependencySignal>) → RenderedDocument`, the same
> additive-pure-function form as T12's `SemanticEnricher.Enrich`. Skipping the call degrades to a
> document with no dependency section rather than breaking anything.
>
> **Beyond the `Where` field:** `Rendering/RenderedDocument.cs` (T11's file) gained the
> `DependencySection` init-only property and the four lines of `ToMarkdown` that emit it. There is
> no way to place the section in the output without the type that owns Markdown assembly knowing
> about it; the alternative was string surgery on assembled Markdown. `MarkdownRenderer.cs` itself
> is untouched.
>
> Target is `TargetService?.Value ?? RawTarget`, which needs no messaging special case: a messaging
> half-edge has no `TargetService` until Stage 3, so the topic name is simply what there is to show
> (P2-10 as amended), while T18's direct references show the real correlated service.
>
> Labels reuse `JsonNamingPolicy.KebabCaseLower`, the same policy T20 and T21 use, so one dependency
> reads identically in the document, in `dependencies.json`, and in the diagram.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(rendering): render detected-dependencies section per document`

---

### T24: Implement AnalysisPipeline orchestrator

**What**: Wire all three stages into one sequential run.
**Where**: `src/Csharp2Md.Core/Pipeline/AnalysisPipeline.cs`
**Depends on**: T23
**Reuses**: T3 loader, T9 resolver, T13 writer, T19 graph builder, T20-T22 writers
**Requirement**: P1-19

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-concurrency-patterns`

**Done when**:
- [x] Stage 1 fully completes before any detector runs (internal-package matching and name resolution require the complete catalog) — asserted by a direct-reference edge to `Acme.Shared.Contracts`, deliberately the **last** manifest entry, produced while the **first** service is analyzed
- [x] **Sequential-workspace invariant asserted by test**: never more than one `MSBuildWorkspace` alive at a time; each is disposed before the next opens (P1-19) — exact `open/close` event sequence over a real two-service run
- [x] Rendered documents written and discarded per document — no whole-codebase model retained (AD-001) — asserted by observing service A's `.md` already on disk at the moment service B's workspace opens
- [x] `CancellationToken` accepted and honored throughout — pre-cancelled token throws and loads zero services
- [x] Integration test runs the full pipeline against the fixture and asserts the generated artifact set
- [x] Gate check passes: `dotnet test`
- [x] Test count: 14 tests across `Pipeline/AnalysisPipelineTests.cs` + `Pipeline/AnalysisPipelineInvariantTests.cs` (suite 259 → 273; no silent deletions)

> **The loader is injected as a `SolutionLoadFunc` delegate, not called directly.** P1-19 is
> unobservable from outside otherwise: `SolutionLoader` owns its workspace inside `LoadAsync`
> (`using var workspace`, single call site — structural, verified in T3), so "one workspace at a
> time" is exactly "no two `LoadAsync` calls overlap", and only a seam can witness that. The
> parameterless constructor wires the real `SolutionLoader`, so production has no substitution.
> An interface was not introduced because that would mean editing T3's file, outside this task.
>
> **Synthesizing a solution for a service that has no solution file happens here, not in
> `ServiceDiscoverer`.** design.md assigns it to T6, but T6 shipped without it (`SolutionPath` is
> `null` for `LooseProjects` and `ManifestOverride` boundaries), and T6 is outside this task's
> `Where`. The pipeline is the first component that actually needs a solution path and is the one
> that can own the temp file's lifetime, so it writes a `.slnx` with absolute project paths into a
> temp directory and deletes it after the load. P1-05 therefore holds uniformly: one
> `OpenSolutionAsync` per service, no `OpenProjectAsync` anywhere. Absolute paths in a synthesized
> `.slnx` were **verified empirically** against the fixture, not assumed.
>
> **Stage 1 drops project paths that do not exist on disk, with a warning.** The fixture's
> `Acme.Orders.slnx` deliberately references `Acme.DoesNotExist` (added in T3 for P1-06), and
> `ProjectIdentityReader` (T7) only catches `XmlException` — a missing file throws
> `FileNotFoundException` straight out of `XDocument.Load`. Filtering in Stage 1 is P1-06's own rule
> ("skipped rather than aborting") applied before Roslyn is involved, and keeps `ProjectPaths` a
> single coherent list. **Observed, not fixed (out of scope, T7's file):** `ProjectIdentityReader`
> is unguarded against a missing project file.
>
> Two rules the Done-when list implies but does not spell out, both forced by the fixture:
> a project pulled into the workspace **transitively** (by a project reference) is skipped unless it
> is one of the service's own `ProjectPaths` — otherwise a shared library's documents would be
> rendered once per service that references it; and a document whose path **escapes** the service
> root (`Acme.Orders.slnx` reaches into sibling directories) is nested under its owning project's
> name rather than written with a `..` path, which would place output outside the run's output
> directory entirely. Both are asserted.
>
> Manifest loading runs inside the pipeline rather than in the CLI, matching design.md's own Stage-1
> diagram, and returns `PipelineRunResult.Failed(ManifestError)` **before** `PrepareRun` — so P1-16's
> "without writing any output" is a property of the pipeline, not of the caller. The CLI (T26) maps
> that failure to an exit code.

**Tests**: integration
**Gate**: full

**Commit**: `feat(pipeline): orchestrate inventory, analysis, and aggregate stages`

---

### T25: Implement RunReporter

**What**: Print the end-of-run summary of degraded and possible-missing-restore projects.
**Where**: `src/Csharp2Md.Core/Pipeline/RunReporter.cs`
**Depends on**: T24
**Reuses**: `LoadReport` from T3
**Requirement**: P1-10

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [x] Lists every project marked degraded or possible-missing-restore, with a `dotnet restore` suggestion for each (P1-10)
- [x] Projects classified unsupported-for-compilation are **not** reported as failures (P1-09 consistency) — neither listed nor counted as needing attention
- [x] A fully healthy run prints a clean summary with no false warnings
- [x] Unit tests cover: degraded only, missing-restore only, both, and clean
- [x] Gate check passes: `dotnet test --filter Category!=Integration`
- [x] Test count: 6 tests in `Pipeline/RunReporterTests.cs` (quick suite 245 → 251; no silent deletions)

> Shape is `static string Summarize(LoadReport)` — it builds the summary, it does not print it.
> design.md's component line says "→ console", but a reporter that writes to `Console` can only be
> tested by capturing console state, which is shared and order-dependent across a parallel test run.
> The CLI (T26) does the single `Console.Write`. No `TextWriter` overload was added: nothing needs
> a second sink.
>
> **Spec-precision gap, decided: P1-10 does not define the summary's wording.** The exact text is
> asserted line-for-line here rather than loosely matched, so the format is pinned by tests and the
> e2e assertion in T26 has something stable to check. Clean runs print
> `Run summary: N project(s) loaded, none degraded or missing a restore.`, which also reads correctly
> when a manifest resolved zero services (P1-17's path) without a special case for it.
>
> `Describe` throws on a status the summary never lists, rather than falling through to a default —
> same rule as T14's classifier, for the same reason: an invented label in an operator-facing summary
> is worse than a loud failure. It is unreachable by construction (guarded by `NeedsAttention`).

**Tests**: unit
**Gate**: quick

**Commit**: `feat(pipeline): report degraded and unrestored projects at end of run`

---

### T26: Wire the CLI end to end with exit-code contract

**What**: Connect the CLI to the full pipeline and lock in the exit-code behavior with e2e tests.
**Where**: `src/Csharp2Md.Cli/Program.cs`, `tests/Csharp2Md.Core.Tests/Cli/EndToEndTests.cs`
**Depends on**: T25
**Reuses**: CLI skeleton from T4, pipeline from T24, reporter from T25
**Requirement**: P3-05, P1-16

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:slopwatch`, `dotnet-test:assertion-quality`, `dotnet-test:test-gap-analysis`

**Done when**:
- [x] Valid `--manifest` + `--output` runs the full pipeline and exits `0` (P3-05)
- [x] **Exits `0` even when projects were degraded** — a degraded-but-completed run is a success (P3-05) — asserted against `Acme.Broken`'s genuinely-unrestorable project
- [x] Invalid manifest exits non-zero with a specific message and writes no output (P1-16)
- [x] BuildHost unable to resolve `dotnet` produces an actionable message, not a raw exception — reproduced for real by overriding `PATH`/`DOTNET_ROOT` to a directory with no `dotnet`, per `ProcessRunner`'s environment-override contract; asserted stderr contains the actionable message and neither `"Unhandled exception"` nor a raw stack trace frame
- [x] E2E test runs the tool against the fixture and asserts the full artifact set: mirrored `.md` tree, per-service and root `index.md`, `dependencies.json`, Mermaid diagram, and the console summary
- [x] E2E test asserts the fixture's known dependency set matches `dependencies.json` exactly (the P1 and P2 Independent Tests, made executable) — 6 known edges asserted by exact (source, target, communication, resolution) tuple: HTTP/hard-coded, gRPC/unresolved, messaging/correlated, messaging/unpaired (P2-15), and both direct-reference edges
- [x] Gate check passes: `dotnet build -c Release` → `dotnet format --verify-no-changes` → `dotnet test`
- [x] Test count: 24 tests across `Cli/EndToEndTests.cs` (new, 4 tests) + `Cli/CliArgumentValidationTests.cs` (4) + `Cli/PackagingSmokeTests.cs` (1) + pre-existing Cli tests (suite 279 → 283; no silent deletions)

> **Fixed 2 pre-existing tests broken by T24's own wiring, not by this task's new code.** `CliArgumentValidationTests.Run_WithValidArguments_ExitsZeroAndReportsProjectCount` and `PackagingSmokeTests.PackedTool_RunFromOutsideRepo_...` were written in T4 (Phase 0), before `AnalysisPipeline`/`ManifestLoader` existed, and passed a raw `.slnx` path as `--manifest`. Once T24 wired the real pipeline (this task's own dependency), `--manifest` genuinely means "a `manifest.json`" (P1-16) — the old shortcut now fails with `Manifest is not valid JSON: '<' is an invalid start of a value`, since XML doesn't parse as JSON. Fixed both to build a real `manifest.json` via `FixtureManifest.WriteRoots` (T24's own helper, reused rather than duplicated) targeting `Acme.Orders`'s automatic `.sln` heuristic (P1-02) — same 3-project count (`Acme.Orders`, `Acme.Shared.Contracts`, `Acme.Broken`) as the original assertion, just reached the spec-correct way. Not a "weaken the test" case: this is a genuine contract change from an earlier phase's temporary shortcut to the real one, corroborated by the spec's own CLI invocation shape (spec.md Assumptions: `--manifest <path-to-manifest.json>`).
>
> T2's known gRPC-client gap (flagged since Phase 0/1, see STATE.md) is closed here: `fixtures/SyntheticSolution/Acme.Orders/PaymentsGrpcClient.cs` declares `Grpc.Core.ClientBase` in-fixture (same reasoning as `IEventBus` standing in for a broker — the fixture must stay restorable without new external dependencies) and `OrderService.AuthorizePaymentAsync` calls its unary RPC. `GrpcClientDetector` (T16) matches on the namespace-qualified base type, so this exercises the real detection rule. The detected target (`Payments`, the proto service name) matches no config entry, so per **AD-005** the edge is `sincrono-bloqueante` / `unresolved` rather than resolved to `Acme.Payments`.
>
> Session note: this task was executed across three attempts due to external usage limits unrelated to the code (a sub-agent session limit, then a monthly spend limit) — see `.specs/STATE.md` Handoff for the full account. T24/T25 (this phase's earlier tasks) completed cleanly on the second attempt; T26 itself was finished directly rather than via a further sub-agent dispatch once the fixture and the two broken pre-existing tests were diagnosed.

**Tests**: integration
**Gate**: build

**Commit**: `feat(cli): wire end-to-end run with exit-code contract`

---

## Phase Execution Map

Phases run in sequence: Phase 0 → Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5.

Full edge list (one edge per line; cross-phase edges always point backward):

```
T1 → T2
T2 → T3
T3 → T4
T1 → T5
T5 → T6
T6 → T7
T6 → T8
T8 → T9
T1 → T10
T10 → T11
T11 → T12
T11 → T13
T10 → T14
T14 → T15
T14 → T16
T14 → T17
T14 → T18
T18 → T19
T19 → T20
T19 → T21
T13 → T22
T11 → T23
T18 → T23
T23 → T24
T24 → T25
T25 → T26
```

Execution is strictly sequential - there is no intra-phase parallelism.

**Batch packing (26 tasks):**

| Batch | Phases | Tasks | Character |
| --- | --- | --- | --- |
| 1 | Phase 0 + Phase 1 | T1-T9 (9) | Mixed: risk spike + mechanical inventory |
| 2 | Phase 2 + Phase 3 | T10-T18 (9) | Core domain: renderer fidelity + detection |
| 3 | Phase 4 + Phase 5 | T19-T26 (8) | Aggregation + integration |

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: Scaffold repo | build config (cohesive unit) | ✅ Granular |
| T2: Fixture solution | test data (cohesive unit) | ✅ Granular |
| T3: SolutionLoader | 1 component | ✅ Granular |
| T4: Packaging + smoke | 1 concern (packaging) | ✅ Granular |
| T5: ManifestLoader | 1 component | ✅ Granular |
| T6: ServiceDiscoverer | 1 component | ✅ Granular |
| T7: ProjectIdentityReader | 1 component | ✅ Granular |
| T8: ConfigIndexer | 1 component | ✅ Granular |
| T9: ServiceNameResolver | 1 component | ✅ Granular |
| T10: Domain model | 1 cohesive model set | ✅ Granular |
| T11: MarkdownRenderer core | 1 component | ✅ Granular |
| T12: SemanticEnricher | 1 component | ✅ Granular |
| T13: OutputWriter | 1 component | ✅ Granular |
| T14: Detector contracts + classifier | 1 cohesive contract set | ✅ Granular |
| T15: HttpClientDetector | 1 component | ✅ Granular |
| T16: GrpcClientDetector | 1 component | ✅ Granular |
| T17: MessagingDetector | 1 component | ✅ Granular |
| T18: DirectReferenceDetector | 1 component | ✅ Granular |
| T19: GraphBuilder | 1 component | ✅ Granular |
| T20: DependencyJsonWriter | 1 component | ✅ Granular |
| T21: MermaidWriter | 1 component | ✅ Granular |
| T22: IndexWriter | 1 component | ✅ Granular |
| T23: DependencySectionRenderer | 1 component | ✅ Granular |
| T24: AnalysisPipeline | 1 component | ✅ Granular |
| T25: RunReporter | 1 component | ✅ Granular |
| T26: CLI wiring | 1 entry point | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | (start of Phase 0) | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T1 (prior phase) | (start of Phase 1) | ✅ Match |
| T6 | T5 | T5 → T6 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T6 | T6 → T8 | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T1 (prior phase) | (start of Phase 2) | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T11 | T11 → T13 | ✅ Match |
| T14 | T10 (prior phase) | (start of Phase 3) | ✅ Match |
| T15 | T14 | T14 → T15 | ✅ Match |
| T16 | T14 | T14 → T16 | ✅ Match |
| T17 | T14 | T14 → T17 | ✅ Match |
| T18 | T14 | T14 → T18 | ✅ Match |
| T19 | T18 (prior phase) | (start of Phase 4) | ✅ Match |
| T20 | T19 | T19 → T20 | ✅ Match |
| T21 | T19 | T19 → T21 | ✅ Match |
| T22 | T13 (prior phase) | (standalone in Phase 4) | ✅ Match |
| T23 | T11, T18 (prior phases) | (start of Phase 5) | ✅ Match |
| T24 | T23 | T23 → T24 | ✅ Match |
| T25 | T24 | T24 → T25 | ✅ Match |
| T26 | T25 | T25 → T26 | ✅ Match |

No dependency points to a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Build config | none | none | ✅ OK |
| T2 | Fixture projects | none | none | ✅ OK |
| T3 | Roslyn loading | integration | integration | ✅ OK |
| T4 | CLI end-to-end | integration | integration | ✅ OK |
| T5 | Domain logic | unit | unit | ✅ OK |
| T6 | Domain logic | unit | unit | ✅ OK |
| T7 | Domain logic | unit | unit | ✅ OK |
| T8 | Domain logic | unit | unit | ✅ OK |
| T9 | Domain logic | unit | unit | ✅ OK |
| T10 | Domain logic | unit | unit | ✅ OK |
| T11 | Renderer | unit | unit | ✅ OK |
| T12 | Renderer | unit | unit | ✅ OK |
| T13 | Output writers | unit | unit | ✅ OK |
| T14 | Domain logic | unit | unit | ✅ OK |
| T15 | Domain logic | unit | unit | ✅ OK |
| T16 | Domain logic | unit | unit | ✅ OK |
| T17 | Domain logic | unit | unit | ✅ OK |
| T18 | Domain logic | unit | unit | ✅ OK |
| T19 | Domain logic | unit | unit | ✅ OK |
| T20 | Output writers | unit | unit | ✅ OK |
| T21 | Output writers | unit | unit | ✅ OK |
| T22 | Output writers | unit | unit | ✅ OK |
| T23 | Renderer | unit | unit | ✅ OK |
| T24 | Pipeline orchestration | integration | integration | ✅ OK |
| T25 | Domain logic | unit | unit | ✅ OK |
| T26 | CLI end-to-end | integration | integration | ✅ OK |

No violations. No task defers its tests to another task.

---

## Validator Warnings - Reviewed and Accepted

`validate_tasks.py` exits **0 errors, 10 warnings**. Each warning was reviewed against the granularity rule ("2-3 related things in the same cohesive unit = OK"). None is a genuine smell; splitting any of them would produce tasks that cannot be independently tested or committed.

| Warning | Task(s) | Why accepted |
| --- | --- | --- |
| `Tests: none` | T1, T2 | Matrix explicitly says `none` for build config and fixture projects — build gate only. Not test deferral: neither task creates production logic. |
| Multiple files: build config | T1 | One deliverable — a working build. Splitting `.slnx` from `Directory.Build.props` yields tasks that cannot compile or be gated independently. |
| Multiple files: component + its model | T3, T5, T6 | Each pairs a component with the small record it owns and returns (`LoadReport`, `Manifest`, `ServiceCatalog`). The model has no meaning or test without its producer. |
| Multiple files: packaging | T4, T26 | `.csproj` + `Program.cs` + its test form one packaging/wiring concern. The test is co-located by design, not a separate unit. |
| Multiple files: domain model set | T10 | Small cohesive value objects and records introduced together. Five separate tasks for five tiny types would be ceremony, and each would carry a near-empty gate. |
| Multiple files: contract set | T14 | Two sibling interfaces plus the classifier they share. The split into document-level and project-level detection is exactly AD-004's shape. |

---

## Requirement Coverage

All 39 spec criteria map to at least one task.

| Requirement | Task(s) |
| --- | --- |
| P1-01, P1-02, P1-03, P1-04 | T6 |
| P1-05, P1-06, P1-07, P1-08, P1-09 | T3 |
| P1-10 | T25 |
| P1-11 | T11, T13 |
| P1-12 | T11 |
| P1-13, P1-14 | T22 |
| P1-15 | T13 |
| P1-16 | T5, T26 |
| P1-17, P1-18 | T6 |
| P1-19 | T24 |
| P2-01 | T15 |
| P2-02 | T16 |
| P2-03 | T17 |
| P2-04, P2-05 | T18 |
| P2-06 | T8 |
| P2-07, P2-08, P2-09 | T9 |
| P2-10 | T23 |
| P2-11 | T20 |
| P2-12 | T10, T14, T20 |
| P2-13 | T21 |
| P2-14, P2-15 | T19 |
| P3-01, P3-02, P3-03, P3-04 | T4 |
| P3-05 | T26 |
