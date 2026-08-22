# Component Graph Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and
Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the
full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**User-confirmed deviation from the standard flow:** the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by standing user request, as it was for
`symbol-index`, `relation-collector`, `data-access-discovery` and `relation-resolver`. No mutants are
injected at any point. The Verifier's spec-anchored outcome check, per-AC `file:line` evidence, and
`validation.md` report still run as normal.

---

**Spec**: `.specs/features/component-graph/spec.md`
**Design**: `.specs/features/component-graph/design.md`
**Status**: Approved

**Scope of this task list**: P1 only — `COMP-01` through `COMP-32`. The spec's P2 (`COMP-40`, `COMP-41`,
service-level rollup) and P3 (`COMP-50`, placeholder nodes) stories are deliberately not broken down here.

**Design amendment made during task breakdown**: `ComponentGraphProjection` also carries
`ImmutableArray<ComponentGraphEdge> Edges` — the deduped edge set the Mermaid is rendered *from*. This makes
edge selection (T4) verifiable without depending on the renderer (T5), and the edge set is a real product of
the projector rather than a test-only hook: a future JSON graph or component page consumes the same list.

**Tools**: no MCP is used anywhere in this feature — every task is local C# authoring, testing or Markdown.
Skills come from `CLAUDE.md`'s routing table (`dotnet-skills:*` for authoring style, `dotnet-test:*` for test
quality), named per task below.

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/Csharp2Md.Core.Tests/Projection/Aggregates/RelationProjectorTests.cs`,
> `DatabaseAggregateProjectorTests.cs`, `CanonicalAggregateWriterTests.cs`, `ResolutionMetricsProjectorTests.cs`,
> `Analysis/Relations/RelationFragmentBuilderTests.cs`, `Analysis/Indexes/SymbolIndexTests.cs`,
> `Analysis/AnalysisEngineTests.cs`, `Analysis/RelationResolverEndToEndTests.cs`,
> `Analysis/DataAccessDiscoveryEndToEndTests.cs`, `Analysis/V3DeterminismTests.cs`) and project guidelines.
> Guidelines found: `AGENTS.md` / `CLAUDE.md` — they route test quality to the `dotnet-test:*` skills as
> post-hoc gates rather than declaring a coverage threshold. No coverage-threshold tool config and no CI
> workflow exist in this repo, so strong defaults apply to the Coverage Expectation column.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Component fact production (`ComponentFragmentBuilder`, `ComponentFragmentResult`) | unit | All branches; 1:1 to COMP-01/03/04/08; the empty-input short circuit and the duplicate-identity throw each proven, not assumed | `tests/Csharp2Md.Core.Tests/Analysis/Components/ComponentFragmentBuilderTests.cs` (new) | `dotnet test csharp2md.slnx` |
| Graph node resolution (`GraphNodeIndex`, `GraphNode`, `GraphNodeShape`) | unit | All branches; one test per endpoint shape in the design's resolution table, plus the unresolvable shape and the one-project-two-components throw | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/GraphNodeIndexTests.cs` (new) | `dotnet test csharp2md.slnx` |
| Graph projection (`ComponentGraphProjector`, `ComponentGraphProjection`) | unit | All branches; 1:1 to COMP-05, COMP-10..COMP-26, COMP-30, COMP-32; every drop rule proven by a case that would otherwise render | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/ComponentGraphProjectorTests.cs` (new) | `dotnet test csharp2md.slnx` |
| Relation projection, narrowed (`RelationProjector`) | unit | The 10 surviving partition tests keep passing; the 3 component/Mermaid tests are rewritten stricter into the projector suite above, never deleted | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/RelationProjectorTests.cs` | `dotnet test csharp2md.slnx` |
| Aggregate contracts + writer (`AggregateOutputSnapshot`, `CanonicalAggregateWriter`) | unit | Both files written on every run including the empty one; `SchemaVersion` still 5 | `tests/Csharp2Md.Core.Tests/Projection/Aggregates/CanonicalAggregateWriterTests.cs` | `dotnet test csharp2md.slnx` |
| Pipeline wiring (`AnalysisEngine`) | integration | The component fragment reaches the manifest; `C2M-CG-001` reaches `raw/facts/diagnostics.json` from a real run, not from a projector unit test | `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs` | `dotnet test csharp2md.slnx` |
| End-to-end pipeline | integration | All four P1 Independent Tests verbatim, plus every Edge Case listed in spec.md | `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs` (new, mirrors `RelationResolverEndToEndTests.cs`) | `dotnet test csharp2md.slnx` |
| Determinism | integration | Two runs over identical input produce byte-identical `raw/dependencies.mmd` | `tests/Csharp2Md.Core.Tests/Analysis/V3DeterminismTests.cs` | `dotnet test csharp2md.slnx` |
| Decision and spec documents (`STATE.md`, `spec.md`, roadmap) | none | Build gate only — Markdown, no behaviour | `.specs/**`, `llmwiki-reverse-engineering-roadmap.md` | build gate only |

## Gate Check Commands

> Generated from the project's established commands, consistent with every prior feature's handoff in
> `.specs/STATE.md`. No CI workflow file exists in this repo to source them from instead.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks with unit tests only | `dotnet test csharp2md.slnx` |
| Full | After tasks with integration tests (pipeline wiring, fixture-level, determinism) | `dotnet test csharp2md.slnx` (integration tests live in the same suite; no separate command exists in this repo) |
| Build | After phase completion and for document-only tasks | `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx` |

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a
phase execute in order.

### Phase 1: Facts and node resolution

The two pure building blocks, plus persistence of the first one.

```
T1 → T2
T3
```

### Phase 2: Graph projection

Everything that turns relations plus nodes into the two output documents.

```
T3 → T4 → T5 → T6
T4 → T7
T4 → T8
```

### Phase 3: Wiring and the superseded path

Removes the broken implementation and plugs the new one in at the correct point in the pipeline.

```
T5 → T9 → T10 → T11
T7 → T9
```

### Phase 4: End-to-end proof

Each of the spec's Independent Tests, run against the real CLI over the real fixture.

```
T11 → T12
T11 → T13
T11 → T14
T11 → T15
```

### Phase 5: Close out

```
T12 → T16 → T17
T13 → T16
T14 → T16
T15 → T16
```

---

## Task Breakdown

### T1: Build component facts from project facts

**What**: Add `ComponentFragmentResult` and `ComponentFragmentBuilder.Build`, minting one `ComponentFact` per
`ProjectFact` and validating them into the run's single solution-level component fragment.
**Where**: `src/Csharp2Md.Core/Analysis/Components/ComponentFragmentBuilder.cs`
**Depends on**: None
**Reuses**: `RelationFragmentBuilder`'s exact structure — empty-input short circuit, `validate(FactValidationInput.Create(...))`, a result record with a static `Empty`
**Requirement**: COMP-01, COMP-03, COMP-04, COMP-08

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] `ComponentKind` is `"project"` and `ProjectIds` holds exactly the one owning `ProjectFactId`
- [ ] Header `Resolution` is copied from the source `ProjectFact.Header.Resolution`, never hardcoded — a test with a `Syntactic` project and one with an `Exact` project both assert the mirrored value
- [ ] Header carries `new FactProvenance("csharp2md.components", "1")` and an empty `Evidence` array
- [ ] Two projects resolving to the same `ComponentFactId` throw `InvalidOperationException`
- [ ] Empty project input returns `ComponentFragmentResult.Empty` with no fragment and no diagnostics
- [ ] `Components` is exposed on the result separately from `Fragment`
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(components): mint a component fact per analysed project`

---

### T2: Persist the component fragment through the pipeline

**What**: Build the component fragment in `AnalyzeAsync` pass two, extend `knownFactIds` with every
`ProjectFactId` so `C2M-FV-002` accepts its references, persist it, and add it to `aggregateFragments`.
**Where**: `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs`
**Depends on**: T1
**Reuses**: the `databaseResolution` / `relations` persistence blocks immediately above it, including their `structuralFailure` handling
**Requirement**: COMP-02, COMP-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] The component fragment appears in `raw/facts/manifest.json` after a real `AnalyzeAsync` run
- [ ] `knownFactIds` includes every project id; a component referencing an unknown project id yields `C2M-FV-002` and sets `structuralFailure`
- [ ] A run over a multi-solution input produces one component for a project reached twice, not two
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `feat(components): persist the component fragment`

---

### T3: Resolve a relation endpoint to a graph node

**What**: Add `GraphNode`, `GraphNodeShape` and `GraphNodeIndex` with a flat `FrozenDictionary<FactId, GraphNode>`
built from components, projects, documents, symbols, database objects and database columns.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/GraphNodeIndex.cs`
**Depends on**: None
**Reuses**: `SymbolIndex`'s `FrozenDictionary` + ordinal-comparer discipline
**Requirement**: COMP-09, COMP-21

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] One test per row of the design's resolution table: project id, document id, symbol id, database object id, database column id
- [ ] A column id resolves to its owning object's node, and the column itself is never a node
- [ ] An unrelated `FactId` returns `false` with a null node
- [ ] A project id claimed by two components throws `InvalidOperationException`
- [ ] Node labels come from `ProjectFact.Name` / `DatabaseObjectFact.Name`; `NodeId` is the component or object identity
- [ ] No test and no production line parses or unquotes a `FactId` string
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(graph): resolve relation endpoints to graph nodes`

---

### T4: Select and dedupe the graph's edges

**What**: Add `ComponentGraphProjection`, `ComponentGraphEdge` and `ComponentGraphProjector.Project`'s edge
selection: walk validated fragments, resolve both endpoints, apply the three drop rules, and group survivors
into deduped edges carrying a count.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/ComponentGraphProjector.cs`
**Depends on**: T3
**Reuses**: `RelationProjector.Project`'s fragment-walking loop
**Requirement**: COMP-10, COMP-12, COMP-13, COMP-14, COMP-19, COMP-21, COMP-22

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] A relation whose endpoints resolve to two different nodes yields one edge
- [ ] A relation with a null `TargetId` is dropped
- [ ] A relation whose source or target resolves to no node is dropped and counted as omitted
- [ ] A relation whose endpoints resolve to the same node is dropped
- [ ] Three relations sharing (source, target, partition, kind) collapse to one edge with `Count == 3`
- [ ] Two relations differing only in kind stay two edges
- [ ] A column-targeted relation folds into the same group as an object-targeted relation of the same kind
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(graph): select and dedupe component graph edges`

---

### T5: Render the Mermaid flowchart

**What**: Render node lines and edge lines from the deduped edge set — positional `nodeN` aliases, rectangle
versus cylinder shapes, and the `<partition>:<kind> ×<count>` edge label.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/ComponentGraphProjector.cs`
**Depends on**: T4
**Reuses**: `FactualJsonMapper.WireRelationPartition` for the partition name in a label
**Requirement**: COMP-11, COMP-15, COMP-17, COMP-20, COMP-23, COMP-32

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] A project node renders as `    node0["Acme.Orders"]` and a database node as `    node1[("order_headers")]`
- [ ] An edge renders as `    node0 -->|structural:calls ×5| node1`
- [ ] A node touched by no surviving edge produces no node line
- [ ] Zero surviving edges produces exactly `flowchart LR\n` with no node lines
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(graph): render the component flowchart`

---

### T6: Make the diagram deterministic and escape-safe

**What**: Apply the `(Label, NodeId)` node ordering and the six-part edge ordering, and add `Escape` with the
new `|` rule.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/ComponentGraphProjector.cs`
**Depends on**: T5
**Reuses**: `RelationProjector.Escape`'s existing four replacements, moved and extended
**Requirement**: COMP-16, COMP-18, COMP-30

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Feeding the same facts in two different input orders produces identical output strings
- [ ] Two database objects sharing a `Name` on different connections stay two nodes in a stable order — proven by a test that would pass either way if ordering keyed on label alone
- [ ] `Escape` maps `#`→`#35;`, `"`→`#quot;`, `|`→`#124;`, `\r`→ empty, `\n`→ single space, each with its own assertion
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(graph): order and escape the flowchart deterministically`

---

### T7: Render the component index

**What**: Render `components.md`'s body from the `ComponentFact`s the fragments carry — heading, then each
component ordered by component id with its kind and project ids.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/ComponentGraphProjector.cs`
**Depends on**: T4
**Reuses**: `RelationProjector.ComponentIndex`'s existing Markdown shape, moved
**Requirement**: COMP-05, COMP-24

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Every component appears, ordered by component id, with its kind and project ids
- [ ] A component with no edge in the diagram still appears in the index
- [ ] No database object appears, proven by a case whose fragments contain database objects
- [ ] Zero components produces exactly `# Components\n`
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(graph): render the component index`

---

### T8: Report omitted relations as one summary diagnostic

**What**: Emit `C2M-CG-001` once per run when at least one relation was dropped for an unmapped endpoint,
carrying the count and anchored on the ordinal-first omitted relation.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/ComponentGraphProjector.cs`
**Depends on**: T4
**Reuses**: `DatabaseAggregateProjector`'s representative-fact anchoring pattern
**Requirement**: COMP-25, COMP-26

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Five relations with unmapped endpoints produce exactly one diagnostic carrying `omitted_relation_count` = 5
- [ ] Severity is `Information` and stage is `Projection`
- [ ] A run whose only drops are self-edges and null targets produces no diagnostic at all
- [ ] The anchor fact id is stable across two runs with shuffled input order
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(graph): summarise omitted relations in one diagnostic`

---

### T9: Narrow RelationProjector to its partition job

**What**: Delete the `ComponentFact` case, the `Components` field, `Mermaid`, `ComponentIndex` and `Escape`;
narrow `RelationProjectionResult` to `Partitions` alone.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/RelationProjector.cs`
**Depends on**: T5, T7
**Reuses**: nothing new — this is removal of the superseded implementation
**Requirement**: COMP-05, COMP-11

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:slopwatch`

**Done when**:

- [ ] The duplicate-relation-identity throw and the `Enum.GetValues<RelationPartition>()` projection survive untouched
- [ ] The 3 component/Mermaid tests are rewritten into `ComponentGraphProjectorTests.cs` asserting real edges — they currently assert an empty diagram, so the replacements must be strictly stronger
- [ ] No test is deleted without a stronger replacement named in the commit body
- [ ] The other 10 `RelationProjectorTests` still pass unchanged
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release`, then `dotnet format csharp2md.slnx --verify-no-changes`, then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `refactor(projection)!: narrow RelationProjector to relation partitions`

---

### T10: Carry the graph projection through the snapshot

**What**: Add `ComponentGraphProjection? Graph = null` to `AggregateOutputSnapshot` and have
`CanonicalAggregateWriter` write `raw/dependencies.mmd` and `raw/codebase/components.md` from it.
**Where**: `src/Csharp2Md.Core/Projection/Aggregates/CanonicalAggregateWriter.cs`
**Depends on**: T9
**Reuses**: the existing `?? "flowchart LR\n"` / `?? "# Components\n"` fallbacks at lines 85-86
**Requirement**: COMP-06, COMP-07

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] `Graph` is a trailing optional parameter, mirroring `Relations` and `Database`
- [ ] A snapshot with no graph still writes both files with their empty-document contents
- [ ] `FactualJsonSerializer.SchemaVersion` is asserted to still be 5 and `schemas/facts.schema.json` is unmodified
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(projection): write the graph from its own snapshot member`

---

### T11: Project the graph before coverage is computed

**What**: Build the node index and project the graph on their own lines in `AnalyzeAsync`, adding the
projector's diagnostics to `analysisDiagnostics` **before** `CoverageProjector.Project` runs.
**Where**: `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs`
**Depends on**: T10
**Reuses**: the `databaseAggregate` block at lines 212-213, which is the existing precedent for a projector whose diagnostics precede coverage
**Requirement**: COMP-27

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] `ComponentGraphProjector.Project` is called on its own statement, not inline in the `snapshot` constructor
- [ ] `C2M-CG-001` is present in `raw/facts/diagnostics.json` after a real `AnalyzeAsync` run over a fixture that omits at least one relation — asserted against the written file, not against the projector's return value
- [ ] A `C2M-CG-001` diagnostic does not change the run's exit code
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release`, then `dotnet format csharp2md.slnx --verify-no-changes`, then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: integration
**Gate**: build

**Commit**: `feat(graph): project the component graph before coverage`

---

### T12: Prove the component-facts Independent Test

**What**: Add `ComponentGraphEndToEndTests` and prove P1 story 1's Independent Test verbatim against a real
CLI run over `fixtures/SyntheticSolution`.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs`
**Depends on**: T11
**Reuses**: `RelationResolverEndToEndTests.cs`'s run-once-assert-many fixture structure
**Requirement**: COMP-01, COMP-02, COMP-03, COMP-04, COMP-05, COMP-29

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] The manifest leads to a fragment holding exactly 5 `ComponentFact`s, named individually
- [ ] Each carries `resolution: "syntactic"` in the default syntax-only mode
- [ ] `Acme.DoesNotExist` has a component, appears in `components.md`, and appears nowhere in `dependencies.mmd`
- [ ] `components.md` lists all 5 and nothing else
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `test(graph): prove the component-facts independent test`

---

### T13: Prove the deduped-edges Independent Test

**What**: Prove P1 story 2's Independent Test verbatim — the 7 named non-`data` edges, the counts, and the
absence of any self-edge.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs`
**Depends on**: T11
**Reuses**: T12's shared run fixture
**Requirement**: COMP-10, COMP-11, COMP-12, COMP-17, COMP-19, COMP-28

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] `Acme.Orders → Acme.Shared.Contracts` labelled `structural:calls ×5` is asserted by exact line
- [ ] All 7 non-`data` edges from the spec's Independent Test are asserted, each by source, target and kind
- [ ] No edge has the same source and target node
- [ ] `Acme.Broken` has a component but no edge, confirming COMP-28
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `test(graph): prove the deduped-edges independent test`

---

### T14: Prove the database-nodes Independent Test

**What**: Prove P1 story 3's Independent Test verbatim — cylinder nodes for the three database objects, the
`data:writes-column ×4` edge, and the absence of any column node.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/ComponentGraphEndToEndTests.cs`
**Depends on**: T11
**Reuses**: T12's shared run fixture
**Requirement**: COMP-20, COMP-21, COMP-22, COMP-23, COMP-24, COMP-30

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] `order_headers`, `Orders` and `usp_RebuildOrderTotals` each render as `[("…")]`
- [ ] Each is reached by an edge from the `Acme.Orders` rectangle
- [ ] A `data:writes-column ×4` edge into `Orders` is asserted by exact line
- [ ] No node is named after a column, and `components.md` mentions none of the three objects
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `test(graph): prove the database-nodes independent test`

---

### T15: Prove the diagram is byte-identical across runs

**What**: Extend `V3DeterminismTests` to assert two runs over identical input write byte-identical
`raw/dependencies.mmd` and `raw/codebase/components.md`.
**Where**: `tests/Csharp2Md.Core.Tests/Analysis/V3DeterminismTests.cs`
**Depends on**: T11
**Reuses**: the file's existing two-run comparison helpers
**Requirement**: COMP-16, COMP-32

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Both files compared byte-for-byte, not line-count or length
- [ ] The assertion would fail if node aliases were assigned in discovery order — confirmed by reasoning recorded in the commit body
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: integration
**Gate**: full

**Commit**: `test(graph): pin the diagram's byte-level determinism`

---

### T16: Record AD-021 and update the roadmap

**What**: Append `AD-021` to `.specs/STATE.md` superseding `AD-020`, set `AD-020`'s status to
`superseded by AD-021`, and flip the roadmap's `component-graph` row to CONCLUÍDO.
**Where**: `.specs/STATE.md`
**Depends on**: T12, T13, T14, T15
**Reuses**: the existing `AD-NNN` entry shape — Decision, Reason, Trade-off, Scope, Date, Status
**Requirement**: COMP-07

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] `AD-021` states the one-component-per-project rule, the deduped-edge rule, and that database objects are nodes without being `ComponentFact`s
- [ ] `AD-021` explicitly records that `AD-015`'s orphaned `Detection/` question stays open
- [ ] `AD-020`'s status reads `superseded by AD-021`
- [ ] The roadmap §9 `component-graph` row and the §B.5 row both reflect completion
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release`, then `dotnet format csharp2md.slnx --verify-no-changes`, then `dotnet test csharp2md.slnx`

**Tests**: none
**Gate**: build

**Commit**: `docs(state): supersede AD-020 with the real component graph`

---

### T17: Close the requirement traceability

**What**: Flip all 32 P1 rows in the spec's Requirement Traceability table from Pending to Verified, citing
the covering test for each.
**Where**: `.specs/features/component-graph/spec.md`
**Depends on**: T16
**Reuses**: `relation-resolver`'s closing traceability commit as the format precedent
**Requirement**: COMP-07

**Tools**:

- MCP: NONE
- Skill: NONE

**Done when**:

- [ ] Every `COMP-01`..`COMP-32` row reads Verified
- [ ] The P2/P3 rows stay Pending and are not silently flipped
- [ ] The Coverage line reflects the final counts
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release`, then `dotnet format csharp2md.slnx --verify-no-changes`, then `dotnet test csharp2md.slnx`

**Tests**: none
**Gate**: build

**Commit**: `docs(spec): close the component-graph P1 traceability`

---

## Phase Execution Map

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5

Phase 1:  T1, T2, T3
Phase 2:  T4, T5, T6, T7, T8
Phase 3:  T9, T10, T11
Phase 4:  T12, T13, T14, T15
Phase 5:  T16, T17
```

Arrows live in the per-phase blocks under **Execution Plan** above; this map only shows phase order and
membership, so the two never drift into disagreeing about dependencies.

Execution is strictly sequential - there is no intra-phase parallelism.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: Component fact builder | 1 file, 1 builder + its result record | ✅ Granular |
| T2: Persist the fragment | 1 file, 1 insertion block | ✅ Granular |
| T3: Graph node index | 1 file, 1 index + 2 value types | ✅ Granular |
| T4: Edge selection | 1 file, 1 function | ✅ Granular |
| T5: Mermaid rendering | 1 file, 1 function | ✅ Granular |
| T6: Ordering and escaping | 1 file, 2 cohesive concerns | ✅ Granular |
| T7: Component index | 1 file, 1 function | ✅ Granular |
| T8: Omission diagnostic | 1 file, 1 diagnostic | ✅ Granular |
| T9: Narrow RelationProjector | 1 file, removal only | ✅ Granular |
| T10: Snapshot member + writer | 1 writer + 1 record parameter | ✅ Granular |
| T11: Pipeline wiring | 1 file, 1 insertion block | ✅ Granular |
| T12-T14: One Independent Test each | 1 test file, 1 story each | ✅ Granular |
| T15: Determinism assertion | 1 test file, 1 assertion pair | ✅ Granular |
| T16: AD-021 | 1 decision record | ✅ Granular |
| T17: Traceability | 1 table | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | no inbound arrow | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | None | no inbound arrow | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | T5 → T6 | ✅ Match |
| T7 | T4 | T4 → T7 | ✅ Match |
| T8 | T4 | T4 → T8 | ✅ Match |
| T9 | T5, T7 | T5 → T9, T7 → T9 | ✅ Match |
| T10 | T9 | T9 → T10 | ✅ Match |
| T11 | T10 | T10 → T11 | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T11 | T11 → T13 | ✅ Match |
| T14 | T11 | T11 → T14 | ✅ Match |
| T15 | T11 | T11 → T15 | ✅ Match |
| T16 | T12, T13, T14, T15 | T12 → T16, T13 → T16, T14 → T16, T15 → T16 | ✅ Match |
| T17 | T16 | T16 → T17 | ✅ Match |

No task depends on a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Component fact production | unit | unit | ✅ OK |
| T2 | Pipeline wiring | integration | integration | ✅ OK |
| T3 | Graph node resolution | unit | unit | ✅ OK |
| T4 | Graph projection | unit | unit | ✅ OK |
| T5 | Graph projection | unit | unit | ✅ OK |
| T6 | Graph projection | unit | unit | ✅ OK |
| T7 | Graph projection | unit | unit | ✅ OK |
| T8 | Graph projection | unit | unit | ✅ OK |
| T9 | Relation projection, narrowed | unit | unit | ✅ OK |
| T10 | Aggregate contracts + writer | unit | unit | ✅ OK |
| T11 | Pipeline wiring | integration | integration | ✅ OK |
| T12 | End-to-end pipeline | integration | integration | ✅ OK |
| T13 | End-to-end pipeline | integration | integration | ✅ OK |
| T14 | End-to-end pipeline | integration | integration | ✅ OK |
| T15 | Determinism | integration | integration | ✅ OK |
| T16 | Decision documents | none | none | ✅ OK |
| T17 | Spec documents | none | none | ✅ OK |
