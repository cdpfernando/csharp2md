# csharp2md

English | [Português](README.pt-BR.md)

`csharp2md` analyzes `1..N` explicit C#/.NET solutions and publishes one knowledge package: evidence-backed facts, observations, causal relations and directly navigable source projections for LLM consumption.

## Project status

The knowledge package contract is the only contract in the repository. The replacement is complete: `Csharp2Md.Core` owns analysis, package building and publication, and `Csharp2Md.Cli` is its only external seam. There is no version dispatch, legacy reader, converter or compatibility route.

Start here:

1. [Domain language](CONTEXT.md)
2. [Active decisions and handoff](.specs/STATE.md)
3. [Package contract specification](docs/specs/pacote-conhecimento-util-e-confiavel.md)

## Product boundaries

- The generator owns deterministic extraction, classification, validation, persistence and retrieval projections.
- Markdown is a projection; machine-readable facts and observations are authoritative.
- Business-rule interpretation belongs to a downstream LLM supplied with complete relevant code.
- Query engines, wiki ingestion, `kb`, QMD, embeddings and incremental analysis are deferred until the generator output is complete.

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

## Run the CLI

The CLI exposes two commands: `analyze` and `validate`.

### `analyze`

Restore the solutions you want to analyze, then pass each one and a single output directory:

```shell
dotnet restore "path/to/MySolution.slnx"
csharp2md analyze --solution "path/to/MySolution.slnx" --output "artifacts/my-solution"
```

| Option            | Required | Meaning                                                                            |
| ----------------- | -------- | ---------------------------------------------------------------------------------- |
| `--solution`      | yes      | Path to a solution to analyze. Repeat once per solution; one package covers them all. |
| `--output`        | yes      | Directory that receives the committed package.                                      |
| `--include-tests` | no       | Include test projects and documents, and record that choice in the package identity. |

Analysis either commits a whole package or changes nothing. A run materializes, rehydrates and validates in staging, certifies the four retrieval journeys, and only then swaps the root `manifest.json` under lock. A rejected run leaves the previous package untouched.

### `validate`

Re-validate an already-published package. It reads only the package and touches no solution:

```shell
csharp2md validate --package "artifacts/my-solution"
```

### Run without installing

Use `dotnet run` from the repository root. `--no-launch-profile` disables the repository's predefined launch settings, and everything after `--` is passed to the CLI:

```shell
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- --help
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- analyze --solution "path/to/MySolution.slnx" --output "artifacts/my-solution"
```

### Try the bundled fixture

`fixtures/SyntheticSolution` is the versioned analysis fixture. `Acme.Orders` is one of its solutions:

```shell
dotnet restore fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx
dotnet run --project src/Csharp2Md.Cli --no-launch-profile -- analyze --solution fixtures/SyntheticSolution/Acme.Orders/Acme.Orders.slnx --output artifacts/orders-out
```

With the tool installed, replace `dotnet run --project src/Csharp2Md.Cli --no-launch-profile --` with `csharp2md`.

## The published package

A reader starts at exactly one file: `manifest.json` at the output root. It points at the committed generation under `generations/<digest>/`, and nothing else needs to be enumerated to navigate the package.

For each analyzed solution the manifest declares eight navigation indexes — `identity`, `roots`, `outgoing`, `incoming`, `contracts`, `persistence`, `evidence` and `measures` — and four certified retrieval journeys: `locate`, `follow_flow`, `reverse_impact` and `evidence_disposition`. Certification results live in `certification.json` and journey budgets in `measurements.json`.

Read the CLI diagnostics as well as the files: generated content alone does not imply a successful analysis. Diagnostics are written to stderr as `csharp2md: code=... stage=... cause=...`, with the solution, project, variant, family and artifact coordinates that apply.

Exit codes:

| Code | Meaning              |
| ---- | -------------------- |
| `0`  | Success              |
| `1`  | Invalid invocation   |
| `4`  | Certification failed |
| `5`  | Structural corruption |

## Development baseline

- .NET SDK as specified in [global.json](global.json)
- `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0
- no direct `Microsoft.Build.*` reference and no `MSBuildLocator.RegisterDefaults()`: Roslyn 4.9+ loads projects through an out-of-process BuildHost

The solution contains four projects — `Csharp2Md.Core`, `Csharp2Md.Cli` and their test projects:

```shell
dotnet build csharp2md.slnx
dotnet test csharp2md.slnx
```

`fixtures/SyntheticSolution` is the only versioned analysis fixture. Local clones of larger corpora are optional: when present they widen acceptance, and when absent their tests skip rather than fail.
