# Compact Relation Retrieval Index Context

**Gathered:** 2026-08-23
**Spec:** `.specs/features/compact-retrieval-index/spec.md`
**Status:** Approved

---

## Feature Boundary

A feature substitui somente a representação física de `raw/index/`: dados exclusivos de relações,
metadados compartilhados normalizados, postings compactos para as cinco famílias existentes, shards
limitados, UNKNOWNs sem duplicação no resumo e uma medição reproduzível antes/depois. `raw/facts/`,
identidades factuais, resolução e semântica lógica das consultas permanecem inalterados.

---

## Implementation Decisions

### Contract and compatibility

- O formato compacto é o schema derivado 2.
- O reader falha explicitamente ao receber schema 1 ou qualquer versão desconhecida; não há dual-read transparente.
- Uma consulta bem-sucedida devolve os mesmos ids e payloads lógicos completos que o formato 1 devolvia, reconstruídos pelo reader.

### Compact location and layout

- Relações são ordenadas por `relation_id` e recebem ordinais inteiros densos.
- Postings e grupos UNKNOWN armazenam ordinais, nunca evidência, proveniência, texto observado ou extensões.
- Shards de relações, metadados e postings usam diretórios planos e descritores por faixa de ordinal ou SHA-256; nenhuma chave ganha diretório próprio.
- O posting de projeto preserva o primeiro projeto comprovado pela ordem das evidências, como no schema 1, mas sem repetir `project_id` no registro superior.

### Noise and redundancy removal

- Documento, projeto, caminho relativo e origem gerada são persistidos uma vez por documento.
- Referência de fragmento, SHA-256 e versão do gerador são persistidos uma vez por origem factual.
- Evidências guardam somente ordinal de documento, coordenadas e extensões próprias.
- `details` é um campo conhecido persistido uma vez; `observed_target_text` é derivado e não duplica `details` em `extensions`.
- O resumo não repete `analysis`, `trust` ou `restore_performed`; esses valores pertencem ao manifest.
- `indexed_symbol_count` é substituído por `indexed_endpoint_count`.
- Métricas separam todos os valores de `resolution` e `resolution_method`; percentuais não são persistidos.
- `entities.json`, `events.json`, `integrations.json` e `high-centrality.json` deixam de existir no schema 2.
- O catálogo de entry points comprovados permanece porque participa da priorização dos grupos UNKNOWN.

### Performance proof

- Contadores internos provam que um prefixo aceito não é reserializado.
- O índice é escrito diretamente como JSON UTF-8 não indentado, sem string UTF-16 intermediária.
- Fragmentos factuais de relações são consumidos progressivamente por stream.
- O benchmark usa 25000 relações determinísticas e processos isolados para as variantes 1 e 2.
- O relatório mantém amostras brutas e exige redução estrita de bytes, arquivos, mediana de tempo e mediana de pico de working set.
- O benchmark é um comando de Release reproduzível desta feature, não um gate permanente da suíte comum.

### Agent's Discretion

- Divisão exata das classes internas e nomes de tipos, desde que os contratos observáveis da spec permaneçam iguais.
- Quantidade de warm-ups e amostras do benchmark, desde que o relatório registre a metodologia e use processos novos.
- Forma exata dos descritores e algoritmo de busca por faixa de hash, desde que uma chave localize todos os shards candidatos sem abrir fatos.

### Declined / Undiscussed Gray Areas → Assumptions

- O ticket não definiu compatibilidade de leitura com schema 1. O default é rejeição explícita, registrado na spec.
- O ticket não definiu a identidade compacta. O default é ordinal inteiro por `relation_id`, registrado na spec.
- O ticket não definiu como tratar metadados repetidos. O default é normalizá-los por documento e origem factual, com reconstrução transparente pelo reader.
- O ticket não definiu a permanência dos catálogos derivados. O default é remover os quatro catálogos redundantes e manter somente entry points comprovados.
- O ticket não definiu a correção das métricas herdadas. O default é separar os dois eixos de resolução, remover percentuais deriváveis e usar o nome `indexed_endpoint_count`.
- O ticket não definiu limiar percentual para o benchmark. O default é redução estrita nas quatro métricas, sem margem adicional.
- O ticket não definiu se o benchmark pertence ao CI. O default é um comando reproduzível executado e registrado nesta entrega.

---

## Specific References

- Ticket `.scratch/llmwiki-roadmap/issues/10-compact-retrieval-index.md`.
- Formato 1 entregue por `.specs/features/relation-retrieval-index/`.

---

## Deferred Ideas

- Gate contínuo de performance em hardware controlado.
- Compressão do índice ou codificação binária de postings.
- Novas famílias de consulta ou ingestão da wiki.
