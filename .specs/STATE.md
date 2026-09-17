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

- **Discrimination sensor**: skipped, per the standing `AGENTS.md` rule. Every Phase 7 and Phase 8 task instead carries a hand-run fault injection recorded in its `tasks.md` gate note.
- **Blockers**: none.
- **Uncommitted files**: `.specs/STATE.md` (this file), plus pre-existing `AGENTS.md` and `docs/specs/pacote-conhecimento-util-e-confiavel.md` edits and untracked `research/`, `.specs/LESSONS.md`, `.specs/lessons.json`.
- **Local wart**: `fixtures/csharp2md-analyze-out-2b1ee4ef…` and `…-5e8132fd…` are leftover analyze outputs sitting in `fixtures/`. Not created by this session; check they are gitignored before the next clean.
- **Branch**: `feature/simplif`
