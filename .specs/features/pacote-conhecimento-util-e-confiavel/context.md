# Pacote de conhecimento útil e confiável Context

**Gathered:** 2026-09-15
**Spec:** `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`
**Status:** Confirmado

## Feature Boundary

Substituir o contrato publicado pelo csharp2md por um pacote compacto, factual, diretamente navegável e validado de ponta a ponta. A feature inclui retenção, dependências, medidas, variantes, identidade compacta, publicação atômica, Markdown, índices de máquina e certificação das quatro jornadas. Não inclui query engine, compatibilidade legada ou interpretação de regras de negócio.

## Implementation Decisions

### Simplicidade e corte limpo

- Manter somente o contrato corrente.
- Não criar adaptadores, leitores legados, feature flags ou caminhos paralelos.
- Reusar código existente apenas quando ele satisfizer diretamente o novo contrato e reduzir a solução.
- Remover testes e infraestrutura que existam apenas para fixar o produto substituído.

### Módulos profundos e orientados a resultados

- A análise produz um grafo factual em memória.
- A construção recebe o grafo e uma política e produz um plano completo do pacote.
- A publicação recebe o plano, materializa, reidrata, valida e retorna apenas um pacote comprometido.
- Roslyn, passes, sharding, handles e staging permanecem detalhes internos.
- A CLI completa é o único seam externo de aceitação.

### Retenção e projeções

- O grafo publicado começa em Component, Deployment Unit, Entry Point e Boundary Operation.
- Relações confirmadas formam o fechamento retido até contratos, sistemas externos, persistência e efeitos observáveis.
- Lacunas entram somente quando afetam as mesmas jornadas.
- Markdown e índices de máquina derivam do mesmo modelo retido.
- Dependência é uma Retrieval Projection auditável de Confirmed Relations, não uma nova verdade factual.

### Integridade e armazenamento

- Variantes são avaliadas por projeto.
- Identidade lógica e ocorrências qualificadas por variante permanecem separadas.
- IDs públicos têm exatamente 20 caracteres no formato `<tipo>_<digest-base32hex-80-bits>`; tabelas ordenadas por chave canônica atribuem handles base36 de até 6 caracteres a partir de `0`.
- Identidades, strings e evidências são deduplicadas dentro do pacote da solução.
- A primeira versão estima tokens como bytes UTF-8 divididos por `4.0`; o manifesto registra a fórmula e o divisor para permitir calibração posterior.
- Fragmentos imediatos e diferidos passam por um único caminho de materialização e validação.
- A troca do pacote acontece somente depois de reidratação e validação completas em staging.

### Agent's Discretion

- Nomes internos de tipos e arquivos.
- Algoritmos simples que preservem exatamente as medidas definidas na spec.
- Faixa-alvo de bytes dos shards, desde que os budgets e a ausência de arquivo por registro sejam provados.
- Ordem interna de implementação, desde que os estados intermediários incompletos sejam explícitos e o estado final passe pelo seam da CLI.

### Declined / Undiscussed Gray Areas → Assumptions

- A ordenação de lacunas e a garantia mínima de concorrência estão registradas na seção `Assumptions & Open Questions` da spec. A aprovação da spec confirma esses defaults.

## Specific References

- `docs/specs/pacote-conhecimento-util-e-confiavel.md` contém o diagnóstico, as 58 histórias originais, as decisões de implementação e as baselines medidas em 2026-09-15.
- O pedido “sempre mantenha simples” é uma restrição transversal deste workstream.

## Known Limitations

- A extração causal reconhece efeitos externos por nome de método (`CausalRelationExtractor.cs:218-227`) e só emite `contract` a partir de publicação de mensagem genérica. Stacks .NET comuns ficam invisíveis: o Pitstop declara HTTP por atributo com Refit (`WebApp/RESTClients/CustomerManagementAPI.cs:11`) e publica por `IMessagePublisher.PublishMessageAsync(string, object, string)`, então mediu zero fatos de HTTP, gRPC, messaging e contrato em 2026-09-16. Alargar a extração para clientes HTTP declarados por atributo, endpoints de controller e publicação não-genérica com payload resolvível é um workstream próprio, ainda não iniciado.

## Deferred Ideas

- Alargamento da extração causal descrito em `Known Limitations`.
- Query engine, embeddings, wiki, UI e interpretação de regras de negócio.
- Compatibilidade ou migração de pacotes legados.
- Otimização geral do tempo de análise.
