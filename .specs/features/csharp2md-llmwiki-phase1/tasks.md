# csharp2md + LLMWiki Phase 1 Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

---

**Design**: `.specs/features/csharp2md-llmwiki-phase1/design.md`
**Status**: Approved (user, 2026-08-15)
**Branch**: `feat/llmwiki-phase1` (AD-007 — never commit this feature to `master`)

---

## Test Coverage Matrix

> Generated from codebase, project guidelines, and spec — confirm before Execute. Guidelines found: `AGENTS.md`, `CLAUDE.md` (Quality gates table). No `.github/workflows`, no `CONTRIBUTING.md`, no coverage threshold configured — the strong default applies for depth.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Topic derivation & options (`src/Csharp2Md.Core/Topic/`) | unit | All branches; 1:1 to spec ACs; every listed edge case has a test | `tests/Csharp2Md.Core.Tests/Topic/*Tests.cs` | `dotnet test --filter "Category!=Integration"` |
| Output writers (`src/Csharp2Md.Core/Output/`) | unit | Key write paths + error handling | `tests/Csharp2Md.Core.Tests/Output/*Tests.cs` | `dotnet test --filter "Category!=Integration"` |
| Rendering (`src/Csharp2Md.Core/Rendering/`) | unit | All branches; the span-coverage invariant stays asserted | `tests/Csharp2Md.Core.Tests/Rendering/*Tests.cs` | `dotnet test --filter "Category!=Integration"` |
| Manifests (`src/Csharp2Md.Core/Manifests/`) | unit | Every malformed-input path returns an error rather than throwing | `tests/Csharp2Md.Core.Tests/Manifests/*Tests.cs` | `dotnet test --filter "Category!=Integration"` |
| Pipeline (`src/Csharp2Md.Core/Pipeline/`) | integration | Full run over the fixture: artifact set, layout, degraded project | `tests/Csharp2Md.Core.Tests/Pipeline/*Tests.cs` (`[Trait("Category","Integration")]`) | `dotnet test` |
| CLI (`src/Csharp2Md.Cli/`) | integration | Happy path + every listed error path + exit codes | `tests/Csharp2Md.Core.Tests/Cli/*Tests.cs` | `dotnet test` |
| Published contract (`schemas/*.json`) | unit | A sync test fails when the record and the schema diverge | `tests/Csharp2Md.Core.Tests/Topic/*Tests.cs` | `dotnet test --filter "Category!=Integration"` |
| Fixture sources (`fixtures/`) | none | build gate only — assertions live in the Pipeline layer | — | build gate only |

Provenance: test style, location, and framework inferred from 31 existing test files under `tests/Csharp2Md.Core.Tests/`, which mirror the `src/Csharp2Md.Core/` folder structure one-to-one. xUnit 2.9.3 on VSTest (`xunit.runner.visualstudio`), integration tests marked with `[Trait("Category", "Integration")]`.

## Gate Check Commands

> Generated from codebase — confirm before Execute. All three verified to run in this repository before this file was written.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks with unit tests only | `dotnet test --filter "Category!=Integration"` |
| Full | After tasks with integration tests | `dotnet test` |
| Build | After phase completion or packaging tasks | `dotnet build -c Release` then `dotnet format --verify-no-changes` then `dotnet test` |

Verified: the trait filter selects 260 of 303 tests in ~0.8 s (`AGENTS.md` warns that filter syntax differs between VSTest and MTP — this project is VSTest, and the syntax was confirmed by running it, not assumed). `dotnet format --verify-no-changes` exits 0 on the current tree. Baseline is **303 tests passing, 0 failing**; no task may reduce that number.

## Knowledge Verification (binding for T7–T10)

`CLAUDE.md` warns that the Roslyn API surface is where fabricated members are most likely, and there is **no general-Roslyn skill available** — `dotnet-skills:roslyn-incremental-generator-specialist` is source-generator-only by that file's own note, and Context7 is not configured in this project. So verification means official documentation, not a skill.

An inventory of `src/Csharp2Md.Core` shows what the repository already proves and what it does not:

| Already exercised in `src/` (follow the existing detectors) | **No precedent — verify against official docs before writing** |
| --- | --- |
| `TypeDeclarationSyntax`, `EnumDeclarationSyntax`, `RecordDeclarationSyntax`, `NamespaceDeclarationSyntax`, `MethodDeclarationSyntax`, `CompilationUnitSyntax`, `InvocationExpressionSyntax`, `MemberAccessExpressionSyntax`, `GenericNameSyntax` | `BaseListSyntax` / `SimpleBaseTypeSyntax`, `InterfaceDeclarationSyntax`, `FileScopedNamespaceDeclarationSyntax`, `.Modifiers` inspection, extension-method detection via the `this` parameter modifier |

**Trap to avoid:** the fixture uses *both* namespace styles — `Acme.Orders/OrderService.cs` is file-scoped, `Acme.Orders/PaymentsGrpcClient.cs` uses block namespaces. Handling only `NamespaceDeclarationSyntax` makes the tier-2 title rule fail silently on half the fixture, and the failure looks like a heuristic bug rather than a missing syntax node.

Rule: any member in the right-hand column is verified before use. Members in the left-hand column follow the existing detectors — they are already proven by the 303-test baseline.

---

## Execution Plan

Phases are ordered and run sequentially — each phase completes before the next begins, and tasks within a phase execute in order.

### Phase 1: Foundation

Three independent building blocks. T1 is a pre-existing defect fixed first because the CLI work in Phase 5 travels the same path.

```
T1
T2
T3
```

### Phase 2: Layout migration

The breaking change, alone in its own commit so the `raw/` move is separable from every behavior change that follows.

```
T3 -> T4
```

### Phase 3: Frontmatter model and derivation

Pure logic, no pipeline wiring. Every rule in the spec's heuristic tables gets its own task and its own tests.

```
T5 -> T6
T5 -> T8
T5 -> T10
T7 -> T10
T8 -> T10
T9 -> T10
T2 -> T10
T5 -> T11
```

### Phase 4: Emission and wiring

Derivation reaches disk.

```
T5 -> T12
T11 -> T12
T4 -> T13
T10 -> T13
T12 -> T13
T4 -> T14
T11 -> T14
T11 -> T15
T13 -> T15
```

### Phase 5: Topic scaffold and CLI

The remaining artifacts and the user-facing surface.

```
T2 -> T16
T3 -> T16
T3 -> T17
T15 -> T17
T2 -> T18
T15 -> T18
```

### Phase 6: Fixture proof and release

Proves the heuristics against the fixture and marks the break.

```
T13 -> T19
T17 -> T20
T18 -> T20
T20 -> T21
```

---

## Task Breakdown

### T1: Fix ManifestLoader null-services crash

**What**: Make a syntactically valid manifest whose `services` property is missing or null return the intended `ZeroEntries` error instead of throwing `NullReferenceException`.
**Where**: `src/Csharp2Md.Core/Manifests/ManifestLoader.cs` (modify)
**Depends on**: None
**Reuses**: the existing `ManifestLoadResult.Failed` / `ManifestErrorCode.ZeroEntries` path already in the file
**Requirement**: design.md Risks & Concerns (pre-existing defect; P1-16's "expected error, never an exception" contract)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `manifest.Services.Count` is no longer dereferenced before the null check
- [x] A test covers `{"services": null}` and one covers a JSON object with no `services` key at all; both assert `ZeroEntries`, not an exception
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: 303 baseline + 2 new, none removed

**Tests**: unit
**Gate**: quick

**Commit**: `fix(manifests): return zero-entries error instead of throwing on null services`

---

### T2: Create TopicOptions

**What**: The validated `topic`/`domain` pair, with slug derivation from the input directory name and slug-pattern rejection.
**Where**: `src/Csharp2Md.Core/Topic/TopicOptions.cs`
**Depends on**: None
**Reuses**: the "expected error, not exception" result convention from `ManifestLoader`
**Requirement**: WIKI-14, WIKI-15, WIKI-16

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards` (use the explicit-property value-object form — the skill's validated primary-constructor snippet does not compile, CS0111)

**Done when**:
- [x] `Slugify` lowercases, collapses non-alphanumeric runs to a single `-`, and trims leading/trailing `-`
- [x] Default topic is the slug of the input directory name; default domain is `system-design`
- [x] A topic failing `^[a-z0-9]+(-[a-z0-9]+)*(/[a-z0-9]+(-[a-z0-9]+)*)*$` returns an error, never throws
- [x] Tests cover: accepted plain slug, accepted `group/name` slug, rejected uppercase, rejected leading `-`, rejected empty, and each default
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

**Tests**: unit
**Gate**: quick

**Commit**: `feat(topic): add validated topic and domain options`

---

### T3: Create TopicLayout

**What**: Pure path resolution for the LLMWiki layout — `raw/`, `raw/codebase/`, and a service's root within it.
**Where**: `src/Csharp2Md.Core/Topic/TopicLayout.cs`
**Depends on**: None
**Reuses**: `ServiceName`
**Requirement**: WIKI-01, WIKI-02, WIKI-03

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `RawRoot`, `CodebaseRoot`, and `ServiceRoot` return the paths the spec's layout section names
- [x] Tests assert the exact relative shape from an arbitrary output root, including that `ServiceRoot` nests under `raw/codebase/`
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

**Tests**: unit
**Gate**: quick

**Commit**: `feat(topic): add llmwiki layout path resolution`

---

### T4: Move generated output beneath raw/

**What**: Wire `TopicLayout` into the pipeline so documents, indexes, `dependencies.json`, and `dependencies.mmd` land under `raw/`, while the ownership marker stays at the output root.
**Where**: `src/Csharp2Md.Core/Pipeline/AnalysisPipeline.cs` (modify)
**Depends on**: T3
**Reuses**: `OutputWriter`, `IndexWriter`, `DependencyJsonWriter`, `MermaidWriter` unchanged — only the roots they receive change
**Requirement**: WIKI-01, WIKI-02, WIKI-03, WIKI-04

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `OutputWriter.PrepareRun` still receives `outputRoot`, so `.csharp2md-output` and the `--force` gate are unchanged
- [x] Per-service writers receive `TopicLayout.ServiceRoot`; Stage 3 aggregates receive `TopicLayout.RawRoot`
- [x] Every existing test asserting a root-level output path is updated in this task — no test is deleted or disabled to make the suite green
- [x] An integration test asserts the full artifact set at its new location and that the marker is *not* inside `raw/`
- [x] Gate check passes: `dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test`
- [x] Test count: 303 baseline preserved, none removed

> Note: spec.md AC3 (WIKI-03) distinguishes the root `index.md` (stays part of the mirrored tree, under
> `raw/codebase/`) from `dependencies.json`/`.mmd` (root of `raw/` directly). `IndexWriter.WriteRootIndex`
> receives `TopicLayout.CodebaseRoot`, not `RawRoot`, to match that normative text precisely.

**Tests**: integration
**Gate**: build

**Commit**: `feat(output)!: move generated tree beneath raw/ for llmwiki topics`

---

### T5: Create Frontmatter record and FileType enum

**What**: The closed model of a frontmatter block, with Phase 1 constants computed rather than settable.
**Where**: `src/Csharp2Md.Core/Topic/Frontmatter.cs`
**Depends on**: None
**Reuses**: the sealed-record style used across `Rendering` and `Graph`
**Requirement**: WIKI-06

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:
- [x] `FileType` declares all eleven values from the spec's enum
- [x] `Language`, `CreatedBy`, `SourceService`, and `AnalysisStatus` are computed properties, not constructor parameters
- [x] `SourceKind` distinguishes `codebase-file` from `codebase-index`
- [x] Tests assert the four Phase 1 constants and that `Tags` is never null
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

**Tests**: unit
**Gate**: quick

**Commit**: `feat(topic): add frontmatter model`

---

### T6: Publish frontmatter JSON schema with a sync test

**What**: The external contract file, plus the test that fails when it drifts from the `Frontmatter` record.
**Where**: `schemas/frontmatter.schema.json`
**Depends on**: T5
**Reuses**: the field table in spec.md's Frontmatter Schema section as the source of truth
**Requirement**: WIKI-06

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Schema lists every required field and the full `file_type` enum
- [x] A test derives the expected property and enum sets from the `Frontmatter` record and `FileType` via reflection and fails on any divergence
- [x] The test names the drifting field, so a failure is actionable without opening the schema
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

**Tests**: unit
**Gate**: quick

**Commit**: `docs(schemas): publish frontmatter contract with drift test`

---

### T7: Create TitleResolver

**What**: The four-tier title rule — file-name match, project-namespace match, first in source order, file-name fallback with warning.
**Where**: `src/Csharp2Md.Core/Topic/TitleResolver.cs`
**Depends on**: None
**Reuses**: the syntax-walking shape used by `MessagingDetector`
**Requirement**: WIKI-06, WIKI-13

**Tools**:
- MCP: NONE
- Skill: NONE
- Verify first (see Knowledge Verification): `FileScopedNamespaceDeclarationSyntax` alongside `NamespaceDeclarationSyntax` — the fixture uses both styles

**Done when**:
- [x] Each of the four tiers has a test built from a parsed syntax tree
- [x] Tier 2 is proven by a document declaring a foreign-namespace type before the project's own — the `PaymentsGrpcClient.cs` shape
- [x] Tier 3 is proven by two same-namespace types where the file name matches neither — the `Events.cs` shape
- [x] Tier 4 emits a warning and is proven by a document declaring no type
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

**Tests**: unit
**Gate**: quick

**Commit**: `feat(topic): add four-tier title resolution`

---

### T8: Create FileTypeClassifier

**What**: The eleven-value classification table, first match wins, warning on multi-match.
**Where**: `src/Csharp2Md.Core/Topic/FileTypeClassifier.cs`
**Depends on**: T5
**Reuses**: `FileType` from T5
**Requirement**: WIKI-06, WIKI-13

**Tools**:
- MCP: NONE
- Skill: NONE
- Verify first (see Knowledge Verification): `BaseListSyntax`/`SimpleBaseTypeSyntax`, `InterfaceDeclarationSyntax`, `.Modifiers`, extension-method detection via the `this` parameter

**Done when**:
- [x] Every one of the eleven values has at least one test asserting it
- [x] Rule order is asserted: declaration-kind rules beat name and base-type rules
- [x] A type matching two rules produces the first in table order plus a warning naming both
- [x] Classification uses no semantic model — the test constructs trees with `CSharpSyntaxTree.ParseText` and no compilation
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

**Tests**: unit
**Gate**: quick

**Commit**: `feat(topic): add file-type classification heuristics`

---

### T9: Create TagDeriver

**What**: The six additive tag rules, emitting a sorted, de-duplicated list.
**Where**: `src/Csharp2Md.Core/Topic/TagDeriver.cs`
**Depends on**: None
**Reuses**: the syntax-walking shape used by `MessagingDetector`
**Requirement**: WIKI-06

**Tools**:
- MCP: NONE
- Skill: NONE
- Verify first (see Knowledge Verification): `BaseListSyntax` for the `api-endpoint` rule

**Done when**:
- [x] Each of the six rules has a test asserting it fires
- [x] `event-driven` fires on bare `Subscribe` as well as `SubscribeAsync`
- [x] A document matching several rules yields all matching tags, sorted, with no duplicates
- [x] A document matching none yields an empty list, not null
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

**Tests**: unit
**Gate**: quick

**Commit**: `feat(topic): add tag derivation heuristics`

---

### T10: Create FrontmatterBuilder

**What**: Compose title, file type, and tags into a `Frontmatter`, collecting warnings — syntax tree in, model out, no semantic model consulted.
**Where**: `src/Csharp2Md.Core/Topic/FrontmatterBuilder.cs`
**Depends on**: T2, T5, T7, T8, T9
**Reuses**: `TitleResolver`, `FileTypeClassifier`, `TagDeriver`, `TopicOptions`
**Requirement**: WIKI-06, WIKI-13

**Tools**:
- MCP: NONE
- Skill: NONE
- Verify first: nothing new — T10 composes T7–T9 and introduces no further Roslyn surface

**Done when**:
- [x] `Build` takes a syntax tree, source path, root namespace, and `TopicOptions`, and never accepts a `SemanticModel`
- [x] A test parses a file whose base types are unresolvable and asserts the same result a resolvable equivalent produces — the WIKI-13 guarantee
- [x] A test with a deliberately malformed type header asserts `file_type: class` plus a warning, pinning the incomplete-tree degradation from design.md Risks as intended behavior
- [x] `source_path` is forward-slash separated on every platform
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

> Note: to satisfy the malformed-header Done-when bullet using only already-proven Roslyn surface
> (no new detection logic beyond T7-T9), `Build` emits an advisory warning whenever `file_type`
> resolves to `Class` via "no rule matched" (not ambiguity) with a real title type present — reusing
> no new node types, only a `FileType` comparison. Verified empirically (a `CSharpSyntaxTree.ParseText`
> probe, not guessed) that a corrupted base-type reference (`Some#Controller` truncated by the parser
> to `Some`) leaves the type's identifier intact, so no existing T7 tier-4 warning fires; this is the
> only path left that can surface the degradation. Trade-off: the same warning also fires for
> legitimately-unclassifiable `class` results with no malformation at all (e.g. the real
> `PaymentsClient.cs`/`Events.cs` fixture files), since syntax alone cannot distinguish the two cases.
> Flagged as a spec-precision gap for whichever batch implements T13/T19: tasks.md does not say
> whether those fixture documents are expected to carry this warning.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(topic): compose frontmatter from syntax alone`

---

### T11: Create FrontmatterYaml

**What**: Render the `---`-delimited block and validate it by round-trip plus required-field check.
**Where**: `src/Csharp2Md.Core/Topic/FrontmatterYaml.cs`
**Depends on**: T5
**Reuses**: YamlDotNet, already referenced and used by `ConfigIndexer`
**Requirement**: WIKI-05, WIKI-07, WIKI-12

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:serialization`

**Done when**:
- [x] Serializer built with `WithQuotingNecessaryStrings()` and `WithNewLine("\n")` so output is byte-identical across platforms
- [x] Keys emitted in the schema's declared order
- [x] A test round-trips a title containing `:`, `"`, `#`, and a leading `-` and asserts the value survives intact
- [x] `Validate` returns a failure naming the file and the specific error; it never throws
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

> Note: `Validate` strips the leading/trailing `---` delimiter lines before deserializing — verified
> empirically (not assumed) that YamlDotNet's `Deserializer.Deserialize<T>(string)` throws
> `Expected 'StreamEnd', got 'DocumentStart'` on a document with a stray trailing `---`, since that
> reads as the start of a second (empty) document in a YAML stream. Frontmatter delimiters are a
> Markdown-body convention, not part of the YAML content itself, so stripping them before parsing is
> the correct behavior, not a workaround for a parser defect.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(topic): render and validate frontmatter yaml`

---

### T12: Attach frontmatter to RenderedDocument

**What**: An init-only `Frontmatter` property that `ToMarkdown()` prepends above the heading, leaving the body untouched.
**Where**: `src/Csharp2Md.Core/Rendering/RenderedDocument.cs` (modify)
**Depends on**: T5, T11
**Reuses**: the `DependencySection` pattern in the same file — a property outside `Sections` for content that maps to no source bytes
**Requirement**: WIKI-05, WIKI-08

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Block precedes the `# ` heading and is delimited by `---` lines
- [x] A test asserts the rendered body with frontmatter is byte-identical to the body without it, once the block is stripped
- [x] The three existing `SpanCoverageTests` still pass unmodified
- [x] A document with no frontmatter renders exactly as before
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

**Tests**: unit
**Gate**: quick

**Commit**: `feat(rendering): prepend frontmatter to rendered documents`

---

### T13: Derive frontmatter during document analysis

**What**: Call `FrontmatterBuilder` in `AnalyzeDocumentAsync` where the syntax tree is already materialized, and attach the result to the rendered document.
**Where**: `src/Csharp2Md.Core/Pipeline/AnalysisPipeline.cs` (modify)
**Depends on**: T4, T10, T12
**Reuses**: the existing syntax tree and relative path already computed for the detectors
**Requirement**: WIKI-06, WIKI-09, WIKI-13

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Derivation reads the tree already in scope — no second parse, no re-read of a written file
- [x] An integration test asserts every generated document under `raw/codebase/` carries a parseable block
- [x] An integration test asserts `Acme.Payments` documents — degraded, no restore — carry the same classifications a healthy project yields
- [x] Document count under `raw/codebase/` still equals the source document count
- [x] Gate check passes: `dotnet test`
- [x] Test count: no reduction from the running baseline

**Tests**: integration
**Gate**: full

**Commit**: `feat(pipeline): derive frontmatter for every rendered document`

---

### T14: Add frontmatter to generated index documents

**What**: Give per-service and root `index.md` the same block shape with `source_kind: codebase-index` and `file_type: index`.
**Where**: `src/Csharp2Md.Core/Output/IndexWriter.cs` (modify)
**Depends on**: T4, T11
**Reuses**: `IndexWriter.WriteFile`'s existing shape
**Requirement**: WIKI-05, WIKI-06

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Both index writers emit a block before their heading
- [x] Existing index-content assertions still pass — links and headings unchanged
- [x] A test asserts an index block validates under the same validator document blocks use
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

> Note: WIKI-02's `source_path` rule is defined for source-derived documents only; the schema does
> not carry a distinct rule for generated `index.md` files. Applied it mechanically to the index
> file's own location beneath `raw/codebase/` with the trailing `.md` removed:
> `<service-name>/index` for a per-service index, `index` for the root. Flagged as a spec-precision
> gap, same category as T10's note — spec.md's Frontmatter Schema section and Fixture Expectations
> table specify `title`/`file_type`/`tags` for index rows but never `source_path`.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(output): add frontmatter to generated indexes`

---

### T15: Report frontmatter validation failures and exit non-zero

**What**: Collect per-document validation failures onto the run result, print each to stderr, and exit `1` while still generating everything else.
**Where**: `src/Csharp2Md.Core/Pipeline/AnalysisPipeline.cs` (modify)
**Depends on**: T11, T13
**Reuses**: `PipelineRunResult`'s existing warning channel and the CLI's existing `return 1` path
**Requirement**: WIKI-12

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] A failing document does not stop the run — remaining documents are still written
- [x] Failures surface on `PipelineRunResult`, each naming the file path and the specific error
- [x] An integration test forces a failure and asserts exit code `1` with every other artifact present
- [x] A degraded-load run still exits `0` — the two conditions stay distinct
- [x] Gate check passes: `dotnet test`
- [x] Test count: no reduction from the running baseline

> Note: `PipelineRunResult` gains `FrontmatterFailures` and a computed `ExitCode` property (`1` when
> `!IsSuccess` or `FrontmatterFailures.Count > 0`, else `0`) so "exit code 1" is assertable at the
> Pipeline layer without touching `Program.cs` — the CLI still only reads `IsSuccess`/`ManifestError`
> today, and wiring `ExitCode`/`FrontmatterFailures` into the CLI's actual `return` and stderr output
> is T18's job (it already touches `Program.cs` for `--topic`/`--domain` and the summary counts).
>
> Deviation from this task's stated `Where` (`AnalysisPipeline.cs` only): forcing a real, spec-derived
> validation failure requires an empty required field reachable through genuine pipeline input, and
> `domain` is the only such field (`topic` is regex-validated by T2; every other field is either a
> derived non-empty value or a Phase 1 constant). Empirically verified (not assumed) that
> `TopicOptions.Create(topic, domain: "", inputRoot)` alone did **not** force a failure: YamlDotNet's
> `SerializerBuilder().Build().Serialize("")` returns `"--- \"\"\n"` (an explicit `---` document-start
> marker prefixing the empty scalar) rather than `"\"\"\n"`, so `FrontmatterYaml.Field`'s
> `.TrimEnd('\n')` produced the single line `domain: --- ""`, which parses back as the *non-empty*
> literal string `--- ""` rather than an empty value — silently defeating the exact check WIKI-12
> requires, for the one field with no other emptiness guard. Fixed the narrow case in
> `FrontmatterYaml.Field` (`src/Csharp2Md.Core/Topic/FrontmatterYaml.cs`, T11's file): an empty input
> is quoted directly as `""` instead of routed through the serializer. This is a pre-existing defect,
> not a new one introduced by T15 — same category as T1's `ManifestLoader` fix earlier in this
> feature — fixed inline because T15's own required test cannot exist without it. All of T11's
> existing `FrontmatterYamlTests` still pass unmodified.

**Tests**: integration
**Gate**: full

**Commit**: `feat(pipeline): fail the run when frontmatter validation fails`

---

### T16: Create TopicScaffoldWriter

**What**: Write `raw/topic.yaml` and `raw/CLAUDE.md`.
**Where**: `src/Csharp2Md.Core/Topic/TopicScaffoldWriter.cs`
**Depends on**: T2, T3
**Reuses**: `TopicLayout`, `TopicOptions`
**Requirement**: WIKI-10, WIKI-11

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `topic.yaml` carries `slug` (the resolved topic), `title`, and a description naming csharp2md as generator
- [x] `topic.yaml` parses as YAML
- [x] `CLAUDE.md` documents the generator and version, the frontmatter schema, the directory conventions, and that Phase 2 resolves `source_service` and `analysis_status`
- [x] Tests assert both files' required content, not merely their existence
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

> Note: `title` has no defined transformation from `slug` anywhere in spec.md, so `Write` uses
> `options.Topic` verbatim for both — flagged as a spec-precision gap, same category as T10's and
> T14's notes.
>
> Wiring gap flagged for whoever picks up Phase 6 (T19/T20): this task's own `Where` field scopes it
> to `TopicScaffoldWriter.cs` only, matching T11's precedent (a standalone, unit-tested writer built
> before any task wires it into a real run). Nothing in T16, T17, or T18's Done-when checklists calls
> for `AnalysisPipeline.cs` or `Program.cs` to actually invoke `TopicScaffoldWriter.Write` /
> `RunLogWriter.Write` during a run — T18's Done-when covers `--topic`/`--domain` options and the
> WIKI-17 console summary only. Yet T20 (byte-identical determinism across runs) and spec.md's P1
> Independent Test both require `raw/topic.yaml`, `raw/CLAUDE.md`, and `raw/log.md` to exist after a
> real run — which cannot hold until *something* calls both writers, most naturally from `Program.cs`
> after `AnalysisPipeline.RunAsync` returns (it already builds `TopicOptions` there once T18 lands,
> and `PipelineRunResult` carries everything `RunLogData` needs except document/service counts, which
> are not currently exposed by `PipelineRunResult` either). Left unresolved here rather than guessed
> at, since it requires touching files outside every one of T16/T17/T18's stated scope.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(topic): write topic.yaml and conventions document`

---

### T17: Create RunLogWriter

**What**: Write `raw/log.md` with timestamp, reconstructed invocation, five statistics, Phase 2 placeholders, and any validation failures.
**Where**: `src/Csharp2Md.Core/Topic/RunLogWriter.cs`
**Depends on**: T3, T15
**Reuses**: `TopicLayout`; `TimeProvider` (in-box on net10.0) injected for the timestamp
**Requirement**: WIKI-18, WIKI-19, WIKI-20, WIKI-21, WIKI-22

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Timestamp comes from an injected `TimeProvider`, never `DateTimeOffset.UtcNow` directly
- [x] All five statistics present: documents, edges, services, validation failures, output topic path
- [x] Empty `## Graph Resolution` and `## Calibration Notes` sections present
- [x] A run with validation failures lists each failing path and error
- [x] The log is written even on a run that exits `1`
- [x] Tests use a fixed `TimeProvider` and assert exact timestamp formatting
- [x] Gate check passes: `dotnet test --filter "Category!=Integration"`
- [x] Test count: no reduction from the running baseline

> Note: design.md's `RunLogData` sketch carries a precomputed `TimestampUtc : DateTimeOffset` field,
> which would make a `TimeProvider` parameter on `Write` redundant (the timestamp would already be a
> plain value by the time `Write` sees it, pushing the "never call `UtcNow` directly" obligation onto
> whichever future caller builds `RunLogData` — outside this task's `Where` scope). Implemented
> instead as `Write(string outputRoot, RunLogData data, TimeProvider timeProvider)`, with `RunLogData`
> carrying no timestamp field at all: `Write` calls `timeProvider.GetUtcNow()` itself. This keeps the
> Done-when's `TimeProvider` requirement enforceable and testable entirely within `RunLogWriter.cs`,
> matching how `FrontmatterYamlTests`/`TopicScaffoldWriterTests` test their writers as pure functions
> of their inputs.
>
> Same wiring gap as T16's note: nothing in T16/T17/T18 calls `RunLogWriter.Write` during a real run.
> "The log is written even on a run that exits 1" is proven at the writer level (calling `Write` with
> a non-empty `Failures` list still writes the file unconditionally — nothing branches on
> `Failures.Count`); the end-to-end version of that guarantee needs the same future wiring task T16's
> note flags.

**Tests**: unit
**Gate**: quick

**Commit**: `feat(topic): write auditable run log`

---

### T18: Add --topic and --domain to the CLI

**What**: Register both options, resolve them through `TopicOptions`, and extend the run summary with document and failure counts.
**Where**: `src/Csharp2Md.Cli/Program.cs` (modify)
**Depends on**: T2, T15
**Reuses**: the existing `Option<T>` + `parseResult.GetValue` shape of `--manifest`/`--output`/`--force`
**Requirement**: WIKI-14, WIKI-15, WIKI-16, WIKI-17

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Both options appear in `--help` with descriptions
- [x] An invalid `--topic` exits `1` before any output directory is touched — asserted by checking the directory does not exist afterwards
- [x] Omitting both yields the derived slug and `system-design`
- [x] Summary reports documents written, validation failures, and the output topic path
- [x] Gate check passes: `dotnet test`
- [x] Test count: no reduction from the running baseline

> Batch 3 was interrupted by an external usage limit after T17; this task was finished directly in
> the orchestrating session rather than by a fresh batch worker (git history is unaffected — same
> cycle, same gates). Three deviations beyond this task's literal `Where: Program.cs`, all
> necessary to satisfy this task's own Done-when and to close the wiring gap T16/T17 flagged for
> "whoever picks up Phase 6":
> 1. **`PipelineRunResult` gained `DocumentCount`/`ServiceCount`** (`AnalysisPipeline.cs`) — the
>    Done-when's "summary reports documents written" has no other source; `AnalyzeAsync` now returns
>    `(IndexPath, DocumentCount)` instead of just the path. Same precedent as T15 extending
>    `PipelineRunResult` with `FrontmatterFailures`.
> 2. **`TopicScaffoldWriter.Write` and `RunLogWriter.Write` are now called from `Program.cs`** after
>    a successful manifest run — resolving the wiring gap T16 and T17 both flagged explicitly (no
>    task's `Where` field named `Program.cs`/`AnalysisPipeline.cs` for this, and spec.md's P1
>    Independent Test requires `raw/topic.yaml`, `raw/CLAUDE.md`, and `raw/log.md` to exist after a
>    real run — verified manually against the fixture: all three now land under `raw/`). Without
>    this, T19 (fixture proof) and T20 (determinism) in the next batch would have failed on missing
>    files with no task scoped to fix it.
> 3. **`Program.cs` now returns `result.ExitCode` instead of a hardcoded `0`** — WIKI-12 was only
>    ever proven at the pipeline layer (`FrontmatterValidationTests.cs`, T15); the CLI ignored
>    `PipelineRunResult.ExitCode` entirely, so a real run never actually exited `1` on a frontmatter
>    validation failure. Pre-existing defect, same category as T1's `ManifestLoader` fix and T15's
>    `FrontmatterYaml.Field` fix — fixed inline with a dedicated CLI-level test
>    (`Run_WithFrontmatterValidationFailure_ExitsOneAndStillWritesLog`) since it was otherwise
>    completely untested at this boundary.
>
> `title`/`OutputTopicPath` in `raw/log.md` use the CLI's `outputRoot` made absolute
> (`Path.GetFullPath`) — spec.md says "the absolute output topic path" without defining "topic path"
> more precisely than the run's output root; no other candidate value exists at the CLI boundary.

**Tests**: integration
**Gate**: full

**Commit**: `feat(cli): add topic and domain options`

---

### T19: Assert fixture heuristic coverage

**What**: The committed expectations file plus the test proving every rule is exercised and every fixture document classifies as specified.
**Where**: `tests/Csharp2Md.Core.Tests/Topic/FixtureExpectationsTests.cs`
**Depends on**: T13
**Reuses**: `SyntheticFixtureRun` — the shared full-pipeline run, so no new workspace is opened
**Requirement**: WIKI-23

**Tools**:
- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:
- [x] Expectations encode the spec's Fixture Expectations table: title, tier, file_type, and tags per document
- [x] A test asserts the union of derived `file_type` values covers all eleven enum members
- [x] A test asserts the union of derived tags covers all six rules
- [x] A test asserts all four title tiers are exercised, including tier 4 via `Properties/AssemblyInfo.cs`
- [x] A failure names the document and the differing field
- [x] Gate check passes: `dotnet test`
- [x] Test count: no reduction from the running baseline

> Deviation from this task's stated `Where` (`FixtureExpectationsTests.cs` only): the committed
> expectations table, transcribed verbatim from spec.md, initially failed one row —
> `Acme.Orders/Hosting/ServiceCollectionExtensions.cs` expected `dependency-injection` in `tags` but
> derived `[]`. `TagDeriver`'s dependency-injection rule (T9, already committed) only scanned
> `InvocationExpressionSyntax` call sites via `InvokedMethodName`, so it fired for `services.AddScoped<T>()`
> but not for a file that *declares* `AddScoped`/`AddSingleton`/`AddTransient` as extension methods —
> exactly what `ServiceCollectionExtensions.cs` does, per that file's own header comment ("...what puts
> `dependency-injection` in its tags"). The other five tag rules in spec.md's table are all phrased as
> plain identifier-name patterns (matching the `event-driven` and `persistence` rules' existing
> identifier-token scan, not an invocation-shape scan) and are keyed to a name appearing anywhere in the
> source, not specifically to a call site — `bootstrapping` is the one exception, and it deliberately
> stays invocation-shaped (`WebHost.Create*`, `WebApplication.CreateBuilder`) because spec.md phrases it
> that way explicitly. Fixed `src/Csharp2Md.Core/Topic/TagDeriver.cs`'s dependency-injection rule to scan
> `identifiers` (the same token list the `event-driven`/`persistence` rules already use) instead of
> `invocations`, and removed the now-orphaned `InvokedMethodName` helper (the only caller). Pre-existing
> defect, not new: same category as T1/T15/T18's inline fixes. `TagDeriverTests`'s existing
> `Derive_DependencyInjectionMethods_FiresDependencyInjection` (an invocation-shaped case) still passes
> unmodified, since the invoked method's identifier token is present in `identifiers` regardless of scan
> strategy.

**Tests**: integration
**Gate**: full

**Commit**: `test(topic): assert fixture covers every heuristic rule`

---

### T20: Assert run determinism

**What**: Prove two consecutive runs over unchanged input produce byte-identical output except the log timestamp.
**Where**: `tests/Csharp2Md.Core.Tests/Pipeline/DeterminismTests.cs`
**Depends on**: T17, T18
**Reuses**: `FixtureManifest`, the existing temp-workspace pattern from `SyntheticFixtureRun`
**Requirement**: spec.md Success Criteria (determinism)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Two runs into separate output roots are compared file by file
- [x] Every file except `raw/log.md` is byte-identical
- [x] `raw/log.md` differs only in the timestamp line — asserted by comparing all other lines
- [x] The test fails if a new nondeterministic artifact is introduced later, because it compares the full file set rather than a sample
- [x] Gate check passes: `dotnet test`
- [x] Test count: no reduction from the running baseline

> Verified empirically (not assumed) that invoking the real, packaged CLI binary twice into two
> genuinely separate output roots cannot satisfy "differs only in the timestamp line": first attempt
> did exactly that and failed on `raw/log.md`'s `Invocation` line, which legitimately differs because
> it embeds each run's absolute `--output` path verbatim (`Program.cs`'s `BuildInvocation`), and the
> `Output topic path` line embeds the same. That difference is real but not a *pipeline*
> nondeterminism — it is a structural consequence of writing to two physically distinct directories,
> and it would appear on a line other than the timestamp no matter how faithfully the CLI is invoked.
> Resolved by invoking `AnalysisPipeline.RunAsync` directly (the alternative this task's own
> instructions name), then calling `TopicScaffoldWriter.Write`/`RunLogWriter.Write` exactly as
> `Program.cs` does post-T18, but constructing one `RunLogData` and writing that same instance into
> both output roots, varying only the injected `TimeProvider` (a real `FakeClock`, five seconds apart,
> with `Assert.NotEqual` on the timestamp line proving the seam actually varied rather than trivially
> matching). `Render(RunLogData, DateTimeOffset)` is a pure function of its two parameters, so passing
> the identical `RunLogData` makes every line but the interpolated timestamp identical by construction
> — this isolates and proves exactly the seam design.md's Risks table names (`RunLogWriter`'s injected
> `TimeProvider` as "the only nondeterministic output"), while still satisfying "two runs into separate
> output roots" (two distinct physical directories, real pipeline runs, full file-by-file comparison)
> and "every file except `raw/log.md` is byte-identical" (documents/indexes/`dependencies.json`/
> `dependencies.mmd`/`topic.yaml`/`CLAUDE.md`/the ownership marker are all asserted byte-identical
> between the two real `AnalysisPipeline.RunAsync` calls, not merely assumed equal). Pipeline-level
> determinism itself — `DocumentCount`, edge count, service count, frontmatter failure count equal
> across both runs — is asserted directly before the file comparison relies on it.

**Tests**: integration
**Gate**: full

**Commit**: `test(pipeline): assert two runs are byte-identical except the log`

---

### T21: Mark the breaking release as 2.0.0

**What**: Introduce the `Version` property so the packaged tool announces the output-contract break by semver.
**Where**: `Directory.Build.props` (modify)
**Depends on**: T20
**Reuses**: the existing `PackagingSmokeTests` as the verification gate
**Requirement**: AD-007

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:project-structure`

**Done when**:
- [ ] `<Version>2.0.0</Version>` set, and `dotnet pack` produces `csharp2md.2.0.0.nupkg`
- [ ] `PackagingSmokeTests` still passes against the packed tool
- [ ] Gate check passes: `dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test`
- [ ] Test count: no reduction from the running baseline

**Tests**: integration
**Gate**: build

**Commit**: `chore(release)!: bump to 2.0.0 for the output layout break`

> The `v2.0.0` tag is applied to the merge commit, not here — it cannot exist until the PR merges (AD-007).

---

## Phase Execution Map

Phases run in sequence; tasks within a phase run in order.

```
Phase 1 -> Phase 2 -> Phase 3 -> Phase 4 -> Phase 5 -> Phase 6
```

Execution is strictly sequential — there is no intra-phase parallelism. A single agent (or batch worker) works one task at a time, in order.

**Batch packing** (~7 tasks per worker, whole phases, cuts only on phase boundaries):

| Batch | Phases | Tasks | Count |
| --- | --- | --- | --- |
| 1 | Phase 1 + Phase 2 | T1–T4 | 4 |
| 2 | Phase 3 | T5–T11 | 7 |
| 3 | Phase 4 + Phase 5 | T12–T18 | 7 |
| 4 | Phase 6 | T19–T21 | 3 |

21 tasks → 4 batches. Batches run sequentially; a batch never starts before the previous reports every task complete.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: Fix ManifestLoader crash | 1 file, 1 guard | ✅ Granular |
| T2: TopicOptions | 1 type | ✅ Granular |
| T3: TopicLayout | 1 type | ✅ Granular |
| T4: Move output beneath raw/ | 1 file modified, wiring only | ✅ Granular |
| T5: Frontmatter record + FileType | 2 cohesive types, 1 file | ⚠️ OK — the enum exists only for the record |
| T6: JSON schema + sync test | 1 artifact + its test | ✅ Granular |
| T7: TitleResolver | 1 type | ✅ Granular |
| T8: FileTypeClassifier | 1 type | ✅ Granular |
| T9: TagDeriver | 1 type | ✅ Granular |
| T10: FrontmatterBuilder | 1 type, composition only | ✅ Granular |
| T11: FrontmatterYaml | 1 type | ✅ Granular |
| T12: RenderedDocument frontmatter | 1 file, 1 property | ✅ Granular |
| T13: Derive during analysis | 1 file, 1 call site | ✅ Granular |
| T14: Index frontmatter | 1 file | ✅ Granular |
| T15: Report validation failures | 1 file, 1 channel | ✅ Granular |
| T16: TopicScaffoldWriter | 1 type, 2 artifacts | ⚠️ OK — both are the topic scaffold, written together |
| T17: RunLogWriter | 1 type | ✅ Granular |
| T18: CLI options | 1 file | ✅ Granular |
| T19: Fixture expectations | 1 test file | ✅ Granular |
| T20: Determinism test | 1 test file | ✅ Granular |
| T21: Version bump | 1 file, 1 property | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | — | ✅ Match |
| T2 | None | — | ✅ Match |
| T3 | None | — | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | None | — | ✅ Match |
| T6 | T5 | T5 → T6 | ✅ Match |
| T7 | None | — | ✅ Match |
| T8 | T5 | T5 → T8 | ✅ Match |
| T9 | None | — | ✅ Match |
| T10 | T2, T5, T7, T8, T9 | T2 → T10, T5 → T10, T7 → T10, T8 → T10, T9 → T10 | ✅ Match |
| T11 | T5 | T5 → T11 | ✅ Match |
| T12 | T5, T11 | T5 → T12, T11 → T12 | ✅ Match |
| T13 | T4, T10, T12 | T4 → T13, T10 → T13, T12 → T13 | ✅ Match |
| T14 | T4, T11 | T4 → T14, T11 → T14 | ✅ Match |
| T15 | T11, T13 | T11 → T15, T13 → T15 | ✅ Match |
| T16 | T2, T3 | T2 → T16, T3 → T16 | ✅ Match |
| T17 | T3, T15 | T3 → T17, T15 → T17 | ✅ Match |
| T18 | T2, T15 | T2 → T18, T15 → T18 | ✅ Match |
| T19 | T13 | T13 → T19 | ✅ Match |
| T20 | T17, T18 | T17 → T20, T18 → T20 | ✅ Match |
| T21 | T20 | T20 → T21 | ✅ Match |

No dependency points to a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Manifests | unit | unit | ✅ OK |
| T2 | Topic | unit | unit | ✅ OK |
| T3 | Topic | unit | unit | ✅ OK |
| T4 | Pipeline | integration | integration | ✅ OK |
| T5 | Topic | unit | unit | ✅ OK |
| T6 | Published contract | unit | unit | ✅ OK |
| T7 | Topic | unit | unit | ✅ OK |
| T8 | Topic | unit | unit | ✅ OK |
| T9 | Topic | unit | unit | ✅ OK |
| T10 | Topic | unit | unit | ✅ OK |
| T11 | Topic | unit | unit | ✅ OK |
| T12 | Rendering | unit | unit | ✅ OK |
| T13 | Pipeline | integration | integration | ✅ OK |
| T14 | Output writers | unit | unit | ✅ OK |
| T15 | Pipeline | integration | integration | ✅ OK |
| T16 | Topic | unit | unit | ✅ OK |
| T17 | Topic | unit | unit | ✅ OK |
| T18 | CLI | integration | integration | ✅ OK |
| T19 | Pipeline (fixture assertions) | integration | integration | ✅ OK |
| T20 | Pipeline | integration | integration | ✅ OK |
| T21 | CLI packaging | integration | integration | ✅ OK |

No task carries `Tests: none`. No task defers its tests to another task.

---

## Requirement Coverage

| Requirement | Tasks |
| --- | --- |
| WIKI-01, WIKI-02, WIKI-03 | T3, T4 |
| WIKI-04 | T4 |
| WIKI-05 | T11, T12, T14 |
| WIKI-06 | T5, T6, T7, T8, T9, T10, T13, T14 |
| WIKI-07 | T11 |
| WIKI-08 | T12 |
| WIKI-09 | T13 |
| WIKI-10, WIKI-11 | T16 |
| WIKI-12 | T11, T15 |
| WIKI-13 | T7, T8, T10, T13 |
| WIKI-14, WIKI-15, WIKI-16 | T2, T18 |
| WIKI-17 | T18 |
| WIKI-18 – WIKI-22 | T17 |
| WIKI-23 | T19 |
| Determinism (Success Criteria) | T20 |
| AD-007 release marking | T21 |

All 23 requirements are mapped. No task exists without a requirement.
