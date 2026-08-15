# CLI Directory Input Test Research

## Scope

The feature changes two production areas: output preparation in `OutputWriter`, and input/path resolution across `AnalysisPipeline` and the CLI entry point. Tests stay in the existing xUnit v2 project and follow its unit/integration split.

## Existing conventions

- SDK-style `net10.0` test project using xUnit 2.9.3 on VSTest.
- Unit tests live beside their production area under `tests/Csharp2Md.Core.Tests/`.
- Process-level CLI tests carry `[Trait("Category", "Integration")]`.
- Temporary directories are created per test and deleted in `finally` or `IDisposable.Dispose`.
- The Roslyn static-pairing analyzer found `OutputWriter` paired with `OutputWriterTests`; `AnalysisPipeline` paired with pipeline tests; top-level `Program.cs` has no declared type and is exercised by CLI process tests. Static pairing is a heuristic, not line or branch coverage.

## Acceptance checklist

- Direct positional directory and current-directory input work without a manifest file.
- Missing/file input and conflicting manifest + directory fail before output.
- Default output uses the effective input directory name plus `_md`.
- Manifest mode derives default output from the manifest-containing directory.
- Explicit `--output` is the exact destination.
- Missing/empty output receives `.csharp2md-output`.
- Marked output regenerates without force.
- Non-empty unmarked output is unchanged and requires `--force`.
- `--force` replaces unmarked output without prompting and creates the marker.
- Filesystem roots, the input directory, and input ancestors cannot be cleared, even with force.
- Existing manifest errors and explicit manifest behavior remain intact.

