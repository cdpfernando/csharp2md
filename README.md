# csharp2md

English | [Português](README.pt-BR.md)

`csharp2md` is being restructured as an architecture knowledge engine for C#/.NET. It will analyze `1..N` explicit solutions and publish precise, evidence-backed facts, observations, causal relations and directly navigable source projections for LLM consumption.

## Migration status

The target architecture is approved but not implemented. Current source code, CLI behavior and schemas are legacy and are not the future product contract. The first replacement feature spec has intentionally not been created yet.

Start here:

1. [Domain language](CONTEXT.md)
2. [Target architecture](docs/architecture/README.md)
3. [Active decisions](.specs/STATE.md)
4. [Replacement roadmap](architecture-knowledge-engine-roadmap.md)

## Product boundaries

- The generator owns deterministic extraction, classification, validation, persistence and retrieval projections.
- Markdown is a projection; machine-readable facts and observations are authoritative.
- Business-rule interpretation belongs to a downstream LLM supplied with complete relevant code.
- Query engines, wiki ingestion, `kb`, QMD, embeddings and incremental analysis are deferred until the generator output is complete.
- No backward compatibility is required during the replacement. Compatibility begins at the first completed post-migration release.

## Development baseline

- .NET SDK 10
- `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0
- no direct `Microsoft.Build.*` reference and no `MSBuildLocator.RegisterDefaults()`

```shell
dotnet build csharp2md.slnx
dotnet test csharp2md.slnx
```

Do not infer target semantics from the legacy implementation. New implementation work begins only by explicitly starting the next roadmap workstream through `tlc-spec-driven`.
