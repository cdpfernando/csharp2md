# Retrieval Projections Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**Standing skip — discrimination sensor**: the user runs Stryker manually; do not run the sensor's fault-injection pass. Every other Verifier step (spec-anchored coverage check, gate check, code-quality check) still runs as documented.

---

**Design**: `.specs/features/retrieval-projections/design.md`
**Status**: Approved

---

## Test Coverage Matrix

> Generated from codebase sampling and project guidelines. Guidelines found: [`AGENTS.md`](AGENTS.md), [`CLAUDE.md`](CLAUDE.md) (retrieval-led reasoning, Roslyn API verification, multi-csproj test execution note, LocalCorpus fixture rule), [`Directory.Build.props`](Directory.Build.props) (`TreatWarningsAsErrors`). No CI workflow exists. Carried forward from the `components-deployments-configuration` matrix and extended with the projection layer this feature creates.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain literals and facts | unit | All branches; construction guards; 1:1 to spec ACs | `tests/Csharp2Md.Domain.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj` |
| Storage wire mapping and ordering | unit | All branches; ordering keys asserted total; round-trip | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Storage publication pipeline and validation | unit + integration | All branches; every abort path asserted with its reason code | `tests/Csharp2Md.Storage.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj` |
| Projection projectors (pure, no I/O) | unit | All branches; 1:1 to spec ACs; every listed edge case | `tests/Csharp2Md.Projection.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |
| Analysis write port and readers | unit | All branches; reader contract asserted with a counting double | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Semantics emitters (Roslyn-backed) | unit + integration | All branches; fixture-backed assertions on the emitted fact | `tests/Csharp2Md.Analysis.Tests/Semantics/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| Pipeline stage / orchestrator | integration | Stage wiring, arity and name checks, engine wiring | `tests/Csharp2Md.Analysis.Tests/Pipeline/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| End-to-end (fixture analyze) | integration | Full pipeline; citations resolve; determinism; secret absence | `tests/Csharp2Md.Analysis.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus"` |
| CLI surface | unit | No new flags; wiring asserted; CLI→Domain isolation | `tests/Csharp2Md.Cli.Tests/**/*.cs` | `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus"` |
| Project files (`.csproj` references) | unit | Reference set asserted by XML inspection, per the existing isolation tests | `tests/**/Isolation/*.cs` | matching project's command |

## Gate Check Commands

> Generated from codebase — confirm before Execute. Multi-csproj `dotnet test` hits MSB1008; run each test project separately. `Category=LocalCorpus` is excluded from every gate; those tests run only after the Verifier and only when the local clones exist.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After unit-only tasks scoped to a single assembly | the matching project's command from the matrix above |
| Full | After tasks that cross Domain/Storage/Projection or touch the wire contract | `dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |
| Build | After phase completion, port changes, and every task touching Analysis or the CLI | `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj` |

**Measured baseline (2026-08-26, `792d9cb`)**: `dotnet build` clean, 0 warnings, 0 errors. **1342 tests, 1342 passing, 0 failing** — Domain 549, Analysis 590, Storage 173, Cli 27, Projection 3. No baseline repair phase is needed. Every task reports its new total; a drop means a silent deletion.

---

## Tooling

Confirmed at task approval: the routing in [`CLAUDE.md`](CLAUDE.md) applies as written. No skill is invoked speculatively outside these routes.

| When | Skill |
| --- | --- |
| A task creates or changes a C# type | `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-type-design-performance` |
| After a phase lands substantial new code | `dotnet-skills:slopwatch` |
| Before declaring the suite done | `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns` |
| Before the Verifier runs | `dotnet-test:test-gap-analysis` |
| Running gates | `dotnet-test:run-tests` |

Known defect to avoid: the `csharp-coding-standards` validated value-object snippet does not compile (CS0111 — a validating constructor duplicates the primary constructor). Use the explicit-property form. This matters for `DeclarationLocator`, `ArtifactSlot` and `ArtifactCitation` in T3, T4 and T19.

**MCP**: none required. Context7 applies only if an unfamiliar Roslyn or `System.Text.Json` API is needed, per the Knowledge Verification Chain.

---

## Execution Plan

Phases are ordered and run sequentially — each phase completes before the next begins, and tasks within a phase execute in order. The first task of each phase carries `Depends on: None` because phase ordering is the protocol, not an intra-phase edge.

### Phase 1: Total ordering and one authority on keys

Published ordinals are not deterministic today. Nothing downstream is trustworthy until they are, and until one component owns the canonical-key conventions.

```
T1 → T2 → T3 → T4 → T5 → T6 → T7
```

### Phase 2: The source seam

The write port must be able to carry source bytes before any source artifact exists.

```
T8 → T9 → T10 → T11 → T12
```

### Phase 3: Projector port and fail-closed validation

Prove the abort behaviour before there is anything real to publish.

```
T13 → T14 → T15 → T16 → T17 → T18
```

### Phase 4: Declaration locators

Catalogs, postings and Markdown all link into source through this.

```
T19 → T20 → T21 → T22
```

### Phase 5: Source projection and redaction

The first real projection; exercises the reader and the validator's span check.

```
T23 → T24 → T25 → T26 → T27
```

### Phase 6: Catalogs

```
T28 → T29 → T30 → T31 → T32
```

### Phase 7: Postings

```
T33 → T34 → T35 → T36 → T37
```

### Phase 8: Markdown pages and guides

Consume the citations phases 6 and 7 produce.

```
T38 → T39 → T40 → T41 → T42
```

### Phase 9: Sharding, determinism, security and isolation

Cross-cutting assertions over everything above.

```
T43 → T44 → T45 → T46 → T47 → T48 → T49
```

---

## Task Breakdown

### T1: Make the confirmed-relation ordering key total

**What**: Append `:{ContentSha256}` to the confirmed-relation ordering key so ties between relations of the same kind and endpoints resolve deterministically instead of by snapshot insertion order.
**Where**: `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`
**Depends on**: None
**Reuses**: `ConfirmedRelationDto.ContentSha256`, already computed
**Requirement**: RP-46, RP-48

**Tools**: per the Tooling table above.

**Done when**:
- [x] The ordering key includes the content hash as the final component
- [x] A test constructs two confirmed relations of the same kind between the same source and target differing only in evidence chain, maps them from two different insertion orders, and asserts identical output order
- [x] Gate check passes: Full gate command
- [x] Test count: 1343+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T2: Make the observation ordering key total

**What**: Append `:{ContentSha256}` to the observation ordering key, closing the latent defect recorded in the 5D handoff.
**Where**: `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`
**Depends on**: T1
**Reuses**: `ObservationDto.ContentSha256`, already computed
**Requirement**: RP-46, RP-48

**Tools**: per the Tooling table above.

**Done when**:
- [x] The ordering key includes the content hash as the final component
- [x] A test maps two observations sharing owner, kind and occurrence ordinal but differing in payload, from two insertion orders, and asserts identical output order
- [x] Gate check passes: Full gate command
- [x] Test count: 1344+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T3: Introduce PublishedPackageView with canonical slots

**What**: Create the view that owns canonical artifact keys and element counts, lifting the conventions currently inlined in `PackagePublisher`.
**Where**: `src/Csharp2Md.Storage/Mapping/PublishedPackageView.cs`
**Depends on**: T2
**Reuses**: `TaxonomyTables.Default` ordering; the key strings in `PackagePublisher.ToPublicationOrder`
**Requirement**: RP-19, RP-26

**Tools**: per the Tooling table above.

**Done when**:
- [x] `ArtifactSlot` and `ArtifactCitation` records defined per the design
- [x] `From(WireDocument)` produces slots only for content that will be published, omitting empty shards
- [x] Tests assert the slot set for a filled document and for `FactualSnapshot.Empty`
- [x] Gate check passes: Full gate command
- [x] Test count: 1350+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T4: Add fact and relation location lookups to the view

**What**: Implement `TryLocate` and `TryLocateRelation` so a caller turns a fact id or a relation index into an artifact key plus ordinal.
**Where**: `src/Csharp2Md.Storage/Mapping/PublishedPackageView.cs`
**Depends on**: T3
**Reuses**: the slot table built in T3
**Requirement**: RP-20, RP-27

**Tools**: per the Tooling table above.

**Done when**:
- [x] Both lookups return the key and zero-based ordinal within that artifact
- [x] A test resolves every fact in a filled document and asserts the cited artifact indexed at the cited ordinal yields that fact
- [x] Both lookups return false for an unknown id rather than throwing
- [x] Gate check passes: Full gate command
- [x] Test count: 1356+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T5: Derive the manifest from published fragments

**What**: Create `ManifestBuilder` that builds `ManifestEnvelope` from the final fragment list, so the manifest lists exactly what exists.
**Where**: `src/Csharp2Md.Storage/Mapping/ManifestBuilder.cs`
**Depends on**: T4
**Reuses**: `ManifestEnvelope`, `ManifestEntry`, the stem-form key convention from `DomainMapper.AddFamily`
**Requirement**: RP-03

**Tools**: per the Tooling table above.

**Done when**:
- [x] Every manifest entry corresponds to a fragment in the same publication
- [x] A test asserts no manifest entry names an artifact absent from the fragment list, using a document with several empty families
- [x] Existing manifest assertions still pass unchanged
- [x] Gate check passes: Full gate command
- [x] Test count: 1362+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T6: Retire manifest construction from DomainMapper

**What**: Remove `BuildManifestArtifacts` and its call site so `ToWire` no longer produces a manifest, leaving `ManifestBuilder` as the only producer.
**Where**: `src/Csharp2Md.Storage/Mapping/DomainMapper.cs`
**Depends on**: T5
**Reuses**: `ManifestBuilder` from T5
**Requirement**: RP-03

**Tools**: per the Tooling table above.

**Done when**:
- [x] `WireDocument.Manifest` is populated by the publication path, not by `ToWire`
- [x] `PackagePublisher` reads its keys from `PublishedPackageView` rather than inline strings
- [x] A test asserts the published manifest lists no entry with `Count = 0` for an omitted shard
- [x] Gate check passes: Full gate command
- [x] Test count: 1365+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T7: Extract PublicationPipeline shared by both stores

**What**: Create the internal commit sequence and make `FilesystemTransactionalStore` and `InMemoryTransactionalStore` delegate to it.
**Where**: `src/Csharp2Md.Storage/Mapping/PublicationPipeline.cs`
**Depends on**: T6
**Reuses**: the three-line sequence currently duplicated in both stores
**Requirement**: RP-02

**Tools**: per the Tooling table above.

**Done when**:
- [x] Both stores call the pipeline and hold no independent copy of the sequence
- [x] A test asserts both stores produce the same fragment key set for the same snapshot
- [x] Gate check passes: Build gate command
- [x] Test count: 1370+ tests pass (no silent deletions)

**Tests**: integration
**Gate**: build

---

### T8: Declare ISourceDocumentReader on the write port

**What**: Add the reader contract beside `ITransactionalStore`, exposing `TryRead` and a canonically ordered `Documents` list.
**Where**: `src/Csharp2Md.Analysis/Storage/ISourceDocumentReader.cs`
**Depends on**: None
**Reuses**: `DocumentId` from Domain; the port's existing location and style
**Requirement**: RP-07

**Tools**: per the Tooling table above.

**Done when**:
- [x] Interface defined per the design, with no Roslyn or JSON types on its surface
- [x] A test asserts the Analysis public surface still exposes no forbidden namespace
- [x] Gate check passes: Build gate command
- [x] Test count: 1372+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: build

---

### T9: Add the deferred form to StagedFragment

**What**: Add `StagedFragment.Deferred` and `ReadPayload()` so a fragment can materialize its bytes once, on demand, without retaining them.
**Where**: `src/Csharp2Md.Analysis/Storage/StagedFragment.cs`
**Depends on**: T8
**Reuses**: the existing eager constructor, which stays source-compatible
**Requirement**: RP-57

**Tools**: per the Tooling table above.

**Done when**:
- [x] `ReadPayload()` returns the stored array for eager fragments and invokes the provider for deferred ones
- [x] A test asserts a deferred fragment's provider is invoked exactly once across two `ReadPayload()` calls, or that the second call is rejected — whichever the implementation chooses, asserted explicitly
- [x] `IsDeferred` is observable on the returned publication
- [x] Gate check passes: Build gate command
- [x] Test count: 1376+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: build

---

### T10: Thread the reader through Open and staging writes

**What**: Change `ITransactionalStore.Open` to accept an `ISourceDocumentReader`, update both stores, and make `WriteStaging` write deferred fragments without holding their bytes.
**Where**: `src/Csharp2Md.Analysis/Storage/ITransactionalStore.cs`
**Depends on**: T9
**Reuses**: the existing staging and atomic-swap logic, unchanged
**Requirement**: RP-05, RP-57

**Tools**: per the Tooling table above.

**Done when**:
- [x] `Open` takes the reader; no projector member is added to the port
- [x] `WriteStaging` streams each deferred fragment and does not accumulate payloads
- [x] A test with a counting reader asserts each document is requested at most once during one commit
- [x] Gate check passes: Build gate command
- [x] Test count: 1381+ tests pass (no silent deletions)

**Tests**: integration
**Gate**: build

---

### T11: Implement the filesystem source reader in the inventory

**What**: Build the `DocumentId → absolute path` map during inventory and expose it as an `ISourceDocumentReader` that reads lazily.
**Where**: `src/Csharp2Md.Analysis/Inventory/FilesystemSourceDocumentReader.cs`
**Depends on**: T10
**Reuses**: `InventoryStage`'s authorized root and per-document absolute paths
**Requirement**: RP-07

**Tools**: per the Tooling table above.

**Done when**:
- [x] `Documents` is ordered by document id, ordinally
- [x] `TryRead` returns false for a path removed after inventory, without throwing
- [x] Tests cover a readable document, a removed document, and ordering
- [x] Gate check passes: Build gate command
- [x] Test count: 1386+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: build

---

### T12: Wire the reader through AnalysisEngine

**What**: Have `AnalysisEngine` construct the reader from the inventory result and pass it to `_store.Open`.
**Where**: `src/Csharp2Md.Analysis/AnalysisEngine.cs`
**Depends on**: T11
**Reuses**: the existing per-solution session lifecycle
**Requirement**: RP-07

**Tools**: per the Tooling table above.

**Done when**:
- [x] Every solution's session receives a reader covering that solution's documents only
- [x] A multi-solution test asserts one solution's reader never exposes another's documents
- [x] Gate check passes: Build gate command
- [x] Test count: 1390+ tests pass (no silent deletions)

**Tests**: integration
**Gate**: build

---

### T13: Declare IPackageProjector and the empty Projection implementation

**What**: Add the projector port to Storage and an empty `PackageProjector` in Projection, with the project references the design requires.
**Where**: `src/Csharp2Md.Storage/IPackageProjector.cs`
**Depends on**: None
**Reuses**: `StagedFragment` as the return type, so projections need no parallel concept
**Requirement**: RP-01

**Tools**: per the Tooling table above.

**Done when**:
- [x] `Csharp2Md.Projection.csproj` references Storage and Domain, and nothing else
- [x] `PackageProjector` implements the port and returns an empty fragment array
- [x] A test asserts the reference set by XML inspection, matching the existing isolation-test pattern
- [x] Gate check passes: Build gate command
- [x] Test count: 1394+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: build

---

### T14: Invoke the projector inside the publication pipeline

**What**: Call the projector between validation and ordering, and include its fragments in the manifest and the publication.
**Where**: `src/Csharp2Md.Storage/Mapping/PublicationPipeline.cs`
**Depends on**: T13
**Reuses**: `ManifestBuilder` from T5, which already derives entries from fragments
**Requirement**: RP-03

**Tools**: per the Tooling table above.

**Done when**:
- [x] A projector returning one fragment produces that fragment on disk and a matching manifest entry
- [x] The projector receives the post-validation document, not the pre-validation one
- [x] Gate check passes: Full gate command
- [x] Test count: 1398+ tests pass (no silent deletions)

**Tests**: integration
**Gate**: full

---

### T15: Validate projection keys and ordinals

**What**: Create `ProjectionValidator` and implement the missing-key and out-of-range-ordinal checks, running before any staging write.
**Where**: `src/Csharp2Md.Storage/Validation/ProjectionValidator.cs`
**Depends on**: T14
**Reuses**: `PublicationRejectedException` and `PackageValidator`'s reason-code style
**Requirement**: RP-41, RP-42, RP-43

**Tools**: per the Tooling table above.

**Done when**:
- [x] A projection citing an absent artifact key aborts with `projection-key` naming that key
- [x] A projection citing an out-of-range ordinal aborts with `projection-ordinal` naming key and ordinal
- [x] A test asserts no staging file exists on disk after either abort
- [x] Gate check passes: Full gate command
- [x] Test count: 1404+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T16: Validate reproduced values and locator spans

**What**: Add the mismatched-value and out-of-bounds-span checks to `ProjectionValidator`.
**Where**: `src/Csharp2Md.Storage/Validation/ProjectionValidator.cs`
**Depends on**: T15
**Reuses**: the `ArtifactCitation` structure from T3
**Requirement**: RP-44, RP-45

**Tools**: per the Tooling table above.

**Done when**:
- [x] A Markdown value differing from the cited payload aborts with `projection-value` naming page and value
- [x] A locator span outside the published source artifact aborts with `projection-span` naming the locator
- [x] Both rejection details are asserted on their message content, not just the exception type
- [x] Gate check passes: Full gate command
- [x] Test count: 1410+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T17: Assert the abort preserves the prior package

**What**: Prove that a throwing projector leaves the previously committed package byte-identical and removes the staging directory.
**Where**: `tests/Csharp2Md.Storage.Tests/Filesystem/ProjectionAbortTests.cs`
**Depends on**: T16
**Reuses**: the existing atomic-replace and `.bak` rollback tests as the pattern
**Requirement**: RP-04

**Tools**: per the Tooling table above.

**Done when**:
- [x] Commit once with a working projector, then again with a throwing one; assert every file's bytes are unchanged
- [x] Assert no staging or `.bak` directory survives the failed attempt
- [x] Assert `PublicationRejectedException` carries a `projection` reason
- [x] Gate check passes: Full gate command
- [x] Test count: 1414+ tests pass (no silent deletions)

**Tests**: integration
**Gate**: full

---

### T18: Wire the projector into the CLI

**What**: Construct `FilesystemTransactionalStore` with a `PackageProjector` in `CommandFactory`, adding no command or option.
**Where**: `src/Csharp2Md.Cli/CommandFactory.cs`
**Depends on**: T17
**Reuses**: the existing `analyze` action and its store construction
**Requirement**: RP-06

**Tools**: per the Tooling table above.

**Done when**:
- [x] `analyze` publishes projections without any new flag
- [x] A store constructed without a projector commits factual artifacts alone and publishes no projection artifact
- [x] The CLI still declares no direct project reference to Domain
- [x] Gate check passes: Build gate command
- [x] Test count: 1418+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: build

---

### T19: Add DeclarationLocator to Domain

**What**: Define the locator value type and add it as an optional member of `Symbol`, leaving identity computation untouched.
**Where**: `src/Csharp2Md.Domain/Literals/DeclarationLocator.cs`
**Depends on**: None
**Reuses**: `DocumentId`, `SourceSpan`, `DocumentHash`
**Requirement**: RP-13

**Tools**: per the Tooling table above.

**Done when**:
- [x] `Symbol.Create` accepts an optional locator and produces the same `FactId` with or without it
- [x] Construction guards reject an uninitialized document or hash when a locator is supplied
- [x] Tests cover present, absent and invalid locators
- [x] Gate check passes: Full gate command
- [x] Test count: 1424+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T20: Populate the locator in SymbolFactEmitter

**What**: Fill the declaration locator from each declaration's syntax span, breaking ties across multiple declaring references ordinally.
**Where**: `src/Csharp2Md.Analysis/Semantics/SymbolFactEmitter.cs`
**Depends on**: T19
**Reuses**: the node and semantic model the emitter already holds
**Requirement**: RP-14, RP-15, RP-16

**Tools**: per the Tooling table above.

**Done when**:
- [x] The span covers the whole declaration; a test slices the fixture file at that span and asserts the slice parses as a complete member
- [x] A partial type declared in two documents yields the ordinally first locator, asserted from both document orders
- [x] A declaration in a document with no `Document` fact yields no locator
- [x] Gate check passes: Build gate command
- [x] Test count: 1430+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: build

---

### T21: Carry the locator through the wire mapping

**What**: Extend `SymbolDto` with the locator and round-trip it through `DomainMapper`.
**Where**: `src/Csharp2Md.Storage/Wire/StructuralFactDtos.cs`
**Depends on**: T20
**Reuses**: `EvidenceLocatorDto` and `SourceSpanDto` shapes for consistency
**Requirement**: RP-13

**Tools**: per the Tooling table above.

**Done when**:
- [x] A symbol with a locator round-trips to Domain and back with an equal locator
- [x] A symbol without a locator round-trips to an absent locator, not a default-valued one
- [x] Gate check passes: Full gate command
- [x] Test count: 1434+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T22: Assert the taxonomy registry did not drift

**What**: Prove that adding the locator left `contracts/taxonomy-registry.json` byte-identical.
**Where**: `tests/Csharp2Md.Storage.Tests/Schema/EmbeddedRegistryDriftTests.cs`
**Depends on**: T21
**Reuses**: the existing byte-comparison drift test
**Requirement**: RP-17

**Tools**: per the Tooling table above.

**Done when**:
- [x] The committed registry bytes equal the projected registry bytes
- [x] A test asserts `Symbol`'s registry entry still names exactly `project` and `signature` as identity components
- [x] Gate check passes: Full gate command
- [x] Test count: 1436+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T23: Emit one source artifact per analyzed document

**What**: Create `SourceProjector` emitting a deferred fragment per document, keyed by owning project and repository-relative path.
**Where**: `src/Csharp2Md.Projection/Source/SourceProjector.cs`
**Depends on**: None
**Reuses**: `ISourceDocumentReader`; `DocumentDto.RelativePath`
**Requirement**: RP-07, RP-08

**Tools**: per the Tooling table above.

**Done when**:
- [x] Exactly one artifact per inventoried document, with no absolute path in any key
- [x] Two documents in different projects sharing a relative path produce distinct keys
- [x] Non-UTF-8 bytes pass through unchanged
- [x] Gate check passes: Full gate command
- [x] Test count: 1442+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T24: Redact suspected-secret spans

**What**: Replace each suspected-secret span with the fixed 17-byte marker, merging overlapping spans before substitution.
**Where**: `src/Csharp2Md.Projection/Source/SecretRedactor.cs`
**Depends on**: T23
**Reuses**: the suspected-secret records already in `DiagnosticsEnvelope`
**Requirement**: RP-09

**Tools**: per the Tooling table above.

**Done when**:
- [x] The marker length is constant regardless of the span's original length
- [x] Overlapping spans merge into one, so no marker is nested inside another
- [x] A span covering the whole document yields an artifact consisting solely of the marker
- [x] Gate check passes: Full gate command
- [x] Test count: 1450+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T25: Emit the redaction envelope

**What**: Write the sidecar declaring `redacted`, the ordinal-sorted spans and both hashes, only for redacted documents.
**Where**: `src/Csharp2Md.Projection/Source/RedactionEnvelope.cs`
**Depends on**: T24
**Reuses**: `CanonicalJson` for deterministic serialization
**Requirement**: RP-10

**Tools**: per the Tooling table above.

**Done when**:
- [x] A redacted document has an envelope declaring both hashes and every redacted span in ordinal order
- [x] An unredacted document has no envelope
- [x] Gate check passes: Full gate command
- [x] Test count: 1455+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T26: Verify source integrity against the published hash

**What**: Compare each document's bytes read at commit time against `DocumentDto.ContentSha256` and abort on drift.
**Where**: `src/Csharp2Md.Projection/Source/SourceProjector.cs`
**Depends on**: T25
**Reuses**: the `ContentSha256` already published in `facts/structural.json`
**Requirement**: RP-11

**Tools**: per the Tooling table above.

**Done when**:
- [x] An unredacted document's published sha256 equals its `ContentSha256`
- [x] A document whose bytes changed after inventory aborts with `source-drift` naming the document
- [x] A test asserts the prior package survives that abort byte-identical
- [x] Gate check passes: Full gate command
- [x] Test count: 1461+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T27: Assert no secret reaches the published package

**What**: End-to-end test over the fixture asserting the suspected secret's bytes appear in no published artifact.
**Where**: `tests/Csharp2Md.Analysis.Tests/Projection/SourceSecretAbsenceTests.cs`
**Depends on**: T26
**Reuses**: the fixture's existing suspected-secret configuration value
**Requirement**: RP-12

**Tools**: per the Tooling table above.

**Done when**:
- [ ] The secret's literal bytes appear in no fragment: source, catalog, posting, Markdown, manifest or diagnostic
- [ ] No hash of the individual secret value appears in any fragment
- [ ] The marker occupies the secret's span in the published source artifact
- [ ] Gate check passes: Build gate command
- [ ] Test count: 1465+ tests pass (no silent deletions)

**Tests**: integration
**Gate**: build

---

### T28: Project the entry point and boundary operation catalogs

**What**: Create `CatalogProjector` emitting these two catalogs, each entry citing key and ordinal.
**Where**: `src/Csharp2Md.Projection/Catalogs/CatalogProjector.cs`
**Depends on**: None
**Reuses**: `PublishedPackageView.TryLocate`, so no key convention is restated
**Requirement**: RP-18

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Every entry carries fact id, artifact key and zero-based ordinal
- [ ] A test resolves every entry against the cited artifact and asserts the fact id matches
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1471+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T29: Project the component, deployment unit and contract catalogs

**What**: Extend `CatalogProjector` with these three identity kinds.
**Where**: `src/Csharp2Md.Projection/Catalogs/CatalogProjector.cs`
**Depends on**: T28
**Reuses**: the entry shape established in T28
**Requirement**: RP-18

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Components and deployment units share one catalog, per the spec's wording
- [ ] Every entry resolves against its cited artifact and ordinal
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1477+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T30: Project the data store, object and field catalog

**What**: Extend `CatalogProjector` with the persistence identities in one catalog.
**Where**: `src/Csharp2Md.Projection/Catalogs/CatalogProjector.cs`
**Depends on**: T29
**Reuses**: the entry shape from T28
**Requirement**: RP-18

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Stores, objects and fields appear in one catalog, each labelled by its fact type
- [ ] Every entry resolves against its cited artifact and ordinal
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1483+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T31: Project the prioritized unknowns catalog

**What**: Rank unresolved records by their owner's confirmed-relation degree, descending, then by fact id.
**Where**: `src/Csharp2Md.Projection/Catalogs/UnknownRanking.cs`
**Depends on**: T30
**Reuses**: the confirmed-relation arrays in `PublishedPackageView.Document`
**Requirement**: RP-23

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Degree counts relations where the owner is either source or target
- [ ] An owner with zero relations ranks last rather than being omitted
- [ ] Equal degrees break by fact id ordinally, asserted from two input orders
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1490+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T32: Enforce catalog omission, ordering and value provenance

**What**: Omit a catalog whose family has no facts, order entries by fact id, and reject any value absent from the cited artifact.
**Where**: `src/Csharp2Md.Projection/Catalogs/CatalogProjector.cs`
**Depends on**: T31
**Reuses**: the empty-shard omission convention already in `PackagePublisher`
**Requirement**: RP-21, RP-22, RP-24

**Tools**: per the Tooling table above.

**Done when**:
- [ ] A family with no facts produces no catalog artifact, asserted against an empty document
- [ ] Entries are ordered by ordinal comparison of the fact id
- [ ] A test asserts every catalog entry value is present in the artifact it cites
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1497+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T33: Project outgoing and incoming relation postings

**What**: Create `PostingProjector` emitting, per fact id, the confirmed relations where it is source and where it is target.
**Where**: `src/Csharp2Md.Projection/Postings/PostingProjector.cs`
**Depends on**: None
**Reuses**: `PublishedPackageView.TryLocateRelation`
**Requirement**: RP-25, RP-26

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Entries carry artifact key and ordinal only, with no relation payload or evidence chain
- [ ] A self-relation appears in both the outgoing and the incoming posting for that fact
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1504+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T34: Project callers and callees postings

**What**: Derive these two postings from confirmed `invokes` relations only.
**Where**: `src/Csharp2Md.Projection/Postings/PostingProjector.cs`
**Depends on**: T33
**Reuses**: the entry shape from T33
**Requirement**: RP-28

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Only confirmed `invokes` relations contribute; a test asserts a candidate invocation appears in neither
- [ ] The callers posting for a known callee lists exactly the callers present in `relations/confirmed/invokes.json`
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1511+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T35: Project contract and data-access postings

**What**: Emit contract producers and consumers, and data readers and writers.
**Where**: `src/Csharp2Md.Projection/Postings/PostingProjector.cs`
**Depends on**: T34
**Reuses**: `TaxonomyTables.Default.Relations` for canonical relation ordering
**Requirement**: RP-25

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Producer and consumer roles derive from the contract binding's payload role, not from a name
- [ ] Reader and writer roles derive from the data operation's kind
- [ ] Every cited ordinal resolves to the claimed relation
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1519+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T36: Project unknowns and open frontier postings

**What**: Emit these separately and keep candidates, unresolved records and frontiers out of the confirmed postings.
**Where**: `src/Csharp2Md.Projection/Postings/PostingProjector.cs`
**Depends on**: T35
**Reuses**: the candidate, unresolved and frontier arrays already in the wire document
**Requirement**: RP-29

**Tools**: per the Tooling table above.

**Done when**:
- [ ] A test asserts no candidate, unresolved record or frontier id appears in any confirmed posting
- [ ] Frontiers cite `relations/frontiers.json` with a resolving ordinal
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1526+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T37: Assert every citation resolves end to end

**What**: Analyze the fixture and resolve every catalog and posting citation against the published artifacts.
**Where**: `tests/Csharp2Md.Analysis.Tests/Projection/CitationResolutionTests.cs`
**Depends on**: T36
**Reuses**: the fixture analyze harness used by the 5D integration tests
**Requirement**: RP-20, RP-27, RP-30

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Every citation in every published catalog and posting resolves to the claimed fact or relation
- [ ] Posting groups are ordered by subject fact id, entries by artifact key then ordinal
- [ ] Gate check passes: Build gate command
- [ ] Test count: 1532+ tests pass (no silent deletions)

**Tests**: integration
**Gate**: build

---

### T38: Project entry point and boundary operation pages

**What**: Create `MarkdownProjector` emitting these pages with fact id, facets, direct confirmed relations and links.
**Where**: `src/Csharp2Md.Projection/Markdown/MarkdownProjector.cs`
**Depends on**: None
**Reuses**: the `ArtifactCitation` structure the validator already checks
**Requirement**: RP-31, RP-32

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Each page states the identity's fact id, its facet values and its direct confirmed relations
- [ ] Links point at catalogs, postings and `source/` artifacts that exist in the same publication
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1539+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T39: Project component, contract and persistence pages

**What**: Extend `MarkdownProjector` with component, deployment unit, contract, data store and data object pages, each citing value provenance.
**Where**: `src/Csharp2Md.Projection/Markdown/MarkdownProjector.cs`
**Depends on**: T38
**Reuses**: the page shape from T38
**Requirement**: RP-31, RP-34

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Every reproduced value cites the canonical key and ordinal it came from
- [ ] A test parses each page and asserts the cited artifact at the cited ordinal holds that value
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1547+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T40: Enforce the Markdown boundary and page determinism

**What**: Assert no page introduces a non-authoritative value, no page exists per callable or observation, and two runs produce identical bytes.
**Where**: `tests/Csharp2Md.Projection.Tests/Markdown/MarkdownBoundaryTests.cs`
**Depends on**: T39
**Reuses**: the determinism assertion style from the 5D classifier tests
**Requirement**: RP-33, RP-35, RP-36

**Tools**: per the Tooling table above.

**Done when**:
- [ ] No page names an identity absent from the facts
- [ ] No page exists for a callable, symbol, observation or individual relation
- [ ] Two projections of the same document produce byte-identical pages
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1554+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T41: Project the retrieval guide

**What**: Emit `retrieval.md` documenting the seven retrieval scenarios and naming the artifacts each one reads.
**Where**: `src/Csharp2Md.Projection/Guides/RetrievalGuideProjector.cs`
**Depends on**: T40
**Reuses**: `PublishedPackageView.Slots`, so the guide names only artifacts that exist
**Requirement**: RP-37, RP-40

**Tools**: per the Tooling table above.

**Done when**:
- [ ] All seven scenarios from `output-and-retrieval.md` are documented
- [ ] Each scenario names at least one artifact key present in the same publication
- [ ] The guide is listed in the manifest and contains no absolute path
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1561+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T42: Project the generated AGENTS.md

**What**: Emit the package's `AGENTS.md` explaining the manifest entry point, proof states, source retrieval and the Markdown-is-not-authority rule.
**Where**: `src/Csharp2Md.Projection/Guides/AgentsGuideProjector.cs`
**Depends on**: T41
**Reuses**: the slot list from T41
**Requirement**: RP-38, RP-39

**Tools**: per the Tooling table above.

**Done when**:
- [ ] All four required subjects are covered
- [ ] A test asserts the text enumerates no fact type, observation kind, facet axis or relation triple
- [ ] The file is listed in the manifest and contains no absolute path
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1567+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T43: Implement bounded sharding

**What**: Create `ShardWriter` splitting a catalog or posting past a configurable ceiling, by a fact-id-derived bucket key.
**Where**: `src/Csharp2Md.Projection/ShardWriter.cs`
**Depends on**: None
**Reuses**: `CanonicalJson` for measuring serialized size
**Requirement**: RP-52, RP-53, RP-54

**Tools**: per the Tooling table above.

**Done when**:
- [ ] The default ceiling is 1 MiB and is overridable
- [ ] A synthetic over-ceiling input splits; the fixture's own catalogs do not
- [ ] Bucket keys derive from the fact id; a test renaming every display name yields the same assignment
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1574+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T44: Assert split preserves content and manifest coverage

**What**: Prove the union of shard entries equals the unsplit set and every shard is in the manifest.
**Where**: `tests/Csharp2Md.Projection.Tests/ShardWriterInvarianceTests.cs`
**Depends on**: T43
**Reuses**: the synthetic over-ceiling input from T43
**Requirement**: RP-55, RP-56

**Tools**: per the Tooling table above.

**Done when**:
- [ ] The union of shard entries equals the entry set produced without splitting
- [ ] Every resulting shard appears in the manifest
- [ ] Re-running produces the same shard assignment
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1580+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T45: Assert projection determinism across runs, paths and input order

**What**: Analyze two copies of the fixture at different absolute paths with shuffled document order and compare every projection artifact byte for byte.
**Where**: `tests/Csharp2Md.Analysis.Tests/Projection/ProjectionDeterminismTests.cs`
**Depends on**: T44
**Reuses**: the two-clone determinism harness from the 5C and 5D suites
**Requirement**: RP-46, RP-47, RP-48

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Two runs at the same path produce identical projection bytes
- [ ] Two clones at different absolute paths produce identical projection bytes
- [ ] Shuffled document order produces identical projection bytes
- [ ] Gate check passes: Build gate command
- [ ] Test count: 1586+ tests pass (no silent deletions)

**Tests**: integration
**Gate**: build

---

### T46: Assert no absolute path in any projection artifact

**What**: Extend the absolute-path check to cover projection fragments, reusing the existing filesystem-root heuristics.
**Where**: `src/Csharp2Md.Storage/Validation/ProjectionValidator.cs`
**Depends on**: T45
**Reuses**: `PackageValidator.EnsureNoAbsolutePaths` and its `UnixFilesystemRoots` set
**Requirement**: RP-49

**Tools**: per the Tooling table above.

**Done when**:
- [ ] A projection containing a Windows or Unix absolute path aborts the publication
- [ ] A test asserts no published projection artifact contains an absolute path for the fixture
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1592+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: full

---

### T47: Assert the assembly reference set

**What**: Prove Projection references only Storage and Domain, Analysis references neither Projection nor Storage, and the CLI declares no direct Domain reference.
**Where**: `tests/Csharp2Md.Projection.Tests/Isolation/ProjectionIsolationTests.cs`
**Depends on**: T46
**Reuses**: the `.csproj` XML inspection pattern from `StorageIsolationTests`
**Requirement**: RP-50

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Projection's reference set is exactly Storage and Domain
- [ ] Analysis declares no reference to Storage, Projection or Cli
- [ ] The CLI declares no direct reference to Domain
- [ ] Gate check passes: Build gate command
- [ ] Test count: 1598+ tests pass (no silent deletions)

**Tests**: unit
**Gate**: build

---

### T48: Assert the empty publication publishes no projection

**What**: Commit an empty snapshot and prove the package holds the manifest and the registry and nothing else.
**Where**: `tests/Csharp2Md.Storage.Tests/Filesystem/EmptyProjectionCommitTests.cs`
**Depends on**: T47
**Reuses**: `FilesystemEmptyCommitTests` as the pattern
**Requirement**: RP-51

**Tools**: per the Tooling table above.

**Done when**:
- [ ] No catalog, posting, Markdown, source or guide artifact is published
- [ ] The manifest lists exactly the artifacts that exist
- [ ] Gate check passes: Full gate command
- [ ] Test count: 1602+ tests pass (no silent deletions)

**Tests**: integration
**Gate**: full

---

### T49: Close the spec-anchored coverage matrix

**What**: Add the AC-to-test trait mapping for every requirement this feature declares, so the Verifier can re-derive coverage independently.
**Where**: `tests/Csharp2Md.Analysis.Tests/Projection/RequirementCoverageTests.cs`
**Depends on**: T48
**Reuses**: the `[Trait("Requirement", ...)]` convention used across all prior workstreams
**Requirement**: RP-01 through RP-57

**Tools**: per the Tooling table above.

**Done when**:
- [ ] Every RP-NN from RP-01 to RP-57 is carried by at least one test trait
- [ ] A test enumerates the traits and fails naming any requirement with no test
- [ ] Every edge case listed in the spec has a named test
- [ ] Gate check passes: Build gate command
- [ ] Test count: 1610+ tests pass (no silent deletions)

**Tests**: integration
**Gate**: build

---

## Phase Execution Map

```
Phase 1:  T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7
Phase 2:  T8 -> T9 -> T10 -> T11 -> T12
Phase 3:  T13 -> T14 -> T15 -> T16 -> T17 -> T18
Phase 4:  T19 -> T20 -> T21 -> T22
Phase 5:  T23 -> T24 -> T25 -> T26 -> T27
Phase 6:  T28 -> T29 -> T30 -> T31 -> T32
Phase 7:  T33 -> T34 -> T35 -> T36 -> T37
Phase 8:  T38 -> T39 -> T40 -> T41 -> T42
Phase 9:  T43 -> T44 -> T45 -> T46 -> T47 -> T48 -> T49
```

Execution is strictly sequential — there is no intra-phase parallelism. A single agent (or batch worker) works one task at a time, in order.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1, T2 | 1 ordering key each, same file | ✅ Granular |
| T3, T4 | 1 type, then its lookups | ✅ Granular |
| T5, T6 | 1 new builder, then 1 removal | ✅ Granular |
| T7 | 1 extracted sequence | ✅ Granular |
| T8–T12 | 1 interface / 1 type change / 1 signature / 1 implementation / 1 wiring | ✅ Granular |
| T13–T18 | 1 port / 1 call site / 2 validator rule pairs / 1 test file / 1 wiring | ✅ Granular |
| T19–T22 | 1 value type / 1 emitter / 1 DTO / 1 test file | ✅ Granular |
| T23–T27 | 1 projector / 1 redactor / 1 envelope / 1 check / 1 test file | ✅ Granular |
| T28–T32 | 1 catalog group each, same file, cohesive | ✅ Granular |
| T33–T37 | 1 posting family each, same file, cohesive | ✅ Granular |
| T38–T42 | 1 page group / 1 boundary test / 2 guides | ✅ Granular |
| T43–T49 | 1 writer / 1 invariance test / 4 cross-cutting suites / 1 coverage map | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | phase head | ✅ Match |
| T2 | T1 | T1 -> T2 | ✅ Match |
| T3 | T2 | T2 -> T3 | ✅ Match |
| T4 | T3 | T3 -> T4 | ✅ Match |
| T5 | T4 | T4 -> T5 | ✅ Match |
| T6 | T5 | T5 -> T6 | ✅ Match |
| T7 | T6 | T6 -> T7 | ✅ Match |
| T8 | None | phase head | ✅ Match |
| T9 | T8 | T8 -> T9 | ✅ Match |
| T10 | T9 | T9 -> T10 | ✅ Match |
| T11 | T10 | T10 -> T11 | ✅ Match |
| T12 | T11 | T11 -> T12 | ✅ Match |
| T13 | None | phase head | ✅ Match |
| T14 | T13 | T13 -> T14 | ✅ Match |
| T15 | T14 | T14 -> T15 | ✅ Match |
| T16 | T15 | T15 -> T16 | ✅ Match |
| T17 | T16 | T16 -> T17 | ✅ Match |
| T18 | T17 | T17 -> T18 | ✅ Match |
| T19 | None | phase head | ✅ Match |
| T20 | T19 | T19 -> T20 | ✅ Match |
| T21 | T20 | T20 -> T21 | ✅ Match |
| T22 | T21 | T21 -> T22 | ✅ Match |
| T23 | None | phase head | ✅ Match |
| T24 | T23 | T23 -> T24 | ✅ Match |
| T25 | T24 | T24 -> T25 | ✅ Match |
| T26 | T25 | T25 -> T26 | ✅ Match |
| T27 | T26 | T26 -> T27 | ✅ Match |
| T28 | None | phase head | ✅ Match |
| T29 | T28 | T28 -> T29 | ✅ Match |
| T30 | T29 | T29 -> T30 | ✅ Match |
| T31 | T30 | T30 -> T31 | ✅ Match |
| T32 | T31 | T31 -> T32 | ✅ Match |
| T33 | None | phase head | ✅ Match |
| T34 | T33 | T33 -> T34 | ✅ Match |
| T35 | T34 | T34 -> T35 | ✅ Match |
| T36 | T35 | T35 -> T36 | ✅ Match |
| T37 | T36 | T36 -> T37 | ✅ Match |
| T38 | None | phase head | ✅ Match |
| T39 | T38 | T38 -> T39 | ✅ Match |
| T40 | T39 | T39 -> T40 | ✅ Match |
| T41 | T40 | T40 -> T41 | ✅ Match |
| T42 | T41 | T41 -> T42 | ✅ Match |
| T43 | None | phase head | ✅ Match |
| T44 | T43 | T43 -> T44 | ✅ Match |
| T45 | T44 | T44 -> T45 | ✅ Match |
| T46 | T45 | T45 -> T46 | ✅ Match |
| T47 | T46 | T46 -> T47 | ✅ Match |
| T48 | T47 | T47 -> T48 | ✅ Match |
| T49 | T48 | T48 -> T49 | ✅ Match |

No task depends on a task in a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1, T2 | Storage wire mapping and ordering | unit | unit | ✅ OK |
| T3, T4, T5, T6 | Storage wire mapping and ordering | unit | unit | ✅ OK |
| T7 | Storage publication pipeline | unit + integration | integration | ✅ OK |
| T8 | Analysis write port | unit | unit | ✅ OK |
| T9 | Analysis write port | unit | unit | ✅ OK |
| T10 | Analysis write port + Storage pipeline | unit + integration | integration | ✅ OK |
| T11 | Analysis readers | unit | unit | ✅ OK |
| T12 | Pipeline / orchestrator | integration | integration | ✅ OK |
| T13 | Storage port + project files | unit | unit | ✅ OK |
| T14 | Storage publication pipeline | unit + integration | integration | ✅ OK |
| T15, T16 | Storage validation | unit + integration | unit | ✅ OK |
| T17 | Storage publication pipeline | unit + integration | integration | ✅ OK |
| T18 | CLI surface | unit | unit | ✅ OK |
| T19 | Domain literals and facts | unit | unit | ✅ OK |
| T20 | Semantics emitters | unit + integration | unit | ✅ OK |
| T21 | Storage wire mapping | unit | unit | ✅ OK |
| T22 | Storage wire mapping | unit | unit | ✅ OK |
| T23–T26 | Projection projectors | unit | unit | ✅ OK |
| T27 | End-to-end fixture analyze | integration | integration | ✅ OK |
| T28–T32 | Projection projectors | unit | unit | ✅ OK |
| T33–T36 | Projection projectors | unit | unit | ✅ OK |
| T37 | End-to-end fixture analyze | integration | integration | ✅ OK |
| T38–T40 | Projection projectors | unit | unit | ✅ OK |
| T41, T42 | Projection projectors | unit | unit | ✅ OK |
| T43, T44 | Projection projectors | unit | unit | ✅ OK |
| T45 | End-to-end fixture analyze | integration | integration | ✅ OK |
| T46 | Storage validation | unit + integration | unit | ✅ OK |
| T47 | Project files (`.csproj` references) | unit | unit | ✅ OK |
| T48 | Storage publication pipeline | unit + integration | integration | ✅ OK |
| T49 | End-to-end fixture analyze | integration | integration | ✅ OK |

No task defers its tests to a later task. Every task that creates code also creates the tests that verify it.

---

## Requirement Coverage

All 57 requirements map to at least one task.

| Requirement | Tasks | Requirement | Tasks |
| --- | --- | --- | --- |
| RP-01 | T13 | RP-30 | T37 |
| RP-02 | T7 | RP-31 | T38, T39 |
| RP-03 | T5, T6, T14 | RP-32 | T38 |
| RP-04 | T17 | RP-33 | T40 |
| RP-05 | T10 | RP-34 | T39 |
| RP-06 | T18 | RP-35 | T40 |
| RP-07 | T8, T11, T12, T23 | RP-36 | T40 |
| RP-08 | T23 | RP-37 | T41 |
| RP-09 | T24 | RP-38 | T42 |
| RP-10 | T25 | RP-39 | T42 |
| RP-11 | T26 | RP-40 | T41 |
| RP-12 | T27 | RP-41 | T15 |
| RP-13 | T19, T21 | RP-42 | T15 |
| RP-14 | T20 | RP-43 | T15 |
| RP-15 | T20 | RP-44 | T16 |
| RP-16 | T20 | RP-45 | T16 |
| RP-17 | T22 | RP-46 | T1, T2, T45 |
| RP-18 | T28, T29, T30 | RP-47 | T45 |
| RP-19 | T3 | RP-48 | T1, T2, T45 |
| RP-20 | T4, T37 | RP-49 | T46 |
| RP-21 | T32 | RP-50 | T47 |
| RP-22 | T32 | RP-51 | T48 |
| RP-23 | T31 | RP-52 | T43 |
| RP-24 | T32 | RP-53 | T43 |
| RP-25 | T33, T35 | RP-54 | T43 |
| RP-26 | T3, T33 | RP-55 | T44 |
| RP-27 | T4, T37 | RP-56 | T44 |
| RP-28 | T34 | RP-57 | T9, T10 |
| RP-29 | T36 | | |
