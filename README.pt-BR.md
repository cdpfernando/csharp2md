# csharp2md

[English](README.md) | Português

O `csharp2md` está sendo reestruturado como um motor de conhecimento arquitetural para C#/.NET. Ele analisará `1..N` soluções explícitas e publicará fatos, observações, relações causais e projeções de fonte precisas, auditáveis e diretamente navegáveis por uma LLM.

## Estado da migração

A arquitetura alvo está aprovada, mas ainda não foi implementada. O código-fonte, o comportamento do CLI e os schemas atuais são legados e não representam o contrato futuro. A primeira spec da substituição ainda não foi criada, intencionalmente.

Comece por:

1. [Linguagem do domínio](CONTEXT.md)
2. [Arquitetura alvo](docs/architecture/README.md)
3. [Decisões ativas](.specs/STATE.md)
4. [Roadmap da substituição](architecture-knowledge-engine-roadmap.md)

## Limites do produto

- O gerador é responsável por extração determinística, classificação, validação, persistência e projeções de recuperação.
- Markdown é uma projeção; fatos e observações legíveis por máquina são a autoridade.
- A interpretação de regras de negócio pertence a uma LLM posterior, alimentada com o código relevante completo.
- Query engines, ingestão de wiki, `kb`, QMD, embeddings e análise incremental ficam adiados até o output do gerador estar completo.
- Não existe exigência de retrocompatibilidade durante a substituição. A continuidade começa no primeiro release completo pós-migração.

## Base de desenvolvimento

- .NET SDK 10
- `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0
- sem referência direta a `Microsoft.Build.*` e sem `MSBuildLocator.RegisterDefaults()`

```shell
dotnet build csharp2md.slnx
dotnet test csharp2md.slnx
```

Não deduza a semântica alvo a partir da implementação legada. O novo desenvolvimento só começa quando o próximo workstream do roadmap for iniciado explicitamente pelo processo `tlc-spec-driven`.
