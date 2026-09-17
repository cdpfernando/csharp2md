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
- **Phase / Task**: Phase 10 (oracle-anchored dependency accuracy), **T70-T72 committed and complete**. Phase 9's T67 and T69 are done; **T68 is parked** because the session redirected away from file size. The feature Verifier returned **PASS 71/71** on iteration 2 - against a fixture that could not discriminate, so that verdict is not trustworthy and Phase 10 exists to replace it with a measured one.
- **Completed**: T1-T67 and T69. Phase 8 landed at `db16a8a` (T60), `a55f831` (T61), `03a0814` (T62), `bf0bb86` (T63), `98f53fc` (T64), `e8f3ac1` (T65), `df84bac` (T66). Phase 9: `f2939d4` (plan), `74a8713` (T67), `6bc1f27` (T69). The passing Verifier report is at `accfd41`; the first Verifier's FAIL report and the Phase 8 plan at `10ceb90`.
- **Gate at `aed46f5`**: Release build 0 warnings / 0 errors; `Csharp2Md.Core.Tests` **621/621**; `Csharp2Md.Cli.Tests` **94 passed / 2 skipped**. Verified independently in the main session, not taken from a sub-agent's report. The 2 skips are the absent eShopOnContainers and Pitstop clones.
- **Only one clone is present**: `fixtures/eShop`. Pitstop and eShopOnContainers are absent, so their `LocalCorpusAnalyzeTests` cases skip by name. The eShop case runs and passes.
- **Next step**: **a product decision the user has not taken.** The measuring instrument now exists and is green; what it measures is bad. The options put to the user were: (1) version the corpus - **done, this is Phase 10**; (2) confirm and fix the projection; (3) re-verify the feature against the corpus; (4) narrow the product to the Roslyn-semantic layer and compose with an existing structural tool. **2 and 3 are now unblocked and cheap; 4 is the open strategic question.**

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

### OPEN FINDING: the component dependency graph is complete, and the package is factually wrong

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

**Not yet done, deliberately.** The traceability table and `validation.md` were left untouched by the user's
decision, so DEP-01, DEP-02, DEP-03 and PKG-05 still read `Complete` while the real corpus contradicts them.
Anyone reading the table before this finding is fixed is reading a claim the evidence does not support.

**Reproduce**: the throwaway diagnostic that produced these numbers is kept outside the repo at
`<scratchpad>/eshop-fanout-diagnostic.cs`; drop it into `tests/Csharp2Md.Core.Tests/`, run its single fact, and
delete it again. It calls `SolutionAnalyzer.AnalyzeAsync` on the eShop clone directly and takes about 90
seconds.

**Deprioritised by the user on 2026-09-17**: package file size and byte ceilings. T68 is parked for the same
reason. Correctness of the projection comes first.

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

### THE NUMBER THAT MATTERS: 7 correct of 60

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

### OPEN FINDING: measured against the oracle, the dependency projection is wrong

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

- **Discrimination sensor**: skipped, per the standing `AGENTS.md` rule. Every Phase 7, 8 and 9 task instead carries a hand-run fault injection recorded in its `tasks.md` gate note.
- **Blockers**: none.
- **Uncommitted files**: `.specs/STATE.md` (this file), plus pre-existing `AGENTS.md` and `docs/specs/pacote-conhecimento-util-e-confiavel.md` edits and untracked `research/`, `.specs/LESSONS.md`, `.specs/lessons.json`.
- **Local wart, cleared**: `fixtures/csharp2md-analyze-out-2b1ee4ef…` and `…-5e8132fd…` were leftover analyze outputs **committed** under `fixtures/` (one tracked file each), carrying the superseded percent-encoded `id1:` identity scheme. Nothing in the tree referenced them; T72 deleted both from git, added the `fixtures/csharp2md-analyze-out-*/` ignore rule and pinned the absence with a retention case.
- **Branch**: `feature/simplif`
