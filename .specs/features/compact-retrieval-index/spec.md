# Compact Relation Retrieval Index Specification

## Problem Statement

O índice derivado atual replica cada `RetrievalRelationEntry` completo em até cinco famílias de consulta,
cria um diretório por chave e reserializa todo o prefixo do shard a cada nova entrada. Em execuções de alta
cardinalidade, esse formato multiplica bytes, arquivos, tempo de projeção e memória. Esta feature normaliza o
índice derivado sem alterar `raw/facts/` nem os resultados observáveis das consultas existentes.

## Goals

- [ ] Serializar os dados exclusivos de cada relação exatamente uma vez, normalizar metadados compartilhados e reconstruir o payload lógico completo pelo reader.
- [ ] Manter shards UTF-8 limitados a 262144 bytes com trabalho de serialização linear.
- [ ] Eliminar diretórios por chave, separar UNKNOWNs do resumo e tornar a versão compacta inequivocamente incompatível com o formato anterior.
- [ ] Remover catálogos, métricas e campos redundantes ou semanticamente enganosos do schema derivado.
- [ ] Registrar uma medição reproduzível que comprove redução de bytes, arquivos, tempo de projeção e pico de memória.

## Out of Scope

| Feature | Reason |
| --- | --- |
| Alterar `raw/facts/`, ids factuais ou o `RelationResolver` | Os fatos permanecem a autoridade conforme AD-008, AD-010, AD-014 e AD-019. |
| Adicionar novas famílias ou mudar a semântica das consultas | A feature preserva projeto, origem, destino, tipo de relação e resolução. |
| Manter leitura transparente do schema derivado 1 | Confundir os dois layouts silenciosamente violaria o requisito de versionamento; o leitor falha explicitamente. |
| Gerar páginas, flows ou ingestão da wiki | O roadmap mantém essas etapas fora do gerador factual. |
| Comparar snapshots factuais históricos | `raw-snapshot-diff` continua sendo uma feature separada. |
| Transformar o benchmark em gate permanente de CI | A medição é reproduzível e executada nesta entrega; estabilidade contínua de performance exige infraestrutura própria. |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here - nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Versão do schema derivado | O formato compacto usa `schema_version: 2`; o leitor compacto rejeita qualquer outra versão com erro que informa versão recebida e esperada. | O formato muda estruturalmente e não pode ser interpretado como evolução aditiva do schema 1. | y |
| Localizador de relação | Cada relação recebe um ordinal inteiro, baseado na ordenação ordinal por `relation_id`; postings e grupos UNKNOWN armazenam somente esses ordinais. | Um inteiro é menor que repetir o id textual e continua determinístico e resolvível pelo armazenamento de relações. | y |
| Layout de alta cardinalidade | Relações usam shards planos em `raw/index/relations/`; metadados usam shards planos em `raw/index/metadata/`; postings usam shards planos em `raw/index/postings/<family>/`, sem subdiretório por chave. Descritores de faixa de ordinal ou SHA-256 no manifest localizam os shards candidatos. | O número de diretórios fica constante, Fact IDs longos não inflam o manifest e uma consulta continua seletiva. | y |
| Compatibilidade observável | Uma consulta retorna os mesmos `relation_id` e campos lógicos completos, na mesma ordem ordinal, que o schema 1 retornava para a mesma família e chave. O reader reconstrói metadados compartilhados normalizados. | Preserva o comportamento do consumidor sem exigir que cada registro físico repita informações de documento e proveniência. | y |
| Escopo de projeto | O posting de projeto preserva a regra do schema 1: usa o primeiro `project_id` comprovado pela ordem das evidências, mas deriva esse valor do metadado referenciado em vez de repeti-lo no registro superior. | Mantém os resultados observáveis da consulta por projeto sem duplicar o campo físico. | y |
| Metadados compartilhados | Documento, projeto, caminho relativo e origem gerada são persistidos uma vez por documento; referência do fragmento, SHA-256 e versão do gerador são persistidos uma vez por origem factual. | Esses valores se repetem em grandes quantidades de evidências e não são dados exclusivos de uma relação. | y |
| Catálogos derivados | O schema 2 omite `entities.json`, `events.json`, `integrations.json` e `high-centrality.json`; o catálogo de entry points comprovados permanece porque participa da priorização de UNKNOWNs. | Os quatro catálogos removidos repetem endpoints ou ids deriváveis das relações, e `high-centrality` não aplicava limiar algum. | y |
| Métricas | O resumo usa `indexed_endpoint_count`, separa contagens completas de `resolution` e `resolution_method` e não persiste percentuais deriváveis. | `indexed_symbol_count` incluía endpoints que não eram símbolos, e `dynamic` estava sendo contado no eixo incorreto. | y |
| Codificação JSON | Artefatos do índice são JSON UTF-8 sem indentação, escritos diretamente em bytes e terminados por uma única nova linha. | Remove espaço e alocações intermediárias sem afetar a semântica do consumidor. | y |
| Limite de shard | Relações, metadados e postings têm teto inclusivo de 262144 bytes UTF-8. Um registro individual que não caiba falha antes da publicação do manifest. | Preserva o contrato existente e impede descarte silencioso. | y |
| UNKNOWNs | `unknowns.json` é o único artefato com grupos e seus localizadores; `summary.json` contém apenas `unknown_group_count` e `unknowns_path`. | Evita repetir listas potencialmente grandes no resumo. | y |
| Instrumentação | A projeção registra contagens de serialização de registros e postings; cada valor é serializado uma vez antes de ser anexado ao shard. | Torna a complexidade linear verificável sem usar tempo de parede como proxy em teste unitário. | y |
| Corpus de medição | O benchmark gera deterministicamente 25000 relações com chaves únicas de origem e destino, executa cada variante em processo novo, registra amostras brutas e compara as medianas. | O volume amplia a diferença estrutural e processos isolados tornam tempo e pico de memória comparáveis. | y |
| Critério do benchmark | O formato compacto deve ter valor estritamente menor que o formato 1 em bytes, arquivos, mediana de tempo e mediana de pico de working set; qualquer empate ou regressão falha a medição. | O ticket exige redução nas quatro métricas, não apenas ausência de regressão. | y |

**Open questions:** none - all resolved or logged above.

### Implicit-Requirement Dimensions

| Dimension | Resolution |
| --- | --- |
| Input validation & bounds | O teto inclusivo é 262144 bytes para todo shard; versão, família, chave, ordinal, metadado e localizador inválidos falham de forma determinística. |
| Failure / partial-failure states | Um erro não publica `raw/index/manifest.json`; `raw/facts/` permanece byte a byte inalterado e continua sendo a autoridade. |
| Idempotency / retry / duplicate handling | A mesma entrada produz o mesmo `analysis_run_id`, os mesmos ordinais, caminhos, postings e bytes. Relações distintas por `relation_id` nunca são colapsadas. |
| Auth boundaries & rate limits | N/A porque a projeção é local, não expõe endpoint e não possui usuário remoto. |
| Concurrency / ordering | A projeção mantém ordenação ordinal determinística e não introduz execução concorrente. |
| Data lifecycle / expiry | N/A porque o índice continua descartável e é substituído como parte de cada output novo; a política de retenção não muda. |
| Observability | Contadores de serialização e o relatório de benchmark registram trabalho, bytes, arquivos, tempo e pico de memória; métricas de qualidade preservadas separam os dois eixos de resolução. |
| External-dependency failure | N/A porque a projeção e o benchmark não acessam rede nem serviço externo. |
| State-transition integrity | O manifest é o marcador de publicação e só referencia shards completos do schema 2. |

---

## User Stories

### P1: Recuperar relações por postings compactos ⭐ MVP

**User Story**: Como consumidor do output factual, quero consultar relações pelas cinco chaves existentes sem
carregar o agregado factual e sem armazenar cinco cópias do mesmo payload.

**Why P1**: Esta é a normalização que remove a maior multiplicação de volume sem retirar capacidade do consumidor.

**Acceptance Criteria**:

1. WHEN uma execução projeta relações persistidas THEN the compact-index system SHALL serializar os dados exclusivos de cada relação exatamente uma vez em `raw/index/relations/`. <!-- event-driven -->
2. The compact-index system SHALL representar consultas por `project_id`, `source_id`, `target_id`, `relation_kind` e `resolution` com postings que contêm somente a chave e ordinais de relações. <!-- ubiquitous -->
3. The compact-index system SHALL omitir dos postings evidência, proveniência, texto observado, extensões, metadados compartilhados e qualquer outro campo do payload lógico da relação. <!-- ubiquitous -->
4. WHEN uma chave for consultada THEN the compact-index reader SHALL retornar os mesmos `relation_id` e os mesmos campos lógicos completos, em ordem ordinal, que o schema 1 retornava para a mesma entrada. <!-- event-driven -->
5. WHEN uma consulta resolve postings e registros de relação THEN the compact-index reader SHALL concluir a consulta sem abrir `raw/facts/relations/*.json`. <!-- event-driven -->
6. IF uma relação não possuir `target_id` THEN the compact-index system SHALL omitir somente seu posting de destino e preservar seus postings de origem, tipo, resolução e primeiro projeto comprovado pela ordem das evidências. <!-- unwanted-behavior -->

**Independent Test**: Projetar um corpus com relações resolvidas e não resolvidas, consultar cada uma das cinco
famílias somente pelo índice compacto e comparar ids e payloads com os resultados do formato de referência.

---

### P1: Escrever shards limitados com custo linear

**User Story**: Como operador do gerador, quero que o índice mantenha limites previsíveis sem reserializar
trabalho anterior nem criar uma árvore de diretórios proporcional às chaves.

**Why P1**: Sem este comportamento, o índice continua inviável no volume que motivou o ticket.

**Acceptance Criteria**:

1. The compact-index system SHALL limitar cada shard de relações, metadados e postings a no máximo 262144 bytes UTF-8, incluindo envelope e nova linha final. <!-- ubiquitous -->
2. WHEN um registro individual produz exatamente 262144 bytes THEN the compact-index system SHALL aceitá-lo como um shard válido. <!-- event-driven -->
3. IF um registro individual de relação, metadado ou posting produz mais de 262144 bytes THEN the compact-index system SHALL falhar com um diagnóstico determinístico que informa o tipo do registro, sua identidade ou chave e o teto 262144. <!-- unwanted-behavior -->
4. WHEN N relações e seus postings são emitidos THEN the compact-index system SHALL registrar exatamente N serializações de payload completo e uma serialização por lista de postings emitida, sem serializar novamente um prefixo já aceito. <!-- event-driven -->
5. WHEN uma relação adicional é anexada ao corpus THEN the compact-index system SHALL aumentar o trabalho instrumentado somente pelas serializações dessa relação e dos postings que ela acrescenta ou cria. <!-- event-driven -->
6. WHEN o número de chaves únicas de origem e destino aumenta de 10 para 10000 THEN the compact-index system SHALL manter o mesmo conjunto de diretórios de relações e famílias de postings, sem criar diretório por chave. <!-- event-driven -->
7. WHEN um artefato do índice é serializado THEN the compact-index system SHALL escrevê-lo diretamente como JSON UTF-8 não indentado, sem materializar uma string UTF-16 intermediária. <!-- event-driven -->
8. WHEN fragmentos factuais de relações são lidos THEN the compact-index system SHALL consumi-los progressivamente por stream, sem usar `File.ReadAllBytes` nem materializar o documento JSON factual completo. <!-- event-driven -->

**Independent Test**: Executar writers instrumentados no limite exato, no excesso de um byte e em corpus de alta
cardinalidade; comparar contadores de serialização e os diretórios criados.

---

### P1: Publicar um contrato compacto inequívoco e determinístico

**User Story**: Como autor de um consumidor, quero detectar imediatamente o formato físico do índice e receber
bytes determinísticos para poder armazenar, comparar e reprocessar o output com segurança.

**Why P1**: Um schema novo sem fronteira explícita permite leitura incorreta e corrupção silenciosa.

**Acceptance Criteria**:

1. The compact-index system SHALL registrar `schema_version: 2` no manifest, no resumo, nos shards de relações, nos shards de postings e no artefato UNKNOWN. <!-- ubiquitous -->
2. IF o compact-index reader recebe um documento cujo `schema_version` não é 2 THEN the reader SHALL rejeitá-lo com um erro que informa a versão recebida e a versão esperada. <!-- unwanted-behavior -->
3. The compact-index system SHALL calcular `analysis_run_id` a partir da identidade de entrada e do schema 2, sem reutilizar a identidade do schema 1. <!-- ubiquitous -->
4. WHEN duas projeções recebem a mesma entrada em ordens diferentes THEN the compact-index system SHALL produzir o mesmo `analysis_run_id`, os mesmos ordinais, os mesmos postings, os mesmos caminhos e bytes idênticos em todos os arquivos do índice. <!-- event-driven -->
5. IF uma execução não contém relações THEN the compact-index system SHALL publicar manifest, resumo, catálogo de entry points e UNKNOWNs válidos do schema 2, sem shards de relações ou postings e com contagens zero. <!-- unwanted-behavior -->

**Independent Test**: Projetar duas permutações da mesma entrada, comparar todos os arquivos byte a byte e provar
que o leitor rejeita um manifest e um shard do schema 1.

---

### P1: Manter UNKNOWNs em um único artefato

**User Story**: Como revisor da análise, quero acessar os mesmos grupos UNKNOWN priorizados sem duplicar suas
listas no resumo da execução.

**Why P1**: Em execuções grandes, repetir localizadores de relações no resumo recria o custo que a compactação remove.

**Acceptance Criteria**:

1. WHEN relações não resolvidas são agrupadas THEN the compact-index system SHALL persistir todos os grupos somente em `raw/index/unknowns.json`, usando ordinais para localizar suas relações completas. <!-- event-driven -->
2. The compact-index system SHALL manter a ordenação dos grupos por entry point comprovado, impacto descendente e identidade ordinal. <!-- ubiquitous -->
3. The compact-index summary SHALL conter `unknown_group_count` e `unknowns_path` e SHALL omitir grupos UNKNOWN, `relation_id` e arrays de ordinais. <!-- ubiquitous -->
4. WHEN um grupo UNKNOWN é resolvido pelo reader THEN the compact-index reader SHALL retornar os mesmos `relation_id`, causa, origem, texto observado, contagem e impacto que o formato 1 expunha. <!-- event-driven -->

**Independent Test**: Projetar grupos repetidos e priorizados, inspecionar a forma exata do resumo e resolver o
artefato único de UNKNOWNs contra os registros completos.

---

### P1: Eliminar metadados e projeções redundantes

**User Story**: Como consumidor do índice, quero receber somente informação factual ou derivada que acrescente
sinal para que o formato compacto não preserve ruído, nomes enganosos ou cópias regeneráveis.

**Why P1**: Normalizar apenas as famílias de consulta deixaria grande parte da repetição e duas métricas
semanticamente incorretas dentro do novo schema.

**Acceptance Criteria**:

1. WHEN evidências referenciam o mesmo documento THEN the compact-index system SHALL persistir `document_id`, `project_id`, caminho relativo e origem gerada uma vez no armazenamento de metadados e usar um ordinal de documento nas evidências. <!-- event-driven -->
2. WHEN relações provêm do mesmo fragmento factual THEN the compact-index system SHALL persistir referência, SHA-256 e versão do gerador uma vez na proveniência compartilhada e omitir esses valores das evidências individuais. <!-- event-driven -->
3. WHEN uma relação contém `details` THEN the compact-index system SHALL persistir `details` uma vez como campo conhecido e derivar `observed_target_text` sem copiar `details` para `extensions`. <!-- event-driven -->
4. The compact-index summary SHALL omitir `analysis`, `trust` e `restore_performed`, e o compact-index reader SHALL obtê-los somente do manifest. <!-- ubiquitous -->
5. The compact-index summary SHALL publicar `indexed_endpoint_count` e SHALL omitir o campo enganoso `indexed_symbol_count`. <!-- ubiquitous -->
6. WHEN métricas por partição ou tipo de relação são publicadas THEN the compact-index summary SHALL separar contagens completas de `resolution` das contagens completas de `resolution_method` e SHALL omitir percentuais deriváveis. <!-- event-driven -->
7. The compact-index system SHALL omitir os artefatos `catalogues/entities.json`, `catalogues/events.json`, `catalogues/integrations.json` e `catalogues/high-centrality.json`. <!-- ubiquitous -->
8. WHEN o reader reconstrói uma relação THEN the compact-index reader SHALL combinar relação, metadados e proveniência sem duplicar, perder ou fabricar campos. <!-- event-driven -->

**Independent Test**: Projetar relações do mesmo fragmento e documento, verificar a ocorrência física única de
cada metadado, a ausência dos quatro catálogos e dos campos removidos e comparar o payload lógico reconstruído
com o payload de referência.

---

### P1: Comprovar a redução em corpus grande

**User Story**: Como mantenedor do gerador, quero uma comparação reproduzível entre o formato entregue pelo
ticket 02 e o formato compacto para decidir com evidência se a migração cumpre seu objetivo.

**Why P1**: A feature é motivada por escala; testes apenas funcionais não provam que a escala melhorou.

**Acceptance Criteria**:

1. WHEN o benchmark é executado THEN the benchmark system SHALL gerar o mesmo corpus de 25000 relações, registrar runtime e sistema operacional e executar os formatos 1 e 2 em processos separados. <!-- event-driven -->
2. WHEN cada variante termina THEN the benchmark system SHALL registrar bytes totais, quantidade de arquivos, amostras de tempo de projeção e amostras de pico de working set em um relatório JSON versionado. <!-- event-driven -->
3. WHEN o relatório compara as medianas THEN the benchmark system SHALL comprovar que o formato 2 tem bytes, arquivos, tempo de projeção e pico de working set estritamente menores que o formato 1. <!-- event-driven -->
4. IF qualquer uma das quatro métricas empata, regride ou não é registrada THEN the benchmark system SHALL encerrar com status diferente de zero e identificar a métrica. <!-- unwanted-behavior -->

**Independent Test**: Executar o comando documentado em Release, validar o schema do relatório e confirmar as
quatro desigualdades estritas usando os valores registrados.

---

## Edge Cases

- IF dois `relation_id` iguais aparecem na entrada THEN the compact-index system SHALL falhar antes de atribuir ordinais ambíguos.
- IF um posting referencia um ordinal inexistente THEN the compact-index reader SHALL falhar e informar família, chave e ordinal.
- IF uma evidência referencia um ordinal de documento ou proveniência inexistente THEN the compact-index reader SHALL falhar e informar o ordinal e o shard da relação.
- IF uma chave de posting atravessa dois shards por causa do teto THEN the compact-index reader SHALL combinar todos os shards candidatos e retornar cada relação uma vez em ordem ordinal.
- IF a última entrada cabe no shard mas a vírgula, o fechamento do envelope ou a nova linha excede 262144 bytes THEN the compact-index system SHALL iniciar outro shard.
- IF um shard, manifest ou UNKNOWN pertence a outro `analysis_run_id` THEN the compact-index reader SHALL rejeitar a composição antes de retornar resultados.
- WHEN uma relação contém extensões opacas ou múltiplas evidências THEN the compact-index system SHALL preservar extensões somente no registro a que pertencem e o reader SHALL retornar cada evidência reconstruída sem perda após resolver um posting.

## Requirement Traceability

| Requirement ID | Story | Phase | Exact evidence | Status |
| --- | --- | --- | --- | --- |
| CRI-01 | P1: Recuperar relações por postings compactos | T8 | `RetrievalIndexProjectorTests.Project_WritesOneRelationPayloadAndOrdinalOnlyPostingsThenManifestLast` asserts the exact relation ids stored once under relation shards. | Verified |
| CRI-02 | P1: Recuperar relações por postings compactos | T7 | `CompactRetrievalIndexBuilderTests.Build_StoresOnlyKeysAndOrdinalsAcrossFivePostingFamiliesAndOmitsAbsentTargetOnly` asserts all five families and their ordinal arrays. | Verified |
| CRI-03 | P1: Recuperar relações por postings compactos | T7 | `RetrievalIndexProjectorTests.Project_WritesOneRelationPayloadAndOrdinalOnlyPostingsThenManifestLast` asserts every posting entry has exactly `key` and `relation_ordinals`. | Verified |
| CRI-04 | P1: Recuperar relações por postings compactos | T9 | `RelationRetrievalIndexEndToEndTests.AllFiveLookups_ReconstructExactRealRelationSetsWithoutFactualReads` compares ids and complete logical payloads for all five families. | Verified |
| CRI-05 | P1: Recuperar relações por postings compactos | T9 | `RetrievalIndexReaderV2Tests.Query_ReconstructsEveryLogicalFieldWithoutOpeningFacts` and the real-output five-lookup test assert that no `raw/facts/` path is opened. | Verified |
| CRI-06 | P1: Recuperar relações por postings compactos | T7 | `CompactRetrievalIndexBuilderTests.Build_StoresOnlyKeysAndOrdinalsAcrossFivePostingFamiliesAndOmitsAbsentTargetOnly` asserts only the absent target posting is omitted. | Verified |
| CRI-07 | P1: Escrever shards limitados com custo linear | T5 | `BoundedUtf8ShardWriterTests.Pack_AcceptsAnExactLimitAndRejectsOneByteOver` and `Pack_SplitsRecordsIntoConsecutiveEnvelopes` assert the complete-envelope byte ceiling. | Verified |
| CRI-08 | P1: Escrever shards limitados com custo linear | T5 | `BoundedShardWriterTests.Write_DefaultLimitAccepts262144BytesAndRejects262145ByteSingleton` asserts an exact 262144-byte artifact is accepted. | Verified |
| CRI-09 | P1: Escrever shards limitados com custo linear | T5 | `BoundedUtf8ShardWriterTests.Pack_AcceptsAnExactLimitAndRejectsOneByteOver` and `BoundedShardWriterTests.Write_OversizedSingletonThrowsWithFamilyKeyAndRelationId` assert deterministic over-limit diagnostics. | Verified |
| CRI-10 | P1: Escrever shards limitados com custo linear | T5 | `RetrievalIndexProjectorTests.Project_WritesOneRelationPayloadAndOrdinalOnlyPostingsThenManifestLast` asserts exact relation, metadata, posting-list, posting-ordinal and envelope counters. | Verified |
| CRI-11 | P1: Escrever shards limitados com custo linear | T7 | `RetrievalIndexProjectorTests.Project_AddingOneRelationIncrementsOnlyItsLinearSerializationWork` asserts the exact incremental counter deltas. | Verified |
| CRI-12 | P1: Escrever shards limitados com custo linear | T11 | `CompactRetrievalIndexBuilderTests.Build_PreservesHighCardinalityUniqueKeys` covers 10 and 10000 unique keys; the canonical report records only 56 schema-2 files at 25000 keys. | Verified |
| CRI-13 | P1: Escrever shards limitados com custo linear | T5 | `RetrievalIndexProjectorTests.Project_WritesOneRelationPayloadAndOrdinalOnlyPostingsThenManifestLast` validates strict UTF-8 and exactly one final LF in every index artifact. | Verified |
| CRI-14 | P1: Escrever shards limitados com custo linear | T6 | `FactualFragmentScannerTests.ScanRelations_YieldsOneDisposedValueAtATimeAcrossBufferBoundaries` proves progressive stream consumption across buffers. | Verified |
| CRI-15 | P1: Publicar um contrato compacto inequívoco e determinístico | T4 | `RelationRetrievalIndexEndToEndTests.RRI07_RRI08_RRI10_RRI11_RRI12_SyntaxOnlySummaryReportsQualityAndLimits` and `PackagingSmokeTests.PackedTool_RunFromOutsideRepo_LoadsFixtureSolutionAndReportsProjectCount` assert schema 2 in real and packed output. | Verified |
| CRI-16 | P1: Publicar um contrato compacto inequívoco e determinístico | T9 | `RetrievalIndexReaderV2Tests.Open_RejectsEverySchemaExceptTwoWithReceivedAndExpectedVersions` asserts received and expected versions. | Verified |
| CRI-17 | P1: Publicar um contrato compacto inequívoco e determinístico | T8 | `RetrievalIndexProjectorTests.ComputeAnalysisRunId_IsSchemaTwoBoundAndManifestOrderInvariant` proves schema-2 binding and inequality with schema 1. | Verified |
| CRI-18 | P1: Publicar um contrato compacto inequívoco e determinístico | T11 | `RetrievalIndexProjectorTests.Project_FragmentPermutationsProduceIdenticalPathsAndBytes` compares run id, every path and every byte across input permutations. | Verified |
| CRI-19 | P1: Publicar um contrato compacto inequívoco e determinístico | T8 | `RetrievalIndexProjectorTests.Project_EmptyInputPublishesOnlyValidZeroArtifacts` asserts valid empty manifest, summary, entry points and UNKNOWNs with no relation/posting shards. | Verified |
| CRI-20 | P1: Manter UNKNOWNs em um único artefato | T7 | `RetrievalIndexProjectorTests.Project_NormalizesMetadataKnownFieldsExtensionsAndUnknowns` asserts UNKNOWN ordinals in the dedicated catalogue. | Verified |
| CRI-21 | P1: Manter UNKNOWNs em um único artefato | T7 | `CompactRetrievalIndexBuilderTests.Build_UnknownGroupsAreUniqueAndOrderedByEntryPointImpactThenOrdinal` asserts entry-point, impact and ordinal ordering. | Verified |
| CRI-22 | P1: Manter UNKNOWNs em um único artefato | T8 | `RetrievalIndexProjectorTests.Project_NormalizesMetadataKnownFieldsExtensionsAndUnknowns` asserts summary count-only shape and absence of entries. | Verified |
| CRI-23 | P1: Manter UNKNOWNs em um único artefato | T9 | `RetrievalIndexReaderV2Tests.ReadUnknownGroups_ReconstructsCompleteRelations` asserts id, cause, origin, observed text, count and reconstructed relations. | Verified |
| CRI-24 | P1: Eliminar metadados e projeções redundantes | T7 | `CompactRetrievalIndexBuilderTests.Build_NormalizesKnownRepeatedAndEvidenceOnlyMetadataIntoDenseDeterministicTables` asserts deduplicated document metadata and dense ordinals. | Verified |
| CRI-25 | P1: Eliminar metadados e projeções redundantes | T7 | The same builder test asserts deduplicated origin metadata; `RetrievalIndexProjectorTests.Project_NormalizesMetadataKnownFieldsExtensionsAndUnknowns` asserts evidence stores ordinals only. | Verified |
| CRI-26 | P1: Eliminar metadados e projeções redundantes | T4 | `RetrievalIndexProjectorTests.Project_NormalizesMetadataKnownFieldsExtensionsAndUnknowns` asserts known details once, derived target text and no copied `details` extension. | Verified |
| CRI-27 | P1: Eliminar metadados e projeções redundantes | T8 | `RelationRetrievalIndexEndToEndTests.RRI07_RRI08_RRI10_RRI11_RRI12_SyntaxOnlySummaryReportsQualityAndLimits` asserts analysis/trust/restore live in manifest while summary omits them. | Verified |
| CRI-28 | P1: Eliminar metadados e projeções redundantes | T7 | That real-output summary test asserts `indexed_endpoint_count` and absence of `indexed_symbol_count`. | Verified |
| CRI-29 | P1: Eliminar metadados e projeções redundantes | T7 | `CompactRetrievalIndexBuilderTests.Build_MetricsExposeEveryResolutionAndMethodBucketSeparatelyAndCountDistinctEndpoints` asserts complete independent resolution and method buckets. | Verified |
| CRI-30 | P1: Eliminar metadados e projeções redundantes | T10 | `CanonicalAggregateWriterTests.Write_ReplacementRemovesSchema1CataloguesAndPerKeyShards` and the real-output writers test assert all four redundant catalogues are absent. | Verified |
| CRI-31 | P1: Eliminar metadados e projeções redundantes | T9 | `RetrievalIndexReaderV2Tests.Query_ReconstructsEveryLogicalFieldWithoutOpeningFacts` asserts every reconstructed relation/evidence/extension field. | Verified |
| CRI-32 | P1: Comprovar a redução em corpus grande | T12 | `RetrievalIndexBenchmarkHarnessTests.SmallCorpus_UsesFreshProcessesAndRetainsEveryRawSample` asserts isolation; `benchmark-report.json` records `corpus`, `environment` and `methodology` for exactly 25000 relations. | Verified |
| CRI-33 | P1: Comprovar a redução em corpus grande | T12 | The harness test asserts raw arrays; `benchmark-report.json.variants` records total bytes, file count and all raw time/memory samples for both schemas. | Verified |
| CRI-34 | P1: Comprovar a redução em corpus grande | T13 | `benchmark-report.json.comparison` is true for all four strict reductions and its variants record the compared medians/totals. | Verified |
| CRI-35 | P1: Comprovar a redução em corpus grande | T12 | `RetrievalIndexBenchmarkHarnessTests.Comparison_RequiresEveryMetricToBePresentAndStrictlyLower` asserts missing, equal and regressed metric names; the canonical benchmark command exited zero. | Verified |

**Coverage:** 35 total, 35 mapped to tasks, 0 unmapped.

---

## Success Criteria

- [x] As cinco consultas retornam payloads lógicos semanticamente idênticos ao formato 1 usando somente manifest, metadados, postings e shards de relações do schema 2.
- [x] Dados exclusivos de cada relação e metadados compartilhados existem uma vez, todo shard respeita 262144 bytes e a instrumentação prova serialização linear.
- [x] Dez e dez mil chaves únicas produzem o mesmo conjunto de diretórios, UNKNOWNs não são duplicados no resumo e o reader rejeita schema incorreto.
- [x] Os quatro catálogos redundantes e os campos enganosos não existem; métricas de qualidade mantêm `resolution` e `resolution_method` separados.
- [x] O relatório reproduzível registra e comprova redução estrita nas quatro métricas exigidas.
