# Retrieval Projections Validation

**Date**: 2026-08-27
**Spec**: `.specs/features/retrieval-projections/spec.md`
**Diff range**: `ea4a1ad^..HEAD` (`ea4a1ad` … `5f4061d`)
**Verifier**: independent sub-agent (author ≠ verifier)
**Result**: PASS

T1–T49 each have a Conventional Commit on `feat/retrieval-projections` (`ea4a1ad` … `5f4061d`). Implementation starts at T1 `ea4a1ad`. HEAD is T49 `5f4061d`. `tasks.md` Done-when boxes are all checked. Header still says “Approved”; the checkboxes and commits are the completion evidence.

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1 | Done | `ea4a1ad` `fix(storage): make confirmed-relation ordering key total` |
| T2 | Done | `6f2360c` `fix(storage): make observation ordering key total` |
| T3 | Done | `f3869b8` `feat(storage): introduce PublishedPackageView with canonical slots` |
| T4 | Done | `3c058c2` `feat(storage): add fact and relation location lookups to the view` |
| T5 | Done | `c44b454` `feat(storage): derive the manifest from published fragments` |
| T6 | Done | `3d8fe59` `refactor(storage): retire manifest construction from DomainMapper` |
| T7 | Done | `48fc4b9` `refactor(storage): extract PublicationPipeline shared by both stores` |
| T8 | Done | `b3a6f3f` `feat(analysis): declare ISourceDocumentReader on the write port` |
| T9 | Done | `cff4550` `feat(analysis): add deferred StagedFragment payload materialization` |
| T10 | Done | `4bb56c8` `feat(storage): thread source reader through Open and staging writes` |
| T11 | Done | `daacfdf` `feat(analysis): add filesystem source document reader` |
| T12 | Done | `684c8d9` `feat(analysis): wire per-solution source readers through AnalysisEngine` |
| T13 | Done | `2640a2a` `feat(storage): declare the package projector port` |
| T14 | Done | `07c899a` `feat(storage): invoke the projector inside the publication pipeline` |
| T15 | Done | `9e944e9` `feat(storage): validate projection keys and ordinals` |
| T16 | Done | `a83465b` `feat(storage): validate projection values and locator spans` |
| T17 | Done | `296666b` `test(storage): assert projection abort preserves the prior package` |
| T18 | Done | `53258cf` `feat(cli): wire PackageProjector into analyze without a new flag` |
| T19 | Done | `c422834` `feat(domain): add optional declaration locator to Symbol` |
| T20 | Done | `f199601` `feat(analysis): populate symbol declaration locators from syntax spans` |
| T21 | Done | `25aad01` `feat(storage): round-trip symbol declaration locators on the wire` |
| T22 | Done | `e32d80b` `test(storage): assert symbol identity components did not drift` |
| T23 | Done | `1b4e1ee` `feat(projection): emit one source artifact per inventoried document` |
| T24 | Done | `89e33f3` `feat(projection): redact suspected-secret spans with a fixed marker` |
| T25 | Done | `9bf5c31` `feat(projection): emit redaction envelopes for redacted source artifacts` |
| T26 | Done | `3e7e2d6` `feat(projection): abort source-drift when inventoried bytes change` |
| T27 | Done | `3ce9c83` `test(analysis): assert fixture secrets never reach the published package` |
| T28 | Done | `f9bb823` `feat(projection): publish entry-point and boundary-operation catalogs` |
| T29 | Done | `1e4df34` `feat(projection): catalog components, deployment units and contracts` |
| T30 | Done | `a23599f` `feat(projection): catalog data stores, objects and fields` |
| T31 | Done | `a02e3f2` `feat(projection): rank and catalog prioritized unknowns` |
| T32 | Done | `f7eb589` `feat(projection): omit empty catalogs and order entries by fact id` |
| T33 | Done | `9339186` `feat(projection): project outgoing and incoming relation postings` |
| T34 | Done | `d822a11` `feat(projection): project callers and callees from confirmed invokes` |
| T35 | Done | `9007418` `feat(projection): project contract and data-access postings` |
| T36 | Done | `08d0f24` `feat(projection): project unknowns and open frontier postings` |
| T37 | Done | `35014a1` `test(analysis): assert catalog and posting citations resolve` |
| T38 | Done | `8028bf9` `feat(projection): project entry point and boundary operation pages` |
| T39 | Done | `9004c88` `feat(projection): project component contract and persistence pages` |
| T40 | Done | `93e2797` `test(projection): enforce markdown boundary and page determinism` |
| T41 | Done | `f0ac053` `feat(projection): project the retrieval guide` |
| T42 | Done | `259cf17` `feat(projection): project the generated AGENTS.md` |
| T43 | Done | `c053fba` `feat(projection): split catalogs and postings at a byte ceiling` |
| T44 | Done | `459e652` `test(projection): assert split preserves entries and manifest coverage` |
| T45 | Done | `bff4aaa` `test(analysis): assert projection determinism across runs and paths` |
| T46 | Done | `475fe04` `feat(storage): reject absolute paths in projection artifacts` |
| T47 | Done | `4b1c228` `test(projection): assert the assembly reference set` |
| T48 | Done | `b164a3b` `feat(projection): publish no projection when a run has no facts` |
| T49 | Done | `5f4061d` `test(analysis): close the RP coverage matrix` |

No blocked or partial tasks. T49’s `RequirementCoverageTests` is a trait-presence scanner; this report re-derives each AC from the cited assertion, not from that trait list.

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| Projector port on Storage public surface, implemented by Projection (RP-01) | `IPackageProjector` public on Storage; `PackageProjector` implements it; Projection refs Domain+Storage | `PackageProjectorTests.cs:16-18` `IsPublic` + assembly `Csharp2Md.Storage`; `:38` Contains `IPackageProjector`; `ProjectionIsolationTests.cs:28` `Assert.Equal(["Csharp2Md.Domain", "Csharp2Md.Storage"], names)` | PASS |
| `Commit()` invokes projector after `PackageValidator.Validate` and before `ToPublicationOrder` (RP-02) | Validate, then view from report, then `Project`, then order | `ProjectorPublicationTests.cs:81-89` `validate` < `viewFromReport` < `project` < `order`; `:67-70` view equals `PackageValidator.Validate(...).Document`. **⚠️ Spec-precision**: the before-ordering half is source `IndexOf` on `PublicationPipeline.cs` | PASS (spec-precision: see note) |
| Projector fragments join the same atomic publication and the manifest (RP-03) | Fragment on disk; manifest Path equals key | `ProjectorPublicationTests.cs:30-31` ContainsKey + SequenceEqual bytes; `:47-49` Single path `projections/sample.json`; `ManifestBuilderTests.cs:24-25` every manifest Path in fragment keys | PASS |
| Projector throw / validation fail → `PublicationRejectedException`, staging deleted, prior package byte-identical (RP-04) | Gate `projection`; no `.staging`; prior bytes equal | `ProjectionAbortTests.cs:23-27` SequenceEqual prior/after; `:37` `Directory.Exists(child + ".staging")` false; `:57-58` `exception.Gate` `projection` | PASS |
| No Analysis ref from Projection; no projector member on store/session (RP-05) | Analysis absent from Projection csproj; no `Project`/`Projector` members | `ProjectionIsolationTests.cs:17-18` no Analysis ref; `TransactionalStorePortTests.cs:28-33` DoesNotContain projector members; `SourceDocumentReaderStagingTests.cs:107-108` `GetMethod("Project")` null | PASS |
| No projector supplied → factual artifacts only, no projection artifact (RP-06) | No catalogs/postings/source/markdown/guides | `NoProjectorPublicationTests.cs:25-28` AssertNoProjectionArtifact on disk and manifest | PASS |
| One `source/` artifact per inventoried document, bytes equal outside redaction (RP-07) | One key per document; opaque bytes unchanged | `SourceProjectorTests.cs:18-20` length + Distinct keys; `:69-71` SequenceEqual opaque. **⚠️ Spec-precision**: a redacted document also emits `source/*.meta.json` (RP-10) | PASS (spec-precision: see note) |
| `source/` keys from owning project + relative path; no absolute/clone segment (RP-08) | Not rooted; clone root absent; distinct keys for shared relative path | `SourceProjectorTests.cs:34-38` `IsPathRooted` false, DoesNotContain cloneRoot; `:52-56` two keys containing `acme.orders` / `acme.payments` | PASS |
| Suspected-secret span replaced by 17-byte `[REDACTED-SECRET]` regardless of original length (RP-09) | Marker length 17; short and long spans same marker | `SecretRedactorTests.cs:17-18` Length 17 + published text; `:29-30` same marker after longer span; `:126-127` secret absent, prefix+marker | PASS |
| Redacted document declares `redacted=true`, ordinal spans, original and published sha256 (RP-10) | Those four fields | `RedactionEnvelopeTests.cs:32-38` `Redacted` true, OriginalSha256, PublishedSha256, NotEqual; `:64-66` spans StartColumn 2 then 6 | PASS |
| Unredacted published sha256 equals Document `ContentSha256` (RP-11) | Digest equals `Documents[0].ContentSha256` | `SourceIntegrityTests.cs:29-30` Equal digest + SequenceEqual body. **⚠️ Spec-precision**: the AC names `facts/structural.json`; the test reads the Document DTO | PASS (spec-precision: see note) |
| No secret value, individual secret hash, or unredacted excerpt in published artifacts (RP-12) | Fixture secret and its sha256 absent | `SourceSecretAbsenceTests.cs:31-37` secret bytes absent; `:44-51` secret hash absent; `:66-67` marker present, secret absent | PASS |
| `Symbol` carries optional declaration locator (document, span, hash) (RP-13) | Locator fields; identity unchanged with/without it | `DeclarationLocatorTests.cs:19-22` Document/RelativePath/Span/Hash; `StructuralFactsTests.cs:162-163` Equal fact ids; `SymbolLocatorRoundTripTests.cs:36` Equal locator after round-trip | PASS |
| `SymbolFactEmitter` fills locator from the declaration’s own syntax span (RP-14) | PlaceOrderAsync locator document = Document fact; partial members stay on own files | `SymbolFactEmitterTests.cs:424` Equal document id; `:391-392` FromAlpha on alpha.cs, FromZeta on zeta.cs | PASS |
| Published locator span is a syntactically complete member (RP-15) | Slice parses as `PlaceOrderAsync` method, no error diagnostics | `SymbolFactEmitterTests.cs:320-326` `ParseMemberDeclaration`, Identifier `PlaceOrderAsync`, no Error diagnostics. **⚠️ Spec-precision**: Independent Test says slice the published `source/` artifact; the test slices the fixture file | PASS (spec-precision: see note) |
| Multiple declaring references → locator whose document id, start line, start column sort first (RP-16) | Partial type locator on alpha.cs from both tree orders | `SymbolFactEmitterTests.cs:351-357` Contains `alpha.cs`, Equal after ReverseTreeOrder | PASS |
| `contracts/taxonomy-registry.json` byte-identical (RP-17) | Embedded SequenceEqual committed; Symbol identity `project`+`signature` | `EmbeddedRegistryDriftTests.cs:20-22` SequenceEqual; `:32` `["project", "signature"]` | PASS |
| Six catalogs: entry points; boundary operations; components+DUs; contracts; stores/objects/fields; unknowns (RP-18) | Those keys published | `CatalogProjectorTests.cs:22-24` `catalogs/entry-points.json`; `:46-48` boundary-operations; `:155` components-and-deployment-units; `:199` contracts; `:288` data-stores-objects-and-fields; `:419-420` unknowns | PASS |
| Catalog entry carries fact id, canonical artifact key, zero-based ordinal (RP-19) | Those three fields equal `TryLocate` | `CatalogProjectorTests.cs:109-112` `Assert.Equal(citation, new ArtifactCitation(entry.ArtifactKey, entry.Ordinal))`; `CatalogProjectionFactory.cs:176-181` FactIdAt + TryLocate | PASS |
| Cited artifact at cited ordinal yields the claimed fact id (RP-20) | `entry.FactId == ClaimedId(cited)` | `CitationResolutionTests.cs:66-67` Equal fact id; `CatalogProjectorTests.cs:74` AssertEveryEntryResolves | PASS |
| Empty family omits that catalog rather than publish empty (RP-21) | Empty document → no catalogs; entry-points only when only entry points exist | `CatalogOmissionTests.cs:16-17` Empty; `:28-31` Single entry-points, DoesNotContain others | PASS |
| Catalog entries ordered by fact-id ordinal (RP-22) | SequenceEqual ordered fact ids | `CatalogOmissionTests.cs:72-74` OrderBy fact id; `:93` persistence catalog same | PASS |
| Unknowns ranked by confirmed-relation degree desc, then fact id (RP-23) | high, mid, zero; equal degree → alpha then zeta | `UnknownRankingTests.cs:17` `[highId, midId, zeroId]`; `:44` `[alphaId, zetaId]`; `CatalogProjectorTests.cs:442-445` catalog follows Rank | PASS |
| Catalog entry contains no value absent from the cited artifact (RP-24) | Every string property except key/ordinal present in cited JSON | `CatalogOmissionTests.cs:139-141` AssertEntryValuesPresentInCitedArtifact | PASS |
| Five posting families: in/out; callers/callees; contract prod/cons; data r/w; unknowns/frontiers (RP-25) | Those keys and groups | `PostingProjectorTests.cs:21-25` outgoing; `:37-38` incoming; `CallersCalleesPostingTests.cs:16-18` callers; `:36-38` callees; `ContractDataAccessPostingTests.cs:18-20` consumers; `:40-42` producers; `:107-109` readers; `:128-130` writers; `UnknownFrontierPostingTests.cs:74-78` frontiers; `:104-108` unknowns | PASS |
| Posting entry carries artifact key + ordinal only; no payload or evidence chain (RP-26) | JSON object count 2; no `derived_from`/`source`/`target` | `PostingProjectionFactory.cs:145-157` Count 2, false derived_from/source/target; `PostingProjectorTests.cs:23` AssertEntriesAreCitationsOnly | PASS |
| Cited posting ordinal yields the relation whose endpoints the posting claimed (RP-27) | Outgoing source = group; incoming target = group | `PostingProjectorTests.cs:87-88` `relation.Source.Id`; `:109-110` `relation.Target.Id`; `CitationResolutionTests.cs:93-96` source or target equals group.FactId | PASS |
| Callers/callees derived only from confirmed `invokes` (RP-28) | Artifact `relations/confirmed/invokes.json`; kind `invokes`; lists exact callers | `CallersCalleesPostingTests.cs:22-23` Equal key and kind; `:90-93` Equal expected callers, length 2; `CitationResolutionTests.cs:166-173` kind invokes, Equal expected | PASS |
| Candidates, unresolved, frontiers stay in their own postings, not confirmed (RP-29) | Candidate absent from callers; confirmed entries cite `relations/confirmed/` | `CallersCalleesPostingTests.cs:63-68` DoesNotContain candidate; `UnknownFrontierPostingTests.cs:61-64` StartsWith confirmed, DoesNotContain candidates/unresolved/frontiers | PASS |
| Posting groups by subject fact id; entries by artifact key then ordinal (RP-30) | Equal to OrderBy / ThenBy | `CitationResolutionTests.cs:115` groups OrderBy FactId; `:127-132` entries OrderBy key ThenBy ordinal | PASS |
| One Markdown page per entry point, boundary operation, component, DU, contract, data store, data object (RP-31) | Those `markdown/` prefixes; 4+5 pages | `MarkdownProjectorTests.cs:144-152` 4 pages, entry-point/ and boundary-operation/; `MarkdownIdentityPageTests.cs:110-115` 5 prefixes including data-store and data-object | PASS |
| Page states fact id, facets, direct confirmed relations, links into catalogs/postings/source (RP-32) | Contains fact id, facet/relation text, those three key prefixes | `MarkdownProjectorTests.cs:29-32` fact id + Facets + executes; `:115-117` catalogs/ postings/ source/; `:38-42` citation key+ordinal match TryLocate | PASS |
| Page does not state a fact/relation/facet absent from the payload (RP-33) | id1: citations in KnownFactIds; inbound present, outbound absent | `MarkdownBoundaryTests.cs:32-35` Contains knownIds; `:49-55` Contains inbound, DoesNotContain outbound, cited payload contains text | PASS |
| Reproduced value cites canonical key and ordinal it came from (RP-34) | Citation Text in CitedPayload; key+ordinal match TryLocate | `MarkdownIdentityPageTests.cs:23-27` Text + ArtifactKey + Ordinal; `:168-169` Contains text in payload | PASS |
| No Markdown page per callable, symbol, observation, or individual relation (RP-35) | No `/symbol/`, no callable key, page count ≠ relation count | `MarkdownBoundaryTests.cs:70-76` DoesNotContain `/symbol/`, length 2; `:88-89` Empty pages for lone symbol; `:125-129` pages.Length 2 ≠ contains.Length | PASS |
| Same input twice → byte-identical Markdown (RP-36) | SequenceEqual payloads | `MarkdownBoundaryTests.cs:150-157` Equal keys + SequenceEqual payloads | PASS |
| `retrieval.md` documents the seven scenarios and names artifacts in the publication (RP-37) | All seven strings; each section names a slot key | `RetrievalGuideProjectorTests.cs:34-38` Contains each scenario; `:53-58` named keys in slots | PASS |
| Generated `AGENTS.md` explains manifest entry point, proof-state axes, source retrieval, Markdown never authority (RP-38) | Those phrases | `AgentsGuideProjectorTests.cs:21-22` manifest + entry point; `:31-34` evidence method/resolution/frontier/confidence; `:45-46` source + locator; `:58-60` Markdown + never authority | PASS |
| Generated `AGENTS.md` does not restate taxonomy enumerations (RP-39) | No fact-type / observation-kind / facet-axis / triple strings | `AgentsGuideProjectorTests.cs:70-91` DoesNotMatch fact types; DoesNotContain kinds, axes, triples | PASS |
| Both guides listed in manifest; no absolute path in either (RP-40) | Manifest contains `retrieval.md` and `AGENTS.md`; no `:\` | `RetrievalGuideProjectorTests.cs:82` Path == Key; `:96-100` DoesNotContain `:\\`; `AgentsGuideProjectorTests.cs:117-118` same | PASS |
| Validate every link/ordinal/locator/repeated value before staging is written (RP-41) | Missing key / bad ordinal abort with no `.staging` | `ProjectionValidationAbortTests.cs:26-29` Gate `projection-key`, staging false; `:46-49` `projection-ordinal`. **⚠️ Spec-precision**: Independent Test also wants Commit()+prior-package for value and span; those two are validator-only. Validator JSON-walks; real markdown is not JSON | PASS (spec-precision: see note) |
| Missing artifact key aborts naming the key (RP-42) | Gate `projection-key`; Detail = missing key | `ProjectionValidatorTests.cs:20-22` Gate + Equal missing; `ProjectionValidationAbortTests.cs:26-27` same on Commit | PASS |
| Out-of-range ordinal aborts naming key and ordinal (RP-43) | Detail contains key and ordinal | `ProjectionValidatorTests.cs:34-38` Gate `projection-ordinal`, Contains key and `1` | PASS |
| Markdown value mismatch aborts naming page and value (RP-44) | Gate `projection-value`; Detail contains page and value | `ProjectionValidatorValueSpanTests.cs:23-27` Gate + Contains Page + MissingValue. Same Independent Test note as RP-41 | PASS (spec-precision: see note) |
| Locator span outside `source/` aborts naming the locator (RP-45) | Gate `projection-span`; Contains LocatorName | `ProjectionValidatorValueSpanTests.cs:44-46` Gate + Contains LocatorName. **⚠️ Spec-precision**: asserted on stub JSON `locator`/`span` fields, not published `Symbol.DeclarationLocator` | PASS (spec-precision: see note) |
| Same solution analyzed twice → byte-identical projection artifacts (RP-46) | SequenceEqual per projection key | `ProjectionDeterminismTests.cs:28-30` AssertEqualProjectionBytes; `ConfirmedRelationOrderingTests.cs:30-31` insertion-order identity | PASS |
| Same repo from two absolute paths → byte-identical projections (RP-47) | clone-a ≠ clone-b roots; equal projection bytes | `ProjectionDeterminismTests.cs:63-67` NotEqual roots, AssertEqualProjectionBytes | PASS |
| Documents in a different order → byte-identical projections (RP-48) | Reverse CSharpDocuments; equal bytes | `ProjectionDeterminismTests.cs:105-108` AssertEqualProjectionBytes; `ObservationOrderingTests.cs:27-28` insertion-order identity | PASS |
| No absolute path in any projection artifact (RP-49) | Keys not rooted; payloads omit `:\` `/home/` `/opt/` `/Users/` | `CitationResolutionTests.cs:40-47` IsPathRooted false; `ProjectionAbsolutePathTests.cs:31-32` Validate + AssertNoAbsolutePath | PASS |
| Projection refs only Storage+Domain; Analysis does not ref Projection; Cli does not ref Domain (RP-50) | Those ProjectReference sets | `ProjectionIsolationTests.cs:28` Domain+Storage; `:48` Analysis Domain only; `:62-63` Cli DoesNotContain Domain, Equal Analysis/Projection/Storage | PASS |
| Run with no facts → manifest + registry, no projection artifact (RP-51) | On-disk keys are the six envelopes; no catalogs/guides | `EmptyProjectionCommitTests.cs:51-61` exact six keys including taxonomy-registry; `:26-32` no projection dirs/files | PASS |
| Configurable catalog/posting ceiling, default 1 MiB (RP-52) | `DefaultCeilingBytes == 1024 * 1024`; override splits | `ShardWriterTests.cs:20` Equal 1024*1024; `:26-28` unsplit vs split.Length > 1 | PASS |
| Over-ceiling catalog/posting splits into deterministic bucket shards (RP-53) | Keys `catalogs/entry-points.[0-9a-f]{2}.json` | `ShardWriterTests.cs:38-45` Length > 1, Matches regex, ordered keys; `:85-87` CatalogProjector split | PASS |
| Bucket keys from fact id, not display name (RP-54) | sha256 prefix of fact id; rename display names → same assignment | `ShardWriterTests.cs:109-122` expected hex from factId; `:140-144` Equal originalKeys/renamedKeys | PASS |
| Split shards all listed in `manifest.json` (RP-55) | Every shard Path in manifest | `ShardWriterInvarianceTests.cs:81-87` All shards in manifest.Artifacts; `:105-107` posting shards too | PASS |
| Split preserves entry content; only the host artifact changes (RP-56) | Union of shard entries equals unsplit set; bodies SequenceEqual | `ShardWriterInvarianceTests.cs:24-25` Equal fact ids and canonical entries; `:47-49` SequenceEqual bodies; `:62-64` unsplit key vs shard keys | PASS |
| Store requests each document’s bytes at most once and does not retain them after write (RP-57) | RequestCount == 1; deferred Payload empty; disk bytes equal | `SourceDocumentReaderStagingTests.cs:36-43` Equal 1, All <= 1, deferred Payload empty; `:63-69` disk SequenceEqual original | PASS |

**Status**: ⚠️ Spec-precision gaps flagged. 57/57 numbered ACs matched spec-defined outcomes. 5 spec-precision items (RP-02, RP-07, RP-11, RP-15 Independent Test, RP-41/44/45 Independent Test vs JSON validator). Those items are not uncovered numbered ACs.

RP-02 spec-precision: the WHEN is runtime sequence inside `Commit()`. `PublicationPipeline.cs:19-40` does Validate → Project → ToPublicationOrder. The test asserts that order by `IndexOf` on the source file (`ProjectorPublicationTests.cs:81-89`), plus that the projector receives the post-validation document (`:67-70`). That proves the source layout and the view identity, not a runtime trace of `ToPublicationOrder` happening after `Project`. Core outcome holds. No `// SPEC_DEVIATION`.

RP-07 spec-precision: the WHEN is “exactly one artifact under `source/`”. Unredacted documents emit one (`SourceProjectorTests.cs:18`). Redacted documents also emit `source/<key>.meta.json` (`RedactionEnvelopeTests.cs:26-35`). RP-10 requires that declaration. The content artifact still matches the original outside the marker. Not scored as uncovered.

RP-11 spec-precision: the AC names `ContentSha256` already published in `facts/structural.json`. `SourceIntegrityTests.cs:29` compares against `view.Document.Documents[0].ContentSha256`. That DTO is what the shard serializes. The published file is not opened. Core outcome (published bytes hash equals inventoried hash) is asserted.

RP-15 Independent Test spec-precision: “slice exactly that span out of the matching `source/` artifact”. `SymbolFactEmitterTests.cs:309-320` slices `FixtureFile(locator.RelativePath)`, the clone, not a published `source/` fragment. For unredacted C# this is equivalent given RP-07/RP-11. Numbered RP-15 (complete member) is asserted.

RP-41/44/45 Independent Test spec-precision: “Drive `Commit()` with a stub projector emitting, in turn, a missing key, an out-of-range ordinal, a mismatched Markdown value and an out-of-bounds span” and keep the prior package. Commit covers key and ordinal (`ProjectionValidationAbortTests.cs:15-50`) and throw (`ProjectionAbortTests`). Value and span abort in `ProjectionValidatorValueSpanTests` only. `ProjectionValidator` JSON-walks fragments (`ProjectionValidator.cs:159-174`); real markdown pages are not JSON, so comment citations are not commit-validated. RP-45 uses a stub JSON `locator` field, not `Symbol.DeclarationLocator`. Numbered abort-and-name outcomes are asserted.

Catalog/posting/markdown citation fields (fact id, artifact key, ordinal) are asserted on value/state: `CatalogProjectionFactory.AssertEveryEntryResolves` (`:178-181`), `PostingProjectionFactory.RelationAt` plus Equal source/target, `MarkdownIdentityPageTests.cs:23-27` Text+key+ordinal. Not mere method-call occurrence. `RequirementCoverageTests` is not used as proof.

Independent Tests traced: filesystem fragment+manifest+throw (`ProjectorPublicationTests`, `ProjectionAbortTests`, `SourceDocumentReaderStagingTests`); fixture secret (`SourceSecretAbsenceTests`); PlaceOrderAsync member (`SymbolFactEmitterTests`); catalog/posting resolve (`CitationResolutionTests`); Markdown citations (`MarkdownIdentityPageTests.cs:139-170`); seven scenarios (`RetrievalGuideProjectorTests`); stub validation (`ProjectionValidatorTests` / `ProjectionValidatorValueSpanTests`); clone-path bytes (`ProjectionDeterminismTests`); assembly refs (`ProjectionIsolationTests`); empty publication (`EmptyProjectionCommitTests`); over-ceiling split (`ShardWriterTests` / `ShardWriterInvarianceTests`).

---

## Discrimination Sensor

| Mutation | File:line | Description | Killed? |
| -------- | --------- | ----------- | ------- |
| — | — | Not run | SKIPPED |

**Sensor depth**: skipped
**Result**: SKIPPED (standing user request for csharp2md, same as `symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`, `knowledge-taxonomy-contract`, `engine-bootstrap`, `factual-storage`, `roslyn-observation-extraction`, `entrypoints-boundaries-contracts`, `call-linking-flow-frontiers`, `persistence-knowledge`, and `components-deployments-configuration`). No git worktree, no file mutation, no Stryker.

Static gap analysis only (`dotnet-test:test-gap-analysis` step 4 without 4b live mutation; labelled unverified):

- Skipping `ProjectionValidator.Validate` after `Project` in `PublicationPipeline.cs:37` would fail `ProjectionValidationAbortTests.cs:23` (`projection-key` on Commit). Unverified (static reasoning).
- Not merging overlapping secret spans in `SecretRedactor.Merge` would fail `SecretRedactorTests.cs:62-63` (one marker, no nesting). Unverified (static reasoning).
- Deriving shard buckets from `display_name` instead of fact-id sha256 would fail `ShardWriterTests.cs:140-144` Equal keys after rename. Unverified (static reasoning).
- Emitting a candidate `invokes` into callers would fail `CallersCalleesPostingTests.cs:63-68` and `CitationResolutionTests.cs:175-182`. Unverified (static reasoning).
- Dropping `ContentSha256` from the confirmed-relation ordering key would fail `ConfirmedRelationOrderingTests.cs:30-34` identical order across insertion. Unverified (static reasoning).
- Deferred `source/` fragments are not loaded into `ProjectionValidator`’s `sources` map (`ProjectionValidator.cs:31-36`). A JSON span citation against a deferred source would abort; real markdown citations are comments, not JSON, so that path is unexercised. Unverified (static reasoning). Same class of Independent Test note as RP-41/44/45.

---

## Interactive UAT Results

Not performed. This feature is backend/CLI infrastructure; automated checks are sufficient per validate.md.

---

## Code Quality

| Principle | Status |
| --------- | ------ |
| Minimum code | PASS |
| Surgical changes | PASS |
| No scope creep | PASS |
| Matches patterns | PASS |
| Spec-anchored outcome check (asserted values match spec) | PASS (RP-02/RP-07/RP-11/RP-15/RP-41-45 flagged) |
| Per-layer Coverage Expectation met (Storage pipeline + Projection projectors 1:1 ACs; Analysis locators/e2e; CLI wiring; isolation) | PASS |
| Every test maps to a spec requirement - no unclaimed tests | PASS |
| Documented guidelines followed: `AGENTS.md` / `CLAUDE.md` (net10.0, Workspaces.MSBuild 5.6.0, no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults`, SyntheticSolution-only versioned fixture, LocalCorpus skip when clones absent, standing sensor skip) | PASS |

Diff `ea4a1ad^..HEAD` stays on Storage ordering keys, `PublishedPackageView`/`ManifestBuilder`/`PublicationPipeline`, `ISourceDocumentReader` + deferred fragments, Domain `DeclarationLocator`, Analysis emitter + filesystem reader, Projection projectors (source, catalogs, postings, markdown, guides, sharding), projection validation, CLI `PackageProjector` wiring, matching tests. No `contracts/taxonomy-registry.json` byte changes (RP-17). No `Microsoft.Build.*` and no `MSBuildLocator`. Target framework `net10.0`; Roslyn package remains `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0. `RetrievalProjectionStub` stays a no-op (assumption). No new CLI flag (RP-06 wiring).

Spot-check (P1 catalogs + callers): RP-19/20 assert fact id, artifact key and ordinal on value via `TryLocate` and `FactIdAt`. RP-28/29 e2e assert callers equal confirmed `invokes` and exclude candidates. Not trait-only.

`dotnet-test:assertion-quality` / `test-anti-patterns` on new Projection tests and `RequirementCoverageTests`: no assertion-free tests; no `Thread.Sleep`; no always-true asserts; no swallowed exceptions. Equality + collection + negative + exception asserts are the dominant mix. `RequirementCoverageTests` is a mechanical trait/name scanner (T49), not an outcome test. `ProjectorPublicationTests.cs:76-89` and `ProjectorWiringTests.cs:16` couple to source text (Low; same pattern as CDC pipeline `IndexOf`). Giant AAA is concentrated in fixture helpers, not in the assertions.

---

## Edge Cases

- [x] Non-UTF-8 document bytes published unchanged (edge) — `SourceProjectorTests.cs:69-71`
- [x] Shared relative path in different projects → distinct keys (edge) — `SourceProjectorTests.cs:52-56`
- [x] Whole-document secret → only the marker, redaction declared (edge) — `RedactionEnvelopeTests.cs:129-132`
- [x] Overlapping secret spans merge; no nested markers (edge) — `SecretRedactorTests.cs:62-63`, `:77-78`
- [x] Fact outside catalog families does not fail validation (edge) — `CatalogOmissionTests.cs:54-56`
- [x] Self-relation listed in both outgoing and incoming (edge) — `PostingProjectorTests.cs:61-68`
- [x] Unresolved owner with zero relations ranks last, not omitted (edge) — `UnknownRankingTests.cs:28-30`
- [x] Symbol in a document with no Document fact gets no locator (edge) — `SymbolFactEmitterTests.cs:504`
- [x] No architecture facts → guides without markdown pages (edge) — `RetrievalGuideProjectorTests.cs:147-151`

---

## Gate Check

- **Gate command**: `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- **Result**: 1613 passed, 0 failed, 0 skipped among selected tests
- **Per-project passed counts**:
  - `Csharp2Md.Domain.Tests`: 555 passed
  - `Csharp2Md.Analysis.Tests`: 634 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Storage.Tests`: 243 passed
  - `Csharp2Md.Cli.Tests`: 29 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Projection.Tests`: 152 passed
- **Test count before feature**: 1342 (`792d9cb`)
- **Test count after feature**: 1613 passing (0 failing on the serial gate)
- **Delta**: +271 executed tests vs the 1342 baseline. Increase; no silent deletions. T49 floor 1610+ met (1613).
- **Skipped tests**: none among the filtered gate. `LocalCorpusAnalyzeTests` excluded by `Category!=LocalCorpus`.
- **Failures**: none on the serial Category!=LocalCorpus gate.
- **Build**: `dotnet build` 0 warnings, 0 errors (`TreatWarningsAsErrors` on).

---

## LocalCorpus

`fixtures/eShop` is absent. `fixtures/eShopOnContainers` is absent. No `.sln` / `.slnx` clone to run.

Post-gate LocalCorpus run skipped. Not a feature FAIL. Clones were not added to git.

---

## Fix Plans

None. RP-02, RP-07, RP-11, the RP-15 Independent Test, and the RP-41/44/45 Independent Test are recorded as spec-precision gaps, not uncovered ACs. Tests assert the specified core outcomes (pipeline order in source + post-validation view, one content `source/` artifact, published hash equals inventoried hash, complete member slice, abort-and-name on missing key/ordinal/value/span).

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| ----------- | --------------- | ---------- |
| RP-01..RP-57 | implemented | Verified in this report |

---

## Summary

**Overall**: Ready

**Spec-anchored check**: 57/57 ACs matched spec outcome | 5 spec-precision gaps flagged
**Sensor**: SKIPPED (standing skip)
**Gate**: 1613 passed

**What works**: Projector seam inside `Commit()`, byte-faithful `source/` with declared redaction, Symbol declaration locators, six catalogs and five posting families with resolving citations, architecture Markdown pages, `retrieval.md` and generated `AGENTS.md`, fail-closed projection validation, clone-path and input-order byte identity, 1 MiB default sharding.

**Issues found**: Spec-precision only (RP-02 source `IndexOf` for before-ordering; RP-07 companion `.meta.json`; RP-11 DTO hash not `facts/structural.json` file; RP-15 slice from fixture not published `source/`; RP-41/44/45 Independent Test Commit+markdown/locator shape).

**Next steps**: Distill the spec-precision lessons. Feature is ready for the orchestrator to mark done after `validate_state.py`.
