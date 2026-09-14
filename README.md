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

## Prerequisites

- Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), not just the runtime. [global.json](global.json) requests SDK `10.0.300` with `rollForward: latestFeature`, allowing newer .NET 10 feature bands.
- Install Git to clone the repository and allow access to NuGet feeds to restore dependencies.
- For the solutions you analyze, install their required SDKs/workloads and configure any private package feeds.

Check the available SDKs, then clone the repository. Run the following examples from its root directory:

```shell
dotnet --list-sdks
git clone https://github.com/cdpfernando/csharp2md.git
cd csharp2md
dotnet restore csharp2md.slnx
dotnet build csharp2md.slnx --configuration Release --no-restore
```

## Install from source

The CLI project is packaged as a .NET tool. Build a local package and install it globally for your user:

```shell
dotnet pack src/Csharp2Md.Cli/Csharp2Md.Cli.csproj --configuration Release --output artifacts/packages
dotnet tool install --global csharp2md --add-source ./artifacts/packages --version 4.0.0
csharp2md --help
```

`4.0.0` is the current package version in [Directory.Build.props](Directory.Build.props); adjust `--version` if it changes. See the [.NET tool installation documentation](https://learn.microsoft.com/dotnet/core/tools/dotnet-tool-install) for installation options.

If `csharp2md` is not found, reopen your terminal and check that the global tools directory is on `PATH`: `%USERPROFILE%\.dotnet\tools` on Windows or `$HOME/.dotnet/tools` on Linux/macOS.

To replace an existing installation with a newly built package, run `dotnet tool uninstall --global csharp2md`, then repeat the pack/install commands above. The same uninstall command removes the tool when you no longer need it.

## Run the current CLI

These commands describe the current implementation during migration; they are not a commitment to the future CLI contract.

Restore the solution you want to analyze, then provide its `.sln` or `.slnx` file and an output directory:

```shell
dotnet restore "path/to/MySolution.slnx"
csharp2md analyze --solution "path/to/MySolution.slnx" --output "artifacts/my-solution"
```

Replace the example path with your solution. Repeat `--solution` to analyze multiple solutions in one invocation. Use `csharp2md analyze --help` for all analysis options; `csharp2md --help` also lists the `validate` and `compose` commands for already-published packages.

### Run without installing

Use `dotnet run` from the repository root. `--no-launch-profile` disables the repository's predefined launch settings, and everything after `--` is passed to the CLI:

```shell
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- --help
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- analyze --solution "path/to/MySolution.slnx" --output "artifacts/my-solution"
```

### Try the bundled fixture

`Acme.Shipping` is a small solution included in the repository:

```shell
dotnet restore fixtures/SyntheticSolution/Acme.Shipping/Acme.Shipping.slnx
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- analyze --solution fixtures/SyntheticSolution/Acme.Shipping/Acme.Shipping.slnx --output artifacts/shipping-out
```

With the tool installed, replace `dotnet run --project src/Csharp2Md.Cli --no-launch-profile --` with `csharp2md`.

The fixture can publish a package with `degraded` certification (exit code `3`): the current example reports incomplete call coverage. Inspect `run-certification.json` inside the published package for the reasons.

The output directory receives `batch-manifest.json` and a package directory named `s-<hash>` for each published solution. Start with the batch manifest to locate each package. Read the CLI diagnostics and certification result: generated files alone do not imply a fully successful analysis. The current exit codes are:

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1` | Invalid invocation |
| `2` | Partial composition or batch publication failure |
| `3` | Degraded certification |
| `4` | Failed certification |
| `5` | Structural corruption |
| `6` | Incompatible provenance or contract version |

## Development baseline

- .NET SDK as specified in [global.json](global.json)
- `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0
- no direct `Microsoft.Build.*` reference and no `MSBuildLocator.RegisterDefaults()`

```shell
dotnet build csharp2md.slnx
dotnet test csharp2md.slnx
```

Do not infer target semantics from the legacy implementation. New implementation work begins only by explicitly starting the next roadmap workstream through `tlc-spec-driven`.
