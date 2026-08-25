# Knowledge Taxonomy Contract Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and
Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the
full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**User-confirmed deviation from the standard flow:** the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by standing user request, as it was for
`symbol-index`, `relation-collector`, `data-access-discovery` and `relation-resolver`. No mutants are injected
at any point. The Verifier's spec-anchored outcome check, per-AC `file:line` evidence, and `validation.md`
report still run as normal.

**Phase-end quality gate (user-confirmed, applies to every phase):** the last task of each phase - T2, T5, T9,
T16, T22, T27, T33, T40, T47, T51, T54 - additionally runs `dotnet-skills:slopwatch` over that phase's changes
and reports clean before the phase is considered complete. This is in addition to each task's own `Tools` list.

---

**Spec**: `.specs/features/knowledge-taxonomy-contract/spec.md`
**Context**: `.specs/features/knowledge-taxonomy-contract/context.md`
**Design**: `.specs/features/knowledge-taxonomy-contract/design.md`
**Status**: Approved

**Scope of this task list**: the whole feature. The spec has a single P1 tier by confirmed decision, so all
91 requirements `TAX-01` through `TAX-91` are broken down here; nothing is deferred to a later pass.

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/Csharp2Md.Core.Tests/Facts/Identity/FactIdentityTests.cs`,
> `Facts/Metadata/FactMetadataTests.cs`, `Facts/Validation/FactValidatorTests.cs`,
> `Topic/FrontmatterSchemaSyncTests.cs`, `Analysis/Relations/RelationContractsTests.cs`,
> `Projection/Aggregates/RetrievalIndexContractsTests.cs`, `TestPaths.cs`,
> `tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj`) and project guidelines. Guidelines found:
> `AGENTS.md` and `CLAUDE.md` (they route test quality to the `dotnet-test:*` skills as post-hoc gates and
> declare no coverage threshold), `Directory.Build.props:7` (`TreatWarningsAsErrors`),
> `.config/dotnet-tools.json` (slopwatch). No coverage-threshold tool config and no CI workflow exist in this
> repository, so strong defaults apply to the Coverage Expectation column.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Project and solution wiring (`Csharp2Md.Domain.csproj`, `Csharp2Md.Domain.Tests.csproj`, `csharp2md.slnx`) | none | Build gate only - declarative MSBuild files with no branches | `src/Csharp2Md.Domain/*.csproj`, `csharp2md.slnx` | build gate only |
| Assembly isolation invariants (referenced-assembly set, forbidden namespace surface) | unit | 1:1 to TAX-03, TAX-04, TAX-06; each forbidden namespace asserted by name, not as a group | `tests/Csharp2Md.Domain.Tests/Isolation/*Tests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Identity grammar and identity types (`Identity/**`) | unit | All branches; 1:1 to TAX-68..TAX-78; every rejection path asserts the named parameter; determinism asserted by byte equality across a simulated clone-path change and a shuffled input order | `tests/Csharp2Md.Domain.Tests/Identity/*Tests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Closed vocabularies (`Facets/**`, `Proof/**` enums, `ObservationKind`, `RelationKind`) | unit | Set equality against the documented value set for every axis - no extra and no missing member; one out-of-vocabulary rejection per axis naming axis and value; wire pairing total and injective in both directions | `tests/Csharp2Md.Domain.Tests/Facets/*Tests.cs`, `Proof/*Tests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Registry descriptor tables and enforcement API (`Registry/**`) | unit | All branches; every declared entry asserted by set equality; per relation one accepted registered triple and one rejected unregistered triple; duplicate declaration fails naming the duplicate | `tests/Csharp2Md.Domain.Tests/Registry/*Tests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Fact records (`Facts/**`) | unit | All branches; 1:1 to the family ACs TAX-08..TAX-17 and TAX-27..TAX-30; every identity-component guard and facet guard rejected in turn with its component named | `tests/Csharp2Md.Domain.Tests/Facts/*Tests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Observation contract (`Observations/**`) | unit | All branches; 1:1 to TAX-32..TAX-41; the identity rule proven by pairs differing only by locator and only by ordinal; each required metadata component omitted in turn | `tests/Csharp2Md.Domain.Tests/Observations/*Tests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Relation records and proof invariants (`Relations/**`, `Proof/**` records) | unit | All branches; 1:1 to TAX-44..TAX-53 and TAX-60..TAX-67; every required component of a confirmed relation omitted in turn; each non-confirmed record rejected from the confirmed set | `tests/Csharp2Md.Domain.Tests/Relations/*Tests.cs`, `Proof/*Tests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Literals and suspected-secret evidence (`Literals/**`) | unit | All branches; every allowlisted role accepted and one out-of-allowlist literal rejected naming the field; reflection proves no original-literal and no secret-hash member | `tests/Csharp2Md.Domain.Tests/Literals/*Tests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Registry projection and drift gate (`tests/Csharp2Md.Domain.Tests/Registry/TaxonomyRegistryWriter.cs`) | unit | Two runs byte-identical; the committed file compared byte for byte; a hand edit and a duplicate declaration each fail naming the differing or duplicated entry | `tests/Csharp2Md.Domain.Tests/Registry/*Tests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Cross-surface reflection invariants | unit | Whole public and internal domain surface: no numeric confidence member, no mutating member, every TAX ID carried by at least one test trait | `tests/Csharp2Md.Domain.Tests/Surface/*Tests.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Committed contract artifact (`contracts/taxonomy-registry.json`) | none | Generated data, not code - its correctness is the drift gate's assertion in the layer above | `contracts/taxonomy-registry.json` | build gate only |

## Gate Check Commands

> Generated from the repository's own build and test entry points (`csharp2md.slnx`, `global.json`,
> `tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj`) and consistent with every prior feature's gate in
> `.specs/STATE.md`. No CI workflow file exists in this repository to source them from instead. The test stack
> is xUnit 2.9.3 on `Microsoft.NET.Test.Sdk` (VSTest), so `dotnet test` takes a project or solution path.
> No `.editorconfig` exists, so `dotnet format` enforces whitespace and import ordering only.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks whose tests live only in the new domain test project | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Full | After tasks that change the solution surface, the committed contract artifact, or anything the legacy tree could notice | `dotnet test csharp2md.slnx` |
| Build | After phase completion and for project- or artifact-only tasks | `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx` |

Every task carrying the `build` gate is a phase-end task, so it also runs `dotnet-skills:slopwatch` over the
phase's changes per the protocol above. The tool is pinned locally in `.config/dotnet-tools.json`.

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a
phase execute in the listed order. Each phase diagram shows the intra-phase chain; `Depends on` in a task body
names its execution predecessor, and the first task of each phase depends on the last task of the phase before
it.

### Phase 1: Assembly foundation and isolation

The dependency-free assembly, its test project, and the structural invariant that keeps it isolated.

```
T1 → T2
```

### Phase 2: Identity grammar

The ported `id1:` grammar and the two primitives every identity and every fact reference is built from.

```
T3 → T4 → T5
```

### Phase 3: Scoped identities and determinism

Workspace, solution, project and analysis-variant identities, then the cross-type determinism proof.

```
T6 → T7 → T8 → T9
```

### Phase 4: Closed vocabularies

Every closed axis as an independent enum, then the explicit CLR-to-wire pairing that validates and publishes
them. Nothing here consults the registry, so the registry can be declared against it in Phase 5.

```
T10 → T11 → T12 → T13 → T14 → T15 → T16
```

### Phase 5: Registry tables and enforcement

The declarative taxonomy and the guards that read it. This is the single authority AD-013 names.

```
T17 → T18 → T19 → T20 → T21 → T22
```

### Phase 6: Literals and evidence primitives

The source-evidence value types and the literal boundary that keeps secrets out of payloads.

```
T23 → T24 → T25 → T26 → T27
```

### Phase 7: Observations and promotion audit trail

The observation contract and the promotion record that carries why a fact was produced.

```
T28 → T29 → T30 → T31 → T32 → T33
```

### Phase 8: Fact records

The five families and their seventeen types, closed by a bijection test against the registry.

```
T34 → T35 → T36 → T37 → T38 → T39 → T40
```

### Phase 9: Relations and proof invariants

Construction-time triple enforcement, the three non-confirmed outcomes, and the confirmed-set boundary.

```
T41 → T42 → T43 → T44 → T45 → T46 → T47
```

### Phase 10: Registry projection and drift gate

The emitter outside the domain, the committed artifact, and the byte comparison that stops them drifting.

```
T48 → T49 → T50 → T51
```

### Phase 11: Cross-surface closure

The invariants that can only be asserted over the finished surface.

```
T52 → T53 → T54
```

---

## Task Breakdown

### Phase 1: Assembly foundation and isolation

#### T1: Create the dependency-free domain project

**What**: Add `src/Csharp2Md.Domain/Csharp2Md.Domain.csproj` targeting `net10.0` with no `PackageReference`
and no `ProjectReference`, and register it in `csharp2md.slnx` under the `src` folder.
**Where**: `src/Csharp2Md.Domain/Csharp2Md.Domain.csproj`, `csharp2md.slnx`
**Depends on**: None
**Reuses**: `src/Csharp2Md.Core/Csharp2Md.Core.csproj` project shape; `Directory.Build.props:20` supplies
`Microsoft.SourceLink.GitHub` and `Directory.Build.props:25` the global `System.Collections.Immutable` using
**Requirement**: TAX-01, TAX-02, TAX-05, TAX-06

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:project-structure`, `dotnet-msbuild:directory-build-organization`

**Done when**:

- [x] `TargetFramework` is `net10.0` and the project declares no package or project reference of its own
- [x] The project appears in `csharp2md.slnx` under `/src/` and legacy entries are untouched
- [x] The project holds no reference to `Csharp2Md.Core` and `Csharp2Md.Core.csproj` is unmodified
- [x] A placeholder-free build: the project contains at least one real type so the assembly is not empty
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: none
**Gate**: build

**Commit**: `feat(domain): add the dependency-free taxonomy assembly`

---

#### T2: Create the domain test project and the assembly-isolation invariant

**What**: Add `tests/Csharp2Md.Domain.Tests` (xUnit + Verify package set, registered in `csharp2md.slnx` under
`tests`), a `DomainTestPaths` repo-root helper, the `[Trait("Requirement", "TAX-nn")]` convention every test in
this feature carries, and the isolation tests asserting the domain's referenced-assembly set and its
public-plus-internal surface expose no `Microsoft.CodeAnalysis`, `Microsoft.Build`, `System.Text.Json` or
`System.IO` type, and that neither assembly references the other.
**Where**: `tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`,
`tests/Csharp2Md.Domain.Tests/DomainTestPaths.cs`,
`tests/Csharp2Md.Domain.Tests/Isolation/DomainIsolationTests.cs`, `csharp2md.slnx`
**Depends on**: T1
**Reuses**: `tests/Csharp2Md.Core.Tests/Csharp2Md.Core.Tests.csproj` package set and `Using Include="Xunit"`;
`tests/Csharp2Md.Core.Tests/TestPaths.cs:10` repo-root walk
**Requirement**: TAX-03, TAX-04, TAX-06

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:scaffold-dotnet-test-project`, `dotnet-skills:project-structure`

**Done when**:

- [x] The test project references `Csharp2Md.Domain` only - not `Csharp2Md.Core`
- [x] Each forbidden namespace is asserted by name in its own case, and the failure message names the offending type
- [x] The reference direction is asserted in both directions (domain to core, core to domain)
- [x] `DomainTestPaths.RepoRoot` resolves by walking to `csharp2md.slnx`
- [x] The requirement-trait convention is documented in the test project and used by these tests
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `test(domain): pin the taxonomy assembly's isolation`

---

### Phase 2: Identity grammar

#### T3: Port the fact identity grammar

**What**: Port `FactId` and `FactIdGrammar` into the domain, adding the explicit parameter name to `Encode`'s
rejection and the `-` sentinel for an absent optional component, so a missing required component fails naming
itself instead of aborting anonymously.
**Where**: `src/Csharp2Md.Domain/Identity/FactId.cs`
**Depends on**: T2
**Reuses**: `src/Csharp2Md.Core/Facts/Identity/FactId.cs:22` (`FactIdGrammar.Create`,
`ValidateRelativePath`, `RequireCanonicalText`, percent-encoding) - copied, not referenced, because the type is
`internal` and TAX-06 forbids the reference
**Requirement**: TAX-68, TAX-69, TAX-76

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] An absolute path, a rooted path, a backslash path and a dot-segment path are each rejected
- [x] A source location, a label and a timestamp cannot enter an identity: the grammar accepts only declared key-value components
- [x] A whitespace-only or non-canonical component is rejected with its parameter name in the exception
- [x] An absent optional component encodes as the `-` sentinel rather than throwing
- [x] Percent-encoding is uppercase-hex and byte-for-byte identical to the legacy grammar for the same input
- [x] Reading an uninitialized `FactId` throws `InvalidOperationException`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(identity): port the fact identity grammar into the domain`

---

#### T4: Port the canonical symbol signature

**What**: Port `CanonicalSymbolSignature` and its parameter-modifier types as the `Symbol` identity component,
keeping the `sig1` prefix rewrite and the `-` sentinel for absent parameter and type-argument lists.
**Where**: `src/Csharp2Md.Domain/Identity/CanonicalSymbolSignature.cs`
**Depends on**: T3
**Reuses**: `src/Csharp2Md.Core/Facts/Identity/CanonicalSymbolSignature.cs:13` - copied
**Requirement**: TAX-69, TAX-76

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Kind, container, metadata name, arity, type, parameters and type arguments all participate; parameter names do not
- [x] Absent parameter and type-argument lists render as `-`
- [x] A negative arity is rejected with `ArgumentOutOfRangeException`
- [x] Two signatures differing only in a preceding unrelated edit are equal, and no `line` or `span` component appears
- [x] Reading an uninitialized signature throws `InvalidOperationException`
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(identity): port the canonical symbol signature`

---

#### T5: Add the fact reference and the identity ledger

**What**: Add `FactReference` - the `readonly record struct (FactId Id, string FactType)` that lets a triple be
checked without materializing a fact - and `IdentityLedger`, which registers references by identity string and
throws naming both fact types when two distinct facts collide.
**Where**: `src/Csharp2Md.Domain/Identity/FactReference.cs`,
`src/Csharp2Md.Domain/Identity/IdentityLedger.cs`
**Depends on**: T4
**Reuses**: `src/Csharp2Md.Domain/Identity/FactId.cs` (`RequireCanonicalText` for the fact-type token)
**Requirement**: TAX-77

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] A reference carries the identity and the fact type, and a non-canonical fact type is rejected
- [x] Registering the same reference twice is idempotent and does not throw
- [x] Registering two distinct fact types on one identity string throws naming both
- [x] Comparison is ordinal throughout - two identities differing only in case are distinct
- [x] `Count` reflects distinct registered identities
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(identity): detect identity collisions across facts`

---

### Phase 3: Scoped identities and determinism

#### T6: Add the workspace and solution identities

**What**: Add `WorkspaceIdentity.Create(logicalName)`, which scopes every globally composable identity, and
`SolutionId.Create(workspace, logicalRelativePath)`, derived from a logical relative path only.
**Where**: `src/Csharp2Md.Domain/Identity/WorkspaceIdentity.cs`,
`src/Csharp2Md.Domain/Identity/SolutionId.cs`
**Depends on**: T5
**Reuses**: `src/Csharp2Md.Domain/Identity/FactId.cs` grammar and `ValidateRelativePath`
**Requirement**: TAX-72, TAX-73

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] A solution identity built under two different absolute roots from the same logical relative path is byte-identical
- [x] The workspace component appears in the solution identity, and two workspaces produce distinct solution identities
- [x] An absolute or non-normalized solution path is rejected naming the parameter
- [x] A missing workspace name is rejected naming the parameter, with no partial identity produced
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 52 total, 0 failed

**Tests**: unit
**Gate**: quick

**Commit**: `feat(identity): scope solution identity by workspace`

---

#### T7: Add the project identity and its logical key

**What**: Add `LogicalKey` and `ProjectId.Create(solution, logicalRelativePath, logicalKey)`, where the default
identity derives from the logical relative path and an explicit key overrides it so the identity survives a
path change.
**Where**: `src/Csharp2Md.Domain/Identity/ProjectId.cs`
**Depends on**: T6
**Reuses**: `src/Csharp2Md.Domain/Identity/SolutionId.cs` nesting pattern
**Requirement**: TAX-74, TAX-75

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-nullable-reference-types`

**Done when**:

- [x] Without a logical key, moving the project to a different logical relative path yields a different identity
- [x] With a logical key, the same move yields the identical identity
- [x] The logical key is validated as canonical text and rejected otherwise, naming the parameter
- [x] The owning solution participates in the identity: the same relative path under two solutions is distinct
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 59 total, 0 failed

**Tests**: unit
**Gate**: quick

**Commit**: `feat(identity): preserve project identity through a logical key`

---

#### T8: Add the analysis-variant identity

**What**: Add `AnalysisVariantId.Create(targetFramework, configuration, symbols, environment)` with ordinal
sorting and de-duplication of preprocessor symbols so variant identity is order-independent.
**Where**: `src/Csharp2Md.Domain/Identity/AnalysisVariantId.cs`
**Depends on**: T7
**Reuses**: `src/Csharp2Md.Domain/Identity/FactId.cs` grammar; the ordinal-sort-and-deduplicate shape at
`tests/Csharp2Md.Core.Tests/Facts/Identity/FactIdentityTests.cs:132`
**Requirement**: TAX-78

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [x] All four components participate: changing any one changes the identity
- [x] Symbols supplied in two different orders, and with a duplicate, produce one identity
- [x] A non-canonical component is rejected naming the parameter
- [x] No absolute path, location or timestamp can enter the variant identity
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 70 total, 0 failed

**Tests**: unit
**Gate**: quick

**Commit**: `feat(identity): add the analysis-variant identity`

---

#### T9: Prove identity determinism across clone path and input order

**What**: Author the cross-type determinism suite: derive every identity twice from synthetic inputs under a
simulated clone-path change and a shuffled input order and assert byte equality, then assert that a missing
required component and a collision each fail with the expected names. This is the spec's Independent Test for
the identity story and spans four types authored in T4 through T8, so it cannot live inside any one of them.
**Where**: `tests/Csharp2Md.Domain.Tests/Identity/IdentityDeterminismTests.cs`
**Depends on**: T8
**Reuses**: `tests/Csharp2Md.Core.Tests/Facts/Identity/FactIdentityTests.cs:41` clone-path shape and `:132`
order-independence shape
**Requirement**: TAX-70, TAX-71, TAX-76

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [x] Two simulated clone paths produce byte-identical solution, project, variant and symbol identities
- [x] Two input orders produce byte-identical identities for every collection-bearing component
- [x] Every required component omitted in turn fails naming that component, with no partial identity emitted
- [x] A forced collision fails naming both participating fact types
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [x] Test count recorded (no silent deletions) — Csharp2Md.Domain.Tests: 80 total, 0 failed; Csharp2Md.Core.Tests: 1579 total, 1578 passed, 1 pre-existing unrelated failure (`MigrationLedgerTests.BaselineCategory_StillHasARepresentativeV3Test`)

**Tests**: unit
**Gate**: build

**Commit**: `test(identity): prove determinism across clone path and order`

---

### Phase 4: Closed vocabularies

#### T10: Add the boundary-operation axes

**What**: Add `BoundaryProtocol`, `BoundaryDirection` and `BoundaryRole` as three independent enums with
exactly the documented members, and no composite kind combining them.
**Where**: `src/Csharp2Md.Domain/Facets/BoundaryAxes.cs`
**Depends on**: T9
**Reuses**: nothing
**Requirement**: TAX-18, TAX-19, TAX-20, TAX-21

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Set equality against `http`, `grpc`, `messaging`, `cli`, `scheduler`, `function`
- [x] Set equality against `inbound`, `outbound`
- [x] Set equality against `command`, `query`, `event`, `stream`, `lifecycle`
- [x] A reflection assertion proves no type in `Facets` combines protocol, direction and role into one enum
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 84 passed (80 baseline + 4 new)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facets): add the boundary-operation axes`

---

#### T11: Add the persistence axes and the mapping states

**What**: Add `DataStoreTechnology`, `DataObjectForm`, `DataOperationKind` and `MappingStateKind`, the last
closing the CLR-to-physical mapping to explicit confirmation, conventional candidate and unresolved.
**Where**: `src/Csharp2Md.Domain/Facets/PersistenceAxes.cs`
**Depends on**: T10
**Reuses**: nothing
**Requirement**: TAX-22, TAX-23, TAX-24, TAX-31

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [x] Set equality against `relational`, `document`, `key-value`, `cache`, `unknown`
- [x] Set equality against `table`, `view`, `collection`, `key-space`, `cache-region`, `unknown`
- [x] Set equality against `read`, `insert`, `update`, `delete`, `execute`, `unknown`
- [x] Set equality against explicit confirmation, conventional candidate, unresolved - and nothing else
- [x] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [x] Test count recorded (no silent deletions) — 88 passed (84 baseline + 4 new)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facets): add the persistence axes and mapping states`

---

#### T12: Add symbol facets as values rather than types

**What**: Add `SymbolFacet` carrying `callable`, `controller`, `handler`, `repository`, `client` and `service`
as facet values, plus `SymbolFacetSet.Create` producing a normalized, distinct, ordered set.
**Where**: `src/Csharp2Md.Domain/Facets/SymbolFacets.cs`
**Depends on**: T11
**Reuses**: the ordinal-sort-and-deduplicate shape from T8
**Requirement**: TAX-13, TAX-14

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] `callable` is a facet value and no `Callable` fact type exists anywhere in the domain
- [ ] `controller`, `handler`, `repository`, `client` and `service` are facet values, asserted by name
- [ ] The set is not a `[Flags]` enum: two orders and a duplicate produce one equal, ordered set
- [ ] An undefined facet reached by cast is rejected naming the axis and the value
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facets): model symbol roles as facet values`

---

#### T13: Add the three proof-state axes

**What**: Add `EvidenceMethod`, `Resolution` and `Frontier` as three independent enums, replacing the legacy
combined `FactResolution`.
**Where**: `src/Csharp2Md.Domain/Proof/ProofAxes.cs`
**Depends on**: T12
**Reuses**: nothing. Explicitly rejects `src/Csharp2Md.Core/Facts/Model/FactResolution.cs:3`, which mixes
resolution, evidence method and a confidence gradient in one enum
**Requirement**: TAX-55, TAX-56, TAX-57, TAX-58

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Set equality against `semantic`, `syntactic`, `configured`
- [ ] Set equality against `confirmed`, `candidate`, `unresolved`
- [ ] Set equality against `closed`, `open`
- [ ] No member of any of the three names a value from another axis, and no combined enum exists
- [ ] No `Stronger` or `Rank` precedence helper is ported
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(proof): add the three independent proof-state axes`

---

#### T14: Add the observation kinds and emission tiers

**What**: Add `ObservationKind` with exactly the ten documented kinds and `EmissionTier` with
`AlwaysWhenBindable` and `RegisteredContextOnly`.
**Where**: `src/Csharp2Md.Domain/Observations/ObservationKind.cs`
**Depends on**: T13
**Reuses**: nothing
**Requirement**: TAX-32

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Set equality against `Invocation`, `ObjectCreation`, `TypeUsage`, `BaseType`, `AttributeUsage`, `Assignment`, `Configuration`, `RouteDeclaration`, `MessageOperation`, `DataAccess`
- [ ] `EmissionTier` has exactly two members
- [ ] No kind names a Roslyn syntax or symbol concept
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(observations): add the observation kinds and emission tiers`

---

#### T15: Add the twelve canonical relation kinds

**What**: Add `RelationKind` with exactly the twelve documented members, each in one canonical direction, and
no generic or inverse edge.
**Where**: `src/Csharp2Md.Domain/Relations/RelationKind.cs`
**Depends on**: T14
**Reuses**: nothing
**Requirement**: TAX-42, TAX-43, TAX-47

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Set equality against `contains`, `belongs-to`, `included-in`, `executes`, `invokes`, `implements-operation`, `targets`, `uses-contract`, `accesses-data`, `operates-on`, `maps-to`, `configured-by`
- [ ] No `references` and no `dependsOn` member exists, asserted by name
- [ ] No member is the inverse of another, asserted against an explicit list of forbidden inverse names
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): add the twelve canonical relation kinds`

---

#### T16: Add the CLR-to-wire pairing and out-of-vocabulary rejection

**What**: Add `FacetAxes` holding the explicit CLR-member-to-wire-value pairing for every axis authored in T10
through T15, plus `WireValue<TEnum>` which validates with `Enum.IsDefined` and throws naming the axis and the
rejected value. The pairing owns the axis name and value list so the registry in Phase 5 can be declared
against it without a circular dependency.
**Where**: `src/Csharp2Md.Domain/Facets/FacetAxes.cs`
**Depends on**: T15
**Reuses**: the exhaustive-switch-with-explicit-throw shape at
`src/Csharp2Md.Core/Facts/Model/FactResolution.cs:31`
**Requirement**: TAX-25, TAX-26

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] The pairing is total and injective in both directions for every axis, asserted by reflection over the enum members rather than by a hand-listed set
- [ ] A wire value is never derived from a CLR member name: a rename would fail the pairing test
- [ ] An undefined value reached by cast is rejected with the axis name and the rejected value in the message
- [ ] `unknown` resolves to a registered wire value on all three persistence axes and is not treated as absent
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `feat(facets): pair every axis to its wire value explicitly`

---

### Phase 5: Registry tables and enforcement

#### T17: Declare the fact families and fact types

**What**: Add `FactFamily`, `FactTypeDescriptor` and the ordered `FactTypes` table declaring the five families
and their seventeen types with each type's identity components, inside a `TaxonomyTables` value that can be
copied and extended by a test.
**Where**: `src/Csharp2Md.Domain/Registry/FactTypeDescriptor.cs`,
`src/Csharp2Md.Domain/Registry/TaxonomyTables.cs`
**Depends on**: T16
**Reuses**: `ImmutableArray<T>` via the global using at `Directory.Build.props:25`
**Requirement**: TAX-07, TAX-08, TAX-09, TAX-10, TAX-11, TAX-12, TAX-15

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Exactly five families, asserted by set equality
- [ ] Each family's type set asserted by set equality: 4 structural, 5 architecture, 3 contract, 4 persistence, 1 configuration
- [ ] No fact type named for a facet-only role (`Controller`, `Handler`, `Repository`, `Client`, `Service`, `Callable`)
- [ ] No fact type whose payload is a functional explanation - no `BusinessRule` or equivalent, asserted by name
- [ ] Declared order is stable and preserved by `ImmutableArray`, never by a frozen collection
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(registry): declare the five fact families and their types`

---

#### T18: Declare the relation matrix and its minimum evidence methods

**What**: Add `RelationTriple`, `RelationDescriptor` and the ordered `Relations` table declaring, for each of
the twelve relations, its registered source-target triples and its minimum accepted evidence method. Table
initialization derives the frozen lookup and throws naming any triple declared twice.
**Where**: `src/Csharp2Md.Domain/Registry/RelationDescriptor.cs`
**Depends on**: T17
**Reuses**: `docs/architecture/taxonomy.md:90` valid semantic shapes; `src/Csharp2Md.Domain/Registry/TaxonomyTables.cs`
**Requirement**: TAX-44, TAX-52, TAX-90

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Every relation declares at least one triple, and `contains` is restricted to structural owner and structural child
- [ ] Every relation declares a minimum accepted evidence method
- [ ] A duplicate triple declaration throws at initialization naming the relation and both fact types
- [ ] Every triple's endpoints are names present in the fact-type table, asserted by set containment
- [ ] The frozen lookup is derived from the ordered array and the ordered array is what any consumer enumerates
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(registry): declare the relation matrix and evidence minimums`

---

#### T19: Add triple and evidence enforcement

**What**: Add `TaxonomyRegistry.IsRegisteredTriple`, `RequireRegisteredTriple` (throwing naming source,
relation and target), `MinimumEvidenceMethod` and `RequireSufficientEvidence` (throwing naming the relation,
the required method and the supplied one).
**Where**: `src/Csharp2Md.Domain/Registry/TaxonomyRegistry.cs`
**Depends on**: T18
**Reuses**: `src/Csharp2Md.Domain/Registry/RelationDescriptor.cs` frozen lookup
**Requirement**: TAX-45, TAX-53, TAX-54

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] A registered triple is accepted and an unregistered one throws with all three names in the message
- [ ] Syntactic evidence supplied where the registry requires semantic evidence is rejected naming both methods
- [ ] Configured evidence is accepted or rejected per the declared minimum, not by an assumed ordering
- [ ] No name-similarity, prefix-similarity or path-similarity evidence method exists, asserted by name over `EvidenceMethod`
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(registry): enforce registered triples and evidence minimums`

---

#### T20: Declare the mapping roles and contract payload roles

**What**: Add the closed `maps-to` mapping-role table (`contract-implementation`, `data-object-mapping`,
`data-field-mapping`, `serialization-binding`) and the closed payload-role table a `uses-contract` relation
must declare against.
**Where**: `src/Csharp2Md.Domain/Registry/MappingRoles.cs`
**Depends on**: T19
**Reuses**: `src/Csharp2Md.Domain/Registry/TaxonomyTables.cs`
**Requirement**: TAX-48

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:

- [ ] Set equality against exactly the four mapping roles
- [ ] An out-of-vocabulary mapping role is rejected naming the axis and the value
- [ ] The payload-role vocabulary is closed and asserted by set equality
- [ ] Both tables are ordered arrays, and both are reachable from `TaxonomyTables`
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(registry): close the mapping and payload role vocabularies`

---

#### T21: Declare the observation kinds and their emission tiers

**What**: Add `ObservationKindDescriptor` and the ordered table pairing each of the ten kinds to its wire name
and emission tier.
**Where**: `src/Csharp2Md.Domain/Registry/ObservationKindDescriptor.cs`
**Depends on**: T20
**Reuses**: `src/Csharp2Md.Domain/Observations/ObservationKind.cs`;
`src/Csharp2Md.Domain/Facets/FacetAxes.cs` pairing pattern
**Requirement**: TAX-33, TAX-34

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] `Invocation`, `ObjectCreation`, `TypeUsage`, `BaseType`, `AttributeUsage` are each `AlwaysWhenBindable`
- [ ] `Assignment`, `Configuration`, `RouteDeclaration`, `MessageOperation`, `DataAccess` are each `RegisteredContextOnly`
- [ ] The table covers every enum member exactly once, asserted by reflection over `ObservationKind`
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(registry): declare observation kinds and emission tiers`

---

#### T22: Declare the five version axes and the additive-change guarantee

**What**: Add `TaxonomyVersions` exposing `schema_version`, `taxonomy_version`, `observation_schema_version`,
`extractor_set_version` and `classifier_set_version` as five independent monotonic integers starting at 1, and
project the facet and proof axes into the registry's `FacetAxes` and `ProofAxes` descriptor arrays so the full
enumeration TAX-84 requires is reachable from one place.
**Where**: `src/Csharp2Md.Domain/Registry/TaxonomyVersions.cs`,
`src/Csharp2Md.Domain/Registry/FacetAxisDescriptor.cs`
**Depends on**: T21
**Reuses**: `src/Csharp2Md.Domain/Facets/FacetAxes.cs` pairing tables as the value source
**Requirement**: TAX-83, TAX-84, TAX-88, TAX-89

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] Five axes exist as separate members, all at 1, and no axis is derived from another
- [ ] The registry enumerates every family, fact type, observation kind, facet axis with its values, relation with its triples and minimum evidence method, mapping role, proof axis and version axis
- [ ] Extending a copied `TaxonomyTables` with an additional axis value or fact type keeps every previously valid identity and every previously registered triple valid
- [ ] Removing a value from a closed axis, or renaming one, is detected as a non-additive change rather than silently accepted
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `feat(registry): declare the five version axes and axis tables`

---

### Phase 6: Literals and evidence primitives

#### T23: Add the document and span primitives

**What**: Add `DocumentId`, `SourceSpan` and `DocumentHash` - the location and integrity primitives evidence is
built from, none of which may enter an identity.
**Where**: `src/Csharp2Md.Domain/Literals/DocumentPrimitives.cs`
**Depends on**: T22
**Reuses**: `src/Csharp2Md.Core/Facts/Metadata/Evidence.cs:33` start-must-not-follow-end validation
**Requirement**: TAX-38, TAX-81

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] A span whose end precedes its start is rejected naming the parameter
- [ ] A negative line or column is rejected
- [ ] A document hash must be a lowercase hex digest of fixed length; anything else is rejected
- [ ] None of the three exposes an absolute path
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(literals): add the document and span primitives`

---

#### T24: Add the evidence locator with deterministic ordering

**What**: Add `EvidenceLocator` carrying a document identity, a relative path and a span, with the ordinal
document-then-path-then-position comparison that makes evidence sort deterministically.
**Where**: `src/Csharp2Md.Domain/Literals/EvidenceLocator.cs`
**Depends on**: T23
**Reuses**: `src/Csharp2Md.Core/Facts/Metadata/Evidence.cs:46` comparison shape
**Requirement**: TAX-38

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Comparison is ordinal and total: document, then relative path, then start, then end
- [ ] Sorting the same locators supplied in two orders yields the same sequence
- [ ] An absolute or non-normalized relative path is rejected naming the parameter
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(literals): order evidence locators deterministically`

---

#### T25: Add the structural literal allowlist

**What**: Add `LiteralRole` with exactly the eight allowlisted roles and `StructuralLiteral.Create(role, value)`
as the only way a literal reaches a fact or observation payload, rejecting an out-of-allowlist literal naming
the field.
**Where**: `src/Csharp2Md.Domain/Literals/StructuralLiteral.cs`
**Depends on**: T24
**Reuses**: `docs/architecture/quality-and-security.md:68` allowlist;
`src/Csharp2Md.Domain/Identity/FactId.cs` `RequireCanonicalText`
**Requirement**: TAX-79, TAX-80

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] Set equality against route, protocol name, channel, schema name, table name, field name, configuration key, client name
- [ ] One literal per role is accepted, and an undefined role reached by cast is rejected naming the axis
- [ ] A literal supplied without a role, or with a role the field does not permit, is rejected naming the field
- [ ] No `string` payload field on any fact or observation bypasses this type, asserted by reflection over the payload-carrying surface
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(literals): bound payload literals to the structural allowlist`

---

#### T26: Add suspected-secret evidence without the secret

**What**: Add `RedactedExcerpt` and `SuspectedSecretEvidence.Create(document, span, hash, excerpt)`, exposing no
member that carries an individual secret value or a hash of one.
**Where**: `src/Csharp2Md.Domain/Literals/SuspectedSecretEvidence.cs`
**Depends on**: T25
**Reuses**: `src/Csharp2Md.Domain/Literals/DocumentPrimitives.cs`
**Requirement**: TAX-81, TAX-82

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] The evidence carries exactly document identity, span, document hash and redacted excerpt
- [ ] A reflection assertion proves no member is an original literal or a per-secret hash, checked by member name and by type
- [ ] The document hash is the whole-document hash, not a hash of the span content
- [ ] An unredacted excerpt - one containing the candidate secret verbatim - is rejected
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(literals): model suspected-secret evidence without the secret`

---

#### T27: Add the observation payload and extraction metadata

**What**: Add `NormalizedPayload` as an ordered array of canonical key-value pairs, plus `BindingDiagnostic`
and `ExtractorVersion`.
**Where**: `src/Csharp2Md.Domain/Observations/NormalizedPayload.cs`,
`src/Csharp2Md.Domain/Observations/ExtractionMetadata.cs`
**Depends on**: T26
**Reuses**: `src/Csharp2Md.Domain/Literals/StructuralLiteral.cs` for payload values;
`src/Csharp2Md.Domain/Identity/FactId.cs` canonical-text guard
**Requirement**: TAX-35, TAX-38

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Payload pairs are canonicalized and ordered so two shuffled inputs produce one equal payload
- [ ] A payload value that is not a `StructuralLiteral` cannot be supplied
- [ ] A duplicate key is rejected naming the key
- [ ] `ExtractorVersion` is a monotonic integer and rejects a non-positive value
- [ ] `BindingDiagnostic` names no Roslyn type
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `feat(observations): add the normalized payload and extraction metadata`

---

### Phase 7: Observations and promotion audit trail

#### T28: Add the observation identity

**What**: Add `ObservationIdentity` derived from owner, kind, normalized payload and structural occurrence
ordinal only - never from the evidence locator.
**Where**: `src/Csharp2Md.Domain/Observations/ObservationIdentity.cs`
**Depends on**: T27
**Reuses**: `src/Csharp2Md.Domain/Identity/FactId.cs` grammar
**Requirement**: TAX-35, TAX-36, TAX-37

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Two observations differing only by evidence locator report one identity
- [ ] Two observations differing only by occurrence ordinal report distinct identities
- [ ] A non-positive ordinal is rejected
- [ ] No location, label or timestamp component appears in the identity string
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(observations): derive identity from payload and ordinal only`

---

#### T29: Add the immutable observation record

**What**: Add `Observation.Create` requiring owner, kind, normalized payload, evidence locator, extraction
method, binding diagnostic, document hash and extractor version, rejecting each omission by name, with no
mutating member and no Roslyn type on the contract.
**Where**: `src/Csharp2Md.Domain/Observations/Observation.cs`
**Depends on**: T28
**Reuses**: `src/Csharp2Md.Domain/Observations/ObservationIdentity.cs`,
`src/Csharp2Md.Domain/Literals/EvidenceLocator.cs`
**Requirement**: TAX-38, TAX-39, TAX-40, TAX-41

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`, `dotnet-skills:csharp-nullable-reference-types`

**Done when**:

- [ ] All eight required components present; each omitted in turn is rejected naming the missing component
- [ ] A missing document hash and a missing extractor version are each rejected by name specifically
- [ ] Reflection proves the observation surface exposes no `Microsoft.CodeAnalysis` type
- [ ] Reflection proves no settable property, no `with`-bypassing mutator and no mutable collection member
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(observations): require complete immutable observation evidence`

---

#### T30: Add the evidence chain

**What**: Add `EvidenceChain.Create(derivedFrom)` over an ordered array of observation identities, rejecting an
empty chain.
**Where**: `src/Csharp2Md.Domain/Proof/EvidenceChain.cs`
**Depends on**: T29
**Reuses**: `src/Csharp2Md.Domain/Observations/ObservationIdentity.cs`
**Requirement**: TAX-60

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] An empty chain is rejected naming the parameter
- [ ] Ordering is deterministic and duplicate identities collapse to one entry
- [ ] Two chains built from shuffled inputs are equal
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(proof): require a non-empty evidence chain`

---

#### T31: Add the classifier identity

**What**: Add `ClassifierIdentity` as a `readonly record struct (string Id, int Version)` with a canonical
reverse-DNS identifier and a positive version.
**Where**: `src/Csharp2Md.Domain/Proof/ClassifierIdentity.cs`
**Depends on**: T30
**Reuses**: the reverse-DNS validation shape at
`tests/Csharp2Md.Core.Tests/Facts/Identity/FactIdentityTests.cs:160`
**Requirement**: TAX-60

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] A non-lowercase, non-reverse-DNS or dotless identifier is rejected
- [ ] A non-positive version is rejected
- [ ] Reading an uninitialized value throws `InvalidOperationException`
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(proof): add the versioned classifier identity`

---

#### T32: Add the rejected candidate with its cause

**What**: Add `RejectedCandidate` requiring a rejection cause, so a promotion cannot record a rejection without
saying why.
**Where**: `src/Csharp2Md.Domain/Proof/RejectedCandidate.cs`
**Depends on**: T31
**Reuses**: `src/Csharp2Md.Domain/Proof/EvidenceChain.cs`
**Requirement**: TAX-66

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] Construction without a cause is rejected naming the parameter
- [ ] The cause is a closed vocabulary value, not free text, and an undefined value is rejected
- [ ] The candidate carries the evidence that was available when it was rejected
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(proof): require a cause on every rejected candidate`

---

#### T33: Add the promotion record

**What**: Add `PromotionRecord.Create` declaring required observations, accepted evidence methods, negative
conditions, produced facts, produced relations, produced facets, rejected candidates, classifier identifier and
classifier version.
**Where**: `src/Csharp2Md.Domain/Proof/PromotionRecord.cs`
**Depends on**: T32
**Reuses**: `src/Csharp2Md.Domain/Proof/RejectedCandidate.cs`,
`src/Csharp2Md.Domain/Proof/ClassifierIdentity.cs`
**Requirement**: TAX-67

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] All nine declared components are required; each omitted in turn is rejected naming it
- [ ] Every accepted evidence method is a registered `EvidenceMethod`
- [ ] Every produced facet is a registered axis value
- [ ] An empty required-observation set is rejected: a promotion with no evidence requirement cannot exist
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `feat(proof): declare the full promotion audit trail`

---

### Phase 8: Fact records

#### T34: Add the structural facts

**What**: Add `IFact` and the structural family - `Solution`, `Project`, `Document`, `Symbol` - as sealed
immutable records with validating `Create` factories, `Symbol` carrying its facet set rather than a role type.
**Where**: `src/Csharp2Md.Domain/Facts/Structural/StructuralFacts.cs`
**Depends on**: T33
**Reuses**: `src/Csharp2Md.Domain/Identity/SolutionId.cs`, `ProjectId.cs`, `CanonicalSymbolSignature.cs`,
`src/Csharp2Md.Domain/Facets/SymbolFacets.cs`
**Requirement**: TAX-08, TAX-14

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Exactly four types in the family, each exposing `Reference` and `Family`
- [ ] Every identity component is validated at construction; each omission rejected naming the component
- [ ] `Symbol` carries `SymbolFacetSet` and there is no `Callable` type
- [ ] Each type's `Reference.FactType` matches its registry descriptor name exactly
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): add the structural fact family`

---

#### T35: Add the component, deployment unit and external system

**What**: Add `Component`, `DeploymentUnit` and `ExternalSystem` as sealed immutable records with validating
factories and order-independent owner sets.
**Where**: `src/Csharp2Md.Domain/Facts/Architecture/ComponentFacts.cs`
**Depends on**: T34
**Reuses**: `src/Csharp2Md.Domain/Facts/Structural/StructuralFacts.cs` shape; the owner-sorting shape at
`tests/Csharp2Md.Core.Tests/Facts/Identity/FactIdentityTests.cs:132`
**Requirement**: TAX-09

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Component owners supplied in two orders, with a duplicate, produce one identity
- [ ] Each type's identity components are validated and each omission rejected by name
- [ ] An external system is identified without any absolute address or credential-bearing component
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): add the component and deployment facts`

---

#### T36: Add the entry point and boundary operation

**What**: Add `EntryPoint` and `BoundaryOperation` as distinct records with no conversion between them, deriving
outbound HTTP identity from owning component, direction, protocol, destination scope, HTTP method and
normalized route, and inbound identity from owning component and protocol operation key.
**Where**: `src/Csharp2Md.Domain/Facts/Architecture/BoundaryFacts.cs`
**Depends on**: T35
**Reuses**: `src/Csharp2Md.Domain/Facets/BoundaryAxes.cs`,
`src/Csharp2Md.Domain/Literals/StructuralLiteral.cs` for the route literal
**Requirement**: TAX-27, TAX-28, TAX-29, TAX-30

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] Neither type exposes a conversion, cast or factory that derives it from the other
- [ ] One `Symbol` reference participates in an entry-point fact and a boundary-operation fact at the same time
- [ ] Outbound HTTP identity changes when any of its six components changes, and is stable otherwise
- [ ] Inbound identity uses exactly owning component and protocol operation key
- [ ] The route enters identity only as a normalized structural literal - no raw absolute URL
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): separate entry points from boundary operations`

---

#### T37: Add the contract facts

**What**: Add `Contract`, `ContractBinding` and `ContractRevision`, where a contract requires a proven protocol
or schema key and a CLR type alone cannot constitute one, and structural or name similarity cannot merge
contracts across owners.
**Where**: `src/Csharp2Md.Domain/Facts/Contracts/ContractFacts.cs`
**Depends on**: T36
**Reuses**: `src/Csharp2Md.Domain/Registry/MappingRoles.cs` payload roles;
`src/Csharp2Md.Domain/Facts/Architecture/BoundaryFacts.cs`
**Requirement**: TAX-10, TAX-16, TAX-17

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] Exactly three types in the family
- [ ] Constructing a contract from a CLR type with no protocol or schema key is rejected naming the missing proof
- [ ] Two owners with structurally identical or similarly named payloads and no shared key produce distinct contract identities
- [ ] Two owners with the same proven protocol or schema key produce one contract identity
- [ ] A binding requires an operation, a payload role, a CLR symbol and a contract; each omission rejected by name
- [ ] A revision carries a structural fingerprint that is not a hash of any literal value
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): require proven identity for contract facts`

---

#### T38: Add the persistence facts

**What**: Add `DataStore`, `DataObject`, `DataField` and `DataOperation`, each carrying its closed facet axis and
its mapping state.
**Where**: `src/Csharp2Md.Domain/Facts/Persistence/PersistenceFacts.cs`
**Depends on**: T37
**Reuses**: `src/Csharp2Md.Domain/Facets/PersistenceAxes.cs`;
`src/Csharp2Md.Domain/Literals/StructuralLiteral.cs` for schema, table and field names
**Requirement**: TAX-11

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Exactly four types in the family
- [ ] `technology`, `form` and `operation` accept only registered axis values, and `unknown` is accepted as registered
- [ ] Table, schema and field names enter only as structural literals; a connection string cannot be supplied
- [ ] Each type's mapping state is one of the three closed states
- [ ] Case-differing names produce distinct identities under ordinal comparison
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): add the persistence fact family`

---

#### T39: Add the configuration binding

**What**: Add `ConfigurationBinding` as the single configuration-family type, proving configuration structure
without carrying a sensitive value.
**Where**: `src/Csharp2Md.Domain/Facts/Configuration/ConfigurationBinding.cs`
**Depends on**: T38
**Reuses**: `src/Csharp2Md.Domain/Literals/StructuralLiteral.cs` configuration-key role
**Requirement**: TAX-12

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] Exactly one type in the family
- [ ] The configuration key enters only as a structural literal
- [ ] No member carries a configuration value, a connection string or a token, asserted by reflection
- [ ] The bound fact reference is required and validated
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(facts): add the configuration binding fact`

---

#### T40: Close the family sets against the registry

**What**: Author the family-closure suite: assert set equality between each family's CLR record set and its
registry descriptor set in both directions, that the mapping is bijective, that no fact type names a facet-only
role, and that no fact type carries a functional explanation. This is the spec's Independent Test for the
fact-family story and the test AD-013's trade-off names.
**Where**: `tests/Csharp2Md.Domain.Tests/Facts/FactFamilyClosureTests.cs`
**Depends on**: T39
**Reuses**: `tests/Csharp2Md.Core.Tests/Topic/FrontmatterSchemaSyncTests.cs:33` symmetric-difference assertion
shape
**Requirement**: TAX-07, TAX-13, TAX-15

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns`

**Done when**:

- [ ] Exactly five families and exactly seventeen types, asserted by reflection over the assembly, not by a hand-written list of the same names
- [ ] Symmetric difference between registry descriptors and CLR records is empty in both directions, and the failure message names the offenders
- [ ] No fact type name matches a facet-only role or a business-rule concept
- [ ] Every `IFact` implementation is reachable from exactly one family
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `test(facts): close the family sets against the registry`

---

### Phase 9: Relations and proof invariants

#### T41: Add the relation facet binding

**What**: Add `FacetBinding`, the validated set of registered facet values a relation carries, including the
`maps-to` mapping role and the `uses-contract` payload role.
**Where**: `src/Csharp2Md.Domain/Relations/FacetBinding.cs`
**Depends on**: T40
**Reuses**: `src/Csharp2Md.Domain/Facets/FacetAxes.cs`, `src/Csharp2Md.Domain/Registry/MappingRoles.cs`
**Requirement**: TAX-60

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Every bound value is validated against its registered axis; an unregistered value is rejected naming axis and value
- [ ] A binding is ordered and order-independent: two shuffled inputs are equal
- [ ] A facet axis irrelevant to a relation cannot be bound to it
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): validate relation facet bindings`

---

#### T42: Add the confirmed relation

**What**: Add `ConfirmedRelation.Create` validating the source-relation-target triple against the registry and
requiring a typed source, a typed target, registered facets, an evidence chain, a classifier identity, a
classifier version and at least one analysis variant, with `Resolution` as a computed property fixed at
`Confirmed`.
**Where**: `src/Csharp2Md.Domain/Relations/ConfirmedRelation.cs`
**Depends on**: T41
**Reuses**: `src/Csharp2Md.Domain/Registry/TaxonomyRegistry.cs` triple enforcement;
`src/Csharp2Md.Domain/Proof/EvidenceChain.cs`
**Requirement**: TAX-44, TAX-45, TAX-60, TAX-61, TAX-63

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`, `dotnet-skills:csharp-type-design-performance`

**Done when**:

- [ ] Every required component omitted in turn is rejected naming it
- [ ] An unregistered triple is rejected naming source fact type, relation and target fact type
- [ ] An absent target identity is rejected - no dangling edge can be constructed
- [ ] An empty analysis-variant set is rejected
- [ ] `Resolution` has no constructor parameter and no setter, proven by reflection, and always reads `Confirmed`
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): enforce the confirmed relation invariants`

---

#### T43: Add the relation shape guards

**What**: Add the guards the triple alone does not express: a callable-requiring relation accepts only a
`Symbol` carrying the callable facet, `uses-contract` requires a registered payload role, `targets` accepts only
an inbound boundary operation, a deployment unit or an external system, `operates-on` accepts only a data object
or a data field, and supplied evidence must meet the relation's registered minimum.
**Where**: `src/Csharp2Md.Domain/Relations/RelationShapeGuards.cs`
**Depends on**: T42
**Reuses**: `src/Csharp2Md.Domain/Registry/TaxonomyRegistry.cs`,
`src/Csharp2Md.Domain/Facets/SymbolFacets.cs`
**Requirement**: TAX-46, TAX-49, TAX-50, TAX-51, TAX-53

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] A `Symbol` without the callable facet is rejected for every callable-requiring relation, named individually
- [ ] `uses-contract` without a registered payload role is rejected naming the relation and the missing role
- [ ] `targets` accepts each of its three legal target shapes and rejects an outbound operation and a symbol
- [ ] `operates-on` accepts a data object and a data field and rejects a data store
- [ ] Syntactic evidence where semantic is required is rejected naming both methods
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): guard relation shapes beyond the triple`

---

#### T44: Add the candidate and unresolved records

**What**: Add `CandidateLink.Create` and `UnresolvedRecord.Create`, the two non-confirmed outcomes, each
carrying its cause and the evidence available when the identity failed to close.
**Where**: `src/Csharp2Md.Domain/Relations/CandidateLink.cs`,
`src/Csharp2Md.Domain/Relations/UnresolvedRecord.cs`
**Depends on**: T43
**Reuses**: `src/Csharp2Md.Domain/Proof/EvidenceChain.cs`,
`src/Csharp2Md.Domain/Proof/RejectedCandidate.cs` cause vocabulary
**Requirement**: TAX-64

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] A candidate's resolution reads `Candidate` and an unresolved record's reads `Unresolved`, neither settable
- [ ] An unresolved record without a cause is rejected naming the parameter
- [ ] An unresolved record carries its available evidence and does not require a target identity
- [ ] A candidate carries a target it proposes; a candidate with no proposed target is rejected
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): represent candidate and unresolved outcomes`

---

#### T45: Add the open frontier

**What**: Add `OpenFrontier.Create(occurrence, cause)`, recorded on the originating occurrence so a further
continuation never weakens the confirmed relation already at that occurrence.
**Where**: `src/Csharp2Md.Domain/Relations/OpenFrontier.cs`
**Depends on**: T44
**Reuses**: `src/Csharp2Md.Domain/Observations/ObservationIdentity.cs`
**Requirement**: TAX-65

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] A frontier is keyed on the observation occurrence, not on the relation
- [ ] Recording a frontier at an occurrence that already carries a confirmed relation leaves that relation byte-identical, asserted before and after
- [ ] `frontier` reads `Open` and is not settable
- [ ] A frontier without a cause is rejected naming the parameter
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): record open frontiers without weakening edges`

---

#### T46: Add the confirmed relation set boundary

**What**: Add `ConfirmedRelationSet.Add`, rejecting any record whose resolution is not `Confirmed` so a
candidate or an unresolved record can never become an edge in the confirmed graph.
**Where**: `src/Csharp2Md.Domain/Relations/ConfirmedRelationSet.cs`
**Depends on**: T45
**Reuses**: `src/Csharp2Md.Domain/Identity/IdentityLedger.cs` registration shape
**Requirement**: TAX-62

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:csharp-api-design`

**Done when**:

- [ ] A confirmed relation is accepted
- [ ] A candidate and an unresolved record are each rejected naming the offending resolution
- [ ] The set is enumerable in deterministic order and adding the same relation twice is idempotent
- [ ] No member exposes a way to mutate a contained relation
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `feat(relations): keep non-confirmed records out of the graph`

---

#### T47: Prove the matrix accepts and rejects per relation

**What**: Author the relation-matrix suite: for each of the twelve relations, one registered triple accepted and
one unregistered triple rejected with source, relation and target named. This is the spec's Independent Test for
the relation story and spans the registry and every relation record.
**Where**: `tests/Csharp2Md.Domain.Tests/Relations/RelationMatrixTests.cs`
**Depends on**: T46
**Reuses**: `tests/Csharp2Md.Core.Tests/Analysis/Relations/RelationContractsTests.cs` per-kind table-driven
shape
**Requirement**: TAX-42, TAX-44, TAX-45

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`, `dotnet-test:test-gap-analysis`

**Done when**:

- [ ] Twelve accepted cases and twelve rejected cases, driven from the enum so a new relation without a case fails the suite
- [ ] Each rejection message contains all three names, asserted on the message, not just the exception type
- [ ] `contains` is proven restricted to structural owner and structural child, and rejected for a non-structural pair
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `test(relations): prove the matrix accepts and rejects per relation`

---

### Phase 10: Registry projection and drift gate

#### T48: Add the registry writer outside the domain

**What**: Add `TaxonomyRegistryWriter` in the test project, walking the ordered descriptor arrays to produce
UTF-8 JSON without BOM, LF endings, two-space indent and a declared stable key order, taking the tables as a
parameter so a test can inject a modified copy.
**Where**: `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyRegistryWriter.cs`
**Depends on**: T47
**Reuses**: `tests/Csharp2Md.Domain.Tests/DomainTestPaths.cs`; `System.Text.Json`, which only the test project
references
**Requirement**: TAX-84, TAX-85, TAX-87

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:serialization`

**Done when**:

- [ ] Two runs against unchanged tables produce byte-identical output, compared as bytes
- [ ] Output has no BOM, LF endings only, two-space indent, and keys in the declared order
- [ ] The emitted document enumerates every family, fact type, observation kind, facet axis with its values, relation with its triples and minimum evidence method, mapping role, proof axis and version axis
- [ ] The writer lives in the test project and `Csharp2Md.Domain` still declares no JSON dependency
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(registry): emit the taxonomy registry deterministically`

---

#### T49: Commit the registry artifact and its drift gate

**What**: Generate and commit `contracts/taxonomy-registry.json`, and add the drift gate comparing the committed
bytes to freshly emitted bytes, failing with the differing entries named.
**Where**: `contracts/taxonomy-registry.json`,
`tests/Csharp2Md.Domain.Tests/Registry/RegistryDriftGateTests.cs`
**Depends on**: T48
**Reuses**: `tests/Csharp2Md.Core.Tests/Topic/FrontmatterSchemaSyncTests.cs:33` drift-message shape
**Requirement**: TAX-86

**Tools**:

- MCP: NONE
- Skill: `dotnet-skills:snapshot-testing`

**Done when**:

- [ ] The committed file is byte-identical to the emitter output on a clean tree
- [ ] A hand-edited copy fails the gate, and the failure message names the differing entries, not just "files differ"
- [ ] The gate compares bytes, not a parsed document, so ordering and whitespace changes are caught
- [ ] The file resolves through the repository-root walk, so the gate passes from any working directory
- [ ] Gate check passes: `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: full

**Commit**: `feat(contracts): commit the taxonomy registry behind a drift gate`

---

#### T50: Prove emission never depends on frozen enumeration

**What**: Assert that the writer projects only from ordered `ImmutableArray` sources and that no frozen
collection is enumerated on the emission path, so the drift gate is deterministic rather than flaky.
**Where**: `tests/Csharp2Md.Domain.Tests/Registry/EmissionOrderTests.cs`
**Depends on**: T49
**Reuses**: `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyRegistryWriter.cs`
**Requirement**: TAX-85

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Every registry member the writer reads is an `ImmutableArray` or a value, asserted by reflection over the writer's dependencies
- [ ] No `FrozenSet` or `FrozenDictionary` member is reachable from the writer's projection path
- [ ] Emission order matches declared order for every table, asserted element by element rather than by count
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(registry): pin emission to declared order`

---

#### T51: Prove a duplicate triple fails the emitter

**What**: Assert that injecting a duplicated relation triple into a copied table set fails the emitter naming
the duplicate, closing TAX-90 at the level the criterion states it.
**Where**: `tests/Csharp2Md.Domain.Tests/Registry/DuplicateTripleTests.cs`
**Depends on**: T50
**Reuses**: `src/Csharp2Md.Domain/Registry/RelationDescriptor.cs` initialization guard
**Requirement**: TAX-90

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] A duplicated triple fails the emitter, and the message names the relation and both fact types
- [ ] The same duplication fails registry initialization, proving the emitter inherits the guard rather than re-implementing it
- [ ] The production table set is unmodified by the test
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `test(registry): fail the emitter on a duplicate triple`

---

### Phase 11: Cross-surface closure

#### T52: Prove no numeric confidence exists anywhere

**What**: Assert by reflection over the whole domain assembly that no public or internal member on any fact,
observation, relation, candidate or promotion type is a numeric confidence, score, weight, probability or
ranking.
**Where**: `tests/Csharp2Md.Domain.Tests/Surface/NoNumericConfidenceTests.cs`
**Depends on**: T51
**Reuses**: the reflection-over-assembly shape from
`tests/Csharp2Md.Domain.Tests/Isolation/DomainIsolationTests.cs`
**Requirement**: TAX-59

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`, `dotnet-skills:slopwatch`

**Done when**:

- [ ] Every type in the assembly is scanned, not a hand-listed subset, and the assertion fails naming the offending member
- [ ] Forbidden names include confidence, score, weight, probability, rank and certainty; the version axes and the occurrence ordinal are explicitly allowed
- [ ] A numeric member added to any taxonomy type is caught, verified by a negative control asserted against a local decoy type
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(domain): prove no numeric confidence on the taxonomy`

---

#### T53: Prove every taxonomy type is immutable

**What**: Assert by reflection that every taxonomy type exposes no settable property, no public field, no
mutable collection member and no mutating method.
**Where**: `tests/Csharp2Md.Domain.Tests/Surface/ImmutabilityTests.cs`
**Depends on**: T52
**Reuses**: `tests/Csharp2Md.Domain.Tests/Surface/NoNumericConfidenceTests.cs` assembly walk
**Requirement**: TAX-41, TAX-91

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:assertion-quality`

**Done when**:

- [ ] Every type is scanned; the assertion fails naming the offending type and member
- [ ] Collection-typed members are `ImmutableArray` or an immutable set, never `List`, array or `IList`
- [ ] Observations specifically are asserted immutable, closing TAX-41 at its own criterion
- [ ] A negative control proves the scan catches a settable property
- [ ] Gate check passes: `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: quick

**Commit**: `test(domain): prove the taxonomy surface is immutable`

---

#### T54: Close requirement traceability mechanically

**What**: Add the coverage gate asserting every requirement ID `TAX-01` through `TAX-91` is carried by at least
one test trait, and update the spec's traceability table to Verified.
**Where**: `tests/Csharp2Md.Domain.Tests/Surface/RequirementCoverageTests.cs`,
`.specs/features/knowledge-taxonomy-contract/spec.md`
**Depends on**: T53
**Reuses**: the `[Trait("Requirement", "TAX-nn")]` convention established in T2
**Requirement**: all of TAX-01..TAX-91

**Tools**:

- MCP: NONE
- Skill: `dotnet-test:test-gap-analysis`, `dotnet-test:assertion-quality`

**Done when**:

- [ ] The expected ID set is generated as `TAX-01`..`TAX-91`, not hand-listed
- [ ] Every ID has at least one test carrying its trait; the failure message names the uncovered IDs
- [ ] A trait naming an ID outside the range also fails, so a typo cannot masquerade as coverage
- [ ] The spec traceability table and coverage line are updated in the same commit
- [ ] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`
- [ ] Test count recorded (no silent deletions)

**Tests**: unit
**Gate**: build

**Commit**: `test(domain): gate requirement traceability on test traits`

---

## Phase Execution Map

Phases run in sequence; tasks within a phase run in the listed order. Execution is strictly sequential - there
is no intra-phase parallelism.

```
Phase 1 (T1-T2)    assembly foundation and isolation
Phase 2 (T3-T5)    identity grammar
Phase 3 (T6-T9)    scoped identities and determinism
Phase 4 (T10-T16)  closed vocabularies
Phase 5 (T17-T22)  registry tables and enforcement
Phase 6 (T23-T27)  literals and evidence primitives
Phase 7 (T28-T33)  observations and promotion audit trail
Phase 8 (T34-T40)  fact records
Phase 9 (T41-T47)  relations and proof invariants
Phase 10 (T48-T51) registry projection and drift gate
Phase 11 (T52-T54) cross-surface closure
```

**Batch packing at Execute**: 54 tasks pack into nine task-budgeted batches on whole-phase boundaries -
`P1+P2` (5), `P3` (4), `P4` (7), `P5` (6), `P6` (5), `P7` (6), `P8` (7), `P9` (7), `P10+P11` (7). That is more
than one batch, so the sub-agent offer is presented before execution begins.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1 | 1 project file + its solution entry | ✅ Granular |
| T2 | 1 test project + 2 files + its solution entry | ✅ Granular - a test project with no test is a hollow deliverable, so its first invariant is absorbed backward |
| T3, T4 | 1 ported file each | ✅ Granular |
| T5 | 2 files, one concept - the ledger's only input is the reference | ✅ Granular (cohesive) |
| T6 | 2 files - solution identity is meaningless without the workspace that scopes it | ✅ Granular (cohesive) |
| T7, T8 | 1 file each | ✅ Granular |
| T9 | 1 test file - a cross-type invariant spanning T4-T8, not deferred tests for one task | ✅ Granular |
| T10-T16 | 1 vocabulary file each | ✅ Granular |
| T17 | 2 files - the descriptor type and the table it declares | ✅ Granular (cohesive) |
| T18-T21 | 1 file each | ✅ Granular |
| T22 | 2 files - the version axes and the axis descriptor they are enumerated beside | ✅ Granular (cohesive) |
| T23-T26 | 1 file each | ✅ Granular |
| T27 | 2 files - payload and the extraction metadata that accompanies it | ✅ Granular (cohesive) |
| T28-T33 | 1 file each | ✅ Granular |
| T34-T39 | 1 file per fact family - the closed family set is the cohesive unit and its set-equality assertion is one test | ✅ Granular (cohesive) |
| T40 | 1 test file - a cross-family bijection spanning T34-T39 | ✅ Granular |
| T41-T43 | 1 file each | ✅ Granular |
| T44 | 2 files - the two non-confirmed outcomes share one cause vocabulary | ✅ Granular (cohesive) |
| T45, T46 | 1 file each | ✅ Granular |
| T47 | 1 test file - a per-relation matrix suite spanning the registry and every record | ✅ Granular |
| T48 | 1 file | ✅ Granular |
| T49 | 1 generated artifact + its gate - the artifact is unverifiable without the gate | ✅ Granular (cohesive) |
| T50-T53 | 1 test file each | ✅ Granular |
| T54 | 1 test file + the spec traceability update it closes | ✅ Granular (cohesive) |

No task creates more than one cohesive concept. Every multi-file `Where` is a single deliverable split across
files by the repository's existing file-per-concept convention, not two deliverables bundled together.

---

## Diagram-Definition Cross-Check

Within a phase the chain is strictly linear, so each task's `Depends on` names its immediate predecessor, and
the first task of a phase depends on the last task of the phase before it (a backward cross-phase edge, which
the diagrams do not draw because phase diagrams are drawn per phase).

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | chain head of Phase 1 | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | cross-phase, backward | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T5 | cross-phase, backward | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T7 | T7 → T8 | ✅ Match |
| T9 | T8 | T8 → T9 | ✅ Match |
| T10 | T9 | cross-phase, backward | ✅ Match |
| T11..T16 | T10..T15 respectively | T10 → T11 → T12 → T13 → T14 → T15 → T16 | ✅ Match |
| T17 | T16 | cross-phase, backward | ✅ Match |
| T18..T22 | T17..T21 respectively | T17 → T18 → T19 → T20 → T21 → T22 | ✅ Match |
| T23 | T22 | cross-phase, backward | ✅ Match |
| T24..T27 | T23..T26 respectively | T23 → T24 → T25 → T26 → T27 | ✅ Match |
| T28 | T27 | cross-phase, backward | ✅ Match |
| T29..T33 | T28..T32 respectively | T28 → T29 → T30 → T31 → T32 → T33 | ✅ Match |
| T34 | T33 | cross-phase, backward | ✅ Match |
| T35..T40 | T34..T39 respectively | T34 → T35 → T36 → T37 → T38 → T39 → T40 | ✅ Match |
| T41 | T40 | cross-phase, backward | ✅ Match |
| T42..T47 | T41..T46 respectively | T41 → T42 → T43 → T44 → T45 → T46 → T47 | ✅ Match |
| T48 | T47 | cross-phase, backward | ✅ Match |
| T49..T51 | T48..T50 respectively | T48 → T49 → T50 → T51 | ✅ Match |
| T52 | T51 | cross-phase, backward | ✅ Match |
| T53, T54 | T52, T53 | T52 → T53 → T54 | ✅ Match |

No task depends on a task in a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Project and solution wiring | none | none | ✅ OK |
| T2 | Assembly isolation invariants (+ wiring) | unit | unit | ✅ OK |
| T3, T4 | Identity grammar and identity types | unit | unit | ✅ OK |
| T5 | Identity grammar and identity types | unit | unit | ✅ OK |
| T6, T7, T8 | Identity grammar and identity types | unit | unit | ✅ OK |
| T9 | Identity grammar and identity types | unit | unit | ✅ OK |
| T10-T15 | Closed vocabularies | unit | unit | ✅ OK |
| T16 | Closed vocabularies | unit | unit | ✅ OK |
| T17-T22 | Registry descriptor tables and enforcement API | unit | unit | ✅ OK |
| T23, T24 | Literals and suspected-secret evidence | unit | unit | ✅ OK |
| T25, T26 | Literals and suspected-secret evidence | unit | unit | ✅ OK |
| T27 | Observation contract | unit | unit | ✅ OK |
| T28, T29 | Observation contract | unit | unit | ✅ OK |
| T30-T33 | Relation records and proof invariants | unit | unit | ✅ OK |
| T34-T39 | Fact records | unit | unit | ✅ OK |
| T40 | Fact records | unit | unit | ✅ OK |
| T41-T46 | Relation records and proof invariants | unit | unit | ✅ OK |
| T47 | Relation records and proof invariants | unit | unit | ✅ OK |
| T48 | Registry projection and drift gate | unit | unit | ✅ OK |
| T49 | Registry projection and drift gate (+ committed artifact) | unit | unit | ✅ OK |
| T50, T51 | Registry projection and drift gate | unit | unit | ✅ OK |
| T52-T54 | Cross-surface reflection invariants | unit | unit | ✅ OK |

Only T1 declares `Tests: none`, and the matrix declares `none` for project and solution wiring - a declarative
MSBuild file with no branches, covered by the build gate. No task defers its tests to another task.
