# Analysis Publication Resilience Validation

**Date**: 2026-09-15
**Spec**: `.specs/features/analysis-publication-resilience/spec.md`
**Diff range**: `0c3999e..HEAD` (`a0a9598` … `b717b5c`)
**Verifier**: independent sub-agent (author ≠ verifier)
**Result**: PASS

T1–T13 each have a Conventional Commit on `feature/generator-cli-projections-certification` (`a0a9598` … `b717b5c`). Implementation starts at T1 `a0a9598`. HEAD is T13 `b717b5c`. `tasks.md` Done-when boxes are all checked. Header still says “Approved”; the checkboxes and commits are the completion evidence.

Issue 01 was already on HEAD. T1/T2 added APR traits and filled nested-failure / option-freeze assertions; `git show --stat a0a9598` and `8221d71` touch no `src/` file.

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1 | Done | `a0a9598` `test(analysis): pin pipeline-failure detail to APR-01 through APR-07` |
| T2 | Done | `8221d71` `test(cli): pin unpublished stderr-once and analyze option freeze` |
| T3 | Done | `93cbd87` `feat(analysis): expose EvidenceScope.Qualifying without constructing a chain` |
| T4 | Done | `235934d` `feat(analysis): fall back to document-scoped structural contains evidence` |
| T5 | Done | `6600ec4` `feat(domain): split canonical signature lists with nested delimiter depth` |
| T6 | Done | `416f84c` `feat(storage): rehydrate nested signatures through Domain SplitTopLevel` |
| T7 | Done | `a70d3f4` `fix(projection): stop quoting absent unknown and frontier posting keys` |
| T8 | Done | `644284b` `test(fixtures): add the unlabeled PublicationResilience solution skeleton` |
| T9 | Done | `bd186e4` `test(fixtures): add the invocation-only builder constructor` |
| T10 | Done | `f3944f9` `test(fixtures): add nested tuple, generic and array signatures` |
| T11 | Done | `c315007` `test(fixtures): size unknown and frontier postings past the default ceiling` |
| T12 | Done | `d3029e2` `test(cli): certify default-budget analyze of PublicationResilience` |
| T13 | Done | `b717b5c` `test(cli): analyze Pitstop as an optional LocalCorpus` |

No blocked or partial tasks. Trait scanners are not coverage; each AC below is re-derived from the cited assertion.

---

## Spec-Anchored Acceptance Criteria

### P1: Safe pipeline failure details

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| APR-01 unpublished exception names failing stage and root type | `FailingStage` is the throwing stage; `Detail` contains the root exception type | `tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineStageFailureTests.cs:36-37` `Assert.Equal(thrower.Name, outcome.FailingStage)` and `Assert.Equal("InvalidOperationException: Stage 'Classification and Promotion' failed.", outcome.Detail)`; nested case `:75-77` stage name + `InvalidOperationException: Could not parse` | PASS |
| APR-02 failure detail is exactly one sanitized line | one line; root message only after sanitization | `tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineFailureDetailTests.cs:15-17` `Assert.Equal($"InvalidOperationException: {message}", detail)` and `DoesNotContain` `\r`/`\n`; `:126-128` same for a caught exception that has a stack; nested pipeline `:77-84` one-line detail, no line breaks | PASS |
| APR-03 secret, absolute path, source excerpt, line break removed; type alone when nothing safe remains | those contents gone; type-only when the remainder is empty | `PipelineFailureDetailTests.cs:31` path-only → `Assert.Equal("InvalidOperationException", detail)`; `:43-44` secret stripped to `Password=***`; `:71-73` unlabeled syntax → type alone; `:104-106` `source:` excerpt → type alone; nested pipeline `:78-84` path/secret/source/inner/environment/`\n` absent | PASS |
| APR-04 unpublished CLI stderr contains the safe detail exactly once; stdout and exit 2 unchanged | stderr is one `csharp2md:` line with the detail; stdout summary; exit 2 | `tests/Csharp2Md.Cli.Tests/AnalyzeExitCodeTests.cs:58-65` `Assert.Equal(2, exitCode)`; stdout is the two-line summary; stderr equals exactly one line `csharp2md: unpublished at Inventory {safeDetail} {path}` | PASS |
| APR-05 no stack, inner chain, exception data, environment, raw syntax on the failure path | those strings absent from `Detail` | nested pipeline `PipelineStageFailureTests.cs:81-82` no outer message, no `environmentValue`; `:78-80` no path/secret/source; `PipelineFailureDetailTests.cs:129-131` `Assert.False(string.IsNullOrEmpty(caught.StackTrace))` then `DoesNotContain(caught.StackTrace, detail)` | PASS |
| APR-06 cancellation is not an unexpected pipeline failure | `FailingStage` and `Detail` stay null; `OperationCanceledException` still propagates | `tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineCancellationTests.cs:22-23` `ThrowsAsync<OperationCanceledException>`; `:51-53` `Assert.Null(outcome.FailingStage)` and `Assert.Null(outcome.Detail)` | PASS |
| APR-07 failed retry aborts staging and keeps prior package bytes | staging gone; prior payloads `SequenceEqual` | `PipelineStageFailureTests.cs:107-116` in-memory payload `SequenceEqual`; `:146-149` no `*.staging` and `PackageSnapshot.AssertEqual`; `AbortPreservesPackageTests.cs:56-68` same byte check after a later abort | PASS |
| APR-08 no new CLI option, logger, sink, schema field or package artifact | analyze product options stay the five named flags; no new surface in this diff | `tests/Csharp2Md.Cli.Tests/AnalyzeOptionSurfaceTests.cs:45-47` `Assert.Equal(["--allowlist", "--max-file-reads-per-scenario", "--output", "--reading-budget-tokens", "--solution"], analyzeProductOptions)`. `git diff --name-only 0c3999e..HEAD -- src` is only `EvidenceScope.cs`, `ContainsRelationEmitter.cs`, `CanonicalSymbolSignature.cs`, `RetrievalGuideProjector.cs`, `WireFactMapping.cs` | PASS |

### P1: Preserve valid evidence for contains relations

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| APR-09 own qualifying structural evidence, no other scope | evidence owners are the symbol; kinds exclude Invocation/DataAccess | `tests/Csharp2Md.Analysis.Tests/Extraction/ContainsRelationEmitterTests.cs:98-105` `Assert.All(..., identity => Assert.Equal(controllerType.Reference, identity.Owner))`, contains `BaseType`, `DoesNotContain` Invocation/DataAccess; mix case `:266` same owner filter | PASS |
| APR-10 invocation-only symbol falls back to document-scoped structural evidence | constructor owns only behavioral; chain is non-empty and structural | `ContainsRelationEmitterTests.cs:194-213` `Assert.All(owned, Invocation or DataAccess)`; `Assert.Contains` a non-behavioral observation; `Assert.Single` contains edge; `Assert.NotEmpty` chain; `Assert.All` chain kinds not Invocation/DataAccess | PASS |
| APR-11 confirmed contains chain is non-empty and has no Invocation/DataAccess | non-empty; those kinds absent | builder `:208-213`; mix `:263-269`; `EvidenceScopeTests.cs:57` `Assert.Equal([declaration], qualifying)`; `:73` empty qualifying does not throw | PASS |
| APR-12 neither scope qualifies: omit the edge, one `contains-evidence-unqualified` on the symbol fact id, keep the rest | no contains to that symbol; diagnostic code/id/one-line message; symbol still present | `ContainsRelationEmitterTests.cs:301-312` `DoesNotContain` contains targeting `host`; `Assert.Single` diagnostic `Code == "contains-evidence-unqualified"` and `IdentityOrKey == host.Reference.Id.Value`; `Assert.Equal("contains relation omitted because no qualifying structural evidence was found.", diagnostic.Message)`; `DoesNotContain('\n')`; Host fact still in the snapshot | PASS |
| APR-13 confirmed relation still derives from at least one observation | empty qualifying set still throws; emitter never creates an empty chain | `EvidenceScopeTests.cs:103-105` `Assert.Throws<ArgumentException>` message contains `"at least one observation"`; `:117-118` empty candidate set; APR-12 `:316-320` remaining contains edges `NotEmpty` and non-behavioral | PASS |
| APR-14 existing symbol-owned structural `contains` keeps owner, kinds, cardinality | OrdersController evidence stays own-scope `BaseType` | `ContainsRelationEmitterTests.cs:97-102` `NotEmpty`, all owners `controllerType.Reference`, contains `BaseType`, distinct identities | PASS |
| APR-15 no new fact family, observation kind, relation kind, facet, identity namespace or schema version | registry/schema axes unchanged | `git diff --stat 0c3999e..HEAD -- contracts schemas` empty. `tests/Csharp2Md.Domain.Tests/Registry/TaxonomyVersionsTests.cs:13-17` all five axes still `1`. `RegistryDriftGateTests.cs:18-19` committed registry `SequenceEqual` fresh emission. Inventory extract `:45-52` still emits only `Contains` | PASS |

### P1: Round-trip nested canonical symbol signatures

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| APR-16 named-tuple parameter Domain → wire → Domain | exact `CanonicalSymbolSignature` equality | `tests/Csharp2Md.Storage.Tests/Mapping/NestedSignatureRoundTripTests.cs:101-103` `Assert.Equal(symbol, restoredSymbol)`, `Assert.Equal(signature, restoredSymbol.Signature)`, `Assert.Equal(signature.Value, restoredSymbol.Signature.Value)` on the `named-tuple` theory row; Domain split `CanonicalSymbolSignatureTests.cs:142-148` tuple-internal commas are not slices | PASS |
| APR-17 two tuple parameters stay distinct | three slices: tuple, bool, tuple | `CanonicalSymbolSignatureTests.cs:142-148` three-element array; Storage theory row `two-tuples` uses the same round-trip asserts `:101-103` | PASS |
| APR-18 nested generic-in-tuple and tuple-in-generic | split only at depth 0; wire equality | `CanonicalSymbolSignatureTests.cs:160-171` both nestings; Storage `nested-generic-tuple` row `:101-103` | PASS |
| APR-19 multidimensional array rank commas are not separators | `Int32[,,]` is one slice | `CanonicalSymbolSignatureTests.cs:181` `Assert.Equal(["global::System.Int32[,,]", "global::System.String"], ...)`; Storage `multidimensional-array` row `:101-103` | PASS |
| APR-20 mixed generic/tuple/array is deterministic, top-level commas only | two calls equal; three slices | `CanonicalSymbolSignatureTests.cs:194-200` `Assert.Equal(first.ToArray(), second.ToArray())` and the three slice values; Storage `mixed` row `:101-103` | PASS |
| APR-21 existing simple and generic identities stay byte-identical | same slices; wire bytes equal | `CanonicalSymbolSignatureTests.cs:211-217` simple and `Dictionary<...>` slices; `NestedSignatureRoundTripTests.cs:124-126` `Assert.Equal` Domain + `CanonicalJson.Write` `SequenceEqual`; `FactRoundTripTests.cs:90` Symbol fixture `Assert.Equal(snapshot.Facts[0], restored.Facts[0])` | PASS |
| APR-22 malformed or delimiter-unbalanced wire is structural corruption | `PublicationRejectedException` gate `construction`; Domain `ArgumentException` | `CanonicalSymbolSignatureTests.cs:227` `Assert.Throws<ArgumentException>` for unclosed `<>`, `()`, `[]`; `NestedSignatureRoundTripTests.cs:157-160` `Assert.Equal("construction", exception.Gate)` and id in `Detail` | PASS |
| APR-23 identity namespace, signature fields, escaping, taxonomy, schema unchanged | `Create` still joins with `,`; registry bytes unchanged | `CanonicalSymbolSignatureTests.cs:242-244` exact `sig1;...parameters=...%2C...` string; `RegistryDriftGateTests.cs:18-19`; `git diff` empty under `contracts/` | PASS |

### P1: Shard-aware disposition posting guidance

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| APR-24 exact `postings/unknowns.json` stays backticked | exact-key sentence includes `` `postings/unknowns.json` `` | `tests/Csharp2Md.Projection.Tests/Guides/RetrievalGuideProjectorTests.cs:159-161` unresolved-relation shard still backticks the posting key; `:317` `Assert.Contains("postings/unknowns.json", named)` when the exact key exists | PASS |
| APR-25 sharded unknown posting family names a matching shard, no backticked base key | shard wording; `` `postings/unknowns.json` `` absent | `RetrievalGuideProjectorTests.cs:179-183` `Assert.Contains("...matching postings/unknowns.json shard...")` and `DoesNotContain("`postings/unknowns.json`")` | PASS |
| APR-26 same exact-versus-sharded rule for `postings/frontiers.json` | exact backticks; sharded matching-shard sentence | `:202-205` exact frontier backticks `` `postings/frontiers.json` ``; `:221-225` sharded `DoesNotContain("`postings/frontiers.json`")` | PASS |
| APR-27 candidate, unresolved, open-frontier *relation* families keep existing shard-aware wording | unresolved relation shard sentence; candidate/frontier exact keys still named | `:137-145` confirmed `invokes` shard sentence without `` `relations/confirmed/invokes.json` ``; `:159-163` unresolved relation shard without `` `relations/unresolved.json` ``; `:314-318` candidate/unresolved/frontier relation keys named when exact | PASS |
| APR-28 every backticked artifact key exists; `ValidateNoAbsentKeys` stays strict | throw on a quoted missing key; sharded guides do not quote the absent base | `:363-367` `Assert.Equal("projection-key", exception.Gate)` and `Assert.Equal(offender, exception.Detail)`; `:183` and `:225` no backticked absent posting base. Projector `Project` calls `ValidateNoAbsentKeys` before returning | PASS |
| APR-29 artifacts stay inside declared ceiling rules; no new exclusion | derived 32 KiB ceiling; over-ceiling files are the existing single-record exemption | `tests/Csharp2Md.Analysis.Tests/Fixtures/PublicationResilienceCeilingTests.cs:23` `Assert.Equal(32 * 1024, derivedCeiling)`; `:59` manifest `ArtifactCeilingBytes`; `:70-74` offenders must be `IsSingleRecordShard`. Diff does not touch a ceiling-exclusion API | PASS |
| APR-30 missing posting/disposition family is absent, not sharded | three `none is recognized in this package` lines; no disposition artifact keys | `RetrievalGuideProjectorTests.cs:333-334` `Assert.Empty(ArtifactKeys(section))` and `Assert.Equal(3, Regex.Matches(..., "none is recognized in this package").Count)` | PASS |
| APR-31 two projections of the same view and ceiling are byte-identical | payload `SequenceEqual` | `RetrievalGuideProjectorTests.cs:264` `Assert.True(first[0].Payload.AsSpan().SequenceEqual(second[0].Payload.AsSpan()))` | PASS |
| APR-32 validator strictness, taxonomy, schemas, retrieval semantics unchanged | `ValidateNoAbsentKeys` still abort-and-name; registry/schema axes untouched | `:366-367` gate `projection-key`; `TaxonomyVersionsTests.cs:13-17`; `git diff` empty under `contracts/` and `schemas/` | PASS |

### P1: Default-budget CLI publication across the regressions

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| APR-33 regression tree is `fixtures/PublicationResilience`, unlabeled, not CertificationCorpus | path, not corpus root, no `labels/` | `tests/Csharp2Md.Analysis.Tests/Fixtures/PublicationResilienceTests.cs:15-21` `Assert.Equal(...fixtures/PublicationResilience, RootPath)`; `Assert.NotEqual(CertificationCorpusPaths.RootPath, ...)`; `Assert.False(Directory.Exists(...labels))` | PASS |
| APR-34 `fixtures/SyntheticSolution` byte-identical; integrity gate still passes | committed digest unchanged; tree not in this diff | `tests/Csharp2Md.Analysis.Tests/Fixtures/SyntheticSolutionImmutabilityTests.cs:55` `Assert.Equal(ExpectedDigest, ComputeDigest(actual))` (ran in the Analysis gate). `git diff --stat 0c3999e..HEAD -- fixtures/SyntheticSolution` empty | PASS |
| APR-35 default-budget `analyze` commits without extraction/corruption/key rejection | no budget flags; exit success/degraded; no `csharp2md:` stderr; manifest present | `tests/Csharp2Md.Cli.Tests/PublicationResilienceAnalyzeTests.cs:64-66` args have no `reading-budget` / `max-file-reads`; `:77-83` no `csharp2md:` on stderr, exit `Success` or `Degraded`, Solution+Symbol facts, `manifest.json` exists | PASS |
| APR-36 published builder `contains` is non-empty, structural, free of Invocation/DataAccess | document-to-symbol edge; chain kinds | `PublicationResilienceAnalyzeTests.cs:95-111` constructor owns only Invocation/DataAccess; relation source is a `Document`; `NotEmpty` chain; `Assert.All` not Invocation/DataAccess. Engine path `PublicationResilienceBuilderTests.cs:40-64` same on the committed package | PASS |
| APR-37 tuple, nested-generic, multidimensional-array symbols restore equal canonical identities | four shapes found; Domain equality after wire | `PublicationResilienceAnalyzeTests.cs:116-140` four `AssertSymbol` filters then `AssertRoundTrip` `:155-157` `Assert.Equal(published, restoredSymbol)` and signature/`Value`. Engine `PublicationResilienceSignatureTests.cs:50-53` same four shapes | PASS |
| APR-38 retrieval guide backtick keys resolve, including sharded unknown/frontier postings | base keys absent; shard keys present; every `path`-shaped backtick exists | `PublicationResilienceAnalyzeTests.cs:165-185` `DoesNotContain` exact `postings/unknowns.json` and `frontiers.json`; `Contains` `postings/unknowns.` / `postings/frontiers.` shards; each backticked `*.json`/`*.md` key is in `relativeKeys`. Ceiling fixture `:44-54` same shard presence | PASS |
| APR-39 package, projection, run-certification, manifest-reachability, determinism, ceiling gates pass with no exclusions | validators run on this package | `PublicationResilienceAnalyzeTests.cs:191-228` `PackageValidator.ValidatePackageDirectory`; `FactualPackageReader.Read`; certification `passed`/`degraded` matches exit; every manifest path `File.Exists`; derived ceiling with only the existing single-record exemption; `:47-51` `validate` exit success/degraded; `:53` two-run digest (also APR-40) | PASS |
| APR-40 two default-budget runs produce identical package bytes | SHA-256 over relative paths+bytes equal | `PublicationResilienceAnalyzeTests.cs:53` `Assert.Equal(PackageDigest(first), PackageDigest(second))` | PASS |
| APR-41 optional Pitstop: 15 isolated project solutions plus full solution; absence does not fail CI | 16 skippable LocalCorpus rows; skip when file missing | `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs:106-118` one `pitstop.sln` row plus 15 hardcoded project `.csproj` rows; `:72-76` `$XunitDynamicSkip$` when missing; `:86-91` when present, writes `manifest.json`. Rows are `Category=LocalCorpus` and were excluded from this gate. **⚠️ Spec-precision**: APR-41 names “15 isolated project solutions”. The clone has one `pitstop.sln`. Isolated rows wrap each `.csproj` in a temp `.slnx` (`:121-139`). The assumption table says “15 isolated projects plus the full solution”. Operational outcome holds; there is no `// SPEC_DEVIATION` marker | PASS (spec-precision: see note) |
| APR-42 Pitstop/eShop/eShopOnContainers stay gitignored; diagnostic counts are not golden | gitignore entries; no exact diagnostic assert | `tests/Csharp2Md.Cli.Tests/Isolation/FixtureRetentionTests.cs:33-35` `Assert.Contains` `fixtures/eShop/`, `eShopOnContainers/`, `Pitstop/`; Pitstop test asserts exit is one of Success/Degraded/CertificationFailed and a manifest exists, not a diagnostic count. `git diff --stat 0c3999e..HEAD -- fixtures/eShop fixtures/eShopOnContainers fixtures/Pitstop` empty | PASS |

**Status**: All 42 ACs covered. 1 spec-precision gap flagged (APR-41 isolated rows synthesize a temp `.slnx` around each `.csproj`).

---

## Discrimination Sensor

**Sensor depth**: standing skip (same as every prior csharp2md feature)
**Result**: SKIPPED (standing user request for csharp2md, same as prior features). No git worktree, no file mutation, no Stryker.

Complementary `dotnet-test:test-gap-analysis` is **static reasoning only** (Step 4b mutations not applied). Label: unverified.

| Hypothetical mutation | Why it would be killed or survive | Unverified class |
| --------------------- | --------------------------------- | ---------------- |
| `ContainsRelationEmitter` skips document fallback | Builder tests `Assert.Single` a contains edge after proving own observations are only Invocation/DataAccess; `EvidenceScope.For` on an empty own-set throws | Killed |
| Qualifying inverted so Invocation stays | `EvidenceScopeTests.cs:57` `Assert.Equal([declaration], qualifying)`; builder `Assert.All` chain kinds | Killed |
| `SelectPostingBucket` always backticks the base key | APR-25/26 `DoesNotContain("`postings/unknowns.json`")` / frontiers; `ValidateNoAbsentKeys` would abort `Project` | Killed |
| `SplitTopLevel` splits inside `(...)` | `CanonicalSymbolSignatureTests.cs:142-148` exact three-slice array | Killed |
| Extra closer `Foo)` treated as unbalanced | production only throws when final `depth != 0`; extra closers are ignored. Tests cover unclosed openers only (`:221-227`). APR-22 “delimiter-unbalanced” is broader than T5’s `final depth != 0` | Survived (static, low: T5 defined the throw as depth ≠ 0) |
| `SelectPostingBucket` else-branch does not call `HasFamily` | missing posting family while the relation family exists would be worded as sharded. Unreachable if `PostingProjector` always emits unknowns/frontiers with those records. APR-30 is asserted on missing *relation* families (`:333-334`) | Equivalent / unreachable in the published path |

None of the static survivors is scored as an uncovered AC.

---

## Interactive UAT Results

| # | Test | Result | Details |
| - | ---- | ------ | ------- |
| 1 | Interactive UAT | Skip | CLI/backend generator. No user-facing UI. Automated gate is sufficient |

---

## Code Quality

Checked against `.claude/skills/tlc-spec-driven/references/coding-principles.md` and `CLAUDE.md` / `AGENTS.md` on diff `0c3999e..HEAD`.

| Principle | Status |
| --------- | ------ |
| Minimum code | Pass. Production is `Qualifying`, contains-scope selection, shared `SplitTopLevel`, `SelectPostingBucket`. Storage’s `<>`-only splitter was deleted, not duplicated |
| Surgical changes | Pass. Five `src/` files. RequirementCoverage allowlists gained `APR-` so new traits do not fail ROSE/TAX scanners |
| No scope creep | Pass. No new CLI flag, fact family, schema, ceiling exclusion, or taxonomy version. `design.md` remains untracked |
| Matches patterns | Pass. Diagnostic kebab-case, `$XunitDynamicSkip$` LocalCorpus, `CeilingCalculator.Derive`, `CliInvoke.RunAsync` |
| Spec-anchored outcome check (asserted values match spec) | Pass. Named fields (stage, detail, diagnostic code/id/message, signature `Value`, posting wording, package digest) are asserted, not call counts |
| Per-layer Coverage Expectation met (domain 1:1 ACs; routes happy+edge+error) | Pass. Domain split 1:1 to APR-16..23 shapes; emitter integration for APR-09..15; CLI vertical slice for APR-33..40 |
| Every test maps to a spec requirement - no unclaimed tests | Pass. New tests map to APR ids, listed edge cases, or T8 skeleton (APR-33). LocalCorpus Pitstop rows map to APR-41/42 |
| Documented guidelines followed: `CLAUDE.md` / `AGENTS.md`, `Directory.Build.props` `TreatWarningsAsErrors`, standing sensor skip | Pass |
| Would senior engineer approve? | Pass, with the APR-41 temp-`.slnx` wording recorded as spec-precision rather than a silent rewrite of “solutions” |

---

## Edge Cases

- [x] Symbol owns mixed structural and behavioral observations: keep only structural — `ContainsRelationEmitterTests.cs:257-269`
- [x] Document-scoped fallback still runs `EvidenceScope` so behavioral kinds never enter the chain — `ContainsRelationEmitterTests.cs:208-213` (production `ContainsRelationEmitter.cs:84` calls `EvidenceScope.For` on the selected candidates)
- [x] Sanitization reduces the message to empty or a redaction marker: exception type alone — `PipelineFailureDetailTests.cs:31`, `:71-73`, `:104-106`; production `PipelineFailureDetail.cs:37-39` treats `***` and `[REDACTED]` as empty
- [x] Only the unknown posting family shards; frontier stays exact — `RetrievalGuideProjectorTests.cs:242-251`
- [x] Confirmed-relation family already sharded keeps the non-backtick shard sentence — `RetrievalGuideProjectorTests.cs:137-145`
- [x] Comma inside `()`, `<>` or `[]` at depth > 0 is part of the current argument — `CanonicalSymbolSignatureTests.cs:142-200`

---

## Gate Check

- **Gate command**: `dotnet build`; then one `dotnet test` per csproj, with `--filter "Category!=LocalCorpus"` on Analysis and Cli (PowerShell `;`)
- **Build**: 0 warnings, 0 errors
- **Result**: 2134 passed, 0 failed, 0 skipped among selected tests
- **Per-project passed counts**:
  - `Csharp2Md.Domain.Tests`: 575 passed
  - `Csharp2Md.Analysis.Tests`: 864 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Storage.Tests`: 396 passed
  - `Csharp2Md.Cli.Tests`: 65 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Projection.Tests`: 234 passed
- **Test count before this feature (tasks.md live floor at Execute start)**: 2101
- **Test count after T13 (tasks.md floor)**: 2134
- **Delta**: +33 vs Execute-start floor; matches the T13 floor exactly. No silent deletion
- **Skipped tests**: none among the filtered gate. `LocalCorpusAnalyzeTests` (eShop, eShopOnContainers, Pitstop) excluded by `Category!=LocalCorpus`
- **Failures**: none

---

## LocalCorpus

`fixtures/Pitstop` is absent. `fixtures/eShop` is absent. `fixtures/eShopOnContainers` is absent.

Post-gate LocalCorpus run skipped. Not a feature FAIL. Clones were not added to git. APR-41 absence path is the skip at `LocalCorpusAnalyzeTests.cs:72-76`.

---

## Fix Plans

None. APR-41 is a spec-precision / unrecorded implementation detail (temp `.slnx` around each `.csproj` because the clone has one `pitstop.sln`), not an uncovered AC. No production or test edit from this verifier.

---

## Requirement Traceability Update

Verifier does not edit `spec.md` (read-only pass). Status below is this report’s judgment.

| Requirement | Previous Status | New Status |
| ----------- | --------------- | ---------- |
| APR-01..APR-40, APR-42 | Verified (tasks) | Verified in this report |
| APR-41 | Verified (tasks) | Verified, with spec-precision on “isolated project solutions” vs synthesized `.slnx` |

---

## Summary

**Overall**: Ready

**Spec-anchored check**: 42/42 ACs matched spec outcome | 1 spec-precision gap flagged
**Sensor**: SKIPPED (standing skip)
**Gate**: 2134 passed

**What works**: Unexpected pipeline failures stay unpublished, one-line and prior-package-preserving. Invocation-only builders publish document-scoped structural `contains`, or `contains-evidence-unqualified` without aborting. Nested tuples, generics and array ranks round-trip. `retrieval.md` no longer backticks absent unknown/frontier posting keys. Default-budget `analyze` of `fixtures/PublicationResilience` commits a valid, navigable, byte-deterministic package. SyntheticSolution bytes and labeled corpus trees are untouched.

**Issues found**: APR-41 wording vs Pitstop layout: isolated rows wrap each of 15 `.csproj` files in a temp `.slnx` rather than consuming 15 pre-existing solution files.

**Next steps**: Distill the APR-41 spec-precision lesson. Feature is ready for the orchestrator to mark done after `validate_state.py`.
