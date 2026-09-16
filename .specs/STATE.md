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
- **Phase / Task**: Execute / Phase 6, T51 complete. Chain is `T43 -> T48 -> T46 -> T47 -> T49 -> T50 -> T51 -> T44 -> T45`.
- **Completed**: T1-T43, T48, T46, T47, T49, T50, T51. `src/` is clean at `5359b6b`; Core Release quick gate is green (534 passed, 0 failed, 0 skipped).
- **In-progress** (file:line): `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs` (T44 tests, written ahead, uncommitted). Pitstop passes; eShop and eShopOnContainers fail.
- **Next step**: Decide the scope of the package-size work below, then run T44.
- **Blockers**: eShop and eShopOnContainers are rejected at `package-budget: 'bytes'` in package-building, before publication. Measured 2026-09-16 with a temporary diagnostic in `PackageBuilder.Build` (since reverted):
  - eShopOnContainers: 117,080,074 bytes over the 64 MiB ceiling, 248 artifacts. `measures/dependencies.000000.json` is 59.4 MB and `measures/summary.json` is 52.8 MB; everything else totals 4.9 MB.
  - eShop: 156,285,412 bytes, 215 artifacts. `dependencies.000000.json` is 97.3 MB, `summary.json` is 53.3 MB, rest 5.6 MB.
  - Composition of Pitstop's 12.69 MB dependency artifact, as a proxy: handle references serialize with a `{"value":"su"}` envelope, so `relations` + `evidence` handle arrays cost 4,372,683 bytes for a few hundred thousand two-to-three character handles; `StoredRelation.source_canonical_key` + `target_canonical_key` plus `AggregatedDependency.source`/`target` cost another 4,295,172 bytes of full canonical entity keys. Together about 70% of the artifact. `summary.json` carries the same two problems through `ScopeMeasures.Entity` and `ImpactTarget`.
  - Two candidate tasks, neither started: serialize local handles as bare JSON strings instead of `{"value": ...}`; extend T49's local-handle table to entity and variant canonical keys in stored relations, aggregated dependencies and scope measures. Sharding does not help here: the budget is on total bytes, not per file.
- **Decisions settled this session**: T50 replaced the array-shaped evidence index with a shard router (`tables/evidence.NNNNNN.json` plus an ordinal-range index); the evidence table packs at 32 KiB rather than the 64 KiB bulk target, which `context.md` Agent's Discretion allows. T51 settled the partial-causal-category question the previous handoff left open: persistence alone is a terminal, not a flow root, so a persistence-only solution records `not_applicable:no-causal-root`. Pitstop now commits at 136 files and 19.50 MiB. Decision D (arbitrary document-scope target) remains open and blocks nothing.
- **Uncommitted files**: `LocalCorpusAnalyzeTests.cs` (T44 tests), `.specs/STATE.md`, plus pre-existing `AGENTS.md`, `docs/` and `research/` changes.
- **Branch**: `feature/simplif`
