# LLMWiki para engenharia reversa de codebases C#/.NET

## Estado do documento

| Campo | Valor |
|---|---|
| Objetivo | Converter uma codebase C#/.NET em conhecimento navegável, auditável e incremental |
| Escopo | Qualquer codebase C#/.NET; nenhum domínio, nome de solução ou arquitetura é pressuposto |
| Estado atual | O `RelationResolver` P1 foi implementado; os artefatos factuais canônicos já são emitidos |
| Escopo ativo | Fechar a geração do insumo factual, escalável e auditável |
| Escopo adiado | Ingestão, páginas, templates e formato da wiki |
| Próximo marco | Índices e projeções de recuperação do gerador, sem gerar ou alterar `wiki/` |
| Controle de entrega | Este documento — a tabela da seção 9 é a autoridade de prioridade/ordem/status |
| Execução | `tlc-spec-driven` por item — cada linha PENDENTE vira `.specs/features/<feature>/` (spec → design → tasks → execute) ao ser puxada da fila. Processo lido diretamente de `.agents/skills/tlc-spec-driven/` (`SKILL.md` + `references/*.md`, scripts de validação em `scripts/*.py`), não invocado por comando nativo de uma ferramenta — qualquer agente com leitura de arquivo e shell (Claude Code, Codex, Cursor, Windsurf, ou outro) segue os mesmos arquivos e produz o mesmo `.specs/features/<feature>/` |

## 1. Visão do produto

A LLMWiki não é documentação gerada de forma livre. Ela é uma base de conhecimento derivada de evidências extraídas de uma codebase. Seu papel é responder, de modo rastreável:

- o que o sistema contém;
- quais responsabilidades cada parte possui;
- como a execução começa e atravessa componentes;
- como dados, eventos e integrações se movem;
- quais conclusões são fatos, inferências ou lacunas;
- onde a evidência de cada afirmação pode ser encontrada.

O sistema de origem é sempre C#/.NET, mas pode usar qualquer combinação de projetos, frameworks, padrões arquiteturais ou convenções internas. A wiki descobre essas características a partir da saída do analisador; ela não as presume.

### Delimitação de escopo atual

Neste momento, o trabalho termina no **insumo canônico produzido pelo gerador**. O objetivo é que ele seja completo, determinístico, verificável, escalável e recuperável por consumidores futuros.

Ficam deliberadamente fora do escopo atual:

- ingestão por LLM ou por qualquer ferramenta de wiki;
- diretório `wiki/`, páginas, templates, links e taxonomia de apresentação;
- síntese de módulos, serviços, fluxos, regras de negócio e decisões;
- lint de conteúdo da wiki e atualização de páginas existentes.

As seções que tratam desses itens permanecem apenas como referência futura e estão marcadas como **ADIADO**. Elas não são requisitos do gerador nem devem orientar mudanças de código agora.

```text
Codebase C#/.NET
      |
      +-- extração de símbolos --------> SymbolIndex
      |
      +-- collectors ------------------> RawRelations
      |                                      |
      +-- descoberta de persistência ----> Database mappings
                                             |
                                             v
                                      RelationResolver [CONCLUÍDO]
                                             |
                                             v
                                  relações resolvidas + evidências
                                             |
                                             v
                                  facts / graph canônicos [PENDENTE]
                                             |
                                             v
                                       ingestão da LLMWiki
                                             |
                                             v
                                             wiki persistente
```

## 2. Decisões já concluídas

### 2.1 Escopo da LLMWiki — CONCLUÍDO

A LLMWiki é genérica entre codebases C#/.NET. Ela não deve conhecer previamente nomes de projetos, domínios de negócio, serviços, módulos nem arquitetura do sistema analisado.

### 2.2 Separação entre descoberta e conhecimento — CONCLUÍDO

`raw/` é a fonte de verdade produzida pelo analisador. `wiki/` é conhecimento derivado e pode ser atualizado ou reconstruído sem alterar `raw/`.

### 2.3 Pipeline de relações — CONCLUÍDO

O processo foi separado em três responsabilidades:

```text
detectar -> coletar -> resolver
```

O collector registra que uma relação existe. O resolver identifica o alvo quando há evidência suficiente. Uma relação não deve ser removida apenas porque seu alvo ainda não foi resolvido.

### 2.4 RelationResolver P1 — CONCLUÍDO

O `RelationResolver` é o estágio que transforma `RawRelation` em `RelationFact` no segundo passe, depois que o `SymbolIndex` está completo. O artefato preserva alvo textual quando necessário, candidato(s), método de resolução, metadados, proveniência e evidências. A cadeia atual é `ExistingTarget` → `DatabaseRelation` → `ReceiverType` → `SymbolIndex` → `Unresolved`, e nunca escolhe silenciosamente entre candidatos ambíguos.

Tentativas individuais de estratégia e valores numéricos de confiança ainda não fazem parte do artefato publicado; são escopo P3 (`RELR-46`..`RELR-49`).

### 2.5 Topic raw, fatos e projeção Markdown — CONCLUÍDO

O gerador já implementa a fundação factual da LLMWiki. Cada execução escreve um tópico `raw/` com `raw/codebase/`, frontmatter, `topic.yaml`, `CLAUDE.md`, `log.md`, fragmentos validados, `manifest.json`, cobertura, diagnósticos e projeções agregadas. Os fragmentos têm ids estáveis, hashes, proveniência e evidências; o manifesto é o marcador de commit da árvore factual.

Essa entrega corresponde à fase de preparação do tópico, e não à wiki compilada: `wiki/` continua sendo uma camada posterior, derivada e atualizável.

### 2.6 Descoberta de acesso a dados — CONCLUÍDO

O gerador já produz objetos e colunas de banco comprovados por literais/configuração, um catálogo em `raw/facts/database.json` e relações da partição `data`. Mapeamentos convencionais, SQL dinâmico e destinos não comprovados preservam texto/evidência e não criam identidades fictícias.

### 2.7 Limite conhecido do grafo de componentes — PENDENTE

O `raw/dependencies.mmd` agora representa um grafo de componentes utilizável. A feature `component-graph` cria um
`ComponentFact` por `ProjectFact`, resolve endpoints de projeto, documento, símbolo e banco via `GraphNodeIndex`,
deduplica arestas por origem, destino, partição e tipo, e inclui objetos de banco como nós. A validação independente
comprovou ao menos uma aresta real entre componentes no fixture ponta a ponta; AD-020 foi substituída por AD-021.

## 3. Contrato de conhecimento

Toda afirmação material na wiki deve ser classificada como `FACT`, `INFERENCE` ou `UNKNOWN`.

### FACT

Afirmação diretamente sustentada por um artefato em `raw/`. Exemplos: um tipo implementa uma interface; um método chama outro; há uma rota HTTP; uma entidade é mapeada para uma tabela por configuração explícita.

Requisitos:

- ao menos uma evidência localizável;
- a redação não extrapola o que a evidência demonstra;
- o vínculo de evidência aponta para um artefato em `raw/`, nunca apenas para outra página da wiki.

### INFERENCE

Conclusão derivada de múltiplos fatos. Exemplos: um grupo de componentes parece formar uma capacidade; uma sequência de chamadas parece compor um fluxo; uma decisão arquitetural é provável.

Requisitos:

- evidências que sustentam o raciocínio;
- nível de confiança;
- linguagem proporcional à confiança, por exemplo: “indica”, “aparenta”, “provavelmente”.

### UNKNOWN

Uma lacuna real de conhecimento: alvo dinâmico, símbolo ambíguo, fluxo incompleto, dono não identificado ou configuração ausente.

Requisitos:

- descrição precisa do que não foi resolvido;
- origem ou evidência do problema;
- candidatos, se houver;
- ação de investigação sugerida quando for possível.

`UNKNOWN` é conhecimento válido. Não deve ser eliminado por suposição plausível.

## 4. Estrutura canônica

```text
llmwiki/
├── raw/                         # imutável; saída do analisador
│   ├── codebase/                # representação navegável do código
│   ├── facts/                   # fatos estruturados
│   ├── relations/               # relações coletadas e resolvidas
│   ├── artifacts/               # diagramas, logs e demais artefatos
│   └── metadata/                # versão, execução, escopo e hashes
├── schema/                      # instruções, taxonomia e templates
└── wiki/                        # conhecimento derivado e incremental
    ├── index.md
    ├── system/
    ├── modules/
    ├── services/
    ├── components/
    ├── entry-points/
    ├── flows/
    ├── events/
    ├── integrations/
    ├── entities/
    ├── business-rules/
    ├── configuration/
    ├── decisions/
    ├── glossary/
    └── unknowns/
```

Fonte de consulta, em ordem de preferência:

1. `raw/facts/`;
2. `raw/relations/`;
3. `raw/codebase/`;
4. demais artefatos em `raw/`.

Uma página existente jamais vira evidência primária de outra página. Ela apenas facilita navegação e descoberta.

## 5. RelationResolver: contrato operacional

### 5.1 Entrada mínima

Uma `RawRelation` deve conter identificador, origem, tipo, contexto de projeto/documento e evidência. Quando disponíveis, também deve preservar `TargetId`, `TargetText`, receiver, tipo do receiver, membro, contagem/tipos de argumentos e metadados específicos do protocolo.

Os metadados permitem que a relação seja extensível: método e rota HTTP, tipo de evento, operação de banco, texto de tabela/coluna, connection name e outros dados sem tornar o modelo central dependente de protocolos específicos.

### 5.2 Saída publicada atualmente

O `RelationFact` publicado mantém:

- `Id`, `SourceId` e `Kind`;
- `TargetId` e `TargetText`, inclusive quando o alvo não foi resolvido;
- `FactHeader.Resolution` e `ResolutionMethod`;
- `Candidates` para ambiguidades;
- `Evidence` agregada;
- `Metadata` preservada ou enriquecida.

`Confidence` numérica e `Attempts` por estratégia ainda não são emitidos. Quando P3 for implementado, ambos devem ser campos adicionais, fora dos detalhes que participam da identidade da relação.

### 5.3 Estados de resolução

| Estado | Significado | Tratamento na wiki |
|---|---|---|
| `exact` | Binding semântico ou alvo previamente identificado de forma confiável | FACT, confiança 1.00 |
| `candidate` | Um candidato foi selecionado com critérios explícitos | FACT qualificado; conservar os critérios |
| `syntactic` | A sintaxe e o índice identificam o destino | FACT, confiança alta |
| `configured` | Configuração explícita identifica o destino | FACT, confiança alta |
| `convention` | Destino deriva de convenção reconhecida | INFERENCE, confiança média/alta |
| `dynamic` | Relação existe, mas o destino é calculado em runtime | UNKNOWN; preservar o texto e o contexto |
| `heuristic` | Destino provável, mas não conclusivo | INFERENCE, confiança baixa/média |
| `unresolved` | Não há evidência suficiente para identificar o alvo | UNKNOWN, com candidatos e tentativas |

`FactHeader.Resolution` mede o quanto o fato está comprovado; `ResolutionMethod` descreve a rota que levou ao alvo. Eles não são sinônimos. `Confidence`, quando existir em P3, será uma medida adicional e não deve substituir nenhum dos dois.

### 5.4 Ordem recomendada de estratégias

```text
alvo existente
  -> binding semântico
  -> candidatos do compilador
  -> tipo do receiver
  -> SymbolIndex
  -> banco de dados/mappings
  -> configuração
  -> protocolo específico
  -> convenção
  -> heurística
  -> não resolvido
```

Cada tentativa precisa ser determinística e auditável. Quando restarem candidatos indistinguíveis, o resultado correto é `unresolved`, não “o primeiro candidato”.

### 5.5 Relações a preservar

O modelo deve aceitar, entre outras, relações de código (`calls`, `creates`, `references`, `implements`, `inherits`, `depends-on`, `uses`, `registers`), eventos (`publishes`, `subscribes`, `handles`), comunicação (`http-call`, `uses-http-client`, `grpc-call`) e persistência (`reads`, `writes`, `inserts`, `updates`, `deletes`, `executes`, `maps-to`, `connects-to`).

É preferível acrescentar tipos a apagar detalhes da intenção: `updates` pode também produzir uma visão normalizada de `writes`, mas não deve perder a operação original.

### 5.6 Persistência e configurações

Resolução de entidade para tabela usa, nesta ordem: configuração explícita, metadata do ORM, mapeamento de `DbSet`, convenção, heurística e não resolução. Convenções nunca são `exact`.

SQL literal pode produzir relações sintáticas para tabela, coluna e filtros. SQL com destino interpolado ou calculado deve produzir `dynamic` e preservar as expressões disponíveis.

Destinos configurados — HTTP clients nomeados, endpoints, connection names, opções e feature flags — podem ser resolvidos por configuração, mas valores secretos jamais devem aparecer no grafo ou na wiki. Registre o nome e a finalidade, nunca senha, token, chave ou conteúdo de certificado.

## 6. Como produzir os artefatos para a wiki

### 6.1 Relações resolvidas canônicas — CONCLUÍDO

O `CanonicalAggregateWriter` já persiste relações resolvidas em partições de `raw/facts/relations/` e escreve `resolution.json` com métricas por método e por partição. A relação factual possui id determinístico, origem, destino quando comprovado, detalhes, evidência, proveniência, candidatos, diagnóstico e método de resolução.

O contexto transitório usado pelo resolver — receiver, tipos de argumentos e tentativas intermediárias — é propositalmente mantido apenas durante a execução. Isso preserva a estabilidade de `relation_id`, mas significa que P3 é necessário para tornar as tentativas auditáveis no artefato publicado.

### 6.2 Facts canônicos, manifest e acesso a dados — CONCLUÍDO

O formato factual já cobre projetos, documentos, símbolos, relações, banco, cobertura e diagnósticos. Os hashes e referências do manifest permitem localizar e validar cada fragmento sem depender de nomes de arquivo humanos. `DatabaseObjectFact` e `DatabaseColumnFact` já fornecem nós de persistência quando a identidade foi comprovada.

### 6.3 Índice de recuperação para consumidores futuros — PENDENTE

O próximo artefato do gerador não deve duplicar nem reescrever fatos. Deve ser uma projeção derivada, versionada e regenerável, produzida a partir de fragmentos persistidos, com shards limitados e índices por projeto, origem, destino, tipo de relação, entrada e estado de resolução.

Esse trabalho deve respeitar o ciclo factual existente: os fragmentos permanecem a autoridade; índices e resumos são apenas meios eficientes de recuperação. Não cria páginas, não interpreta domínio e não é uma implementação de ingestão. Ele substitui a proposta anterior de “serializar relações resolvidas”, pois essa serialização já existe.

### 6.4 Grafo de componentes e Mermaid — CONCLUÍDO

`component-graph` produz `ComponentFact` a partir do inventário ativo e substitui a projeção Mermaid baseada em
chaves incompatíveis por `ComponentGraphProjector`. A execução ponta a ponta prova arestas reais, deduplicadas e
determinísticas em `raw/dependencies.mmd`, que agora é base confiável para mapas de módulos e componentes.

### 6.5 Protocolos e destinos runtime — PENDENTE (P2 do RelationResolver)

Os requisitos `RELR-40` a `RELR-45` ainda estão pendentes: nós comprovados por literal para clientes/endereços HTTP, endpoints HTTP, configurações e destinos de eventos. A implementação deve primeiro decidir se porta ou substitui os detectores atualmente órfãos; simplesmente adicionar um resolver não é suficiente sem um produtor ativo de claims para DI, gRPC e ASP.NET Core.

### 6.6 Resolução auditável e enriquecimento cruzado — PENDENTE (P3 do RelationResolver)

Os requisitos `RELR-46` a `RELR-49` ainda estão pendentes: confiança por estratégia, registro de tentativas e um segundo passe que use relações já resolvidas para enriquecer outras. Essa é a fonte correta para `confidence` e `attempts`; eles não devem ser inventados pelo ingest da wiki.

### 6.7 Ingestão incremental — ADIADO

Quando o escopo de wiki for retomado, cada execução deverá comparar ids/hashes de fatos e relações com a execução anterior e então:

1. atualizar páginas impactadas;
2. manter páginas não impactadas;
3. recalcular links afetados;
4. resolver ou reabrir `UNKNOWN`s relacionados;
5. registrar proveniência da execução;
6. nunca reescrever indiscriminadamente a wiki inteira.

## 7. Schema da LLMWiki

O pacote `schema/` deve conter instruções para o agente, taxonomia, ontologia, convenções, evidências, confiança, ingestão, lint e templates padronizados.

### Taxonomia

As páginas possíveis são: `System`, `Module`, `Service`, `Component`, `Entry Point`, `Flow`, `Event`, `Integration`, `Entity`, `Business Rule`, `Configuration`, `Decision` e `Unknown`.

Esses são conceitos de documentação, não rótulos automáticos do código. Por exemplo, o sufixo `Service` em uma classe não basta para classificá-la como página `Service`; entradas HTTP precisam de evidência de rota/host; uma DTO não é automaticamente uma entidade.

### Ontologia

Relações permitidas incluem agrupamento (`contains`, `belongs-to`, `part-of`), código (`calls`, `creates`, `implements`, `inherits`, `depends-on`, `uses`, `configures`), comportamento (`publishes`, `subscribes`, `handles`, `reads`, `writes`, `validates`, `transforms`), integração (`integrates-with`, `communicates-with`, `persists-to`, `routes-to`) e fluxo (`starts-flow`, `continues-flow`, `ends-flow`, `branches-to`, `fails-to`, `retries`, `compensates`).

O nome da relação precisa refletir a evidência. Não trocar `uses` por `calls`, nem `writes` por `persists-to`, sem base adicional.

### Template universal de página

Todas as páginas, respeitando particularidades de seu tipo, devem ter:

```markdown
---
type: <taxonomy type>
id: <stable id>
status: active | deprecated | unknown
source-scope: <projects/documents>
last-reviewed-run: <run id>
---

# <nome>

## Summary

## Facts

## Inferences

## Relationships

## Evidence

## Unknowns

## Related
```

Um template específico adiciona campos úteis: rota e protocolo para entry points, etapas para flows, contrato/direção para eventos, operações/tabelas para integrações, propriedades/mapeamentos para entidades e escopo/efeito para regras de negócio.

### Linking e prevenção de duplicidade

Antes de criar uma página, procurar por `id` estável, símbolo canônico, caminho e aliases existentes. Atualizar a página já identificada quando a identidade for a mesma. Nomes visualmente parecidos não bastam para mesclar; nomes diferentes não bastam para separar.

Usar links internos para conceitos já conhecidos, preferencialmente pelo identificador/caminho canônico. Páginas devem ter links de entrada ou constar no índice de seu tipo; páginas deliberadamente isoladas devem explicar por quê.

## 8. Lint da wiki

Uma execução de lint deve falhar para:

- FACT sem evidência;
- INFERENCE sem evidência ou sem confiança;
- `exact`, `candidate`, `syntactic` ou `configured` sem origem/evidência;
- link interno quebrado;
- página duplicada para o mesmo id estável;
- página fora da taxonomia ou sem template mínimo;
- segredo aparente em conteúdo ou metadados;
- relação com origem ausente;
- conteúdo que declara certeza sobre um `dynamic` ou `unresolved`.

O lint deve alertar, mas não necessariamente falhar, para:

- inferência com confiança baixa;
- páginas sem links de entrada;
- UNKNOWN sem sugestão de investigação;
- páginas com evidência obsoleta em relação à última execução;
- mistura excessiva entre descrição técnica e hipótese de negócio.

## 9. Plano de evolução recomendado

Esta tabela é a fila de entrega. Cada linha `PENDENTE` tem um `feature` — o slug que, ao ser puxado, vira
`.specs/features/<feature>/` e passa pelas quatro fases do `tlc-spec-driven` (Specify → Design → Tasks →
Execute), exatamente como `csharp2md-v3`, `data-access-discovery`, `relation-collector` e `relation-resolver`
já foram entregues. `Bloqueado por` referencia outro `feature` desta mesma tabela; ausência de valor significa
que o item pode começar imediatamente. Este roadmap não implementa nada por si — ele só ordena e rastreia; a
implementação real e suas decisões (`AD-0xx`) continuam vivendo em `.specs/STATE.md`.

**Agnóstico de agente/LLM.** O processo `tlc-spec-driven` não é um comando exclusivo de uma ferramenta — é um
conjunto de arquivos (`SKILL.md`, `references/*.md` por fase, `scripts/*.py` para validação estrutural)
espelhado sem diferença de conteúdo em `.claude/skills/`, `.agents/skills/`, `.cursor/skills/` e
`.windsurf/skills/` (todos sob `tlc-spec-driven/`, hash idêntico). Um agente com invocação nativa de skill
(Claude Code, Cursor, Windsurf) usa esse atalho; um agente sem esse mecanismo (Codex ou qualquer outro com
apenas leitura de arquivo e shell) lê `SKILL.md` e a referência da fase atual diretamente e roda os mesmos
scripts de validação via shell — o resultado em `.specs/features/<feature>/` é idêntico nos dois caminhos.
Nenhuma linha desta tabela ou dos tickets locais assume Claude especificamente.

| Prioridade | Entrega | Estado | Feature | Bloqueado por | Critério de pronto |
|---|---|---|---|---|---|
| P0 | LLMWiki raw topic, factual fragments e manifest | **CONCLUÍDO** | `csharp2md-v3` / `csharp2md-llmwiki-phase1` | — | Fonte de verdade versionada, validada e navegável por referências |
| P0 | SymbolIndex, acesso a dados e RelationResolver P1 | **CONCLUÍDO** | `symbol-index` / `data-access-discovery` / `relation-collector` / `relation-resolver` | — | Relações e nós comprovados preservam evidência, método e incerteza |
| P0 | Índice de recuperação do gerador | PENDENTE | `relation-retrieval-index` | — | Shards e resumos permitem recuperar contexto sem carregar agregados gigantes; UNKNOWNs agrupados por causa; `resolution` vs `resolution_method` formalizados; métricas de qualidade por execução |
| P0 | ComponentFact + correção do Mermaid | **CONCLUÍDO** | `component-graph` | — | `dependencies.mmd` contém ao menos uma aresta real entre componentes; fecha AD-020 |
| P1 | Correção de seleção de candidato em `publishes` | PENDENTE | `relation-resolver-candidate-fix` | — | Relação `publishes` prefere o tipo do evento sobre classe/construtor quando ambos são candidatos |
| P1 | Produtores ativos de claims para DI/gRPC/ASP.NET Core | PENDENTE | `detection-tree-revival` | — | Decisão porta-ou-aposenta a árvore `Detection/` órfã registrada como `AD-0xx`; os três sinais produzem claims reais em execução ponta a ponta |
| P1 | Protocolos runtime: HTTP/eventos | PENDENTE | `relation-resolver-p2-http-events` | — | Subconjunto de `RELR-40`..`RELR-45` cobrindo clientes/rotas HTTP e destinos de eventos, usando claims que `relation-collector` já produz |
| P1 | Protocolos runtime: DI/gRPC/ASP.NET Core | PENDENTE | `relation-resolver-p2-di-grpc-aspnet` | `detection-tree-revival` | Restante de `RELR-40`..`RELR-45`; nós somente quando comprovados por literal/configuração |
| P2 | Índice de configuração (DI, opções, named clients, connection names, endpoints) | PENDENTE | `configuration-index` | `detection-tree-revival` (parcial, para DI) | Índice consultável sem capturar segredos; origem de §10 "Melhorias para aumentar precisão" #2 |
| P2 | Snapshots de raw entre execuções | PENDENTE | `raw-snapshot-diff` | — | Compara duas execuções e reporta relações removidas/alteradas/novas; origem de §10 "Melhorias para operação contínua" #1 |
| — | Pacote `schema/` completo | **ADIADO** | — | Será definido depois que o contrato do insumo estiver fechado |
| — | Ingest inicial: system/modules | **ADIADO** | — | Não é requisito do gerador atual |
| — | Ingest: entry points/services/components | **ADIADO** | — | Não é requisito do gerador atual |
| — | Ingest: events/integrations/entities | **ADIADO** | — | Não é requisito do gerador atual |
| P2 | Resolução auditável e enriquecimento cruzado | PENDENTE | `relation-resolver-p3` | `relation-resolver-p2-http-events`, `relation-resolver-p2-di-grpc-aspnet` | Requisitos `RELR-46`..`RELR-49` implementados sem instabilizar ids |
| — | Ingest: flows | **ADIADO** | — | Será discutido após a geração do insumo |
| — | Ingest: business rules/decisions | **ADIADO** | — | Será discutido após a geração do insumo |
| — | Lint e atualização incremental da wiki | **ADIADO** | — | Não é requisito do gerador atual |

## 10. Melhorias sugeridas

Cada item tem uma linha **Rastreamento** apontando para onde ele entrou na fila da seção 9 — como critério de
aceite de um `feature` existente, como `feature` novo próprio, ou `ADIADO` quando pertence ao escopo de
ingestão da wiki, ainda fora do escopo ativo do gerador.

### Melhorias imediatas

1. **Schema versionado para relações.** Acrescentar `schemaVersion` e `analysisRunId` evita incompatibilidade silenciosa entre o analisador e o ingest.
   **Rastreamento:** critério de aceite de `relation-retrieval-index` (ticket 02) — é exatamente o contrato de
   versionamento que a camada de recuperação precisa expor.
2. **Ids determinísticos.** Derivar ids de símbolo, kind, contexto e localização estável; não usar ids aleatórios para relações persistidas.
   **Rastreamento:** já satisfeito pela gramática `id1:` (AD-014) e por `RelationFactId.Create`
   (ownerId/kind/claim/ordinal, ver design de `relation-collector`). Adicionado como critério de
   *verificação* em `relation-retrieval-index` (ticket 02) em vez de um item de implementação novo, para que
   a garantia fique comprovada contra a saída real, não apenas presumida.
3. **Proveniência completa.** Registrar versão do repositório/análise, projeto, documento e hash de conteúdo em cada evidência relevante.
   **Rastreamento:** critério de aceite de `relation-retrieval-index` (ticket 02).
4. **Deduplicação semântica no serializer.** Colapsar apenas relações idênticas em semântica e evidência compatível; manter ocorrências distintas quando o local ou contexto altera o significado.
   **Rastreamento:** critério de aceite de `relation-retrieval-index` (ticket 02).
5. **Contrato de compatibilidade.** Tratar novos campos como aditivos e preservar campos desconhecidos no ingest.
   **Rastreamento:** critério de aceite de `relation-retrieval-index` (ticket 02).

### Melhorias para aumentar precisão

1. **Score explicável.** Para `candidate` e `heuristic`, registrar os critérios e pesos que levaram à escolha.
   **Rastreamento:** critério de aceite de `relation-resolver-p3` (ticket 07) — é a mesma tentativa/confiança
   auditável de `RELR-46`..`RELR-49`, só que com o detalhe explícito dos critérios/pesos por candidato.
2. **Índice de configuração.** Indexar registrations de DI, opções, named clients, connection names e endpoints sem capturar segredos.
   **Rastreamento:** `feature` novo, `configuration-index` (ticket 08 na fila local), bloqueado por
   `detection-tree-revival` para o sinal de DI. Opções (`Microsoft.Extensions.Options`) e connection
   names/identidade de banco ainda não têm produtor de claims ativo — fica como sub-escopo em aberto para a
   fase Specify de `configuration-index` decidir, não presumido aqui.
3. **Rastreio de chamada interprocedural limitado.** Definir limite de profundidade, detecção de ciclos e orçamento por entry point antes de montar flows.
   **Rastreamento: ADIADO.** É trabalho preparatório para `flows`, que a seção 9 já marca `ADIADO` — não entra
   na fila ativa do gerador.
4. **Classificação de generated code.** Marcar origem gerada e reduzir sua influência em inferências arquiteturais, salvo configuração explícita em contrário.
   **Rastreamento:** dividido. Marcar a origem gerada no fato/evidência é escopo ativo do gerador — critério de
   aceite de `relation-retrieval-index` (ticket 02). Reduzir a influência em *inferências* arquiteturais é
   escopo de ingestão da wiki (`INFERENCE`, seção 3) e fica **ADIADO**.
5. **Testes como evidência qualificada.** Testes corroboram comportamento, mas nomes de teste isolados não comprovam a implementação em runtime.
   **Rastreamento: ADIADO.** É uma regra de epistemologia de ingestão (como o lint/ingest da wiki deve tratar
   evidência de teste), não um fato que o gerador precisa produzir.

### Melhorias para operação contínua

1. **Snapshots de raw.** Comparar execuções para identificar relações removidas, alteradas e novas.
   **Rastreamento:** `feature` novo, `raw-snapshot-diff` (ticket 09 na fila local) — sem bloqueio, lê os
   mesmos fragmentos persistidos que todo o resto.
2. **Fila de UNKNOWNs.** Ordenar por impacto: entry points e relações de alta centralidade primeiro.
   **Rastreamento:** critério de aceite de `relation-retrieval-index` (ticket 02), que já agrupa UNKNOWNs por
   causa — este item soma a ordenação por impacto ao mesmo agrupamento.
3. **Relatório de cobertura.** Medir símbolos indexados, relações resolvidas por estado, entry points mapeados, links válidos e UNKNOWNs abertos.
   **Rastreamento:** critério de aceite de `relation-retrieval-index` (ticket 02) para símbolos
   indexados/relações por estado/entry points/UNKNOWNs abertos. "Links válidos" é específico de páginas da
   wiki e fica **ADIADO**.
4. **Revisão humana pontual.** Priorizar para revisão as inferências de baixa confiança que afetam flows, regras ou decisões.
5. **Lint no pipeline.** Executar lint antes de publicar a wiki ou aceitar atualização incremental.

## 11. Critério de sucesso da POC

A POC é bem-sucedida quando, para uma codebase C#/.NET não previamente conhecida, a wiki permite responder com evidência a perguntas como:

- Quais projetos e componentes relevantes existem?
- Quais são os entry points e o que eles acionam?
- Quais serviços e componentes participam de uma capacidade?
- Quais eventos são publicados e consumidos?
- Quais integrações externas e operações de dados foram identificadas?
- Como funciona um fluxo relevante, e onde ele ainda é incompleto?
- Quais conclusões são inferências e qual a confiança delas?

O resultado desejado não é uma documentação que pareça completa: é uma base de conhecimento que seja útil, rastreável e honesta sobre aquilo que ainda não se sabe.

## Apêndice A. Validação contra uma execução extensa — `custom`

Esta seção registra a validação do contrato acima contra a saída em `D:\workspace\csharp2md-local-output\custom`. Ela descreve o formato observado; não altera artefatos gerados.

### A.1 Inventário observado

| Item | Valor observado |
|---|---:|
| Versão do manifest | 2 |
| Versão do gerador | 3.0.1 |
| Modo efetivo de análise | `syntax-only` |
| Trust do resultado | `untrusted` |
| Projetos cobertos | 501 |
| Documentos cobertos | 11.851 |
| Arquivos de fatos | 12.368 |
| Relações reportadas | 206.484 |
| Relações brutas agregadas | aproximadamente 1,0 GiB |
| Projeções de relações | aproximadamente 694 MiB |
| Maior projeção (`structural.json`) | aproximadamente 642 MiB |

O resultado contém `raw/facts/manifest.json`, fatos particionados por documento/projeto, documentos Markdown em `raw/codebase/`, relação bruta em `raw/facts/relation/` e projeções por categoria em `raw/facts/relations/`. A estrutura é adequada como fonte de verdade e possui os elementos necessários para evidência: ids estáveis, proveniência do detector, caminho relativo e intervalo de linha/coluna.

### A.2 Ajuste de semântica: resultado versus método

O formato observado separa corretamente dois campos que devem permanecer distintos na LLMWiki:

| Campo | Pergunta respondida | Exemplo observado |
|---|---|---|
| `header.resolution` | Qual é a qualidade do alvo final? | `exact` |
| `resolution_method` | Qual estratégia chegou ao resultado? | `configured` |

Uma gravação de dados observada possui `header.resolution: exact` e `resolution_method: configured`: o alvo foi identificado exatamente por uma configuração. Portanto, a classificação da wiki deve seguir **`header.resolution`**, enquanto `resolution_method` deve ser mantido como proveniência técnica da conclusão.

Substitua a regra simplificada da seção 5.3 por esta regra operacional:

- `header.resolution` determina FACT, INFERENCE ou UNKNOWN;
- `resolution_method` explica *como* o resultado foi obtido;
- `configured`, `syntactic`, `candidate` e similares podem ser métodos de resolução, sem reduzir automaticamente um alvo exato a inferência;
- se um método heurístico produzir alvo não comprovado, `header.resolution` deve refletir a incerteza correspondente;
- ambos os campos devem ser preservados na projeção consumida pelo ingest.

### A.3 Cobertura e limites atuais

O resumo de resolução registra:

| Método | Quantidade | Proporção aproximada |
|---|---:|---:|
| `candidate` | 25.694 | 12,4% |
| `syntactic` | 30.636 | 14,8% |
| `configured` | 38 | menor que 0,1% |
| `dynamic` | 8 | menor que 0,1% |
| `unresolved` | 150.108 | 72,7% |

As projeções também registram 9.487 relações HTTP, todas não resolvidas, 235 de eventos e 60 de dados. `compile-time`, `dependency-injection` e `grpc` não apresentam relações nesta execução.

Esses zeros **não significam ausência no sistema**. O manifest declara `syntax-only`, portanto não houve análise semântica restaurada/compilada. O modo deve ser propagado ao ingest como limite de confiança: relações ausentes ou não resolvidas nesse modo não podem sustentar conclusões negativas sobre DI, gRPC, chamadas internas ou tipos externos.

### A.4 Decisões de ingestão para esta escala

Não entregue o diretório `raw/` inteiro, nem um arquivo de projeção de centenas de MiB, como contexto para uma LLM. O ingest deve ser orientado a recuperação (retrieval), com uma camada derivada, reproduzível e descartável — por exemplo `raw/index/` ou `derived/` — contendo:

1. índices por `source_id`, `target_id`, documento, projeto, tipo de relação e estado de resolução;
2. shards JSONL comprimidos ou arquivos pequenos, com teto previsível de tamanho;
3. um resumo de contagens por projeto e categoria;
4. um índice de entry points, integrações, eventos, entidades e relações de alta centralidade;
5. um registro de `UNKNOWN`s agrupado por causa, em vez de uma página para cada uma das 150.108 relações;
6. ponteiros para os fatos e trechos originais usados como evidência.

A projeção derivada nunca substitui `raw/facts`; ela existe apenas para tornar a ingestão eficiente e seletiva.

### A.5 Melhorias priorizadas descobertas na validação

| Prioridade | Melhoria | Motivo |
|---|---|---|
| P0 | Formalizar `resolution` e `resolution_method` no schema | Evita confundir qualidade do resultado com estratégia de resolução |
| P0 | Criar índice/sharding de relações para ingest | Há um fato de relações de cerca de 1,0 GiB e uma projeção estrutural de cerca de 642 MiB |
| P0 | Agrupar UNKNOWNs por causa, fonte e destino textual | Impede que o volume de não resolvidos produza milhares de páginas inúteis |
| P0 | Propagar `analysis.effective`, `trust` e `restore_performed` às páginas | Torna os limites da análise visíveis e auditáveis |
| P1 | Executar modo semântico quando viável | Deve elevar cobertura de chamadas, DI, herança genérica e destinos externos |
| P1 | Melhorar resolução HTTP | Todas as relações HTTP estão sem destino; rota, base address, client nomeado e configuração precisam ser encadeados |
| P1 | Ajustar seleção de candidatos por tipo de símbolo | Um evento observado lista classe e construtor como candidatos; a relação `publishes` deve preferir o tipo de evento |
| P2 | Emitir métricas de qualidade por execução | Comparar o percentual de resolvidas, dinâmicas e não resolvidas entre versões |

### A.6 Regra de priorização para a primeira ingestão

Para essa codebase, a primeira ingestão deve trabalhar por projeto/serviço e não por arquivo. A ordem recomendada é:

1. `solutions.json`, projetos e `dependencies.mmd` para mapa estrutural;
2. relações `data`, `events` e configurações resolvidas, porque são pouco volumosas e de alto valor explicativo;
3. entry points e relações de saída recuperadas por `source_id`;
4. componentes de alta centralidade no índice estrutural;
5. fluxos apenas depois que os componentes e entry points principais existirem.

Durante essa fase, relações HTTP sem rota ou destino devem virar UNKNOWNs agregados, por exemplo “chamadas HTTP sem rota identificada no projeto X”, com amostras e links para as evidências — nunca milhares de páginas individuais.

## Apêndice B. Alinhamento com a codebase e as especificações do gerador

Esta atualização foi conferida contra `D:\workspace\csharp2md`, incluindo `.specs/STATE.md`, as especificações de `csharp2md-v3`, `csharp2md-llmwiki-phase1`, `data-access-discovery`, `symbol-index` e `relation-resolver`.

### B.1 Capacidades já entregues pelo gerador

| Capacidade | Estado confirmado | Implicação para a LLMWiki |
|---|---|---|
| Tópico `raw/` e frontmatter | Concluído | `raw/codebase/` já é uma fonte estruturada e navegável; não é preciso reconstruir a árvore de código |
| Fragmentos factuais validados + manifesto | Concluído | Fatos possuem id, hash, referência, evidência e proveniência; o manifest é o ponto inicial de navegação |
| `SymbolIndex` | Concluído | A resolução é baseada em identidade de símbolo e pode reportar ambiguidade sem escolher um vencedor |
| Acesso a dados | Concluído | Banco possui catálogo e relações de dados; identidades são criadas apenas com evidência suficiente |
| RelationResolver P1 | Concluído | Os 39 requisitos P1 foram verificados; o resolver é o único escritor alcançável de `RelationFact` |
| Métricas e diagnósticos do resolver | Concluído | `resolution.json` e `diagnostics.json` suportam medição e investigação de lacunas |
| Modo semântico confiável | Concluído | Já existe como opção operacional; requer `--analysis semantic --trust trusted-solution` e não é um novo item de implementação |

### B.2 Restrições arquiteturais que o roadmap deve respeitar

1. **Fatos fragmentados são a autoridade.** A arquitetura do gerador valida, persiste, projeta Markdown e descarta fragmentos por documento; a ingestão não deve introduzir um segundo modelo concorrente.
2. **Relações vivem em um fragmento de solução.** Essa decisão permite que sejam resolvidas depois que todo o `SymbolIndex` existe. Ela explica o grande artefato observado e não deve ser revertida apenas para facilitar contexto de LLM.
3. **Índices devem ser derivados.** Um `WikiIndexProjector` pode ler os fragmentos persistidos e produzir shards recuperáveis, mas não pode mudar ids, provas ou semântica de `raw/facts`.
4. **Destino runtime não comprovado continua nulo.** URLs, chaves de ambiente, nomes lógicos e discovery names podem ser exibidos como texto observado, mas não se tornam serviço/integração por conveniência.
5. **A análise semântica é uma escolha de confiança.** A wiki deve expor modo efetivo, trust, restore e isolamento; não deve converter limitações do modo sintático em afirmações sobre ausência de comportamento.

### B.3 Backlog técnico real, em ordem de dependência

Nomes entre crases são os `feature` da tabela da seção 9 — a fila real vive lá; este diagrama só explica o
porquê das dependências. Itens `ADIADO` (ingest, flows, regras/decisões) aparecem apenas como referência
futura, fora da fila ativa.

```text
`component-graph`            `relation-retrieval-index`   `relation-resolver-candidate-fix`
       |                              |                              |
       v                              |                              |
grafo de componentes                  |                              |
   verificável                        |                              |
       |                              |                              |
       ' - - - (ADIADO: ingest) - - -'                               |
                                                                      |
`detection-tree-revival`                                             |
       |                                                             |
       v                                                             |
`relation-resolver-p2-http-events`   `relation-resolver-p2-di-grpc-aspnet`
       |                                       |
       +-------------------+-------------------+
                            v
              (ADIADO: entry points / integrações)
                            |
                            v
                 `relation-resolver-p3`
                            |
                            v
              (ADIADO: flows, regras e decisões)
```

`component-graph`, `relation-retrieval-index` e `relation-resolver-candidate-fix` não têm dependência entre
si e podem começar em paralelo — todos leem os mesmos fatos persistidos. `relation-resolver-p2-http-events`
não depende de `detection-tree-revival` (usa claims que `relation-collector` já produz), mas
`relation-resolver-p2-di-grpc-aspnet` depende, porque DI/gRPC/ASP.NET Core não têm produtor de claims ativo
hoje. `relation-resolver-p3` só faz sentido depois que os dois ramos de protocolo P2 aumentarem o volume de
relações resolvidas para enriquecer. A criação de páginas de módulos/componentes e um Mermaid útil para a
wiki (ADIADO) dependem de `component-graph`, mas não fazem parte da fila ativa.

### B.4 Itens que não devem ser relatados como “faltando”

- Serialização versionada de relações resolvidas: já existe em `raw/facts/relations/`.
- Facts canônicos, manifest, ids estáveis e evidência: já existem.
- Mapeamento entidade/tabela e propriedade/coluna comprovado: já existe na descoberta de acesso a dados.
- Análise semântica: já existe; o resultado observado estava em modo sintático por escolha de execução, não por ausência de capacidade.
- Criação de nós a partir de convenção, URLs ou nomes lógicos: não é falta; é uma proibição deliberada de evidência para evitar identidades falsas.

### B.5 Itens que permanecem explicitamente fora da implementação atual

`Feature` referencia a fila da seção 9 — é lá que a ordem e o status oficiais vivem.

| Área | Estado | Feature | Condição para avançar |
|---|---|---|---|
| Componentes e arestas Mermaid reais | Concluído | `component-graph` | Concluído: produtor de `ComponentFact`, resolução de endpoints e projeção Mermaid deduplicada |
| Índice de recuperação para consumidores | Pendente | `relation-retrieval-index` | Shards/resumos derivados dos fragmentos persistidos, sem duplicar `raw/facts` |
| Seleção de candidato em `publishes` | Pendente | `relation-resolver-candidate-fix` | Preferir o tipo do evento sobre classe/construtor quando ambos são candidatos |
| DI, gRPC, ASP.NET Core e referências compile-time no caminho ativo | Pendente | `detection-tree-revival` | Portar, religar ou aposentar conscientemente a árvore `Detection/` atualmente órfã |
| HTTP e eventos como nós de protocolo | Pendente, P2 do resolver | `relation-resolver-p2-http-events` | Subconjunto de `RELR-40`..`RELR-45` usando claims que `relation-collector` já produz |
| DI, gRPC, ASP.NET Core como nós de protocolo | Pendente, P2 do resolver | `relation-resolver-p2-di-grpc-aspnet` | Restante de `RELR-40`..`RELR-45`; depende de `detection-tree-revival` |
| Tentativas, confiança e segundo passe | Pendente, P3 do resolver | `relation-resolver-p3` | `RELR-46`..`RELR-49`, com campos fora da identidade da relação |
| Índice de configuração (DI, opções, named clients, connection names, endpoints) | Pendente | `configuration-index` | Origem §10 "aumentar precisão" #2; depende de `detection-tree-revival` para DI, sem produtor de claims para opções/connection names ainda |
| Snapshots de raw entre execuções | Pendente | `raw-snapshot-diff` | Origem §10 "operação contínua" #1; sem bloqueio |
| Wiki compilada, flows, regras de negócio e decisões | Pendente | — (ADIADO) | Ingestão baseada em recuperação, schema e lint; não pertence ao gerador factual por si só |
