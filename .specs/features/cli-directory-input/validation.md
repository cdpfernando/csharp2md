# Optional Manifest and Directory CLI Validation

## Validation: PASS

**Date**: 2026-08-15
**Spec**: `.specs/features/cli-directory-input/spec.md`
**Diff range**: `3eed735..7f8d128` (`09bdddc`, `3ebc032`, `3cbfc7e`, `7f8d128`)
**Verifier**: independent sub-agent (author != verifier)

The implementation passed every build-level gate. All 18 requirements and every listed edge case now have spec-matching assertion evidence. Three targeted safety and error-contract mutants were killed.

## Commit Completion

| Commit | Scope | Status |
| --- | --- | --- |
| `09bdddc` | Safe output ownership and regeneration | Implemented; acceptance evidence reviewed |
| `3ebc032` | Manifest-equivalent direct input pipeline | Implemented; acceptance evidence reviewed |
| `3cbfc7e` | Optional CLI inputs and output derivation | Implemented; acceptance evidence reviewed |
| `7f8d128` | Root-input and zero-entry-manifest regression evidence | Implemented; both prior gaps closed |

## Spec-Anchored Acceptance Criteria

| Requirement | Spec-defined outcome | Exact assertion evidence | Result |
| --- | --- | --- | --- |
| CLI-01 | One positional directory is analyzed as one service root. | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:43` invokes the positional directory; `:45` `Assert.Equal(0, result.ExitCode)`; `:46` calls `AssertGenerated`, whose assertions at `:294-296` require the marker, root index, and service Markdown. The packaged path is also exercised at `tests/Csharp2Md.Core.Tests/Cli/PackagingSmokeTests.cs:54-57`: `Assert.True(runResult.ExitCode == 0, ...)` and `Assert.Contains("1 of 3 project(s) need attention", runResult.StandardOutput)`. | PASS |
| CLI-02 | No input arguments analyze the current working directory as one service root. | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:23` invokes with `string.Empty` and the input as working directory; `:25` `Assert.Equal(0, result.ExitCode)`; `:26` calls the artifact assertions at `:294-296`. | PASS |
| CLI-03 | A missing direct directory exits non-zero, identifies the path, and writes no output. | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:133-135`: `Assert.NotEqual(0, result.ExitCode)`, `Assert.Contains(missing, result.StandardError, StringComparison.Ordinal)`, and `Assert.False(Directory.Exists(expectedOutput))`. | PASS |
| CLI-04 | Positional directory plus `--manifest` exits non-zero, prints usage, and does not analyze/write output. | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:112-114`: `Assert.NotEqual(0, result.ExitCode)`, `Assert.Contains("Usage:", result.StandardError, StringComparison.Ordinal)`, and `Assert.False(Directory.Exists(output))`. | PASS |
| CLI-05 | Omitted output in direct mode is the sibling `<input-name>_md`. | `tests/Csharp2Md.Core.Tests/Output/OutputPathResolverTests.cs:17`: `Assert.Equal(Path.Combine(parent, "src_md"), output)`. Process coverage: `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:41-46` defines `src_md`, asserts exit `0`, and calls the artifact assertions at `:294-296`. | PASS |
| CLI-06 | Omitted output in manifest mode is the sibling `<manifest-directory-name>_md`. | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:64-69` defines `config_md`, invokes manifest-only mode, asserts `Assert.Equal(0, result.ExitCode)`, and calls the artifact assertions at `:294-296`. | PASS |
| CLI-07 | Explicit `--output` is the exact final directory and no input-name suffix is appended. | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:88-90`: `Assert.Equal(0, result.ExitCode)`, artifact assertions at `:294-296`, and `Assert.False(Directory.Exists(Path.Combine(output, "src_md")))`. | PASS |
| CLI-08 | Manifest-only mode preserves manifest loading, validation, and discovery behavior. | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:269-270`: `Assert.Equal(0, result.ExitCode)` and `Assert.Contains("1 of 3 project(s) need attention", result.StandardOutput)`. Full manifest artifact regression evidence is at `tests/Csharp2Md.Core.Tests/Cli/EndToEndTests.cs:33-53`, including exit `0`, exact document set, indexes, graph files, and run summary assertions. | PASS |
| CLI-09 | An existing file used as direct input exits non-zero, identifies the path, and writes no output. | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:155-157`: `Assert.NotEqual(0, result.ExitCode)`, `Assert.Contains(file, result.StandardError, StringComparison.Ordinal)`, and `Assert.False(Directory.Exists(expectedOutput))`. | PASS |
| CLI-10 | An unnamed input root cannot derive output; CLI exits non-zero, requires explicit `--output`, and writes nothing. | Pure derivation evidence: `tests/Csharp2Md.Core.Tests/Output/OutputPathResolverTests.cs:32` `Assert.Null(output)`. Process evidence added by `7f8d128`: `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:174` invokes the filesystem root without `--output`; `:176-178` assert `Assert.NotEqual(0, result.ExitCode)`, `Assert.Contains("--output", result.StandardError, StringComparison.Ordinal)`, and `Assert.Empty(Directory.GetFileSystemEntries(workspace))`. | PASS |
| CLI-11 | Missing, malformed, and zero-entry manifests retain non-zero behavior and write no output. | Missing: `tests/Csharp2Md.Core.Tests/Pipeline/AnalysisPipelineInvariantTests.cs:107-109` asserts failure, `ManifestErrorCode.FileMissing`, and absent output. Malformed: `:94-96` asserts failure, `ManifestErrorCode.MalformedJson`, and absent output; process evidence at `tests/Csharp2Md.Core.Tests/Cli/EndToEndTests.cs:120-122` asserts non-zero, an identified CLI error, and no output. Zero entries, added by `7f8d128`: `tests/Csharp2Md.Core.Tests/Pipeline/AnalysisPipelineInvariantTests.cs:120-122` assert failed result, exact `ManifestErrorCode.ZeroEntries`, and absent output. | PASS |
| CLI-12 | Missing or empty output is prepared with `.csharp2md-output`. | Missing output: `tests/Csharp2Md.Core.Tests/Output/OutputWriterTests.cs:106-107` asserts the directory and marker exist. Empty output: `:118-120` asserts the marker is the directory's only entry. Pipeline evidence: `tests/Csharp2Md.Core.Tests/Pipeline/AnalysisPipelineInvariantTests.cs:125-129` asserts success, marker and Markdown existence, and no manifest file. | PASS |
| CLI-13 | Marked non-empty output is replaced without force and the marker is recreated. | `tests/Csharp2Md.Core.Tests/Output/OutputWriterTests.cs:93-95`: `Assert.Equal([Path.Combine(output, ".csharp2md-output")], Directory.GetFileSystemEntries(output))`. The exact set excludes all stale content and includes the recreated marker. | PASS |
| CLI-14 | Non-empty unmarked output exits non-zero unchanged and identifies `--force`. | Unit evidence at `tests/Csharp2Md.Core.Tests/Output/OutputWriterTests.cs:131-136` asserts `OutputPreparationException`, `--force`, preserved bytes, and the exact unchanged entry set. Process evidence at `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:178-181` asserts non-zero, `--force`, preserved bytes, and exact entries. | PASS |
| CLI-15 | `--force` replaces non-empty unmarked output without prompting and creates the marker. | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:203-205` asserts exit `0`, removal of the old file, and calls artifact assertions at `:294-296`, including the marker. Unit evidence at `tests/Csharp2Md.Core.Tests/Output/OutputWriterTests.cs:149-152` asserts the old file is absent and the marker is the only entry after preparation. | PASS |
| CLI-16 | A filesystem-root output is rejected before file operations, independent of force. | Safe pure-path evidence, avoiding root deletion: `tests/Csharp2Md.Core.Tests/Output/OutputWriterTests.cs:160-162` calls `OutputWriter.ValidateSafety(root, _root)` and asserts `Assert.Contains("filesystem root", error, StringComparison.OrdinalIgnoreCase)`. `ValidateSafety` has no file operations, and `PrepareRun` invokes it before ownership/force handling. | PASS |
| CLI-17 | Output equal to or ancestral to input is rejected even with force and source content is preserved. | Equal path: `tests/Csharp2Md.Core.Tests/Output/OutputWriterTests.cs:172-175` asserts `OutputPreparationException` under `force: true` and exact source bytes. Ancestor: `:186-189` asserts the same. CLI-level assertions at `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:225-249` require non-zero exits, source existence, and the safety error for both cases. | PASS |
| CLI-18 | Unnecessary `--force` is accepted without changing the selected path or behavior. | `tests/Csharp2Md.Core.Tests/Output/OutputWriterTests.cs:198-201` prepares an empty explicitly selected output with `force: true`, then asserts the marker at that exact path and `Assert.False(Directory.Exists(Path.Combine(output, "input_md")))`. | PASS |

**Acceptance status**: 18/18 requirements fully evidence-backed. No spec-precision gaps were found.

## Edge Cases

| Edge case | Exact assertion evidence | Result |
| --- | --- | --- |
| Existing file used as direct input | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:155-157`: non-zero, path in stderr, derived output absent. | PASS |
| Input has no terminal name | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:176-178`: non-zero exit, `--output` in stderr, and empty working directory; `tests/Csharp2Md.Core.Tests/Output/OutputPathResolverTests.cs:32`: `Assert.Null(output)`. | PASS |
| Missing manifest | `tests/Csharp2Md.Core.Tests/Pipeline/AnalysisPipelineInvariantTests.cs:107-109`: failed result, `FileMissing`, output absent. | PASS |
| Malformed manifest | `tests/Csharp2Md.Core.Tests/Pipeline/AnalysisPipelineInvariantTests.cs:94-96`: failed result, `MalformedJson`, output absent; `tests/Csharp2Md.Core.Tests/Cli/EndToEndTests.cs:120-122`: non-zero, CLI error, no output. | PASS |
| Zero-entry manifest | `tests/Csharp2Md.Core.Tests/Pipeline/AnalysisPipelineInvariantTests.cs:120-122`: failed result, exact `ZeroEntries`, and output absent. | PASS |
| `--force` without a collision | `tests/Csharp2Md.Core.Tests/Output/OutputWriterTests.cs:200-201`: marker at the selected path and no derived nested path. | PASS |

## Build-Level Gate

| Gate | Result |
| --- | --- |
| `dotnet build csharp2md.slnx -c Release` | PASS: 0 errors; 6 SourceLink warnings caused by the repository having no configured remote. |
| `dotnet format csharp2md.slnx --verify-no-changes --no-restore` | PASS: exit code 0; workspace-load warning only. |
| `dotnet test csharp2md.slnx -c Release --no-build --no-restore` | PASS: 303 passed, 0 failed, 0 skipped, 303 total in 23 seconds. |

The recorded pre-feature baseline is 283 passing tests in `.specs/STATE.md`. The current suite has 303, a delta of +20. The two cases added by `7f8d128` account exactly for the increase from the previous 301-test validation. No tests or assertions were weakened in `3eed735..7f8d128`.

## Discrimination Sensor

| Mutation | Scratch location | Covering test and observed failure | Result |
| --- | --- | --- | --- |
| Weaken ancestor protection from `IsSameOrAncestor(output, input)` to equality-only `PathEquals(output, input)`. | Disposable detached worktree at `3cbfc7e`; production target `src/Csharp2Md.Core/Output/OutputWriter.cs:63`. | `tests/Csharp2Md.Core.Tests/Output/OutputWriterTests.cs:186` failed with `Assert.Throws() Failure: No exception was thrown`. Focused run: 0 passed, 1 failed. | KILLED |
| Remove the literal `--output` guidance from the unnamed-root CLI error. | Disposable detached worktree at `7f8d128`; production target `src/Csharp2Md.Cli/Program.cs:64`. | `tests/Csharp2Md.Core.Tests/Cli/CliArgumentValidationTests.cs:177` failed with `Assert.Contains() Failure: Sub-string not found`, expected `--output`. | KILLED |
| Return `MalformedJson` instead of `ZeroEntries` for an empty manifest. | Disposable detached worktree at `7f8d128`; production target `src/Csharp2Md.Core/Manifests/ManifestLoader.cs:31`. | `tests/Csharp2Md.Core.Tests/Pipeline/AnalysisPipelineInvariantTests.cs:121` failed with expected `ZeroEntries`, actual `MalformedJson`. | KILLED |

**Sensor depth**: lightweight, three targeted behavior mutations across the original and post-fix verification passes.
**Sensor result**: 3/3 killed.
**Isolation**: both disposable worktrees were removed. Real-tree `git status --porcelain=v1` before and after each sensor was identical, apart from this authorized validation report, with no mutation residue.

## Test and Code Quality

- Reviewed all 44 test methods / 52 concrete cases in the six specified test files. Every test contains meaningful outcome assertions; there are no assertion-free, trivial-only, self-referential, broad-exception, missing-await, ordering, or flakiness findings.
- Assertion coverage includes equality, Boolean, exception, string, collection, negative, state/side-effect, and structural/deep checks. Refusal tests assert both failure and preservation, not only exception presence.
- Production changes are small and cohesive: CLI selection, a path resolver, output safety, and the manifest-equivalent pipeline overload. They follow existing project structure and AD-001 through AD-004; no Roslyn API surface was changed.
- The diff is behavior-focused. `.testagent/plan.md` and `.testagent/research.md` are feature-specific evidence, not unrelated scope.
- `git diff --check 3eed735..3cbfc7e` reports one extra blank line at EOF in each `.testagent` Markdown file. This is a minor documentation-hygiene issue and does not affect behavior or gates.
- `PackagingSmokeTests` retains a pre-existing low-severity temp-directory cleanup omission. The feature changed its invocation mode, not its resource lifecycle.

## Ranked Gaps

None. The two prior acceptance-evidence gaps are closed by `7f8d128`. `git diff --check` still reports an extra EOF blank line in each feature-specific `.testagent` Markdown file; this is non-blocking documentation hygiene, not a behavioral or acceptance gap.

## Traceability Judgment

| Requirement | Previous status | Verifier status |
| --- | --- | --- |
| CLI-01 through CLI-18 | Implementing | Verified |

## Summary

**Overall**: Ready.

- **Spec-anchored check**: 18/18 fully matched; 0 evidence gaps; 0 spec-precision gaps.
- **Gate**: Release build, format verification, and 303-test suite all passed.
- **Sensor**: 3/3 targeted mutations killed across the two verifier passes; real-tree isolation confirmed.
- **Next step**: mark CLI-01 through CLI-18 verified. No production-code defect was observed.
