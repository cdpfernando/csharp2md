# STATE

## Decisions

### AD-001
- **Decision**: O produto terá somente `Csharp2Md.Core`, com módulos internos de Analysis, PackageBuilding e Publication, e `Csharp2Md.Cli` como seam externo.
- **Reason**: A topologia concentra complexidade atrás de uma facade pequena, elimina dependências invertidas e não exige contratos públicos entre papéis internos.
- **Trade-off**: A substituição move ou remove a maior parte da estrutura atual e produz fases intermediárias deliberadamente incompletas.
- **Scope**: Todo código de produto, testes e features futuras do gerador.
- **Date**: 2026-09-15
- **Status**: active

### AD-002
- **Decision**: A publicação usará gerações imutáveis e comprometerá uma geração validada pela troca atômica do `manifest.json` raiz sob lock.
- **Reason**: Um único commit marker garante que leitores vejam a geração anterior ou a nova depois de reidratação e validação completas.
- **Trade-off**: O layout mantém um diretório de geração e requer coordenação de lock e limpeza pós-commit.
- **Scope**: Materialização, leitura, validação e substituição de pacotes locais.
- **Date**: 2026-09-15
- **Status**: active

### AD-003
- **Decision**: Variantes serão descobertas sem `TargetFramework` global e analisadas em workspaces separados por projeto raiz e target framework avaliado.
- **Reason**: Propriedades globais de solução contaminam projetos; o seam por projeto preserva a avaliação real e ainda fornece semântica de Project References.
- **Trade-off**: Projetos referenciados podem ser carregados mais de uma vez e o tempo total de análise pode aumentar.
- **Scope**: Carregamento Roslyn, identidade de variante e análise multi-target.
- **Date**: 2026-09-15
- **Status**: active

## Handoff

- **Feature**: `pacote-conhecimento-util-e-confiavel` / `.specs/features/pacote-conhecimento-util-e-confiavel`
- **Phase / Task**: Phase 12 (close the Component/DeploymentUnit-scope fan-out), **T79 committed, complete, and independently re-verified PASS**. This phase was opened by the fix→re-verify loop after the queued full-scope Verifier (dispatched per Phase 11's own next step) returned **FAIL**: 70 of 71 ACs matched, DEP-01 failed at Component/DeploymentUnit scope (138/144 false pairs on `fixtures/eShop`). T79 fixed it. **The queued second full-scope Verifier (iteration 2 of the bounded 3-iteration fix→re-verify loop) has now run and returned PASS**: 71/71 ACs + 5/5 edge cases, gate green (Core 640/640, Cli 97+2 skips, `Category=OracleCorpus` 60/0/0 held, `Category=LocalCorpus` 5 passed/2 skipped — the new eShop density case ran and passed), `validate_state.py` exit 0. It re-measured DEP-01's fix independently (own decoder, not T79's gate note) and confirmed 12/144 self-pairs, 0 cross-component, at both Component and DeploymentUnit scope. Full report at `.specs/features/pacote-conhecimento-util-e-confiavel/validation.md`. **The feature is functionally done; two new non-blocking Major gaps the Verifier found (not part of DEP-01's scope) are open for the user's call — see below.**
- **Completed**: T1-T67, T69-T79. Phase 10: `bd901e4` (T70), `1386f9e` (T71), `aed46f5` (T72). Phase 11: `997fb7a` (T73), `7545454` (T74), `c5d69c3` (T75), `926f5bb` (T76), `e76894d` (T77), `d826c96` (T78). Phase 12: `2457702` (T79).
- **RESOLVED (Phase 12, T79): the Component/DeploymentUnit-scope fan-out the Verifier measured is closed, on the same real corpus.** Re-measured directly on `fixtures/eShop` after the fix: **12 of 144 possible component pairs (8.3%), all 12 self-pairs, 0 cross-component false positives** — down from 138/144 (95.8%). `Ordering.API`'s outgoing Component-scope set is now just itself; none of the eleven other components (the six the Verifier named plus Catalog.API, Identity.API, Webhooks.API found while fixing) appear. `LocalCorpusAnalyzeTests.Analyze_eShop_ComponentScopeHasNoCrossComponentFalsePositive` (Cli.Tests, `Category=LocalCorpus`) now holds this at zero, gated so its absence never fails CI per CRT-07.
- **Root cause, confirmed by direct measurement in three passes, not by inspection alone**: a symbol's occurrence (and, for two of three sites, its canonical key) was attributed to whichever project happened to *observe* it rather than to the project that actually *declares* it — the same defect shape T73 already fixed once for `ProjectReference` targets, recurring independently at three sites: (1) `CausalRelationExtractor.AddSymbol` defaulted every target symbol's occurrence owner to the citing root, missing genuinely shared library symbols (e.g. `eShop.ServiceDefaults`'s extensions); (2) every Callable/Symbol canonical key was built from `symbol.ToDisplayString()` alone, and Roslyn renders a top-level-statements entry point identically (`<top-level-statements-entry-point>`) in every project regardless of assembly, so every host's own `Program.cs` collided onto one entity — the dominant driver, since nearly every top-level call shares this one source; (3) `ConfigurationPersistenceExtractor`'s `DataStore`/`DataOperation`/`DataObject` entities had the same two defects one layer down — eShop's own template scaffolds an identically-named, identically-namespaced `MigrateDbContextExtensions` helper independently into every service, which collided the same way as (2). Each was found by re-measuring the real eShop clone after the previous fix showed no change, not by reasoning about the code alone — a lesson worth keeping: a plausible single root cause does not guarantee the defect is closed.
- **Fix**: a new `SymbolOwnership.Resolve` (shared by both extractors) resolves a symbol's true declaring project by exact `SyntaxTree` object identity against the root's own compilation and its directly-referenced projects' compilations, falling back to the citing project only when unresolved, never excluding. Every affected entity kind's canonical key now folds in the resolved owner via a new `CanonicalIdentity.CreateEntityKey(solution, kind, owner, name)` overload, so two textually-identical but physically-distinct symbols never collide while a genuinely shared symbol still unifies to one entity.
- **Gate after T79**: Release build 0 warnings / 0 errors; `Csharp2Md.Core.Tests` **640/640**; `Csharp2Md.Cli.Tests` **97 passed / 2 skipped**; `Category=OracleCorpus` unchanged at 60/0/0 (this fix touches Callable/Symbol/DataStore/DataOperation/DataObject entities only, not Project-scope `ProjectReference` attribution).
- **Only one clone is present**: `fixtures/eShop`. Pitstop and eShopOnContainers are absent, so their `LocalCorpusAnalyzeTests` cases skip by name. The eShop cases run and pass.
- **Other findings from the first full-scope Verifier, not yet acted on** (ranked Major/Minor, none blocking): (a) `fixtures/ArchitectureDependencyLab`'s six solutions each have exactly one Component/DeploymentUnit, so the oracle corpus cannot discriminate *any* scope above Project — `oracle/scenarios.json`'s 63 scenarios have no test consumer at all, only `project-references.json` is scored; a future task should add a multi-host solution to the corpus and score against `scenarios.json`. (b) Seven of DEP-02's eight categories (all but `project-reference`) have no corpus-level accuracy measurement — separation is proven, recall is not, and no AC currently requires it (spec-precision gap, not a failure). (c) STO-07's determinism is proven against a fixed in-memory model, never against build state, which this file already records as non-deterministic (SistemaA cold vs. warm: 80 vs. 115 aggregated dependencies). (d) 78 test cases carry no `Requirement` trait, including the CLI suites that solely own CRT-07 and CRT-09; a few traits are misplaced (e.g. `PublicationSafetyScannerTests.cs:44`'s PUB-06 case traited CRT-08). (e) `OracleProjectReferenceScoreTests.ProjectReferences_HoldTheRecordedDefectBaseline` still names a "defect baseline" for a state that is correct at Project scope. (f) T67's `tasks.md` Done-when boxes are stale (unchecked) though the work itself was verified done differently.
- **Two new findings from the second (PASS) Verifier, neither blocking, both Major**:
  1. **`ProjectReference` `nature: Direct` is overclaimed on eShop.** 8 of 35 published direct edges (every `X → EventBusRabbitMQ` consumer) are transitive-only — Roslyn's `Project.ProjectReferences` is already the resolved closure, not the direct-declaration list `design.md:222` requires. The prior (first) Verifier's report cited this exact 6-target set as proof its decoder read correctly, so the overclaim predates T79 and was walked past twice.
  2. **The T79 ownership defect is still live at a fourth site.** `ArchitectureFactExtractor.cs:120,143,151,192` keys Symbol/EntryPoint/BoundaryOperation entities on bare `ToDisplayString()`, the same pattern T79 fixed in `CausalRelationExtractor` and `ConfigurationPersistenceExtractor`. Dormant on eShop (untested), but it splits one physical type into two entity identities depending on which extractor emits it first.
  - Also downgraded: Gap 3 from the first report (`ScopePairingTests` encoding the defect shape) is now Minor — `Build_ComponentScopeAggregatesAGenuineCrossComponentRelation` would still pass pre-fix since `PackageBuilder` itself was untouched, so it isn't a regression guard; the real guards are the new eShop CLI case plus the two strengthened unit tests.
  - Worth watching, not a gap: Component scope is now correct but nearly empty (12 self-pairs, zero inter-component edges across all 12 eShop services), and no committed gate would notice if it silently went to zero.
- **Next step**: none required to close DEP-01 — it's done. The two new Major gaps above are candidates for a Phase 13, at the user's discretion; neither fails a written AC today.

### What the first Verifier found, and what Phase 8 did about it

The first Verifier returned FAIL on `ff42c35..71e7094` with 9 ranked gaps and 64/71 ACs matching their spec outcome.

- **Its blocker was re-scoped before Phase 8 opened.** It read CRT-04 as "the package exceeds its own size contract", measuring eShop at 78.61 MiB against 64 MiB. CRT-04 pins that ceiling for **eShopOnContainers**, not eShop, and the spec sets no size ceiling on eShop at all. There was no measured violation. The real defect was EDG-03's and is now closed by T60.
- T60: `PackageBudget.ForCorpus` picks the limit from the corpus by solution file; eShopOnContainers resolves to 1,500 / 64 MiB and Pitstop to 750 / 25 MiB. Multiple pinned corpora take the componentwise minimum. The 96 MiB default is now only the fallback for a corpus the spec pins nothing on, so T58's decision to raise it rather than bound the document pages is preserved.
- T61: `PublicationMeasurements` gained `BySolution` (attributed by the `solutions/{id}/` prefix) and `Corpus` (carrying the ceiling that was applied). The EDG-03 diagnostic now names the corpus, which `design.md:575` required and T57 had left half done.
- T62: STO-01's derivation is pinned by two literal IDs computed outside the codebase plus one independent recomputation. The old grammar/determinism cases held for any digest, slice or alphabet.
- T63: NAV-07's 8-read / 12,000-token arm was never executed, because every case built `component:` roots. A theory now covers deployment, entrypoint and boundary roots.
- T64: DEP-05's multi-scope reuse is asserted on all four scopes plus a payload-written-once check; NAV-02 now asserts a Deployment Unit row that resolves to a written artifact.
- T65: `EnsureValid` was discarding the `Family` and `Artifact` the validator had already derived, so the analyze path could not populate PUB-08's coordinates. Fixed, plus `FamilyFor` gained the three publication trailers and `/measures/`.
- T66: CRT-02 now states the causal-root precondition the certifier and its test already relied on; the traceability table reads **68 Complete, 1 Partial (PUB-08), 2 Unverified (CRT-04, CRT-05)**.

### Open items the Verifier should weigh

- **PUB-08 is Partial on purpose.** `KnowledgePackageFailureTests` still feeds a hand-built `EngineDiagnostic` through a CLI stub for all nine rejection classes. T65 proved the engine populates the coordinates on two real pipeline failures, but no single test spans render-and-populate. Driving all nine for real needs a fixture per rejection class.
- **Deferred, not dropped**: the first Verifier's Fix 8 - 80 test cases with no `Requirement` trait, including the CLI suite that solely owns CRT-04/05/07/09, plus four misplaced traits at `PackageBuilderTests.cs:10-13`. It blocks no acceptance criterion and was left out of Phase 8.
- **CRT-04 and CRT-05 remain unmeasured end to end.** The builder enforces both ceilings and unit cases assert the spec's numbers, but re-run the LocalCorpus filter when the clones return.
- **`PackageBudget.Default` at 96 MiB is still an AD candidate.** It was raised in T58 and narrowed in meaning by T60; no AD records it.

### RESOLVED (Phase 11, T74/T75/T77): the component dependency graph is complete, and the package is factually wrong

**Fixed.** `Entities_NeverRetainABareSymbolOrCallableFromOutsideTheAnalyzedSource` and `Entities_NeverNameATestesProjectByDefault` (Cli.Tests, `Category=OracleCorpus`) now hold this closed on the real corpus: zero `symbol:`/`callable:` entities from outside the analyzed source, and zero entities naming a `.Testes` project, in any of the six solutions. T74 excluded causal edges to symbols with no source declaration (`ISymbol.Locations.IsInSource`); T75/T77 excluded test-project roots, incoming edges and target-side membership lifting. The finding below is kept verbatim as the record of what was measured and why, not as a current defect.

Measured on the real `fixtures/eShop` clone on 2026-09-17, after the second Verifier returned PASS 71/71.
**This is the highest-priority item in the feature and it invalidates the usefulness of the shipped output.**

**Symptom.** Every one of the 71 component pages carries the identical shape: 52 outgoing edges, 17 distinct
targets, 14 `ProjectReference`. A direct measurement over the analysed graph produces **289 of 289 possible
component pairs, including all 17 self-pairs** - the complete graph. It carries no information.

**The claims are false, not merely noisy.** `src/Ordering.API/Ordering.API.csproj` really references exactly
five projects: `EventBusRabbitMQ`, `IntegrationEventLogEF`, `eShop.ServiceDefaults`, `Ordering.Domain`,
`Ordering.Infrastructure`. **None of the five appears** as a dependency of it in the package, and all 14
`ProjectReference` edges it does publish are false. For `eShop.AppHost` and the test projects the direction is
inverted: they reference `Ordering.API`, not the reverse.

**Root cause, measured.** Primitive and BCL symbols are retained as graph entities and occur in every project,
so their component membership is all 17. Component-scope lifting then emits `source.Components x
target.Components`:

| Category | Endpoint pairs contributed |
| --- | --- |
| `structural-type-use` | 1,206,044 |
| `internal-invocation` | 296,218 |
| `project-reference` | 1,464 |
| everything else | < 800 |

The worst individual relations fan out 13x17 = 221 pairs each, targeting `symbol:string` and `symbol:int`.
`string`, `int`, `System.Threading.Tasks.Task`, `bool` and `CancellationToken` are each attributed to all 17
components; 1,332 entities are attributed to more than one. The entity `solution:eShop:eShop.slnx` is itself
attributed to 17 components.

PKG-05 already forbids this: it excludes "usos de tipo nao retidos". A `structural-type-use` edge to `string`
is exactly an unretained type use, and retention is not filtering it.

**Second, separate leak.** The manifest records `include_tests: false`, yet 12 test projects are published as
Component and DeploymentUnit roots and the summary carries 154 `UnitTests`/`FunctionalTests` mentions. Also
PKG-05.

**Why the whole suite is green and two Verifier runs passed.** Every dependency-scope acceptance test runs on
`fixtures/SyntheticSolution`, which has two entities. At that size a complete graph and a correct graph are
indistinguishable. `ScopePairingTests.Build_ComponentScopeKeepsItsMembershipDerivation` even asserts a
**self-edge** (`entity:component -> entity:component`) as correct behaviour. T64 proved DEP-05 against the same
fixture and passed, because multi-scope reuse holds in a degenerate graph too. No fixture in the tree can
discriminate a correct projection from this one.

**Done (T78).** `spec.md`'s DEP-01, DEP-02, DEP-03 and PKG-05 rows now name the fixing tasks (T73-T77) and cite the
measured test that backs `Complete`, rather than stating it unsupported as this finding originally recorded.
`validation.md` (the Verifier's own report) still reflects the pre-Phase-11 PASS and was not rewritten — a fresh
Verifier run against the corrected projection is the natural next check, not a hand-edit of the old report.

**Reproduce**: the throwaway diagnostic that produced these numbers is kept outside the repo at
`<scratchpad>/eshop-fanout-diagnostic.cs`; drop it into `tests/Csharp2Md.Core.Tests/`, run its single fact, and
delete it again. It calls `SolutionAnalyzer.AnalyzeAsync` on the eShop clone directly and takes about 90
seconds.

**Deprioritised by the user on 2026-09-17**: package file size and byte ceilings. T68 is parked for the same
reason. Correctness of the projection comes first.

**Known residual, not closed by Phase 11 (named, not dropped).** A constructed generic's `Locations` resolve to
its unbound original definition, so `List<Foo>` (a BCL container of a user type `Foo`) is excluded from
`structural-type-use` exactly like a bare BCL type would be — `Foo`'s own direct uses elsewhere are unaffected,
but a type reachable *only* wrapped in a BCL generic produces no edge. This one is still open; no measurement
has shown it live.

**RESOLVED (Phase 12, T79).** The second half of this residual — a legitimately shared in-solution symbol
fanning its Component-scope membership out to every component that uses it — was flagged here as "no
measurement showed that as a live defect." A full-scope Verifier's direct measurement on `fixtures/eShop`
showed exactly that: 138 of 144 possible component pairs (95.8%). `SymbolOwnership.Resolve` (T79) now
attributes a symbol's occurrence and canonical key to its real declaring project by exact `SyntaxTree`
identity, closing it to 0 cross-component false positives on the same corpus. See the Phase 12 section above
for the full mechanism.

### The corpus is now versioned as `fixtures/ArchitectureDependencyLab` (Phase 10)

`D:\workspace\projetosintetico` is the user's own synthetic corpus, purpose-built to calibrate C#/.NET
architecture analysers. **It is the instrument this project has been missing.** Zero commits, no remote, so it
is local material with no vendoring or licensing question.

- 6 `.slnx` solutions, 41 projects, **an oracle of exactly 87 `ProjectReference`** plus **63 scenarios**
  (`oracle/scenarios.json`) with five expected states: `confirmed` 38, `absent` 12, `candidate` 10,
  `open-frontier` 2, `unknown` 1. The 12 `NEG-*` are lookalikes that must be reported absent.
- **593 KB / 171 files clean** (the 489 MB on disk is all `bin`/`obj`). The current versioned fixture,
  `fixtures/SyntheticSolution`, is 146 KB / 46 files - same order of magnitude.
- The two private packages it consumes are 4 KB and 5 KB `.nupkg`; committing them removes the `dotnet pack`
  prerequisite and makes the fixture self-contained.
- **Works cold**: analysed with no `bin`, no `obj`, no restore - 13s for SistemaA, 22s for SistemaE, ~100s for
  all six. Fits the full gate, not the quick gate.
- **Analysis is NOT deterministic against build state.** SistemaA cold yields 80 aggregated dependencies and 26
  self-edges; warm yields 115 and 42 - 44% more. The oracle-facing ProjectReference verdict was identical both
  ways, so **assertions must be anchored to the oracle, never to totals**. This also puts a question over
  STO-06/STO-07, which promise byte-identical output.
- Versioning it contradicts the standing `AGENTS.md` rule that `fixtures/SyntheticSolution` is the only
  versioned analysis fixture. That rule must be rewritten deliberately, not bypassed.

### RESOLVED (Phase 11, T73): THE NUMBER THAT MATTERS moved from 7 correct of 60 to 60 correct of 60

**Fixed.** The baselines below are the pre-T73 measurement, kept verbatim as history. `Corpus_ScoresTheRecordedShareOfItsReachableOracle` now asserts **60 correct, 0 false positives, 0 test-policy leaks** — the table's "total" row moved from 7/29/4 to 60/0/0 in one commit (T73), with the two remaining leak-adjacent fixes (T75-T77) closing the test-policy-leak column from 27 to 0. Root cause, confirmed by hand (not the two unconfirmed hypotheses this section originally recorded): `ProjectVariantWorkspace.ReferencedProjects()` named every project the workspace happened to load rather than the root's real `Project.ProjectReferences`, compounded by an evidence-key collision and an occurrence-attribution bug in `CausalRelationExtractor` — see `tasks.md` T73 for the full decision record.

#### 7 correct of 60 (historical measurement, superseded)

`tests/Csharp2Md.Cli.Tests/OracleProjectReferenceScoreTests.cs` (9 cases, `Category=OracleCorpus`, never skips)
scores the package's Project-scope `ProjectReference` edges against the oracle, per solution, and holds each at
its measured defect baseline. Run it with
`dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj -c Release --filter "Category=OracleCorpus"`.

**The target is 60, not 87.** Of the oracle's 87 edges, 27 name a `.Testes` project and the analysis runs
without `--include-tests`, so PKG-05 requires the generator to exclude them. Every stated edge is classified
into one of three buckets - correct, false positive, or **test-policy leak** (a real edge that should not be in
the default package, so evidence of the PKG-05 leak rather than a dependency error).

| solution | oracle | reachable | correct | false positives | test-policy leaks |
| --- | --- | --- | --- | --- | --- |
| SistemaA | 4 | 3 | 1 | 3 | 0 |
| SistemaB | 18 | 12 | 2 | 5 | 1 |
| SistemaC | 1 | **0** | - | - | - |
| SistemaD | 8 | 5 | 2 | 3 | 1 |
| SistemaE | 28 | 20 | 1 | 9 | 1 |
| SistemaE.Copia | 28 | 20 | 1 | 9 | 1 |
| **total** | **87** | **60** | **7** | **29** | **4** |

`SistemaC`'s single oracle edge is excluded by policy, so it has nothing to score; its case asserts that by name
rather than reporting a false failure.

**The baselines are recorded defects, not expected outcomes.** Each case asserts `>=` on correct, `<=` on false
positives and leaks, *and* exact equality, so a regression fails and **an improvement also fails**, with a
message saying the baseline is stale and must be raised in the commit that improved it. Verified by hand:
lowering `SistemaE`'s recorded correct count killed exactly that case, and the message named the 60-edge target,
all nine false positives, the leak and all nineteen missing edges.

**The false positives are one family**: a self-edge per API project plus an API-to-everything fan-out. No edge
originates at a library project. A sub-agent attributed this to `PackageBuilder.Pairs` giving Project scope to
the *evidence document's owning project*; the earlier `ProjectVariantWorkspace.ReferencedProjects()` hypothesis
is recorded below. **Both are unconfirmed - verify before fixing.**

### RESOLVED (Phase 11, T73): measured against the oracle, the dependency projection is wrong

**Fixed**, and the root-cause candidate below was correct in spirit but not in the specific line: `ProjectVariantWorkspace.ReferencedProjects()` (not `CausalRelationExtractor.cs:98` alone) was the entry point, compounded by two aggregation bugs found while confirming it by hand. See the T73 gate note in `tasks.md` for the full, confirmed mechanism and the fault-injection proof. The measurement below is kept as the historical baseline the fix is measured against.

Scored with `<scratchpad>/compare_oracle.py` before the T69 fix, on the three solutions that then published:

| System | Oracle refs | Correct | False positives |
| --- | --- | --- | --- |
| SistemaA | 4 | **1** | 3 |
| SistemaB | 18 | **3** | 5 |
| SistemaE | 28 | **2** | 9 |
| Total | 50 | **6** | 17 |

Recall 12%, precision 26%. **Every produced edge originates at `Api` or `Testes`**; no library-to-library edge
is ever produced. That is entry-point reachability flattened as if direct, not the reference graph. The corpus's
flagship scenario - `SistemaA.Aplicacao -> SistemaA.Infraestrutura`, a deliberate inversion - is undetected,
and `Api -> Infraestrutura` is invented in its place, which is precisely the corpus's `NEG-003`.

Root cause candidate, **unconfirmed**: `ProjectVariantWorkspace.ReferencedProjects()` returns
`Solution.Projects.Where(p => p.Id != RootProject.Id)` - every project in the workspace except the root, which
is not a reference list at all. `CausalRelationExtractor.cs:98` emits `root -> each of them` as
`project-reference`. Roslyn's `Project.ProjectReferences` is the correct source and is not used. Verify before
fixing.

### Benchmark against graphify, and what it leaves as the real target

`graphify` (Graphify-Labs/graphify, Python, tree-sitter, Apache-2.0, 118.9k stars, active) was run on the same
corpus with `graphify update . --no-cluster` - 3 seconds, local, no LLM.

- **graphify scores 87 of 87 project references. Precision and recall 100%, zero false positives, zero
  cross-system edges, and the nested isolated copy stays separate** (oracle rules 2 and 3 both hold). It catches
  the deliberate inversion csharp2md misses and does not produce `NEG-003`.
- **graphify cannot express method-level framework calls.** Zero nodes for `SaveChangesAsync`,
  `ExecuteSqlRawAsync`, `FromSqlRaw`, `GetStringAsync`, `SetStringAsync`, `ToTable`, `GetConnectionString`, and
  zero nodes for any HTTP route. Its vocabulary is `references/imports/calls/contains/defines/inherits/
  implements/method/dispatches_to`. It gets `SistemaADbContext -inherits-> DbContext` and stops there.
- csharp2md's **relation layer is genuinely good and differentiated**: `CotacaoRepository.SaveAsync ->
  dataoperation:SaveChangesAsync`, `-> datastore:Microsoft.EntityFrameworkCore.DbSet<SistemaA.Nucleo.Cotacao>`
  (generic instantiation resolved through the type system), and HTTP routes as first-class entities such as
  `entity:...:externalsystem:http:/v1/tokens/validar`. Tree-sitter cannot produce any of this.

**Sizing of the differentiated product**, scored with `<scratchpad>/score_scenarios.py` after T69, all six
solutions publishing. The probe is deliberately generous - it asks whether the target symbol exists as a
node/entity, not whether the edge is correct - so these are **upper bounds, and they overstate csharp2md**,
whose edges are largely wrong even where the symbols are present.

| | of 38 `confirmed` |
| --- | --- |
| graphify expresses | 22 |
| csharp2md expresses | 28 |
| both | 19 |
| **neither** | **7** (4 HTTP, 1 data-access, 1 configuration, 1 hosting) |
| **only csharp2md** | **9** |

So **16 of 38 confirmed scenarios (42%) depend on Roslyn-grade semantics**: 7 to build, 9 that already work and
need the projection repaired. The single largest gap is HTTP route extraction - and it is inconsistent rather
than absent: **3 of 7 HTTP scenarios are detected** (SistemaC, SistemaD, SistemaE-002), 4 are not
(SistemaA, SistemaB x2, SistemaE-001).

**The recurring pattern across every dimension measured today: the extraction layer holds the right facts and
the projection layer destroys them.** Persistence facts are exact at relation level and become
`Program.cs -> CotacoesTests.cs` plus three self-edges once aggregated. `IEventBus.PublishAsync<PedidoRecebido>`
is extracted in SistemaB, yet FollowFlow reports `no-causal-root` there because it never becomes a Messaging
dependency in the outgoing index. Fixing the projection is worth more than anything else on this list.

**T69 made the pipeline honest, which exposed this.** FollowFlow now certifies on 1 of 5 systems: A and B report
`no-causal-root`, C and D `absent-terminal:contracts`, E passes. T69 did not cause that - it stopped hiding it
behind a `Failed`.

**Test-project leakage (PKG-05) reconfirmed on a controlled corpus**: with `include_tests: false`,
`CotacoesTests.cs` is a retained document in SistemaA and appears among its persistence facts.

**RESOLVED (Phase 11, T77).** This exact leak — `CotacoesTests.cs` reachable through a shared symbol — was the concrete case that drove T77: `BuildMemberships`/`BuildProjectsByDocument` widened a genuinely-retained production entity's membership with an occurrence recorded while analysing the test project as its own root. `Entities_NeverNameATestesProjectByDefault` now holds this at zero across the whole corpus.

- **Discrimination sensor**: skipped, per the standing `AGENTS.md` rule. Every Phase 7 through 11 task instead carries a hand-run fault injection recorded in its `tasks.md` gate note.
- **Blockers**: none.
- **Uncommitted files**: `.specs/STATE.md` (this file, this post-Verifier correction pass), plus pre-existing `AGENTS.md` and `docs/specs/pacote-conhecimento-util-e-confiavel.md` edits held by the user, and untracked `research/`, `.specs/LESSONS.md`, `.specs/lessons.json`.
- **Verifier's non-blocking observations** (full detail in `validation.md`): (1) three opt-in cases in `SourceInventoryTests.cs` carry a `PKG-08` trait but actually prove PKG-05's `--include-tests` arm; (2) `RecordedDefects`→`RecordedBaselines`'s rename left `defect`-named locals and the method name `ProjectReferences_HoldTheRecordedDefectBaseline` describing a now-correct state as a "defect"; (3) `OracleProjectReferenceScoreTests.cs`'s `>=`/`<=` ratchet checks are now redundant beside the exact-equality conjunction that follows them; (4) T76's tasks.md gate note said "2 focused cases" where 1 Fact + 3 InlineData rows is 4 (totals elsewhere are correct). None affects coverage or correctness; naming/wording only.
- **Local wart, cleared**: `fixtures/csharp2md-analyze-out-2b1ee4ef…` and `…-5e8132fd…` were leftover analyze outputs **committed** under `fixtures/` (one tracked file each), carrying the superseded percent-encoded `id1:` identity scheme. Nothing in the tree referenced them; T72 deleted both from git, added the `fixtures/csharp2md-analyze-out-*/` ignore rule and pinned the absence with a retention case.
- **Branch**: `feature/simplif`
