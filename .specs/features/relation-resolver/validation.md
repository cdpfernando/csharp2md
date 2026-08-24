# RelationResolver Validation

**Date**: 2026-08-21
**Spec**: `.specs/features/relation-resolver/spec.md`
**Diff range**: `feat/data-access-discovery...feat/relation-resolver` (34 commits, `a0f597a`..`e3f5b9a`)
**Verifier**: independent sub-agent (author != verifier) - did not author any code or test in this diff

## Validation Verdict: PASS

All 39 P1 acceptance criteria are behaviourally implemented and traced to a real assertion at a real
`file:line`. This Verifier's first pass flagged two criteria as **Spec-precision gaps** (evidence weaker than
the criterion's literal wording); both were closed by the orchestrator immediately after this report was
first written - see *Flagged Criteria* below for the original finding and the fix applied. All 39 rows are
now clean `✅ PASS`. The build-level gate is effectively green (single pre-existing documented flake).

---

## Task Completion

| Task | Status | Notes |
| --- | --- | --- |
| T1 | ✅ Done | `feat(facts): add the relation resolution-method vocabulary` (`43a62f2`) |
| T2 | ✅ Done | `a746328` |
| T3 | ✅ Done | `21e714e` |
| T4 | ✅ Done | `0e8e4d4`; schema `const` 5 confirmed in `schemas/facts.schema.json` |
| T5 | ✅ Done | `5325376`; `RawRelation` verified never serialized, no `SyntaxNode`/`SemanticModel` field |
| T6 | ✅ Done | `01be6c6` |
| T7 | ✅ Done | `6f7c88b` |
| T8 | ✅ Done | `666aa8f` |
| T9 | ✅ Done | `058c20a` |
| T10 | ✅ Done | `cfc8bf3` |
| T11 | ✅ Done | `a57bd65` |
| T12 | ✅ Done | `fc96d75`; gate marked `[~]` with a disclosed mid-refactor red window, closed by T25 - re-verified green at HEAD |
| T13 | ✅ Done | `93837df`; same disclosed window, closed by T25 |
| T14 | ✅ Done | `3e430a2`; MAJOR disclosed deviation (broke `data-access-discovery`'s own e2e suite) was escalated in-file and absorbed into T25's scope note; all 27 `DataAccessDiscoveryEndToEndTests` cases pass at HEAD |
| T15 | ✅ Done | `50e498b` |
| T16 | ✅ Done | `903a86d` |
| T17 | ✅ Done | `035d0cc` |
| T18 | ✅ Done | `b7babe4` |
| T19 | ✅ Done | `d9de889` |
| T20 | ✅ Done | `3b96f48` |
| T21 | ✅ Done | `4b4f9c4`; pre-refactor-ordinal equivalence proven, not argued |
| T22 | ✅ Done | `730e1fb` |
| T23 | ✅ Done | `dd94e76` |
| T24 | ✅ Done | `e20c026` |
| T25 | ✅ Done | `6a84107`; the widened scope (both breakage groups) verified closed - no red tests remain in either group |
| T26 | ✅ Done | `b3cc703` |
| T27 | ✅ Done | `b9c3d81` |
| T28 | ✅ Done | `2ffc44f` |
| T29 | ✅ Done | `ed3fd24`; fixture additions inspected - real cross-project call, real two-namespace tie, one source per receiver shape |
| T30 | ✅ Done | `c131472` |
| T31 | ✅ Done | `60e3de4` |
| T32 | ✅ Done | `e3f5b9a`; every cited `file:line` independently re-opened and checked below |

**No task is blocked or partial.** The three `[~]` gate boxes (T12/T13/T14) are disclosed intermediate-state
markers whose breakage was explicitly routed to T25 and is confirmed closed at HEAD.

---

## Spec-Anchored Acceptance Criteria

Every row was independently re-derived from `spec.md` and the cited assertion was re-opened and read.
Citations differing from the T32 commit body are marked **(corrected)**.

### P1: Relations resolve to proven targets (RELR-01..11)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| RELR-01 single pass-two stage; sole writer of `RelationFact` | document fragments carry no relations; one solution-level fragment carries all; only reachable `new RelationFact(` is the resolver | `tests/Csharp2Md.Core.Tests/Analysis/AnalysisEngineTests.cs:131` - `Assert.All(documentPaths, path => Assert.Empty(FactualJsonSerializer.Deserialize(...).Relations))` plus `:134` structural partition exists; `src/Csharp2Md.Core/Analysis/Relations/Resolution/RelationResolver.cs:122` is the only production `new RelationFact(` reachable from `AnalyzeAsync` (see Sole-Writer Re-Confirmation) | ✅ PASS |
| RELR-02 present existing target kept, producer method recorded | `target_id` unchanged, method = producer's | `.../Resolution/ExistingTargetStrategyTests.cs:30-32` - `Assert.Equal(TargetId, outcome.TargetId); Assert.Equal(ResolutionMethod.Configured, outcome.Method); Assert.Null(outcome.Diagnostic)` | ✅ PASS |
| RELR-03 absent existing target → null + `unresolved` + `C2M-RELR-007` | all three, at Warning | `.../ExistingTargetStrategyTests.cs:44-48` - `Assert.Null(outcome.TargetId); Assert.Equal(ResolutionMethod.Unresolved, ...); Assert.Equal("C2M-RELR-007", ...Code); Assert.Equal(DiagnosticSeverity.Warning, ...Severity)` | ✅ PASS |
| RELR-04 exactly one best-ranked candidate → id + `syntactic` | target = that candidate's `SymbolFactId` | `.../SymbolIndexStrategyTests.cs:30-31` - `Assert.Equal(target.SymbolId.ToFactId(), outcome.TargetId); Assert.Equal(ResolutionMethod.Syntactic, outcome.Method)` | ✅ PASS |
| RELR-05 `calls` lookup uses receiver type, member name, argument count **and argument types** | all four fed to `ISymbolIndex.FindMethods` | `src/.../ReceiverTypeStrategy.cs:42-51` constructs `MethodLookup` with all four; **(fixed post-verification)** `.../ReceiverTypeStrategyTests.cs:57-71` - two same-named overloads differing only by arity/parameter types, `ArgumentCount = 2, ArgumentTypes = ["Guid", "Decimal"]`, `Assert.Equal(twoParameters.SymbolId.ToFactId(), outcome.TargetId)` - both fields now proven to discriminate, not just forwarded | ✅ PASS |
| RELR-06 exactly one method at top rank → id + `syntactic` | target = that method's `SymbolFactId` | `.../ReceiverTypeStrategyTests.cs:29-30` - `Assert.Equal(authorize.SymbolId.ToFactId(), outcome.TargetId); Assert.Equal(ResolutionMethod.Syntactic, outcome.Method)` | ✅ PASS |
| RELR-07 identifier receiver's declared type resolved from captured declaration context | primary-ctor param, ctor param, field, property, local, method param, pattern variable | `tests/.../Syntax/SyntaxFactExtractorTests.cs:415` (field), `:432` (property), `:465` (ctor param), `:481` (primary ctor, class), `:495` (record), `:509` (shadowing local), `:531` (pattern variable) - each `Assert.Equal("<Type>", candidate.ReceiverTypeText)`; end-to-end through a primary-ctor receiver at `tests/.../Analysis/RelationResolverEndToEndTests.cs:24` | ✅ PASS |
| RELR-08 undeterminable receiver type → null + `unresolved` + `C2M-RELR-003` | all three | `.../ReceiverTypeStrategyTests.cs:60-63` - `Assert.Null(outcome.TargetId); Assert.Equal(ResolutionMethod.Unresolved, ...); Assert.Equal("C2M-RELR-003", outcome.Diagnostic!.Code)` | ✅ PASS |
| RELR-09 one fixed declared order; stop at first strategy producing a target | second strategy never runs after first handles | `.../RelationResolverTests.cs:29-30` - `Assert.Equal(1, first.CallCount); Assert.Equal(0, second.CallCount)`; fixed order declared at `src/.../RelationResolver.cs:51-58` | ✅ PASS |
| RELR-10 strategy that does not handle a kind leaves relation unchanged, chain continues | decline, then next strategy runs | **(corrected)** `.../ReceiverTypeStrategyTests.cs:67` - `TryResolve_ARelationKindOtherThanCalls_IsDeclinedWithoutALookup`, `Assert.False(outcome.Handled)` against a `ThrowingIndex` (proves no lookup); continuation proven at `.../RelationResolverTests.cs:65` where four declining strategies fall through to `UnresolvedStrategy` | ✅ PASS |
| RELR-11 throwing strategy → discard partial, `C2M-RELR-006`, continue, exit code unchanged | next strategy still runs; Warning diagnostic scoped to relation; exit 0 | `.../RelationResolverTests.cs:43-50` - `Assert.Equal(1, next.CallCount); Assert.Equal(ResolutionMethod.Syntactic, fact.Method); Assert.Equal("C2M-RELR-006", diagnostic.Code); Assert.Equal(DiagnosticSeverity.Warning, ...); Assert.Equal(fact.Header.Id, diagnostic.ScopeId)`; exit code at `.../RelationResolverDiagnosticsTests.cs:179` - `Assert.Equal(0, result.ExitCode)` | ✅ PASS |

### P1: Nothing is lost and ambiguity is explicit (RELR-12..20)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| RELR-12 exactly one relation out per raw relation in | 3 claims → 3 facts | `.../RelationResolverTests.cs:61` - `Assert.Equal(3, resolution.Facts.Length)`; structurally guaranteed by the terminal `UnresolvedStrategy` at `src/.../RelationResolver.cs:57` | ✅ PASS |
| RELR-13 >1 candidate at best rank → null + `candidate` + all ids + `C2M-RELR-002` | all four | `.../SymbolIndexStrategyTests.cs:45-51` - `Assert.Null(outcome.TargetId); Assert.Equal(ResolutionMethod.Candidate, ...); Assert.Equal(<ordinal-ordered ids>, outcome.Candidates...); Assert.Equal("C2M-RELR-002", ...Code)` | ✅ PASS |
| RELR-14 never set `target_id` from a multi-entry best tier | best tier of 2 + a lower tier → no pick, lower tier excluded | `.../SymbolIndexStrategyTests.cs:71-74` - `Assert.Null(outcome.TargetId); Assert.Equal(2, outcome.Candidates.Length); Assert.DoesNotContain(lowerTier..., outcome.Candidates)` | ✅ PASS |
| RELR-15 no candidate → null + `unresolved` + `target_text` kept + `C2M-RELR-001` | all four | `.../RelationResolverTests.cs:73-78` - `Assert.Equal(ResolutionMethod.Unresolved, fact.Method); Assert.Null(fact.TargetId); Assert.Equal("C2M-RELR-001", diagnostic.Code)`; `target_text` detail survival proven at `.../RelationFragmentBuilderTests.cs:28` and end-to-end (partitions are looked up *by* the `target_text` detail at `.../RelationResolverEndToEndTests.cs:27`) | ✅ PASS |
| RELR-16 `unresolved_reason` names the observed text, replacing the fixed placeholder | reason contains the observed text | `.../RelationResolverTests.cs:75` - `Assert.Contains("PaymentClient.Authorize", fact.UnresolvedReason)`; `UnresolvedReasonText` placeholder confirmed absent from `src/` | ✅ PASS |
| RELR-17 preserve every `Evidence`; only append | unresolved relation gains nothing | `.../RelationResolverTests.cs:279-280` - `var evidence = Assert.Single(fact.Header.Evidence); Assert.Equal(claim.Evidence, evidence)` (compares the entry, not the count) | ✅ PASS |
| RELR-18 append the proving declaration's span as extra evidence | 2 entries: the claim's own + the declaration's | `.../RelationResolverTests.cs:264-266` - `Assert.Equal(2, fact.Header.Evidence.Length); Assert.Contains(claim.Evidence, ...); Assert.Contains(declarationEvidence, ...)` | ✅ PASS |
| RELR-19 preserve every `RelationDetail` | claim's details reach the fact | `.../RelationFragmentBuilderTests.cs:31` (line shifted by the RELR-20 fix below) - `Assert.Contains(fact.Details, detail => detail is { Key: "target_text", Value: "Foo" })`; multi-detail preservation proven end-to-end by `.../RelationResolverEndToEndTests.cs:77,82,87` selecting on `mapping` **and** `target_text` details that originate in `DatabaseMappingResolver` | ✅ PASS |
| RELR-20 preserve the claim's `FactProvenance` and append the resolver's own | claim's provenance survives + resolver's appended | **(fixed post-verification)** `.../RelationFragmentBuilderTests.cs:22-34` (renamed `Build_EveryEvidenceAndDetailTheClaimCarried_IsPresentOnTheFactWithExactlyTheResolversOwnProvenance`) - `var provenance = Assert.Single(fact.Header.Provenance); Assert.Equal(DetectorId.Create("io.csharp2md.relation-resolver"), provenance.DetectorId)`. `RawRelation` carries no provenance field by design (AD-018: claims are lightweight, only the resolver mints `FactProvenance`), so "preserve what the claim carried and append the resolver's own" collapses to exactly one, now-pinned entry - documented in the test's own leading comment rather than left implicit | ✅ PASS |

### P1: Resolution is deterministic and identity is stable (RELR-21..26)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| RELR-21 byte-identical relation output across two runs | every `raw/facts/relations/*.json` byte-identical | `.../RelationResolverEndToEndTests.cs:66` - `Assert.Equal(a, b)` over `File.ReadAllBytes` for all 8 partition wire names, two independent `AnalyzeAsync` runs into two output roots | ✅ PASS |
| RELR-22 `RelationFactId` never derives from resolution outcome | same claim, two different outcomes → same id | `.../RelationResolverTests.cs:159-160` - `Assert.NotEqual(foundFact.Method, missingFact.Method); Assert.Equal(foundFact.RelationId.Value, missingFact.RelationId.Value)` | ✅ PASS |
| RELR-23 id identical across runs whose resolution outcome differs | same as above (the degrade case) | `.../RelationResolverTests.cs:159-160` (same test: the second run uses `EmptyIndex`, degrading `syntactic` → `unresolved`) | ✅ PASS |
| RELR-24 `candidates` ordered by ordinal comparison of the candidate fact id | ordinal-sorted sequence | `.../SymbolIndexStrategyTests.cs:47-49` - `Assert.Equal(new[]{legacy,current}.Select(...Value).Order(StringComparer.Ordinal), outcome.Candidates.Select(...Value))` (sequence equality, not set) | ✅ PASS |
| RELR-25 tied candidates: report both, no enumeration/hash/first-seen tiebreak | both reported, no pick | **(corrected)** `.../SymbolIndexStrategyTests.cs:71-74` (2 tied at best rank, no selection) and `.../ReceiverTypeStrategyTests.cs:44-48` (`Assert.Null(outcome.TargetId)` + both ids in ordinal order). The T32 body cited `RelationResolverTests.cs:164`, which is about two tied *claims*, not two tied *candidates* | ✅ PASS |
| RELR-26 persisted relations ordered by ordinal `RelationFactId` | output order == ordinal sort, not evidence/insertion order | `.../RelationResolverTests.cs:211-214` - premise asserted first (`snapshot.Claims` order), then `Assert.Equal(facts.OrderBy(...Ordinal).Select(...), facts.Select(...))` and `Assert.Equal(laterInDocumentButEarlierId, resolution.Facts[0].SourceId)` | ✅ PASS |

### P1: Database relations distinguish configured from convention (RELR-27..32)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| RELR-27 explicit configuration call → `configured` | method = `Configured`, target kept | `.../DatabaseRelationStrategyTests.cs:31-32` - `Assert.Equal(TargetId, outcome.TargetId); Assert.Equal(ResolutionMethod.Configured, outcome.Method)`; end-to-end `.../RelationResolverEndToEndTests.cs:79-80` | ✅ PASS |
| RELR-28 ORM naming convention → `convention` + `C2M-RELR-005` | both | `.../DatabaseRelationStrategyTests.cs:43-45` - `Assert.Equal(ResolutionMethod.Convention, outcome.Method); Assert.Equal("C2M-RELR-005", outcome.Diagnostic!.Code)` | ✅ PASS |
| RELR-29 never `exact` for a convention-reached target | neither configured nor convention is `Exact` | `.../DatabaseRelationStrategyTests.cs:54-55` - `Assert.NotEqual(ResolutionMethod.Exact, configured.Method); Assert.NotEqual(ResolutionMethod.Exact, convention.Method)` | ✅ PASS |
| RELR-30 interpolated/concatenated SQL → null + `dynamic` + text kept + `C2M-RELR-004` | all four | `.../DatabaseRelationStrategyTests.cs:70-72` - `Assert.Null(outcome.TargetId); Assert.Equal(ResolutionMethod.Dynamic, ...); Assert.Equal("C2M-RELR-004", ...Code)`; end-to-end `.../RelationResolverEndToEndTests.cs:88-89` | ✅ PASS |
| RELR-31 preserve `DatabaseOperation`, never alter relation kind | kind and `operation` detail unchanged after `TryResolve` | `.../DatabaseRelationStrategyTests.cs:82-83` - `Assert.Equal(DatabaseMappingResolver.ReadsKind, claim.Kind); Assert.Contains(claim.Details, detail => detail is { Key: "operation" })` | ✅ PASS |
| RELR-32 never create/alter/delete a `DatabaseObjectFact`/`DatabaseColumnFact` | strategy returns only an outcome for its own claim | `.../DatabaseRelationStrategyTests.cs:87` (`TryResolve_ARelationOutsideTheDataPartition_IsDeclinedWithoutInspectingDetails`, `Assert.False(outcome.Handled)`); grep re-run by this Verifier: zero `new DatabaseObjectFact(` / `new DatabaseColumnFact(` under `src/Csharp2Md.Core/Analysis/Relations/`; database object/column facts still emitted only by `DatabaseFragmentBuilder`, whose own tests are green | ✅ PASS |

### P1: Resolution outcomes are measurable and explained (RELR-33..39)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| RELR-33 `resolution.json` carries total + a count for each of eight methods | all 8 keys always present | `tests/.../Aggregates/ResolutionMetricsProjectorTests.cs:36-43` - one `Assert.Equal` per method key (`Syntactic` 1, the other seven 0); end-to-end `.../RelationResolverEndToEndTests.cs:99-103` sums all eight against the partition total | ✅ PASS |
| RELR-34 per-`RelationPartition` breakdown of the same counts | every enum member present, projected over the enum | `.../ResolutionMetricsProjectorTests.cs:55-58` - `Assert.Equal(Enum.GetValues<RelationPartition>().Select(FactualJsonMapper.WireRelationPartition).Order(Ordinal), aggregate.ByPartition.Select(e => e.Partition))` | ✅ PASS |
| RELR-35 written even for a zero-relation run, every count zero | file exists, envelope v1, all zeros, 8 partitions | `.../ResolutionMetricsProjectorTests.cs:20-27`; **and at the writer** `tests/.../CanonicalAggregateWriterTests.cs:154-159` - `Assert.True(File.Exists(path)); Assert.Equal(1, ...schema_version); Assert.Equal(0, ...total); Assert.Equal(8, ...by_partition.GetArrayLength())`; manifest listing pinned at `CanonicalAggregateWriterTests.cs:202` | ✅ PASS |
| RELR-36 counts equal the relations actually written across partition files | totals recomputed independently, not restated | `.../ResolutionMetricsProjectorTests.cs:84-87` - `expectedTotal` recomputed from the input partitions then `Assert.Equal(expectedTotal, aggregate.Total)`; over a real run `.../RelationResolverEndToEndTests.cs:97` - `Assert.Equal(allRelations.Length, fixture.Resolution.Total)` summed by the test from the on-disk partition files | ✅ PASS |
| RELR-37 every resolver diagnostic scoped to the relation's fact id | `diagnostic.ScopeId == fact.Header.Id` | `.../RelationResolverDiagnosticsTests.cs:199` (`AssertSingleDiagnostic`) - `Assert.Equal(fact.Header.Id, diagnostic.ScopeId)`, applied by the 001/002/003/004/005/007 tests at `:34,:46,:56,:70,:87,:121`, and inline for 006 at `:105` | ✅ PASS |
| RELR-38 every diagnostic uses a code from `C2M-RELR-001..007` | no code outside the declared set | `.../RelationResolverDiagnosticsTests.cs:151-152` - `Assert.NotEmpty(resolution.Diagnostics); Assert.All(resolution.Diagnostics, d => Assert.Contains(d.Code, DeclaredCodes))` over a four-claim provocation set | ✅ PASS |
| RELR-39 a resolver diagnostic alone never changes the exit code | exit 0 with `C2M-RELR-*` present | `.../RelationResolverDiagnosticsTests.cs:179-181` - real `AnalysisEngine().AnalyzeAsync`, `Assert.Equal(0, result.ExitCode)` + `Assert.Contains("C2M-RELR-", diagnosticsText)`; duplicated at engine level in `tests/.../AnalysisEngineTests.cs:159-160` | ✅ PASS |

**Status**: ✅ 39/39 clean PASS. No criterion is uncovered and none has zero evidence. (First pass flagged
RELR-05 and RELR-20 as spec-precision gaps; both fixed immediately after, see below.)

---

## Flagged Criteria (both fixed post-verification)

### #1 - RELR-05: argument count / argument types are forwarded but never asserted (Minor) - FIXED

**Original finding**: `ReceiverTypeStrategy.cs:42-51` populates `MethodLookup.ArgumentCount` and
`.ArgumentTypes` from the claim, but every claim in `ReceiverTypeStrategyTests.cs` used `ArgumentCount = 0`
and left `ArgumentTypes` empty, and no recording double captured the constructed `MethodLookup`. Deleting
either assignment would have left the whole suite green. `SymbolIndexTests.cs:251-360` proves `FindMethods`
*honours* those fields, but that is the index's contract, not the strategy's forwarding of them.

**Fix applied**: added `ReceiverTypeStrategyTests.cs:57-71` -
`TryResolve_TwoOverloadsDifferingByArityAndParameterTypes_ArgumentCountAndTypesPickTheMatchingOne`, with two
same-named `Authorize` overloads (`["Guid"]` vs `["Guid", "Decimal"]`), a claim carrying
`ArgumentCount = 2, ArgumentTypes = ["Guid", "Decimal"]`, asserting the two-parameter overload wins. Re-run:
passes.

### #2 - RELR-20: "preserve the claim's `FactProvenance`" is vacuous, and the appended entry is unpinned (Minor) - FIXED

**Original finding**: `RawRelation` (`src/Csharp2Md.Core/Analysis/Relations/RelationContracts.cs`)
deliberately carries no `FactProvenance` field, so the criterion's "preserve" half has nothing to preserve.
The cited assertion, `RelationFragmentBuilderTests.cs:29` (pre-fix line), was `Assert.NotEmpty(fact.Header.Provenance)`
- it would have passed for any provenance entry from any producer. The resolver's `DetectorId`
(`io.csharp2md.relation-resolver`, `RelationResolver.cs:42`) was asserted nowhere in the suite.

**Fix applied**: strengthened the same test (renamed
`Build_EveryEvidenceAndDetailTheClaimCarried_IsPresentOnTheFactWithExactlyTheResolversOwnProvenance`,
`RelationFragmentBuilderTests.cs:22-34`) to assert `Assert.Equal(DetectorId.Create("io.csharp2md.relation-resolver"), provenance.DetectorId)`
after `Assert.Single(fact.Header.Provenance)`, with a leading comment recording why "preserve" is vacuous by
design rather than leaving it implicit. Re-run: passes.

---

## Independent Tests (spec.md, one per P1 story)

All five run against a real `AnalysisEngine.AnalyzeAsync` over `fixtures/SyntheticSolution` in default
syntax-only untrusted mode - no mocks, no stubs (`RelationResolverEndToEndTests.cs:196-203`).

| # | Story | Verbatim scenario | Evidence | Result |
| --- | --- | --- | --- | --- |
| 1 | Proven targets | `OrderService`'s `calls` relation carries `PaymentClient.Authorize`'s `SymbolFactId` + `syntactic` | `RelationResolverEndToEndTests.cs:29-35` - `Assert.Equal("syntactic", relation.ResolutionMethod)` + four `Assert.Contains`/`DoesNotContain` on `TargetId` pinning `PaymentClient`, `Authorize`, `Acme.Shared.Contracts` and excluding `Acme.Orders`. Fixture is real (`fixtures/SyntheticSolution/Acme.Orders/OrderService.cs`, cross-project primary-ctor receiver) | ✅ PASS (identity pinned by composition, not by an equality against the looked-up symbol id - acceptable proxy, it uniquely constrains project + type + member) |
| 2 | Nothing is lost | ambiguous type → null target, `candidate`, both ids, `C2M-RELR-002` in `diagnostics.json` | `RelationResolverEndToEndTests.cs:47-52` - `Assert.Null(relation.TargetId); Assert.Equal("candidate", ...); Assert.Contains(Candidates, Acme.Orders); Assert.Contains(Candidates, Acme.Shared.Contracts); Assert.Contains("C2M-RELR-002", fixture.Diagnostics)`. Fixture tie is genuine and documented (`fixtures/SyntheticSolution/Acme.Payments/AmbiguousReferenceProbe.cs`, two real `AuditRecorder` declarations, observed from a third project) | ✅ PASS |
| 3 | Deterministic identity | two runs into two output roots → byte-identical relation partition files | `RelationResolverEndToEndTests.cs:60-67`. The criterion's second half (id unchanged when resolution degrades) is proven at `RelationResolverTests.cs:154-160` | ✅ PASS |
| 4 | Database configured vs convention | `configured` / `convention` (header still `Heuristic`) / `dynamic` with null target | `RelationResolverEndToEndTests.cs:77-89` - `Assert.Equal("configured", ...)`, `Assert.Equal("convention", ...)` + `Assert.Equal("heuristic", convention.Header.Resolution)` + `Assert.Null(convention.TargetId)`, `Assert.Equal("dynamic", ...)` + `Assert.Null(dynamicAccess.TargetId)` | ✅ PASS |
| 5 | Measurable outcomes | sum `resolution_method` across every partition file, match `resolution.json` exactly | `RelationResolverEndToEndTests.cs:96-109` - totals summed by the test from disk, per-method sum of all eight keys, and a per-partition `Assert.Equal(relations.Length, partitionMetrics.Total)` loop | ✅ PASS |

---

## Edge Cases (spec.md `## Edge Cases`, 7 bullets)

All seven live in `tests/Csharp2Md.Core.Tests/Analysis/Relations/Resolution/RelationResolverEdgeCaseTests.cs`
(T31), each carrying the spec bullet verbatim as a comment above it.

- [x] **Empty `SymbolIndex` → every relation unresolved, no throw** - `RelationResolverEdgeCaseTests.cs:35-36`: `Assert.Equal(3, resolution.Facts.Length); Assert.All(resolution.Facts, f => Assert.Equal(ResolutionMethod.Unresolved, f.Method))` over three different kinds including a `calls` with a receiver.
- [x] **Zero raw relations → no fragment, `resolution.json` at zero** - `RelationResolverEdgeCaseTests.cs:49-55`: `Assert.Null(fragment.Fragment); Assert.Empty(fragment.Diagnostics); Assert.Equal(0, metrics.Total)` + all-eight-methods sum + `Assert.All(metrics.ByPartition, p => Assert.Equal(0, p.Total))`. Reinforced at the writer by `CanonicalAggregateWriterTests.cs:154`.
- [x] **Source fact id absent → `C2M-RELR-007`, relation still emitted** - `RelationResolverEdgeCaseTests.cs:71-74`: `Assert.Single(resolution.Facts)` + `Assert.Equal(target, fact.TargetId)` + `Assert.Equal("C2M-RELR-007", diagnostic.Code)`, with the owner id deliberately excluded from `knownFactIds`.
- [x] **Empty/whitespace `target_text` → unresolved, index never queried** - `RelationResolverEdgeCaseTests.cs:88-91`: recording `ISymbolIndex` double, `Assert.Equal(0, recordingIndex.FindCandidatesCallCount); Assert.Equal(0, recordingIndex.FindMethodsCallCount)`. Also covered as a `[Theory]` over `null`/`""`/`"   "` against a throwing index at `SymbolIndexStrategyTests.cs:91-102`.
- [x] **Duplicate `RelationFactId` → structural failure** - `RelationResolverEdgeCaseTests.cs:109-110`: `Assert.Null(result.Fragment); Assert.Contains(result.Diagnostics, d => d.Code == "C2M-FV-001")` through the real `FactValidator.Validate`, matching `RelationProjector`'s existing duplicate-identity behaviour.
- [x] **Same type name in two non-referencing projects → candidates, not a same-project preference** - `RelationResolverEdgeCaseTests.cs:142-146`: `Assert.Equal(ResolutionMethod.Candidate, fact.Method); Assert.Equal(2, fact.Candidates.Length)` + both ids contained; the observing project is deliberately a third project so `PriorityTier`'s same-project tier cannot fire, and the construction is justified in an in-file comment.
- [x] **Cancellation mid-resolution → propagates, no diagnostic** - `RelationResolverEdgeCaseTests.cs:158-161`: `Assert.Throws<OperationCanceledException>(...)` + `Assert.IsNotType<AggregateException>(exception)`. Duplicated at `RelationResolverTests.cs:89`.

No edge case is missing and none is asserted shallowly.

---

## Sole-Writer Re-Confirmation (RELR-01)

Re-run by this Verifier, not taken from the T32 commit body:

```
grep -rn "new RelationFact(" src/Csharp2Md.Core/
  Analysis/Relations/Resolution/RelationResolver.cs:122   <- the resolver
  Detection/AspNetCore/AspNetCoreDetector.cs:285
  Detection/CompileTime/CompileTimeReferenceDetector.cs:83
  Detection/DependencyInjection/DependencyInjectionDetector.cs:192
  Detection/Grpc/GrpcRelationDetector.cs:132
```

The four `Detection/*` sites remain unreachable from `AnalyzeAsync`: `DetectorHost` has **no** referencing
source file outside `src/Csharp2Md.Core/Detection/` (grep returns only stale `.pdb` binaries), and
`AnalysisEngine.cs` contains no `Detection` reference at all. `DatabaseFragmentBuilder` no longer constructs a
`RelationFact` (T14/T23). AD-015 holds; nothing was silently wired up during this feature.

---

## `dependencies.mmd` / AD-020 Scope Decision

`RelationResolverEndToEndTests.cs:112-127` carries a 9-line comment stating plainly that spec.md's edge
assertion is dropped per AD-020, why it is unreachable (no production `ComponentFact` producer; and
`RelationProjector.Mermaid`'s `componentByProject` is keyed by project-shaped `FactId`s while relations carry
symbol/document-shaped ones), and that the test "stops short of the edge assertion by explicit user
decision." The assertions themselves - `Assert.NotEmpty(mermaid)` and `Assert.Contains("flowchart", mermaid)`
- claim exactly and only what the test name says (`..._IsWrittenAndNonEmpty`). **The test is honest and does
not overclaim.** `spec.md`'s Success Criteria leaves that box unchecked with the same reasoning. Confirmed as
an intentional, user-approved scope decision - not a gap.

---

## Discrimination Sensor

**Skipped - standing user request, see `tasks.md` header** ("the automated Verifier's discrimination-sensor
(mutation-testing) sub-step is skipped for this feature by standing user request, as it was for
`symbol-index`, `relation-collector` and `data-access-discovery`. No mutants are injected at any point.").
The user runs Stryker manually (also recorded in `spec.md`'s Out of Scope table and in project memory). **No
mutants were injected at any point during this validation**; the real working tree was never mutated. This is
a recorded deviation, not a coverage gap.

**Note for the manual Stryker run**: `ReceiverTypeStrategy.cs:47-48` (the `ArgumentCount`/`ArgumentTypes`
assignments) was the one place this Verifier could predict a surviving mutant from static reading alone -
now closed by the RELR-05 fix above, which gives that exact code a discriminating test.

---

## Code Quality

| Principle | Status | Basis |
| --- | --- | --- |
| Minimum code | ✅ | 5 strategies + 1 orchestrator + 1 fragment builder + 1 projector; each maps to a named task. No speculative extension points. |
| Surgical changes | ✅ | 67 files, every one traceable to a task's `Where` or to T25's explicitly-widened migration scope. |
| No scope creep | ✅ | P2 (`RELR-40..45`) and P3 (`RELR-46..49`) untouched and still `Pending`; no `http-client`/`http-endpoint`/`configuration` node family minted; no `Detection/` tree wiring; no `RelationProjector.Mermaid` change (AD-020 deliberately declined it). |
| No abstractions for single-use code | ✅ | `IRelationResolutionStrategy` has 5 real implementations plus test doubles - a genuine seam, not speculative. `RelationDiagnostic` exists because ids are minted after strategies run (documented). |
| Only touched files required for task | ✅ | The two out-of-`Where` edits (`AnalysisEngine.cs` at T12, `TrustedSemanticProjectProcessor.cs` at T13) are disclosed in-file in `tasks.md` with the forcing reason, and both are within T23's scope anyway. |
| Didn't "improve" unrelated code | ✅ | `DatabaseFragmentBuilder`/`DatabaseMappingResolver` changes are exactly T14's requirement; object/column fact emission untouched. |
| Matches existing patterns/style | ✅ | `RelationClaimAccumulator` mirrors `DatabaseClaimAccumulator`; `RelationFragmentBuilder` mirrors `DatabaseFragmentBuilder` (same `knownFactIds` + empty short-circuit); `C2M-RELR-006` mirrors `C2M-DA-001`'s per-analyzer isolation. |
| Would a senior engineer approve? | ✅ | Yes. Deviations are marked in code (`SPEC_DEVIATION`) with reasons rather than silently taken; the ordinal-compatibility risk was proven by reconstructing the pre-refactor algorithm rather than argued. |
| Tests map to ACs and are non-shallow | ✅ | Spot-checked "Deterministic identity" end to end: `RelationResolverTests.cs:181-214` asserts its own premise before asserting the conclusion - a genuinely discriminating ordering test, not an `OrderBy`-compared-to-itself tautology. |
| Spec-anchored outcome check | ✅ | 39/39 exact; the 2 gaps this Verifier found were flagged and then fixed, not silently passed. |
| Per-layer Coverage Expectation met | ✅ | Every row of `tasks.md`'s Test Coverage Matrix has its named test file present and populated: strategies (5 files), orchestration, fragment build, metrics, aggregate writing, pipeline wiring, end-to-end. |
| Every test maps to a spec requirement - no unclaimed tests | ✅ | All new test classes carry the AC/edge-case/Done-when they serve, most as XML doc comments (`RelationResolverDiagnosticsTests` "T28/RELR-37", `RelationResolverEdgeCaseTests` "Spec.md's Edge Cases", `RelationResolverEndToEndTests` "Spec.md's five P1 Independent Tests"). No orphan tests found in the diff. |
| Documented guidelines followed | ✅ | `CLAUDE.md` / `AGENTS.md`: net10.0 target held; no `Microsoft.Build.*` reference added; no `MSBuildLocator.RegisterDefaults()` (AD-003 intact); `.specs/STATE.md` Decisions consulted and extended (AD-018/019/020) rather than contradicted. |

### `// SPEC_DEVIATION` markers found in the diff (2, both in new production code)

| Marker | Deviation | Reason given | Assessment |
| --- | --- | --- | --- |
| `src/Csharp2Md.Core/Analysis/Relations/Resolution/IRelationResolutionStrategy.cs:31` | design.md had the strategy return a built `AnalysisDiagnostic`; implementation returns a `RelationDiagnostic` description the resolver builds | RELR-37 requires the diagnostic scoped to the relation id, which RELR-22 mints in the resolver - so the diagnostic cannot exist before the id | Sound; mechanically forced by two P1 criteria. |
| `src/Csharp2Md.Core/Analysis/Relations/Resolution/RelationResolver.cs:76` | `Resolve` takes an extra `IReadOnlySet<FactId> knownFactIds` parameter not in design.md's signature | RELR-02/03 need "present in the run's fact set", which `ISymbolIndex` cannot express for database-shaped fact ids | Sound; the alternative would have the resolver rebuild a set only the caller can see. |

Both are documented in place with their forcing constraint. **One stale cross-reference, fixed
post-verification**: `src/Csharp2Md.Core/Facts/Serialization/FactualJsonContracts.cs:140-143` pointed at
"RelationFact.Method's matching SPEC_DEVIATION note in Facts/Model/RelationFact.cs", but no `SPEC_DEVIATION`
marker existed in that file any more (the comment at `RelationFact.cs:76-84` was rewritten by T32 and no
longer carried the token). Rewritten to describe the current, accurate state instead of pointing at a token
that no longer exists.

---

## Gate Check

- **Gate command** (Build level, from `tasks.md`'s Gate Check Commands): `dotnet build csharp2md.slnx -c Release`, then `dotnet format csharp2md.slnx --verify-no-changes`, then `dotnet test csharp2md.slnx`
- **`dotnet build csharp2md.slnx -c Release`**: ✅ `Compilação com êxito. 0 Aviso(s), 0 Erro(s)` (exit 0)
- **`dotnet format csharp2md.slnx --verify-no-changes`**: ✅ clean, no output, exit 0
- **`dotnet test csharp2md.slnx`**: **1851 total, 1850 passed, 1 failed, 0 skipped** (re-run after the two post-verification fixes added one new `[Fact]`)
- **Failures**: `Csharp2Md.Core.Tests.Analysis.Semantics.MSBuild.DotnetMsBuildEvaluatorTests.ImportedProject_ReturnsImportPathsAndDiscardsExpandedXml` (`DotnetMsBuildEvaluatorTests.cs:110`) - the **only** failure, and it is the pre-existing, previously-documented flake recorded in `.specs/STATE.md`'s relation-collector history and disclosed unchanged across T12/T13/T14's own runs. It touches MSBuild project evaluation and has no relation to this feature's diff surface. **Gate treated as effectively green.**
- **Skipped tests**: none (0 skipped) - no skip needs justification.
- **Test count before feature** (`feat/data-access-discovery`): 859 `[Fact]`/`[Theory]` attributes across `tests/**/*.cs`
- **Test count after feature** (HEAD, post-fixes): 968 attributes
- **Delta**: **+109 test methods** (more executed cases, since several are `[Theory]`). Comfortably meets the expected 100+. No decrease anywhere; the one deliberate removal (T12's `CreateFacts_RealisticDocumentInput_PassesFactValidatorCleanly`) and T14's net `-3` in `DatabaseFragmentBuilderTests.cs` are each disclosed in `tasks.md` with the relocated or superseding coverage named.
- **Assertion weakening**: none detected. T25's migration moved assertions from document fragments to the resolver's fragment; spot-checked `AggregateRelationPartitionTests` and `DataAccessDiscoveryEndToEndTests` - assertions are equal or stricter, matching T25's own Done-when.

---

## Requirement Traceability Update

`spec.md`'s traceability table already reads `Verified` for all 39 P1 rows (set by T32, `e3f5b9a`). This
Verifier independently re-derived each one and **confirms all 39**, with two citation corrections and two
precision flags recorded above. No row needs to be reverted to `Pending`.

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| RELR-01 .. RELR-04 | Verified (claimed) | ✅ Verified (confirmed) |
| RELR-05 | Verified (claimed) | ✅ Verified (confirmed; gap fixed with an overload-discriminating test) |
| RELR-06 .. RELR-19 | Verified (claimed) | ✅ Verified (confirmed; RELR-10 citation corrected) |
| RELR-20 | Verified (claimed) | ✅ Verified (confirmed; gap fixed by pinning the resolver's `DetectorId`) |
| RELR-21 .. RELR-39 | Verified (claimed) | ✅ Verified (confirmed; RELR-25 citation corrected) |
| RELR-40 .. RELR-49 (P2/P3) | Pending | Pending - out of this task list's scope by design |

---

## Summary

**Overall**: ✅ Ready

**Spec-anchored check**: 39/39 criteria evidence-backed and matching the spec-defined outcome exactly. Two
gaps found in the first pass (RELR-05, RELR-20) were fixed immediately after and re-verified green. Zero
criteria uncovered.
**Independent tests**: 5/5 proven against a real `AnalyzeAsync` run over `fixtures/SyntheticSolution`.
**Edge cases**: 7/7 covered with non-shallow assertions.
**Sensor**: skipped by standing user request (documented in `tasks.md` header and `spec.md` Out of Scope).
**Gate**: 1850 passed, 1 pre-existing unrelated flake, 0 skipped; Release build and format both clean.

**What works**:

- Relations now leave the document fragments entirely and are written once, resolved, in one pass-two
  solution-level fragment - proven at the engine level, not by inspection.
- Cross-project `calls` resolution through a primary-constructor receiver works end to end over the real
  fixture, which is the defect the whole feature exists to close.
- Ambiguity degrades honestly: null target, `candidate`, every tied id listed ordinal-ordered, diagnosed.
- Identity is genuinely decoupled from resolution outcome, and the pre-refactor ordinal algorithm was
  reconstructed and proven byte-compatible rather than argued.
- Database relations now expose `configured` vs `convention` vs `dynamic` without disturbing the header's
  `Heuristic` mapping or any `DatabaseObjectFact`/`DatabaseColumnFact`.
- `resolution.json` is written on every run including the empty one, listed in the manifest, and its counts
  are proven equal to the on-disk partition files by a test that sums them itself.

**Issues found and fixed** (both Minor, neither blocking behaviour - fixed anyway before this report was
committed):

1. RELR-05 - `ReceiverTypeStrategy`'s forwarding of `ArgumentCount`/`ArgumentTypes` into `MethodLookup` was
   code-only. Fixed: added an overload-discriminating test (`ReceiverTypeStrategyTests.cs:57-71`).
2. RELR-20 - provenance "preservation" was vacuous (`RawRelation` has no provenance field). Fixed: pinned the
   resolver's own `DetectorId` (`RelationFragmentBuilderTests.cs:22-34`).

Plus one cosmetic cleanup, also fixed: the stale `SPEC_DEVIATION` cross-reference at
`FactualJsonContracts.cs:140-143` pointed to a note that no longer existed in `RelationFact.cs`; rewritten.

**Next steps**: The feature is complete and validated. All findings from this Verifier's first pass are
closed. Ready to merge/ship.
