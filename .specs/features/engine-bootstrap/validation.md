# Engine Bootstrap Validation

## Validation: engine-bootstrap - PASS ✅ (2 spec-precision gaps flagged, 0 hard AC failures)

**Date**: 2026-08-25
**Spec**: `.specs/features/engine-bootstrap/spec.md`
**Diff range**: `557ac47..62789a0` (T1 is `557ac47`; T52 is `d1d325b`; post-T52 fix `62789a0` adds directory-absence tests; 53 commits on `codex/architecture-knowledge-engine-docs`)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Task Completion

All 52 tasks in `tasks.md` (T1–T52) have every "Done when" line checked `[x]`. No task is partial or blocked. `git log --oneline 557ac47^..d1d325b` lists 52 commits whose messages match each task's `**Commit**:` field. Inclusive range `557ac47^..62789a0` adds one post-T52 commit that is not a `tasks.md` row.

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1–T52 | ✅ Done | All "Done when" items checked; commit present for each |
| post-T52 `62789a0` | ✅ Done | `test(engine): assert excised Core, benchmarks and schemas are absent` — adds `CoreDirectory_DoesNotExist`, `RetrievalIndexBenchmarksDirectory_DoesNotExist`, and `SchemasDirectory_DoesNotExist` in `DomainIsolationTests.cs` |

---

## Spec-Anchored Acceptance Criteria

Evidence-or-zero: every row cites `file:line` + the concrete assertion. Spec-defined outcomes were re-derived from `spec.md`, not from `tasks.md` comments or the previous FAIL report.

### P1: Target assembly topology with enforced boundaries

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| ENG-01 Analysis, Storage, Projection exist, `net10.0`, listed under `src` | Each of the three csproj files targets `net10.0` and appears in `csharp2md.slnx` `/src/` | `tests/Csharp2Md.Analysis.Tests/Isolation/SolutionTopologyTests.cs:67-72` — `srcPaths.Contains(relativePath)`; `:88-90` — `targetFramework == "net10.0"` (theory over Analysis, Storage, Projection) | ✅ PASS |
| ENG-02 CLI targets `net10.0`, packs as `csharp2md`, listed under `src` | `TargetFramework`=`net10.0`, `PackAsTool`=true, `ToolCommandName`=`csharp2md`, slnx `/src/` contains the CLI csproj | `tests/Csharp2Md.Cli.Tests/Isolation/CliIsolationTests.cs:33-35` — `Assert.Equal("net10.0"...)`, `Assert.Equal("true", PackAsTool)`, `Assert.Equal("csharp2md", ToolCommandName)`; `:59` — `Assert.Contains("src/Csharp2Md.Cli/Csharp2Md.Cli.csproj", srcPaths)` | ✅ PASS |
| ENG-03 Analysis declares no project reference to Storage, Projection or Cli | Each forbidden include is absent, failure names the project | `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisIsolationTests.cs:33-48` — theory over Storage/Projection/Cli/Domain; `Assert.True(offending is null, $"... must not declare a project reference to {forbiddenProject}...")` | ✅ PASS |
| ENG-04 Projection declares no project reference to Analysis, Storage or Cli | Each forbidden include is absent, failure names the project | `tests/Csharp2Md.Projection.Tests/Isolation/ProjectionIsolationTests.cs:8-20` — three facts calling `AssertNoProjectReference`; `:35-37` names the forbidden project | ✅ PASS |
| ENG-05 Domain still declares no package and no project reference | csproj text contains neither element | `AnalysisIsolationTests.cs:58-59` — `Assert.DoesNotContain("<PackageReference"...)`, `Assert.DoesNotContain("<ProjectReference"...)`. Independently read `src/Csharp2Md.Domain/Csharp2Md.Domain.csproj` (only `TargetFramework`/`ImplicitUsings`/`Nullable`/`InternalsVisibleTo`) | ✅ PASS |
| ENG-06 any production project with `Microsoft.CodeAnalysis*` or `Microsoft.Build*` package fails naming project and package | Boundary test names the offending project and package | `SolutionTopologyTests.cs:96-106` — theory over Analysis/Storage/Projection × two prefixes; `Assert.True(offending is null, $"{projectName} must not declare a package reference to '{offending}'...")`. **The theory does not include `Csharp2Md.Cli` or `Csharp2Md.Domain`.** Domain is closed by ENG-05. Cli's only `PackageReference` is `System.CommandLine` (`src/Csharp2Md.Cli/Csharp2Md.Cli.csproj:28`). ENG-51 also dropped the Roslyn `PackageVersion` entries, so a new Roslyn reference could not restore. The AC's "any production project" walk is still incomplete in this test | ⚠️ Spec-precision gap |
| ENG-07 public Analysis type exposing CodeAnalysis/Build/Json fails naming the type | Offending type named | `AnalysisIsolationTests.cs:65-74` — theory over the three namespaces; `Assert.True(offendingType is null, $"Type '{offendingType?.FullName}' from forbidden namespace...")` | ✅ PASS |
| ENG-08 CLI project references only Analysis, Storage, Projection; not Domain | Set equality of the three names; Domain absent | `CliIsolationTests.cs:22-24` — `Assert.DoesNotContain("Csharp2Md.Domain", names)` and `Assert.Equal(ExpectedProjectReferences, names)` where the expected set is Analysis, Projection, Storage | ✅ PASS |
| ENG-09 solution builds with `TreatWarningsAsErrors` | Build succeeds | `SolutionTopologyTests.cs:60-61` pins `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props`. **Build outcome confirmed by this Verifier's Gate Check** (`dotnet build csharp2md.slnx -c Release` exit 0, 0 warnings, 0 errors) | ✅ PASS (precondition unit-tested; outcome confirmed by Gate Check) |

### P1: One analysis facade over an eight-stage pipeline

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| ENG-10 exactly one public analysis interface | Exactly `IAnalysisEngine` | `tests/Csharp2Md.Analysis.Tests/IAnalysisEngineTests.cs:18-20` — `analysisInterfaces.Length == 1 && analysisInterfaces[0] == "Csharp2Md.Analysis.IAnalysisEngine"` | ✅ PASS |
| ENG-11 public surface limited to entry, request, result, storage port | Allowlist only; extras named | `tests/Csharp2Md.Analysis.Tests/Isolation/AnalysisPublicSurfaceTests.cs:42-44` — `Assert.True(extras.Length == 0, $"Public Analysis type(s) outside the allowlist: ...")`. Allowlist matches `design.md` (entry interface + `AnalysisEngine`, request, result types, port types) | ✅ PASS |
| ENG-12 no public pass/classifier/adapter/extractor/stage type | Those names absent from exported types | `AnalysisPublicSurfaceTests.cs:57-59`; `tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineStageContractTests.cs:57-65` — `Assert.DoesNotContain("IPipelineStage", exportedNames)` and each stub class name | ✅ PASS |
| ENG-13 stages run Inventory → … → Batch Composition | Recorded order equals the eight architecture names | `PipelineStageContractTests.cs:27` — `Assert.Equal(DeclaredStageNames, names)`; `tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineOrchestratorTests.cs:20-21` — executed names and `context.Reports` names equal `StubStages.DeclaredNames`; shuffled construction rejected at `:36` | ✅ PASS |
| ENG-14 substituting a stage runs it in that position with no orchestrator change | Persistence substitute runs at index 5; orchestrator type unchanged | `tests/Csharp2Md.Analysis.Tests/Pipeline/StageSubstitutionTests.cs:23-25` — `Assert.Equal(["Persistence"], executed)` and `outcome.Stages[5].Name == "Persistence"`; `:41-43` — both engines use `PipelineOrchestrator` and `Assert.Equal(defaultOrchestrator.GetType(), substitutedOrchestrator.GetType())` | ✅ PASS |
| ENG-15 every stage reports 0/0/0 facts/observations/relations | Counts are 0 on every `StageReport` | `tests/Csharp2Md.Analysis.Tests/Pipeline/DefaultPipelineZerosTests.cs:27-37` — `Assert.Equal(PublicationStatus.Committed, outcome.Status)`, `Assert.Equal(8, outcome.Stages.Length)`, loop `Assert.Equal(0, report.FactCount/ObservationCount/RelationCount)` | ✅ PASS |
| ENG-16 no file/directory created, modified or deleted in the working tree **or the process temporary directory** | Snapshot before/after is equal for both locations (spec assumption table) | `tests/Csharp2Md.Analysis.Tests/Pipeline/NoFilesystemWriteTests.cs:36-37` — `Assert.Equal(before, after)` over `src/`, `tests/`, `contracts/`, `fixtures/` excluding `bin`/`obj`/`TestResults`. **Does not snapshot `Path.GetTempPath()`.** `design.md` documents that omission as a race mitigation and pairs it with `InMemoryTransactionalStoreTests.cs:53-67` (`StorageProductionSources_DoNotUseSystemIo`). The AC letter still names the process temp directory | ⚠️ Spec-precision gap |
| ENG-17 analysis entry accepts a cancellation token | `AnalyzeAsync(AnalysisRequest, CancellationToken)` | `IAnalysisEngineTests.cs:34-39` — two parameters, `parameters[1].ParameterType == typeof(CancellationToken)`, returns `Task<AnalysisResult>` | ✅ PASS |
| ENG-18 cancel during a run stops before the next stage and does not commit | Stages 1–3 only; status `Unpublished`; no publication | `tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineCancellationTests.cs:35-44` — `PublicationStatus.Unpublished`, executed names are the first three stages, `Assert.False(store.TryGetPublication(...))` | ✅ PASS |
| ENG-19 stage failure skips remaining stages and reports the failing stage by name | `FailingStage` equals the substitute name; later probes do not run | `tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineStageFailureTests.cs:35-48` — `Assert.Equal(thrower.Name, outcome.FailingStage)`, `Assert.DoesNotContain` later stage names, `TryGetPublication` is false | ✅ PASS |

### P1: Transactional storage port with atomic publication

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| ENG-20 staging, commit and abort are three distinct operations | Separate members; no merged Stage+Commit method | `tests/Csharp2Md.Analysis.Tests/Storage/TransactionalStorePortTests.cs:29-45` — `Assert.NotSame(stage, commit)` etc.; `:59-61` — no method name containing both `Stage` and `Commit` | ✅ PASS |
| ENG-21 Storage provides an in-memory adapter of that port | Sessions isolated by key; `InMemoryTransactionalStore` implements the port | `tests/Csharp2Md.Storage.Tests/InMemoryTransactionalStoreTests.cs:22-27` — keys and canonical-key lists isolated; type is `InMemoryTransactionalStore : ITransactionalStore` | ✅ PASS |
| ENG-22 successful analysis commits staged fragments exactly once | Spy commit count is 1 | `tests/Csharp2Md.Analysis.Tests/Pipeline/CommitOnceTests.cs:21-23` — `Assert.Equal(PublicationStatus.Committed, outcome.Status)`, `Assert.Equal(1, store.CommitCount)`, `Assert.Equal(0, store.AbortCount)` | ✅ PASS |
| ENG-23 on commit, manifest is published after every other staged artifact | Last artifact role is `Manifest`; payload-then-manifest even if staged reversed | `tests/Csharp2Md.Analysis.Tests/Pipeline/PersistenceManifestTests.cs:31` — `Assert.Equal(ArtifactRole.Manifest, publication.ArtifactsInPublicationOrder[^1].Role)`; `InMemoryTransactionalStoreTests.cs:81-84` — payload `alpha`/`zeta` then manifest | ✅ PASS |
| ENG-24 structural corruption aborts commit and leaves previously published output unchanged | Second run `Unpublished` + `StructuralCorruption`; prior publication byte-equal | `tests/Csharp2Md.Analysis.Tests/Pipeline/StructuralCorruptionTests.cs:42-61` — `PublicationStatus.Unpublished`, `outcome.StructuralCorruption` true, `Assert.Equal(prior, kept)` and payload `SequenceEqual` | ✅ PASS |
| ENG-25 stage failure after staging discards every fragment for that solution | No publication; aborted keys absent | `PipelineStageFailureTests.cs:48` — `Assert.False(store.TryGetPublication(...))`; `InMemoryTransactionalStoreTests.cs:144-153` — aborted canonical key absent from the later commit | ✅ PASS |
| ENG-26 unknowns/candidates/frontiers without corruption still commit | Status `Committed` and the unknowns flag true | `tests/Csharp2Md.Analysis.Tests/Pipeline/UnknownsCommitTests.cs:25-31` — `PublicationStatus.Committed`, `HasUnknownsOrCandidatesOrFrontiers` true, `CommitCount == 1`, `AbortCount == 0` | ✅ PASS |
| ENG-27 two staging orders of the same fragments yield identical committed content | Publication-order artifacts compare equal, payloads then manifest | `tests/Csharp2Md.Analysis.Tests/Pipeline/StagingOrderTests.cs:28-32` — `AssertEqualArtifacts` plus last role Manifest; `InMemoryTransactionalStoreTests.cs:102-115` — byte-identical across reversed staging | ✅ PASS |
| ENG-28 Storage does not classify, promote or interpret fragments | No Domain type on the surface; arbitrary bytes round-trip | `tests/Csharp2Md.Storage.Tests/Isolation/StorageIsolationTests.cs:22-24` — no Domain type exposed; `:34-36` — Domain assembly not referenced; `:62-67` — taxonomy-shaped JSON bytes stored unchanged | ✅ PASS |

### P1: Multi-solution isolation and determinism

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| ENG-29 request accepts one or more solution paths | One path and two distinct paths accepted as supplied | `tests/Csharp2Md.Analysis.Tests/AnalysisRequestTests.cs:13` and `:24` — `Assert.Equal(paths, request.SolutionPaths)` | ✅ PASS |
| ENG-30 empty request rejected naming the missing input | `ArgumentException` `ParamName` is `solutionPaths`; message names the missing input | `AnalysisRequestTests.cs:31-34` — `Assert.Equal("solutionPaths", exception.ParamName)`, `Assert.Contains("one or more solutionPaths", exception.Message)` | ✅ PASS |
| ENG-31 duplicate path rejected naming the duplicate | Relative and absolute form of the same file rejected; message contains the absolute path | `AnalysisRequestTests.cs:57-58` — `ParamName == "solutionPaths"`, `Assert.Contains(absolute, exception.Message)` | ✅ PASS |
| ENG-32 each of several solutions analyzed and committed independently | Three outcomes; distinct publication keys | `tests/Csharp2Md.Analysis.Tests/Pipeline/MultiSolutionIsolationTests.cs:35-57` — three entries, A and C `Committed` with distinct `SolutionKey`, B unpublished | ✅ PASS |
| ENG-33 one failed solution does not block the rest | First and third commit; middle unpublished with `FailingStage` set | `MultiSolutionIsolationTests.cs:38-56` — A/C `Committed`, B `Unpublished` with `FailingStage == "Classification and Promotion"` | ✅ PASS |
| ENG-34 shuffled input: identical per-solution results, ordered by logical relative path | Both orders emit `["alpha/a.sln", "zeta/b.sln"]`; corresponding outcomes equal | `tests/Csharp2Md.Analysis.Tests/Pipeline/CanonicalResultOrderTests.cs:25-29` — canonical path order and `AssertEqualOutcome` pairwise | ✅ PASS |
| ENG-35 each solution gets its own pipeline context with no shared state | Three distinct `PipelineContext` instances | `MultiSolutionIsolationTests.cs:59-65` — `Assert.NotSame` all pairs; each `SolutionPath` matches the requested file | ✅ PASS |
| ENG-36 no compilation, symbol index or graph shared across solutions | No `Microsoft.CodeAnalysis.Compilation` instance field on the engine | `MultiSolutionIsolationTests.cs:72-75` — `compilationFields.Length == 0`. The stub pipeline has no symbol-index or graph type to hold; this is the strongest check available without Roslyn in this workstream | ✅ PASS |

### P1: Provisional analyze CLI

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| ENG-37 exactly one verb, `analyze` | Single subcommand named `analyze`; root has no action | `tests/Csharp2Md.Cli.Tests/AnalyzeCommandTreeTests.cs:14-16` — `Assert.Single(root.Subcommands)`, `Assert.Equal("analyze", analyze.Name)`, `Assert.Null(root.Action)` | ✅ PASS |
| ENG-38 `--solution` repeatable and required at least once | `Required` true, `Arity.OneOrMore`, no positional arguments | `AnalyzeCommandTreeTests.cs:24-28` — `Assert.True(solution.Required)`, `Assert.Equal(ArgumentArity.OneOrMore, solution.Arity)`, `Assert.Empty(analyze.Arguments)` | ✅ PASS |
| ENG-39 no `--solution` → exit 1, stderr names the option | Exit 1; stderr contains `--solution` | `tests/Csharp2Md.Cli.Tests/AnalyzeInvocationErrorTests.cs:11-12` — `Assert.Equal(1, exitCode)`, `Assert.Contains("--solution", stderr)` | ✅ PASS |
| ENG-40 missing path → exit 1, stderr names that path | Exit 1; stderr contains the missing path | `AnalyzeInvocationErrorTests.cs:24-25` — `Assert.Equal(1, exitCode)`, `Assert.Contains(missingPath, stderr)` | ✅ PASS |
| ENG-41 completed run with no structural failure → exit 0 | Exit 0 against an existing fixture | `tests/Csharp2Md.Cli.Tests/AnalyzeSuccessTests.cs:20` — `Assert.Equal(0, exitCode)` | ✅ PASS |
| ENG-42 structural failure aborting any solution → exit 2 | Fake unpublished/corruption outcome maps to 2 | `tests/Csharp2Md.Cli.Tests/AnalyzeExitCodeTests.cs:26` — `Assert.Equal(2, exitCode)` | ✅ PASS |
| ENG-43 unknowns without structural failure → exit 0 | Fake committed+unknowns outcome maps to 0 | `AnalyzeExitCodeTests.cs:48` — `Assert.Equal(0, exitCode)` | ✅ PASS |
| ENG-44 diagnostics on stderr, summary on stdout | Summary on stdout; `csharp2md:` diagnostics not on stdout | `AnalyzeSuccessTests.cs:21-24` — non-empty stdout containing the solution path; `Assert.DoesNotContain("csharp2md:", stdout)`. A successful stub run emits no diagnostics, so the stderr half is observed on the invocation-error path (ENG-39) rather than on a diagnostic-producing success | ✅ PASS |
| ENG-45 no `--topic/--domain/--manifest/--output/--trust/--include-source-generators/--analysis-timeout` | Each name absent from root and analyze options | `tests/Csharp2Md.Cli.Tests/AnalyzeOptionSurfaceTests.cs:31-34` — `Assert.DoesNotContain(removed, rootNames/analyzeNames)` for all seven; `:40-41` — the only product option is `--solution` | ✅ PASS |

### P1: Legacy excision with a port ledger

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| ENG-46 repository contains no `src/Csharp2Md.Core` directory | `Directory.Exists("src/Csharp2Md.Core")` is false | `tests/Csharp2Md.Domain.Tests/Isolation/DomainIsolationTests.cs:83-90` — `CoreDirectory_DoesNotExist`, `Assert.False(Directory.Exists(path), $"Repository must not contain '{path}'.")` with `path` = `src/Csharp2Md.Core`. Independently: `Test-Path src/Csharp2Md.Core` is false; `git ls-files` for that tree is empty at `62789a0` | ✅ PASS |
| ENG-47 repository contains no `tests/Csharp2Md.Core.Tests` directory | Directory does not exist | `DomainIsolationTests.cs:73-80` — `Assert.False(Directory.Exists(path), $"Repository must not contain '{path}'.")`. Independently: `Test-Path tests/Csharp2Md.Core.Tests` is false | ✅ PASS |
| ENG-48 repository contains no `benchmarks/Csharp2Md.RetrievalIndex.Benchmarks` and no `schemas` directory | Both directories absent | `DomainIsolationTests.cs:93-100` — `RetrievalIndexBenchmarksDirectory_DoesNotExist`; `:103-110` — `SchemasDirectory_DoesNotExist`. Both `Assert.False(Directory.Exists(path), ...)`. Independently: both `Test-Path` results are false | ✅ PASS |
| ENG-49 slnx lists only Domain, Analysis, Storage, Projection, CLI and their test projects | Extra paths named; missing allowlisted paths named | `SolutionTopologyTests.cs:45-50` — `extras.Length == 0` / `missing.Length == 0` against a 10-project allowlist matching the spec. Independently read `csharp2md.slnx`: five `/src/` + five `/tests/` projects, no Core/benchmarks | ✅ PASS |
| ENG-50 `fixtures/SyntheticSolution` retained | Directory exists and contains a `.slnx` or `.csproj` | `tests/Csharp2Md.Cli.Tests/Isolation/FixtureRetentionTests.cs:10-21` — `Assert.True(Directory.Exists(path))` and `projectOrSolutionFiles.Length > 0` | ✅ PASS |
| ENG-51 unused `PackageVersion` fails naming the package | Every declared version has a consumer in the solution or `fixtures/` | `tests/Csharp2Md.Analysis.Tests/Isolation/PackageHygieneTests.cs:51-53` — `unused.Length == 0` naming leftover ids; `:26-28` also asserts dropped Roslyn/YamlDotNet versions are absent | ✅ PASS |
| ENG-52 committed port ledger with former path, one-line responsibility, last commit SHA | File exists; every row has a 40-char SHA; design-reuse paths recorded | `tests/Csharp2Md.Analysis.Tests/Isolation/PortLedgerTests.cs:24-26` (file exists); `:39-44` (SHA + responsibility per design-reuse path); `:59-61` (every row has a SHA). Independently read `docs/architecture/legacy-port-ledger.md` (19 rows, all SHAs 40 hex chars) | ✅ PASS |
| ENG-53 ledger names Roslyn sanitation probes and CLI security-boundary tests for later re-establishment | Those two areas, former paths, workstreams 4 and 8 | `PortLedgerTests.cs:70-73` — former path `.../RoslynSanitationProbeTests.cs`, `ReEstablishIn == "workstream 4"`; `:82-85` — `.../V3SecurityBoundaryTests.cs`, `"workstream 8"` | ✅ PASS |
| ENG-54 full suite over `csharp2md.slnx` reports zero failing tests | Zero failures | No unit test can invoke the whole suite. **Confirmed by this Verifier's Gate Check**: `dotnet test csharp2md.slnx` → 660 passed, 0 failed, 0 skipped, exit 0 | ✅ PASS (suite outcome confirmed by Gate Check) |

### P1: Reachable relation shape guards

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| ENG-55 non-callable source/target rejected through public `Create` | `ArgumentException` naming the fact parameter and the relation | `tests/Csharp2Md.Domain.Tests/Relations/ConfirmedRelationCreateCallableTests.cs:48-61` — `Create(RelationKind.Invokes, ..., source without Callable)` throws, `ParamName == "sourceFact"`, message contains `Invokes`; `:70-82` for `Executes` on `targetFact`. Act is `ConfirmedRelation.Create`, not the guard helper | ✅ PASS |
| ENG-56 `targets` with a non-inbound-BO / non-DU / non-external-system target rejected through `Create` | Rejected; message names `Targets` | `tests/Csharp2Md.Domain.Tests/Relations/ConfirmedRelationCreateTargetShapeTests.cs:61-73` — outbound `BoundaryOperation` via `Create`, message contains `Targets` and `Outbound` | ✅ PASS |
| ENG-57 `operates-on` with a non data-object/data-field target rejected through `Create` | Rejected; message names `OperatesOn` | `ConfirmedRelationCreateTargetShapeTests.cs:82-94` — `DataStore` target via `Create`, message contains `OperatesOn` and `DataStore` | ✅ PASS |
| ENG-58 syntactic evidence where semantic is required rejected through `Create` | Rejected naming the relation and both evidence methods | `tests/Csharp2Md.Domain.Tests/Relations/ConfirmedRelationTests.cs:178-190` — `Create(Invokes, ..., EvidenceMethod.Syntactic)` message contains `Invokes`, `Semantic`, `Syntactic` | ✅ PASS |
| ENG-59 the three shape guards each have a production call site reachable from `Create` | Live `RelationShapeGuards.{name}(` in the `Create` body | `tests/Csharp2Md.Domain.Tests/Relations/ConfirmedRelationCreateGuardReachabilityTests.cs:31-33` — `liveCalls.Length >= 1` for each of `RequireCallableIfNeeded`, `RequireLegalTargetShape`, `RequireSufficientEvidence`. Independently read `src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs:84,106,118,129` | ✅ PASS |
| ENG-60 registry emitter leaves `contracts/taxonomy-registry.json` byte-identical | Byte equality against a fresh emit | `tests/Csharp2Md.Domain.Tests/Registry/RegistryDriftGateTests.cs:18-19` — `committedBytes.AsSpan().SequenceEqual(freshBytes)` | ✅ PASS |
| ENG-61 TAX-46/50/51/53 traceability rows read `Verified` with no spec-precision-gap clause | Each row contains `\| Verified \|` and does not contain `spec-precision gap` | `RegistryDriftGateTests.cs:33-35` — `Assert.Contains("| Verified |", line)` and `Assert.DoesNotContain("spec-precision gap", line)` for the four ids. Independently read those four rows in `.specs/features/knowledge-taxonomy-contract/spec.md` | ✅ PASS |

**Status**: ⚠️ Spec-precision gaps flagged — 59/61 ACs matched the spec-defined outcome with `file:line` evidence · ⚠️ 2 spec-precision gaps (ENG-06 incomplete production-project walk; ENG-16 temp-directory snapshot omitted per `design.md`) · 0 uncovered (❌) · 0 hard AC failures

---

## Findings on the flagged items

### 1. ENG-06 theory data omits Cli and Domain (spec-precision)

Independently re-derived from `SolutionTopologyTests.ForbiddenPackageCases` (`:14-20`): it only yields Analysis, Storage and Projection. The AC says "any production project in the solution". Domain is already package-free (ENG-05, independently confirmed by reading `Csharp2Md.Domain.csproj`). Cli declares only `System.CommandLine` (`Csharp2Md.Cli.csproj:28`). ENG-51 removed the Roslyn `PackageVersion` entries, so a new Roslyn `PackageReference` would fail restore. The shipped tree satisfies the outcome; the test would not name Cli if it grew a `Microsoft.CodeAnalysis` reference that somehow resolved. Flagged, not treated as a hard miss of the shipped invariant.

### 2. ENG-16 does not snapshot the process temp directory (spec-precision)

The AC and the assumptions table both require a working-tree **and** process-temp snapshot. The test hashes four repo roots and explicitly skips `bin`/`obj`/`TestResults`. `design.md` records "Snapshotting `Path.GetTempPath()` is racy under parallel xUnit" and "Do not snapshot `Path.GetTempPath()`", substituting a repo-tree snapshot plus a `System.IO` absence check on Storage sources. That is an approved design mitigation of a precise AC, not an implementer skip of the working-tree half. Flagged as ⚠️, not ❌.

### 3. ENG-46 and ENG-48 are now pinned (closed since `62789a0`)

The previous FAIL report had no `Directory.Exists` assertion for `src/Csharp2Md.Core`, `benchmarks/Csharp2Md.RetrievalIndex.Benchmarks`, or `schemas`. This pass found those three facts next to ENG-47's existing `CoreTestsDirectory_DoesNotExist`, all in `DomainIsolationTests.cs`, introduced by `62789a0`. Disk check at verification time: all four excised paths are absent. Not a gap.

---

## Edge Cases

From spec.md's Edge Cases section:

- [x] Empty solution set rejected naming the missing input (ENG-30) — `AnalysisRequestTests.cs:31-34`
- [x] Duplicate solution path rejected naming the duplicate (ENG-31) — `AnalysisRequestTests.cs:57-58`
- [x] One of many solutions failing still commits the rest (ENG-33) — `MultiSolutionIsolationTests.cs:38-56`
- [x] Stage failure after staging discards that solution's fragments (ENG-25) — `PipelineStageFailureTests.cs:48`
- [x] Structural corruption aborts and previously published output survives (ENG-24) — `StructuralCorruptionTests.cs:53-61`
- [x] Unknowns and candidates still commit and exit 0 (ENG-26, ENG-43) — `UnknownsCommitTests.cs:25`; `AnalyzeExitCodeTests.cs:48`
- [x] Cancellation mid-run stops before the next stage without committing (ENG-18) — `PipelineCancellationTests.cs:35-44`
- [x] Shuffled input yields identical canonical order (ENG-34) — `CanonicalResultOrderTests.cs:25-29`
- [x] Non-existent `--solution` path exits 1 naming the path (ENG-40) — `AnalyzeInvocationErrorTests.cs:24-25`
- [x] Unused `PackageVersion` fails naming the package (ENG-51) — `PackageHygieneTests.cs:51-53`

All 10 documented edge cases are handled and evidenced.

---

## Discrimination Sensor

**Sensor**: skipped — standing project decision (user runs Stryker manually; documented in `tasks.md`'s header and `.specs/STATE.md`'s standing engineering constraints, consistent with `symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver` and `knowledge-taxonomy-contract`). No mutants were injected. This produces no pass/fail signal by design and is not treated as a gap.

---

## Interactive UAT Results

Not performed. This feature is a walking-skeleton CLI and assembly topology; the spec's Independent Tests are fully automated. No user-facing flow requiring human judgment.

---

## Code Quality

Sampled across phases: relation construction (`ConfirmedRelation.cs:46-133`), isolation tests (`DomainIsolationTests.cs`, `SolutionTopologyTests.cs`, `AnalysisIsolationTests.cs`), storage adapter (`InMemoryTransactionalStoreTests.cs`), facade (`IAnalysisEngineTests.cs`, `PipelineOrchestratorTests.cs`, `StageSubstitutionTests.cs`), CLI (`AnalyzeCommandTreeTests.cs`, `CliIsolationTests.cs`), excision pins (`PortLedgerTests.cs`, `PackageHygieneTests.cs`, `FixtureRetentionTests.cs`).

| Principle | Status |
| --- | --- |
| Minimum code | ✅ — eight stub stages, one in-memory adapter, one `analyze` verb; no Roslyn, no on-disk package, no extra verbs |
| Surgical changes | ✅ — Domain change is the carried-forward `Create` signature only (`EvidenceMethod` + optional `sourceFact`/`targetFact`); Core/tests/benchmarks/schemas deleted rather than quarantined |
| No scope creep | ✅ — matches the Out-of-Scope table; Projection is a marker assembly; Inventory is a stub |
| Matches patterns | ✅ — `[Trait("Requirement", "ENG-nn")]` continues the Domain TAX convention; isolation tests parse csproj/slnx the same way as `DomainIsolationTests` |
| Spec-anchored outcome check (asserted values match spec) | ⚠️ — 59/61 exact; 2 ⚠️; 0 ❌ |
| Per-layer Coverage Expectation met | ✅ — isolation layer now asserts deleted trees (ENG-46, ENG-47, ENG-48) plus topology/package/surface allowlists; domain 1:1 for ENG-55..61; no route/e2e layer in this CLI-skeleton feature |
| Every test maps to a spec AC/edge case/Done-when | ✅ — every new test file carries an `ENG-nn` trait; smoke `AssemblyMarker` tests are tagged ENG-01 |
| Documented guidelines followed | ✅ — `AGENTS.md`/`CLAUDE.md` (no `Microsoft.Build.*`, no `MSBuildLocator`; Analysis has zero Roslyn/MSBuild package references), `Directory.Build.props` `TreatWarningsAsErrors`, xUnit 2.9.3 / VSTest on net10.0 |

Would a senior engineer approve? Yes. The walking skeleton, port, CLI honesty, relation-guard wiring, and excision pins (including the post-T52 absence tests) are sound. The two spec-precision flags are documentation/coverage-walk nits, not missing behavior.

---

## Gate Check

- **Gate command**: `dotnet build csharp2md.slnx -c Release` → exit 0 (0 warnings, 0 errors) → `dotnet format csharp2md.slnx --verify-no-changes` → exit 0 → `dotnet test csharp2md.slnx` → exit 0
- **Result**: **660 passed, 0 failed, 0 skipped**
  - `Csharp2Md.Domain.Tests`: 544 passed
  - `Csharp2Md.Analysis.Tests`: 82 passed
  - `Csharp2Md.Storage.Tests`: 15 passed
  - `Csharp2Md.Cli.Tests`: 16 passed
  - `Csharp2Md.Projection.Tests`: 3 passed
- **Test count before feature**: `Csharp2Md.Domain.Tests` 523; `Csharp2Md.Core.Tests` 1579 (1578 passing + 1 pre-existing `MigrationLedgerTests` failure, documented by the previous workstream). Total 2102.
- **Test count after feature**: 660 across the five remaining test projects. `Csharp2Md.Core.Tests` is gone. Domain is 544 (523 prior + relation-guard tests + 3 absence tests from `62789a0`).
- **Delta**: Domain +21; +116 in new Analysis/Storage/Projection/Cli test projects; −1579 Core.Tests. Net −1442. The decrease is the explicit ENG-47 excision (and closes the pre-existing `MigrationLedgerTests` failure by deletion, as the spec's Success Criteria required). No silent weakening observed in the sampled assertions.
- **Skipped tests**: none.
- **Failures**: none.

---

## Fix Plans

No blocking fix tasks. Optional, non-blocking follow-ups for the two spec-precision flags:

### Fix 1 (non-blocking): Include Cli and Domain in the ENG-06 theory

- **Root cause**: `ForbiddenPackageCases` only walks the three new assemblies.
- **Fix task**: Yield Cli and Domain csproj paths in `SolutionTopologyTests.ForbiddenPackageCases`.
- **Priority**: Minor

### Fix 2 (non-blocking): Reconcile ENG-16 temp-directory wording

- **Root cause**: AC + assumptions table name the process temp directory; `design.md` forbids snapshotting it.
- **Fix task**: Either amend the AC/assumption to the approved design (repo-tree snapshot + no `System.IO` in Storage) or add a non-racy temp probe. Do not snapshot `Path.GetTempPath()` under parallel xUnit.
- **Priority**: Minor

---

## Requirement Traceability Update

`spec.md` was not edited (Verifier scoped to `validation.md` only). Recommended statuses:

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| ENG-01 – ENG-05 | Verified | ✅ Verified |
| ENG-06 | Verified | ⚠️ Verified with spec-precision gap (Cli/Domain omitted from the theory) |
| ENG-07 – ENG-15 | Verified | ✅ Verified |
| ENG-16 | Verified | ⚠️ Verified with spec-precision gap (temp directory not snapshotted) |
| ENG-17 – ENG-61 | Verified | ✅ Verified |

---

## Summary

**Overall**: ⚠️ Issues (spec-precision gaps flagged, not blocking; feature is otherwise complete and correctly built/tested)

**Spec-anchored check**: 59/61 ACs matched the spec-defined outcome exactly with `file:line` evidence; 2 spec-precision gaps (ENG-06, ENG-16)
**Sensor**: skipped — standing project decision
**Gate**: 660 passed, 0 failed, 0 skipped

**What works**: All 52 tasks are checked and committed, plus post-T52 `62789a0` pins excised directory absence. The four target assemblies exist with AD-006 boundaries enforced by tests. The eight-stage stub pipeline runs in declared order, is substitutable, reports zeros, cancels, isolates stage failure, commits once, publishes the manifest last, aborts on corruption, and keeps unknowns as a committed outcome. Multi-solution isolation and canonical order hold. The CLI exposes only `analyze --solution` with exit codes 0/1/2. Domain `ConfirmedRelation.Create` invokes the three shape guards (TAX-46/50/51/53 closed). Core, Core.Tests, benchmarks, and schemas are absent on disk and asserted so. The suite is green.

**Issues found**: ENG-06's forbidden-package theory does not walk Cli/Domain. ENG-16 does not snapshot the process temp directory, by approved `design.md` mitigation. Neither is a missing AC.

**Next steps**: Optional minor follow-ups above. Feature is ready to close.
