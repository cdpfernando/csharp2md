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
- **Phase / Task**: Phase 12 / T79. The feature is complete; the second full-scope Verifier returned PASS (71/71 ACs, 5/5 edge cases).
- **Completed**: T1-T67, T69-T79 (T68 parked by the user: package size ceilings are deprioritised behind projection correctness).
- **In-progress**: none.
- **Next step**: none required. The open items below are candidates for a new phase or feature, at the user's discretion.
- **Blockers**: none.
- **Uncommitted files**: none.
- **Branch**: `master` (feature merged via PR #25).

### Open items (none fails a written AC)

- **Major: `ProjectReference` `nature: Direct` is overclaimed.** On `fixtures/eShop`, 8 of 35 published direct edges (every `X → EventBusRabbitMQ` consumer) are transitive-only; `design.md:222` requires the direct-declaration list.
- **Major: the T79 ownership defect is live at a fourth site.** `ArchitectureFactExtractor` keys Symbol/EntryPoint/BoundaryOperation entities on bare `ToDisplayString()`, the pattern T79 fixed in `CausalRelationExtractor` and `ConfigurationPersistenceExtractor`.
- `fixtures/ArchitectureDependencyLab` cannot discriminate any scope above Project (one Component/DeploymentUnit per solution); `oracle/scenarios.json` has no test consumer.
- Seven of DEP-02's eight categories (all but `project-reference`) have no corpus-level accuracy measurement.
- STO-07 determinism is proven only against a fixed in-memory model; analysis is not deterministic against build state (SistemaA cold vs. warm: 80 vs. 115 aggregated dependencies).
- 78 test cases carry no `Requirement` trait, including the CLI suites that solely own CRT-07 and CRT-09. Misplaced traits: `PublicationSafetyScannerTests.cs:44` (PUB-06 case traited CRT-08), `PackageBuilderTests.cs:10-13`, and three `SourceInventoryTests.cs` cases traited PKG-08 that prove PKG-05's `--include-tests` arm.
- `OracleProjectReferenceScoreTests.ProjectReferences_HoldTheRecordedDefectBaseline` and its `defect`-named locals describe a now-correct state; its `>=`/`<=` checks are redundant beside the exact-equality assertion.
- T67's `tasks.md` Done-when boxes are unchecked although the work was verified.
- PUB-08 is Partial: `KnowledgePackageFailureTests` feeds hand-built diagnostics through a CLI stub for the nine rejection classes; no single test spans render-and-populate.
- CRT-04 and CRT-05 are unmeasured end to end; re-run `Category=LocalCorpus` when the eShopOnContainers and Pitstop clones are present.
- `PackageBudget.Default` (96 MiB fallback) is an AD candidate with no AD recording it.
- Residual: a constructed generic's `Locations` resolve to its unbound BCL definition, so a user type reachable only inside a BCL generic (e.g. `List<Foo>`) produces no `structural-type-use` edge. No measurement has shown it live.

Execution history, gate evidence and fault injections are in `tasks.md`; the Verifier's full report is in `validation.md`.
