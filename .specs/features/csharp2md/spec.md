# csharp2md (v1) Specification

## Problem Statement

A C#/.NET microservices codebase is expensive to navigate: understanding what a service does and which other services it talks to means opening an IDE, loading multiple solutions, and manually tracing HTTP clients, gRPC calls, and messaging code across repositories. `csharp2md` is a standalone `dotnet global tool` that uses Roslyn (`MSBuildWorkspace`) to convert a C#/.NET codebase into full-fidelity Markdown (one file per source file) and a consolidated, typed dependency graph between services — so the codebase becomes readable both by humans and by LLM tooling without an IDE.

## Goals

- [ ] Given a manifest of service roots, produce one Markdown file per source file, mirroring the original folder structure, with full method-body fidelity (not summarized)
- [ ] Detect HTTP/gRPC, messaging (pub/sub), and internal direct-reference dependencies between services, resolving logical service names via `appsettings*.json`/`docker-compose.yml` where possible
- [ ] Produce a consolidated `dependencies.json` and a Mermaid diagram of the service graph, with every edge labeled by communication type
- [ ] Ship as an installable `dotnet global tool` runnable from any directory against any C#/.NET codebase
- [ ] Degrade gracefully against real-world codebases: a project that fails to load or appears to be missing a NuGet restore must not abort the whole run

## Out of Scope

Explicitly excluded from v1. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Shared-database dependency detector (the "database per service" anti-pattern signal) | Different detection mechanism (compares config, not code); inflates v1 scope. Decided candidate for v2. |
| Token-consumption study (raw codebase vs. generated Markdown) | Deferred personal research question, unrelated to building the tool itself. |
| Embedded token counting in the tool's output | Same as above — deferred to v2, would be built once the token study exists. |
| Dynamic/external plugin architecture for dependency detectors | No third-party consumer without source access exists yet; only the author writes detectors. `IDependencyDetector` interface is kept clean so this remains possible later without a redesign. |
| Automatic `dotnet restore` execution | `MSBuildWorkspace` has no native restore hook (confirmed via source + closed upstream PRs); auto-restoring would couple the tool to a network/NuGet-feed step. The tool detects and reports likely-missing restores instead (see LOAD-06/07/08). |
| Automatic detection of multiple bounded contexts inside a single `.sln` (modular monolith case) | The `.sln`-per-service heuristic doesn't capture this. Documented limitation; manual manifest override (DISC-03) is the only workaround in v1. |
| Languages other than C#/.NET | Roslyn only covers C#/.NET; out of scope by construction. |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here — nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Manifest file format | JSON | Consistent with `dependencies.json` output and the `appsettings*.json` convention the tool already parses; easy to extend with override fields later | y |
| Output-directory rerun behavior | Full overwrite/regenerate on every run | Output must always exactly reflect current source state; avoids stale-file drift; simplest to implement and reason about | y |
| Service processing concurrency | Sequential (one service at a time) | Documented Roslyn regression (400–800% slower opening solutions in certain version ranges) and real-world reports of high memory use on large solutions make stacking multiple `MSBuildWorkspace` instances concurrently a real risk; sequential is the safe v1 default | y |
| "Internal" NuGet package identification | Match by `PackageId`: a referenced package is internal if its `PackageId` equals the `PackageId` declared in another manifest service's own `.csproj` | Zero extra config, self-maintaining as services are added to the manifest | y |
| Generated/build-output file exclusion | Exclude `obj/`, `bin/`, and files matching `*.g.cs`/`*.designer.cs` from Markdown generation | These aren't meaningful documentation targets; including them adds noise without value and duplicates hand-written source | y |
| CLI invocation shape | `csharp2md --manifest <path-to-manifest.json> --output <path-to-output-dir>`, both required | No existing convention to match (greenfield CLI); mirrors common dotnet-tool CLI shape (named required options), self-documenting and order-independent | y |
| Diagram/document link style | Generated `.md` files use relative Markdown links (`[Foo.cs](./Foo.cs.md)`) for intra-service links and index links | Keeps generated output portable (viewable on GitHub, in an IDE, or fed to an LLM) without needing a server | y |

**Open questions:** none — all resolved or logged above.

---

## User Stories

### P1: Browse a codebase as Markdown ⭐ MVP

**User Story**: As a developer working on a multi-service .NET codebase, I want to run the tool against a manifest of service roots and get one Markdown file per source file (plus per-service and root index files), so I can browse my codebase as documentation without opening an IDE, and so the tool doesn't fall over on a real, imperfect codebase.

**Why P1**: This is the smallest slice that's independently demoable end-to-end: point the tool at real projects, get readable Markdown output. Every later story builds on this pipeline (discovery → load → generate).

**Acceptance Criteria**:

1. WHEN the user runs the tool with a manifest file listing one or more root paths (with optional wildcards) THEN the system SHALL resolve each pattern to concrete directory paths. <!-- event-driven -->
2. WHEN a resolved directory contains exactly one `.sln` file THEN the system SHALL treat that directory as one service boundary using that solution. <!-- event-driven -->
3. WHEN a resolved directory contains no `.sln` file but contains one or more `.csproj` files THEN the system SHALL treat that directory as one service boundary using the `.csproj` file(s) found. <!-- event-driven -->
4. WHERE a manifest entry declares an explicit service-boundary override THEN the system SHALL use the override's declared file set instead of the automatic `.sln`/`.csproj` heuristic for that entry. <!-- optional-feature -->
5. WHEN the tool loads a service's solution THEN the system SHALL call `MSBuildWorkspace.OpenSolutionAsync` once for the whole solution rather than calling `OpenProjectAsync` per project in a loop. <!-- event-driven -->
6. WHILE opening a solution the system SHALL set `SkipUnrecognizedProjects = true` so a project with an invalid path, missing project file, or unrecognized extension is skipped rather than aborting the whole solution load. <!-- state-driven -->
7. IF `workspace.Diagnostics` contains an entry with `Kind == WorkspaceDiagnosticKind.Failure` for a project THEN the system SHALL record that project as degraded and continue processing the remaining projects in the solution. <!-- unwanted-behavior -->
8. IF a project's `Compilation.GetDiagnostics()` contains one or more `Error`-severity diagnostics indicating an unresolved type/namespace reference THEN the system SHALL record that project in a "possible missing restore" list and continue processing. <!-- unwanted-behavior -->
9. WHEN a project's `GetCompilationAsync()` returns `null` THEN the system SHALL treat that project as unsupported-for-compilation (not as a failure) and skip Markdown generation for its documents only. <!-- event-driven -->
10. WHEN analysis of a service completes THEN the system SHALL print a summary listing every project recorded as degraded or possibly missing a restore, with a suggestion to run `dotnet restore` for each listed project. <!-- event-driven -->
11. WHEN Markdown generation runs for a source document THEN the system SHALL produce exactly one `.md` file per source `.cs` document (excluding `obj/`, `bin/`, `*.g.cs`, `*.designer.cs`), placed at a path mirroring that document's relative path within its service's source tree. <!-- event-driven -->
12. The system SHALL include each method's full body text (not a summary or truncation) in the generated Markdown for that method. <!-- ubiquitous -->
13. WHEN all files for a service finish generating THEN the system SHALL write one `index.md` at the root of that service's output folder, linking to every generated file for that service. <!-- event-driven -->
14. WHEN all services finish processing THEN the system SHALL write one root `index.md` linking to every per-service `index.md`. <!-- event-driven -->
15. WHEN the tool starts a run and the configured output directory already contains files from a previous run THEN the system SHALL delete existing generated content and regenerate the full output directory from the current run. <!-- event-driven -->
16. IF the manifest file is missing, malformed JSON, or resolves to zero root paths THEN the system SHALL exit with a non-zero status code and a message identifying the specific problem, without writing any output. <!-- unwanted-behavior -->
17. IF a manifest wildcard pattern matches zero directories THEN the system SHALL log a warning naming the unmatched pattern and continue processing the remaining manifest entries. <!-- unwanted-behavior -->
18. IF two manifest entries resolve to the same service root THEN the system SHALL process that service exactly once and log a warning identifying the duplicate. <!-- unwanted-behavior -->
19. The system SHALL process services one at a time (sequentially), never opening more than one service's `MSBuildWorkspace` concurrently. <!-- ubiquitous -->

**Independent Test**: Run the tool against a synthetic fixture solution (2-3 projects, one with an intentionally missing package reference) via a manifest listing its root; verify one `.md` per `.cs` file is produced mirroring the folder structure, a per-service `index.md` and root `index.md` exist, and the console summary lists the project with the missing reference under "possible missing restore."

---

### P2: See service dependencies, in each file and as a graph

**User Story**: As a developer, I want the tool to detect HTTP/gRPC, messaging, and internal direct-reference dependencies between services — resolving logical service names via config where possible — and show them both inline per file and as a consolidated, typed graph, so I can understand service coupling without reading code.

**Why P2**: Builds directly on P1's generated Markdown and loaded compilations; the dependency graph is the tool's other core value proposition (alongside readability) but requires P1's pipeline to exist first.

**Acceptance Criteria**:

1. WHEN a document contains a call recognized as an HTTP client invocation (e.g., `IHttpClientFactory.CreateClient(...)` or a typed `HttpClient` usage) THEN the system SHALL record a dependency edge of communication type "síncrono-bloqueante" or "assíncrono-fire-and-forget" (per ACs P2-12) from the calling service to the resolved target. <!-- event-driven -->
2. WHEN a document contains a call recognized as a gRPC client invocation THEN the system SHALL record a dependency edge from the calling service to the resolved target, classified per AC P2-12. <!-- event-driven -->
3. WHEN a document contains a recognized pub/sub messaging publish or subscribe call THEN the system SHALL record a messaging signal of communication type `pub-sub-evento` carrying the topic or message type name and the role (publish or subscribe). <!-- event-driven -->
4. WHEN a service's project references another manifest service's project output, OR references a NuGet package whose `PackageId` matches a `PackageId` declared in another manifest service's own `.csproj` THEN the system SHALL record a dependency edge of communication type "direct-reference" between the two services. <!-- event-driven -->
5. IF a project reference or package reference does not match any other manifest service (i.e., it is a generic public/third-party library) THEN the system SHALL NOT record it as a dependency edge. <!-- unwanted-behavior -->
6. WHEN a detected HTTP/gRPC call target is a logical name (e.g., `CreateClient("OrderService")`) THEN the system SHALL attempt to resolve that logical name to a concrete manifest service by matching it against `appsettings*.json` and `docker-compose.yml` files found within the analyzed service roots. <!-- event-driven -->
7. IF a logical name resolves to a literal, hard-coded address or value in config THEN the system SHALL classify that edge's resolution as "hard-coded". <!-- unwanted-behavior -->
8. IF a logical name resolves via an environment-variable reference or a service-discovery/registry lookup in config THEN the system SHALL classify that edge's resolution as "dynamic". <!-- unwanted-behavior -->
9. IF a logical name cannot be resolved against any known config source THEN the system SHALL still record the dependency edge, using the unresolved logical name as the target, and mark its resolution as "unresolved". <!-- unwanted-behavior -->
10. WHEN Markdown is generated for a source file that has one or more detected dependencies THEN the system SHALL include a "Dependências detectadas" section in that file's `.md` listing, for each dependency, its communication type, target, and resolution classification — using the topic or message type name as the target for messaging signals, whose counterpart service is not correlated until the graph is assembled (P2-14). <!-- event-driven -->
11. WHEN all services finish processing THEN the system SHALL write a `dependencies.json` file containing every detected edge, each with source service, target service, communication type, and resolution classification. <!-- event-driven -->
12. The system SHALL classify every recorded edge's communication type as exactly one of: `sincrono-bloqueante`, `assincrono-fire-and-forget`, `pub-sub-evento`, `streaming-bidirecional`, `direct-reference`. <!-- ubiquitous -->
13. WHEN all services finish processing THEN the system SHALL generate a Mermaid diagram derived from `dependencies.json`, in which every edge is labeled with its communication type. <!-- event-driven -->
14. WHEN all services finish processing THEN the system SHALL correlate a publish signal for a topic or message type in one service with a subscribe signal for the same topic or message type in another service, producing one `pub-sub-evento` edge directed from publisher to subscriber. <!-- event-driven -->
15. IF a messaging publish or subscribe signal has no counterpart in any other service THEN the system SHALL retain it as an edge whose target is the topic or message type name, with resolution classified as "unresolved", rather than discarding it. <!-- unwanted-behavior -->

**Independent Test**: Run the tool against a synthetic fixture with a simulated HTTP call (logical name resolvable via `appsettings.json`), a simulated gRPC call, a simulated pub/sub publish, and an internal project reference between two of the fixture's services; verify `dependencies.json` contains four edges with correct types/classifications, the Mermaid diagram renders all four labeled edges, and the calling files' generated `.md` show a correct "Dependências detectadas" section.

---

### P3: Install and run as a dotnet global tool

**User Story**: As a developer, I want to install `csharp2md` as a `dotnet global tool`, so I can run it from any directory against any codebase without cloning or building this repository myself.

**Why P3**: Distribution is required for v1 (the tool is meant to be run manually against varied codebases), but it's a packaging concern layered on top of a working pipeline (P1+P2) — it doesn't block building or testing the core pipeline.

**Acceptance Criteria**:

1. The system SHALL build with `<PackAsTool>true</PackAsTool>`, `<OutputType>Exe</OutputType>`, and an explicit `<ToolCommandName>csharp2md</ToolCommandName>` set in the project file. <!-- ubiquitous -->
2. WHEN `dotnet pack` runs on the project THEN the system SHALL produce a `.nupkg` at the configured `PackageOutputPath`. <!-- event-driven -->
3. WHEN the packaged tool is installed via `dotnet tool install --global --add-source <path> csharp2md` THEN the system SHALL be invokable as `csharp2md` from any working directory. <!-- event-driven -->
4. WHEN invoked with no arguments, or without both `--manifest` and `--output` THEN the system SHALL print usage help to the console and exit with a non-zero status code. <!-- unwanted-behavior -->
5. WHEN invoked with `--manifest <path> --output <path>` where both are valid THEN the system SHALL run the full pipeline (P1 + P2) and exit with status code 0 on completion, even if some projects were recorded as degraded (a degraded-but-completed run is still a successful exit). <!-- event-driven -->

**Independent Test**: `dotnet pack` the project, `dotnet tool install --global --add-source ./nupkg csharp2md`, then from an unrelated directory run `csharp2md --manifest <path> --output <path>` against the synthetic fixture and confirm it produces the same output as running the project directly with `dotnet run`.

---

## Edge Cases

- IF the manifest file is missing, malformed JSON, or resolves to zero root paths THEN system SHALL exit non-zero without writing output (P1-16).
- IF a manifest wildcard pattern matches zero directories THEN system SHALL warn and continue (P1-17).
- IF two manifest entries resolve to the same service root THEN system SHALL process once and warn (P1-18).
- IF a project reference/package reference is a generic public library (no matching manifest `PackageId`) THEN system SHALL NOT record it as a dependency edge (P2-05).
- IF a logical service name can't be resolved via config THEN system SHALL still record the edge, marked "unresolved" (P2-09).
- WHEN the output directory already has content from a prior run THEN system SHALL fully overwrite it (P1-15).

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| P1-01 | P1: Browse a codebase as Markdown | Execute (T6) | Verified |
| P1-02 | P1: Browse a codebase as Markdown | Execute (T6) | Verified |
| P1-03 | P1: Browse a codebase as Markdown | Execute (T6) | Verified |
| P1-04 | P1: Browse a codebase as Markdown | Execute (T6) | Verified |
| P1-05 | P1: Browse a codebase as Markdown | Execute (T3) | Verified |
| P1-06 | P1: Browse a codebase as Markdown | Execute (T3) | Verified |
| P1-07 | P1: Browse a codebase as Markdown | Execute (T3) | Verified |
| P1-08 | P1: Browse a codebase as Markdown | Execute (T3) | Verified |
| P1-09 | P1: Browse a codebase as Markdown | Execute (T3) | Verified |
| P1-10 | P1: Browse a codebase as Markdown | Execute (T25) | Verified |
| P1-11 | P1: Browse a codebase as Markdown | Execute (T11, T13) | Verified |
| P1-12 | P1: Browse a codebase as Markdown | Execute (T11) | Verified |
| P1-13 | P1: Browse a codebase as Markdown | Execute (T22) | Verified |
| P1-14 | P1: Browse a codebase as Markdown | Execute (T22) | Verified |
| P1-15 | P1: Browse a codebase as Markdown | Execute (T13) | Verified |
| P1-16 | P1: Browse a codebase as Markdown | Execute (T5, T26) | Verified |
| P1-17 | P1: Browse a codebase as Markdown | Execute (T6) | Verified |
| P1-18 | P1: Browse a codebase as Markdown | Execute (T6) | Verified |
| P1-19 | P1: Browse a codebase as Markdown | Execute (T24) | Verified |
| P2-01 | P2: See service dependencies | Execute (T15) | Verified |
| P2-02 | P2: See service dependencies | Execute (T16) | Verified |
| P2-03 | P2: See service dependencies | Execute (T17) | Verified |
| P2-04 | P2: See service dependencies | Execute (T18) | Verified |
| P2-05 | P2: See service dependencies | Execute (T18) | Verified |
| P2-06 | P2: See service dependencies | Execute (T8) | Verified |
| P2-07 | P2: See service dependencies | Execute (T9) | Verified |
| P2-08 | P2: See service dependencies | Execute (T9) | Verified |
| P2-09 | P2: See service dependencies | Execute (T9) | Verified |
| P2-10 | P2: See service dependencies | Execute (T23) | Verified |
| P2-11 | P2: See service dependencies | Execute (T20) | Verified |
| P2-12 | P2: See service dependencies | Execute (T10, T14, T20) | Verified |
| P2-13 | P2: See service dependencies | Execute (T21) | Verified |
| P2-14 | P2: See service dependencies | Execute (T19) | Verified ⚠️ (spec-precision gap on resolution kind — see validation.md) |
| P2-15 | P2: See service dependencies | Execute (T19) | Verified |
| P3-01 | P3: Install and run as a dotnet global tool | Execute (T4) | Verified |
| P3-02 | P3: Install and run as a dotnet global tool | Execute (T4) | Verified |
| P3-03 | P3: Install and run as a dotnet global tool | Execute (T4) | Verified |
| P3-04 | P3: Install and run as a dotnet global tool | Execute (T4) | Verified |
| P3-05 | P3: Install and run as a dotnet global tool | Execute (T26) | Verified |

**ID format:** `P[story-number]-[NN]` (e.g., `P1-01`, `P2-06`).

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 39 total (19 + 15 + 5), 39 mapped to tasks, 0 unmapped. Independent Verifier PASS (`.specs/features/csharp2md/validation.md`): 39/39 acceptance criteria evidence-backed, discrimination sensor 6/6 mutations killed. One spec-precision gap flagged and resolved with documented reasoning (P2-14 — see validation.md).

---

## Success Criteria

- [ ] Running the tool against the synthetic fixture produces correct Markdown, index files, `dependencies.json`, and a labeled Mermaid diagram matching the fixture's known dependencies
- [ ] The tool never aborts a full run because one project in one service failed to load or restore
- [ ] The packaged tool installs and runs via `dotnet tool install --global` from a directory outside the tool's own repo
- [ ] A synthetic .NET fixture (2-3 projects simulating HTTP/gRPC/messaging) exists in-repo and is used as the primary TDD/validation target before running against the user's real (unshared) codebase
