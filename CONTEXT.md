# Architecture Knowledge Engine

O csharp2md transforma uma ou mais soluções C#/.NET em conhecimento técnico factual, auditável e diretamente navegável. Este glossário define a linguagem canônica do produto; nomes de máquina permanecem em inglês.

## Evidence and classification

**Evidence**:
Referência verificável ao código, configuração ou artefato que sustenta uma observação ou promoção.
_Avoid_: Proof text, excerpt

**Observation**:
Ocorrência imutável diretamente extraída de uma fonte, sem afirmar significado arquitetural.
_Avoid_: Claim, inferred relation, raw fact

**Classification**:
Interpretação determinística e versionada de observações segundo uma regra registrada.
_Avoid_: Heuristic label, tag

**Candidate**:
Classificação ou ligação plausível que ainda não possui evidência suficiente para promoção.
_Avoid_: Weak fact, probable fact

**Unknown**:
Tentativa identificada de classificação ou resolução que não pôde ser concluída, acompanhada de causa e evidência disponível.
_Avoid_: Missing data, ignored item

**Open Frontier**:
Ponto em que o comportamento pode continuar, mas o destino não é estaticamente demonstrável no escopo analisado.
_Avoid_: Broken edge, guessed target

## Structural knowledge

**Structural Fact**:
Identidade comprovada da organização física ou semântica do código: solução, projeto, documento ou símbolo.
_Avoid_: Architectural fact

**Callable**:
Símbolo executável que pode possuir corpo e participar de invocações, como método, construtor, função local ou lambda identificável.
_Avoid_: Function when the C# construct is not known

## Architecture

**Component**:
Agrupamento lógico de código com responsabilidade técnica coesa, formado a partir de evidência de deploy, uso privado, compartilhamento ou configuração explícita.
_Avoid_: Project, service, module

**Deployment Unit**:
Unidade executável ou publicável que pode ser implantada independentemente.
_Avoid_: Component, project, service

**Entry Point**:
Ponto comprovado pelo qual uma execução ou fluxo pode começar.
_Avoid_: Public method, endpoint

**Boundary Operation**:
Interação de entrada ou saída que atravessa uma fronteira por HTTP, gRPC, mensageria, CLI, scheduler ou função.
_Avoid_: Entry point, call, integration

**External System**:
Destino externo com identidade demonstrada por fonte ou configuração autorizada.
_Avoid_: Raw URL, logical name, guessed service

## Contracts

**Contract**:
Identidade lógica de um payload de fronteira, compartilhada apenas quando uma chave comum é comprovada.
_Avoid_: DTO, CLR type, similar payload

**Contract Binding**:
Associação comprovada entre uma operação, o papel do payload, um símbolo CLR e um contrato.
_Avoid_: Type reference

**Contract Revision**:
Forma estrutural observada de um contrato em uma execução ou variante.
_Avoid_: Compatibility decision, new contract

## Persistence

**Data Store**:
Escopo lógico ou físico de persistência, local ao deployment até que identidade compartilhada seja comprovada.
_Avoid_: Connection string, database name without proof

**Data Object**:
Estrutura endereçável dentro de um data store, como tabela, view, collection, key-space ou região de cache.
_Avoid_: Entity class

**Data Field**:
Campo lógico ou físico pertencente a um data object.
_Avoid_: Property without mapping

**Data Operation**:
Ocorrência de leitura, inserção, atualização, remoção ou execução sobre dados.
_Avoid_: Repository, data dependency

## Runtime and output

**Confirmed Relation**:
Aresta causal direta com origem, destino e cadeia de evidência identificados.
_Avoid_: Reference, dependency, possible relation

**Analysis Variant**:
Combinação explícita de target framework, configuração, símbolos e ambiente considerada durante a análise.
_Avoid_: Build, run mode

**Run Certification**:
Resultado da cobertura e integridade de uma execução específica.
_Avoid_: Classifier quality

**Engine Certification**:
Resultado de precisão e recall do motor em corpora previamente rotulados.
_Avoid_: Run coverage

**Retrieval Projection**:
Catálogo, posting ou página derivada que torna o pacote factual navegável sem alterar sua semântica.
_Avoid_: Fact, query result, wiki article
