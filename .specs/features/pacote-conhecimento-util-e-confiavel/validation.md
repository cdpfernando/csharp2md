# Pacote de conhecimento útil e confiável — Phase 11 Validation

**Date**: 2026-09-17
**Spec**: `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`
**Diff range**: `997fb7a..d826c96` (Phase 11: `997fb7a` T73, `7545454` T74, `c5d69c3` T75, `926f5bb` T76, `e76894d` T77, `d826c96` T78)
**Verifier**: independent sub-agent (author ≠ verifier), read-only over the implementation

## Validation Verdict — PASS ✅

**Scope of this report**: the Phase 11 remediation of the retained dependency projection, anchored to
DEP-01, DEP-02, DEP-03 and PKG-05. It is not a re-verification of all 71 requirements.

---

## This report supersedes the prior validation.md, and why

The previous `validation.md` recorded a **PASS 71/71 measured before Phase 11**, and its DEP-01/DEP-02/
DEP-03/PKG-05 evidence was drawn almost entirely from `fixtures/SyntheticSolution`. That fixture cannot
discriminate the defect Phase 11 fixed: with two entities in a scope, a *complete* dependency graph and a
*correct* dependency graph are the same graph, so every assertion in the old report held while the real
projection published 289 of 289 possible component pairs on `fixtures/eShop` and 7 of 60 reachable
`ProjectReference` edges on the vendored corpus. The old PASS was therefore true of its fixture and false of
the product.

This verification is anchored instead to `fixtures/ArchitectureDependencyLab` and its normative oracle
(`oracle/project-references.json`, 87 declared `ProjectReference` edges across six solutions), which *can*
distinguish a complete graph from a correct one. The old report's other requirement rows are not restated
here; they were not re-measured and this report does not renew them.

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T73 emit project references from the resolved reference graph | ✅ Done | All 4 Done-when boxes checked; scope legitimately widened to two compounding aggregation bugs, recorded in the task's Decision |
| T74 exclude causal edges to symbols outside source | ✅ Done | All 4 boxes checked; a named residual (BCL-generic-wrapped user types) is written down, not dropped |
| T75 exclude test entities from retention | ✅ Done | One Done-when box was **revised while executing** (corpus leak drop deferred to T76) and the revision is stated in the task, not silently edited away |
| T76 recognize `.Testes` as a test-project path segment | ✅ Done | Fourth box revised while executing; the stricter wire-level check it surfaced became T77 |
| T77 exclude test occurrences from membership | ✅ Done | Added mid-phase; all 4 boxes checked |
| T78 correct the traceability and close the open finding | ✅ Done | Documentation only; `validate_state.py` cited as its gate |

No unchecked `- [ ]` box remains in T73–T78.

---

## Spec-Anchored Acceptance Criteria

Evidence-or-zero. Every row cites `file:line` in the real test tree plus the assertion expression, and the
asserted value is compared against the spec's own EARS outcome. Paths are relative to `D:/workspace/csharp2md`.

### DEP-01 — "The Retrieval Projection SHALL agregar Confirmed Relations nos escopos Document, Project, Component e Deployment Unit usando somente **pertencimento comprovado**"

| Sub-outcome the spec defines | `file:line` + assertion | Result |
| --- | --- | --- |
| Project-scope aggregation states exactly the *proven* references — no more (no unproven membership) and no fewer | `tests/Csharp2Md.Cli.Tests/OracleProjectReferenceScoreTests.cs:86-90` — `Assert.True(score.Correct == baseline.CorrectToday && score.FalsePositives == baseline.FalsePositivesToday && score.TestPolicyLeaks == baseline.TestPolicyLeaksToday, …)` over the six-solution theory, with baselines at `:59-66` | ✅ PASS |
| Corpus-wide the same, in one place | `:133-137` — `Assert.True(correct == BaselineCorrect && falsePositives == BaselineFalsePositives && leaks == BaselineTestPolicyLeaks, …)` where `BaselineCorrect = 60`, `BaselineFalsePositives = 0`, `BaselineTestPolicyLeaks = 0` (`:145-147`); corpus shape pinned at `:130-132` — `Assert.Equal(87, oracleEdges)`, `Assert.Equal(27, excluded)`, `Assert.Equal(60, reachable)` | ✅ PASS |
| Membership is not inflated by the workspace's transitive load | `tests/Csharp2Md.Core.Tests/Analysis/ProjectVariantWorkspaceTests.cs:44` — `Assert.Equal(["Acme.Shipping"], referenced)` for root `Acme.Shipping.Tests`, whose real chain is `Tests → Shipping → Shared.Contracts` | ✅ PASS |
| Evidence is not reattributed across roots citing the same target | `tests/Csharp2Md.Core.Tests/Analysis/CausalRelationExtractorTests.cs:241-247` — `Assert.NotEqual(firstEvidence[0], secondEvidence[0])` plus `Assert.Equal(CreateDocumentKey(solution, "src/Api/Api.csproj"), Assert.Single(first.Evidence).DocumentCanonicalKey)` and the `Worker` counterpart | ✅ PASS |
| A referenced project's occurrence belongs to itself, not to the citing root | `:258-260` — `Assert.Equal(target.CanonicalKey, occurrence.Project.CanonicalKey)` on the single occurrence of `src/Shared/Shared.csproj` | ✅ PASS |
| Component-scope membership is not the union of every project using a shared BCL symbol | `tests/Csharp2Md.Cli.Tests/OracleProjectReferenceScoreTests.cs:164-168` — `Assert.True(leaked.Length == 0, …)` where `leaked` = entity keys starting `symbol:`/`callable:`, over all six solutions | ✅ PASS |

**Non-vacuity check (performed, not assumed).** I re-derived the oracle independently of the code under test:
`fixtures/ArchitectureDependencyLab/oracle/project-references.json` holds 87 edges; grouping them and
excluding any edge whose source or target ends in `.Testes` gives per-solution reachable counts of
SistemaA 3, SistemaB 12, SistemaC 0, SistemaD 5, SistemaE 20, SistemaE.Copia 20 — **exactly** the
`CorrectToday` values in `RecordedBaselines:59-66`, totalling 60 with 27 excluded. So `CorrectToday` is not
an arbitrary defect level: it equals the *entire* reachable oracle. Combined with `FalsePositives == 0` and
`TestPolicyLeaks == 0`, the conjunction at `:86-90` asserts set equality (`produced == reachable`), not a
threshold. A tautological or always-true assertion cannot pass here: an empty projection scores 0 correct and
fails, and any extra edge scores a false positive or a leak and fails. The three `>=` / `<=` ratchet lines at
`:83-85` are individually weaker, but they are followed by the exact-equality conjunction, so the case as a
whole is exact.

### DEP-02 — "SHALL preservar separadamente Project Reference, invocação interna, uso estrutural de tipo, HTTP, gRPC, mensageria, contrato e persistência"

| Sub-outcome the spec defines | `file:line` + assertion | Result |
| --- | --- | --- |
| The eight categories are a closed, separate set | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetrievalContractTests.cs:24-40` — `Assert.Equal([ProjectReference, InternalInvocation, StructuralTypeUse, Http, Grpc, Messaging, Contract, Persistence], Enum.GetValues<DependencyCategory>())` | ✅ PASS |
| `project-reference` survives Phase 11 as its own category on the wire, separable from the other seven | `tests/Csharp2Md.Cli.Tests/OracleProjectReferenceScoreTests.cs:426-427` — the decoder selects only `scope == 1 && category == 0` and still recovers 60 of 60 edges, i.e. the category is preserved discretely in the committed package, decoded outside `Csharp2Md.Core` | ✅ PASS |
| `internal-invocation` and `structural-type-use` stay distinct and are *not* collapsed by T74's exclusion | `tests/Csharp2Md.Core.Tests/Analysis/CausalRelationExtractorTests.cs:28-30` (invocation, Callable→Callable) and `:52-55` (structural type use) still assert their own categories; `:108-109` — `Assert.Contains(result.Relations, r => r.Category == "internal-invocation")` proves a genuine cross-project call survives | ✅ PASS |
| Http / Grpc / Messaging / Contract remain separately emitted after the change | `:118-120` `Assert.Equal("http:payments/authorize", target.DisplayName)`; `:141-143` `Assert.StartsWith("grpc:", …)`; `:152` messaging; `:162-164` `Assert.Equal(EntityKind.Contract, target.Kind)` | ✅ PASS |
| Project-scope edges never name a project from another system (oracle rules 2 and 3) | `tests/Csharp2Md.Cli.Tests/OracleProjectReferenceScoreTests.cs:220-223` — `Assert.True(foreign.Length == 0, …)` per solution | ✅ PASS |

### DEP-03 — "The dependência agregada SHALL declarar origem, destino, escopo, categorias participantes, quantidade de ocorrências, variantes observadas, natureza direta ou transitiva e **referências às relações e evidências de origem**"

| Sub-outcome the spec defines | `file:line` + assertion | Result |
| --- | --- | --- |
| All eight declared members carry real values | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetrievalContractTests.cs:57-69` — `Assert.Equal(AggregationScope.Component, d.Scope)`, `Assert.Equal("src-1", d.Source.Value)`, `Assert.Equal("dst-1", d.Target.Value)`, `Assert.Equal(DependencyCategory.Http, d.Category)`, `Assert.Equal(3, d.OccurrenceCount)`, `Assert.Equal("var-1", Assert.Single(d.Variants).Value)`, `Assert.Equal("rel-1", Assert.Single(d.Relations).Value)`, `Assert.Equal("ev-1", Assert.Single(d.Evidence).Value)` | ✅ PASS |
| Direct and transitive natures are distinct | `:41-49` — `Assert.Equal(DependencyNature.Direct, direct.Nature)` / `Assert.Equal(DependencyNature.Transitive, transitive.Nature)` | ✅ PASS |
| The *origin* the aggregated edge declares is the real citing root — the Phase 11 defect | `tests/Csharp2Md.Core.Tests/Analysis/CausalRelationExtractorTests.cs:242-247` — each root's project-reference evidence resolves to that root's own `.csproj` document key, so two roots citing one target no longer collapse to one origin | ✅ PASS |
| Project-scope source/target are the proven owner and the proven container | `tests/Csharp2Md.Core.Tests/PackageBuilding/ScopePairingTests.cs:32-41` — `Assert.Equal(<project owning the evidence document>, Assert.Single(Edges(Project)).Source.Value)` and the target counterpart | ✅ PASS |
| Occurrence count equals the confirmed evidence count | `:51-52` — `Assert.Equal(1, Assert.Single(EdgesInto(TargetDocument)).OccurrenceCount)` | ✅ PASS |

### PKG-05 — "The pacote padrão SHALL excluir **testes**, observações sem promoção, **usos de tipo não retidos**, fontes sem citação, valores brutos de configuração e registros comuns em arquivos individuais"

| Sub-outcome the spec defines | `file:line` + assertion | Result |
| --- | --- | --- |
| Non-retained type uses (BCL/NuGet/metadata symbols) produce no relation and no entity | `tests/Csharp2Md.Core.Tests/Analysis/CausalRelationExtractorTests.cs:64-65` — `Assert.DoesNotContain(result.Relations, r => r.Category == "internal-invocation")` + `Assert.DoesNotContain(result.Entities, e => e.DisplayName == "Trim")`; `:74-75` the `string` type-use counterpart | ✅ PASS |
| …and the same holds on a real, multi-project corpus | `tests/Csharp2Md.Cli.Tests/OracleProjectReferenceScoreTests.cs:164-168` — zero `symbol:`/`callable:` entity keys in all six solutions | ✅ PASS |
| A root whose every occurrence is test-owned is excluded by default | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetainedGraphBuilderTests.cs:46` — `Assert.DoesNotContain(result.Entities, e => e.CanonicalKey == "entity:root")` for `rootProjectPath: "App.Tests/App.Tests.csproj"` | ✅ PASS |
| An incoming (dependent) edge from a test-only source is excluded by default | `tests/Csharp2Md.Core.Tests/PackageBuilding/RetentionPolicyTests.cs:35` — `Assert.DoesNotContain(Apply(callerIsTestProject: true).Relations, x => x.CanonicalKey == "relation:caller-root")`, while `:16-18` keep the non-test dependent retained unchanged | ✅ PASS |
| A retained production entity's membership is not widened by a test-side occurrence | `tests/Csharp2Md.Core.Tests/PackageBuilding/ScopePairingTests.cs:72-73` — `Assert.DoesNotContain(edges, e => e.Scope == Document && e.Target.Value == TestDocument)` and the Project-scope counterpart | ✅ PASS |
| The heuristic recognizes the corpus's own naming, and does not over-match | `tests/Csharp2Md.Core.Tests/Analysis/SourceInventoryTests.cs:196` (theory rows `:191-193`: `src/SistemaA.Testes/Foo.cs → true`, `src/Testemunho/Foo.cs → false`, `src/Manifesto/Foo.cs → false`); `:207-208` — `Assert.True(IsTestProject(testProject))` / `Assert.False(IsTestProject(productionProject))` | ✅ PASS |
| End to end, the committed default package names no `.Testes` project anywhere | `tests/Csharp2Md.Cli.Tests/OracleProjectReferenceScoreTests.cs:189-193` — `Assert.True(leaked.Length == 0, …)` where `leaked` = entity keys containing `.Testes`, over all six solutions | ✅ PASS |
| Opt-in arm (`--include-tests`) genuinely re-admits what default mode excludes | `RetainedGraphBuilderTests.cs:53`, `RetentionPolicyTests.cs:38`, `ScopePairingTests.cs:80` — each `Assert.Contains` the exact item its default-mode sibling asserts absent | ✅ PASS |

**Non-vacuity check on the two corpus-wide `leaked.Length == 0` cases.** These would pass silently if the
decoder found no entities at all for a solution. For SistemaA/B/D/E/E.Copia the oracle score independently
proves the entity tables are populated and correctly indexed (the edges are decoded *through* them). SistemaC
scores 0 by design, so I measured it directly: I ran `analyze` on `SistemaC.slnx` alone into a scratch
directory and read its committed shards — **37 entities, 0 naming `.Testes`, 0 `symbol:`/`callable:`**. The
assertions are therefore substantive for all six solutions. (Scratch output deleted; no repository file was
touched.)

**Status**: ✅ All four in-scope ACs matched their spec-defined outcome. 0 spec-precision gaps.

---

## Discrimination Sensor

**Skipped**, per the standing project rule in `AGENTS.md`: the automated mutation/fault-injection sensor is a
permanent skip for this project and the user runs Stryker manually. This is not a per-feature decision and was
not re-litigated.

In its place, each of T73–T77 records a *hand-run* fault injection. I spot-checked three of those claims by
reconstructing the pre-fix code from git and re-deriving the outcome by inspection against the real fixtures.
No mutation was applied to the working tree; `git status --porcelain` before and after this verification is
identical (`M AGENTS.md`, `M docs/specs/pacote-conhecimento-util-e-confiavel.md` — both pre-existing and
untouched by me).

| # | Claim spot-checked | Reconstruction | Verdict |
| - | ------------------ | -------------- | ------- |
| 1 | T73: reverting `ReferencedProjects()` to `Solution.Projects.Where(p => p.Id != RootProject.Id)` kills `ReferencedProjects_NamesOnlyTheDirectReference_NotATransitiveOne` | `ProjectVariantWorkspace.OpenAsync` calls `MSBuildWorkspace.OpenProjectAsync`, which loads the root's **transitive** project closure into `CurrentSolution`. `fixtures/SyntheticSolution` really does hold `Acme.Shipping.Tests → Acme.Shipping → Acme.Shared.Contracts` (verified in the two `.csproj` files). The reverted expression therefore returns `{Acme.Shipping, Acme.Shared.Contracts}` against an assertion of exactly `["Acme.Shipping"]` | ✅ Claim holds |
| 2 | T74: removing the `IsDeclaredInAnalyzedSource` guard kills `Extract_InternalInvocation_ToABclMethod_IsNotRetained` and `Extract_StructuralTypeUse_OfABclType_IsNotRetained` | Without the guard both call sites run `AddSymbol` unconditionally on the resolved target. The `Extract` helper (`CausalRelationExtractorTests.cs:281-298`) references `typeof(object).Assembly`, so `"x".Trim()` and the `string` parameter type resolve to real metadata symbols and would emit an `internal-invocation` / `structural-type-use` relation and entity — exactly what the two `Assert.DoesNotContain` calls forbid | ✅ Claim holds |
| 3 | T77: reverting `NonTestOccurrences` to return `graph.Occurrences` unconditionally kills `Build_TargetMembershipExcludesATestProjectOccurrenceByDefault` | `PackageBuilder.Pairs` crosses `origin.Documents × target.Documents` and `origin.Projects × target.Projects`. Unfiltered, `entity:target`'s test-side occurrence (locator `App.Tests/TargetCalledFromTest.cs`, project `App.Tests/App.Tests.csproj`) enters its membership, producing precisely the Document- and Project-scope edges the test asserts absent | ✅ Claim holds |

I also confirmed the fourth claim's mechanism incidentally: `RetainedGraphBuilderTests.Graph(rootProjectPath:)`
gives `entity:root` a single, test-owned occurrence, so without the `(includeTests || !IsTestOnly(...))` guard
it is retained as a root and T75's case fails.

**Sensor depth**: skipped by project rule; 3 of 5 hand-run claims independently corroborated by inspection.
**Result**: PASS ✅ — no claim was found overstated.

---

## Payload / Conjunction Rule

Checked against the new and changed assertions in the diff. No new test asserts only "a call happened" or "a
collection is non-empty":

- Every counting assertion in `OracleProjectReferenceScoreTests` compares against an exact integer that I
  re-derived from the oracle file independently of the product code (60 / 0 / 0; 87 / 27 / 60).
- Every default-exclusion case has a matching opt-in case asserting the *same* named item is present — the
  pair rules out an implementation that simply drops everything.
- `Extract_ProjectReference_EvidenceKeyNamesBothTheCitingRootAndTheTarget` asserts the two evidence keys
  differ **and** each resolves to a specific, named document key; asserting only inequality would have been
  the shallow version.
- `Extract_ProjectReference_TargetEntityOccursInItselfNotInTheCitingRoot` asserts the occurrence's owning
  project equals the exact referenced-project canonical key, not merely that an occurrence exists.
- `ReferencedProjects_NamesOnlyTheDirectReference_NotATransitiveOne` uses `Assert.Equal` on the whole
  sequence, not `Assert.Contains`.
- `SourceInventoryTests`' new theory rows include two negatives (`Testemunho`, `Manifesto`) that pin the
  literal `.` separator, so a sloppy `Contains("Testes")` implementation fails.

---

## Code Quality

| Principle | Status | Note |
| --------- | ------ | ---- |
| Minimum code | ✅ | Six production files, 123 changed lines total; each change is the admission check or the payload the defect required |
| Surgical changes | ✅ | `ReferencedProjects()` swaps one expression; `AddEntity`/`AddNamed` gain one optional `owner` parameter; three call sites gain a boolean |
| No scope creep | ✅ | No new abstraction, no configuration, no flag beyond the `includeTests` already threaded through `PackageBuilder` |
| Matches patterns | ✅ | `SourceInventory.IsTestProject` reuses `LooksLikeTestDocument` rather than reimplementing it; the filter is threaded, not duplicated as a policy object |
| Spec-anchored outcome check | ✅ | See the AC tables; every asserted value matches the spec's own wording |
| Every test maps to a requirement | ✅ | All 16 new cases carry a `Requirement` trait (PKG-05, PKG-08, DEP-01, DEP-02) |
| Comments explain *why* | ✅ | Each production change carries a comment naming the requirement and the failure mode it prevents |
| Documented guidelines followed | ✅ | `AGENTS.md` (sensor skip honoured and noted in the tasks; `net10.0`; no `Microsoft.Build.*`; no `MSBuildLocator.RegisterDefaults()`); Context7 consulted for the two Roslyn APIs used (`Project.ProjectReferences`, `ISymbol.Locations.IsInSource`), as the tasks record |

Two small, deliberate duplications exist and are acceptable: `RetainedGraphBuilder.IsTestOnly` and
`RetentionPolicy.IsTestOnlySource` are the same four-line local function in two files. Extracting a shared
helper would be the larger change; both delegate to the one real predicate (`SourceInventory.IsTestProject`),
so the rule itself is single-sourced.

---

## Edge Cases

- [x] **EDG-04** (a dependency existing in only one Analysis Variant keeps its qualification without
  duplicating source/target identities) — unaffected: the variant handle still comes from the evidence record
  (`PackageBuilder.Contributions`), and T73 changed only the evidence *payload*, not its variant.
- [x] **EDG-01** (a relation without resolvable evidence is omitted or the plan is rejected) — still enforced;
  `RetainedGraphBuilderTests.cs:57-63` (four `invalid-confirmed-relation` cases) pass unchanged with the new
  `includeTests` parameter.
- [x] An entity with **zero** occurrences is not treated as test-owned: `owners.Length > 0 && owners.All(...)`
  in both retention sites, so an unprovable owner never causes silent exclusion — consistent with DEP-01's
  "somente pertencimento comprovado".
- [x] A same-solution `ProjectReference` target is **not** swept up by T74's metadata exclusion
  (`CausalRelationExtractorTests.cs:80-109`, a genuine two-`Compilation` case).

---

## Gate Check

Run independently by this Verifier, on `d826c96`, in the real working tree.

- **Build**: `dotnet build csharp2md.slnx --configuration Release` → exit 0, 0 warnings, 0 errors.
- **Tests**: `dotnet test csharp2md.slnx --configuration Release --no-build` → exit 0.
  - `Csharp2Md.Core.Tests`: **637 passed, 0 failed, 0 skipped** (637 total).
  - `Csharp2Md.Cli.Tests`: **96 passed, 0 failed, 2 skipped** (98 total).
- **Oracle filter**: `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --configuration Release --no-build --filter "Category=OracleCorpus"` → **11 passed, 0 failed** (6 test methods; `ProjectReferences_HoldTheRecordedDefectBaseline` is a 6-case theory). Score held at 60 correct / 0 false positives / 0 test-policy leaks.
- **Total**: 733 passed, 0 failed, 2 skipped.
- **Skipped tests, justified**: `LocalCorpusAnalyzeTests.Analyze_eShopOnContainers_CommitsWithinFileAndByteCeilings` and `…Analyze_Pitstop_…` — the two local clones are absent. CRT-07 requires exactly this behaviour ("a ausência dos clones SHALL não falhar CI"), so these are expected skips, not failures. `fixtures/eShop` **is** present; its case ran and passed.
- **Test count before Phase 11**: 621 (Core) / 94 (Cli). **After**: 637 / 96. **Delta**: +16 Core, +2 Cli. No test was deleted or weakened; the only edits to existing cases are the mechanical `includeTests:` argument additions and one theory that gained three rows.
- **Failures**: none.

---

## Requirement Traceability Update

No change proposed. T78 already moved DEP-01, DEP-02, DEP-03 and PKG-05 from an unsupported `Complete` to a
`Complete` that cites the measuring test, and this verification confirms those citations are real and
non-vacuous.

| Requirement | Previous status | Status after this verification |
| ----------- | --------------- | ------------------------------ |
| DEP-01 | Complete (measured, T78) | ✅ Verified — 60/60 reachable oracle edges, 0 false positives |
| DEP-02 | Complete (T78) | ✅ Verified — eight categories closed and separately decodable off the wire |
| DEP-03 | Complete (T78) | ✅ Verified — all declared members asserted with exact values; origin defect closed |
| PKG-05 | Complete (measured, T78) | ✅ Verified — 0 `.Testes` entities and 0 out-of-source `symbol:`/`callable:` entities across six real solutions |

Requirements outside this phase's scope (PUB-08 `Partial`, CRT-04/CRT-05 `Unverified`) were **not**
re-measured here and keep their recorded status.

---

## Non-blocking observations

Ranked; none of these changes the PASS verdict and none is a coverage gap.

1. **`STATE.md`'s Handoff is stale by one commit.** It still reads "T78 … in progress" and lists
   `.specs/STATE.md` / `spec.md` as uncommitted, which was true as T78 was being written but is not true at
   `d826c96`. Cosmetic; the substantive RESOLVED sections are accurate.
2. **Two new cases carry a `PKG-08` trait that does not match PKG-08's spec outcome.**
   `RetainedGraphBuilderTests.cs:50` and `RetentionPolicyTests.cs:37` (plus `ScopePairingTests.cs:77`) prove
   the `--include-tests` *behaviour*, whereas PKG-08's EARS text is about recording that choice "no manifesto
   e na identidade da execução" — which is covered instead by
   `SourceInventoryTests.cs:122-136`. These are really the opt-in arm of PKG-05. This echoes the first
   Verifier's deferred "misplaced traits" finding; it costs nothing today but makes trait-based coverage
   reports slightly misleading.
3. **`OracleProjectReferenceScoreTests.ProjectReferences_HoldTheRecordedDefectBaseline` keeps a name that no
   longer describes it.** It holds a *correct* state, not a defect baseline; the class already renamed
   `RecordedDefects` → `RecordedBaselines` and its local variables still read `defect`. Naming only.
4. **The three ratchet lines at `:83-85` are redundant** now that `:86-90` asserts exact equality at the
   ceiling. Harmless (they give a distinct first failure message), but a reader could mistake the case for a
   one-sided threshold. The class's own doc comment already says this.
5. **T76's gate note says "up from 631 by this task's 2 focused cases" while the count moved 631 → 635.** The
   task added one `[Fact]` plus three `[InlineData]` rows — four cases. The total (635) and the final 637 are
   both correct and independently confirmed; only the parenthetical is imprecise.

---

## Summary

**Overall**: ✅ Ready

**Spec-anchored check**: 4/4 in-scope ACs matched their spec-defined outcome; 0 spec-precision gaps.
**Sensor**: skipped per `AGENTS.md`; 3 hand-run discrimination claims spot-checked and all corroborated.
**Gate**: 733 passed, 0 failed, 2 justified skips; Release build 0 warnings / 0 errors.

**What works**: the Project-scope `ProjectReference` projection now reproduces the vendored corpus's oracle
exactly — 60 of 60 reachable edges, nothing invented, nothing leaked from a test project — and the retained
graph carries no BCL/framework symbol and no `.Testes`-owned entity in any of the six solutions. The three
independent test-admission points (root selection, incoming-edge admission, target-side membership lifting)
are each pinned by a default-exclusion case paired with an opt-in case.

**Issues found**: none blocking. Five naming/annotation/staleness observations are listed above.

**Next steps**: Phase 11 is verified. The open questions this report does not touch remain open: PUB-08 is
still `Partial`, CRT-04/CRT-05 are still `Unverified` pending the absent clones, and the named residuals in
`STATE.md` (BCL-generic-wrapped user types; legitimately shared in-solution types fanning out at Component
scope) are recorded, not closed. A full 71-requirement re-verification against
`fixtures/ArchitectureDependencyLab` — rather than `fixtures/SyntheticSolution` — is the natural successor to
this report.
