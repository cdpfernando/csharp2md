# csharp2md

English | [Português](./README.pt-BR.md)

`csharp2md` converts a C#/.NET project or codebase into Markdown files and
generates indexes and a consolidated view of dependencies between services.

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

## Generated output

In addition to one `.cs.md` file for each C# source file, the output contains:

- `index.md` at the root and for each service;
- `dependencies.json` containing the dependency graph;
- `dependencies.mmd` containing the Mermaid diagram;
- `.csharp2md-output`, which identifies a directory managed by the tool.
