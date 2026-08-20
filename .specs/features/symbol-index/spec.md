# SymbolIndex Specification

## Problem Statement

The generator already discovers most declared symbols in a solution — syntactically always, semantically when
trusted-solution mode binds cleanly (AD-012) — but nothing lets a later stage ask a *global* question about
them: "does a type named `PaymentsClient` exist anywhere in this solution?", "does it have a method named
`AuthorizePayment`?", "which project owns it?", "are there multiple candidates?". Today's only aggregate lookup
structure, `SolutionAnalysisIndex`, indexes projects/targets/relations for component classification and is never
queried by name or signature; there is no name-indexed, cross-project view of symbols at all. `RelationCollector`
(shipped feature, AD-015) already produces `target_text` on every relation it emits and explicitly defers turning
that text into a proven `target_id` to "a future RelationResolver" — but no structure exists yet that a resolver
could query to attempt that binding. `SymbolIndex` is that missing structure: a persistent, queryable
representation of every symbol the generator has discovered, regardless of whether Roslyn's semantic binding for
that symbol succeeded, partially succeeded, or produced an `IErrorTypeSymbol`.

## Goals

- [ ] Every symbol the generator discovers — semantically resolved or syntax-only, in any project of the
      solution — is queryable through one component (`ISymbolIndex`) by exact id, simple name, qualified name,
      and containing-type + member name, in near-constant time (no full-solution scan per query).
- [ ] A query that matches more than one distinct symbol never silently picks a winner: the index reports every
      candidate and lets the caller (a future consumer) decide, or reports the match unambiguous when there is
      exactly one.
- [ ] The index is built and queryable from a real `AnalysisEngine.AnalyzeAsync` run against
      `fixtures/SyntheticSolution` — not only from hand-built unit-test fixtures — so this feature does not repeat
      the fate `SolutionAnalysisIndex`/`DetectorHost` had before AD-015: built, tested in isolation, and never
      actually invoked by the live pipeline.
- [ ] Building the index never throws on inconsistent input (a duplicate id, a dangling containing-symbol
      reference); it degrades to a diagnostic and keeps building, matching the fact-pipeline's existing
      diagnostics-never-abort-the-build discipline (`FactValidator`).

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| `RelationResolver` and wiring `SymbolIndex` into `RelationCollector`'s unresolved `RelationFact.TargetId` | User confirmed this feature ships a standalone, directly-testable query component; a future `RelationResolver` feature consumes it. Mirrors how `relation-collector` shipped without wiring the pre-existing `Detection`/`DetectorHost` abstraction (AD-015). |
| Persisting `facts/symbol-index.json` / `facts/symbols.jsonl` | Section 16 of the user's own request already marks this optional for v1 ("pode existir apenas em memória"). Combined with the `SymbolFact` schema extension already in scope (see Assumptions), a second `facts.json` schema/version change in the same feature is too much surface for one pass. |
| `parameter` as a standalone first-class `IndexedSymbol` entity | `ParameterSyntax` is not currently walked as a declaration site (`SyntaxFactExtractor` only walks `MemberDeclarationSyntax`, which excludes parameters); a parameter's *type* is still indexed as part of its owning method's `ParameterTypes`, which is what every lookup in this spec (`FindMethods`, `MethodLookup`) actually needs. Standalone parameter symbols are listed by the user's own section 5 as later-expandable ("pode ser expandido posteriormente"). |
| Metadata / BCL / NuGet-referenced symbols (e.g. `System.Object`, a NuGet package's public types) | The generator never mints a `SymbolFactId` for a symbol it did not itself declare in the analyzed solution's source; `SymbolIndex` indexes only symbols the generator discovers, consistent with what `SymbolFact` already covers today. |
| Domain inference, Knowledge Graph, configuration resolution, architectural classification, impact analysis, embeddings, semantic search, graph-database persistence | Verbatim from the user's own section 21 ("fora do escopo inicial"). |
| Creating or mutating relations, classifying components, modifying source code | Verbatim from the user's own section 3 ("não deve"). |
| Discrimination-sensor / mutation validation (Stryker) at the Verifier step | User will run it manually afterward; the automated Verifier's mutation-testing sub-step is skipped for this feature by explicit request (same standing preference as `relation-collector`). |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here - nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| `IndexedSymbol` data source | Extend the existing `SymbolFact` (and its JSON contract / schema version) with `Name`, `FullyQualifiedName`, `Namespace`, `ContainingType`, `ContainingSymbolId`, `Signature`, `Arity`, `ParameterTypes` (normalized) rather than building a second, parallel symbol model | User's explicit choice — keeps one factual authority for symbol data per AD-008/AD-009, and `SymbolFactEnricher` already computes most of these display strings transiently today, it just discards them | y |
| Consumer wiring | Standalone `ISymbolIndex` / `SymbolIndexBuilder`, proven by direct tests against this spec's acceptance criteria; no `RelationResolver`, no change to `RelationFact.TargetId` | User's explicit choice | y |
| `IndexedSymbol.Id` identity scheme | Reuse the existing `SymbolFactId.Value` verbatim (`SymbolFactId.CreateResolved` / `CreateSyntactic` / `CreateFallback`, AD-014's grammar) rather than inventing a second identity scheme, even though the user's own sketch (section 6-7) proposes a fresh one | The existing `SymbolFactId` already satisfies section 7's exact requirement — semantic identity when available, stable syntactic identity otherwise, never collapsed to simple-name equality — so a second scheme would just duplicate it with a different string shape | n — facts-you-look-up, not a product decision |
| Actual construction point | `SymbolIndexBuilder.Build` runs as part of `AnalysisEngine.AnalyzeAsync`'s aggregate stage (after every project/document fragment has been produced, per AD-008's PASS-1-after-everything discipline), consuming the same `SymbolFact`/`ProjectFact`/`DocumentFact`/`TargetFact` set already available at that point — not merely callable in isolation from a test | Directly grounded in AD-015's documented failure mode in this exact codebase: `SolutionAnalysisIndex`/`DetectorHost` were built, unit-tested, and never invoked by the real pipeline, confirmed only by grep; this feature's third Goal exists specifically to avoid repeating that | n — engineering default, invite override |
| `FindMethods` argument-count filtering and `params`/optional parameters | `ArgumentCount` filtering compares against the exact `ParameterTypes.Count`; a method with optional or `params` parameters that would still be callable with fewer arguments may be filtered out | Neither the user's `IndexedSymbol` nor `MethodLookup` sketch carries per-parameter optionality/`params` data, and inventing that field is new scope beyond what was asked; documented as a known P2 limitation rather than silently wrong | n — logged limitation, not asked |
| Normalization table scope | The C# built-in alias table from section 11 (`bool`/`byte`/`int`/`long`/`decimal`/`string`/`object`, extended to the full predefined-type set: `sbyte`, `short`, `ushort`, `uint`, `ulong`, `char`, `float`, `double`, `void`) plus a `global::` **prefix-insensitive** comparison; no user-defined `using` **alias** resolution (`using Money = System.Decimal;`) — that requires resolving the alias directive per-document, which no current fact captures | Matches the user's literal list plus the rest of the predefined-type keywords the same C# rule applies to; user-defined aliases are a materially different, per-document problem not in the user's original list | n — scoped default, invite override |
| `SchemaVersion` bump for the extended `SymbolFact` | `FactualJsonSerializer.SchemaVersion` moves from `2` to `3`; every approved JSON/Verify snapshot that includes a `SymbolFactJson` is re-approved as part of this feature's tasks (not silently left stale) | AD-010 already treats `facts/` as versioned; extending a fact's JSON contract is exactly the kind of change that versioning exists for | n — mechanical consequence of the chosen data-source default above |

**Open questions:** none - all resolved or logged above (spec confirmed by the user's explicit answers to the two forked decisions; the rest are grounded, invite-override defaults per Specify's "facts you look up" rule).

---

## User Stories

### P1: Every discovered symbol is findable by identity, name, and location ⭐ MVP

**User Story**: As a future `RelationResolver` (or any other consumer inside the Analysis module), I want to look
up any symbol the generator has already discovered — by its exact id, by simple name, by fully qualified name, or
by containing-type + member name — across every project in the solution, so that I don't have to re-scan
`SymbolFact`s myself or guess whether a name exists at all.

**Why P1**: This is the baseline capability everything else in the spec depends on — without it there is no
index, only a list of facts.

**Acceptance Criteria**:

1. The system SHALL build exactly one `IndexedSymbol` entry, keyed by the symbol's existing `SymbolFactId.Value`,
   for every `SymbolFact` produced by a run — one entry per symbol regardless of trust mode (syntax-only or
   trusted-semantic).
2. WHEN `SymbolIndexBuilder.Build` receives the complete set of `SymbolFact`, `ProjectFact`, `DocumentFact`, and
   `TargetFact` values produced by a finished `AnalysisEngine.AnalyzeAsync` run THEN the system SHALL construct a
   `SymbolIndex` whose query results do not depend on the order those facts were supplied in.
3. WHEN `GetById(id)` is called with a `SymbolFactId` value present in the index THEN the system SHALL return
   that exact `IndexedSymbol` via a direct key lookup (a `FrozenDictionary`-class structure), not a linear scan
   of all symbols.
4. WHEN `FindByName(simpleName)` is called THEN the system SHALL return every indexed type or member whose `Name`
   equals `simpleName` across every project, ordered deterministically by `Id` (ordinal), including zero results
   as a valid, non-throwing outcome.
5. WHEN `FindByQualifiedName(fullyQualifiedName)` is called THEN the system SHALL match using each symbol's
   normalized comparable qualified-name form (criterion 8), not its raw source-text spelling, so a query for
   `System.String` matches a symbol whose source used `global::System.String` or `string`.
6. The system SHALL preserve each symbol's original, non-normalized source-text type spelling alongside its
   normalized comparable form — normalization SHALL NOT discard the original for evidence/debug purposes.
7. WHEN `FindMembers(containingType, memberName)` is called THEN the system SHALL return every indexed member
   whose `ContainingType` matches `containingType` and whose `Name` equals `memberName`, regardless of that
   member's own `Resolution` level.
8. The system SHALL normalize the C# predefined-type aliases (`bool`, `byte`, `sbyte`, `short`, `ushort`, `int`,
   `uint`, `long`, `ulong`, `char`, `float`, `double`, `decimal`, `string`, `object`, `void`) and a leading
   `global::` prefix to one comparable fully-qualified form before indexing by qualified name, containing type,
   or parameter type.
9. WHEN a document's `SemanticModel` does not bind a declaration (syntax-only mode, or trusted mode returning an
   error/candidate symbol) THEN the system SHALL still index that declaration under its syntactic identity
   (`SymbolFactId.CreateSyntactic`) with `Resolution` set to `Syntactic` or `Unresolved` — never `Exact`.
10. The system SHALL populate `Name`, `Namespace`, and `ContainingType` for every indexed type/member purely from
    syntax when no `SemanticModel` is available, without requiring trusted-solution mode (AD-012's default,
    syntax-only, untrusted).
11. IF the solution being indexed has zero documents/symbols THEN `SymbolIndexBuilder.Build` SHALL return an
    empty, queryable `SymbolIndex` rather than throwing.
12. The system SHALL treat two symbols with the same simple `Name` but different `Namespace`/`ContainingType` as
    distinct entries — never merged or deduplicated by simple name alone (the user's own section 7 rule).

**Independent Test**: Run `AnalysisEngine.AnalyzeAsync` against `fixtures/SyntheticSolution` in the default
(syntax-only) mode, take the resulting `SymbolFact`s, and build a `SymbolIndex` from them. Confirm
`FindByName("PaymentsService")` returns the type declared in `Acme.Payments/PaymentsService.cs`, and
`FindMembers("PaymentsService", "AuthorizePayment")` returns that method — both present with `Resolution =
Syntactic`, `Namespace`/`ContainingType` populated correctly, and no `SemanticModel` involved. Re-run the same
assertions in trusted-solution mode and confirm the same two lookups now report `Resolution = Exact`.

---

### P2: Method lookup and contextual candidate ranking with explicit ambiguity

**User Story**: As a future `RelationResolver`, I want to look up a method by containing type, name, and argument
shape, and — when a simple-name query matches more than one unrelated symbol — be told explicitly that the match
is ambiguous instead of receiving one arbitrarily chosen candidate, so that I never silently bind a relation to
the wrong symbol.

**Why P2**: Builds on P1's data; this is the part of the spec the "existem múltiplos candidatos?" /
"qual candidato está mais próximo?" problem statement questions are actually about.

**Acceptance Criteria**:

1. WHEN `FindMethods(lookup)` is called with `lookup.ArgumentCount` set THEN the system SHALL return only methods
   whose `ParameterTypes.Count` equals that value (exact match; see Assumptions for the `params`/optional-
   parameter limitation).
2. WHEN `FindMethods(lookup)` additionally sets `ArgumentTypes` THEN the system SHALL prefer candidates whose
   `ParameterTypes` match those argument types (using the normalized form from P1 criterion 8) over candidates
   with the same count but unmatched types, without discarding the unmatched-type candidates from the result —
   both remain visible to the caller, ranked, not filtered to one.
3. WHEN `FindCandidates(lookup)` is called with `SymbolLookup` contextual hints (a `ContainingType`, `Namespace`,
   `ProjectId`, or `Imports` list) THEN the system SHALL order the returned candidates by the priority sequence:
   exact id, then fully qualified name, then same containing type, then same namespace, then a namespace present
   in `Imports`, then same project, then any project (global simple name) — without collapsing ties within one
   priority tier to a single result.
4. WHEN a `FindByName` or `FindCandidates` lookup matches indexed symbols belonging to more than one distinct
   namespace or containing type at the same priority tier THEN the system SHALL report the result as ambiguous
   (`SymbolLookupResult.Status = Ambiguous`) with every tied candidate listed, rather than returning a single
   symbol.
5. The system SHALL NOT choose an arbitrary winner among equally-ranked candidates in any lookup method — a tie
   always surfaces as `Ambiguous`, never resolved by declaration order, first-match, or any other implicit
   tie-break.
6. WHEN exactly one candidate matches a lookup (at any priority tier) THEN the system SHALL report
   `SymbolLookupResult.Status = Unique` (or equivalent) with that one candidate — ambiguity reporting SHALL NOT
   fire for genuinely unique matches.

**Independent Test**: Build a two-project fixture where `Company.Legacy.PaymentService` and
`Company.Payments.PaymentService` both exist (mirroring the user's own section 13 example). Confirm
`FindByName("PaymentService")` reports `Ambiguous` with exactly those two candidates, and that
`FindByQualifiedName("Company.Payments.PaymentService")` for the same fixture returns exactly one, unambiguous
result. Separately, against `fixtures/SyntheticSolution`, confirm `FindMethods(new MethodLookup { Name =
"AuthorizePayment", ReceiverType = "PaymentsService", ArgumentCount = <n> })` returns the real method with
`n` matching its actual declared parameter count, and an empty result for a mismatched count.

---

### P3: Build-time diagnostics and index metrics

**User Story**: As the person operating csharp2md, I want the index build to tell me when it found inconsistent
symbol data (a duplicate id, a dangling reference) and to report summary counts, so I can trust what the index
contains without it ever failing the run.

**Why P3**: Observability on top of an already-correct index; valuable but not required to answer the P1/P2
lookup questions the feature exists for.

**Acceptance Criteria**:

1. WHEN `SymbolIndexBuilder.Build` encounters two `SymbolFact`s that would produce the same `SymbolFactId.Value`
   THEN the system SHALL record a `duplicated-symbol-id` diagnostic, keep exactly one of the two entries in the
   index (deterministically, by `Id` ordinal), and continue building — it SHALL NOT throw.
2. WHEN an indexed symbol's `ContainingSymbolId` references an id that is absent from the indexed set THEN the
   system SHALL record an `invalid-containing-symbol` diagnostic and still index the symbol under its own id.
3. WHEN a lookup by simple name across the whole index matches candidates from more than one namespace THEN the
   system SHALL record an `ambiguous-symbol-lookup` diagnostic at build-completion time for every simple name
   that would produce this outcome (not only when a caller happens to query it), so ambiguity is visible without
   having to probe every name.
4. The system SHALL expose, after `Build` completes, counts for: total symbols, symbols per `Resolution` level
   (exact/syntactic/candidate/unresolved), symbols per `IndexedSymbolKind` (class/method/interface/etc.),
   duplicate-id occurrences, and simple names with more than one namespace/containing-type candidate.
5. WHILE running in the default syntax-only mode (no trusted-solution opt-in) the system SHALL still build a
   fully queryable `SymbolIndex` for the whole solution — index construction has no trust-mode precondition.

**Independent Test**: Feed `SymbolIndexBuilder.Build` a hand-built set of `SymbolFact`s containing one deliberate
id collision and one dangling `ContainingSymbolId`; confirm both diagnostics are recorded, the build completes
without throwing, and the returned metrics report the expected total/duplicate/ambiguous counts.

---

## Edge Cases

- IF `FindByQualifiedName` is called with a name that matches no indexed symbol THEN the system SHALL return an
  empty result (not `null`, not an exception) — "not found" and "ambiguous" SHALL remain distinguishable outcomes.
- IF two different projects each declare a type with the identical namespace and simple name (a true duplicate
  declaration, not merely the same simple name in different namespaces) THEN `FindByQualifiedName` SHALL return
  both as distinct candidates (they carry different `SymbolFactId`s because `SymbolFactId` incorporates the
  owning `TargetFactId`/project) rather than merging them into one.
- WHEN a `class`/`struct`/`interface` is declared `partial` across multiple documents THEN the system SHALL index
  each partial declaration under its own existing `SymbolFactId` exactly as `SymbolFact` already does today —
  this feature does not introduce partial-declaration merging into one `IndexedSymbol`, and does not regress
  whatever `SymbolFact` already does for partials.
- IF a method declares zero parameters and a `FindMethods` lookup sets `ArgumentCount = 0` THEN the system SHALL
  match it (the boundary case, not an accidental "unset filter" no-op).
- IF a `SymbolFact`'s `ContainsErrorSymbol` is `true` THEN the system SHALL still index it (per the feature's own
  purpose — indexing symbols that failed full semantic binding), with `Resolution` reflecting the fact's own
  resolution level, never upgraded to `Exact` by the index.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| SYMIDX-01 | P1 (one entry per symbol) | Design | Implementing |
| SYMIDX-02 | P1 (order-independent construction) | Design | Implementing |
| SYMIDX-03 | P1 (GetById, O(1)) | Design | Implementing |
| SYMIDX-04 | P1 (FindByName) | Design | Implementing |
| SYMIDX-05 | P1 (FindByQualifiedName, normalized) | Design | Implementing |
| SYMIDX-06 | P1 (original spelling preserved) | Design | Implementing |
| SYMIDX-07 | P1 (FindMembers) | Design | Implementing |
| SYMIDX-08 | P1 (predefined-type + global:: normalization) | Design | Implementing |
| SYMIDX-09 | P1 (syntax-only indexing, never fabricated Exact) | Design | Implementing |
| SYMIDX-10 | P1 (Name/Namespace/ContainingType from syntax alone) | Design | Implementing |
| SYMIDX-11 | P1 (empty solution) | Design | Implementing |
| SYMIDX-12 | P1 (no simple-name-only merging) | Design | Implementing |
| SYMIDX-13 | P2 (FindMethods argument-count filter) | Design | Implementing |
| SYMIDX-14 | P2 (FindMethods argument-type ranking) | Design | Implementing |
| SYMIDX-15 | P2 (FindCandidates priority ordering) | Design | Implementing |
| SYMIDX-16 | P2 (ambiguity reporting) | Design | Implementing |
| SYMIDX-17 | P2 (no silent tie-break) | Design | Implementing |
| SYMIDX-18 | P2 (unique match reporting) | Design | Implementing |
| SYMIDX-19 | P3 (duplicated-symbol-id diagnostic) | Design | Pending |
| SYMIDX-20 | P3 (invalid-containing-symbol diagnostic) | Design | Pending |
| SYMIDX-21 | P3 (ambiguous-symbol-lookup diagnostic) | Design | Pending |
| SYMIDX-22 | P3 (index metrics) | Design | Pending |
| SYMIDX-23 | P3 (syntax-only mode has no precondition) | Design | Pending |

**ID format:** `SYMIDX-[NUMBER]`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 23 total, 0 mapped to tasks, 23 unmapped ⚠️ (expected at Specify hand-off — Tasks will close this).

---

## Success Criteria

- [ ] `ISymbolIndex.GetById`/`FindByName`/`FindByQualifiedName`/`FindMembers`/`FindMethods`/`FindCandidates` all
      answer correctly against a `SymbolIndex` built from a real `AnalysisEngine.AnalyzeAsync` run over
      `fixtures/SyntheticSolution`, in both syntax-only and trusted-solution mode.
- [ ] No lookup method ever returns a silently-chosen single winner when the underlying data has more than one
      equally-ranked candidate — every such case is provably `Ambiguous` in a test.
- [ ] `SymbolFact`'s extended fields and the bumped `SchemaVersion` (2 → 3) round-trip through
      `FactualJsonSerializer` with every existing approved snapshot re-approved, not left stale.
- [ ] Full test suite green; `dotnet build -c Release` and `dotnet format --verify-no-changes` both clean.
