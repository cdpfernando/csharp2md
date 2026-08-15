# csharp2md Validation

**Date**: 2026-08-15
**Spec**: `.specs/features/csharp2md/spec.md`
**Diff range**: `28a41fc`..`9e601fc` (21 commits, T1-T25) + T26's gated-but-uncommitted working tree
**Verifier**: independent sub-agent (author ≠ verifier), read-only over the real tree

---

## Verdict

**PASS ✅** — 39/39 acceptance criteria traced to `file:line` evidence whose asserted values match the spec-defined outcomes. Gate green (283/283). Discrimination sensor 6/6 mutants killed. One spec-precision gap flagged (P2-14), three non-blocking observations recorded.

---

## Diff-Surface Reconciliation (evidence over narrative)

The briefing and `.specs/STATE.md:54` both state "**18 commits** through T25 (`28a41fc`..`cba424a`)". Verified independently:

- `git rev-list --count 28a41fc..HEAD` = **20**; `git rev-list --count HEAD` = **21**.
- `28a41fc`..`cba424a` inclusive is **20 commits**, not 18.
- A 21st commit, `9e601fc docs(specs): record T26 mid-flight state and monthly-spend-limit blocker`, sits on top of `cba424a` and was not mentioned in the briefing.

This is a bookkeeping inaccuracy in the handoff note, not a code defect. Recorded as Observation 1.

Working-tree state matches the declared T26 file set, plus three files the handoff correctly lists as out-of-scope-but-untracked (`AGENTS.md`, `src/Csharp2Md.Cli/Properties/`, agent-config directories) and two `.specs` files modified in place (`STATE.md`, `tasks.md`).

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1-T25 | ✅ Done | Committed, `28a41fc`..`cba424a` |
| T26 | ✅ Done | Gated green, deliberately uncommitted pending user permission per `CLAUDE.md`. Reviewed as finished work. |

`tasks.md` carries 26 task headings, **171 checked** "Done when" boxes and **0 unchecked**.

---

## Spec-Anchored Acceptance Criteria

### P1: Browse a codebase as Markdown (19 criteria)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| P1-01 wildcard patterns resolve to concrete dirs | each pattern → concrete directory paths | `tests/.../Discovery/ServiceDiscovererTests.cs:25` — `Assert.Equal(2, result.Catalog.Services.Count)` on `Acme.*` | ✅ PASS |
| P1-02 exactly one `.sln` → solution boundary | boundary kind = solution, that `.sln` used | `ServiceDiscovererTests.cs:38` — `Assert.Equal(ServiceBoundaryKind.Solution, service.BoundaryKind)` + `Assert.EndsWith("App.sln", ...)` | ✅ PASS |
| P1-03 no `.sln`, has `.csproj` → csproj boundary | boundary uses the `.csproj` file(s) | `ServiceDiscovererTests.cs:92` — `Assert.Equal(ServiceBoundaryKind.LooseProjects, ...)`, `Assert.Equal(2, service.ProjectPaths.Count)` | ✅ PASS |
| P1-04 manifest override wins over heuristic | override's declared file set used | `ServiceDiscovererTests.cs:105` — `Assert.Equal([Path.Combine(dir,"Other.csproj")], service.ProjectPaths)` despite `.sln` present | ✅ PASS |
| P1-05 `OpenSolutionAsync` once, never `OpenProjectAsync` in a loop | one solution-level open per service | `src/.../Loading/SolutionLoader.cs:18` single `OpenSolutionAsync`; repo-wide grep finds **zero** `OpenProjectAsync` call sites; `SolutionLoaderTests.cs:96` asserts the solution is returned whole | ✅ PASS |
| P1-06 `SkipUnrecognizedProjects = true` | bad project skipped, solution load survives | `SolutionLoader.cs:16`; `SolutionLoaderTests.cs:63` — `Assert.DoesNotContain(..., p.ProjectName.Contains("DoesNotExist"))` + `Assert.Equal(3, ...Projects.Count)` | ✅ PASS |
| P1-07 `Diagnostics` Failure → degraded, continue | project recorded degraded, others continue | `SolutionLoaderTests.cs:51` — `Assert.Equal(ProjectLoadStatus.Degraded, broken.Status)`; `:41` asserts remaining 3 projects still processed | ✅ PASS |
| P1-08 error diagnostics → "possible missing restore" | project on missing-restore list, run continues | `SolutionLoaderTests.cs:77` — `Assert.Equal(ProjectLoadStatus.PossibleMissingRestore, payments.Status)` + `Assert.NotEmpty(payments.Messages)` | ✅ PASS |
| P1-09 `GetCompilationAsync()` null → unsupported, not failure | status unsupported-for-compilation | `Loading/SolutionLoaderClassificationTests.cs:8` — `Assert.Equal(ProjectLoadStatus.UnsupportedForCompilation, result.Status)` via `AdhocWorkspace` with unregistered language | ✅ PASS |
| P1-10 summary lists degraded/missing-restore + `dotnet restore` suggestion | each affected project named with the suggestion | `Pipeline/RunReporterTests.cs:32` — `Assert.Equal("  - Acme.Broken (degraded) — suggested fix: dotnet restore", Lines(summary)[1])` (exact-string, per-project) | ✅ PASS |
| P1-11 one `.md` per `.cs`, mirrored path, exclusions | exact 1:1 set, mirrored, no `obj`/`bin`/`.g.cs`/`.designer.cs` | `Pipeline/AnalysisPipelineTests.cs:15` — `Assert.Equal(expected, GeneratedDocuments(...))` (set equality); `Output/OutputWriterTests.cs:18` path mirroring; `:52`,`:63` exclusion theories | ✅ PASS |
| P1-12 full method body, not summarized | body text verbatim and complete | `Rendering/MarkdownRendererTests.cs:68` — asserts signature, `for` loop, `Console.WriteLine(id);` and `await Task.Delay(1);` all present in the method section | ✅ PASS |
| P1-13 per-service `index.md` linking every file | index exists, links each generated file | `AnalysisPipelineTests.cs:36` — `Assert.Contains("[OrderService.cs](./OrderService.cs.md)", index)` | ✅ PASS |
| P1-14 root `index.md` linking each service index | root index links every per-service index | `AnalysisPipelineTests.cs:47` — three exact `Assert.Contains("[Acme.X](./Acme.X/index.md)")` | ✅ PASS |
| P1-15 full overwrite of prior output | prior content deleted, regenerated | `OutputWriterTests.cs:82` — `Assert.Empty(Directory.GetFileSystemEntries(_root))` after `PrepareRun()`; `:95` asserts output reflects only the current run | ✅ PASS |
| P1-16 bad manifest → non-zero exit, named problem, no output | non-zero exit + specific message + nothing written | `Cli/EndToEndTests.cs:107` — `Assert.NotEqual(0, result.ExitCode)`, `Assert.Contains("csharp2md:", StandardError)`, no output dir; typed codes at `Pipeline/AnalysisPipelineInvariantTests.cs:86` (`ManifestErrorCode.MalformedJson`) and `:100` (`FileMissing`) | ✅ PASS |
| P1-17 unmatched wildcard → warn, continue | warning names pattern, processing continues | `ServiceDiscovererTests.cs:119` — `Assert.Single(result.Warnings)` + `Assert.Contains("zero directories", ...)` | ✅ PASS |
| P1-18 duplicate roots → process once, warn | exactly one service + duplicate warning | `ServiceDiscovererTests.cs:131` — `Assert.Single(result.Catalog.Services)` + `Assert.Contains("Duplicate service root", ...)` | ✅ PASS |
| P1-19 sequential; never two workspaces concurrently | no overlapping workspace lifetimes | `Pipeline/AnalysisPipelineInvariantTests.cs:25` — `Assert.Equal(["open:Acme.Orders","close:Acme.Orders","open:Acme.Shared.Contracts","close:Acme.Shared.Contracts"], events)` — strict open/close interleaving, **not** a comment | ✅ PASS |

**P1-19 depth note.** The assertion observes the pipeline's load-call sequence, and workspace lifetime is bound to that call by `SolutionLoader.cs:15` (`using var workspace = MSBuildWorkspace.Create();` — created and disposed inside the single `LoadAsync` call site, verified by grep to be the only construction site in `src/`). A "close" event therefore genuinely precedes the next "open", so the invariant is real rather than asserted by comment. Corroborated by `AnalysisPipelineInvariantTests.cs:58`, which proves each service's documents are written before the next workspace opens.

### P2: Service dependencies, inline and as a graph (15 criteria)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| P2-01 HTTP client call → edge, typed per P2-12 | blocking vs fire-and-forget by call shape | `Detection/HttpClientDetectorTests.cs:43` — `Assert.Equal(CommunicationType.SincronoBloqueante, signal.Communication)` (awaited); `:55` fire-and-forget (unawaited); `:63` discard; `:71` `.Result`; `:79` `ConfigureAwait` | ✅ PASS |
| P2-02 gRPC client call → edge, typed per P2-12 | unary blocking, streaming bidirectional | `Detection/GrpcClientDetectorTests.cs:75` — `Assert.Equal(CommunicationType.SincronoBloqueante, ...)`; `:94` duplex → `StreamingBidirecional`; `:102` server-streaming | ✅ PASS |
| P2-03 pub/sub call → messaging signal with topic + role | `pub-sub-evento`, topic name, publish/subscribe role | `Detection/MessagingDetectorTests.cs:50` — `Assert.Equal(MessagingRole.Publish, signal.Role)` + `Assert.Equal("OrderPlaced", signal.RawTarget)`; `:62` subscribe half | ✅ PASS |
| P2-04 project ref OR matching `PackageId` → direct-reference | direct-reference edge between the two services | `Detection/DirectReferenceDetectorTests.cs:48` (ProjectReference) and `:67` (PackageId match) — both assert `TargetService == Acme.Shared.Contracts` and `CommunicationType.DirectReference` | ✅ PASS |
| P2-05 third-party lib → NO edge | no dependency edge recorded | `DirectReferenceDetectorTests.cs:89` — `Assert.Empty(signals)` for Newtonsoft.Json/Serilog/Microsoft.Extensions.Http; `:103` asserts only the internal one survives in a mixed set | ✅ PASS |
| P2-06 logical name resolved via `appsettings*.json` / `docker-compose.yml` | name matched against those config sources | `Configuration/ConfigIndexerTests.cs:25` (appsettings), `:56` (docker-compose), `:116` (across multiple roots); resolution at `ServiceNameResolverTests.cs:13` | ✅ PASS |
| P2-07 literal address → "hard-coded" | resolution = HardCoded | `ServiceNameResolverTests.cs:13` — `Assert.Equal(ResolutionKind.HardCoded, result.Kind)`; end-to-end at `HttpClientDetectorTests.cs:90` | ✅ PASS |
| P2-08 env-var / discovery → "dynamic" | resolution = Dynamic | `ServiceNameResolverTests.cs:29` (`${VAR}`), `:45` (`%VAR%`), `:61` (docker-compose name); detector-level at `HttpClientDetectorTests.cs:103` | ✅ PASS |
| P2-09 unresolvable → still record edge, target = raw name, "unresolved" | edge retained, raw name preserved, Unresolved | `ServiceNameResolverTests.cs:74` — `Assert.Equal(ResolutionKind.Unresolved, ...)` + `Assert.Equal("ShippingService", result.LogicalName)`; `HttpClientDetectorTests.cs:114`; `GrpcClientDetectorTests.cs:129` | ✅ PASS |
| P2-10 "Dependências detectadas" section per file | comm type, target, resolution per dependency; topic name for messaging | `Rendering/DependencySectionRendererTests.cs:45` — `Assert.Equal("\| sincrono-bloqueante \| PaymentService \| hard-coded \|", Row(markdown,0))`; `:54` gRPC; `:64` messaging→topic; `:73` direct-ref; `:92` omitted when no deps | ✅ PASS |
| P2-11 `dependencies.json` with source/target/type/resolution | all four fields per edge | `Output/DependencyJsonWriterTests.cs:43` — four exact `Assert.Equal` on `source`,`target`,`communicationType`,`resolution`; `:61` every edge emitted | ✅ PASS |
| P2-12 exactly one of five communication types | the five spec-named values, spec spelling | `Detection/CommunicationClassifierTests.cs` — one test per table row, plus `Classify_PairWithNoTableRow_Throws` theory asserting `ArgumentOutOfRangeException` rather than a plausible default | ✅ PASS |
| P2-13 Mermaid diagram, every edge labeled | each edge labeled with its comm type | `Output/MermaidWriterTests.cs:39` — 5-case theory `Assert.Contains($"svc0 -->\|{expected}\| svc1")`; `:50` — `Assert.All(lines, line => Assert.Contains("-->\|", line))` proves **no edge left bare** | ✅ PASS |
| P2-14 correlate publish→subscribe across services | one edge, publisher → subscriber | `Graph/GraphBuilderTests.cs:36` — asserts Source=Acme.Orders, Target=Acme.Payments, `PubSubEvento`; `:57` evidence from both halves; `:126` multi-publisher fan-in; `:103` no self-edge within one service | ✅ PASS (see spec-precision gap) |
| P2-15 unpaired signal retained, target = topic, "unresolved" | retained as edge, NOT discarded | `GraphBuilderTests.cs:77` (unpaired publish) and `:90` (unpaired subscribe) — both `Assert.Equal(new ServiceName(topic), edge.Target)` + `Assert.Equal(ResolutionKind.Unresolved, edge.Resolution)`; `:148` roleless signal also retained | ✅ PASS |

**P2-14 / P2-15 distinctness confirmed.** These are the two the briefing asked to check hardest. They have **separate, non-overlapping** tests: P2-14 is covered by 4 tests (`:36`, `:57`, `:126`, `:103`), P2-15 by 3 (`:77`, `:90`, `:148`), plus `:161` proving different topics never correlate. Both are additionally exercised at process level in `EndToEndTests.cs:87` (correlated `Acme.Orders`→`Acme.Payments`) and `:91` (unpaired `Acme.Payments`→`PaymentProcessed`). Neither collapses into the other.

### P3: Install and run as a dotnet global tool (5 criteria)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| P3-01 `PackAsTool`/`OutputType`/`ToolCommandName` set | all three present in project file | `src/Csharp2Md.Cli/Csharp2Md.Cli.csproj:4` (`<OutputType>Exe</OutputType>`), `:11` (`<PackAsTool>true</PackAsTool>`), `:12` (`<ToolCommandName>csharp2md</ToolCommandName>`) | ✅ PASS |
| P3-02 `dotnet pack` produces `.nupkg` at `PackageOutputPath` | nupkg exists at configured path | `Cli/PackagingSmokeTests.cs:26` — pack exit 0 + `Assert.NotEmpty(Directory.GetFiles(NupkgDirectory, "*.nupkg"))` | ✅ PASS |
| P3-03 installed global tool invokable from any dir | runs as `csharp2md` from unrelated cwd | `PackagingSmokeTests.cs:31` install exit 0; `:46` runs `ToolCommand` with cwd = temp dir **outside the repo**, asserts exit 0 | ✅ PASS |
| P3-04 no/partial args → usage help + non-zero exit | help printed, non-zero status | `Cli/CliArgumentValidationTests.cs:30` — `Assert.NotEqual(0, ExitCode)` + `Assert.Contains("--manifest"/"--output", StandardOutput)`; `:40`, `:51` partial-arg cases | ✅ PASS |
| P3-05 valid args → full pipeline, exit 0 **even when degraded** | exit code 0 despite degraded projects | `CliArgumentValidationTests.cs:60` — real process, manifest resolving `Acme.Orders.slnx` which bundles the unrestorable `Acme.Broken`; `Assert.Equal(0, result.ExitCode)`. Also `PackagingSmokeTests.cs:54` (packed tool) and `EndToEndTests.cs:33` (fixture containing the missing-restore `Acme.Payments`) | ✅ PASS (see Observation 3) |

**Process-level, not unit-level.** The briefing asked whether the exit-code contract has real process coverage. It does: all P3-04/P3-05 evidence comes from `ProcessRunner`-spawned OS processes asserting on real `ExitCode`/`StandardOutput`/`StandardError`, not on `PipelineRunResult.IsSuccess`. Exit 0 is returned from `src/Csharp2Md.Cli/Program.cs:59` after the degraded summary is printed at `:55`.

### T26 dependency-set assertions vs. spec + AD-005

`EndToEndTests.cs:62-104` asserts six `(source, target, communication, resolution)` tuples. Checked each against spec.md's P2 Independent Test under AD-005's constraint:

| Asserted tuple | Spec basis | Consistent with AD-005? |
| --- | --- | --- |
| `Acme.Orders` → `PaymentService`, sincrono-bloqueante, hard-coded | P2-01 + P2-07 | ✅ target is the resolved raw name, not a catalog service — exactly AD-005 |
| `Acme.Orders` → `Payments`, sincrono-bloqueante, unresolved | P2-02 + P2-09 | ✅ proto service name retained; deliberately not matched to `Acme.Payments` |
| `Acme.Orders` → `Acme.Payments`, pub-sub-evento, not-applicable | P2-14 | ✅ correlated edge targets a real service because messaging correlation *is* defined |
| `Acme.Payments` → `PaymentProcessed`, pub-sub-evento, unresolved | P2-15 | ✅ topic name stands in as target |
| `Acme.Orders` → `Acme.Shared.Contracts`, direct-reference, not-applicable | P2-04 | ✅ `TargetService` populated by the detector (T18's documented exception) |
| `Acme.Payments` → `Acme.Shared.Contracts`, direct-reference, not-applicable | P2-04 | ✅ same |

Spec.md's P2 Independent Test asks for "four edges with correct types/classifications". The fixture yields six, covering all four detector families plus both P2-14 and P2-15 branches — a superset, correctly typed. AD-005 was **not** re-litigated; the asserted targets are precisely what AD-005 prescribes.

**Status**: ✅ All 39 ACs covered — ⚠️ 1 spec-precision gap flagged (P2-14).

---

## Discrimination Sensor

**Isolation method**: `git -c core.longpaths=true worktree add D:/c2md-sensor HEAD`, with T26's nine uncommitted files copied in so the scratch mirrored the real tree. `git stash` was **not** used. Baseline `git status --porcelain` captured before any sensor work (dirty by design, 20 entries including T26's pending files).

Scratch baseline run before mutating: **63 passed, 0 failed** — so every kill below is genuine.

| # | File:line (scratch) | Mutation | Killed? |
| --- | --- | --- | --- |
| 1 | `src/.../Graph/GraphBuilder.cs:82` | Flipped cross-service pairing guard `!=` → `==` (P2-14) | ✅ Killed — 4 failed / 13 |
| 2 | `src/.../Graph/GraphBuilder.cs:89-95` | Deleted the P2-15 retention branch so unpaired signals are dropped | ✅ Killed — 5 failed / 13 |
| 3 | `src/.../Output/OutputWriter.cs:26-29` | Removed the `Directory.Delete(recursive)` side effect (P1-15 full overwrite) | ✅ Killed — 2 failed / 17 |
| 4 | `src/.../Detection/CommunicationClassifier.cs:35` | `(Http, ResultConsumed)` → `AssincronoFireAndForget` instead of `SincronoBloqueante` (P2-12) | ✅ Killed — 4 failed / 23 |
| 5 | `src/.../Rendering/MarkdownRenderer.cs:202` | Off-by-one on the span partition end bound (AD-002 invariant) | ✅ Killed — 14 failed / 24 |
| 6 | `src/.../Loading/SolutionLoader.cs:16` | `SkipUnrecognizedProjects = true` → `false` (P1-06) | ✅ Killed — 6 failed / 8 |

**Sensor depth**: expanded (6 mutations, above the lightweight 1-3 default) — justified by the feature's breadth across graph correlation, rendering fidelity, output semantics, classification, and load resilience.
**Result**: **6/6 killed — PASS ✅**

**Isolation verified**: scratch removed via `git worktree remove --force` + `git worktree prune`; `git worktree list` shows only the real tree; `git status --porcelain` diffed byte-identical against the pre-sensor baseline. The real working tree — including all of T26's uncommitted work — was never modified.

---

## Code Quality

| Principle | Status |
| --- | --- |
| Minimum code | ✅ |
| Surgical changes | ✅ |
| No scope creep | ✅ — out-of-scope items in spec.md (shared-DB detector, token counting, dynamic plugins, auto-restore) are all absent from `src/` |
| Matches patterns | ✅ — sealed types, records, primary constructors, file-scoped namespaces throughout |
| Spec-anchored outcome check | ✅ — assertions target exact spec values (`"sincrono-bloqueante"`, exact summary lines, exact index links), not mere presence |
| Per-layer coverage expectation | ✅ — domain logic 1:1 with ACs; CLI/e2e covers happy + edge + error paths |
| Every test maps to a spec requirement | ✅ — spot-checked all 27 test files; the only non-AC test is `FilterSyntaxProbeTests.cs`, which is T1's documented gate-syntax probe (`tasks.md:142`) |
| Documented guidelines followed | ✅ — `CLAUDE.md`: AD-003 verified by grep (no `MSBuildLocator`, no `Microsoft.Build.*`, no obsolete `WorkspaceFailed`); Roslyn pinned to `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0 in `Directory.Packages.props:7` |

**Roslyn fabrication check.** Every Roslyn member the code relies on — `MSBuildWorkspace.Create()`, `SkipUnrecognizedProjects`, `OpenSolutionAsync`, `workspace.Diagnostics` with `WorkspaceDiagnosticKind.Failure`, `Project.GetCompilationAsync()` returning null, `Compilation.GetDiagnostics()` — is verified **empirically**, not from a comment: the solution compiles against real 5.6.0 with `TreatWarningsAsErrors`, and the integration tests at `SolutionLoaderTests.cs` open real `.slnx` solutions and observe each of these behaviors. No fabricated member signatures found.

---

## Edge Cases

- [x] Manifest missing / malformed / zero roots → non-zero exit, no output (`AnalysisPipelineInvariantTests.cs:86`, `:100`; `EndToEndTests.cs:107`)
- [x] Wildcard matches zero directories → warn and continue (`ServiceDiscovererTests.cs:119`)
- [x] Duplicate manifest roots → process once, warn (`ServiceDiscovererTests.cs:131`)
- [x] Generic third-party reference → no edge (`DirectReferenceDetectorTests.cs:89`)
- [x] Unresolvable logical name → edge retained, marked unresolved (`ServiceNameResolverTests.cs:74`)
- [x] Output directory pre-populated → full overwrite (`OutputWriterTests.cs:82`, `:95`)
- [x] **Beyond spec**: `dotnet` absent from PATH → actionable message, no raw stack trace (`EndToEndTests.cs:131`); ambiguous multi-`.sln` directory (`ServiceDiscovererTests.cs:144`); malformed `.csproj` (`DirectReferenceDetectorTests.cs:...` malformed-project test); Mermaid-significant characters escaped (`MermaidWriterTests.cs:...`); source containing triple backticks (`SpanCoverageTests.cs:67`)

---

## Gate Check

- **Gate command** (Build level, from `tasks.md:41`): `dotnet build -c Release` → `dotnet format --verify-no-changes` → `dotnet test`
- **Result**: **283 passed, 0 failed, 0 skipped** (exit code 0), run independently by the Verifier
- **Test count before feature**: 0 (greenfield)
- **Test count after feature**: 283 — delta **+283**
- **Skipped tests**: none
- **Failures**: none
- **Build warnings**: 6, all SourceLink "no remote configured" — a local-repo condition, already recorded as a known non-blocking gap in `STATE.md:59`

---

## Observations (non-blocking — no fix task required)

### Observation 1: handoff commit count is wrong (documentation)

`.specs/STATE.md:54` claims "**18 commits** through T25 (`28a41fc`..`cba424a`)". The actual count is **20**, and an unmentioned 21st commit (`9e601fc`) sits on top. Suggest correcting the handoff line when T26 is committed.

### Observation 2: `spec.md` traceability table is stale

`spec.md:144-188` still shows every requirement as `Pending` / `Implementing`, and the coverage line reads "0 mapped to tasks, 39 unmapped ⚠️ (Tasks phase not yet run)" — untrue now that all 26 tasks are complete. The Verifier is read-only and did not edit it. Recommended post-validation update: all 39 → **Verified**, coverage line → "39 total, 39 mapped, 0 unmapped".

### Observation 3: P3-05's "even when degraded" rests on an unasserted precondition

`CliArgumentValidationTests.cs:81` and `PackagingSmokeTests.cs:57` both assert `Assert.Contains("3 project(s)", StandardOutput)`. That substring matches **both** reporter outputs — the clean form (`"Run summary: 3 project(s) loaded, none degraded or missing a restore."`, `RunReporterTests.cs:60`) and the needs-attention form (`"Run summary: N of 3 project(s) need attention."`, `RunReporterTests.cs:42`). So if `Acme.Broken` silently stopped being degraded, these tests would still pass and the "even when degraded" half of P3-05 would become vacuous.

This is **not** a coverage gap: exit-0 is genuinely asserted at process level, and degradation is independently asserted at `AnalysisPipelineTests.cs:83` and `SolutionLoaderTests.cs:51`. Mutation 6 confirmed the load-classification path is discriminating. Optional hardening: tighten the assertion to `Assert.Contains("need attention", StandardOutput)` so the degraded precondition is asserted in the same test that asserts exit 0.

### Spec-precision gap: P2-14 does not define the correlated edge's resolution

P2-14 specifies direction and communication type for a correlated publish→subscribe edge but is silent on its resolution classification. The implementation chose `NotApplicable` and documented the reasoning at `GraphBuilder.cs:109-113`, with the test recording the same at `GraphBuilderTests.cs:49-52`. The choice is sound (the edge resolved via topic matching, never via config; `Unresolved` is reserved by P2-15 for signals with no counterpart) and self-consistent with T18's direct-reference treatment. Flagged per the evidence-or-zero rule rather than passed silently — no code change needed; the spec sentence could gain a clause if it is ever revised.

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| P1-01 … P1-19 (19) | Implementing / Pending | ✅ Verified |
| P2-01 … P2-15 (15) | Implementing / Pending | ✅ Verified |
| P3-01 … P3-05 (5) | Implementing / Pending | ✅ Verified |

**Coverage**: 39 total, 39 verified, 0 unmapped.

---

## Summary

**Overall**: ✅ Ready

**Spec-anchored check**: 39/39 ACs matched the spec-defined outcome; 1 spec-precision gap flagged (P2-14 resolution classification)
**Sensor**: 6/6 mutations killed
**Gate**: 283 passed, 0 failed, 0 skipped

**What works**: The full three-stage pipeline (AD-001) runs end to end against the synthetic fixture through the packed, globally-installed tool. AD-002's span-coverage invariant is a genuine byte-for-byte partition test — `SpanCoverageTests.cs:41` reconstructs the source exactly from emitted sections across 7 source shapes including usings, `#region`, inter-member comments, top-level statements, bodyless records and triple-backtick content — and mutation 5 proves it discriminates. AD-003 compliance is verified by grep and by a green Release build with `TreatWarningsAsErrors`. P1-19's sequential-workspace rule is asserted as a strict open/close event sequence, not claimed in a comment. P2-14 and P2-15 have separate, non-overlapping coverage at both unit and process level. The exit-code contract is exercised through real OS processes, including the degraded-but-exit-0 path and a BuildHost-unavailable path that asserts no raw stack trace leaks.

**Issues found**: None blocking. Three non-blocking observations (stale commit count in the handoff, stale traceability table in `spec.md`, and one assertion that could be tightened to pin P3-05's degraded precondition) plus one flagged spec-precision gap.

**Next steps**: Refresh `spec.md`'s traceability table to Verified, correct the commit count in `STATE.md`, then ask the user for permission to commit T26 (`CLAUDE.md` forbids committing without it). The optional P3-05 assertion tightening can ride along or be skipped.
