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
- **Phase / Task**: Phase 8 (second Verifier remediation), **7 of 7 done**. T60-T66 are committed. All 66 tasks are complete; the only step left is the re-dispatched Verifier (iteration 2 of the bounded 3).
- **Completed**: T1-T66. Phase 8 landed at `db16a8a` (T60), `a55f831` (T61), `03a0814` (T62), `bf0bb86` (T63), `98f53fc` (T64), `e8f3ac1` (T65), `df84bac` (T66). The Phase 8 plan and the first Verifier's report are at `10ceb90`.
- **Gate at `df84bac`**: Release build 0 warnings / 0 errors; `Csharp2Md.Core.Tests` **612/612** (up from 588 at `8295dd0`); `Csharp2Md.Cli.Tests` 81/83.
- **Only one clone is present**: `fixtures/eShop`. Pitstop and eShopOnContainers are absent, so their `LocalCorpusAnalyzeTests` cases skip by name. The eShop case runs and passes.
- **Next step**: read the Verifier's verdict for iteration 2. `validation.md` currently holds the iteration-1 FAIL and must be overwritten by the new run.

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

- **Discrimination sensor**: skipped, per the standing `AGENTS.md` rule. Every Phase 7 and Phase 8 task instead carries a hand-run fault injection recorded in its `tasks.md` gate note.
- **Blockers**: none.
- **Uncommitted files**: `.specs/STATE.md` (this file), plus pre-existing `AGENTS.md` and `docs/specs/pacote-conhecimento-util-e-confiavel.md` edits and untracked `research/`, `.specs/LESSONS.md`, `.specs/lessons.json`.
- **Local wart**: `fixtures/csharp2md-analyze-out-2b1ee4ef…` and `…-5e8132fd…` are leftover analyze outputs sitting in `fixtures/`. Not created by this session; check they are gitignored before the next clean.
- **Branch**: `feature/simplif`
