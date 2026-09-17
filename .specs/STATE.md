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
- **Phase / Task**: Phase 7 (Verifier remediation), **5 of 5 done**. T55-T59 are committed. All 59 tasks are complete; the only step left is the feature Verifier.
- **Completed**: T1-T59. Phase 7 landed at `f8dff64` (T55), `901b7c1` (T56), `3bc04f3` (T57), `8295dd0` (T58) and the T59 commit below.
- **Gate at `8295dd0`**: Release build 0 warnings/0 errors; `Csharp2Md.Core.Tests` 588/588 (0 skipped), up from 574 at `3f1ad1e`; `Csharp2Md.Cli.Tests` 81/83.
- **Only one clone is present now**: `fixtures/eShop`. The Pitstop and eShopOnContainers clones are gone from this machine, so their `LocalCorpusAnalyzeTests` cases skip by name. CRT-04 and CRT-05 could not be re-measured after T58; re-run the LocalCorpus filter once those clones return.
- **Next step**: dispatch the feature Verifier. `validate_state.py` still exits 1 because `.specs/features/pacote-conhecimento-util-e-confiavel/validation.md` does not exist — the earlier FAIL verdict was never written to disk (it is absent from the working tree and from history), so the Verifier must create it.
- **What Phase 7 closed so far**:
  - T55: the four non-discriminating tests now drive their named behaviour. Proven by injecting four faults and watching exactly those four fail.
  - T56: the MET-08 scan reflects over every public property in `PackageBuilding`, `Publication` and their sub-namespaces, and forbids `Coupling` alongside score/quality/risk.
  - T57: `PublicationMeasurements.ByFamily` carries per-`ArtifactFamily` counts and bytes, and the EDG-03 diagnostic quotes it. The breakdown excludes the three publication trailers because `measurements.json` cannot report its own size; `sum(ByFamily.ArtifactCount) + 3 == PublishedArtifactCount`.
  - T58: every cited document gets a Markdown page, routed by a `documents.json` index the roots index declares by one path so Locate keeps 3 reads; rows are relative Markdown links, or plain text when the target has no page.
  - T59: `spec.md`'s traceability table no longer has a `Design | Pending` row. Owners are the mechanical union of every task whose `**Requirement**` line names the ID, so all 71 resolve; the `Phase` column is now `Owning task(s)`. 69 read `Complete`, and **CRT-04 and CRT-05 read a new `Unverified`** because their seam skips without the clones. `tasks.md`'s grouped table gained the Phase 7 owners it was missing (T55 on MET-03/PUB-08, T56 on MET-08, T57 on CRT-03, T58 on NAV-03/NAV-04).
- **Budget amended in T58 (AD candidate)**: `PackageBudget.Default` went from 64 to **96 MiB**. Document pages took eShop from 49.21 MiB / 220 files to **78.61 MiB / 965 files**. The user chose to raise the default rather than bound the pages. CRT-04 (eShopOnContainers, 1,500 files / 64 MiB) and CRT-05 (Pitstop, 750 files / 25 MiB) are unchanged in the spec and remain plausible after a +60% growth, but are unverified until those clones return.
- **NAV-04 touched by T58**: `markdown/index.md` linked roots by a root-absolute path that does not resolve from `markdown/`. It now uses the same relative computation as the page rows, so document pages are genuinely reachable from the summary.
- **Discrimination sensor**: skipped, per the standing `AGENTS.md` rule. Each Phase 7 task instead carries a hand-run fault injection recorded in its `tasks.md` gate note.
- **Blockers**: none.
- **Uncommitted files**: `.specs/STATE.md` (this file), plus pre-existing `AGENTS.md` and `docs/specs/pacote-conhecimento-util-e-confiavel.md` edits and untracked `docs/prompts/`, `docs/validation/`, `research/`, `.specs/LESSONS.md`, `.specs/lessons.json`.
- **Local wart**: `fixtures/csharp2md-analyze-out-2b1ee4ef…` and `…-5e8132fd…` are leftover analyze outputs sitting in `fixtures/`. Not created by this session; check they are gitignored before the next clean.
- **Branch**: `feature/simplif`
