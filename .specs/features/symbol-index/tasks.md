# SymbolIndex Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and
Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the
full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user - do not proceed without it.**

**User-confirmed deviation from the standard flow:** the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by explicit user request — the user will run it manually
afterward (same standing preference as `relation-collector`). The Verifier's spec-anchored outcome check,
per-AC evidence, and `validation.md` report still run as normal; only the injected-fault/mutation-kill sub-step
is omitted.

---

**Design**: `.specs/features/symbol-index/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase sampling (`tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs`,
> `Analysis/Semantics/SymbolFactEnricherTests.cs`, `Analysis/Indexes/SolutionAnalysisIndexTests.cs`,
> `Facts/Validation/FactValidatorTests.cs`, `Facts/Serialization/FactualJsonTests.cs`/`FactualSchemaSyncTests.cs`,
> `Analysis/AnalysisEngineTests.cs`, `Analysis/RelationCollectorEndToEndTests.cs`) and project guidelines.
> Guidelines found: `AGENTS.md`/`CLAUDE.md` (routes testing quality to `dotnet-test:*` skills as post-hoc gates,
> not a coverage-threshold config). No coverage-threshold tool config found; strong defaults applied for the
> Coverage Expectation column.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| `TypeNameNormalizer` (pure utility) | unit | All 16 predefined-type aliases + `global::` handling + idempotency; 1:1 to SYMIDX-08 | `tests/Csharp2Md.Core.Tests/Analysis/Semantics/TypeNameNormalizerTests.cs` (new) | `dotnet test csharp2md.slnx` |
| `SymbolFact` extension + syntax-only population (`SyntaxFactExtractor`) | unit | All branches; 1:1 to SYMIDX-01/06/09/10/12; namespace/nested-type/generic-arity/parameter-type edge cases from spec.md's Edge Cases | `tests/Csharp2Md.Core.Tests/Analysis/Syntax/SyntaxFactExtractorTests.cs` | `dotnet test csharp2md.slnx` |
| `FactValidator` (`ContainingSymbolId` reference check) | unit | Dangling-reference case + normal same-document resolution case | `tests/Csharp2Md.Core.Tests/Facts/Validation/FactValidatorTests.cs` | `dotnet test csharp2md.slnx` |
| JSON contract / schema (`SymbolFactJson`, `facts.schema.json`, `SchemaVersion`) | unit | Round-trip serialize/deserialize of every new field; schema sync assertion updated to 3; every existing approved snapshot containing `symbols` re-approved | `tests/Csharp2Md.Core.Tests/Facts/Serialization/FactualJsonTests.cs`, `FactualSchemaSyncTests.cs`, plus snapshot files under `tests/**/snapshots/*.verified.*` | `dotnet test csharp2md.slnx` |
| Semantic population (`SymbolFactEnricher`) | unit | All branches; 1:1 to SYMIDX-01 (Exact-level fields); reuses `CanonicalSignature`'s already-computed locals | `tests/Csharp2Md.Core.Tests/Analysis/Semantics/SymbolFactEnricherTests.cs` | `dotnet test csharp2md.slnx` |
| `SymbolIndex`/`SymbolIndexBuilder` core (`GetById`/`FindByName`/`FindByQualifiedName`/`FindMembers`) | unit | All branches; 1:1 to SYMIDX-02/03/04/05/07/11; every listed Edge Case in spec.md covered | `tests/Csharp2Md.Core.Tests/Analysis/Indexes/SymbolIndexTests.cs` (new) | `dotnet test csharp2md.slnx` |
| `FindMethods`/`MethodLookup` | unit | All branches; 1:1 to SYMIDX-13/14; the real `AuthorizePayment` fixture method proven via a hand-extracted (not full-run) `SyntaxFactExtractor.Extract` call | `tests/Csharp2Md.Core.Tests/Analysis/Indexes/SymbolIndexTests.cs` | `dotnet test csharp2md.slnx` |
| `FindCandidates`/`SymbolLookup`/`SymbolLookupResult` (ambiguity) | unit | All branches; 1:1 to SYMIDX-15/16/17/18; the two-namespace `PaymentService` fixture from spec.md's P2 Independent Test | `tests/Csharp2Md.Core.Tests/Analysis/Indexes/SymbolIndexTests.cs` | `dotnet test csharp2md.slnx` |
| Build-time diagnostics (`duplicated-symbol-id`, `invalid-containing-symbol`, `ambiguous-symbol-lookup`) | unit | All branches; 1:1 to SYMIDX-19/20/21; build never throws in any case | `tests/Csharp2Md.Core.Tests/Analysis/Indexes/SymbolIndexTests.cs` | `dotnet test csharp2md.slnx` |
| `SymbolIndexMetrics`/`IndexedSymbolKind` | unit | 1:1 to SYMIDX-22; every `SyntaxFactExtractor.DeclarationKind` string maps to exactly one `IndexedSymbolKind` (including the `Other` bucket) | `tests/Csharp2Md.Core.Tests/Analysis/Indexes/SymbolIndexTests.cs` | `dotnet test csharp2md.slnx` |
| `AnalysisEngine` wiring (accumulation + `SymbolIndexBuilder.Build` call) | integration | End-to-end against `fixtures/SyntheticSolution`: syntax-only mode and trusted mode both produce a queryable index matching spec.md's P1 Independent Test exactly (`FindByName`/`FindMembers` for `PaymentsService`/`AuthorizePayment`, correct `Resolution` per mode) | `tests/Csharp2Md.Core.Tests/Analysis/SymbolIndexEndToEndTests.cs` (new, mirrors `RelationCollectorEndToEndTests.cs`) | `dotnet test csharp2md.slnx --filter "Category=Integration"` |

## Gate Check Commands

> Generated from the project's established commands (consistent across every prior feature's `STATE.md`
> handoff — no CI workflow file exists in this repo to source them from instead).

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After tasks with unit tests only | `dotnet test csharp2md.slnx` |
| Full | After tasks with integration tests (pipeline wiring, fixture-level) | `dotnet test csharp2md.slnx` (integration tests are `[Trait("Category","Integration")]` in the same suite, no separate command exists in this repo) |
| Build | After phase completion or the schema-version-bump task | `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx` |

---

## Execution Plan

Phases are ordered and run sequentially - each phase completes before the next begins, and tasks within a phase
execute in order.

### Phase 1: Data model foundation

T1 has no dependencies. T2 depends on T1. T3 and T4 depend on T2. T5 depends on T1 and T2. Run in listed order —
see **Phase Execution Map** for the exact edges.

### Phase 2: Core index query surface

T6 depends on Phase 1's T1 and T2 (does not need T4/T5 — its own unit tests hand-build `SymbolFact` instances,
same pattern `SolutionAnalysisIndexTests.cs` already uses).

### Phase 3: Method lookup & candidate ranking

T7 and T8 both depend on Phase 2's T6.

### Phase 4: Diagnostics & metrics

T9 depends on Phase 2's T6. T10 depends on T9.

### Phase 5: Pipeline wiring & real-run proof

T11 depends on Phase 1's T5 and Phase 2's T6.

---

## Task Breakdown

### T1: Add `TypeNameNormalizer`

**What**: New pure static helper that normalizes a type spelling (`global::System.String`, `System.String`,
`string`) to one comparable `global::`-prefixed fully-qualified form, covering all 16 C# predefined-type
keywords (`bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `char`, `float`, `double`,
`decimal`, `string`, `object`, `void`) plus a leading `global::` prefix. Idempotent.
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/TypeNameNormalizer.cs` (new)
**Depends on**: None
**Reuses**: `RelationNoiseFilter`'s shared-static-predicate pattern (`Analysis/Relations/RelationNoiseFilter.cs`) as the structural template — one shared, directly-testable helper, not two divergent inline implementations.
**Requirement**: SYMIDX-08

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [x] `Normalize(string)` maps every one of the 16 predefined-type keywords to its `System.*` metadata name
- [x] A leading `global::` prefix is collapsed/normalized consistently whether or not the input already had one
- [x] `Normalize(Normalize(x)) == Normalize(x)` holds for every tested input (idempotency asserted directly)
- [x] A type spelling that is neither a predefined-type keyword nor `global::`-prefixed passes through unchanged apart from the `global::` prefix being added
- [x] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T2: Extend `SymbolFact` with identity fields and populate them from syntax alone

**What**: Add `Name`, `FullyQualifiedName`, `Namespace`, `ContainingType`, `ContainingSymbolId`, `Signature`,
`Arity`, `ParameterTypes` to the `SymbolFact` record. Populate every one of them in `SyntaxFactExtractor.Extract`
purely from syntax (no `SemanticModel`): `Name` from the existing `DeclarationName`; `Signature` reuses the
existing `DeclarationSignature` raw text unchanged (the "original spelling" AC-06 needs, at zero extra cost);
`Namespace`/`ContainingType` from walking ancestor `BaseNamespaceDeclarationSyntax`/`TypeDeclarationSyntax`
nodes; `ContainingSymbolId` from the existing `ownerByDeclaration` map; `Arity` from `TypeParameterList`/generic
method type-parameter count; `ParameterTypes` from `ParameterSyntax.Type` text run through `TypeNameNormalizer`;
`FullyQualifiedName` from the namespace/containing-type chain run through `TypeNameNormalizer`.
**Where**: `src/Csharp2Md.Core/Facts/Model/Facts.cs` (record extension), `src/Csharp2Md.Core/Analysis/Syntax/SyntaxFactExtractor.cs`
**Depends on**: T1
**Reuses**: `DeclarationName`, `DeclarationSignature`, `ownerByDeclaration` (`SyntaxFactExtractor.cs:44-107,201-224,536-538`) — no new syntax-walking mechanism, only new field population at the existing construction site (`SyntaxFactExtractor.cs:54-62`).
**Requirement**: SYMIDX-01, SYMIDX-06, SYMIDX-09, SYMIDX-10, SYMIDX-12

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`, `dotnet-skills:csharp-nullable-reference-types` (the new nullable fields)

**Done when**:
- [x] `SymbolFact` declares all 8 new fields
- [x] Every `SymbolFact` produced by `SyntaxFactExtractor.Extract` has `Name`/`Signature` populated (never null/empty for a real declaration)
- [x] A nested type's member reports the correct `Namespace` (outer namespace) and `ContainingType` (the nested type's own fully qualified name, not the outer type)
- [x] A generic type/method reports the correct `Arity`
- [x] A method with parameters reports `ParameterTypes` normalized via `TypeNameNormalizer` (e.g. `string` and `System.String` parameters both normalize identically)
- [x] A member declared inside a type reports `ContainingSymbolId` equal to that type's own `SymbolFactId` (reusing `ownerByDeclaration`)
- [x] A top-level declaration (no containing type) has `ContainingSymbolId = null`
- [x] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T3: Validate `ContainingSymbolId` references in `FactValidator`

**What**: Add `ContainingSymbolId` to `FactValidator.GetReferences`'s `SymbolFact` case, symmetric with the
existing `BaseAndInterfaceIds`/`Semantics` reference handling, so a dangling containing-symbol reference is
caught by the existing per-fragment `missing-reference` (`C2M-FV-002`) diagnostic.
**Where**: `src/Csharp2Md.Core/Facts/Validation/FactValidator.cs`
**Depends on**: T2
**Reuses**: The existing `GetReferences` switch pattern (`FactValidator.cs:68-85`) — one more `.Concat`, no new validation mechanism.
**Requirement**: Supports design.md's `SymbolFact` "Relationships" note (`ContainingSymbolId` as a validated self-reference)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] A `SymbolFact` whose `ContainingSymbolId` points to another fact in the same validation input passes validation
- [x] A `SymbolFact` whose `ContainingSymbolId` points to an id absent from the input produces `C2M-FV-002`/`missing-reference`
- [x] A `SymbolFact` with `ContainingSymbolId = null` is unaffected (no new diagnostic)
- [x] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T4: Extend the JSON contract and bump `SchemaVersion` to 3

**What**: Add the 8 new fields to `SymbolFactJson` (`FactualJsonContracts.cs`) and `FactualJsonMapper.Map(SymbolFact)`
(`FactStore.cs`); bump `FactualJsonSerializer.SchemaVersion` from `2` to `3`; update `schemas/facts.schema.json`
(`symbol_fact`'s `required`/`properties`, matching its existing snake_case convention — `name`,
`fully_qualified_name`, `signature`, `arity`, `parameter_types` required; `namespace`, `containing_type`,
`containing_symbol_id` optional, same pattern as the existing `semantics` property) and its `schema_version`
`const` from `2` to `3`; re-approve every existing `.verified.*` snapshot that serializes `SymbolFactJson`
(starting from `FactualJsonTests.Serialize_RepresentativeFactualDocument_MatchesApprovedSpecSnapshot.verified.json`).
**Where**: `src/Csharp2Md.Core/Facts/Serialization/FactualJsonContracts.cs`, `src/Csharp2Md.Core/Facts/Storage/FactStore.cs`, `src/Csharp2Md.Core/Facts/Serialization/FactualJsonSerializer.cs`, `schemas/facts.schema.json`
**Depends on**: T2
**Reuses**: The existing `SymbolFactJson`/`Map(SymbolFact)` shape and the `semantics` optional-property pattern in `facts.schema.json:53` for the three nullable new fields.
**Requirement**: Supports Success Criteria bullet 3 (JSON round-trip + schema version)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `SymbolFactJson` carries all 8 new properties with `JsonPropertyOrder` 9-16
- [x] `FactualJsonMapper.Map(SymbolFact)` maps every new field, including `null` for the three optional ones
- [x] `FactualJsonSerializer.SchemaVersion` is `3`; `FactualSchemaSyncTests`'s schema-version assertion is updated (test renamed to reflect "IsThree", not left asserting the old value under a stale name)
- [x] `schemas/facts.schema.json`'s `symbol_fact` definition and `schema_version` const both reflect the new shape
- [x] Every `.verified.*` snapshot under `tests/**/snapshots/` that includes `"symbols"` is re-approved (diffed and confirmed correct, not blindly accepted) — explicitly enumerated in the commit, not silently left stale
- [x] Gate check passes: `dotnet build csharp2md.slnx -c Release` then `dotnet format csharp2md.slnx --verify-no-changes` then `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: build

---

### T5: Populate the new `SymbolFact` fields semantically in `SymbolFactEnricher`

**What**: In `EnrichSymbol`, populate `Name`, `FullyQualifiedName`, `Namespace`, `ContainingType`,
`ContainingSymbolId`, `Arity`, `ParameterTypes` from the bound `ISymbol` when semantic binding succeeds,
reusing `CanonicalSignature`'s already-computed `container`/`metadata`/`arity`/`type`/`parameters` locals
(currently computed then discarded into the opaque `SymbolFactId` string) and `FindLocalId` (already used for
`overriddenMemberId`/`implementedMemberIds`) for `ContainingSymbolId`. Run `FullyQualifiedName`/`ParameterTypes`
through `TypeNameNormalizer` — `DisplayType`'s default `SymbolDisplayFormat.FullyQualifiedFormat` renders
predefined types as their C# keyword (`"int"`, not `"System.Int32"`), so normalization is still required here,
not only in the syntax-only path. Leave `Signature` as the syntax-only value from T2 (already the "original
spelling"; semantic binding does not change it).
**Where**: `src/Csharp2Md.Core/Analysis/Semantics/SymbolFactEnricher.cs`
**Depends on**: T1, T2
**Reuses**: `CanonicalSignature`'s locals (`SymbolFactEnricher.cs:220-247`), `DisplayType`/`DisplayContainer`/`DisplayNamedType` (`SymbolFactEnricher.cs:256-299`), `FindLocalId` (`SymbolFactEnricher.cs:343-359`) — this task adds zero new Roslyn traversal, only redirects already-computed values onto the new fields.
**Requirement**: SYMIDX-01 (Exact-level population)

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-coding-standards`

**Done when**:
- [x] A symbol with successful semantic binding reports `Resolution = Exact` and `Namespace`/`ContainingType`/`FullyQualifiedName`/`ParameterTypes` matching the Roslyn `ISymbol`'s real, normalized shape (not the syntax-only guess from T2)
- [x] A symbol whose semantic binding contains an error symbol keeps its syntax-only field values (consistent with how `Attributes`/`RelevantTypeReferences` already behave in this method's `containsError` branch)
- [x] `ParameterTypes` for a predefined-type parameter (e.g. `int x`) normalizes to `global::System.Int32`, not the Roslyn-default `"int"` — proving `TypeNameNormalizer` is actually applied here, not assumed already-normalized
- [x] `ContainingSymbolId` for a member resolves to its containing type's real resolved `SymbolFactId` via `FindLocalId`, falling back to the syntax-only value when the containing type wasn't part of this enrichment batch
- [x] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T6: `SymbolIndex`/`SymbolIndexBuilder` core query surface

**What**: New `SymbolIndex` class with `GetById`, `FindByName`, `FindByQualifiedName`, `FindMembers`, backed by
`FrozenDictionary`s keyed by `SymbolFactId`, simple name (ordinal), normalized qualified name, and
`(NormalizedContainingType, memberName)`. `SymbolIndexBuilder.Build(IEnumerable<SymbolFact>, IEnumerable<ProjectFact>, IEnumerable<DocumentFact>, IEnumerable<TargetFact>)`
is the pure, order-independent construction entry point.
**Where**: `src/Csharp2Md.Core/Analysis/Indexes/SymbolIndex.cs` (new)
**Depends on**: T1, T2
**Reuses**: `SolutionAnalysisIndex`'s `FrozenDictionary`/`ToUniqueFrozenDictionary`-style construction idiom (`SolutionAnalysisIndex.cs:130-146`) as the pattern to follow, same folder/namespace `Csharp2Md.Core.Analysis.Indexes`.
**Requirement**: SYMIDX-02, SYMIDX-03, SYMIDX-04, SYMIDX-05, SYMIDX-06, SYMIDX-07, SYMIDX-11

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-type-design-performance` (FrozenDictionary/collection choices)

**Done when**:
- [x] `GetById` returns the exact match via direct dictionary lookup (asserted by construction, not just behavior — no `.Where`/linear scan in the implementation)
- [x] `FindByName` returns every symbol sharing a simple name across projects, ordinal-ordered by `Id`, empty (not null/throw) when nothing matches
- [x] `FindByQualifiedName` matches via `TypeNameNormalizer.Normalize`, so `System.String`/`global::System.String`/`string`-declared symbols under the same qualified name all resolve together
- [x] `FindMembers(containingType, memberName)` returns every member regardless of its own `Resolution`
- [x] Two symbols with the same simple name but different namespace/containing type remain distinct entries (never merged) — SYMIDX-12
- [x] `Build` with zero input facts returns an empty, queryable index, not a throw — SYMIDX-11
- [x] `Build`'s output does not depend on input ordering (same facts, shuffled order, produce identical query results — asserted directly)
- [x] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T7: `FindMethods`/`MethodLookup`

**What**: `MethodLookup` record (`Name`, `ReceiverType`, `Namespace`, `ProjectId`, `ArgumentCount`,
`ArgumentTypes`, `Imports`) and `ISymbolIndex.FindMethods(MethodLookup)`, filtering the containing-type+name
bucket by exact `ParameterTypes.Count` match on `ArgumentCount`, and ranking (not filtering out) candidates by
`ArgumentTypes` match against normalized `ParameterTypes` when both are supplied.
**Where**: `src/Csharp2Md.Core/Analysis/Indexes/SymbolIndex.cs`
**Depends on**: T6
**Reuses**: `FindMembers`'s existing containing-type+name bucket as the starting candidate set.
**Requirement**: SYMIDX-13, SYMIDX-14

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `FindMethods` with `ArgumentCount` set returns only methods whose `ParameterTypes.Count` equals it exactly, including the zero-parameter/zero-argument boundary case
- [x] `FindMethods` with `ArgumentTypes` set returns both an exact-type match and a same-count/different-type candidate, with the exact match ranked first — neither is silently dropped
- [x] Against `fixtures/SyntheticSolution/Acme.Payments/PaymentsService.cs`'s real `AuthorizePayment` method (via a direct `SyntaxFactExtractor.Extract` call, not a full `AnalysisEngine` run), `FindMethods` with the method's real declared argument count returns it, and a mismatched count returns empty
- [x] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T8: `FindCandidates`/`SymbolLookup`/`SymbolLookupResult` with explicit ambiguity

**What**: `SymbolLookup` record (`Name`, `ContainingType`, `Namespace`, `ProjectId`, `Imports`),
`SymbolLookupResult` (`Status`, `Candidates`) with `SymbolLookupStatus { Unique, Ambiguous, NotFound }`, and
`ISymbolIndex.FindCandidates(SymbolLookup)` ordering by the priority sequence (id exact → qualified name → same
containing type → same namespace → namespace in `Imports` → same project → global simple name) without
collapsing a tie within one tier to a single winner.
**Where**: `src/Csharp2Md.Core/Analysis/Indexes/SymbolIndex.cs`
**Depends on**: T6
**Reuses**: `FindByName`'s existing simple-name bucket as the base candidate pool before tier-ranking.
**Requirement**: SYMIDX-15, SYMIDX-16, SYMIDX-17, SYMIDX-18

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] A two-project fixture with `Company.Legacy.PaymentService` and `Company.Payments.PaymentService` (spec.md's own P2 Independent Test example) makes `FindCandidates` (and `FindByName`) report `Status = Ambiguous` with exactly those two candidates
- [x] The same fixture's `FindByQualifiedName("Company.Payments.PaymentService")` returns exactly one, unambiguous result — proving qualified-name lookup bypasses the simple-name ambiguity entirely
- [x] A lookup matching exactly one candidate at any priority tier reports `Status = Unique`, never `Ambiguous`
- [x] No lookup method ever returns a single arbitrarily-chosen symbol when the underlying data has more than one equally-ranked candidate (asserted directly against a constructed tie, not inferred from the ambiguous-fixture test alone)
- [x] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T9: Build-time diagnostics (`duplicated-symbol-id`, `invalid-containing-symbol`, `ambiguous-symbol-lookup`)

**What**: `SymbolIndexBuilder.Build` records `AnalysisDiagnostic`s (`C2M-SYMIDX-001`/`002`/`003`, `DiagnosticStage`
appropriate to index construction) when two `SymbolFact`s collide on `SymbolFactId.Value` (keeping the
ordinal-first entry, never throwing), when a `ContainingSymbolId` references an absent id, and — scanned once at
build-completion time, not per-query — for every simple name whose candidates span more than one
namespace/containing type.
**Where**: `src/Csharp2Md.Core/Analysis/Indexes/SymbolIndex.cs`
**Depends on**: T6
**Reuses**: `AnalysisDiagnostic.Create` / `DiagnosticSeverity` / `DiagnosticStage` (`Facts/Metadata/AnalysisDiagnostic.cs`), same construction pattern `SymbolFactEnricher.Diagnostic` and `FactValidator.Error` already use.
**Requirement**: SYMIDX-19, SYMIDX-20, SYMIDX-21

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Two hand-built `SymbolFact`s sharing one `SymbolFactId` produce a `duplicated-symbol-id` diagnostic, `Build` completes without throwing, and exactly one of the two (the ordinal-first) is queryable afterward
- [x] A `ContainingSymbolId` pointing at an absent id produces an `invalid-containing-symbol` diagnostic, and the symbol is still indexed under its own id
- [x] A simple name with candidates in two different namespaces produces one `ambiguous-symbol-lookup` diagnostic at build time, without requiring a caller to have queried that name
- [x] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T10: `SymbolIndexMetrics` and `IndexedSymbolKind`

**What**: `IndexedSymbolKind` enum (`Namespace, Class, Struct, RecordClass, RecordStruct, Interface, Enum,
Delegate, Constructor, Method, Property, Field, Event, Other`) with a pure mapping function from
`SymbolFact.SymbolKind`'s existing string values (covering all 16 values `SyntaxFactExtractor.DeclarationKind`
already produces, including the 5 not named in spec section 5 — `destructor`, `indexer`, `operator`,
`conversion-operator`, `enum-member`, `member` — mapped to `Other`, never dropped). `SymbolIndexMetrics`
(`TotalSymbols`, `ByResolution`, `ByKind`, `DuplicateIdCount`, `AmbiguousSimpleNameCount`) computed once at the
end of `Build`, exposed as `SymbolIndex.Metrics`.
**Where**: `src/Csharp2Md.Core/Analysis/Indexes/SymbolIndex.cs`
**Depends on**: T9
**Reuses**: `FactResolution` (unchanged, reused as-is per design.md's Tech Decision) for `ByResolution`; T9's diagnostic counts for `DuplicateIdCount`/`AmbiguousSimpleNameCount`.
**Requirement**: SYMIDX-22

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Every one of `SyntaxFactExtractor.DeclarationKind`'s 16 possible string values maps to exactly one `IndexedSymbolKind` (asserted per value, not spot-checked) — the 5 unnamed-in-spec kinds map to `Other`
- [ ] `Metrics.TotalSymbols` equals the indexed symbol count exactly (including duplicates collapsed per T9's rule)
- [ ] `Metrics.ByResolution`/`ByKind` sum to `TotalSymbols`
- [ ] `Metrics.DuplicateIdCount`/`AmbiguousSimpleNameCount` match T9's recorded diagnostic counts exactly, on the same fixture used in T9's tests
- [ ] Gate check passes: `dotnet test csharp2md.slnx`

**Tests**: unit
**Gate**: quick

---

### T11: Wire `SymbolIndexBuilder` into `AnalysisEngine.AnalyzeAsync`

**What**: Add a new `internal` constructor parameter `Action<SymbolIndex>? onSymbolIndexBuilt = null` (mirroring
the existing `FragmentValidationFunc`/`IAnalysisEngineObserver` test-seam pattern — no `AnalysisResult` change).
Accumulate every `SymbolFact`/`ProjectFact`/`DocumentFact`/`TargetFact` produced during the run in new builders
alongside the existing `coverageFacts` accumulator, appended at the same points `coverageFacts.Add(...)` already
fires. After the main double loop completes, call `SymbolIndexBuilder.Build(...)` once and invoke
`onSymbolIndexBuilt?.Invoke(index)`.
**Where**: `src/Csharp2Md.Core/Analysis/AnalysisEngine.cs`
**Depends on**: T5, T6
**Reuses**: The `coverageFacts` accumulation pattern and points (`AnalysisEngine.cs:105,282,316`, plus one new append where `merged.Facts`'s symbols are known, ~line 253-256); the `FragmentValidationFunc`/`IAnalysisEngineObserver` internal-constructor injection pattern (`AnalysisEngine.cs:21-27,51-58`).
**Requirement**: SYMIDX-23, spec.md Goal #3 (real-run proof), P1 Independent Test

**Tools**:
- MCP: NONE
- Skill: `dotnet-skills:csharp-concurrency-patterns` (confirm the accumulation stays safe under this method's existing sequential-await structure — no new concurrency introduced, but worth a pattern check)

**Done when**:
- [ ] Running `AnalysisEngine.AnalyzeAsync` against `fixtures/SyntheticSolution` in default (syntax-only) mode, captured via `onSymbolIndexBuilt`, gives a `SymbolIndex` where `FindByName("PaymentsService")` finds the real type and `FindMembers("PaymentsService", "AuthorizePayment")` finds the real method, both `Resolution = Syntactic`
- [ ] The same run in trusted-solution mode gives `Resolution = Exact` for both lookups
- [ ] Public `AnalysisResult`'s shape is unchanged (no new public property) — confirmed by reading the diff, not just by tests passing
- [ ] Gate check passes: `dotnet test csharp2md.slnx --filter "Category=Integration"` then the full suite `dotnet test csharp2md.slnx`

**Tests**: integration
**Gate**: full

---

## Phase Execution Map

Visual representation of task ordering. Phases run in sequence, and tasks within a phase run in order:

```
T1 -> T2
T2 -> T3
T2 -> T4
T1 -> T5
T2 -> T5
T1 -> T6
T2 -> T6
T6 -> T7
T6 -> T8
T6 -> T9
T9 -> T10
T5 -> T11
T6 -> T11
```

Phase 1 = T1, T2, T3, T4, T5. Phase 2 = T6. Phase 3 = T7, T8. Phase 4 = T9, T10. Phase 5 = T11.

Execution is strictly sequential - there is no intra-phase parallelism. A single agent (or batch worker) works
one task at a time, in order: T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11.

**How phase-based execution works:** see `implement.md`/`sub-agents.md` — 11 tasks packs into more than one
task-budgeted batch (~7/batch), so the orchestrator offers batch sub-agents before Execute begins (e.g.
Phase 1+2 as batch 1 (T1-T6), Phase 3+4+5 as batch 2 (T7-T11), or any other whole-phase split the offer settles
on).

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: `TypeNameNormalizer` | 1 new file, 1 function | ✅ Granular |
| T2: `SymbolFact` extension + syntax population | 1 record extension + 1 producer file | ✅ Granular (record shape and its sole existing construction site are one coherent, non-splittable change — splitting would leave the record with fields no producer populates, an inconsistent intermediate state) |
| T3: `FactValidator` reference check | 1 function (one switch case) | ✅ Granular |
| T4: JSON contract + schema bump | 1 contract record + 1 mapper function + 1 schema file + version bump | ✅ Granular (one cohesive "the wire shape changed" change; splitting the schema file from the C# contract would leave them out of sync mid-task) |
| T5: `SymbolFactEnricher` semantic population | 1 function | ✅ Granular |
| T6: `SymbolIndex` core (4 query methods + `Build`) | 1 new file, 1 component | ✅ Granular (one cohesive class; `Build` and its 4 read methods are inseparable — none is independently useful) |
| T7: `FindMethods`/`MethodLookup` | 1 method + 1 record, same file as T6 | ✅ Granular |
| T8: `FindCandidates`/`SymbolLookup` | 1 method + 2 records, same file as T6 | ✅ Granular |
| T9: Build-time diagnostics | 1 method (3 diagnostic codes, same mechanism) | ✅ Granular |
| T10: Metrics + `IndexedSymbolKind` | 1 enum + 1 record + 1 computation | ✅ Granular |
| T11: `AnalysisEngine` wiring | 1 file, 1 integration point | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | (root) | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T2 | T2 → T4 | ✅ Match |
| T5 | T1, T2 | T1 → T5, T2 → T5 | ✅ Match |
| T6 | T1, T2 | T1 → T6, T2 → T6 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T6 | T6 → T8 | ✅ Match |
| T9 | T6 | T6 → T9 | ✅ Match |
| T10 | T9 | T9 → T10 | ✅ Match |
| T11 | T5, T6 | T5 → T11, T6 → T11 | ✅ Match |

No task depends on a task in a later phase.

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1: `TypeNameNormalizer` | `TypeNameNormalizer` (pure utility) | unit | unit | ✅ OK |
| T2: `SymbolFact` + syntax population | `SymbolFact` extension + syntax-only population | unit | unit | ✅ OK |
| T3: `FactValidator` reference check | `FactValidator` (`ContainingSymbolId` check) | unit | unit | ✅ OK |
| T4: JSON contract + schema bump | JSON contract / schema | unit (+ build gate) | unit | ✅ OK |
| T5: `SymbolFactEnricher` population | Semantic population | unit | unit | ✅ OK |
| T6: `SymbolIndex` core | `SymbolIndex`/`SymbolIndexBuilder` core | unit | unit | ✅ OK |
| T7: `FindMethods` | `FindMethods`/`MethodLookup` | unit | unit | ✅ OK |
| T8: `FindCandidates` | `FindCandidates`/ambiguity | unit | unit | ✅ OK |
| T9: Build-time diagnostics | Diagnostics | unit | unit | ✅ OK |
| T10: Metrics | `SymbolIndexMetrics`/`IndexedSymbolKind` | unit | unit | ✅ OK |
| T11: `AnalysisEngine` wiring | Pipeline wiring | integration | integration | ✅ OK |

No violations. `Tests: none` is not used by any task, so the "matches matrix `none`" rule is not exercised here.
