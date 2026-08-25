# Architecture knowledge engine

## Status

Arquitetura alvo aprovada. Ainda não implementada. A implementação e os schemas atuais são legados e serão removidos durante o roadmap.

## Objective

O csharp2md deve transformar `1..N` soluções C#/.NET em um pacote de conhecimento técnico factual, auditável e diretamente navegável por uma LLM. O pacote deve sustentar perguntas como:

- qual entry point inicia uma operação e por quais callables ela passa;
- quais contratos, efeitos externos e operações de persistência participam do fluxo;
- quais entry points e fluxos podem ser afetados por um símbolo, contrato ou campo de dados;
- por que uma ligação foi confirmada, mantida como candidata ou deixada aberta.

O gerador não interpreta regras de negócio. Ele entrega o código relevante e a cadeia técnica para uma LLM interpretar condições como `balance < amount`.

## Authority hierarchy

```text
source and configuration
  -> structural facts
  -> immutable observations
  -> classified facts
  -> confirmed direct relations
  -> derived retrieval projections
  -> optional future query/wiki/LLM interpretation
```

Uma camada inferior nunca recebe significado inventado por uma camada superior. Markdown, catálogos, diagramas e futuros resultados de query são projeções; não são autoridade factual.

## Pipeline

```text
Inventory
-> Semantic Analysis
-> Observation Extraction
-> Classification and Promotion
-> Validation and Coverage
-> Persistence
-> Retrieval Projection
-> Batch Composition
```

### Inventory

Recebe uma lista explícita de `1..N` soluções. Cada solução é uma unidade semântica independente. Arquivos auxiliares são inventariados apenas dentro da raiz autorizada; symlinks que escapem dela são rejeitados.

### Semantic analysis

C# usa Roslyn e APIs reais do compilador. O modo semântico exige solução confiável; source generators exigem opt-in adicional; diagnostic analyzers nunca executam. Target frameworks e configurações diferentes são `analysis_variant`s explícitas.

### Observation extraction

Extratores emitem somente observações do registry. Detalhes internos de Roslyn não atravessam a interface do extrator. Arquivos suportados fora de C# usam adapters compilados e declaradores de capacidade.

### Classification and promotion

Classificadores versionados consomem observações e registram evidências aceitas, condições negativas e candidatos rejeitados. Ordem de execução não decide conflitos. Passes formam um DAG declarado: estrutura, framework, fronteiras/contratos, causalidade e enriquecimento cruzado.

### Validation and coverage

Unknowns e candidatos são resultados válidos. Fato derivado estruturalmente inválido entra em quarentena e reprova a certificação. Colisão de identidade, hash inválido, duplicidade incompatível ou corrupção sistêmica abortam o commit.

### Persistence and projection

Uma porta transacional recebe fragmentos validados; o adapter de storage escreve em staging e publica o manifest por último. Projeções produzem catálogos, postings, Markdown e fonte navegável sem criar fatos.

### Batch composition

Cada solução publica seu próprio manifest, cobertura e diagnósticos. Um lote com mais de uma solução deriva um catálogo global apenas de deployments, componentes e relações externas comprovadas. Não materializa um `Compilation`, `SymbolIndex` ou grafo interno monolítico.

## Modules and interfaces

### `Csharp2Md.Domain`

Define identidades, tipos, facetas, relações, estados de prova e invariantes. Não depende de Roslyn, filesystem, JSON ou CLI.

### `Csharp2Md.Analysis`

Interface externa única: analisar uma requisição validada e publicar resultados através de uma porta transacional. Esconde Roslyn, adapters, passes e classificadores como seams internos.

### `Csharp2Md.Storage`

Implementa persistência, schemas, sharding, leitura factual, staging e commit. Não classifica nem interpreta fatos.

### `Csharp2Md.Projection`

Produz catálogos, postings, Markdown, fonte e composição navegável a partir de outputs validados. Não recebe Roslyn e não promove conhecimento.

### `Csharp2Md.Cli`

Faz parsing, valida opções, compõe adapters e apresenta resultados. Não contém taxonomia, regras de classificação ou travessia causal.

## Supported initial sources

- C# com Roslyn;
- solution/project/MSBuild metadata;
- `appsettings*.json` e configuração .NET reconhecida;
- Dockerfile e Docker Compose;
- Kubernetes sem template;
- protobuf e OpenAPI;
- SQL literal, arquivos `.sql` e migrations EF Core em C#.

Arquivos não suportados permanecem no inventário. `Unsupported`, `optional capability unavailable` e `adapter failure` são resultados diferentes.

## Framework semantics

Suporte a framework declara pacote/assembly, versões testadas, assinaturas, observações, promoções e limitações. Wrappers internos podem ser mapeados por configuração para tipos e facetas existentes, mas mappings não inventam símbolos, calls, endpoints ou contratos.

Dispatch in-process, como MediatR e domain events, não é mensageria externa. Middleware, decorators e pipeline behaviors são interceptores técnicos; podem ser ocultados em uma leitura de negócio, mas seus efeitos continuam visíveis.

## Explicit non-goals

- `BusinessRuleFact`, decisão de compatibilidade ou explicação funcional;
- LLM, embedding ou busca vetorial dentro do gerador;
- query engine obrigatório;
- wiki compilada, `kb`, QMD ou integração equivalente;
- incremental analysis e snapshot diff na primeira entrega;
- plugins dinâmicos;
- suporte semântico inicial a linguagens diferentes de C#;
- promessa de fechar reflection, runtime dispatch ou variantes não analisadas.

Uma camada de query poderá ser criada depois que o output estiver completo e validado. Ela consumirá somente manifests, fatos e postings, sem redefinir a taxonomia.
