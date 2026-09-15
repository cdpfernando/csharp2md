# Pacote de conhecimento útil e confiável

**Status:** especificação local; implementação não iniciada.

**Triage:** ready-for-agent — registro local, sem publicação no GitHub.

**Origem:** diagnóstico da versão atual em 15 de setembro de 2026 usando os corpora locais eShop, eShopOnContainers e Pitstop.

## Problem Statement

O pacote atualmente produzido não funciona como uma base de conhecimento prática para compreender uma solução. Ele materializa grande parte do inventário intermediário da análise — símbolos, usos de tipos, observações, relações e cópias de fonte — em milhares de arquivos pequenos, mas não oferece um caminho curto e confiável para responder às perguntas arquiteturais que motivam o produto.

O objetivo original também não está representado como contrato de produto: partir de um arquivo, projeto, componente ou serviço, navegar para suas dependências e dependentes e medir esse acoplamento de forma explicável. O motor produz relações atômicas, mas o pacote não oferece uma projeção de dependências que as agregue por escopo, diferencie dependência de compilação de integração em runtime, exponha impacto reverso ou calcule medidas verificáveis como fan-in, fan-out, ocorrências e ciclos. Assim, mesmo fatos corretos permanecem distantes da pergunta do usuário.

Nos corpora locais, o problema aparece em quatro dimensões:

1. **Volume sem utilidade proporcional.** O eShopOnContainers produziu 29.375 arquivos e aproximadamente 139 MB; o Pitstop produziu 10.595 arquivos e aproximadamente 50 MB. As famílias de relações e observações dominam o volume, com milhares de fragmentos de uso de tipo e relações confirmadas. Uma consulta básica para localizar uma identidade exigiu 315 leituras e cerca de 186 mil tokens no eShopOnContainers, e 163 leituras e cerca de 90 mil tokens no Pitstop. O teto declarado pelo próprio produto é de 25 leituras e 100 mil tokens por cenário.
2. **Cobertura anunciada sem exercício útil.** A medição atual exercitou somente a localização de identidade. Os cenários arquiteturais principais — seguir um fluxo, identificar efeitos e avaliar impacto reverso — permaneceram sem exercício, portanto a certificação não demonstra que o pacote responde ao que importa.
3. **Publicação inconsistente.** eShopOnContainers e Pitstop foram materializados, mas falharam ao serem validados em seguida. A análise ignora determinados fragmentos diferidos durante a validação pré-publicação, enquanto o comando de validação posterior inspeciona o conteúdo já materializado. Assim, “committed” não significa “validável”.
4. **Análise incorreta de variantes.** O eShop não publicou pacote porque frameworks-alvo coletados de projetos cliente foram aplicados globalmente à solução, inclusive ao projeto web. A mesma identidade lógica foi acumulada com localizadores incompatíveis e reportada como colisão estrutural. Uma solução válida foi rejeitada por contaminação entre projetos e variantes criada pelo próprio analisador.

Também há duas falhas de segurança e precisão na projeção de fonte:

- um comentário C# iniciado por `//` foi interpretado como caminho UNC absoluto no eShopOnContainers;
- um caminho absoluto real presente em configuração de desenvolvimento do Pitstop atravessou a análise e só foi rejeitado depois da publicação.

Na perspectiva do usuário, o resultado é inútil: é caro de abrir, difícil de navegar, não prova as perguntas centrais e não oferece uma garantia coerente de integridade. A correção precisa mudar o contrato do pacote publicado, e não apenas aumentar limites de shard ou esconder os erros observados.

## Solution

Redefinir o pacote padrão como uma projeção compacta do conhecimento arquitetural comprovado, orientada às perguntas que um humano ou LLM precisa responder. A extração pode continuar rica internamente, mas somente informações que participem de uma resposta, sustentem uma evidência ou expliquem uma lacuna relevante devem ser publicadas.

O pacote deve começar por componentes, unidades de implantação, pontos de entrada e operações de fronteira. A partir dessas raízes, deve manter o fechamento de relações causais confirmadas até contratos, sistemas externos, persistência e demais efeitos observáveis. Fatos estruturais, trechos de evidência e observações só permanecem quando sustentarem esse grafo retido. Candidatos e fronteiras abertas permanecem separados dos fatos confirmados e são priorizados pelo impacto nas mesmas perguntas.

Sobre esse grafo retido, o gerador deve publicar uma Retrieval Projection de dependências. Ela agrega relações confirmadas nos escopos de documento, projeto, componente e Deployment Unit, preservando a ligação com as relações e evidências de origem. “Serviço” é uma apresentação navegável de uma Deployment Unit e dos componentes nela incluídos quando essa identidade estiver comprovada; o nome de um projeto ou diretório, sozinho, não transforma um projeto em serviço.

Cada dependência agregada deve declarar origem, destino, escopo, categorias factuais participantes, quantidade de ocorrências, variantes observadas e referências de evidência. A projeção deve distinguir relações diretas de alcance transitivo e dependências de compilação, chamadas internas, HTTP, gRPC, mensageria, contratos e persistência. Candidatos, unknowns e Open Frontiers não aumentam contagens confirmadas; aparecem ao lado das medidas como cobertura incompleta.

As medidas publicadas devem ser transparentes e reproduzíveis a partir das arestas retidas: dependências e dependentes distintos, fan-out, fan-in, quantidade de ocorrências, cruzamentos entre componentes, participação em ciclos e alcance de impacto reverso. O pacote não deve produzir um score composto ou rótulo de qualidade cujo cálculo ou interpretação não possa ser refeito pelo consumidor.

O formato deve fornecer índices compactos e manifest-led para quatro jornadas:

1. localizar um arquivo, projeto, componente, Deployment Unit, ponto de entrada ou operação de fronteira;
2. seguir um fluxo até contratos, efeitos externos e persistência;
3. consultar dependências, dependentes e impacto reverso a partir de arquivo, projeto, componente, Deployment Unit, contrato ou dado;
4. inspecionar a evidência e a disposição de uma relação, candidato ou lacuna.

Markdown deve ser a porta de entrada humana dessas jornadas. O resumo da solução, as páginas de componentes/serviços e as páginas dos documentos retidos devem ligar diretamente umas às outras, com dependências de saída, dependentes de entrada, medidas e lacunas visíveis. Um link de navegação normal não deve obrigar o consumidor a interpretar um ID opaco, escolher manualmente entre centenas de shards ou abrir primeiro um registro JSON intermediário. Os artefatos legíveis por máquina continuam sendo a autoridade e permitem recomputar cada projeção.

O pacote padrão não deve publicar testes, cada uso de tipo, observações sem promoção, cópias integrais de configuração ou um arquivo por registro. Fontes devem ser incluídas somente quando forem citadas pelo conhecimento retido. Configurações devem ser representadas estruturalmente, sem transportar valores de ambiente ou segredos. A inclusão de testes deve existir somente como opção explícita e identificada no manifesto.

A análise de variantes deve ser planejada por projeto com base na avaliação real do projeto, sem formar um produto cartesiano entre todos os projetos e todos os frameworks-alvo encontrados na solução. Identidades lógicas compartilhadas entre variantes podem permanecer unificadas, mas seus localizadores e evidências precisam ser qualificados pela variante que os produziu.

A publicação deve usar exatamente o mesmo caminho de materialização e validação que o comando de validação posterior. Nenhum fragmento diferido pode escapar da inspeção pré-commit. Um pacote só pode ser anunciado como comprometido se uma reidratação imediata passar por todas as validações aplicáveis.

O armazenamento deve deduplicar identidades e evidências, usar referências compactas e agrupar registros deterministicamente por família e tamanho. IDs persistidos não devem incorporar caminhos, assinaturas completas, IDs ancestrais concatenados ou conteúdo percent-encoded. A identidade estável deve ser um digest curto com prefixo de tipo; referências dentro do pacote devem usar handles locais ainda menores. Nomes, assinaturas e qualificadores legíveis permanecem em campos descritivos separados e são armazenados uma única vez. O crescimento do número de arquivos deve acompanhar famílias e faixas de tamanho, não a quantidade de registros comuns.

A implementação pode substituir integralmente a arquitetura, os modelos, os schemas, os comandos e os testes atuais. O workstream pode começar de uma base vazia e remover a implementação legada antes de construir a substituição. Não é necessário criar adaptadores, migração de pacotes, feature flags, versionamento de contrato, rotas de coexistência ou uma sequência incremental que preserve o funcionamento anterior. O estado final precisa satisfazer somente este contrato e seu seam de aceitação; código, schemas e testes cuja única finalidade seja sustentar o produto anterior devem ser removidos em vez de mantidos temporariamente para desligamento posterior.

Os limites provisórios de aceitação para a entrega inicial são:

- eShopOnContainers: no máximo 1.500 arquivos e 64 MiB;
- Pitstop: no máximo 750 arquivos e 25 MiB;
- localização inicial: no máximo 8 leituras e 12 mil tokens;
- fluxo causal e impacto reverso: no máximo 32 leituras e 125 mil tokens;
- inspeção de prova ou disposição: no máximo 12 leituras e 25 mil tokens.

Esses limites concedem margem de implantação à entrega inicial, mas continuam sendo critérios de produto, não valores a serem afrouxados apenas para fazer o gate passar. Após a primeira baseline estável, eles devem ser reavaliados com a intenção de redução. Qualquer aumento futuro precisa ser baseado em medições das jornadas e registrado como mudança deste contrato corrente, sem introduzir formatos coexistentes.

## User Stories

1. Como consumidor do pacote, quero começar por um manifesto curto que indique componentes, pontos de entrada e jornadas disponíveis, para não precisar conhecer a estrutura interna do gerador.
2. Como consumidor do pacote, quero localizar um componente em até cinco leituras, para obter contexto sem carregar centenas de fragmentos.
3. Como consumidor do pacote, quero localizar os pontos de entrada de uma aplicação e suas operações de fronteira, para saber onde os comportamentos começam.
4. Como consumidor do pacote, quero seguir uma operação de fronteira até seus efeitos confirmados, para entender o fluxo arquitetural relevante.
5. Como consumidor do pacote, quero identificar contratos consumidos ou expostos por um fluxo, para avaliar integrações sem pesquisar toda a solução.
6. Como consumidor do pacote, quero identificar dados persistidos, lidos ou modificados por um fluxo, para compreender seus efeitos sobre o estado.
7. Como consumidor do pacote, quero partir de um contrato, dado ou componente e descobrir quem depende dele, para realizar análise de impacto reverso.
8. Como consumidor do pacote, quero abrir a evidência de uma afirmação diretamente, para auditar por que ela foi considerada verdadeira.
9. Como consumidor do pacote, quero distinguir relações confirmadas de candidatos, para não transformar hipótese em fato.
10. Como consumidor do pacote, quero receber fronteiras abertas priorizadas pelo impacto arquitetural, para investigar primeiro as lacunas que alteram respostas importantes.
11. Como consumidor do pacote, não quero navegar por um inventário completo de símbolos do compilador, para que o produto expresse conhecimento e não detalhes do pipeline.
12. Como consumidor do pacote, não quero milhares de usos de tipo sem ligação com uma resposta arquitetural, para preservar o orçamento de contexto.
13. Como consumidor do pacote, não quero que código de teste polua por padrão o grafo de produção, para que as respostas descrevam o sistema executável.
14. Como mantenedor, quero poder habilitar a análise de testes explicitamente, para investigar cenários em que os testes sejam evidência necessária.
15. Como consumidor do pacote, quero somente fontes citadas pelo conhecimento retido, para evitar cópias extensas que nunca serão abertas.
16. Como responsável por segurança, quero configurações descritas por estrutura e finalidade sem valores sensíveis ou específicos de máquina, para evitar vazamento no artefato.
17. Como mantenedor de solução multi-target, quero que cada projeto seja analisado somente nas variantes que realmente declara, para evitar contaminação entre projetos.
18. Como consumidor, quero que uma identidade lógica comum a várias variantes continue reconhecível como a mesma entidade, para não receber duplicatas artificiais.
19. Como auditor, quero que localizadores e evidências específicos de variante permaneçam identificados, para saber em qual compilação cada afirmação foi observada.
20. Como mantenedor, quero que divergências reais dentro da mesma variante continuem produzindo colisão, para não mascarar corrupção estrutural.
21. Como autor de código C#, quero que comentários iniciados por barras não sejam classificados como caminhos UNC, para que fonte válida não seja rejeitada.
22. Como responsável por segurança, quero que caminhos absolutos reais sejam removidos, redigidos ou rejeitados antes do commit, para que o pacote nunca os publique inadvertidamente.
23. Como operador, quero que o sucesso da análise implique sucesso imediato da validação, para confiar no estado informado pela CLI.
24. Como operador, quero que fragmentos diferidos sejam validados após materialização e antes do commit, para que não exista uma rota alternativa menos segura.
25. Como consumidor, quero que identidades repetidas sejam armazenadas uma vez e referenciadas por handles compactos, para reduzir duplicação sem perder navegabilidade.
26. Como consumidor, quero que a mesma evidência seja armazenada uma vez mesmo quando sustenta várias afirmações, para reduzir volume e manter uma origem canônica.
27. Como mantenedor, quero shards determinísticos agrupados por tamanho, para obter saídas estáveis e evitar milhares de arquivos minúsculos.
28. Como operador, quero que nenhuma família comum gere um arquivo por registro, para que ferramentas de arquivos e agentes consigam percorrer o pacote.
29. Como responsável pelo produto, quero limites explícitos de arquivos, bytes, leituras e tokens, para que “útil” seja verificável e não subjetivo.
30. Como responsável pelo produto, quero que todas as jornadas aplicáveis sejam exercitadas na certificação, para que cobertura declarada represente uso real.
31. Como consumidor, quero que jornadas não aplicáveis sejam explicadas e jornadas não exercitadas sejam reportadas como falha, para evitar confiança indevida.
32. Como operador, quero que uma tentativa inválida preserve atomicamente o último pacote válido, para não trocar conhecimento utilizável por saída corrompida.
33. Como operador, quero diagnósticos que indiquem projeto, variante, família e causa da rejeição, para corrigir o problema sem vasculhar logs extensos.
34. Como usuário de múltiplas soluções, quero que identidades e variantes permaneçam isoladas por unidade de análise, para impedir colisões entre corpora.
35. Como consumidor, quero navegar pelo pacote apenas com arquivos e referências declaradas no manifesto, para não depender de banco vetorial, wiki ou motor de consulta.
36. Como arquiteto, quero que o gerador publique fatos e relações observáveis sem inferir regras de negócio, para manter a fronteira factual do produto.
37. Como mantenedor, quero que somente o contrato corrente exista depois da substituição, para não manter seletores de versão, leitores legados ou caminhos de compatibilidade sem utilidade para o produto final.
38. Como mantenedor, quero métricas reproduzíveis por família, jornada e corpus, para identificar regressões de volume ou recuperabilidade.
39. Como colaborador, quero um corpus pequeno sob controle de fonte que combine multi-target, comentários, configuração, produção e testes, para reproduzir as falhas sem depender de repositórios externos.
40. Como colaborador, quero poder executar verificações adicionais nos clones locais quando presentes, para validar escala real sem tornar esses clones requisito de CI.
41. Como consumidor, quero IDs curtos e opacos, para que referências sejam fáceis de transportar e não consumam contexto repetindo assinaturas inteiras.
42. Como consumidor humano, quero nomes e assinaturas legíveis separados dos IDs, para compreender uma entidade sem depender da codificação de sua identidade.
43. Como mantenedor, quero que o tamanho de um ID seja constante mesmo para tipos genéricos, símbolos aninhados e cadeias causais profundas, para impedir amplificação do pacote.
44. Como desenvolvedor, quero partir de um arquivo e ver suas dependências diretas, para entender o que ele usa sem pesquisar a solução inteira.
45. Como desenvolvedor, quero partir de um arquivo e ver quais arquivos dependem dele, para avaliar o impacto de uma alteração local.
46. Como arquiteto, quero consultar dependências e dependentes agregados por projeto, componente e Deployment Unit, para compreender o acoplamento em diferentes escalas.
47. Como arquiteto, quero distinguir referências de compilação, chamadas internas e integrações de runtime, para não tratar mecanismos diferentes como uma única aresta genérica.
48. Como consumidor, quero que várias ocorrências entre a mesma origem e destino sejam agregadas com uma contagem, para medir recorrência sem receber um registro completo por call site.
49. Como consumidor, quero distinguir dependência direta de alcance transitivo, para saber o que foi observado e o que foi calculado por travessia.
50. Como mantenedor, quero fan-in e fan-out calculados sobre identidades distintas e arestas retidas, para reproduzir os valores sem depender da implementação interna.
51. Como arquiteto, quero identificar ciclos entre projetos, componentes ou Deployment Units, para localizar acoplamento circular comprovado.
52. Como mantenedor, quero medir quantas dependências cruzam componentes, para distinguir coesão interna de acoplamento externo.
53. Como consumidor, quero que a quantidade de candidatos, unknowns e Open Frontiers relevante apareça junto das medidas confirmadas, para não interpretar um número parcial como completo.
54. Como consumidor, quero navegar entre páginas Markdown de arquivos e componentes por links legíveis, para não depender de IDs, ordinais ou nomes de shards.
55. Como consumidor, quero que cada dependência agregada leve às relações e evidências que a sustentam, para auditar a agregação.
56. Como responsável pelo produto, quero que “serviço” seja apresentado somente quando uma Deployment Unit e seus componentes forem demonstrados, para não promover convenção de nome a fato arquitetural.
57. Como consumidor, não quero um score opaco de dependência ou qualidade, para poder interpretar e recalcular todas as medidas publicadas.
58. Como integrador, quero uma representação legível por máquina das mesmas dependências e medidas mostradas em Markdown, para automatizar verificações sem divergência semântica.

## Implementation Decisions

1. **Substituição por corte limpo.** O pacote publicado deixa de representar o ledger completo da extração e passa a representar uma projeção arquitetural retida. Não há requisito de compatibilidade de interface, CLI, wire format, schema, layout em disco ou comportamento com o produto atual. Não serão fornecidos leitores legados, conversores, seletores de versão nem período de coexistência. O manifesto descreve somente o formato corrente necessário para ler e validar o próprio pacote; não carrega uma matriz de versões nem aciona caminhos alternativos de leitura.
2. **Taxonomia factual preservada.** Component, Deployment Unit, Entry Point, Boundary Operation, External System, Contract, Data Store, Data Object, Structural Fact, Confirmed Relation, Candidate, Unknown, Open Frontier, Evidence e Analysis Variant continuam sendo o vocabulário normativo. A correção altera o que é publicado e como é recuperado, não autoriza interpretação de regras de negócio.
3. **Plano de variantes por projeto.** O módulo de inventário deve produzir variantes avaliadas por projeto. Frameworks-alvo não podem ser unidos e reaplicados globalmente. Valores condicionais ou não resolvidos não se tornam variantes até que a avaliação do projeto os resolva.
4. **Identidade lógica e ocorrência de variante separadas.** A identidade de uma entidade continua baseada em sua assinatura e escopo lógico. Declarações, documentos, spans e evidências são ocorrências qualificadas por variante. Ocorrências compatíveis podem sustentar a mesma identidade; valores incompatíveis dentro da mesma variante continuam sendo colisão.
5. **Retenção orientada a raízes.** A projeção começa por componentes, unidades de implantação, pontos de entrada e operações de fronteira. Ela percorre relações confirmadas até integrações, contratos, persistência e outros efeitos, mantendo os fatos e evidências necessários para justificar cada passo.
6. **Lacunas relevantes permanecem visíveis.** Candidatos, unknowns e fronteiras abertas são publicados quando interrompem ou podem alterar uma jornada retida. Itens desconectados dessas jornadas permanecem nas métricas internas, mas não viram payload padrão.
7. **Observações como detalhe de extração.** Invocation, Object Creation, Type Usage, Base Type e Attribute Usage podem continuar sendo produzidos para binding e classificação. Só são persistidos quando constituem evidência necessária de um fato, relação, candidato, fronteira ou diagnóstico retido.
8. **Produção como escopo padrão.** Documentos de teste e entidades alcançáveis apenas por testes ficam fora do pacote padrão. Uma opção explícita pode incluí-los, e essa escolha deve constar no manifesto e na identidade da execução.
9. **Fonte por citação.** A projeção de fonte inclui somente documentos citados por evidências retidas. O conteúdo deve continuar byte-faithful quando seguro; dados que violam a política de publicação não podem ser copiados apenas para preservar fidelidade.
10. **Configuração estrutural e segura.** Arquivos de configuração são representados por chaves, seções, vínculos e evidências necessárias. Valores de ambiente, caminhos absolutos, credenciais e segredos não são payload de conhecimento. Quando sua existência for relevante, registra-se a categoria e a localização segura, não o valor.
11. **Detecção de caminhos por tipo de payload.** Campos estruturados de caminho são validados como caminhos. Texto-fonte não deve ser tokenizado por uma regra que confunda comentários com UNC; sua inspeção precisa respeitar a forma lexical do conteúdo e a política de redação. Caminhos absolutos reais continuam proibidos no artefato materializado.
12. **Validação única antes e depois do commit.** Fragmentos imediatos e diferidos passam pela mesma materialização, normalização e conjunto de validadores. A publicação usa uma área de staging, reidrata o resultado final e só então realiza a troca atômica. O comando de validação reutiliza exatamente essa sequência de leitura e regras.
13. **IDs canônicos curtos.** A entrada canônica usada para calcular identidade pode continuar contendo toda a informação necessária para estabilidade, mas não é persistida como ID. O ID público deve ter prefixo curto de tipo e digest determinístico codificado de forma compacta, com limite total de 40 caracteres. Ele não contém caminho, assinatura, percent-encoding nem outro ID completo. Colisões de digest devem ser detectadas durante a construção e causar falha explícita, nunca sobrescrita silenciosa.
14. **Handles locais mínimos.** Identidades canônicas, documentos, strings e evidências repetidas são deduplicados em tabelas estáveis. Registros de projeção usam handles locais numéricos ou compactos de até 10 caracteres, resolvíveis diretamente pelos índices do manifesto. Um registro não repete o ID público quando um handle local é suficiente.
15. **Apresentação separada da identidade.** Nome, nome qualificado, assinatura, projeto e variante são propriedades descritivas consultáveis. Não fazem parte da representação textual persistida do ID e não são repetidos em cada relação.
16. **Sharding por tamanho e família.** O armazenamento agrupa registros deterministicamente até uma faixa-alvo de bytes, preservando famílias úteis para recuperação. Prefixos de hash podem participar da distribuição, mas não podem criar um shard por registro ordinário nem substituir a medição real do payload.
17. **Índices voltados às jornadas.** Os índices primários são identidade, ponto de entrada/operação, relações de saída, relações de entrada, contrato, persistência e evidência/disposição. Cada índice deve levar diretamente ao próximo passo sem exigir varredura global.
18. **Gate de utilidade.** O pacote registra quantidade de arquivos, bytes, leituras e tokens por jornada. Cenários aplicáveis não exercitados falham na certificação. Limites estruturais impedem a conclusão bem-sucedida da publicação; limites de recuperação impedem certificação bem-sucedida e produzem diagnóstico explícito.
19. **Atomicidade preservada.** Falhas de variante, retenção, segurança, tamanho, materialização ou validação não substituem o último pacote válido. Artefatos de staging podem ser mantidos apenas por opção diagnóstica explícita e nunca são anunciados como pacote comprometido.
20. **Métricas honestas.** Contagens internas de extração e contagens publicadas são reportadas separadamente. Redução de payload não pode ser apresentada como aumento de cobertura, e itens filtrados devem ser agregados por motivo.
21. **Isolamento mantido.** Cada solução continua sendo uma unidade de análise independente. Deduplicação e handles nunca atravessam a fronteira de um pacote.
22. **Sem dependência de serviço externo.** A recuperação aceita nesta correção deve funcionar diretamente sobre o pacote materializado. Motores de consulta, embeddings e compilação de wiki permanecem consumidores posteriores.
23. **Reescrita a partir de uma base vazia autorizada.** A execução não precisa preservar assemblies, módulos, abstrações, estágios, contratos internos, schemas ou testes existentes. Ela pode remover a codebase de produto e a suíte legadas no início do workstream e construir somente os módulos exigidos por esta especificação. Reutilização é uma decisão pontual: uma parte atual só permanece quando seu comportamento ainda é requerido, reduz complexidade e pode ser verificado pelo novo seam de aceitação. Não se mantém infraestrutura antiga apenas para facilitar uma remoção posterior.
24. **Somente o estado final é produto.** Não é requisito que commits intermediários mantenham o comportamento legado, compilem todas as partes removidas ou produzam pacotes utilizáveis. Antes da conclusão, o novo produto deve compilar e passar pelo seam de aceitação completo. O plano de execução deve marcar estados intermediários deliberadamente incompletos e impedir que sejam confundidos com uma entrega pronta.
25. **Testes seguem o novo contrato.** Testes existentes que fixem comportamento, formato ou arquitetura descartados devem ser removidos ou substituídos. Eles não são critérios de regressão. Permanecem relevantes apenas testes que expressem invariantes também exigidas por esta especificação, especialmente segurança, determinismo, isolamento e atomicidade.
26. **Dependência é Retrieval Projection.** Uma dependência agregada não cria uma nova verdade factual nem substitui Confirmed Relation. Ela é derivada deterministicamente do grafo retido, declara os tipos de relação que participaram da agregação e mantém referências para suas origens auditáveis.
27. **Escopos de agregação.** O pacote publica dependências nos escopos Document, Project, Component e Deployment Unit. A subida de escopo usa somente relações de pertencimento comprovadas. A mesma relação de baixo nível pode contribuir para mais de um escopo sem ter seu payload duplicado.
28. **Categorias permanecem separadas.** Project Reference, invocação entre símbolos, uso estrutural de tipo, HTTP, gRPC, mensageria, contrato e persistência são categorias consultáveis. Uma visão combinada pode somá-las por par de entidades, mas precisa preservar a decomposição e não pode chamar correlação de causalidade confirmada.
29. **Ocorrências são agregadas.** Call sites e usos de tipo repetidos contribuem com contagens e evidências deduplicadas para uma aresta agregada. O pacote padrão não publica uma cópia integral de cada ocorrência; apenas evidências necessárias para inspeção e disposições relevantes permanecem acessíveis.
30. **Medidas explicáveis.** Fan-out e fan-in contam destinos e origens distintos, respectivamente. Quantidade de ocorrências conta contribuições observadas antes da deduplicação da aresta. Cruzamento entre componentes conta arestas cujas entidades agregadas pertencem a componentes distintos. Ciclos são calculados sobre o grafo dirigido do escopo solicitado. Impacto reverso é o conjunto alcançável pelo índice de entrada, com profundidade declarada. Valores confirmados e lacunas são reportados separadamente.
31. **Sem score composto.** O contrato não define um índice único de acoplamento, risco ou qualidade. Qualquer interpretação posterior combina medidas explícitas fora do gerador e não altera os fatos publicados.
32. **Navegação humana direta.** A entrada Markdown resume componentes, Deployment Units, ciclos, maiores fan-in/fan-out e jornadas disponíveis. Páginas de componente/serviço e dos documentos retidos apresentam outgoing, incoming, medidas, efeitos e lacunas com links Markdown diretos. JSON, ordinais e shards permanecem detalhes de auditoria e consumo de máquina, não pré-requisitos da jornada humana normal.
33. **Um seam externo de aceitação.** A interface externa validada é a CLI completa: analisar a solução, publicar o pacote, reidratá-lo, validá-lo e responder às quatro jornadas. Internamente, o desenho deve concentrar Roslyn e resolução em um módulo de análise, retenção/agregação/projeção em um módulo de construção do pacote e materialização/validação/commit em um módulo de publicação. Esses módulos não exigem interfaces públicas para passes, classificadores ou projectors internos.
34. **Interfaces internas orientadas a resultados.** O módulo de análise produz um grafo factual em memória; o construtor recebe esse grafo e uma política de publicação e produz um plano completo do pacote; o publicador recebe o plano, materializa, reidrata, valida e retorna somente um pacote comprometido. Complexidade de Roslyn, sharding, handles e staging não atravessa essas interfaces.
35. **Projeções equivalentes.** Markdown e os índices legíveis por máquina são derivados do mesmo conjunto retido de entidades, dependências e medidas. Divergência entre uma página e sua representação de máquina é corrupção de publicação.

## Testing Decisions

1. O principal seam de aceitação é a CLI completa: analisar um corpus versionado representativo, publicar, validar imediatamente o pacote materializado e executar as quatro jornadas de recuperação.
2. O corpus de resiliência de publicação deve ganhar casos pequenos que combinem projeto web, projeto cliente multi-target, identidades presentes em múltiplas variantes, comentário iniciado por `//`, caminho absoluto real em configuração, fonte de produção e fonte de teste.
3. O teste end-to-end deve provar que um framework-alvo de um projeto cliente não é aplicado ao projeto web e que identidades compartilhadas entre variantes não causam colisão artificial.
4. O teste end-to-end deve provar que comentários C# não são caminhos UNC e que caminhos absolutos reais nunca aparecem no pacote comprometido.
5. O teste end-to-end deve provar que qualquer pacote anunciado como comprometido passa pelo comando de validação sem diferença de interpretação.
6. O teste end-to-end deve verificar respostas, não somente arquivos: localização, fluxo até efeitos, impacto reverso e abertura de evidência/disposição precisam retornar as entidades esperadas dentro dos budgets.
7. O teste end-to-end deve verificar ausência: observações desconectadas, usos de tipo não retidos, testes não habilitados, fontes sem citação e valores brutos de configuração não podem aparecer no pacote padrão.
8. Testes focados devem cobrir avaliação de variantes por projeto, acumulação de ocorrências por variante, fechamento da política de retenção, geração e colisão de IDs, deduplicação de referências, packing determinístico de shards, classificação lexical de comentários e validação de fragmentos diferidos.
9. Os testes de packing devem usar distribuições com registros pequenos e grandes, provando estabilidade de ordenação, limite de tamanho e ausência do padrão de um arquivo por registro.
10. Os testes de retenção devem demonstrar que remover uma observação não utilizada não muda respostas e que remover uma evidência necessária torna a afirmação inválida ou não publicável.
11. Os testes de atomicidade devem começar com um pacote válido, provocar cada classe de falha pré-commit e confirmar que o pacote anterior permanece intacto.
12. Os budgets da fixture sob controle de fonte devem ser mais estritos que os corpora reais e suficientes para detectar crescimento proporcional ao número de registros.
13. Quando os clones locais estiverem presentes, a validação de aceitação deve analisar eShop, eShopOnContainers e Pitstop. A ausência desses clones não falha CI.
14. Na aceitação local da entrega inicial, eShop deve concluir sem colisão artificial; eShopOnContainers deve respeitar 1.500 arquivos e 64 MiB; Pitstop deve respeitar 750 arquivos e 25 MiB; todos os pacotes comprometidos devem passar pela validação posterior.
15. As quatro jornadas devem ser marcadas como exercitadas quando aplicáveis. Na entrega inicial, localização deve respeitar 8 leituras e 12 mil tokens; fluxo e impacto reverso, 32 leituras e 125 mil tokens; prova/disposição, 12 leituras e 25 mil tokens.
16. As medições anteriores devem permanecer registradas como baseline de regressão, permitindo demonstrar a redução e evitando que os limites sejam substituídos por uma comparação vaga.
17. Durante execução spec-driven desta correção, permanece vigente a decisão do projeto de não executar o discrimination sensor; os demais gates do Verifier continuam obrigatórios.
18. Testes de identidade devem usar tipos genéricos profundamente aninhados, assinaturas longas e caminhos extensos, provando que todo ID público permanece com até 40 caracteres e todo handle local com até 10 caracteres.
19. Testes de pacote devem confirmar que relações e índices não repetem assinaturas, caminhos ou IDs públicos quando puderem referenciá-los por handle.
20. Os testes de aceitação devem verificar, pelo seam da CLI, respostas esperadas para dependências de um arquivo, dependentes de um arquivo, dependências de um componente/serviço e impacto reverso a partir de contrato ou dado.
21. A fixture sob controle de fonte deve conter ao menos uma Project Reference, uma chamada entre documentos, chamadas repetidas entre a mesma origem e destino, uma dependência entre componentes, uma integração de runtime, um ciclo e uma ocorrência que permaneça candidata, unknown ou Open Frontier.
22. Os testes devem provar que chamadas repetidas produzem uma única dependência agregada com contagem correta e evidências resolvíveis, sem um arquivo ou registro completo por ocorrência no pacote padrão.
23. Fan-in, fan-out, cruzamentos entre componentes, ciclos e impacto reverso devem ser recalculados nos testes a partir das arestas esperadas da fixture, sem usar a implementação de produção para formar a expectativa.
24. Testes de escopo devem provar que a agregação Document → Project → Component → Deployment Unit preserva categorias e não promove nome de projeto ou diretório a serviço.
25. Testes de variantes devem provar que uma dependência presente em uma variante e ausente em outra conserva essa qualificação, sem duplicar a identidade lógica da origem ou do destino.
26. Testes de navegação devem iniciar no resumo Markdown e alcançar dependências, dependentes e evidências por links existentes, sem exigir enumeração de diretório, escolha manual de shard ou decodificação de ID.
27. Testes de equivalência devem comparar as dependências e medidas exibidas em Markdown com a representação legível por máquina derivada do mesmo pacote.
28. Testes focados em módulos internos só complementam o seam da CLI quando localizam melhor uma falha de avaliação de projeto, retenção, agregação, packing ou publicação. Eles verificam resultados observáveis e não fixam quantidade, nomes ou ordem de passes internos.

## Out of Scope

- Criar motor de consulta, banco vetorial, embeddings, wiki ou interface gráfica.
- Interpretar regras de negócio, intenção humana ou semântica não sustentada por evidência.
- Implementar análise geral de dataflow, reflexão, geração dinâmica ou cobertura completa de runtime.
- Descobrir todas as dependências possíveis em runtime ou promover uma correlação sem evidência a dependência confirmada.
- Definir score composto de acoplamento, risco, qualidade ou manutenibilidade, ou classificar automaticamente uma medida como boa ou ruim.
- Tratar todo projeto, assembly, diretório ou convenção de nome como serviço ou Deployment Unit comprovada.
- Publicar cada call site, uso de tipo ou ocorrência bruta como um registro autônomo no pacote padrão.
- Adicionar suporte a outras linguagens.
- Incluir testes no pacote padrão.
- Preservar compatibilidade de API, CLI, wire format, schema, layout ou comportamento com o produto legado.
- Manter a implementação atual funcionando durante a reescrita.
- Criar adaptadores, conversores, feature flags ou rotas paralelas para consumidores do produto anterior.
- Criar versionamento de contrato, dispatch por versão, leitores de formatos anteriores ou uma matriz de compatibilidade.
- Otimizar o tempo total de análise, exceto quando a mudança for consequência direta de evitar trabalho ou persistência desnecessários.
- Reabrir correções já entregues que não afetem utilidade, variantes ou integridade de publicação.
- Alterar ou publicar issues, labels, branches ou pull requests no GitHub.
- Implementar a correção como parte desta especificação.

## Further Notes

- Este documento é local e está pronto para ser convertido em uma feature executável do fluxo spec-driven quando o workstream for explicitamente iniciado.
- A implementação deve ser tratada como substituição do produto atual, não como evolução compatível. O código existente é material de referência e pode ser descartado; a arquitetura normativa e os critérios desta especificação definem o produto final.
- O workstream pode começar removendo a implementação, os schemas e os testes legados. Não existe etapa obrigatória de desativação posterior: qualquer parte reaproveitada precisa justificar sua presença diretamente no produto novo.
- Identidade de build, commit ou execução pode permanecer em diagnósticos para reprodução, mas não constitui versionamento de contrato e não seleciona leitores ou comportamentos alternativos.
- A especificação anterior de redução de complexidade acidental continua como registro das intervenções já planejadas ou entregues. Em caso de conflito, este documento é normativo para o contrato de utilidade do pacote, retenção do conteúdo publicado, isolamento de variantes e equivalência entre análise e validação.
- Baseline observado no eShopOnContainers: 5.725 fatos, 20.179 observações, 6.951 relações, 29.375 arquivos e aproximadamente 139 MB. A localização de identidade exigiu 315 leituras e cerca de 186 mil tokens.
- Baseline observado no Pitstop: 1.811 fatos, 6.999 observações, 3.377 relações, 10.595 arquivos e aproximadamente 50 MB. A localização de identidade exigiu 163 leituras e cerca de 90 mil tokens.
- No eShop, um repro mínimo confirmou que a adição do projeto cliente multi-target faz frameworks-alvo contaminarem o projeto web e gera colisões artificiais. O caso deve ser preservado de forma sintética e pequena.
- No eShopOnContainers, a rejeição posterior foi causada por comentário C# confundido com UNC. No Pitstop, havia caminho absoluto real em configuração de desenvolvimento. O teste deve representar as duas classes sem copiar credenciais ou valores sensíveis dos corpora.
- Os limites de escala dos corpora locais incluem uma folga deliberada para a entrega inicial. A baseline estável deve orientar sua redução; aumentá-los exige evidência de que as quatro jornadas continuam diretamente navegáveis, e o crescimento do corpus, sozinho, não justifica relaxá-los.
- O seam de aceitação foi definido durante o diagnóstico: o produto só está corrigido quando a CLI gera, valida e recupera respostas úteis no mesmo teste de ponta a ponta. Testes unitários existem para localizar regressões, não para substituir essa prova.
- Nesta especificação, “medir dependências” significa publicar contagens de origens, destinos e ocorrências, fan-in, fan-out, cruzamentos entre componentes, ciclos, impacto reverso e lacunas relevantes. Não significa atribuir um score subjetivo ao código.
- A Retrieval Projection de dependências recupera o objetivo original do produto sem diluir a linguagem factual: Confirmed Relation continua sendo a aresta causal auditável; dependência é uma visão agregada e diretamente navegável dessas arestas.
