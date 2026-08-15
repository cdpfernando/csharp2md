# CLI Directory Input Test Plan

## Phase 1: Output ownership

- Extend `OutputWriterTests` with missing, empty, marked, unmarked, forced, root, input-equal, and input-ancestor cases.
- Assert marker presence, exact retained/deleted files, typed failure messages, and pre/post directory snapshots where refusal is required.
- Run the focused `OutputWriterTests` VSTest filter, then the full test project before commit.

## Phase 2: Manifest-equivalent direct input

- Extend pipeline tests to pass an in-memory single-root manifest and prove the same discovery/output behavior without a manifest file.
- Run focused pipeline tests, then the full test project before commit.

## Phase 3: CLI contract

- Extend process tests for zero arguments, positional directory, invalid input, conflicting input modes, both default-output rules, explicit-output override, force behavior, and protected paths.
- Preserve existing manifest success/failure coverage and update obsolete required-argument expectations to the approved contract.
- Run all unit and integration tests plus the repository build/format gate.

