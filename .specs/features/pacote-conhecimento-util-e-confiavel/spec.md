# Pacote de conhecimento útil e confiável Specification

**Status:** Aprovada
**Origem normativa:** `docs/specs/pacote-conhecimento-util-e-confiavel.md`

## Problem Statement

O pacote atual publica inventário intermediário demais e respostas arquiteturais de menos. Ele excede os budgets de arquivos e contexto, não exercita as jornadas centrais, pode anunciar como comprometido um pacote que falha na validação posterior e mistura variantes de projetos distintos.

Esta feature substitui o contrato atual por um pacote compacto, factual, auditável e diretamente navegável. O consumidor deve localizar entidades, seguir fluxos, medir dependências e inspecionar evidências sem conhecer detalhes internos do gerador.

## Goals

- [ ] Publicar somente o grafo arquitetural retido, suas evidências e lacunas relevantes.
- [ ] Tornar quatro jornadas recuperáveis dentro de budgets verificáveis.
- [ ] Agregar dependências e medidas reproduzíveis em quatro escopos.
- [ ] Isolar variantes por projeto e solução.
- [ ] Garantir que todo pacote comprometido seja imediatamente reidratável e validável.
- [ ] Reduzir o output real aos limites definidos para eShopOnContainers e Pitstop.

## Out of Scope

| Feature | Reason |
| --- | --- |
| Motor de consulta, banco vetorial, embeddings, wiki ou UI | O pacote materializado deve ser navegável sem serviço externo. |
| Inferência de regras de negócio ou intenção humana | O gerador permanece factual. |
| Dataflow geral, reflexão e cobertura completa de runtime | Não são necessários para as jornadas contratadas. |
| Score composto de acoplamento, risco ou qualidade | As medidas devem ser explícitas e recalculáveis. |
| Suporte a outras linguagens | A entrega continua restrita a C#/.NET. |
| Compatibilidade com API, CLI, schemas ou layout legados | A substituição usa somente o contrato corrente. |
| Adaptadores, conversores, feature flags e leitores legados | Não haverá coexistência de formatos. |
| Otimização geral do tempo de análise | Só entra a redução decorrente de evitar trabalho ou persistência desnecessários. |
| Operações remotas no GitHub | O workstream é local. |

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Simplicidade da solução | Um contrato, um grafo retido, um plano de pacote e um caminho de validação | O usuário pediu simplicidade permanente e a especificação autoriza corte limpo. | sim |
| Contagem de tokens | Bytes UTF-8 divididos por `4.0`, com o estimador `csharp2md.tokens.bytes-per-token-v1` e o divisor declarados no manifesto | Dobra a estimativa anterior e cria margem de segurança na primeira versão; a baseline estável orientará a calibração posterior. | sim |
| Formato de identidade | ID público fixo de 20 caracteres e handle local base36 de até 6 caracteres | Elimina a amplificação observada de 69–534 caracteres sem reintroduzir semântica no ID. | sim |
| Priorização de lacunas | Ordenar por quantidade de jornadas retidas afetadas, depois por quantidade de raízes afetadas e por identidade canônica | Produz impacto reproduzível sem score subjetivo. | não |
| Falha e estado parcial | Materializar, reidratar e validar em staging antes da troca atômica | Nenhuma saída parcial pode substituir o último pacote válido. | sim |
| Retry e duplicatas | Repetir a mesma entrada avaliada e a mesma política produz bytes idênticos; identidades e evidências repetidas são deduplicadas | O contrato exige determinismo e referências compactas. | sim |
| Auth e rate limit | N/A porque a feature expõe CLI e arquivos locais, sem serviço de rede | Não existe fronteira autenticada neste escopo. | sim |
| Concorrência | Garantir apenas exclusão da troca do mesmo pacote e ausência de commit parcial | Sem servidor ou escrita distribuída, outras garantias seriam escopo adicional. | não |
| Ciclo de vida | N/A para TTL e arquivamento; uma publicação válida substitui atomicamente a anterior | O produto entrega snapshots locais, não armazenamento gerenciado. | sim |
| Dependências externas | Clones locais ampliam a aceitação quando presentes; sua ausência não falha CI | Os únicos testes obrigatórios usam fixture versionada. | sim |

**Open questions:** none. A confirmação desta especificação confirma também os defaults marcados como “não”.

## User Stories

### P1: Publicar somente conhecimento útil ⭐ MVP

**User Story**: Como consumidor, quero um pacote compacto iniciado por raízes arquiteturais para obter respostas sem percorrer o ledger de extração.

**Why P1**: Esta é a mudança de contrato que torna todas as demais jornadas úteis.

**Acceptance Criteria**:

1. **PKG-01:** The gerador SHALL publicar um único manifesto de entrada que liste as soluções, componentes, Deployment Units, pontos de entrada, operações de fronteira e links para as quatro jornadas.
2. **PKG-02:** The pacote padrão SHALL iniciar o grafo retido em Component, Deployment Unit, Entry Point e Boundary Operation comprovados.
3. **PKG-03:** The pacote padrão SHALL reter apenas fatos, relações, observações e evidências que sustentem uma jornada retida ou expliquem uma lacuna relevante dessa jornada.
4. **PKG-04:** The pacote padrão SHALL publicar Candidate, Unknown e Open Frontier somente quando o item puder alterar ou interromper uma jornada retida.
5. **PKG-05:** The pacote padrão SHALL excluir testes, observações sem promoção, usos de tipo não retidos, fontes sem citação, valores brutos de configuração e registros comuns em arquivos individuais.
6. **PKG-06:** The pacote padrão SHALL incluir conteúdo-fonte somente para documentos citados por evidências retidas.
7. **PKG-07:** The pacote padrão SHALL representar configuração por chaves, seções, vínculos, categoria e localização segura, sem valores de ambiente, credenciais, segredos ou caminhos absolutos.
8. **PKG-08:** WHERE a inclusão de testes for habilitada explicitamente, o gerador SHALL registrar essa escolha no manifesto e na identidade da execução.
9. **PKG-09:** The gerador SHALL publicar somente fatos e relações observáveis, sem interpretar regras de negócio.
10. **PKG-10:** WHEN a substituição estiver concluída THEN o repositório SHALL conter somente o contrato corrente, sem dispatch de versão, leitor legado, conversor ou rota de compatibilidade.

**Independent Test**: Executar a CLI sobre a fixture versionada e provar inclusão das raízes e exclusão de cada família proibida no pacote padrão.

---

### P1: Navegar e medir dependências ⭐ MVP

**User Story**: Como desenvolvedor ou arquiteto, quero consultar dependências e dependentes por escopo para explicar acoplamento e impacto.

**Why P1**: Recupera a pergunta central do produto sem transformar a projeção em nova verdade factual.

**Acceptance Criteria**:

1. **DEP-01:** The Retrieval Projection SHALL agregar Confirmed Relations nos escopos Document, Project, Component e Deployment Unit usando somente pertencimento comprovado.
2. **DEP-02:** The Retrieval Projection SHALL preservar separadamente Project Reference, invocação interna, uso estrutural de tipo, HTTP, gRPC, mensageria, contrato e persistência.
3. **DEP-03:** The dependência agregada SHALL declarar origem, destino, escopo, categorias participantes, quantidade de ocorrências, variantes observadas, natureza direta ou transitiva e referências às relações e evidências de origem.
4. **DEP-04:** WHEN várias ocorrências confirmadas tiverem a mesma origem, destino, escopo e categoria THEN a projeção SHALL publicar uma aresta agregada com a contagem total e evidências deduplicadas.
5. **DEP-05:** WHEN uma relação de baixo nível contribuir para mais de um escopo THEN a projeção SHALL reutilizar sua referência sem duplicar seu payload factual.
6. **DEP-06:** The contagem confirmada SHALL excluir Candidate, Unknown e Open Frontier.
7. **DEP-07:** WHEN o consumidor abrir uma dependência agregada THEN o pacote SHALL fornecer referências resolvíveis às Confirmed Relations e evidências que a sustentam.
8. **DEP-08:** IF apenas o nome de projeto, assembly ou diretório sugerir um serviço THEN o pacote SHALL manter o escopo como Project e não apresentar Service ou Deployment Unit.

**Independent Test**: Consultar a fixture a partir de documento, projeto, componente e Deployment Unit e recalcular cada aresta a partir das relações esperadas.

---

### P1: Publicar medidas explicáveis ⭐ MVP

**User Story**: Como arquiteto, quero medidas reproduzíveis para avaliar recorrência, acoplamento, ciclos e impacto reverso.

**Why P1**: “Útil” exige respostas mensuráveis, não um score opaco.

**Acceptance Criteria**:

1. **MET-01:** The fan-out SHALL ser a quantidade de destinos distintos alcançados por arestas retidas no escopo solicitado.
2. **MET-02:** The fan-in SHALL ser a quantidade de origens distintas que alcançam a entidade por arestas retidas no escopo solicitado.
3. **MET-03:** The quantidade de ocorrências SHALL contar contribuições confirmadas antes da deduplicação da aresta agregada.
4. **MET-04:** The cruzamento entre componentes SHALL contar arestas cujas entidades agregadas pertencem a componentes distintos comprovados.
5. **MET-05:** The participação em ciclos SHALL ser calculada sobre o grafo dirigido retido no escopo solicitado.
6. **MET-06:** The impacto reverso SHALL listar o conjunto alcançável pelo índice de entrada e declarar a profundidade percorrida.
7. **MET-07:** The pacote SHALL exibir quantidades relevantes de Candidate, Unknown e Open Frontier separadas das medidas confirmadas.
8. **MET-08:** The pacote SHALL omitir qualquer score composto ou rótulo automático de qualidade, risco ou acoplamento.

**Independent Test**: Recalcular todas as medidas a partir das arestas esperadas da fixture, sem chamar código de produção para formar as expectativas.

---

### P1: Recuperar respostas diretamente ⭐ MVP

**User Story**: Como consumidor humano ou automatizado, quero índices e Markdown equivalentes que conduzam diretamente às respostas.

**Why P1**: O pacote falha se o consumidor ainda precisar varrer shards ou interpretar IDs opacos.

**Acceptance Criteria**:

1. **NAV-01:** The manifesto SHALL apontar diretamente para índices de identidade, ponto de entrada/operação, outgoing, incoming, contrato, persistência e evidência/disposição.
2. **NAV-02:** The resumo Markdown SHALL apresentar componentes, Deployment Units, ciclos, maiores fan-in e fan-out e as quatro jornadas disponíveis.
3. **NAV-03:** The páginas Markdown de componentes, serviços e documentos retidos SHALL apresentar outgoing, incoming, medidas, efeitos e lacunas com links Markdown existentes.
4. **NAV-04:** WHEN o consumidor seguir uma jornada Markdown normal THEN o pacote SHALL dispensar enumeração de diretório, escolha manual de shard e decodificação de ID.
5. **NAV-05:** The Markdown e os índices legíveis por máquina SHALL derivar do mesmo conjunto retido e apresentar dependências e medidas equivalentes.
6. **NAV-06:** WHEN o consumidor localizar um componente THEN a jornada SHALL concluir em no máximo 5 leituras.
7. **NAV-07:** WHEN o consumidor localizar outra raiz suportada THEN a jornada SHALL concluir em no máximo 8 leituras e 12.000 tokens estimados.
8. **NAV-08:** WHEN o consumidor seguir um fluxo causal até contratos, efeitos externos e persistência THEN a jornada SHALL concluir em no máximo 32 leituras e 125.000 tokens estimados.
9. **NAV-09:** WHEN o consumidor calcular impacto reverso a partir de arquivo, projeto, componente, Deployment Unit, contrato ou dado THEN a jornada SHALL concluir em no máximo 32 leituras e 125.000 tokens estimados.
10. **NAV-10:** WHEN o consumidor inspecionar evidência ou disposição THEN a jornada SHALL concluir em no máximo 12 leituras e 25.000 tokens estimados.

**Independent Test**: Começar no manifesto e completar as quatro jornadas medindo somente os arquivos realmente abertos.

---

### P1: Isolar projetos, variantes e soluções ⭐ MVP

**User Story**: Como mantenedor de solução multi-target, quero variantes avaliadas por projeto sem duplicar identidades lógicas compatíveis.

**Why P1**: A contaminação atual rejeita soluções válidas e torna a evidência ambígua.

**Acceptance Criteria**:

1. **VAR-01:** WHEN um projeto for avaliado THEN o analisador SHALL criar somente as Analysis Variants resolvidas pela avaliação real desse projeto.
2. **VAR-02:** The analisador SHALL impedir que frameworks-alvo coletados de um projeto sejam aplicados a outro projeto da solução.
3. **VAR-03:** WHEN ocorrências compatíveis de várias variantes representarem a mesma entidade lógica THEN o analisador SHALL produzir uma identidade lógica única.
4. **VAR-04:** The localizador e a evidência de uma ocorrência SHALL declarar a Analysis Variant que os produziu.
5. **VAR-05:** IF duas ocorrências incompatíveis surgirem dentro da mesma Analysis Variant THEN o analisador SHALL rejeitar a colisão estrutural.
6. **VAR-06:** WHEN várias soluções forem analisadas no mesmo lote THEN identidades, variantes, deduplicação e handles SHALL permanecer isolados por pacote de solução.

**Independent Test**: Usar uma fixture com projeto web e cliente multi-target, identidade compartilhada e divergência real controlada.

---

### P1: Persistir identidades compactas e shards estáveis ⭐ MVP

**User Story**: Como consumidor, quero referências pequenas e deduplicadas para reduzir volume sem perder auditabilidade.

**Why P1**: IDs extensos e um arquivo por registro amplificam custo sem acrescentar conhecimento.

**Acceptance Criteria**:

1. **STO-01:** The ID público SHALL seguir `^[a-z]{3}_[0-9a-v]{16}$`, ter exatamente 20 caracteres e usar os primeiros 80 bits de SHA-256 da identidade canônica em base32hex minúsculo após um prefixo de tipo único.
2. **STO-02:** IF duas entradas canônicas diferentes produzirem o mesmo digest THEN o construtor do pacote SHALL falhar com diagnóstico explícito antes da publicação.
3. **STO-03:** The handle local SHALL seguir `^[0-9a-z]{1,6}$`, representar o ordinal base36 minúsculo atribuído a partir de `0` após ordenação pela chave canônica da tabela e ser resolvível diretamente por um índice declarado no manifesto.
4. **STO-04:** The pacote SHALL armazenar uma única entrada para cada identidade, documento, string e evidência repetida dentro do pacote da solução.
5. **STO-05:** The registros de projeção SHALL usar handles sem repetir ID público, caminho ou assinatura quando a tabela local já fornecer esses dados.
6. **STO-06:** The sharding SHALL agrupar registros deterministicamente por família e faixa real de bytes, sem criar um arquivo por registro comum.
7. **STO-07:** WHEN a mesma entrada avaliada e política forem processadas novamente THEN o gerador SHALL produzir bytes idênticos para o pacote da solução.

**Independent Test**: Gerar entradas pequenas e grandes, identidades profundas e evidências repetidas; comparar bytes, limites e cardinalidade de shards.

---

### P1: Comprometer somente pacotes válidos ⭐ MVP

**User Story**: Como operador, quero que “committed” signifique que o pacote materializado passa imediatamente pela mesma validação pública.

**Why P1**: Integridade anunciada e integridade observada não podem divergir.

**Acceptance Criteria**:

1. **PUB-01:** The publicação SHALL submeter fragmentos imediatos e diferidos à mesma materialização, normalização e coleção de validadores.
2. **PUB-02:** WHEN o plano do pacote estiver materializado em staging THEN o publicador SHALL reidratar e validar o resultado completo antes da troca atômica.
3. **PUB-03:** WHEN o comando de validação ler um pacote THEN ele SHALL reutilizar o mesmo leitor e as mesmas regras aplicadas antes do commit.
4. **PUB-04:** WHEN a análise anunciar um pacote como comprometido THEN uma validação imediata desse diretório SHALL passar sem diferença de interpretação.
5. **PUB-05:** IF variante, retenção, segurança, tamanho, materialização, reidratação ou validação falhar THEN o publicador SHALL preservar byte a byte o último pacote válido.
6. **PUB-06:** WHEN texto-fonte contiver comentário C# iniciado por `//` THEN a inspeção lexical SHALL impedir sua classificação como caminho UNC.
7. **PUB-07:** IF um caminho absoluto real alcançar o plano materializado THEN o publicador SHALL removê-lo, redigi-lo ou rejeitar o plano antes do commit, de modo que o valor nunca apareça no pacote comprometido.
8. **PUB-08:** IF uma publicação for rejeitada THEN o diagnóstico SHALL informar projeto, variante, família e causa aplicáveis.

**Independent Test**: Partir de um pacote válido, provocar cada classe de falha e verificar validação equivalente e preservação atômica.

---

### P1: Certificar utilidade no seam da CLI ⭐ MVP

**User Story**: Como responsável pelo produto, quero que a CLI prove respostas, integridade e budgets em uma fixture pequena e nos corpora locais disponíveis.

**Why P1**: Contar arquivos gerados ou testes unitários não demonstra utilidade do produto final.

**Acceptance Criteria**:

1. **CRT-01:** WHEN uma jornada for aplicável ao corpus THEN a certificação SHALL exercitá-la e falhar se ela não alcançar a resposta esperada.
2. **CRT-02:** WHEN uma jornada não for aplicável ao corpus THEN a certificação SHALL registrá-la como não aplicável com motivo, sem marcá-la como aprovada.
3. **CRT-03:** The pacote SHALL registrar métricas separadas de extração e publicação, itens filtrados por motivo e medidas por família, jornada e corpus.
4. **CRT-04:** WHEN eShopOnContainers estiver presente THEN o pacote comprometido SHALL conter no máximo 1.500 arquivos e 64 MiB.
5. **CRT-05:** WHEN Pitstop estiver presente THEN o pacote comprometido SHALL conter no máximo 750 arquivos e 25 MiB.
6. **CRT-06:** WHEN eShop estiver presente THEN a análise SHALL concluir sem colisão causada por aplicar uma variante de projeto a outro projeto.
7. **CRT-07:** WHERE eShop, eShopOnContainers ou Pitstop estiverem clonados localmente, o Verifier SHALL executar a aceitação correspondente; a ausência dos clones SHALL não falhar CI.
8. **CRT-08:** The fixture versionada SHALL conter multi-target por projeto, código de produção e teste, comentário `//`, caminho absoluto de configuração, Project Reference, chamada entre documentos, chamadas repetidas, dependência entre componentes, integração de runtime, ciclo e uma lacuna não confirmada.
9. **CRT-09:** WHEN a entrega for verificada THEN a CLI SHALL analisar a fixture, publicar, reidratar, validar e completar as quatro jornadas no mesmo seam de aceitação.

**Independent Test**: Executar a CLI ponta a ponta na fixture e, quando disponíveis, nos três clones locais; comparar respostas e budgets com este contrato.

## Edge Cases

1. **EDG-01:** IF uma relação não tiver evidência resolvível THEN o construtor SHALL omiti-la como Confirmed Relation ou rejeitar o plano antes do commit.
2. **EDG-02:** IF uma jornada aplicável exceder seu budget THEN a certificação SHALL falhar com a jornada e a medida excedida no diagnóstico.
3. **EDG-03:** IF o pacote exceder o limite estrutural aplicável ao corpus THEN a publicação SHALL falhar antes da troca atômica.
4. **EDG-04:** WHEN uma dependência existir em somente uma Analysis Variant THEN a projeção SHALL preservar essa qualificação sem duplicar as identidades de origem e destino.
5. **EDG-05:** IF Markdown e representação de máquina divergirem THEN a validação SHALL classificar o pacote como corrompido e impedir o commit.

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| PKG-01 | Conhecimento útil, critério 1 | Design | Pending |
| PKG-02 | Conhecimento útil, critério 2 | Design | Pending |
| PKG-03 | Conhecimento útil, critério 3 | Design | Pending |
| PKG-04 | Conhecimento útil, critério 4 | Design | Pending |
| PKG-05 | Conhecimento útil, critério 5 | Design | Pending |
| PKG-06 | Conhecimento útil, critério 6 | Design | Pending |
| PKG-07 | Conhecimento útil, critério 7 | Design | Pending |
| PKG-08 | Conhecimento útil, critério 8 | Design | Pending |
| PKG-09 | Conhecimento útil, critério 9 | T13 | Complete |
| PKG-10 | Conhecimento útil, critério 10 | Design | Pending |
| DEP-01 | Dependências, critério 1 | Design | Pending |
| DEP-02 | Dependências, critério 2 | T13 | Complete |
| DEP-03 | Dependências, critério 3 | Design | Pending |
| DEP-04 | Dependências, critério 4 | Design | Pending |
| DEP-05 | Dependências, critério 5 | Design | Pending |
| DEP-06 | Dependências, critério 6 | Design | Pending |
| DEP-07 | Dependências, critério 7 | Design | Pending |
| DEP-08 | Dependências, critério 8 | Design | Pending |
| MET-01 | Medidas, critério 1 | Design | Pending |
| MET-02 | Medidas, critério 2 | Design | Pending |
| MET-03 | Medidas, critério 3 | Design | Pending |
| MET-04 | Medidas, critério 4 | Design | Pending |
| MET-05 | Medidas, critério 5 | Design | Pending |
| MET-06 | Medidas, critério 6 | Design | Pending |
| MET-07 | Medidas, critério 7 | Design | Pending |
| MET-08 | Medidas, critério 8 | Design | Pending |
| NAV-01 | Recuperação, critério 1 | Design | Pending |
| NAV-02 | Recuperação, critério 2 | Design | Pending |
| NAV-03 | Recuperação, critério 3 | Design | Pending |
| NAV-04 | Recuperação, critério 4 | Design | Pending |
| NAV-05 | Recuperação, critério 5 | Design | Pending |
| NAV-06 | Recuperação, critério 6 | Design | Pending |
| NAV-07 | Recuperação, critério 7 | Design | Pending |
| NAV-08 | Recuperação, critério 8 | Design | Pending |
| NAV-09 | Recuperação, critério 9 | Design | Pending |
| NAV-10 | Recuperação, critério 10 | Design | Pending |
| VAR-01 | Variantes, critério 1 | Design | Pending |
| VAR-02 | Variantes, critério 2 | Design | Pending |
| VAR-03 | Variantes, critério 3 | Design | Pending |
| VAR-04 | Variantes, critério 4 | Design | Pending |
| VAR-05 | Variantes, critério 5 | Design | Pending |
| VAR-06 | Variantes, critério 6 | Design | Pending |
| STO-01 | Armazenamento, critério 1 | Design | Pending |
| STO-02 | Armazenamento, critério 2 | Design | Pending |
| STO-03 | Armazenamento, critério 3 | Design | Pending |
| STO-04 | Armazenamento, critério 4 | Design | Pending |
| STO-05 | Armazenamento, critério 5 | Design | Pending |
| STO-06 | Armazenamento, critério 6 | Design | Pending |
| STO-07 | Armazenamento, critério 7 | Design | Pending |
| PUB-01 | Publicação, critério 1 | Design | Pending |
| PUB-02 | Publicação, critério 2 | Design | Pending |
| PUB-03 | Publicação, critério 3 | Design | Pending |
| PUB-04 | Publicação, critério 4 | Design | Pending |
| PUB-05 | Publicação, critério 5 | Design | Pending |
| PUB-06 | Publicação, critério 6 | Design | Pending |
| PUB-07 | Publicação, critério 7 | Design | Pending |
| PUB-08 | Publicação, critério 8 | Design | Pending |
| CRT-01 | Certificação, critério 1 | Design | Pending |
| CRT-02 | Certificação, critério 2 | Design | Pending |
| CRT-03 | Certificação, critério 3 | Design | Pending |
| CRT-04 | Certificação, critério 4 | Design | Pending |
| CRT-05 | Certificação, critério 5 | Design | Pending |
| CRT-06 | Certificação, critério 6 | Design | Pending |
| CRT-07 | Certificação, critério 7 | Design | Pending |
| CRT-08 | Certificação, critério 8 | T13 | Complete |
| CRT-09 | Certificação, critério 9 | Design | Pending |
| EDG-01 | Edge case 1 | T13 | Complete |
| EDG-02 | Edge case 2 | Design | Pending |
| EDG-03 | Edge case 3 | Design | Pending |
| EDG-04 | Edge case 4 | T13 | Complete |
| EDG-05 | Edge case 5 | Design | Pending |

**Coverage:** 71 requisitos, todos mapeados para Design; nenhum requisito não mapeado.

## Success Criteria

- [ ] A fixture versionada completa as quatro jornadas pelo seam da CLI e passa na validação imediata.
- [ ] eShop conclui sem colisão artificial quando o clone local estiver presente.
- [ ] eShopOnContainers respeita 1.500 arquivos e 64 MiB quando o clone local estiver presente.
- [ ] Pitstop respeita 750 arquivos e 25 MiB quando o clone local estiver presente.
- [ ] Cada jornada respeita seus limites de leituras e tokens.
- [ ] Cada medida publicada é recalculável a partir das arestas retidas.
- [ ] O pacote padrão não contém testes, valores brutos de configuração, caminhos absolutos nem inventário intermediário desconectado.
