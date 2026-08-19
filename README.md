# csharp2md

English | [Português](./README.pt-BR.md)

`csharp2md` converts a C#/.NET project or codebase into a validated,
machine-readable factual model plus Markdown documentation, with a
consolidated view of dependencies between services.

## Prerequisites

- .NET SDK 10 available on `PATH`.
- Dependencies for the projects being analyzed restored whenever possible.

## Run from source

Use `--` to separate `dotnet run` arguments from `csharp2md` arguments:

```shell
dotnet run --project src/Csharp2Md.Cli -- --help
```

The examples below use the global `csharp2md` command. During development, you
can replace it with `dotnet run --project src/Csharp2Md.Cli --`.

## Examples

### Analyze the current directory

```shell
cd MySolution
csharp2md
```

If the current directory is named `MySolution`, the output is written by
default to a sibling directory named `MySolution_md`.

### Analyze a specific directory

```shell
csharp2md ./src
```

In this case, the default output is `./src_md`. Wrap paths containing spaces in
quotes:

```shell
csharp2md "C:\repos\My Solution"
```

### Choose the output directory

`--output` (or `-o`) represents the exact final path; no suffix is appended:

```shell
csharp2md ./src --output ./docs/code
csharp2md ./src -o ./docs/code
```

### Analyze multiple services with a manifest

Create a `manifest.json` file:

```json
{
  "services": [
    { "path": "services/Orders" },
    { "path": "services/Payments", "name": "Payments API" },
    {
      "path": "services/Notifications",
      "projects": ["services/Notifications/Notifications.Api.csproj"]
    }
  ]
}
```

Then run:

```shell
csharp2md --manifest ./manifest.json --output ./docs/code
```

The `path` field also accepts `*` or `?` in its final segment. For example:

```json
{
  "services": [
    { "path": "services/Acme.*" }
  ]
}
```

Manifest paths are resolved from the directory where the command is run. The
positional directory argument and `--manifest` are alternative modes and
cannot be used together.

### Replace existing output

`csharp2md` can automatically update directories it created. To replace a
non-empty directory that was not created by the tool, use `--force`:

```shell
csharp2md ./src --output ./docs/code --force
```

This command removes the previous contents of the output directory. The tool
always refuses to use the input directory, one of its ancestors, or a file
system root as its output directory.

### Topic and domain

`--topic` and `--domain` label the generated output — written to
`raw/topic.yaml` and the factual manifest — for organizing several analyzed
codebases under one documentation site. Both default sensibly (`--topic` to a
slug of the input directory name, `--domain` to `system-design`), so most runs
can omit them:

```shell
csharp2md ./src --topic payments-api --domain billing
```

### Run against this repository's fixture

Without installing the tool globally:

```shell
dotnet run --project src/Csharp2Md.Cli -- \
  ./fixtures/SyntheticSolution/Acme.Orders \
  --output ./artifacts/example
```

In PowerShell, the same command can be written on one line:

```powershell
dotnet run --project src/Csharp2Md.Cli -- ./fixtures/SyntheticSolution/Acme.Orders --output ./artifacts/example
```

## Install as a global tool from this repository

```shell
dotnet pack src/Csharp2Md.Cli -c Release
dotnet tool install --global --add-source ./src/Csharp2Md.Cli/nupkg csharp2md
csharp2md --help
```

To reinstall a locally packed version, first remove the previous installation
with `dotnet tool uninstall --global csharp2md`.

## Analysis modes and trust

By default, `csharp2md` analyzes source and project files **inertly**: it
never starts `dotnet msbuild`, loads a Roslyn workspace, or runs analyzers,
source generators, or any other executable analysis path. This makes the
default safe to point at an arbitrary or untrusted repository, at the cost of
semantic precision — symbol resolution, framework detection, and relation
facts stay syntactic only.

```shell
csharp2md ./src                                                # syntax-only, untrusted (default)
csharp2md ./src --analysis semantic --trust trusted-solution   # full semantic enrichment
```

| Option | Values | Default | Effect |
| --- | --- | --- | --- |
| `--analysis` | `syntax-only`, `semantic` | `syntax-only` | `semantic` evaluates MSBuild projects and binds Roslyn compilations. |
| `--trust` | `untrusted`, `trusted-solution` | `untrusted` | `semantic` mode requires `--trust trusted-solution`; omitting it reports the trust requirement and exits `1` before touching the output directory. |
| `--include-source-generators` | flag | off | Runs the project's own source generators (never diagnostic analyzers). Requires trusted semantic mode; the invalid combination exits `1` before touching the output directory. |
| `--analysis-timeout` | duration, e.g. `00:05:00` | `00:10:00` | Positive per-service analysis timeout. A non-positive or unparsable value exits `1` before touching the output directory. |

Trusted semantic mode still never performs a restore or runs custom MSBuild
targets or diagnostic analyzers — only property/item evaluation, compilation,
and, with explicit opt-in, the project's own source generators. Only enable it
against solutions you trust.

Any failure in project evaluation, workspace loading, compilation, binding, or
a single detector degrades just the affected scope to its syntactic facts and
records a diagnostic; the run still exits `0` unless a structural validation
failure occurs (for example, a duplicate fact identity).

## Generated output

Every run writes under `<output>/raw/`, alongside a `.csharp2md-output`
marker at the output root that the tool uses to recognize directories it
manages:

- `raw/codebase/<service>/...` — one `.cs.md` file per analyzed source file,
  reconstructed byte-for-byte from the source plus factual annotations
  outside the code, and `components.md`, an index of library ownership.
- `raw/facts/manifest.json` — schema and tool versions, requested and
  effective analysis mode, trust, coverage counts, and the content-addressed
  reference and SHA-256 hash of every persisted fact fragment. This is the
  entry point for any tool consuming `csharp2md`'s output programmatically.
- `raw/facts/<kind>/<hash-prefix>/<hash>.json` — validated, content-addressed
  fact fragments (one per solution, project, target, document, or symbol),
  each referenced from the manifest.
- `raw/facts/relations/{compile-time,inheritance,dependency-injection,http,grpc,events}.json`
  — validated relations, partitioned by kind. An unproven runtime target
  (for example, an HTTP call whose destination is only a name in
  configuration) keeps a `null` target and an explicit `unresolved_reason`
  rather than guessing at a match.
- `raw/facts/diagnostics.json`, `raw/facts/coverage.json` — every degradation
  encountered during the run and honest per-scope coverage (not-applicable,
  unattempted, syntactic, partial, exact, or failed).
- `raw/dependencies.mmd` — a Mermaid diagram built from the validated
  relations above; empty in syntax-only mode, since no runtime relation can
  be confirmed without semantic binding.
- `raw/topic.yaml`, `raw/CLAUDE.md` — a small topic scaffold pointing at
  `raw/facts/manifest.json`.
- `raw/log.md` — a human-readable run summary. It is the only generated file
  that carries a timestamp, and it is never a source of machine-readable
  facts.

Each generated `.cs.md` file carries YAML frontmatter (`schema_version: 2`)
with the document's identity, project, component IDs, classification, an
analysis summary, and a `facts_ref` resolving to its own persisted document
fragment.

## Migrating from v2

v3's output is **not compatible** with v2: the flat `dependencies.json`
dependency graph is gone, replaced by the partitioned, validated relation
files under `raw/facts/relations/`, and every machine-readable artifact now
derives from validated facts rather than heuristic detectors. There is no
compatibility mode or dual-write option — point downstream consumers at
`raw/facts/manifest.json` instead of the old `dependencies.json`.
