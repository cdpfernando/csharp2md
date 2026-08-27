# Multi-Solution Composition Validation

**Date**: 2026-08-27
**Spec**: `.specs/features/multi-solution-composition/spec.md`
**Diff range**: `f4360d3^..HEAD` (`f4360d3` … `7c2c7dc`)
**Verifier**: independent sub-agent (author ≠ verifier)
**Result**: PASS

Re-verify iteration 2 of 3 after F1 closed MSC-39. Re-derived from spec.md MSC-01..40. Did not inherit the iteration-1 FAIL report, worker summaries, or task tags as coverage.

T1–T28 each have a Conventional Commit on `feature/multi-solution-composition` (`f4360d3` … `228fb8e`). F1 is `7c2c7dc`. Implementation starts at T1 `f4360d3`. HEAD is F1 `7c2c7dc`. `tasks.md` Done-when boxes are all checked, including F1. Header still says “Draft”; the checkboxes and commits are the completion evidence.

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1 | Done | `f4360d3` `feat(analysis): add SolutionCoordinate as the single solution identity rule` |
| T2 | Done | `6ccc825` `refactor(analysis): derive engine solution identity from SolutionCoordinate` |
| T3 | Done | `0096ee9` `refactor(analysis): derive inventory solution identity from SolutionCoordinate` |
| T4 | Done | `ffdf422` `feat(analysis): accept a solution coordinate on the transactional store port` |
| T5 | Done | `f52125c` `feat(storage): derive the package directory from the solution identity` |
| T6 | Done | `98a44a0` `fix(storage): publish the solution identity as solution_key in both stores` |
| T7 | Done | `f9657bb` `feat(storage): define the bounded solution contribution contract` |
| T8 | Done | `02e2f59` `feat(analysis): add batch publication to the transactional store port` |
| T9 | Done | `4821e77` `feat(storage): declare the batch composer port` |
| T10 | Done | `c3c96cc` `feat(projection): extract a bounded contribution from the published view` |
| T11 | Done | `6a028ed` `feat(storage): accumulate solution contributions after a successful replace` |
| T12 | Done | `629bb20` `feat(projection): correlate cross-solution messaging operations` |
| T13 | Done | `5fb1611` `feat(projection): correlate contracts shared across solutions` |
| T14 | Done | `f595e33` `feat(projection): correlate cross-solution HTTP operations as candidates` |
| T15 | Done | `16b288e` `feat(projection): publish a grouped global component catalog` |
| T16 | Done | `ac0828c` `feat(projection): publish a grouped global external system catalog` |
| T17 | Done | `c6f110e` `feat(projection): assemble, order and shard the batch composition` |
| T18 | Done | `d269e2a` `feat(storage): validate batch composition entries before publication` |
| T19 | Done | `8d2d8e5` `feat(storage): build the batch manifest` |
| T20 | Done | `5d9f98c` `feat(storage): publish the batch manifest and composition from the filesystem store` |
| T21 | Done | `3e10697` `feat(storage): publish the batch from the in-memory store` |
| T22 | Done | `c17c859` `feat(analysis): publish a batch after the per-solution loop` |
| T23 | Done | `04717c6` `feat(cli): report batch publication failure` |
| T24 | Done | `7a992a6` `test(fixtures): add the Acme.Shipping solution for cross-solution correlation` |
| T25 | Done | `eb6edab` `test(analysis): prove cross-solution correlations over the fixture batch` |
| T26 | Done | `d66fe59` `test(analysis): prove batch output is clone-path and argument-order independent` |
| T27 | Done | `0c9b286` `test(analysis): prove batching leaves per-solution packages untouched` |
| T28 | Done | `228fb8e` `test(analysis): prove partial and fully failed batch behaviour` |
| F1 | Done | `7c2c7dc` `fix(storage): treat a missing package directory as unpublished in the batch manifest` |

No blocked or partial tasks. T11 tests still tagged MSC-39 assert accumulator emptiness after Abort or a blocked Commit; they are the registration gate, not the publish-time edge case. This report cites F1’s `PublishBatch` test for MSC-39.

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| Publish `batch-manifest.json` at the output root (MSC-01) | File exists at root after `PublishBatch` / `AnalyzeAsync` | `FilesystemPublishBatchTests.cs:26-27` `File.Exists(manifestPath)`; `InMemoryPublishBatchTests.cs:31` SequenceEqual disk vs memory manifest | PASS |
| Package directory from identity, never absolute path (MSC-02) | Same file name under two parents → one dir; dir ≠ `sha256(absolute path)` | `FilesystemIdentityDirectoryTests.cs:31-39` `Assert.Equal(nameAfterLeft, nameAfterRight)` + `Assert.NotEqual` path hashes; `SolutionCoordinateTests.cs:15-19` identity equal, clone parent absent | PASS |
| One entry per requested solution: identity, file name, package directory, `committed`/`unpublished` (MSC-03) | Those four fields | `BatchManifestBuilderTests.cs:20-26` Identity, SolutionFileName, PackageDirectory `s-`+32 hex, Status `committed`; `:40-41` unpublished; `BatchPublicationEngineTests.cs:30-41` both records | PASS |
| Order solution entries by identity, ordinal ascending (MSC-04) | Alpha identity before Zeta | `BatchManifestBuilderTests.cs:56-60` Equal `[alpha.Identity, zeta.Identity]` + Ordinal Compare < 0 | PASS |
| Same solutions from two parent directories → identical package names and byte-identical `batch-manifest.json` (MSC-05) | Equal names; equal batch-layer bytes | `BatchDeterminismTests.cs:25-31` `Assert.Equal(namesA, namesB)` + `AssertEqualSnapshots(BatchLayer…)` | PASS |
| Different `--solution` order → byte-identical batch and composition (MSC-06) | SequenceEqual batch-layer | `BatchDeterminismTests.cs:57` `AssertEqualSnapshots(forward, reversed)` | PASS |
| `solution_key` is the identity, identical from both stores (MSC-07) | Equal keys; not rooted | `SolutionKeyPublicationTests.cs:33-36` Equal expected/memory/filesystem, `IsPathRooted` false | PASS |
| N=1 → one entry and no cross-solution relation, shared-contract, or correlation-candidate (MSC-08) | Single file name; those three keys absent from files and manifest | `BatchIsolationTests.cs:47-58` Single `Acme.Orders.slnx`, `ContainsKey` false, `DoesNotContain` those CanonicalKeys | PASS |
| Unpublished entry carries failing stage; committed packages unmodified (MSC-09) | Status `unpublished`, stage name, package bytes equal | `PartialBatchTests.cs:36-44` `AssertEqualSnapshots` + unpublished `Acme.Broken.slnx` FailingStage `Inventory`; `BatchPublicationEngineTests.cs:73-76` stage + `AssertEqualSnapshots` before/after batch | PASS |
| Any unpublished → `complete: false`, `incomplete_scope_reason: solution-unpublished` (MSC-10) | Those two field values | `PartialBatchTests.cs:39-40` `Assert.False(Complete)` + Equal reason; `BatchManifestBuilderTests.cs:80-85` same on JSON | PASS |
| Every solution committed → `complete: true`, no `incomplete_scope_reason` (MSC-11) | Complete true; key omitted | `BatchManifestBuilderTests.cs:96-101` `Assert.True` + `DoesNotContain("incomplete_scope_reason")`; `CrossSolutionCorrelationTests.cs:26` `Assert.True(manifest.Complete)` | PASS |
| Two requested paths with the same identity → reject before analysis, message names both paths (MSC-12) | `ArgumentException`; both paths; `OpenCount == 0` | `DuplicateSolutionIdTests.cs:22-27` ThrowsAsync + Contains both paths + Equal 0; `AnalyzeInvocationErrorTests.cs:89-91` exit 1 + both paths | PASS |
| Unmapped package directory left unmodified and unreferenced (MSC-13) | Snapshot equal; name absent from manifest | `FilesystemPublishBatchTests.cs:75-80` `AssertEqualSnapshots` + `DoesNotContain` stale dir | PASS |
| Earlier `batch-manifest.json` replaced, not merged (MSC-14) | `"stale"` gone; current identity present | `FilesystemPublishBatchTests.cs:53-57` `DoesNotContain("stale")` + Complete + Equal identity | PASS |
| Batch write failure leaves committed packages unmodified and reports non-zero exit (MSC-15) | Packages SequenceEqual; gate set; exit 2 | `FilesystemPublishBatchTests.cs:97-98` Gate `batch-composition` + snapshots; `:116-117` Gate `io`; `AnalyzeBatchFailureTests.cs:24-26` Equal 2 + Contains `batch-composition`; `CommandFactory.cs:78` exit 2 on batch failure | PASS |
| Equal messaging protocol keys across solutions → one `targets` relation (MSC-16) | Kind `targets`; matched key contains `OrderPlaced` | `MessagingCorrelatorTests.cs:27-38` Single + Equal `targets` + matched key; `CrossSolutionCorrelationTests.cs:28-31` Single + `targets` + Contains `OrderPlaced` | PASS |
| Cross-solution relation carries both fact ids, both solution identities, both artifact keys and ordinals, and the matched key (MSC-17) | Those nine fields on value | `MessagingCorrelatorTests.cs:29-38` Equal each field; `CrossSolutionCorrelationTests.cs:43-60` cited ordinals resolve to those fact ids | PASS |
| One outbound matching inbound in two other solutions → one entry per pair (MSC-18) | Length 2; both target facts | `MessagingCorrelatorTests.cs:57-67` Equal 2 + Contains catalog and billing targets | PASS |
| No cross-solution relation whose source and target are the same solution (MSC-19) | Empty relations | `MessagingCorrelatorTests.cs:82-83` `Assert.Empty` Relations and Candidates | PASS |
| Unmatched outbound messaging → no relation and no candidate (MSC-20) | Both empty | `MessagingCorrelatorTests.cs:99-100` `Assert.Empty` | PASS |
| Contract identity in two+ solutions → one `shared-contracts.json` entry with per-owner identity, key, ordinal (MSC-21) | Single entry; two owners with those fields | `ContractCorrelatorTests.cs:22-24` Single + Equal fact id + owners Length 2; `:40-49` Contains each owner key/ordinal; `CrossSolutionCorrelationTests.cs:79-97` two owners, cited proof contains `OrderPlaced` | PASS |
| HTTP method + space + route equals inbound key → one correlation candidate with destination scope (MSC-22) | Both facts, both solutions, matched key, scope | `HttpCorrelatorTests.cs:26-32` Equal those six fields; `CrossSolutionCorrelationTests.cs:34-37` Single + Equal `POST shipments` + `ShippingService` | PASS |
| HTTP pairing never a `cross-solution-relations.json` entry (MSC-23) | Relations empty for HTTP contributions | `HttpCorrelatorTests.cs:50-53` Empty `http.Relations` and `messaging.Relations`; e2e single relation is messaging `targets` (`CrossSolutionCorrelationTests.cs:30-32`) | PASS |
| Composition writes no fact/observation/confirmed/candidate/quarantine into any package (MSC-24) | Solo vs batch package bytes equal; knowledge keys equal | `BatchIsolationTests.cs:22-28` `AssertEqualSnapshots` + Equal `KnowledgeKeys` | PASS |
| Composition reads only the bounded contribution; no symbol, observation, confirmed, candidate, or source byte (MSC-25) | Forbidden types unreachable; Contribute copies listed fields | `SolutionContributionBoundTests.cs:47-56` hits Length 0; `:63-80` property types; `BatchComposerContributeTests.cs:64-68` locator Equals `TryLocate`; `:122-128` direction/protocol/key/scope/http/route | PASS |
| Order relations and candidates by source solution, source fact, target solution, target fact (MSC-26) | SequenceEqual that OrderBy | `BatchComposerComposeTests.cs:43-44` `AssertOrder`; `:147-161` Equal ordered tuple | PASS |
| Order `shared-contracts.json` by contract identity; owners by solution identity (MSC-27) | Owners alpha then zeta | `ContractCorrelatorTests.cs:65-67` Equal `["solution-alpha", "solution-zeta"]`. **⚠️ Spec-precision**: no test with two contract identities asserts entry order; `BatchComposer.cs:73-75` passes `ContractFactId` to `ShardWriter.Write`, which `OrderBy` FactId (`ShardWriter.cs:28`). Reorder byte-identity is `BatchComposerComposeTests.cs:139-144` | PASS (spec-precision: see note) |
| Over-ceiling composition artifact splits with the same shard rule (MSC-28) | Keys `composition/cross-solution-relations.[0-9a-f]{2}.json` | `BatchComposerComposeTests.cs:65-73` unsplit key absent, shards Length > 1, `Assert.Matches` regex | PASS |
| Zero-entry artifact is not published and not referenced from the batch manifest (MSC-29) | Compose omits keys; N=1 manifest omits them | `BatchComposerComposeTests.cs:89-101` `DoesNotContain` empty families; `BatchIsolationTests.cs:50-58` files and `manifest.Artifacts` omit those three keys | PASS |
| Global component catalog lists every component and DU with fact id, owning solution, artifact key, ordinal (MSC-30) | Four entries with those fields | `GlobalComponentCatalogTests.cs:24-44` Length 4 + Contains each quadruple | PASS |
| Group by canonical name; groups by name, entries by solution identity (MSC-31) | `["alpha-api", "zeta-api"]`; entries alpha then zeta | `GlobalComponentCatalogTests.cs:62-69` Equal names and entry identities | PASS |
| Name from two+ solutions → `shared_identity: not-proven` (MSC-32) | One group, `not-proven`, two entries | `GlobalComponentCatalogTests.cs:83-87` Equal name, Equal `NamedIdentityGrouping.NotProven`, Length 2. **⚠️ Spec-precision**: Independent Test asked for two fixture DUs named `ordering-api`; this is synthetic contributions, not the Orders/Shipping fixture | PASS (spec-precision: see note) |
| Do not merge two component or DU identities into one global identity (MSC-33) | Output entry count equals input; three distinct fact ids | `GlobalComponentCatalogTests.cs:123-125` Equal inputCount/outputCount + Distinct Count 3 | PASS |
| External systems in the same grouped unmerged form (MSC-34) | Same grouping helper; two-solution name → `not-proven` | `GlobalExternalSystemCatalogTests.cs:25-32` Equal names/shared/entries; `:58-62` `NotProven` + Length 2 | PASS |
| Every solution unpublished → `complete: false`, all unpublished, no composition artifact (MSC-35) | Those statuses; no `composition/` | `PartialBatchTests.cs:76-86` False Complete, All unpublished, Artifacts empty, `Directory.Exists(composition)` false; `BatchPublicationEngineTests.cs:111-117` same | PASS |
| Committed contribution with no catalog facts → batch-manifest, omit empty composition (MSC-36) | Compose returns empty | `BatchComposerComposeTests.cs:110-111` `Assert.Empty` Compose. Manifest publication for empty fragments is `FilesystemPublishBatchTests.cs:159-160` (no composer) and MSC-01 | PASS |
| Same contract fact identity in two solutions → one shared-contract entry (MSC-37) | `Assert.Single` | `ContractCorrelatorTests.cs:22-24` Single entry, owners Length 2 | PASS |
| Same-solution outbound+inbound messaging with equal key → no composition entry (MSC-38) | Empty relations and candidates | `MessagingCorrelatorTests.cs:82-83` `Assert.Empty` | PASS |
| Contribution present but package directory absent from the output root → unpublished in `batch-manifest.json` (MSC-39) | Manifest entry status `unpublished` | `FilesystemPublishBatchTests.cs:175-187` `Directory.Delete` of `s-*` then `Assert.Equal("unpublished", unpublished.Status)`, `Assert.False(envelope.Complete)`, `Assert.Equal("solution-unpublished", envelope.IncompleteScopeReason)` on published `batch-manifest.json` | PASS |
| Missing output root is created and receives the batch (MSC-40) | Directory exists; `batch-manifest.json` exists | `FilesystemPublishBatchTests.cs:148-161` `Assert.False` then `Assert.True` both | PASS |

**Status**: All numbered ACs covered. 40/40 matched spec-defined outcomes. 2 spec-precision items (MSC-27 entry order; MSC-32 Independent Test fixture). No uncovered AC.

MSC-39: F1 deletes the committed Orders `s-*` directory (`FilesystemPublishBatchTests.cs:174-176`), calls `PublishBatch` with the still-selected contribution (`:178`), then reads `batch-manifest.json` and asserts `unpublished` (`:185`), `complete: false` (`:186`), and `incomplete_scope_reason: solution-unpublished` (`:187`). Sibling Payments stays `committed` and byte-identical (`:192-193`); composition does not cite the missing identity (`:198`). Production path: `FilesystemTransactionalStore.PackagePresent` (`FilesystemTransactionalStore.cs:261-265`) plus `BatchPublication.ResolveContributions` rewrite (`BatchPublication.cs:46-49`). T11 accumulator-empty tests are not this evidence. No `// SPEC_DEVIATION`.

MSC-27 spec-precision: owner order is asserted on value with two identities. Multi-entry order by contract identity is not asserted with two distinct contract ids. Core deterministic order holds via ShardWriter + reorder byte-identity.

MSC-32 spec-precision: numbered AC (`shared_identity: not-proven`) is asserted on synthetic DUs named `ordering-api`. The Independent Test’s two-fixture `ordering-api` pair is not.

Payload/conjunction: relation, candidate, shared-contract, catalog, and batch-manifest fields are asserted on value/state (fact ids, keys, ordinals, matched keys, status, complete, reason). Not mere method-call occurrence. MSC-10/MSC-11/MSC-39 assert `complete` and `incomplete_scope_reason` on the serialized JSON, not only that a property exists.

Independent Tests traced: clone-path batch bytes (`BatchDeterminismTests`); Orders+Shipping messaging pair and HTTP candidate (`CrossSolutionCorrelationTests`); synthetic two-solution `ordering-api` group (`GlobalComponentCatalogTests`, not the fixture Independent Test).

Worker notes, investigated independently:

- ContractPass inbound-only emission lets Shipping contribute `OrderPlaced` without a second outbound pair. MSC-21 e2e still asserts one shared entry with two owners and resolving locators (`CrossSolutionCorrelationTests.cs:79-97`). Fixture/classifier technique; not a SPEC_DEVIATION.
- T28 unpublished sibling is an unreadable `Acme.Broken.slnx` (`PartialBatchTests.cs:114-118` writes `"<Solution>"`). Failing stage is `Inventory` (`:43`). Spec names status + failing stage, not a particular SDK failure.

---

## Discrimination Sensor

| Mutation | File:line | Description | Killed? |
| -------- | --------- | ----------- | ------- |
| — | — | Not run | SKIPPED |

**Sensor depth**: skipped
**Result**: SKIPPED (standing user request for csharp2md, same as `symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`, `knowledge-taxonomy-contract`, `engine-bootstrap`, `factual-storage`, `roslyn-observation-extraction`, `entrypoints-boundaries-contracts`, `call-linking-flow-frontiers`, `persistence-knowledge`, `components-deployments-configuration`, and `retrieval-projections`). No git worktree, no file mutation, no Stryker. Standing skip through this feature.

Static gap analysis only (`dotnet-test:test-gap-analysis` step 4 without 4b live mutation; labelled unverified):

- Flipping `BatchView.Complete` so unpublished records still count as complete would fail `BatchComposerPortTests.cs:56` and `BatchManifestBuilderTests.cs:80`. Unverified (static reasoning).
- Removing the same-solution `continue` in `MessagingCorrelator` would fail `MessagingCorrelatorTests.cs:82`. Unverified (static reasoning).
- Returning HTTP pairs as `Relations` instead of an empty array in `HttpCorrelator` would fail `HttpCorrelatorTests.cs:51`. Unverified (static reasoning).
- Deriving the package directory from `sha256(absolute path)` again would fail `FilesystemIdentityDirectoryTests.cs:34-39`. Unverified (static reasoning).
- Omitting `PackagePresent` / always returning true would leave a deleted `s-*` as `committed`; F1 `FilesystemPublishBatchTests.cs:185` `Assert.Equal("unpublished", unpublished.Status)` would fail. Unverified (static reasoning). The iteration-1 MSC-39 survivor class is now killed on paper by F1.

---

## Interactive UAT Results

Not performed. This feature is backend/CLI infrastructure; automated checks are sufficient per validate.md.

---

## Code Quality

| Principle | Status |
| --------- | ------ |
| Minimum code | PASS |
| Surgical changes | PASS |
| No scope creep | PASS (ContractPass inbound-only is a classifier expansion needed for the Shipping shared-contract fixture; no new CLI flag. F1 is a `PackagePresent` callback plus a status rewrite) |
| Matches patterns | PASS |
| Spec-anchored outcome check (asserted values match spec) | PASS (MSC-27/MSC-32 flagged) |
| Per-layer Coverage Expectation met (domain identity + Storage batch write + Projection correlators 1:1 ACs; Analysis engine/e2e; CLI exit; F1 publish-time missing-directory path) | PASS |
| Every test maps to a spec requirement - no unclaimed tests | PASS |
| Documented guidelines followed: `AGENTS.md` / `CLAUDE.md` (net10.0, Workspaces.MSBuild 5.6.0, no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults`, SyntheticSolution-only versioned fixture, LocalCorpus skip when clones absent, standing sensor skip) | PASS |

Diff `f4360d3^..HEAD` stays on `SolutionCoordinate`, identity-derived `s-*` directories, `solution_key`, `SolutionContribution` / `IBatchComposer` / `PublishBatch`, correlators, global catalogs, batch validation/manifest/stores, engine loop, CLI exit 2, Acme.Shipping fixture, e2e tests, and F1’s missing-directory rewrite. No new analyze flag (`AnalyzeBatchFailureTests.cs:80` Equal `["--output", "--solution"]`). No `Microsoft.Build.*` and no `MSBuildLocator`. Target framework `net10.0`.

Spot-check (P2 messaging + HTTP + MSC-39): MSC-16/17 assert kind, both identities, both locators and matched key on value. MSC-22/23 e2e assert `POST shipments` candidate and a single messaging `targets` relation. MSC-39 asserts `unpublished` / `complete: false` / `solution-unpublished` on the published envelope. Not trait-only.

`dotnet-test:assertion-quality` / `test-anti-patterns` on new Composition, batch, CLI, and F1 tests: no assertion-free tests; no `Thread.Sleep`; no always-true asserts; no swallowed exceptions. Equality + collection + negative + exception asserts are the dominant mix. `MessagingCorrelatorTests.cs:115-120` and `HttpCorrelatorTests.cs:104-109` couple to source text (Low; same pattern as CDC/RP `IndexOf`). Giant AAA is `CrossSolutionCorrelationTests.Analyze_OrdersAndShipping_…` (one scenario, payload-rich). F1 test is one scenario with status, completeness, sibling snapshot, and composition isolation asserts.

---

## Edge Cases

- [x] Every requested solution unpublished → incomplete manifest, no composition (MSC-35) — `PartialBatchTests.cs:76-86`
- [x] Committed contribution with no catalog facts omits empty composition (MSC-36) — `BatchComposerComposeTests.cs:110-111`
- [x] Same contract fact identity → one shared-contract entry (MSC-37) — `ContractCorrelatorTests.cs:22-24`
- [x] Same-solution messaging pair → no composition entry (MSC-38) — `MessagingCorrelatorTests.cs:82-83`
- [x] Contribution whose package directory is absent from the output root → unpublished (MSC-39) — `FilesystemPublishBatchTests.cs:175-187`
- [x] Missing output root is created (MSC-40) — `FilesystemPublishBatchTests.cs:148-161`

---

## Gate Check

- **Gate command**: `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- **Result**: 1723 passed, 0 failed, 0 skipped among selected tests
- **Per-project passed counts**:
  - `Csharp2Md.Domain.Tests`: 555 passed
  - `Csharp2Md.Analysis.Tests`: 662 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Storage.Tests`: 289 passed
  - `Csharp2Md.Cli.Tests`: 33 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Projection.Tests`: 184 passed
- **Test count before feature**: 1617
- **Test count after feature**: 1723 passing (0 failing on the serial gate)
- **Delta**: +106 executed tests vs the 1617 baseline. F1 baseline 1723; no drop.
- **Skipped tests**: none among the filtered gate. `LocalCorpusAnalyzeTests` excluded by `Category!=LocalCorpus`.
- **Failures**: none on the serial Category!=LocalCorpus gate.
- **Build**: `dotnet build` 0 warnings, 0 errors (`TreatWarningsAsErrors` on).

---

## LocalCorpus

`fixtures/eShop` is absent. `fixtures/eShopOnContainers` exists as a directory (`ApiGateways`, `BuildingBlocks`, `Services`, …) but does not contain `eShopOnContainers-ServicesAndWebApps.sln`.

Post-gate `dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category=LocalCorpus"`: both theory rows threw `$XunitDynamicSkip$` (`LocalCorpusAnalyzeTests.cs:19`). This runner reported them as failed `InvalidOperationException` rather than skipped. Neither corpus was analyzed. Not a feature FAIL. Clones were not added to git.

---

## Fix Plans

None. MSC-39 is covered by F1. Remaining items are spec-precision only (MSC-27 multi-entry contract order; MSC-32 Independent Test fixture names).

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| ----------- | --------------- | ---------- |
| MSC-01..MSC-38, MSC-40 | Verified | Verified in this report |
| MSC-39 | Implementing | Verified |

---

## Summary

**Overall**: Ready

**Spec-anchored check**: 40/40 ACs matched spec outcome | 2 spec-precision gaps flagged
**Sensor**: SKIPPED (standing skip)
**Gate**: 1723 passed

**What works**: Clone-independent `s-*` directories and `solution_key`, `batch-manifest.json` for N=1 and N>1, proven messaging `targets` plus HTTP candidates, shared contracts, grouped catalogs with `shared_identity: not-proven`, partial-batch `complete: false` without touching committed packages, CLI exit 2 on batch failure, missing package directory published as `unpublished`.

**Issues found**: none that block. MSC-27 entry order and the MSC-32 Independent Test fixture remain spec-precision only (already recorded as L-018 / L-019).

**Next steps**: `validate_state.py` then commit this report and MSC-39 Verified. No new lessons: remaining precision gaps are already in `.specs/lessons.json`.
