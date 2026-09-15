# Slopwatch gate

Run the gate with `.slopwatch\analyze.ps1`, or directly:

```
dotnet slopwatch analyze --exclude "**/artifacts/**" "**/.scratch/**" "**/fixtures/**" "**/obj/**" "**/bin/**"
```

Without the exclusions the scan reports 726 issues, 720 of them in `artifacts/analyze-out/**`
(third-party corpus source that this tool itself wrote as output) and 2 in a NuGet package vendored
under `.scratch/`. With them it reports only first-party findings.

## Why the exclusions are not in `config.json`

Slopwatch 0.4.2 reads `.slopwatch/config.json` (or `--config <path>`), but its config schema is
`suppressions` / `globalSuppressions` only -- there is no `exclude` key, and the `--exclude` patterns
are a CLI option that never reaches the config file. A config file therefore cannot keep a directory
out of the scan; it can only suppress a named rule, and only for the rules that consult the
suppression checker at all (`SW006`, for one, honours nothing but inline XML comments).

`--exclude` is a sequence option: pass every pattern after a single `--exclude`, not one flag each.

## Baseline

`baseline.json` exists but is empty, so `analyze` and `analyze --no-baseline` report the same set.
Prefer fixing findings over `--update-baseline`.
