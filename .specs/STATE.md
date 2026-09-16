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
- **Phase / Task**: Execute / Phase 6, T39 complete; stopped before T40 as requested
- **Completed**: T1-T39; analyze now invokes KnowledgeEngine once for a validated multi-solution request
- **In-progress** (file:line): none
- **Next step**: Resume with T40 (route validate through KnowledgeEngine.Validate and remove compose); do not skip ahead within Phase 6
- **Blockers**: none; the full legacy suite remains deferred to T45 under the approved interim rule, while the Release build, 486 Core tests, 17 T39 CLI tests and 2 repaired Analysis tests pass
- **Uncommitted files**: pre-existing `AGENTS.md`, `docs/specs/pacote-conhecimento-util-e-confiavel.md`, `docs/prompts/`, `docs/validation/`, and `research/2026-09-15-agent-harness-portability.md` remain outside the T39 commit
- **Branch**: `feature/simplif`
