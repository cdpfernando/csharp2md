# Verificação de aptidão para consumo por LLM

## Resultado

**Parecer: NÃO APTO para consumo autônomo ou de produção por uma LLM.**

O pacote está estruturalmente íntegro, autocontido e possui mecanismos de recuperação que funcionam. Entretanto, a execução não está certificada, as métricas obrigatórias não foram calculadas, há pelo menos um falso positivo arquitetural comprovado, um percurso real perdeu todas as invocações observadas e os maiores payloads excedem em muito um tamanho razoável de contexto.

O pacote pode ser usado **experimentalmente, com supervisão**, desde que a LLM trate todo fato arquitetural como uma pista a confirmar na projeção de fonte e não interprete ausência de relação como ausência de comportamento.

## Escopo

- Pacote: `artifacts/analyze-out/s-cb7a4be0b1a084f3b59e9c2f1e3906f1`
- Solução declarada: `eShopOnContainers-ServicesAndWebApps.sln`
- Data dos artefatos: 2026-08-27 10:01 (horário local do workspace)
- Estado do repositório durante a auditoria: `524d73e`, descrito por Git como `v2.0.0-675-g524d73e`
- A afirmação de que esta é a versão estável mais recente foi aceita como premissa do solicitante. O próprio pacote não registra versão do gerador, commit, build ou data de geração; portanto, a proveniência não pode ser comprovada somente pelo output.

## Matriz de aptidão

| Critério | Estado | Evidência |
| --- | --- | --- |
| Integridade do pacote | PASS | 1.892 artefatos no manifesto; todos existem; chaves e paths únicos; nenhum path absoluto ou com traversal |
| Links internos | PASS | 9.439 links Markdown examinados; 0 links internos quebrados; 1 link externo HTTP válido |
| Recuperação de fonte | PASS | `OrdersController.GetOrderAsync` recuperado integralmente em 19 linhas; hash publicado igual ao arquivo projetado |
| Redação de segredos | PASS | 14 fontes redigidas; 14 sidecars; hashes original/publicado e flag `redacted` consistentes |
| Navegação orientada por LLM | PARCIAL | Catálogos, postings, Markdown, `AGENTS.md` e `retrieval.md` existem, mas o guia aponta para payloads grandes e omite caminhos importantes |
| Legibilidade semântica | FAIL | Páginas usam títulos genéricos e IDs longos/percent-encoded; catálogos não carregam labels compactos |
| Escala/contexto | FAIL | Pacote de 289,75 MiB; 15 arquivos acima de 1 MiB; `contains.json` sozinho tem 169,28 MiB |
| Cobertura factual | FAIL | Quatro métricas obrigatórias estão em `0/0`; medições vazias |
| Certificação da execução | FAIL | `run-certification.json` declara `not_evaluated` |
| Confiabilidade de classificação | FAIL | Helper `private` publicado como `EntryPoint`; percurso real sem contabilização das invocações |
| Prontidão geral | FAIL | Não atende uso autônomo ou de produção |

## Inventário medido

- 1.893 arquivos, incluindo o manifesto
- 303.826.671 bytes (289,75 MiB)
- 638 arquivos JSON e 186 arquivos Markdown
- 6.087 fatos
- 19.991 observações
- 10.829 registros de relação, candidatos, unresolved e frontiers
- 691 diagnósticos
- 1.146 documentos estruturais e 1.160 artefatos em `source/` (inclui 14 sidecars de redação)
- 176 páginas arquiteturais Markdown
- 518 shards de postings

Maiores payloads JSON:

| Artefato | Tamanho |
| --- | ---: |
| `relations/confirmed/contains.json` | 169,28 MiB |
| `relations/confirmed/belongs-to.json` | 21,44 MiB |
| `observations/invocation.json` | 16,61 MiB |
| `observations/type-usage.json` | 16,21 MiB |
| `facts/structural.json` | 8,52 MiB |
| `relations/candidates.json` | 3,40 MiB |
| `relations/unresolved.json` | 2,53 MiB |
| `observations/object-creation.json` | 2,09 MiB |
| `facts/architecture.json` | 1,70 MiB |
| `relations/confirmed/executes.json` | 1,63 MiB |

Os postings estão particionados, mas os payloads canônicos de alta cardinalidade não. Isso conflita com o objetivo arquitetural de não ter arquivo monolítico de relação e torna a leitura direta proibitiva para uma LLM.

## Achados bloqueadores

### B1 — Execução não certificada

`run-certification.json` contém apenas `{"status":"not_evaluated"}`. `coverage.json` registra numerador e denominador zero para entry points, linked calls, contracts e persistence. `measurements.json` não possui registros.

Consequência: não há base para afirmar completude, cobertura, degradação ou custo de recuperação. A ausência de fatos pode ser confundida com ausência de comportamento.

### B2 — Falso positivo comprovado em `EntryPoint`

O método `CatalogController.ChangeUriPlaceholder`, declarado `private` na linha 288 da fonte projetada, foi publicado como `EntryPoint`. O próprio diagnóstico `missing-route-declaration` o identifica como uma action sem `RouteDeclaration`.

Consequência: uma LLM pode inventar uma superfície de entrada externa que não existe.

### B3 — Invocações observadas sem continuidade publicada

No cenário `OrdersController.GetOrderAsync`, o ledger possui três observações semânticas de invocação: `Ok`, `NotFound` e `IOrderQueries.GetOrderAsync`. O pacote também contém o símbolo abstrato `IOrderQueries.GetOrderAsync` e a implementação concreta `OrderQueries.GetOrderAsync`.

Mesmo assim, não existe registro com esse método como origem/owner em:

- `relations/confirmed/invokes.json`
- `relations/candidates.json`
- `relations/unresolved.json`
- `relations/frontiers.json`

Consequência: um fluxo iniciado em uma entry point real para exatamente no corpo inicial, sem indicar a continuação nem declarar honestamente a lacuna.

### B4 — Payloads fora de um orçamento prático de contexto

O guia manda abrir diretamente `facts/architecture.json` (1,70 MiB), `relations/confirmed/executes.json` (1,63 MiB), `facts/structural.json` (8,52 MiB) e `relations/candidates.json` (3,40 MiB). O maior arquivo do pacote tem 169,28 MiB.

Consequência: um agente que siga o guia literalmente desperdiça contexto ou falha antes de chegar ao fato relevante. Os postings particionados não resolvem o primeiro lookup porque o guia não ensina a selecionar o bucket e a localizar o registro canônico por ordinal sem carregar o shard monolítico.

### B5 — Guia de recuperação incompleto

`retrieval.md` apresenta sete cenários, mas:

- manda localizar identidades em `facts/architecture.json`, não nos catálogos;
- para `executes`, `implements-operation` e `invokes`, cita somente `executes.json`;
- para `uses-contract`, `accesses-data`, `operates-on` e `targets`, cita somente `accesses-data.json`;
- para candidatos e frontiers, cita somente `relations/candidates.json`;
- manda parar com base em `coverage.json`, que nesta execução está vazio (`0/0`).

Consequência: a documentação pode produzir respostas incompletas mesmo quando o pacote possui o artefato correto.

## Achados importantes não bloqueadores isoladamente

### I1 — Páginas e catálogos pouco legíveis

A página de `OrdersController.GetOrderAsync` tem o título genérico `EntryPoint`; o nome do método aparece somente dentro de um ID longo e percent-encoded. O catálogo de entry points também publica somente `fact_id`, `artifact_key`, `ordinal` e `fact_type`.

Isso preserva autoridade e determinismo, mas aumenta muito ruído e custo de busca. Uma projeção LLM-friendly deveria adicionar labels compactos e comprovadamente derivados, como componente, tipo, método, protocolo, verbo e rota.

### I2 — Cobertura de contratos não demonstrada

O pacote contém 62 observações de message operation e 6 boundary operations de mensageria outbound, mas não publicou facts, catálogo ou postings de contratos. Sem ground truth, isto não prova sozinho um falso negativo; prova, contudo, que esta execução não consegue sustentar perguntas sobre produtores/consumidores de contratos.

### I3 — HTTP com semântica fragmentada

Das 15 boundary operations HTTP, nenhuma preenche `http_method` ou `route`; todas usam `protocol_operation_key`, por vezes contendo o verbo junto da rota. As páginas Markdown não exibem essa chave, apenas direction e protocol.

### I4 — Diagnósticos volumosos e pouco priorizados

Distribuição dos 691 diagnósticos:

| Código | Quantidade |
| --- | ---: |
| `unsupported-document` | 549 |
| `missing-route-template` | 38 |
| `missing-route-declaration` | 33 |
| `missing-project` | 26 |
| `malformed-configuration-document` | 21 |
| `suspected-secret` | 21 |
| diagnósticos de coverage parciais | 3 |

Os 26 `missing-project` incluem entradas como `ApiGateways`, compatíveis com solution folders sendo tratadas como projetos ausentes. Os 21 documentos de configuração malformados podem refletir sintaxe aceita pelo provider de configuração .NET, mas rejeitada pelo parser estrito usado pelo gerador. Ambos merecem triagem antes de os diagnósticos orientarem uma LLM.

### I5 — Manifesto não informa cardinalidade das projeções

Todos os 1.859 artefatos de catálogo, posting, Markdown e source têm `count: 0` no manifesto, mesmo quando contêm registros. Os paths estão corretos, mas o manifesto não ajuda um consumidor a estimar custo ou escolher o shard.

## Evidências positivas

- O manifesto fecha exatamente o pacote: 1.892 entradas mais o próprio `manifest.json` totalizam os 1.893 arquivos encontrados.
- Nenhuma chave ou path do manifesto está duplicada; nenhum path é absoluto ou escapa do diretório.
- Todos os artefatos listados existem.
- Os 9.438 links internos Markdown resolvem; o único link restante é um HTTP externo válido.
- A recuperação de source por locator funciona no caso examinado e retorna o corpo completo do método.
- A projeção source do caso examinado preserva o SHA-256 esperado.
- Os 14 documentos com redação possuem sidecars que distinguem hash original e publicado; todas as 14 combinações foram validadas.
- Confirmed, candidate, unresolved e open frontier permanecem separados, evitando transformar incerteza em relação confirmada.

## Gates mínimos para mudar o parecer

1. Publicar `run_certification` avaliado e as quatro métricas com numerador, denominador, exclusões, unknowns e motivos de degradação.
2. Garantir que toda invocation reconhecida seja contabilizada exatamente uma vez como confirmada, candidata, unresolved, frontier ou exclusão diagnóstica justificável.
3. Impedir métodos privados/helpers de controller de serem promovidos a entry points; validar no eShop com amostra rotulada independente.
4. Particionar payloads canônicos de alta cardinalidade e medir um teto real de bytes/tokens por arquivo e por cenário.
5. Reescrever `retrieval.md` para começar em catálogos, selecionar buckets de postings e cobrir cada relation/proof-state no artefato correto.
6. Adicionar labels compactos e derivados aos catálogos/Markdown sem mudar a autoridade dos payloads.
7. Resolver ou certificar explicitamente a ausência de contratos e de continuidades de fluxo neste corpus.
8. Registrar no manifesto a proveniência do gerador (versão, commit/build e versão do contrato).
9. Executar cenários rotulados de ponta a ponta no corpus real, medindo arquivos, bytes/tokens, hops, fatos relevantes e ruído.

## Regra de uso temporário

Enquanto os gates acima não forem atendidos, uma LLM consumidora deve receber estas restrições:

1. fatos e relações são pistas, não garantia de completude;
2. toda conclusão relevante deve ser confirmada em `source/`;
3. ausência de relação nunca significa ausência de chamada, contrato ou efeito;
4. candidates, unresolved, frontiers e diagnostics devem ser consultados antes de responder;
5. arquivos canônicos grandes não devem ser carregados integralmente; usar busca textual, catálogos e postings;
6. respostas devem declarar que a execução está `not_evaluated`.
