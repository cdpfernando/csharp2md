---
name: Consistência da documentação
overview: Corrigir referências a documentos inexistentes, afirmações que divergem da codebase e instruções ambíguas/duplicadas em AGENTS.md, STATE.md, READMEs, spec de origem, README da SyntheticSolution e design.md, sem tocar nas cópias da skill nem no código.
todos:
  - id: agents
    content: "Rewrite AGENTS.md Project rules: remove nonexistent refs, fix AD-003 citation, skill by name, fixture list, LocalCorpus paths/command"
    status: pending
  - id: state
    content: Replace STATE.md Handoff body with compact memory.md-format snapshot and deduplicated open items; leave Decisions untouched
    status: pending
  - id: readme
    content: Fix fixture statement in README.md and README.pt-BR.md; verify Acme.Orders restore example, switch to Acme.Journey.slnx if it fails
    status: pending
  - id: origin-spec
    content: Update Status/Triage lines of docs/specs/pacote-conhecimento-util-e-confiavel.md
    status: pending
  - id: synthetic-readme
    content: Rewrite fixtures/SyntheticSolution/README.md without legacy class/ID references, covering current projects, slnx files, oracle and consuming tests
    status: pending
  - id: design-note
    content: Add pre-T45 legacy-path note to design.md tables at lines 157, 539, 587
    status: pending
  - id: verify
    content: Re-run path scan, rg for removed refs, run README commands, validate_state.py, review git diff
    status: pending
isProject: false
---

# Consistência da documentação

## Divergências encontradas (evidência)

- [AGENTS.md](AGENTS.md) linha 12 cita `docs/architecture/` e `architecture-knowledge-engine-roadmap.md`: nenhum dos dois existe. A linha 13 diz que "nenhuma feature spec substituta existe ainda", mas `.specs/features/pacote-conhecimento-util-e-confiavel/` existe e está concluída (validação PASS). Também cita uma "disposition table do roadmap" que não existe.
- AGENTS.md linha 15 lista features `knowledge-taxonomy-contract` até `components-deployments-configuration`, que não existem; só há uma feature.
- AGENTS.md linha 16 atribui o BuildHost fora de processo ao AD-003, mas o AD-003 do [.specs/STATE.md](.specs/STATE.md) trata de variantes por projeto/TFM.
- AGENTS.md linha 13 manda executar via caminho `.agents/skills/.../SKILL.md`, enquanto o `tasks.md` da feature manda ativar a skill pelo nome e nunca pelo caminho.
- AGENTS.md linha 17 manda rodar "os launch profiles CLI correspondentes" aos clones, mas [launchSettings.json](src/Csharp2Md.Cli/Properties/launchSettings.json) não tem perfil para eShop ou Pitstop. O perfil eShopOnContainers aponta para `D:/workspace/reverser/...`, fora de `fixtures/`.
- AGENTS.md chama o código atual de "legacy implementation", o que contradiz o README ("A substituição está concluída").
- [README.md](README.md) e [README.pt-BR.md](README.pt-BR.md), linhas 96 e 135, dizem que `fixtures/SyntheticSolution` é a *única* fixture versionada. Na verdade existem quatro (AGENTS.md e `.gitignore` concordam nisso).
- [docs/specs/pacote-conhecimento-util-e-confiavel.md](docs/specs/pacote-conhecimento-util-e-confiavel.md), linhas 3 e 5, ainda dizem "implementação não iniciada" e "ready-for-agent".
- STATE.md Handoff: branch `feature/simplif` (a branch atual é `master`, já mergeada via PR #25). Lista arquivos não commitados, mas a árvore está limpa. Cita `.specs/LESSONS.md`/`lessons.json` e scripts em `<scratchpad>/`, que não existem. A linha 160 fala de uma regra do AGENTS que já foi reescrita. Há achados duplicados com números conflitantes (80 contra 78 casos sem trait). Tem cerca de 250 linhas, contra os ~500 tokens definidos em `references/memory.md`.
- [fixtures/SyntheticSolution/README.md](fixtures/SyntheticSolution/README.md) cita classes e IDs legados que não existem mais em `src/`/`tests/`: `LooseProjects`, `MessagingDetector`, `ConfigIndexer`, `ServiceNameResolver`, `SolutionLoader`, `SolutionLoaderClassificationTests.cs`, `GrpcClientDetector`, T2/T3/T17/T26, P1-xx/P2-xx e AD-005. Também omite `Acme.Orders.Worker` e `Acme.Journey.slnx`.
- [design.md](.specs/features/pacote-conhecimento-util-e-confiavel/design.md), linhas 157, 540-545 e 588-597, aponta para `src/Csharp2Md.Analysis|Storage|Projection/...`, removidos no corte T45, sem avisar que são caminhos pré-corte.

## Mudanças

1. **AGENTS.md: reescrever a seção "Project rules"** (a seção "Working principles" fica como está):
   - Fontes normativas, em ordem: `CONTEXT.md` (linguagem), `.specs/STATE.md` `## Decisions`, `docs/specs/pacote-conhecimento-util-e-confiavel.md` e sua feature em `.specs/features/`. Onde o código divergir delas, a divergência é um defeito.
   - Criar uma nova feature em `.specs/features/` só quando o usuário iniciar o trabalho explicitamente. Executar com a skill `tlc-spec-driven`, ativada pelo nome.
   - Manter a proibição de recuperar specs, ADRs e contratos superados do histórico do Git, e remover a menção à disposition table.
   - Regra do sensor de discriminação: manter o skip, sem a lista de features inexistentes, e citar a frase já usada no header do `tasks.md` ("Project override: ...").
   - Linha de Roslyn e BuildHost: sem a citação errada ao AD-003.
   - Fixtures em lista (uma linha por fixture, os mesmos fatos de hoje): o que cada uma é e quem a consome (`Category=OracleCorpus` para ArchitectureDependencyLab; nenhum consumidor para CertificationCorpus e PublicationResilience).
   - Clones locais: caminhos esperados por `LocalCorpus.SolutionPath` (`fixtures/eShop/eShop.slnx`, `fixtures/eShopOnContainers/eShopOnContainers-ServicesAndWebApps.sln`, `fixtures/Pitstop/pitstop.sln`) e o comando `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj -c Release --filter "Category=LocalCorpus"`, sem mencionar launch profiles.
2. **STATE.md: substituir apenas o corpo de `## Handoff`** por um snapshot no formato de `memory.md`:
   - Campos: Feature, Phase/Task (T79, feature concluída, Verifier PASS), Completed, Next step, Blockers `none`, Uncommitted files `none`, Branch `master`.
   - Uma lista "Open items" deduplicada: os 2 gaps Major do segundo Verifier, os achados (a)-(f) não blocking, PUB-08 Partial, CRT-04/05 não medidos, `PackageBudget.Default` como candidato a AD e o residual de genéricos BCL.
   - Remissão para `tasks.md` e `validation.md` como registro histórico.
   - `## Decisions` não muda.
3. **READMEs (EN e pt-BR):** trocar "única fixture versionada" por uma frase curta que cite as quatro fixtures e aponte o AGENTS.md para o detalhe. Validar o exemplo `dotnet restore fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx`: esse `.slnx` inclui `Acme.Broken` (SDK inexistente) e `Acme.DoesNotExist`. Se o restore falhar, trocar o exemplo por `fixtures/SyntheticSolution/Acme.Journey.slnx` nos dois READMEs, mantendo-os espelhados.
4. **Spec de origem:** atualizar as linhas `Status` e `Triage` para "implementada; ver `.specs/features/pacote-conhecimento-util-e-confiavel/validation.md`", sem mexer no conteúdo normativo.
5. **SyntheticSolution README:** reescrever como descrição atual. Para cada projeto e `.slnx`, dizer o que ele exercita: restore deliberadamente ausente em Payments, SDK quebrado, projeto ausente, TFM múltiplo em Shared.Contracts, `Acme.Shipping.Tests` para `--include-tests`, `Acme.Orders.Worker` e `Acme.Journey.slnx`. Incluir `knowledge-oracle.json` e os testes que consomem a fixture (`ProjectVariantPlannerTests`, `ProjectVariantWorkspaceTests`, `ArchitectureFactExtractorTests`, `KnowledgePackageJourneyTests`, `KnowledgePackageFailureTests`, `SyntheticSolutionFixtureTests`). Remover os IDs e classes legados. Antes de escrever, ler `Acme.Orders.Worker` e `SyntheticSolutionFixtureTests.cs` para não afirmar nada sem base.
6. **design.md:** adicionar uma linha de nota antes das tabelas das linhas 539 e 587 (e no bullet da 157) dizendo que os caminhos se referem aos assemblies anteriores ao corte T45, removidos da árvore. Não reescrever as tabelas.

## Fora do escopo (só será reportado)

- As cópias da skill em `.agents/`, `.claude/`, `.cursor/` e `.windsurf/` ficam como estão, por decisão sua.
- Arquivos que não são documentação: `fixtures/launch-manifest.json` não tem consumidor e usa o formato legado; o perfil eShopOnContainers em `launchSettings.json` tem caminho absoluto externo.
- Caminhos históricos citados dentro de `tasks.md`, `validation.md` e `context.md`: são registro de execução ou de corpora externos.
- `research/` e `fixtures/ArchitectureDependencyLab/README.md`: material de terceiros ou datado.

## Verificação

- Rodar de novo a varredura de caminhos citados nos `.md` alterados: nenhum ponteiro quebrado além dos marcados como históricos.
- `rg` sem ocorrências de `docs/architecture`, `architecture-knowledge-engine-roadmap`, `knowledge-taxonomy-contract`, `feature/simplif` e das classes legadas nos arquivos alterados.
- Executar o comando `dotnet restore` do README e o `dotnet run ... analyze` que o acompanha. A saída vai para `artifacts/`, que é ignorado pelo git.
- `python3 .agents/skills/tlc-spec-driven/scripts/validate_state.py pacote-conhecimento-util-e-confiavel` sai com 0.
- Revisar o `git diff` para confirmar que `## Decisions` do STATE.md não mudou.