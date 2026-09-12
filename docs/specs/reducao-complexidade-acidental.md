# Redução da complexidade acidental

**Status:** spec de intervenção parcialmente implementada; pendências descritas abaixo.  
**Triage:** `ready-for-agent` — registro local, sem publicação no GitHub.  
**Revisão:** 2026-09-11, sobre HEAD e alterações locais.

## Problem Statement

Manter e evoluir o csharp2md exige acompanhar regras repetidas, parâmetros divergentes e documentação que descreve um estado anterior do produto. Isso aumenta o risco de corrigir um consumidor e deixar outro inconsistente, recriar trabalho já entregue ou publicar uma associação sem evidência suficiente.

A revisão da árvore atual confirmou reconstrução de índices sobre entradas estáveis, buscas repetidas para gerar labels, reconhecimento duplicado entre classificação e cobertura, associação HTTP por posição textual, leitura incorreta de parâmetros genéricos e capacidades condicionais sem consumidores de produção. Também encontrou discrepâncias entre os parâmetros de publicação, composição e avaliação de orçamento, além de uma razão descrita como medida sem evidência de calibração localizada.

O tamanho do código ou sua aparência não demonstram AI slop. Esta intervenção trata mecanismos sem uso efetivo, duplicação de decisões, afirmações sem sustentação e consequências concretas de manutenção ou comportamento. A própria suíte de testes faz parte da intervenção: cenários apenas anunciados, verificações que repetem a implementação e infraestrutura sem uso também aumentam a complexidade e devem ser removidos ou consolidados quando não protegem comportamento distinto.

## Solution

Reduzir o esforço de manutenção e tornar os resultados mais confiáveis, preservando o motor existente e suas cinco assemblies. Consolidar operações equivalentes, corrigir os consumidores que interpretam dados incorretamente e transportar os parâmetros efetivos pelos caminhos de publicação e avaliação.

O usuário deve continuar recebendo conhecimento factual, auditável e navegável, com distinção entre fatos, candidatos, unknowns e open frontiers. Uma refatoração não deve alterar fatos ou identidades corretos. Correções de associação, cobertura, proveniência e layout devem ter diferenças explicadas e verificadas.

A intervenção mantém os 13 itens da revisão e reconhece as mudanças locais já implementadas. Sua organização em entregas menores não autoriza reescrever o motor nem iniciar funcionalidades downstream.

## User Stories

1. Como mantenedor, quero que os documentos ativos descrevam o estado real do produto, para não planejar trabalho já entregue.
2. Como mantenedor, quero distinguir encerramento de workstream de certificação integral, para conhecer as lacunas ainda abertas.
3. Como agente de implementação, quero receber regras aplicáveis ao projeto, para não conservar restrições temporárias de features encerradas.
4. Como mantenedor, quero uma fonte única para instruções compartilhadas entre agentes, para evitar orientações divergentes.
5. Como mantenedor, quero comentários que expliquem regras vigentes, para entender o código sem reconstruir o histórico de tarefas.
6. Como mantenedor, quero uma sequência explícita de etapas reais, para compreender a análise sem stubs ou etapas vazias de produção.
7. Como operador, quero que falhas identifiquem o trabalho que falhou, para diagnosticar problemas de análise, projeção ou composição.
8. Como operador, quero que uma publicação malsucedida preserve o último pacote válido, para continuar consumindo uma saída consistente.
9. Como mantenedor, quero uma implementação compartilhada para cada operação equivalente de leitura de assinaturas e payloads, para corrigir o contrato em um único lugar.
10. Como consumidor, quero parâmetros genéricos, modificadores e tipos compostos interpretados corretamente, para que o reconhecimento de payloads use tipos completos.
11. Como consumidor, quero que melhorias na leitura preservem as identidades existentes, para manter referências estáveis nos casos corretos.
12. Como operador, quero que a análise reutilize índices de entradas estáveis, para evitar trabalho repetido sem introduzir estado entre soluções.
13. Como consumidor, quero labels recuperados por consultas reutilizáveis e citações determinadas pelo layout publicado, para navegar até o registro correto.
14. Como consumidor, quero que uma operação HTTP seja associada ao cliente que a executa, para confiar na fronteira publicada.
15. Como consumidor, quero que a ordem de criação de clientes independentes não altere essa associação, para obter resultados baseados no receptor.
16. Como consumidor, quero que nomes próprios semelhantes a APIs HTTP ou de mensageria não produzam fatos de framework, para evitar falsos positivos.
17. Como consumidor, quero que receptores reatribuídos ou sem origem demonstrável mantenham a limitação rastreável, para não receber destinos inventados.
18. Como mantenedor, quero que classificação e cobertura compartilhem regras equivalentes de reconhecimento, para evitar divergências de população elegível.
19. Como consumidor, quero que candidatos não resolvidos continuem representados nos denominadores, para que a cobertura não reflita somente os casos promovidos.
20. Como operador, quero que publicação factual, projeções e composição usem o limite efetivo da execução, para que o orçamento declarado corresponda à saída.
21. Como consumidor, quero recuperar todos os fatos e resolver as citações após a fragmentação, para não perder conhecimento quando o pacote crescer.
22. Como consumidor, quero que um registro indivisível acima do limite seja publicado sem truncagem e com degradação explícita, para distinguir essa exceção de uma violação silenciosa.
23. Como operador, quero que a avaliação de orçamento dos cenários considere os overrides usados na execução, para interpretar corretamente seu custo de leitura.
24. Como consumidor, quero proveniência com a política, a allowlist e os parâmetros efetivos, para reproduzir e auditar a publicação.
25. Como consumidor, quero evidência reproduzível da calibração e os insumos da derivação do teto, para conferir a estimativa de leitura.
26. Como mantenedor, quero resolver o registro de capacidades sem consumidores, para não manter uma extensão que só existe nos próprios testes.
27. Como mantenedor, quero remover testes sem proteção distinta, cenários fictícios e infraestrutura de teste sem uso, preservando verificações úteis de resultados e invariantes, para reduzir o custo de manutenção da suíte.
28. Como mantenedor, quero comparações de pacotes e medições de desempenho reproduzíveis, para distinguir simplificação demonstrada de hipótese.

## Implementation Decisions

Os números abaixo preservam os itens da revisão de origem.

1. **Documentação e regras ativas:** concentrar o estado de implementação em sua fonte canônica. READMEs, arquitetura e instruções devem apontar para esse estado e abandonar afirmações de que a substituição inteira ainda não foi implementada. Preservar AD-028 e suas lacunas explicitamente adiadas.
2. **Pipeline:** manter a composição explícita de etapas reais já observada na árvore de trabalho. Não reintroduzir stubs de produção, validação de oito nomes históricos, descoberta dinâmica ou agendador genérico. Projeção e composição continuam nos caminhos de publicação correspondentes.
3. **Leitura compartilhada:** manter a decodificação em CanonicalSymbolSignature, no Domain, e reutilizar as operações equivalentes de payload em Analysis. Completar a leitura estruturada necessária aos consumidores sem dependência de Roslyn fora de Analysis e sem nova assembly de utilitários.
4. **Índices:** construir índices de observações e símbolos uma vez após a extração; atualizar somente dados derivados que mudem entre passes. Snapshots completos ficam nos pontos que precisam deles. As consultas de labels usam índices por pacote; LayoutPlan continua determinando as citações. Não introduzir cache global ou paralelização.
5. **Associações:** demonstrar o receptor de chamadas HTTP no caso de variáveis locais inicializadas diretamente por CreateClient e sem reatribuição. Reconhecer métodos e tipos suportados por identidade semântica ou mapping autorizado. Preservar observações e limitações quando a prova não for suficiente; não ampliar para resolução geral de fluxo ou aliases interprocedurais.
6. **Testes:** revisar a suíte como uma fonte própria de complexidade, começando pelos testes de pipeline, identidade e capacidades sem consumidores. Remover testes de cenários não exercitados, duplicações sem proteção distinta, testes exclusivos de estruturas eliminadas e infraestrutura que fique sem uso. Consolidar verificações que congelam detalhes internos quando um teste de comportamento existente já protege o requisito. Antes de acrescentar um teste, verificar se o cenário cabe em um existente. Preservar contratos, isolamento e determinismo, sem exigir substituição um por um nem manter testes apenas por contagem, cobertura de linhas ou rastreabilidade de tarefa encerrada. Registrar no resumo da mudança o comportamento mantido e as remoções; não criar um catálogo permanente por teste.
7. **Cobertura:** compartilhar definições equivalentes de reconhecimento entre classificação e cobertura, preservando a diferença entre elegibilidade e promoção. Denominadores continuam recuperáveis dos dados publicados e incluem candidatos sem promoção comprovada.
8. **Parâmetros efetivos:** preservar a integração factual já existente em PublicationPipeline. Alinhar projeções, BatchComposer, entradas alternativas de publicação e avaliação de orçamento com os parâmetros efetivos. A política de documentos tem uma fonte de versão; o digest respeita a normalização e a semântica da allowlist. Alcançar um destino e respeitar orçamento continuam resultados distintos, sem novo gate de publicação implícito. Preservar o planejamento comum de escrita e citações e a exceção explícita de registro indivisível.
9. **Calibração:** conciliar código e design em uma fórmula com unidades e arredondamento explícitos. Preservar o requisito de medição reproduzível, identificando estimador ou tokenizer, versão, corpus e bytes/tokens observados. Publicar os insumos determinísticos; medições variáveis ficam no registro operacional apropriado. Uma constante usada para estimar tokens não constitui medição independente de sua própria razão.
10. **Capacidades:** registrar e aplicar a alternativa escolhida para ClassifierCapabilityRegistry. A integração deve derivar capacidades da composição real antes do inventário. A remoção deve retirar o mecanismo sem consumidores e emendar explicitamente GCPC-029 e sua rastreabilidade. Em ambos os casos, preservar categorias, allowlist e documentos aceitos/excluídos. A conversa não decidiu entre essas alternativas; não tratar uma delas como já aprovada.
11. **Parâmetros genéricos:** substituir a divisão indiscriminada por vírgulas em BoundaryPass pelo resultado de um leitor compartilhado que respeite a estrutura interna dos tipos. Preservar modificadores e tipos completos. Não presumir perda irreversível na assinatura nem exigir migração de identidades a partir desse defeito de leitura. Alterar o formato exige demonstração independente de insuficiência e decisão explícita de versionamento.
12. **Instruções de agentes:** manter uma fonte única, uma cópia gerada ou uma verificação visível de divergência, conforme o mecanismo suportado pelos consumidores. A diferença de posição de uma regra não prova conflito semântico; o problema é a manutenção manual duplicada.
13. **Comentários e rótulos:** corrigir os comentários históricos ou desatualizados nos arquivos tocados pelos demais itens, incluindo o que afirma que toda proveniência usa allowlist vazia. Corrigir o agrupamento de Verify.Xunit como Roslyn. Preservar explicações de regras aplicáveis e rastreabilidade útil; não abrir uma varredura global separada.

Preservar as cinco assemblies, as representações que cumprem responsabilidades distintas, a redação de segredos e os limites entre extração, classificação, storage e projeções. Não introduzir novas APIs públicas ou mudanças de schema por conveniência; alterações necessárias ao transporte de parâmetros e à calibração devem seguir os contratos e eixos de versão existentes.

## Testing Decisions

**Ponto principal de verificação:** executar AnalysisEngine com os adapters reais de publicação sobre fixtures controladas e verificar o resultado da análise e o pacote recuperado pelo leitor factual. Essa fronteira já existe e corresponde à abordagem aprovada na conversa. Usar o CLI quando o comportamento depende de parsing, composição de adapters ou overrides.

Testes menores complementam essa fronteira para leitura de assinaturas, cálculo do teto e política de documentos quando fornecem um diagnóstico melhor. Não criar novas interfaces apenas para espelhar detalhes internos. Bons testes comparam resultados esperados definidos pelo cenário, sem chamar o helper de produção para calcular a expectativa.

A redução da suíte é uma entrega do item 6, não apenas uma permissão durante outras mudanças. Avaliar cada grupo pelo cenário realmente exercitado, pelo resultado que verifica e pela proteção distinta que oferece. Um teste pode ser removido sem substituição quando o comportamento já está protegido ou a estrutura deixou de existir; corrigir o cenário quando ele protege um contrato necessário ainda não exercitado. Testes de ordem de dependências e relatórios observáveis podem ser úteis, mas não justificam congelar toda a forma interna do pipeline. Não substituir testes curtos úteis por uma infraestrutura genérica de testes.

Concluir o item 6 exige resolver os exemplos identificados e os grupos revisados, apontar as verificações existentes que continuam protegendo contratos importantes e executar os testes afetados após as remoções. Comparar tamanho e duração da suíte nas mesmas condições quando medidos, sem meta arbitrária de redução ou obrigação de que cada teste removido origine outro. A ausência de uma auditoria completa impede afirmar que a maioria dos testes é inútil.

| Cenário | Resultado a verificar |
| --- | --- |
| Repetição da mesma análise; inversão da ordem das soluções; mudanças de diretório absoluto | Saída determinística e isolamento entre soluções |
| Falha de análise, projeção ou composição | Último pacote válido preservado; trabalho que falhou identificável |
| Clientes A e B criados em ordens diferentes; chamada feita em A | Associação somente a A |
| Método próprio com nome HTTP; tipo próprio contendo IEventBus; receptor reatribuído | Nenhum fato inventado por nome, posição ou origem não demonstrada |
| Framework suportado e receptor demonstrável | Operação válida continua publicada |
| Primeiro parâmetro genérico com vários argumentos, genéricos aninhados, tuplas e arrays multidimensionais quando emitidos; ref/out/in | Tipos completos, modificadores preservados e identidades existentes mantidas |
| Helper privado, tipo não reconhecido, candidato sem componente e flush com/sem operação resolvida | Populações e denominadores conhecidos, independentes da função compartilhada |
| Orçamento padrão e override, incluindo cenários entre dois limites | Planejamento, proveniência, composição e avaliação usam os parâmetros correspondentes |
| Entrada acima do teto e registro indivisível acima do teto | Fragmentação ou exceção explícita, sem truncagem, com todos os fatos recuperáveis e citações resolvidas |
| Allowlist não vazia e política vigente | Proveniência corresponde aos insumos efetivos |
| Calibração e entradas inválidas ou sem representação válida em bytes | Derivação reproduzível e rejeição explícita de valores inválidos |
| Integração ou remoção do registro de capacidades | Critérios da alternativa aplicada satisfeitos, mantendo a política atual correta |

Usar como precedentes WholePackageDeterminismTests, AbortPreservesPackageTests, as suítes de publicação e leitura com shards, as verificações de citações e os testes do corpus de certificação. Verificar também a ausência de segredos nos fatos e labels publicados. Domain protege contratos de leitura e identidade; Analysis protege classificação e cobertura; Storage e Projection protegem publicação, recuperação e navegação; CLI protege opções e composição.

Antes das mudanças, registrar uma referência executada sobre SyntheticSolution e CertificationCorpus, com o commit e as alterações locais. Comparar fatos independentemente da distribuição em shards; explicar separadamente mudanças de associação, evidência, cobertura, proveniência, layout e build identity. Medir tempo e alocações nas mesmas condições para avaliar os índices, sem meta percentual arbitrária.

Executar os testes afetados a cada entrega e a suíte pertinente ao integrar. Corpora locais opcionais complementam a avaliação quando presentes; sua ausência não bloqueia a entrega. Não executar Stryker nem o sensor de discriminação por mutação; a regra de skip do projeto permanece vigente.

## Out of Scope

- Reescrever o motor, reduzir o número de assemblies ou eliminar representações apenas para diminuir tipos ou linhas.
- Expandir a taxonomia, os frameworks suportados ou as áreas certificadas.
- Introduzir query engines, kb, QMD, embeddings, compilação de wiki, interpretação de regras de negócio ou análise incremental.
- Criar framework de classificadores, plugins, cache global, agendador genérico ou paralelização.
- Implementar análise geral de fluxo de dados ou resolução interprocedural de aliases.
- Migrar identidades sem demonstração da necessidade ou substituir silenciosamente calibração medida por estimativa fixa.
- Recuperar contratos supersedidos do histórico, reabrir implicitamente a feature encerrada ou iniciar implementação pela publicação desta spec.
- Exigir contagem crescente de testes, quantidade mínima de arquivos removidos ou redução percentual de linhas.

## Further Notes

A base observada é o HEAD 2e93f5fdb3bce86b8f21166b19fb89e3af5437a6 acrescido das alterações locais ainda não commitadas. Este documento consolida a revisão anterior no formato da skill to-spec e permanece local, ainda não rastreado pelo Git na conferência. Sua edição e classificação não iniciam a implementação.

Estado dos 13 itens: implementação observada do item 2; atendimento parcial dos itens 1, 3, 6 e 8; itens 4, 5, 7 e 9 a 13 abertos. Observação estática não equivale a nova validação funcional.

A feature generator-cli-projections-certification foi encerrada por AD-028 com duas lacunas Major adiadas e relatório do Verifier mantido em FAIL. Os resultados anteriores de 2070 testes, build sem avisos e comparação de 947 arquivos são registros anteriores, não execuções realizadas para esta síntese.

A igualdade aritmética que resulta em 32 KiB não comprova como a constante foi escolhida. A divisão incorreta por vírgulas não demonstra colisão ou perda irreversível no formato da assinatura. Não atribuir intenção de fabricação nem exigir migração com base nessas inferências.

O porte estimado é grande, com complexidade média-alta e aproximadamente 6–10 entregas coesas; isso não é uma decomposição de tarefas nem estimativa de prazo. Associações, parâmetros efetivos e calibração concentram a incerteza. A escolha do item 10 e qualquer eventual alteração do requisito de medição precisam ser registradas antes da implementação afetada.

A classificação local ready-for-agent identifica a spec para trabalho posterior; não significa que a implementação esteja concluída ou que decisões ainda abertas tenham sido tomadas.

### Evidências e rastreabilidade dos 13 itens

**Item 1 — Concentrar regras e eliminar instruções obsoletas (parcial).** Os dois READMEs e a arquitetura principal ainda afirmam que a substituição não foi implementada; as instruções também negam a existência de uma spec. Estado e roadmap já registram workstreams encerrados com lacunas adiadas. A divergência pode induzir a recriação de trabalho entregue.

Referências: [AGENTS.md](../../AGENTS.md), [README](../../README.md), [README em português](../../README.pt-BR.md), [arquitetura](../architecture/architecture-knowledge-engine.md), [estado](../../.specs/STATE.md), [roadmap](../../architecture-knowledge-engine-roadmap.md).

**Item 2 — Remover o esqueleto provisório do pipeline (implementação observada).** PipelineStages.CreateDefault constrói seis etapas reais e PipelineOrchestrator exige somente sequência não vazia. O StubStages de produção foi removido localmente; os substitutos permanecem nos testes. Os critérios funcionais não foram reexecutados.

Referências: [PipelineStages](../../src/Csharp2Md.Analysis/Pipeline/PipelineStages.cs), [PipelineOrchestrator](../../src/Csharp2Md.Analysis/Pipeline/PipelineOrchestrator.cs), [PublicationPipeline](../../src/Csharp2Md.Storage/Mapping/PublicationPipeline.cs).

**Item 3 — Dar um único dono à leitura de assinaturas e payloads (parcial).** CanonicalSymbolSignature.Component concentra a decodificação consumida por Analysis e LabelProjector. A leitura estruturada ainda está incompleta por causa do consumidor descrito no item 11; centralizar a decodificação não resolveu esse caso.

Referências: [CanonicalSymbolSignature](../../src/Csharp2Md.Domain/Identity/CanonicalSymbolSignature.cs), [SignatureReader](../../src/Csharp2Md.Analysis/Classification/SignatureReader.cs), [PayloadReader](../../src/Csharp2Md.Analysis/Classification/PayloadReader.cs), [LabelProjector](../../src/Csharp2Md.Projection/Labels/LabelProjector.cs), [restrição PK-43](../../.specs/features/persistence-knowledge/spec.md).

**Item 4 — Separar dados estáveis dos resultados que crescem durante a classificação (aberto).** ClassificationAndPromotionStage chama Refresh após cada passe. ClassifierContext reconstrói snapshot e índices de observações, símbolos e componentes; consultas materializam coleções. LabelProjector usa FirstOrDefault por identidade. O trabalho repetido é observável, mas seu impacto de desempenho ainda não foi medido.

Referências: [ClassifierContext](../../src/Csharp2Md.Analysis/Classification/ClassifierContext.cs), [SnapshotAccumulator](../../src/Csharp2Md.Analysis/Pipeline/SnapshotAccumulator.cs), [ClassificationAndPromotionStage](../../src/Csharp2Md.Analysis/Classification/ClassificationAndPromotionStage.cs), [LabelProjector](../../src/Csharp2Md.Projection/Labels/LabelProjector.cs).

**Item 5 — Corrigir as associações frágeis encontradas, sem ampliar o motor (aberto).** BoundaryPass vincula chamadas ao intervalo textual entre CreateClient e nextCreate. TryHttpMethod consulta o nome do método, enquanto mensageria procura a substring IEventBus no tipo. Posição e coincidência de nomes podem substituir indevidamente a prova do receptor ou framework.

Referências: [BoundaryPass](../../src/Csharp2Md.Analysis/Classification/Passes/BoundaryPass.cs).

**Item 6 — Reduzir a complexidade da própria suíte (parcial).** PipelineStageContractTests está removido localmente, mas a revisão não termina na remoção dos stubs. A contagem estática encontrou 367 arquivos C# e 55.295 linhas em tests, contra 203 arquivos e 23.939 linhas em src, excluindo bin e obj. A razão de 2,31 inclui helpers, fixtures, comentários e linhas vazias; não mede quantidade de testes úteis nem tempo de execução.

Uma amostra de 11 métodos de teste em PipelineStagesTests, DefaultPipelineZerosTests, ProjectIdTests e SolutionIdTests foi confrontada com os contratos de produção. Foram identificados dois achados, com escopos diferentes:

| Achado | Gravidade | Métodos afetados e decisão |
| --- | --- | --- |
| Cenário anunciado não exercitado | Alta | SolutionIdTests.Create_SameLogicalInputsUnderTwoSimulatedAbsoluteRoots_AreByteIdentical chama dois helpers com argumentos idênticos, sem fornecer ou mudar raiz absoluta. Verifica repetição da criação da identidade, mas não independência de diretório. Remover a alegação e o teste redundante quando a proteção já existente em WholePackageDeterminismTests.Analyze_CertificationCorpusFromTwoAbsolutePaths_PublishesByteIdenticalPackage for confirmada: esse teste cria dois clones reais, compara os pacotes e verifica ausência das raízes nos artefatos. Não acrescentar outra simulação nominal. |
| Acoplamento à forma do pipeline | Média | PipelineStagesTests.CreateDefault_IsTheRealStageSequence e CreateDefault_RunsEveryClassifierPassInDependencyOrder repetem tipos, nomes e posições da composição; DefaultPipelineZerosTests.AnalyzeAsync_DefaultPipeline_ProducesFactsObservationsAndRelationsInTheRightStages também fixa seis posições. Consolidar a parte estrutural sem proteção distinta. Preservar as verificações de dependências necessárias, publicação e contadores que correspondam a contratos reais; os três testes não são declarados integralmente inúteis. |

Os outros sete métodos da amostra verificam contratos distintos de identidade por caminho, chave lógica e workspace, além da rejeição de entradas inválidas com parâmetro identificado. As comparações entre identidades e os casos parametrizados podem falhar por defeitos reais; não foram classificados como tautologias ou duplicações. Não houve execução de testes, análise exaustiva de lacunas nem auditoria de toda a suíte nesta conferência.

Referências: [PipelineStagesTests](../../tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineStagesTests.cs), [DefaultPipelineZerosTests](../../tests/Csharp2Md.Analysis.Tests/Pipeline/DefaultPipelineZerosTests.cs), [ProjectIdTests](../../tests/Csharp2Md.Domain.Tests/Identity/ProjectIdTests.cs), [SolutionIdTests](../../tests/Csharp2Md.Domain.Tests/Identity/SolutionIdTests.cs), [WholePackageDeterminismTests](../../tests/Csharp2Md.Analysis.Tests/Determinism/WholePackageDeterminismTests.cs), [regra de contagem](../../.specs/features/generator-cli-projections-certification/tasks.md). `tests/Csharp2Md.Analysis.Tests/Pipeline/PipelineStageContractTests.cs` está removido na árvore de trabalho; não é uma referência navegável atual.

**Item 7 — Dar um único dono às regras de reconhecimento usadas pela cobertura (aberto).** ValidationAndCoverageStage e EntryPointPass mantêm ControllerBaseTypeName, reconhecimento por herança e IsDeclaredOn próprios. A regra IsFlush também se repete em PersistenceModelBuilder. Uma correção pode divergir entre consumidores e distorcer os denominadores.

Referências: [ValidationAndCoverageStage](../../src/Csharp2Md.Analysis/Pipeline/ValidationAndCoverageStage.cs), [EntryPointPass](../../src/Csharp2Md.Analysis/Classification/Passes/EntryPointPass.cs), [PersistenceModelBuilder](../../src/Csharp2Md.Analysis/Classification/Persistence/PersistenceModelBuilder.cs), [RunCertifier](../../src/Csharp2Md.Analysis/Pipeline/RunCertifier.cs).

**Item 8 — Publicar e aplicar os parâmetros efetivamente usados pela execução (parcial).** PublicationPipeline já fornece teto derivado ao planejador factual e digest efetivo à proveniência. Sobrecargas de PackagePublisher e PublishedPackageView usam int.MaxValue. Projetores e BatchComposer mantêm defaults de 1 MiB; inclusive o CLI cria BatchComposer sem o teto derivado. ScenarioResult.WithinBudget usa constantes padrão e a versão da política está duplicada. ScenarioReport.Passed verifica alcance, não orçamento.

Referências: [ProvenanceDto](../../src/Csharp2Md.Storage/Wire/ProvenanceDto.cs), [PublicationPipeline](../../src/Csharp2Md.Storage/Mapping/PublicationPipeline.cs), [LayoutPlanner](../../src/Csharp2Md.Storage/Mapping/LayoutPlanner.cs), [PackagePublisher](../../src/Csharp2Md.Storage/Mapping/PackagePublisher.cs), [PublishedPackageView](../../src/Csharp2Md.Storage/Mapping/PublishedPackageView.cs), [ShardWriter](../../src/Csharp2Md.Projection/ShardWriter.cs), [CommandFactory](../../src/Csharp2Md.Cli/CommandFactory.cs), [BatchComposer](../../src/Csharp2Md.Projection/Composition/BatchComposer.cs), [RetrievalScenarioRunner](../../src/Csharp2Md.Storage/Retrieval/RetrievalScenarioRunner.cs), [FactualPackageReader](../../src/Csharp2Md.Storage/FactualPackageReader.cs), [pendência de integração](../../.specs/features/generator-cli-projections-certification/context.md).

**Item 9 — Tornar demonstrável a calibração do limite de leitura (aberto).** CeilingCalculator descreve 8.192 bytes/token como medido sem evidência de calibração localizada; o design descreve aproximadamente 4 bytes/token e outra derivação. A fórmula resulta em 32 KiB, mas essa igualdade não demonstra a origem da constante.

Referências: [CeilingCalculator](../../src/Csharp2Md.Storage/Mapping/CeilingCalculator.cs), [contrato da feature](../../.specs/features/generator-cli-projections-certification/spec.md), [design da feature](../../.specs/features/generator-cli-projections-certification/design.md).

**Item 10 — Resolver o registro de capacidades sem consumidores de produção (aberto; escolha pendente).** DocumentInventory cria ClassifierCapabilityRegistry com lista vazia; nenhum passe de produção implementa IDocumentConsumingClassifierPass. A ramificação condicional só é exercitada por implementadores de teste e não fornece extensões à composição real.

Referências: [DocumentInventory](../../src/Csharp2Md.Analysis/Inventory/DocumentInventory.cs), [ClassifierCapabilityRegistry](../../src/Csharp2Md.Analysis/Classification/ClassifierCapabilityRegistry.cs), [SupportedDocumentPolicy](../../src/Csharp2Md.Analysis/Inventory/SupportedDocumentPolicy.cs), [GCPC-029](../../.specs/features/generator-cli-projections-certification/spec.md).

**Item 11 — Corrigir a leitura de parâmetros com vírgulas internas (aberto).** BoundaryPass.TryFirstParameterType usa Split(',')[0] e devolve Dictionary<K para um primeiro parâmetro Dictionary<K,V>. O formato decodificado preserva delimitadores de genéricos: o exemplo demonstra defeito no consumidor, não colisão de identidade. InvokesPass compara o componente inteiro e não faz a mesma divisão.

Referências: [BoundaryPass](../../src/Csharp2Md.Analysis/Classification/Passes/BoundaryPass.cs), [SignatureReader](../../src/Csharp2Md.Analysis/Classification/SignatureReader.cs), [CanonicalSymbolSignature](../../src/Csharp2Md.Domain/Identity/CanonicalSymbolSignature.cs), [InvokesPass](../../src/Csharp2Md.Analysis/Classification/Passes/InvokesPass.cs), [SymbolFactEmitter](../../src/Csharp2Md.Analysis/Semantics/SymbolFactEmitter.cs).

**Item 12 — Dar um único dono às instruções de agente (aberto).** AGENTS.md e CLAUDE.md repetem roteamento, gates e regras do projeto, incluindo afirmações obsoletas. A posição diferente da regra de mutação é uma diferença de organização, não conflito semântico comprovado; a manutenção manual duplicada exige corrigir duas fontes.

Referências: [AGENTS.md](../../AGENTS.md), [CLAUDE.md](../../CLAUDE.md).

**Item 13 — Fazer os comentários e rótulos descreverem a intenção corrente (aberto).** CommandFactory e PublicationPipeline narram descoberta e encerramento de tarefas. ProvenanceDto.EmptyAllowlistDigest ainda afirma que toda publicação usa allowlist vazia, contrariando o caminho efetivo. Verify.Xunit está agrupado como Roslyn. Os achados se apoiam no conteúdo, não na quantidade de comentários.

Referências: [CommandFactory](../../src/Csharp2Md.Cli/CommandFactory.cs), [PublicationPipeline](../../src/Csharp2Md.Storage/Mapping/PublicationPipeline.cs), [ProvenanceDto](../../src/Csharp2Md.Storage/Wire/ProvenanceDto.cs), [Directory.Packages.props](../../Directory.Packages.props).
