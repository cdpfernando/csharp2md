# Optional Manifest and Directory CLI Specification

## Problem Statement

The CLI currently requires both `--manifest` and `--output`, which adds ceremony for the common case of documenting a single codebase directory. Developers need to run `csharp2md` against an explicit directory or the current directory while retaining manifests for multi-root and override scenarios.

## Goals

- [ ] Allow a run from an explicit directory without creating a manifest.
- [ ] Allow a zero-argument run from the current working directory.
- [ ] Derive a safe, predictable sibling output directory when `--output` is omitted.
- [ ] Preserve manifest-based runs and explicit output selection.
- [ ] Prevent accidental deletion of directories not previously created by csharp2md.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Changing manifest JSON shape or discovery rules | Manifests remain supported with their existing behavior. |
| Automatically discovering multiple unrelated service roots outside the selected directory | Direct-directory mode represents one root; manifests remain the multi-root mechanism. |
| Changing generated Markdown or dependency analysis | This feature changes CLI input and output-path selection only. |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here - nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Syntax for an explicitly selected directory | `csharp2md [directory]`, using one optional positional argument | Keeps the common invocation short and leaves `--manifest` available for the advanced mode. | y |
| Relationship between positional directory and `--manifest` | They are mutually exclusive; supplying both is a usage error | Each mode defines a different source of service roots, so precedence would be surprising. | y |
| Effective input directory in manifest mode | The directory containing the manifest file | A manifest may contain multiple roots, so its own containing directory is the only single deterministic base for default output. | y |
| Default output naming | A sibling named `<effective-input-directory-name>_md` | Matches the requested `{diretorio_de_entrada}_md` convention and keeps generated content outside the analyzed tree. | y |
| Meaning of explicit `--output` | The value is the exact final output directory; csharp2md does not append `<input>_md` | Matches established CLI conventions and makes explicit configuration literal and predictable. | y |
| Ownership marker | Every successful output preparation creates `.csharp2md-output` at the output root | A persistent marker distinguishes generated output from an arbitrary user directory on later runs. | y |
| Existing non-empty unmarked output | Refuse by default; `--force` is the only override and never prompts | Protects interactive users while remaining deterministic in scripts and CI. | y |
| Absolute deletion guards | Filesystem roots, the effective input directory, and any ancestor of the effective input directory can never be cleared, including with `--force` | `--force` must not turn a path mistake into deletion of source or a broad filesystem tree. | y |
| Remaining implicit-requirement dimensions | Authentication, concurrency, retries, external dependencies, and new observability are N/A for this CLI path-resolution change | The feature performs one local run and introduces no users, shared writers, remote calls, or retryable operations. | y |

**Open questions:** none - the unconfirmed choices above are proposed defaults to confirm with this specification.

---

## User Stories

### P1: Run Directly from a Directory ⭐ MVP

**User Story**: As a developer, I want to run csharp2md from or against a codebase directory without creating a manifest so that the common workflow requires little configuration.

**Why P1**: This is the requested simplification and must work end to end without removing the advanced manifest workflow.

**Acceptance Criteria**:

1. WHEN the user supplies one positional directory argument without `--manifest` THEN the system SHALL analyze that directory as one manifest-equivalent service root.
2. WHEN the user supplies neither a positional directory nor `--manifest` THEN the system SHALL analyze the current working directory as one manifest-equivalent service root.
3. IF the effective direct-input directory does not exist THEN the system SHALL exit with a non-zero status code and print an error identifying that directory without writing output.
4. IF the user supplies both a positional directory and `--manifest` THEN the system SHALL exit with a non-zero status code and print usage guidance without running analysis.

**Independent Test**: From a temporary working directory containing a synthetic solution, run the packaged CLI with no arguments and with that directory as the positional argument; both runs exit zero and generate the expected Markdown artifacts.

### P1: Optional Output and Preserved Manifest Mode ⭐ MVP

**User Story**: As a developer, I want output placement to be automatic for simple runs while retaining explicit control and manifest support when needed.

**Why P1**: Directory input is not a low-ceremony workflow if an output path is still mandatory, and existing manifest users must not regress.

**Acceptance Criteria**:

1. WHEN `--output` is omitted in direct-directory mode THEN the system SHALL write output to a sibling of the effective input directory named `<effective-input-directory-name>_md`.
2. WHEN `--output` is omitted in manifest mode THEN the system SHALL write output to a sibling of the manifest's containing directory named `<manifest-containing-directory-name>_md`.
3. WHEN the user supplies `--output <directory>` THEN the system SHALL write output to that directory instead of deriving a default.
4. WHEN the user supplies `--manifest <file>` without a positional directory THEN the system SHALL load and analyze that manifest using the existing manifest validation and discovery behavior.

**Independent Test**: Run each input mode without `--output` and verify the exact derived sibling path, then run with an explicit output path and verify that it overrides the default.

### P1: Safe Output Regeneration ⭐ MVP

**User Story**: As a developer, I want csharp2md to distinguish its own generated directories from my existing directories so that a mistaken output path does not silently delete unrelated files.

**Why P1**: The pipeline regenerates its output directory in full, making ownership checks essential whenever output can be selected explicitly.

**Acceptance Criteria**:

1. WHEN the output directory does not exist or is empty THEN the system SHALL prepare it and create a `.csharp2md-output` ownership marker at its root.
2. WHEN the output directory is non-empty and contains `.csharp2md-output` at its root THEN the system SHALL replace its prior generated contents without requiring `--force` and recreate the marker.
3. IF the output directory is non-empty and does not contain `.csharp2md-output` at its root THEN the system SHALL exit non-zero without changing that directory and identify `--force` as the explicit override.
4. WHEN the user supplies `--force` for a non-empty unmarked output directory THEN the system SHALL replace its contents without prompting and create `.csharp2md-output` at its root.
5. IF the resolved output directory is a filesystem root THEN the system SHALL exit non-zero without changing it, regardless of `--force`.
6. IF the resolved output directory equals or is an ancestor of the effective input directory THEN the system SHALL exit non-zero without changing it, regardless of `--force`.

**Independent Test**: Exercise empty, marked, unmarked, forced, filesystem-root, input-equal, and input-ancestor output directories; verify exact exit behavior, marker lifecycle, and that every refused directory remains byte-for-byte unchanged.

---

## Edge Cases

- IF a direct-input path names an existing file rather than a directory THEN the system SHALL exit non-zero and identify that path as an invalid input directory.
- IF the effective input directory has no terminal directory name from which to derive `<name>_md` THEN the system SHALL exit non-zero and require an explicit `--output` directory.
- IF a manifest is missing, malformed, or contains zero entries THEN the system SHALL preserve the existing non-zero exit behavior without writing output.
- IF `--force` is supplied without an output collision that requires overriding THEN the system SHALL accept the option without changing the selected output path or analysis behavior.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| CLI-01 | P1: explicit directory | Execute | Implementing |
| CLI-02 | P1: current directory | Execute | Implementing |
| CLI-03 | P1: missing direct directory | Execute | Implementing |
| CLI-04 | P1: mutually exclusive inputs | Execute | Implementing |
| CLI-05 | P1: default direct output | Execute | Implementing |
| CLI-06 | P1: default manifest output | Execute | Implementing |
| CLI-07 | P1: explicit output override | Execute | Implementing |
| CLI-08 | P1: preserved manifest mode | Execute | Implementing |
| CLI-09 | Edge case: file as direct input | Execute | Implementing |
| CLI-10 | Edge case: unnamed input root | Execute | Implementing |
| CLI-11 | Edge case: invalid manifest | Execute | Implementing |
| CLI-12 | P1: create ownership marker | Execute | Implementing |
| CLI-13 | P1: regenerate marked output | Execute | Implementing |
| CLI-14 | P1: refuse unmarked output | Execute | Implementing |
| CLI-15 | P1: force unmarked output | Execute | Implementing |
| CLI-16 | P1: protect filesystem root | Execute | Implementing |
| CLI-17 | P1: protect input and ancestors | Execute | Implementing |
| CLI-18 | Edge case: unnecessary force | Execute | Implementing |

**Coverage:** 18 total, 0 mapped to formal tasks (tasks phase skipped for medium scope), 18 mapped directly to Execute.

---

## Success Criteria

- [ ] `csharp2md`, `csharp2md <directory>`, and `csharp2md --manifest <file>` each complete successfully for valid inputs without requiring `--output`.
- [ ] Existing manifest behavior and explicit `--output` behavior remain covered by automated tests.
- [ ] Invalid or conflicting input modes fail before generated output is written.
- [ ] Re-running against csharp2md-owned output succeeds without interaction, while non-empty unowned output requires `--force`.
- [ ] No invocation can clear the input directory, an input ancestor, or a filesystem root.
