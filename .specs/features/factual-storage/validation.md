# Factual Storage Validation

## Validation: factual-storage - PASS ✅

**Date**: 2026-08-25
**Spec**: `.specs/features/factual-storage/spec.md`
**Diff range**: `e614a6b..4c4948b` (T1 is `e614a6b`; T52 is `341ba6e`; post-T52 fix `4c4948b` retries Windows staging swap on sharing violations; 53 commits on `feat/factual-storage`)
**Verifier**: independent sub-agent (author ≠ verifier)

---

## Task Completion

All 52 tasks in `tasks.md` (T1–T52) have every "Done when" line checked `[x]`. No task is partial or blocked. `git log --oneline e614a6b^..341ba6e` lists 52 commits whose messages match each task's `**Commit**:` field. Inclusive range `e614a6b^..4c4948b` adds one post-T52 commit that is not a `tasks.md` row.

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1–T52 | ✅ Done | All "Done when" items checked; commit present for each |
| post-T52 `4c4948b` | ✅ Done | `fix(storage): retry windows staging swap on sharing violations` — retries `Directory.Move` on `IOException`/`UnauthorizedAccessException`; serializes filesystem tests in an xUnit collection; `PublicationRejectedException.Message` is `gate: detail` |

---

## Spec-Anchored Acceptance Criteria

Evidence-or-zero: every row cites `file:line` + the concrete assertion. Spec-defined outcomes were re-derived from `spec.md`, not from `tasks.md` comments or the previous FAIL report. This pass's Gate Check ran `Csharp2Md.Storage.Tests` to **140 passed, 0 failed**, including the filesystem commit+read cases the previous pass treated as missing unresolved support.

### P1: Versioned wire contracts for every Domain family

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STOR-01 Storage publishes a JSON Schema for each of the 17 registered fact types | One committed schema file per registered fact type | `tests/Csharp2Md.Storage.Tests/Schema/JsonSchemaDriftTests.cs:13-20` — `Assert.Equal(17, factTypes.Length)`; loop `Assert.True(File.Exists(...), $"Missing JSON Schema for registered fact type '{factType.Name}'...")`. `FactRoundTripTests.cs:81-89` round-trips each fixture through Domain equality | ✅ PASS |
| STOR-02 Storage publishes a JSON Schema for each of the 10 registered observation kinds | One committed schema file per registered wire name | `JsonSchemaDriftTests.cs:29-36` — `Assert.Equal(10, kinds.Length)`; loop `Assert.True(File.Exists(...), $"Missing JSON Schema for observation kind '{kind.WireName}'...")`. `ObservationRelationRoundTripTests.cs:46-51` — wire name on DTO, `AssertObservationEqual` after `FromWire` | ✅ PASS |
| STOR-03 Storage publishes JSON Schemas for ConfirmedRelation, CandidateLink, UnresolvedRecord and OpenFrontier | Schema files for those four record names | `JsonSchemaDriftTests.cs:44-49` — loop over `JsonSchemaEmitter.RelationRecordNames` (`ConfirmedRelation`, `CandidateLink`, `UnresolvedRecord`, `OpenFrontier`); `Assert.True(File.Exists(...))`. Round-trips: `ObservationRelationRoundTripTests.cs:70` `Assert.Equal(relation, Assert.Single(restored.ConfirmedRelations))`; `:88` candidates; `:108` unresolved; `:122` frontiers | ✅ PASS |
| STOR-04 Storage publishes JSON Schemas for manifest, coverage, run_certification, diagnostics, quarantine and measurements | Schema files for those six envelopes | `JsonSchemaDriftTests.cs:57-62` — loop over `JsonSchemaEmitter.EnvelopeNames`; `Assert.True(File.Exists(...), $"Missing JSON Schema for envelope '{envelope}'...")` | ✅ PASS |
| STOR-05 WHEN a package is committed THEN it contains a copy of `contracts/taxonomy-registry.json` whose bytes equal the committed registry file | Package copy byte-equal to the committed registry | `tests/Csharp2Md.Storage.Tests/Filesystem/FilesystemEmptyCommitTests.cs:80-82` — `onDisk["contracts/taxonomy-registry.json"].AsSpan().SequenceEqual(committedRegistry)`. Embedded resource also matches: `EmbeddedRegistryDriftTests.cs:17-19` | ✅ PASS |
| STOR-06 WHEN a family has zero records THEN the package omits that payload shard and the manifest reports count 0 | No `facts/`/`observations/`/`relations/` shards; every manifest artifact `Count == 0` | `FilesystemEmptyCommitTests.cs:83-87,95` — `Assert.False(Directory.Exists(.../facts|observations|relations))`; `Assert.All(manifest.Artifacts, entry => Assert.Equal(0, entry.Count))`. Mapper: `EmptySnapshotMappingTests.cs:52-53` — `Assert.Equal(0, entry.Count)` per family. In-memory: `InMemoryTransactionalStoreTests.cs:281-294` | ✅ PASS |
| STOR-07 committed JSON Schema files live under `contracts/` and declare `schema_version` 1 | Path prefix `contracts/`; `schema_version` is 1 | `JsonSchemaDriftTests.cs:72-80` — `Assert.StartsWith("contracts/", ...)`; `Assert.Equal(1, version.GetInt32())` | ✅ PASS |
| STOR-08 IF a committed schema differs from the serializer contract THEN the drift gate fails naming the differing file | Byte mismatch names the relative path | `JsonSchemaDriftTests.cs:91-93` — `committed.AsSpan().SequenceEqual(emitted)` with `DescribeMismatch(relativePath, ...)`. Naming: `:107-108` — `Assert.Contains(relativePath.Replace('\\', '/'), description)` | ✅ PASS |
| STOR-09 WHEN the registry emitter runs after this feature THEN `contracts/taxonomy-registry.json` remains byte-identical | Fresh emit equals the committed file | `tests/Csharp2Md.Domain.Tests/Registry/RegistryDriftGateTests.cs:18-19` — `committedBytes.AsSpan().SequenceEqual(freshBytes)` from `TaxonomyRegistryWriter.Write`. Storage embed stays in lockstep: `EmbeddedRegistryDriftTests.cs:17-19` | ✅ PASS |

### P1: Canonical round-trip through Domain construction

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STOR-10 WHEN a valid Domain fact, observation, confirmed relation, candidate, unresolved record or open frontier is committed and then read THEN the reader returns a value equal to the original under Domain equality | Filesystem `Commit` then `FactualPackageReader.Read` yields Domain equality for every family | `tests/Csharp2Md.Storage.Tests/Reading/CommittedPackageReadTests.cs:54` facts — `AssertDomainEqual(snapshot, result.Snapshot)`; `:71` observations; `:87` confirmed relation; `:109` candidate; `:131` unresolved (`Read_CommittedUnresolvedRecord_EqualsOriginalUnderDomainEquality`); `:153` frontier. Helper `:186-196` — `Assert.Equal(expected.Unresolved[index], actual.Unresolved[index])` (and the same for facts, relations, candidates, frontiers). Mapper-level equality: `FactRoundTripTests.cs:89`; `ObservationRelationRoundTripTests.cs:51,70,88,108,122`. **This Gate Check: Storage.Tests 140 passed, 0 failed, so the filesystem commit+read of unresolved is covered, not missing.** | ✅ PASS |
| STOR-11 `Csharp2Md.Storage` declares a project reference to `Csharp2Md.Domain` | Domain `ProjectReference` present | `tests/Csharp2Md.Storage.Tests/Isolation/StorageIsolationTests.cs:29-31` — `Assert.False(string.IsNullOrWhiteSpace(domainReference), "Csharp2Md.Storage must declare a project reference to Csharp2Md.Domain.")` | ✅ PASS |
| STOR-12 Storage SHALL NOT classify, promote or invent facts, observations or relations | No classifier/promoter/extractor type on the Storage surface | `StorageIsolationTests.cs:60-62` — `Assert.True(offending is null, $"Storage type '{offending?.FullName}' classifies, promotes or extracts facts.")` | ✅ PASS |
| STOR-13 WHEN Storage accepts a fact or observation payload at commit THEN it reconstructs that record through the domain’s public construction API | `FromWire`/`Validate` go through Domain `Create`; construction failure aborts and is not stored | Round-trip `Assert.Equal(snapshot.Facts[0], restored.Facts[0])` in `FactRoundTripTests.cs:89`; observations `ObservationRelationRoundTripTests.cs:51`. Abort when `Create` fails: `PackageValidatorTests.cs:185-186` — `Assert.Equal("construction", exception.Gate)` and `Assert.Contains(invalid.Identity.Id, exception.Detail)` (structural document); `:205-206` for observations | ✅ PASS |

### P1: Filesystem transactional publication

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STOR-14 Storage provides a filesystem adapter that implements the transactional storage port | `FilesystemTransactionalStore : ITransactionalStore`; Open/Stage/Commit publishes a child package | `FilesystemEmptyCommitTests.cs:23-29` — `ITransactionalStore store = new FilesystemTransactionalStore(...)`; `Assert.True(Directory.Exists(output.DirectoryPath))`; `Assert.Equal(SolutionKey, publication.SolutionKey)`. Production CLI: `AnalyzeSuccessTests.cs:55-59` — exit 0 and `manifest.json` under `--output` | ✅ PASS |
| STOR-15 Storage retains the in-memory adapter of that port | `InMemoryTransactionalStore` implements the port; sessions isolated | `InMemoryTransactionalStoreTests.cs:31-41` — distinct keys, isolated artifact sets, `TryGetPublication` for each. Port shape: `TransactionalStorePortTests.cs:33-42` — Stage/Commit/Abort are distinct members; `Stage` takes `FactualSnapshot` | ✅ PASS |
| STOR-16 WHEN a filesystem commit succeeds THEN the manifest is the last published artifact | Last artifact role is `Manifest` | `FilesystemEmptyCommitTests.cs:61-63` — `Assert.Equal(ArtifactRole.Manifest, artifacts[^1].Role)` and `Assert.Equal(PackagePublisher.ManifestKey, artifacts[^1].CanonicalKey)`. In-memory: `InMemoryTransactionalStoreTests.cs:112-113` | ✅ PASS |
| STOR-17 WHEN a filesystem commit succeeds against a directory that already holds a csharp2md package THEN the adapter replaces that package atomically so no observer can read a mix of old and new artifacts | After the second commit the child holds only the new shards; no leftover staging/bak | `FilesystemAtomicReplaceTests.cs:34-39` — `Assert.Contains("facts/structural.json", onDisk.Keys)`; `Assert.DoesNotContain("relations/candidates.json", onDisk.Keys)`; `Assert.False(Directory.Exists(child + ".staging"))`; `Assert.False(Directory.Exists(child + ".bak"))`; `Assert.True(File.Exists(.../manifest.json))` | ✅ PASS |
| STOR-18 IF a filesystem session aborts or a commit fails THEN every artifact from the last successful commit remains byte-identical | Prior snapshot byte-equal after failed commit and after abort | Failed commit: `FilesystemAtomicReplaceTests.cs:61-66` — `Assert.Throws<PublicationRejectedException>(colliding.Commit)`; `AssertEqualSnapshots(prior, after)`. Abort: `:84-87` — `AssertEqualSnapshots(prior, ...)` and old candidates still present | ✅ PASS |
| STOR-19 WHEN one or more solutions are committed under one output root THEN each solution occupies a child directory keyed by its logical solution identity | Child is `s-{32 hex}` under the output root, not the solution file name; N=1 still uses a child | `FilesystemEmptyCommitTests.cs:32-44` — `Assert.NotEqual(Path.GetFullPath(output.DirectoryPath), Path.GetFullPath(child))`; `Assert.Equal(..., Path.GetDirectoryName(child))`; `Assert.Equal(child, Assert.Single(Directory.GetFileSystemEntries(...)))`; `Assert.Matches("^s-[0-9a-f]{32}$", childName)`; `Assert.NotEqual("Acme Payments.sln", childName)` | ✅ PASS |
| STOR-20 IF the solution child path exists and does not contain a committed csharp2md manifest THEN the adapter refuses to write and leaves that path unchanged | Named refusal; directory byte-identical | `FilesystemRefusalTests.cs:44-55` — `Assert.Equal("not-a-package", exception.Gate)`; `Assert.Contains(child, exception.Detail)`; `Assert.False(File.Exists(.../manifest.json))`; key-for-key `SequenceEqual` of the dummy file | ✅ PASS |
| STOR-21 IF the output root path names an existing file THEN the adapter refuses to write naming that path | Named refusal; file bytes unchanged | `FilesystemRefusalTests.cs:21-28` — `Assert.Equal("not-a-package", exception.Gate)`; `Assert.Contains(output.DirectoryPath, exception.Detail)`; `Assert.True(File.Exists(output.DirectoryPath))`; `before.AsSpan().SequenceEqual(File.ReadAllBytes(...))` | ✅ PASS |
| STOR-22 WHEN the output root directory does not exist THEN the adapter creates it | Root exists after commit from an uncreated path | `FilesystemEmptyCommitTests.cs:19-28` — `Assert.False(Directory.Exists(output.DirectoryPath))` before; `Assert.True(Directory.Exists(output.DirectoryPath))` after `Commit` | ✅ PASS |
| STOR-23 IF the adapter cannot create, write or replace files because of an I/O error THEN it aborts, names the error, and leaves the last valid package unchanged | Gate `io`; detail names the path; prior snapshot preserved | `FilesystemIoFailureTests.cs:30-36` — `Assert.Equal("io", exception.Gate)`; `Assert.Contains(stagingPath, exception.Detail)`; `AssertEqualSnapshots(prior, ...)` | ✅ PASS |
| STOR-24 WHERE the in-memory adapter is used the system creates no files and no directories | Probe directory stays empty; working-tree snapshot unchanged | `InMemoryTransactionalStoreTests.cs:327-333` — `Assert.Empty(Directory.GetFileSystemEntries(probe))` before and after `Commit`. Pipeline: `NoFilesystemWriteTests.cs:37-38` — `Assert.Equal(before, after)` | ✅ PASS |

### P1: Commit-time structural gates

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STOR-25 IF a staged payload fails its JSON Schema THEN commit aborts as structural corruption and names the artifact key | Gate `schema`; detail contains the artifact key | `PackageValidatorTests.cs:32-33` unknown property — `Assert.Equal("schema", exception.Gate)`; `Assert.Contains(StructuralArtifactKey, exception.Detail)`. Type mismatch: `:47-48` same pair | ✅ PASS |
| STOR-26 IF a staged payload names a fact type, observation kind or relation kind absent from the taxonomy registry THEN commit aborts naming the unregistered value | Gate `unregistered-kind`; detail contains the unregistered string | `PackageValidatorTests.cs:64-65` fact; `:85-86` observation; `:106-107` relation — `Assert.Equal("unregistered-kind", exception.Gate)` and `Assert.Contains(unregistered, exception.Detail)` | ✅ PASS |
| STOR-27 IF two facts in one solution package share one identity string THEN commit aborts naming the identity | Gate `identity-collision`; detail contains the identity | `PackageValidatorTests.cs:121-122` — `Assert.Equal("identity-collision", exception.Gate)`; `Assert.Contains(identity, exception.Detail)`. Filesystem preserves prior: `FilesystemAtomicReplaceTests.cs:61-63` | ✅ PASS |
| STOR-28 IF a content hash in a payload does not match the bytes it claims to cover THEN commit aborts naming the identity | Gate `content-hash`; detail contains the identity | `PackageValidatorTests.cs:137-138` — `Assert.Equal("content-hash", exception.Gate)`; `Assert.Contains(fact.Identity.Id, exception.Detail)`. Hash covers omitted `content_sha256`: `ContentHashTests.cs:37-40` | ✅ PASS |
| STOR-29 IF a canonical payload contains an absolute filesystem path THEN commit aborts naming the field | Gate `absolute-path`; detail contains the field name; relative paths do not abort | `PackageValidatorTests.cs:155-156` — `Assert.Equal("absolute-path", exception.Gate)`; `Assert.Contains("name", exception.Detail)` for `/opt/app`, `\\server\\share`, `C:/windows/system32`. Relative: `:170-171` — `Assert.Equal(relativePath, report.Document.Documents[0].RelativePath)`; `Assert.True(report.Quarantine.IsEmpty)` | ✅ PASS |
| STOR-30 IF a Structural fact or an observation cannot be reconstructed through the domain’s public construction API THEN commit aborts naming the identity | Gate `construction`; detail contains the identity | `PackageValidatorTests.cs:185-186` structural document `../nope`; `:205-206` observation ordinal 0 — both `Assert.Equal("construction", exception.Gate)` and `Assert.Contains(...Identity..., exception.Detail)` | ✅ PASS |
| STOR-31 WHEN commit aborts for structural corruption THEN no new manifest is published for that session | Prior package/manifest bytes unchanged; pipeline marks `Unpublished` | `FilesystemAtomicReplaceTests.cs:63-66` — prior snapshot including `manifest.json` byte-equal after `identity-collision`. Engine: `StructuralCorruptionTests.cs:90-108` — `PublicationStatus.Unpublished`; kept publication payload `SequenceEqual` prior, last role `Manifest` | ✅ PASS |
| STOR-32 IF an Architecture, Contract, Persistence or Configuration fact or a confirmed relation fails schema or domain construction THEN commit writes that record to quarantine with a named diagnostic, omits it from canonical payloads, marks run certification failed, and commits the remaining valid artifacts | Quarantine named; derived fact omitted; remaining structural fact kept; certification `failed` | `PackageValidatorTests.cs:220-227` — `Assert.Equal("construction", quarantined.Gate)`; `Assert.Contains(invalid.Identity.Id, quarantined.IdentityOrKey)`; `Assert.True(report.Document.Components.IsEmpty)`; `Assert.Equal("Solution", Assert.Single(report.Document.Solutions).Identity.FactType)`; `Assert.Equal("failed", report.Document.RunCertification.Status)`. Published package: `QuarantinedPackageReadTests.cs:33-47` — remaining fact is Solution, Component absent from snapshot, quarantine `Gate == "construction"`, `Certification.Status == "failed"` (`PackageDirectoryWriter` publishes via `PackagePublisher.ToPublicationOrder`, including `quarantine/records.json` and the manifest) | ✅ PASS |
| STOR-33 WHEN a run produces unknowns, candidates or open frontiers but no structural corruption THEN the filesystem adapter commits the package | Filesystem `Commit` succeeds for candidates, unresolved and frontiers; in-memory candidate-only commit publishes a candidates shard | Filesystem: `CommittedPackageReadTests.cs:104-110` candidate `session.Commit()` then `AssertDomainEqual`; `:127-132` unresolved; `:149-154` frontier. Also `FilesystemAtomicReplaceTests.cs:25-27` candidate commit writes `relations/candidates.json`. In-memory: `InMemoryTransactionalStoreTests.cs:305-316` — last role Manifest; `Assert.Contains(... "relations/candidates.json")`; candidates manifest count 1. Validator: `PackageValidatorTests.cs:239-242` candidates and `:254-257` frontiers, `RunCertification.Status == "not_evaluated"`. Pipeline flag: `UnknownsCommitTests.cs:25-31` — `PublicationStatus.Committed`, `HasUnknownsOrCandidatesOrFrontiers` true, `CommitCount == 1` | ✅ PASS |

### P1: Factual package reader

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STOR-34 Storage exposes a public reader that accepts a committed solution-package directory and returns Domain facts, observations, confirmed relations, candidates, unresolved and frontiers | `FactualPackageReader.Read` returns a Domain `FactualSnapshot` | `CommittedPackageReadTests.cs:29-37` empty; `:54` facts; `:71` observations; `:87` confirmed; `:109` candidates; `:131` unresolved; `:153` frontiers — `AssertDomainEqual` / `Assert.Equal(FactualSnapshot.Empty, result.Snapshot)` | ✅ PASS |
| STOR-35 Analysis public surface SHALL NOT include the factual reader | `FactualPackageReader` and `PackageReadResult` absent from Analysis exports | `AnalysisPublicSurfaceTests.cs:66-67` — `Assert.DoesNotContain("FactualPackageReader", names)`; `Assert.DoesNotContain("PackageReadResult", names)` | ✅ PASS |
| STOR-36 IF the reader is given a directory that fails any abort-class commit-time gate THEN it rejects the read naming the failing gate and returns no partial snapshot | Gate named; `result` stays null | `UnreadablePackageReadTests.cs:70-72` — `Assert.Equal("identity-collision", exception.Gate)`; `Assert.Contains(identity, exception.Detail)`; `Assert.Null(result)`. Truncated (manifest removed): `:30-32` — `Assert.Equal("not-a-package", exception.Gate)`; `Assert.Contains(package, exception.Detail)`; `Assert.Null(result)` | ✅ PASS |
| STOR-37 IF the reader is given a path that is not a csharp2md package THEN it rejects the read naming the path | Gate `not-a-package`; detail contains the path; no snapshot | `UnreadablePackageReadTests.cs:48-50` — `Assert.Equal("not-a-package", exception.Gate)`; `Assert.Contains(random, exception.Detail)`; `Assert.Null(result)` | ✅ PASS |
| STOR-38 WHEN the reader loads a package that contains quarantined records THEN it returns the valid Domain snapshot and exposes the quarantine records separately from confirmed facts and relations | Snapshot has remaining Solution only; quarantine is a separate collection | `QuarantinedPackageReadTests.cs:33-47` — `Assert.Equal("Solution", remaining.Reference.FactType)`; `Assert.DoesNotContain(... FactType == "Component")`; `Assert.True(result.Snapshot.ConfirmedRelations.IsEmpty)`; `Assert.Equal("construction", quarantined.Gate)`; `Assert.Equal("failed", result.Certification.Status)` | ✅ PASS |

### P1: Compact payload layout

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STOR-39 The package SHALL NOT contain a directory whose name is a fact identity or an observation identity | Identity strings absent from directory names | `FilesystemCompactLayoutTests.cs:60-61` — `Assert.All(identities, identity => Assert.DoesNotContain(identity, directories))` | ✅ PASS |
| STOR-40 Payload artifacts partitioned by fact family, observations by observation kind, and confirmed relations by relation kind | Family/kind shard paths present | `FilesystemCompactLayoutTests.cs:43-55` — `Assert.Contains("facts/structural.json" | architecture | contract | persistence | configuration)`; `Assert.Contains("observations/" + invocationWire + ".json")`; `Assert.Contains("relations/confirmed/" + containsWire + ".json")` | ✅ PASS |
| STOR-41 Shard and bucket keys SHALL be independent of display names and translated labels | Keys use wire names, not display names | `FilesystemCompactLayoutTests.cs:53-69` — `Assert.NotEqual(nameof(ObservationKind.Invocation), invocationWire)`; `Assert.DoesNotContain("Payments.Api" | "Invoice.cs", directories/keys)` | ✅ PASS |
| STOR-42 A canonical payload record SHALL be serialized once; other package files SHALL reference it by identity rather than duplicating its bytes | Payload shards do not embed each other’s JSON; content hash is stable | `FilesystemCompactLayoutTests.cs:111-112` — `Assert.DoesNotContain(leftJson, rightJson)` pairwise. `ContentHashTests.cs:23-25` — two writes produce the same 64-hex `ContentSha256` | ✅ PASS |
| STOR-43 IF an artifact carries a timestamp or a runtime measurement THEN it SHALL live only in the measurements envelope | Non-measurement files contain neither `"timestamp"` nor `"duration_milliseconds"` | `FilesystemCompactLayoutTests.cs:80-89` — skip `measurements.json`; `Assert.DoesNotContain("\"timestamp\"")` and `Assert.DoesNotContain("\"duration_milliseconds\"")` | ✅ PASS |
| STOR-44 WHEN the same Domain graph is committed twice THEN every canonical payload file SHALL be byte-identical | Same-graph recommit matches; encoding is canonical | `InMemoryTransactionalStoreTests.cs:131` — `AssertEqualCanonicalPayloads(first, sameGraphAgain)` (measurements stripped). Encoding: `CanonicalJsonTests.cs:15-16` no BOM; `:25-26` LF only; `:36-38` two-space indent | ✅ PASS |
| STOR-45 WHEN the same fragments are staged in two different orders THEN the committed canonical payload files SHALL be byte-identical | Reversed staging orders compare equal | `InMemoryTransactionalStoreTests.cs:130` — `AssertEqualCanonicalPayloads(first, second)`. Pipeline: `StagingOrderTests.cs:35-37` — `AssertEqualCanonicalPayloads` of the two publications | ✅ PASS |
| STOR-46 The package SHALL NOT emit catalogs, postings, Markdown pages, source projections or a retrieval guide | Those names/extensions absent | `FilesystemCompactLayoutTests.cs:70-78` — `Assert.DoesNotContain("catalogs"|"postings"|"retrieval.md")`; `Assert.False(key.EndsWith(".md"|".cs"))`; directories do not contain `catalogs`, `postings`, or `source` | ✅ PASS |

### P1: CLI `--output` and a valid empty package

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STOR-47 `analyze` requires `--output <dir>` and at least one `--solution` | `--output` required, arity exactly one; `--solution` still required | `AnalyzeCommandTreeTests.cs:38-40` — `Assert.True(output.Required)`; `Assert.Equal(ArgumentArity.ExactlyOne, output.Arity)`. Product options: `AnalyzeOptionSurfaceTests.cs:43` — `Assert.Equal(["--output", "--solution"], analyzeProductOptions)`. `--solution` required: `AnalyzeCommandTreeTests.cs:26` | ✅ PASS |
| STOR-48 IF `analyze` is invoked with no `--output` THEN the CLI exits 1 and names the missing option on stderr | Exit 1; stderr contains `--output` | `AnalyzeInvocationErrorTests.cs:30-31` — `Assert.Equal(1, exitCode)`; `Assert.Contains("--output", stderr)` | ✅ PASS |
| STOR-49 WHEN `analyze` completes with no structural failure THEN the CLI writes a schema-valid package for every requested solution under the output root and exits 0 | Exit 0; hashed child; reader returns `FactualSnapshot.Empty` | `AnalyzePackageWriteTests.cs:44-57` — `Assert.Equal(0, exitCode)`; `Assert.Matches("^s-[0-9a-f]{32}$", ...)`; `Assert.Equal(FactualSnapshot.Empty, result.Snapshot)` | ✅ PASS |
| STOR-50 WHEN the bootstrapped pipeline produces zero facts, observations and relations THEN each written package is schema-valid, includes the taxonomy-registry copy and a manifest, and reports count 0 for every family | Registry copy, manifest, all counts 0, no family shards | `AnalyzePackageWriteTests.cs:59-68` — registry `Assert.Equal` committed bytes; `Assert.All(manifest.Artifacts, entry => Assert.Equal(0, entry.Count))`. Filesystem: `FilesystemEmptyCommitTests.cs:80-95`. Pipeline: `PersistenceManifestTests.cs:28-39` | ✅ PASS |
| STOR-51 WHEN `analyze` writes a package THEN no file or directory outside the requested output root is created, modified or deleted | Working-tree snapshot excluding the output root is unchanged | `AnalyzePackageWriteTests.cs:44-45` — `Assert.Equal(before, after)` over `src/`, `tests/`, `contracts/`, `fixtures/` excluding `bin`/`obj`/`TestResults` and the output root | ✅ PASS |
| STOR-52 The CLI SHALL NOT expose `--topic`, `--domain`, `--manifest`, `--trust`, `--include-source-generators` or `--analysis-timeout` | Each name absent from root and analyze | `AnalyzeOptionSurfaceTests.cs:32-35` — `Assert.DoesNotContain(removed, rootNames/analyzeNames)` for all six | ✅ PASS |
| STOR-53 `Csharp2Md.Cli` continues to declare no project reference to `Csharp2Md.Domain` | Domain absent from CLI project references | `CliIsolationTests.cs:23-25` — `Assert.DoesNotContain("Csharp2Md.Domain", names)`; `Assert.Equal(ExpectedProjectReferences, names)` (Analysis, Projection, Storage) | ✅ PASS |
| STOR-54 WHEN a run reports unknowns, candidates or open frontiers but no structural failure THEN the CLI writes the package and exits 0 | Exit 0 on the unknowns flag; a filesystem run still writes `manifest.json` | Exit 0: `AnalyzeExitCodeTests.cs:56` — `Assert.Equal(0, exitCode)` for `PublicationStatus.Committed` + `hasUnknownsOrCandidatesOrFrontiers: true`. Write: `:126-130` — `Assert.Equal(0, exitCode)` and `Assert.Equal("manifest.json", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(..., "manifest.json", ...))))`. Adapter-level unknowns commit is STOR-33 | ✅ PASS |
| STOR-55 IF a structural failure aborts publication for any requested solution THEN the CLI exits with code 2 | Exit 2 | `AnalyzeExitCodeTests.cs:31` unpublished/corruption fake → `Assert.Equal(2, exitCode)`; `:149` `PublicationRejectedException` from commit → `Assert.Equal(2, exitCode)` | ✅ PASS |

### P1: Session isolation and cleanup

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| STOR-56 WHEN more than one solution is requested THEN each SHALL commit or abort independently under its own child directory | Two solutions; only the successful child remains, keyed to the first solution | `AnalyzeMultiSolutionTests.cs:47-61` — `Assert.Equal(2, exitCode)`; `Assert.Single(children)`; `Assert.Equal("Acme.Orders.slnx", manifest.SolutionFileName)`; `Assert.NotEqual("Acme.Payments.slnx", manifest.SolutionFileName)` | ✅ PASS |
| STOR-57 IF one solution’s commit aborts THEN every remaining solution SHALL still be analyzed and, on success, committed | First package valid after the second commit is rejected | `AnalyzeMultiSolutionTests.cs:47-56` — exit 2; remaining child reads as `FactualSnapshot.Empty` (the first solution still committed) | ✅ PASS |
| STOR-58 IF a session is aborted or cancelled THEN the adapter leaves no staging directory and no partial artifact for that solution | Staging gone; last package byte-identical | Abort: `FilesystemAbortStagingTests.cs:33-39` — `Assert.False(Directory.Exists(staging))`; prior snapshot `SequenceEqual`. Cancel: `PipelineCancellationTests.cs:83-94` — `PublicationStatus.Unpublished`; `Assert.False(Directory.Exists(child + ".staging"))`; prior package bytes unchanged | ✅ PASS |
| STOR-59 IF a second session opens the same solution package root while another session on that root has neither committed nor aborted THEN the adapter rejects the second open naming the root | Gate `lock`; detail names the root; lock released after abort | Filesystem: `FilesystemLockTests.cs:20-22` — `Assert.Equal("lock", locked.Gate)`; `Assert.Contains(output.DirectoryPath, locked.Detail)`. In-memory: `InMemoryTransactionalStoreTests.cs:174-176` — `Assert.Contains("solution-a", locked.Detail)` | ✅ PASS |
| STOR-60 WHEN a store session has already committed THEN a further `Commit` on that session SHALL be rejected | Gate `session-state` | `InMemoryTransactionalStoreTests.cs:149-151` — `Assert.Throws<PublicationRejectedException>(session.Commit)`; `Assert.Equal("session-state", exception.Gate)`; `Assert.Contains("solution-a", exception.Detail)` | ✅ PASS |
| STOR-61 WHEN a store session has already committed THEN a further `Stage` on that session SHALL be rejected | Gate `session-state` | `InMemoryTransactionalStoreTests.cs:162-164` — `Assert.Throws<PublicationRejectedException>(() => session.Stage(...))`; `Assert.Equal("session-state", exception.Gate)`; `Assert.Contains("solution-a", exception.Detail)` | ✅ PASS |

**Status**: ✅ All ACs covered — 61/61 ACs matched the spec-defined outcome with `file:line` evidence · ⚠️ 0 spec-precision gaps · 0 uncovered (❌) · 0 hard AC failures

---

## Findings on the previous FAIL

The previous Verifier marked STOR-10 and STOR-33 FAIL because `Read_CommittedUnresolvedRecord_*` (and later other `CommittedPackageReadTests` cases) threw `PublicationRejectedException` at `FilesystemTransactionalStore.Session.Commit`. That was a Windows `Directory.Move` sharing-violation flake (`Access to the path '...staging' is denied`), not missing unresolved support.

This pass did not inherit that FAIL. `CommittedPackageReadTests` commits facts, observations, confirmed relations, candidates, unresolved, and frontiers through `FilesystemTransactionalStore` and asserts Domain equality on read (`:54`, `:71`, `:87`, `:109`, `:131`, `:153`). Post-T52 `4c4948b` retries `Directory.Move` on `IOException`/`UnauthorizedAccessException` (`FilesystemTransactionalStore.cs:219-235`) and serializes filesystem tests in `[Collection("FilesystemStore")]`. This Gate Check ran Storage.Tests to 140 passed, 0 failed. STOR-10 and STOR-33 are covered.

---

## Edge Cases

From spec.md's Edge Cases section:

- [x] Omitting `--output` exits 1 naming `--output` (STOR-48) — `AnalyzeInvocationErrorTests.cs:30-31`
- [x] `--output` naming an existing file refuses naming that path (STOR-21) — `FilesystemRefusalTests.cs:21-28`
- [x] Solution child without a csharp2md manifest refused and left unchanged (STOR-20) — `FilesystemRefusalTests.cs:44-55`
- [x] I/O errors during staging abort, name the error, and preserve the last valid package (STOR-23) — `FilesystemIoFailureTests.cs:30-36`
- [x] Two facts colliding on one identity abort naming the identity (STOR-27) — `PackageValidatorTests.cs:121-122`
- [x] Classified/derived fact failing construction is quarantined and the rest commits (STOR-32) — `PackageValidatorTests.cs:220-227`
- [x] Absolute path in a canonical payload aborts naming the field (STOR-29) — `PackageValidatorTests.cs:155-156`
- [x] N = 1 still occupies a child directory under the output root (STOR-19) — `FilesystemEmptyCommitTests.cs:32-36`
- [x] Pipeline zeros still produce a schema-valid package with count 0 (STOR-50) — `AnalyzePackageWriteTests.cs:44-68`
- [x] Cancellation mid-session leaves no staging residue (STOR-58) — `PipelineCancellationTests.cs:88-89`
- [x] In-memory adapter creates no files (STOR-24) — `InMemoryTransactionalStoreTests.cs:327-333`

All 11 documented edge cases are handled and evidenced.

---

## Discrimination Sensor

**Sensor**: skipped — standing project decision (user runs Stryker manually; documented in `tasks.md`'s header and `.specs/STATE.md`'s standing engineering constraints, consistent with `symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`, `knowledge-taxonomy-contract` and `engine-bootstrap`). No mutants were injected. This produces no pass/fail signal by design and is not treated as a gap.

---

## Interactive UAT Results

Not performed. This feature is backend storage, schemas, a filesystem adapter and CLI `--output`; the spec's Independent Tests are fully automated. No user-facing flow requiring human judgment.

---

## Code Quality

Sampled across phases: schema drift (`JsonSchemaDriftTests.cs`), DomainMapper round-trips (`FactRoundTripTests.cs`, `ObservationRelationRoundTripTests.cs`), last-gate validator (`PackageValidatorTests.cs`), filesystem adapter (`FilesystemTransactionalStore.cs:219-235` retry, `FilesystemEmptyCommitTests.cs`, `FilesystemAtomicReplaceTests.cs`), reader (`CommittedPackageReadTests.cs`, `UnreadablePackageReadTests.cs`, `QuarantinedPackageReadTests.cs`), CLI (`AnalyzePackageWriteTests.cs`, `AnalyzeExitCodeTests.cs`, `AnalyzeMultiSolutionTests.cs`), isolation (`StorageIsolationTests.cs`, `AnalysisPublicSurfaceTests.cs`, `CliIsolationTests.cs`).

| Principle | Status |
| --- | --- |
| Minimum code | ✅ — one filesystem adapter, retained in-memory adapter, one reader, committed schemas, last-gate validator; no query engine, no postings, no extra CLI verbs |
| Surgical changes | ✅ — post-T52 change is a bounded `Directory.Move` retry plus an xUnit collection; `PublicationRejectedException.Message` is `gate: detail` |
| No scope creep | ✅ — matches the Out-of-Scope table; Projection stays a marker; extractors remain stubs; batch manifest is not emitted |
| Matches patterns | ✅ — `[Trait("Requirement", "STOR-nn")]` continues the ENG/TAX convention; isolation tests parse csproj the same way as bootstrap |
| Spec-anchored outcome check (asserted values match spec) | ✅ — 61/61 exact; 0 ⚠️; 0 ❌ |
| Per-layer Coverage Expectation met | ✅ — schemas, mapper, validator, filesystem adapter, reader, CLI, isolation, and requirement-trait coverage each have 1:1 AC evidence |
| Every test maps to a spec AC/edge case/Done-when | ✅ — new tests carry `STOR-nn` (or retained `ENG-*` where the contract was rewritten); `RequirementCoverageTests` requires STOR-01..61 |
| Documented guidelines followed | ✅ — `AGENTS.md`/`CLAUDE.md` (no `Microsoft.Build.*`, no `MSBuildLocator`; Analysis still has zero Roslyn/MSBuild package references), `Directory.Build.props` `TreatWarningsAsErrors`, xUnit 2.9.3 / VSTest on net10.0 |

Would a senior engineer approve? Yes. Wire schemas, Domain reconstruction, abort vs quarantine, filesystem atomic replace, factual read-back, compact layout, and `analyze --output` are in place and green. The previous STOR-10/STOR-33 FAIL was a Windows staging-swap flake, closed by `4c4948b` and confirmed by this gate.

---

## Gate Check

- **Gate command**: `dotnet build csharp2md.slnx -c Release` → exit 0 (0 warnings, 0 errors) → `dotnet format csharp2md.slnx --verify-no-changes` → exit 0 → `dotnet test csharp2md.slnx` → exit 0
- **Result**: **801 passed, 0 failed, 0 skipped**
  - `Csharp2Md.Domain.Tests`: 544 passed
  - `Csharp2Md.Analysis.Tests`: 90 passed
  - `Csharp2Md.Storage.Tests`: 140 passed
  - `Csharp2Md.Cli.Tests`: 24 passed
  - `Csharp2Md.Projection.Tests`: 3 passed
- **Test count before feature**: 660 across the five test projects (engine-bootstrap close: Domain 544, Analysis 82, Storage 15, Cli 16, Projection 3)
- **Test count after feature**: 801 across the same five projects
- **Delta**: +141 (Analysis +8, Storage +125, Cli +8; Domain and Projection unchanged). No silent deletions. No assertion weakening observed in the sampled STOR tests.
- **Skipped tests**: none.
- **Failures**: none.

---

## Fix Plans

No blocking fix tasks. No spec-precision follow-ups.

---

## Requirement Traceability Update

`spec.md` was not edited (Verifier scoped to `validation.md` only). Recommended statuses:

| Requirement | Previous Status | New Status |
| ----------- | --------------- | ---------- |
| STOR-01 – STOR-61 | Implementing | ✅ Verified |

---

## Summary

**Overall**: ✅ Ready

**Spec-anchored check**: 61/61 ACs matched the spec-defined outcome exactly with `file:line` evidence; 0 spec-precision gaps
**Sensor**: skipped — standing project decision
**Gate**: 801 passed, 0 failed, 0 skipped

**What works**: All 52 tasks are checked and committed, plus post-T52 `4c4948b` retries Windows staging swap. Storage publishes versioned JSON Schemas and round-trips every Domain family through `Create`. The filesystem adapter stages, validates, publishes the manifest last, replaces atomically, preserves the last valid package on abort, and refuses non-package children. Abort-class gates refuse corrupt payloads; invalid derived records quarantine and the rest commits; unknowns still commit. The reader returns Domain types and rejects unreadable packages. Compact layout has no identity-named directories and no postings/Markdown. `analyze --output` writes a schema-valid empty package, exits 1 without `--output`, and exits 2 on structural abort. Storage.Tests is green, including filesystem commit+read of unresolved records.

**Issues found**: none.

**Next steps**: Feature is ready to close.
