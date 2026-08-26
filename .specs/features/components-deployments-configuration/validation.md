# Components, Deployments and Configuration Validation

**Date**: 2026-08-26
**Spec**: `.specs/features/components-deployments-configuration/spec.md`
**Diff range**: `32e6a10^..HEAD` (`32e6a10` … `5bfb86b`)
**Verifier**: independent sub-agent (author ≠ verifier)
**Result**: PASS

T1–T42 each have a Conventional Commit on `feat/components-deployments-configuration` (`32e6a10` … `5bfb86b`). Implementation starts at T1 `32e6a10`. HEAD is T42 `5bfb86b`. `tasks.md` Done-when boxes are all checked. Header still says “Approved”; the checkboxes and commits are the completion evidence.

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1 | Done | `32e6a10` `fix(tests): allow CLLF and CDC traits in the Analysis requirement guard` |
| T2 | Done | `ad8e988` `fix(tests): allow CLLF and CDC traits in the Storage requirement guard` |
| T3 | Done | `a184153` `fix(tests): assert post-5B open frontiers in the CLI package test` |
| T4 | Done | `a823fa2` `test(fixtures): add an entry point to the Acme.Orders composition root` |
| T5 | Done | `1cdbbb5` `test(fixtures): make Acme.Orders an application project` |
| T6 | Done | `6a678aa` `test(fixtures): add an entry point to Acme.Payments` |
| T7 | Done | `e27c867` `test(fixtures): make Acme.Payments an application project` |
| T8 | Done | `2ff22eb` `test(fixtures): add the Acme.Orders.Worker source` |
| T9 | Done | `68a956e` `test(fixtures): add the Acme.Orders.Worker project` |
| T10 | Done | `be3692f` `test(fixtures): register Acme.Orders.Worker in the Orders solution` |
| T11 | Done | `57e75e4` `test(fixtures): add a second configuration file` |
| T12 | Done | `f641945` `feat(analysis): classify appsettings files as configuration documents` |
| T13 | Done | `4ceef13` `feat(analysis): add authorized root and configuration documents to the pipeline context` |
| T14 | Done | `e5c3820` `feat(analysis): publish the authorized root and configuration documents` |
| T15 | Done | `7fd1c5e` `feat(analysis): emit project metadata observations` |
| T16 | Done | `d63452b` `feat(analysis): read appsettings documents into the ledger` |
| T17 | Done | `ee0699c` `feat(analysis): register the project metadata and configuration emitters` |
| T18 | Done | `c0e39b5` `feat(analysis): define the topology model records` |
| T19 | Done | `64636a4` `feat(analysis): compute project graph and application reach` |
| T20 | Done | `92ad061` `feat(analysis): apply the component grouping rule` |
| T21 | Done | `7d4d41a` `feat(analysis): emit components, deployment units and inclusion relations` |
| T22 | Done | `0d3a539` `feat(analysis): rewrite ComponentPass onto the topology builder` |
| T23 | Done | `5ffd2a0` `feat(analysis): add a symbol-to-component lookup` |
| T24 | Done | `05f0c19` `refactor(analysis): resolve entry-point components by symbol` |
| T25 | Done | `86ee323` `refactor(analysis): resolve boundary components by symbol` |
| T26 | Done | `9abccd6` `feat(analysis): define the configuration model records` |
| T27 | Done | `d20aa9e` `feat(analysis): build the declared configuration key table` |
| T28 | Done | `1272b61` `feat(analysis): correlate configuration keys with consuming facts` |
| T29 | Done | `10c6cef` `feat(analysis): decide targets promotion from configuration` |
| T30 | Done | `742ef92` `feat(analysis): allow a superseded candidate to be removed` |
| T31 | Done | `9ac64e7` `feat(analysis): emit configuration bindings and configured-by relations` |
| T32 | Done | `ed444a0` `feat(analysis): promote targets relations proven by configuration` |
| T33 | Done | `f67c59e` `feat(analysis): add the configuration classifier pass` |
| T34 | Done | `7ec752a` `feat(analysis): register the configuration pass` |
| T35 | Done | `7697156` `feat(analysis): publish component coverage counts` |
| T36 | Done | `6dc3673` `feat(analysis): publish configuration coverage counts` |
| T37 | Done | `540d117` `test(analysis): assert the Orders solution topology end to end` |
| T38 | Done | `5698dac` `test(analysis): assert per-solution grouping isolation` |
| T39 | Done | `8f46a44` `test(analysis): assert topology and configuration determinism` |
| T40 | Done | `1f272bb` `test(analysis): assert configuration security and isolation invariants` |
| T41 | Done | `faad517` `test(storage): round-trip deployment and configuration facts` |
| T42 | Done | `5bfb86b` `test(analysis): reconcile fixture-driven assertions with the 5D ground truth` |

No blocked or partial tasks. T37's Done-when prose still says ShippingService is confirmed `targets`; T42 and the current fixture follow spec CDC-45 (`appsettings.Development.json` declares `Services:CatalogService`, not `ShippingService`). Tests match the spec, not the stale Done-when sentence.

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| WHEN pipeline compiles a project THEN one Configuration observation owned by that Project, located in `.csproj`, `output-kind` is `application` or `library` (CDC-01) | Orders `application`; Shared.Contracts `library`; WinExe `application` | `ProjectMetadataEmitterTests.cs:27` `output-kind`/`application`; `:54` `library`; `:81` WinExe `application`; `ObservationExtractionStageIntegrationTests.cs:32` `application` | PASS |
| WHEN compiled project declares in-solution ProjectReference THEN one observation with `project-reference` = referenced logical path (CDC-02) | `Acme.Shared.Contracts/Acme.Shared.Contracts.csproj` | `ProjectMetadataEmitterTests.cs:107-109` that path; `ObservationExtractionStageIntegrationTests.cs:35-37` same | PASS |
| Every project-metadata observation `evidence_method = configured` (CDC-03) | `EvidenceMethod.Configured` | `ProjectMetadataEmitterTests.cs:29` and `:110` `ExtractionMethod`; `ObservationExtractionStageIntegrationTests.cs:33` and `:96` | PASS |
| WHEN compiled under more than one TFM/variant THEN each distinct output-kind and project-reference pair yields exactly one observation (CDC-04) | One identity set, not duplicated | `ProjectMetadataEmitterTests.cs:136-139` re-emit equal identities. **⚠️ Spec-precision**: the WHEN is multi-TFM; the test re-invokes the emitter on a single-TFM fixture | PASS (spec-precision: see note) |
| WHEN a listed project produces no compilation THEN no project-metadata observation; missing-project/compile-failure diagnostics unchanged (CDC-05) | Acme.Broken empty metadata; diagnostics equal before/after | `ProjectMetadataEmitterTests.cs:155` `unresolvable-sdk` present before; `:161` empty metadata; `:162-163` diagnostics tuples equal; `ObservationExtractionStageIntegrationTests.cs:42` Broken empty | PASS |
| `project-reference` ordinals derive from ordinal-sorted referenced logical paths (CDC-06) | output-kind ordinal 1; Aardvark then Zebra at 2 and 3 | `ProjectMetadataEmitterTests.cs:194-199` ordinals 1/2/3 and Aardvark then Zebra | PASS |
| WHEN reference is not analyzed in this solution THEN no `project-reference` observation and a diagnostic naming the referencing project and the unanalyzed reference (CDC-07) | No Ghost observation; `unanalyzed-project-reference` diagnostic | `ProjectMetadataEmitterTests.cs:244` empty refs; `:247-248` code + `Ghost` in IdentityOrKey. **⚠️ Spec-precision**: IdentityOrKey/message name the unanalyzed path only, not the referencing project | PASS (spec-precision: see note) |
| No type under `Csharp2Md.Analysis.Classification` references `Microsoft.CodeAnalysis` for topology (CDC-08) | No CodeAnalysis member types on classification surface | `ConfigurationIsolationTests.cs:120-142` exported names absent; `offending is null`; `TopologyModelTests.cs:114` topology types leak no Roslyn | PASS |
| WHEN `output-kind=application` THEN exactly one Component named by that project's logical path (CDC-09) | `App/App.csproj`; Orders e2e three named components | `TopologyModelBuilderTests.cs:222` name; `ComponentPassTests.cs:101` name; `TopologyIntegrationTests.cs:31-34` three names including Worker | PASS |
| WHEN a library is reached by exactly one application THEN group into that application's Component; no separate Component (CDC-10) | Payments: one component `Acme.Payments/...`; no Shared.Contracts component | `TopologyModelBuilderTests.cs:169-172` App grouping, no Lib component; `TopologyIsolationTests.cs:20-25` Payments single component | PASS |
| WHEN a library is reached by two or more applications THEN exactly one Component named by the library path (CDC-11) | Shared `Acme.Shared.Contracts/...` in Orders | `TopologyModelBuilderTests.cs:195-198` Shared evidence + name; `TopologyIntegrationTests.cs:34` Contains Shared.Contracts | PASS |
| WHEN a library is reached by no application THEN exactly one Component named by the library path (CDC-12) | Isolated own component, Unreached | `TopologyModelBuilderTests.cs:122-125` Isolated name + Unreached; `ComponentPassTests.cs:198-204` Isolated component | PASS |
| Reachability is transitive across `project-reference` observations (CDC-13) | App reaches Mid+Leaf; diamond Shared private-use; cycle terminates | `TopologyModelBuilderTests.cs:49-54` App groups app/mid/leaf; `:78-82` diamond Shared inside App; `:102-105` cycle three projects | PASS |
| Component.Owners are exactly observed Symbols in grouped projects, ordinal-sorted by fact id (CDC-14) | Observed owners only, sorted; unused excluded | `TopologyEmitterTests.cs:35-39` sorted also+host; `ComponentPassTests.cs:253-254` invoked in, unused out | PASS |
| WHEN symbol is an owner THEN confirmed `belongs-to` Symbol→Component, `evidence_method=semantic`, derived from that symbol's observations (CDC-15) | Kind BelongsTo; source=symbol; chain contains the symbol's observations | `TopologyEmitterTests.cs:82-88` Single belongs-to, source, taxonomy Semantic, both observation identities; `ComponentPassTests.cs:327-331` Payments contracts symbol → Payments component. EvidenceMethod is a Create-time guard (`TopologyEmitter.cs:46`) | PASS |
| WHEN symbol owns no observation THEN not in Owners and no `belongs-to` (CDC-16) | Unused absent from owners and relations | `TopologyEmitterTests.cs:58-61` DoesNotContain unused | PASS |
| Each Symbol is source of at most one `belongs-to` (CDC-17) | One belongs-to per host | `TopologyEmitterTests.cs:89` `belongs.Count(...) == 1` | PASS |
| WHERE ledger has no project-metadata, emit no Component, DeploymentUnit, belongs-to or included-in, and do not throw (CDC-18) | Empty model / zero pass result | `TopologyModelBuilderTests.cs:24-27` empty collections; `ComponentPassTests.cs:55-62` and `:77-81` zero counts, empty facts/relations | PASS |
| WHEN `output-kind=application` THEN exactly one DeploymentUnit named by that logical path (CDC-19) | Two DUs in Orders; one in Payments | `ComponentPassTests.cs:276-282` two DU names; `:314` Payments single DU; `TopologyIntegrationTests.cs:35` `Assert.Equal(2, ...DeploymentUnits.Length)` | PASS |
| WHEN a component is reached by an application THEN confirmed `included-in` Component→DU, `evidence_method=configured`, derived from output-kind/project-reference (CDC-20) | Kind IncludedIn; configured; chain = reach evidence | `TopologyEmitterTests.cs:108-114` Configured minimum, chain equal, fact types; `ComponentPassTests.cs:345` own DU only | PASS |
| WHEN a shared component is reached by two or more applications THEN one `included-in` per reaching application (CDC-21) | Shared source of exactly two included-in, one per DU | `TopologyIntegrationTests.cs:44-47` length 2 and DU id set; `ComponentPassTests.cs:177-180` count 2 | PASS |
| WHEN a component is reached by no application THEN UnresolvedRecord `included-in` cause `insufficient evidence`, no confirmed included-in (CDC-22) | IncludedIn / InsufficientEvidence; no confirmed | `ComponentPassTests.cs:205-212` kind/cause/source; DoesNotContain confirmed; `TopologyEmitterTests.cs:182-184` same | PASS |
| WHEN solution has no application THEN no DeploymentUnit and every component produces CDC-22 unresolved (CDC-23) | Empty DUs; two unresolved; no confirmed included-in | `ComponentPassTests.cs:233-235` empty DUs, 2 unresolved, empty included-in; `TopologyModelBuilderTests.cs:241-248` empty deployments | PASS |
| DeploymentUnit created only from `output-kind`, never from component/folder/assembly/prefix (CDC-24) | No application → no DU; application DU named by logical path | `ComponentPassTests.cs:233` empty DUs for libraries; `TopologyModelBuilderTests.cs:224-226` DU name = App path and Application = app.Id | PASS |
| WHEN inventoried `appsettings*.json` inside authorized root THEN supported configuration document, no `unsupported-document` (CDC-25) | Both fixture files in ConfigurationDocuments; no unsupported for them | `InventoryStageTests.cs:138-148` both relative paths, DoesNotContain unsupported; `DocumentInventoryTests.cs:284-286` AssertConfigurationDocument; `TopologyIntegrationTests.cs:164-168` no unsupported for appsettings.json | PASS |
| WHEN adapter reads a supported document THEN one Configuration observation per leaf with colon-joined `key` (CDC-26) | `Services:PaymentService`; array `Hosts:0`/`Hosts:1` | `ConfigurationDocumentReaderTests.cs:28` key; `:42` Hosts:0/1; `ObservationExtractionStageIntegrationTests.cs:46` PaymentService key | PASS |
| Configuration observation SHALL NOT carry the leaf value, nor any entry other than CDC-29/CDC-30 (CDC-27) | `visible-value` absent from payload | `ConfigurationDocumentReaderTests.cs:55-58` DoesNotContain visible-value; key still `Name` | PASS |
| WHEN leaf is a suspected secret THEN SuspectedSecretEvidence (document, span, hash, redacted excerpt) and value absent from facts/observations/relations/candidates/frontiers/diagnostics (CDC-28) | Redacted excerpt; secret absent | `ConfigurationDocumentReaderTests.cs:146-158` no address/value/secret; excerpt contains `***`; `ConfigurationIsolationTests.cs:42-50` secrets absent from all payloads; `:77-81` document/span/excerpt. Hash is a Create-time field (`SuspectedSecretEvidence.cs:46`) | PASS |
| `${VAR}` / `$VAR` / `%VAR%` → `resolution=dynamic`; other non-empty → `literal`; empty/null → `unknown` (CDC-29) | Those three values | `ConfigurationDocumentReaderTests.cs:73` All dynamic; `:88` literal; `:102` unknown | PASS |
| Absolute URI and not a secret → `address` URI; otherwise no `address` (CDC-30) | Payment URI present; Notification/OrdersDb omit address | `ConfigurationDocumentReaderTests.cs:115` address; `:128-130` omit; `:148` secret omits address; `:241-251` fixture shapes | PASS |
| WHEN document cannot be parsed THEN `malformed-configuration-document` naming the document, no observations, do not abort (CDC-31) | Count 0; empty observations; IdentityOrKey = relative path | `ConfigurationDocumentReaderTests.cs:171-177` count 0, empty, code, `App/appsettings.json`, not rooted | PASS |
| Every configuration-document observation `evidence_method=configured` and document content hash (CDC-32) | Configured + hash equals file hash | `ConfigurationDocumentReaderTests.cs:192-193` ExtractionMethod and DocumentHash; `ObservationExtractionStageIntegrationTests.cs:50-51` | PASS |
| Occurrence ordinals from ordinal-sorted key paths (CDC-33) | Aardvark ordinal 1, Zebra 2 | `ConfigurationDocumentReaderTests.cs:207-210` | PASS |
| Adapter reads only inventoried documents; SHALL NOT resolve a path that escapes the authorized root (CDC-34) | Escape throws; empty observations | `ConfigurationDocumentReaderTests.cs:274-278` InvalidOperationException contains `escapes the authorized root`; `InventoryStageTests.cs:123` AuthorizedRoot equals path-guard root | PASS |
| WHEN a configuration document declares a key THEN exactly one ConfigurationBinding bound to the Component grouping that project, key as ConfigurationKey (CDC-35) | BoundFact = Orders component; keys OrdersDb and PaymentService | `ConfigurationEmitterTests.cs:51-54` BoundFact/role/value; `TopologyIntegrationTests.cs:53-62` those keys + CatalogService from Development.json (no-merge second file) | PASS |
| WHEN ConfigurationBinding created THEN confirmed `configured-by` Component→binding, `evidence_method=configured` (CDC-36) | Source=component, target=binding, Configured | `ConfigurationEmitterTests.cs:115-118` source/target/taxonomy Configured; `TopologyIntegrationTests.cs:79-84` EvidenceMethod `Configured` | PASS |
| WHEN a Symbol-owned Configuration observation key equals a declared key THEN confirmed `configured-by` Symbol→binding (CDC-37) | ConfigureHost → OrdersDb binding | `TopologyIntegrationTests.cs:85-91` source contains ConfigureHost, FactType Symbol, target OrdersDb binding; `ConfigurationModelBuilderTests.cs:139` symbol edge | PASS |
| WHEN DataStore name equals last segment of a `ConnectionStrings` key THEN confirmed `configured-by` DataStore→binding (CDC-38) | OrdersDb store → ConnectionStrings:OrdersDb | `TopologyIntegrationTests.cs:94-99` store.Name `OrdersDb` and relation; `ConfigurationModelBuilderTests.cs:142` store edge | PASS |
| WHEN outbound BoundaryOperation client name equals last segment of a `Services` key THEN confirmed `configured-by` operation→binding (CDC-39) | PaymentService operation → Services:PaymentService | `TopologyIntegrationTests.cs:106-110` source=paymentOperation; `ConfigurationModelBuilderTests.cs:143` boundary edge | PASS |
| Every `configured-by` carries `evidence_method=configured` and a chain containing the declaring configuration observation (CDC-40) | Configured + declaring identity in DerivedFrom | `ConfigurationEmitterTests.cs:117-119` chain equal and Contains evidence; `:187` symbol edge Contains paymentEvidence | PASS |
| Key matching in CDC-37/38/39 is exact ordinal; no prefix/suffix/case-insensitive `configured-by` (CDC-41) | Prefix Payment, suffix PaymentServiceClient, folded paymentservice, ordersdb produce no consumer edge | `ConfigurationModelBuilderTests.cs:196-199` DoesNotContain BoundaryOperation/DataStore/Symbol edges | PASS |
| WHEN a Symbol Configuration key is undeclared THEN UnresolvedRecord `configured-by` `insufficient evidence`, no confirmed relation (CDC-42) | ConfiguredBy / InsufficientEvidence; no confirmed | `ConfigurationEmitterTests.cs:213-219` kind/cause/source; DoesNotContain confirmed | PASS |
| WHEN candidate `targets` matches a `Services` key with `resolution=literal` and `address` THEN confirmed `targets` `configured` and superseded candidate removed (CDC-43) | PaymentService confirmed; no remaining candidate | `TopologyIntegrationTests.cs:125-130` Contains confirmed, DoesNotContain candidate; `ConfigurationEmitterTests.cs:247-254` source/target, RemoveCandidate, taxonomy Configured. Fixture `appsettings.Development.json` uses `CatalogService`, not `ShippingService` | PASS |
| WHEN matching observation is `resolution=dynamic` THEN candidate remains, no confirmed `targets`, OpenFrontier on originating occurrence (CDC-44) | NotificationService candidate + frontier FurtherContinuationObserved | `TopologyIntegrationTests.cs:132-144` Single candidate, DoesNotContain confirmed, frontier cause + occurrence match; `ConfigurationEmitterTests.cs:279-283` candidate kept, frontier = csharp.Identity | PASS |
| WHEN no `Services` key matches THEN candidate unchanged, no confirmed `targets`, no OpenFrontier (CDC-45) | ShippingService candidate only | `TopologyIntegrationTests.cs:146-158` Single candidate, DoesNotContain confirmed, DoesNotContain matching frontier; `ConfigurationModelBuilderTests.cs:300` TargetOutcome.Leave | PASS |
| Confirmed `targets` evidence chain contains both the C# observation and the configuration observation (CDC-46) | Both identities in DerivedFrom | `ConfigurationEmitterTests.cs:250-252` Contains csharp.Identity and declaring; `ConfigurationModelBuilderTests.cs:262-263` both | PASS |
| Classifier SHALL NOT create an ExternalSystem; only confirm/keep/refuse an existing one (CDC-47) | ExternalSystem count unchanged | `ConfigurationEmitterTests.cs:255` equal count; `ConfigurationModelBuilderTests.cs:264-265` equal, `Assert.Equal(1, externalsBefore)` | PASS |
| No confirmed `targets` from prefix/suffix/case-insensitive/path similarity (CDC-48) | Payment / paymentservice → Leave | `ConfigurationModelBuilderTests.cs:335-336` All Leave | PASS |
| WHEN same solution is analyzed from two clone paths THEN components, DUs, bindings, relations, candidates, frontiers, unresolved identical and canonical bytes identical (CDC-49) | Equal identity sets; SequenceEqual payloads | `TopologyDeterminismTests.cs:59-74` NotEmpty + AssertEqualIdentities + SequenceEqual per key | PASS |
| No fact/observation/relation/candidate/frontier/diagnostic contains an absolute path (CDC-50) | Clone root, slash form, drive prefix absent | `ConfigurationIsolationTests.cs:106-110` AssertNoClonePath + ScanNode; `TopologyDeterminismTests.cs:77-78` AssertNoAbsolutePath | PASS |
| WHEN classifier runs twice over the same ledger THEN identical facts/relations/candidates/frontiers/unresolved (CDC-51) | Equal identities and canonical bytes across retries | `TopologyDeterminismTests.cs:96-107` AssertEqualIdentities + SequenceEqual | PASS |
| Fixture connection-string password and every suspected-secret value absent from every committed package artifact (CDC-52) | `appsettings-fixture-secret` and sibling secrets absent | `ConfigurationIsolationTests.cs:42-50` Assert.All DoesNotContain SecretValues | PASS |
| `contracts/taxonomy-registry.json` remains byte-identical; registry drift gate passes (CDC-53) | Published bytes SequenceEqual committed file | `ConfigurationIsolationTests.cs:160-162` SequenceEqual; `AnalyzePackageWriteTests.cs:76-78` same | PASS |
| Analysis SHALL NOT reference Storage/Projection/Cli; Cli SHALL NOT reference Domain (CDC-54) | ProjectReference names | `ConfigurationIsolationTests.cs:174-177` DoesNotContain those three; `:189` Cli DoesNotContain Domain | PASS |
| WHEN passes registered THEN ClassificationAndPromotionStage order is Components, Entry points, Boundaries, Contracts, Persistence, Configuration, Relations, Invokes, Executes; aggregate counts include their output (CDC-55) | Source constructor order; runtime names; classification counts ≥ bindings/relations | `PipelineStagesTests.cs:84-101` IndexOf order; `ComposabilityTests.cs:51-53` name list; `:102-107` FactCount/RelationCount include configuration | PASS |
| WHEN classification completes THEN diagnostics envelope carries component-coverage: projects grouped, applications found, components with no DU (CDC-56) | Those three counts in message | `TopologyEmitterTests.cs:204-209` code `component-coverage`, those three phrases. **⚠️ Spec-precision**: Independent Test names published `diagnostics.json` after Acme.Orders analyze; e2e `TopologyIntegrationTests` reads that file only for `unsupported-document` | PASS (spec-precision: see note) |
| WHEN classification completes THEN diagnostics envelope carries configuration-coverage: declared keys, keys bound, keys read in C# but not declared (CDC-57) | Those three counts | `ConfigurationEmitterTests.cs:332-337` code `configuration-coverage`, those three phrases. Same Independent Test note as CDC-56 | PASS (spec-precision: see note) |
| Coverage records publish counts only, no percentage or recall (CDC-58) | No `%` / percent / pass\|fail\|verdict\|recall | `TopologyEmitterTests.cs:210-212`; `ConfigurationEmitterTests.cs:338-340` DoesNotMatch `%`, DoesNotContain percent | PASS |

**Status**: ⚠️ Spec-precision gaps flagged. 58/58 numbered ACs matched spec-defined outcomes. 3 spec-precision items (CDC-04, CDC-07, P2 Independent Test vs unit snapshot). Those items are not uncovered numbered ACs.

CDC-04 spec-precision: the WHEN is “compiled under more than one target framework or analysis variant”. The fixture is single-TFM (`net10.0`). The test re-emits on the same ledger and asserts identity equality (`ProjectMetadataEmitterTests.cs:136-139`). That proves emitter idempotence (also CDC-51-adjacent), not a two-TFM compile. Core outcome “exactly one observation per distinct pair” is asserted. No `// SPEC_DEVIATION`.

CDC-07 spec-precision: spec requires the diagnostic to name the referencing project and the unanalyzed reference. `ProjectMetadataEmitter.cs:214-217` records IdentityOrKey = the unanalyzed logical path and a message that quotes that path only. Tests assert `Ghost` in IdentityOrKey (`ProjectMetadataEmitterTests.cs:248`). The no-observation half is asserted. Not scored as an uncovered AC.

P2 Independent Test spec-precision: “assert `diagnostics.json` carries both coverage records with non-zero numerators and no `%`”. T35/T36 Done-when require unit tests on a hand-built model. Those unit tests assert the snapshot diagnostic codes, the three counts, and no `%`. TopologyIntegrationTests reads published `diagnostics.json` only to exclude `unsupported-document`. Not scored as an uncovered numbered AC (CDC-56/57/58 still have `file:line` outcome evidence).

EvidenceMethod.Semantic / Configured on belongs-to, included-in, configured-by, and confirmed targets is a Create-time guard. Tests assert classifier identity, source/target, `derived_from`, and taxonomy minimum, plus Storage wire `EvidenceMethod` (`TopologyRecordRoundTripTests.cs:138-140`). Same pattern as EBC-07 / ROSE-17 / PK-32.

Independent Tests traced: Orders grouping (`TopologyIntegrationTests.cs:31-47`, `ComponentPassTests.cs:268-299`); Payments isolation (`TopologyIsolationTests.cs:20-41`); PaymentService/NotificationService/ShippingService (`TopologyIntegrationTests.cs:125-158`); clone-path bytes (`TopologyDeterminismTests.cs:59-74`); password absence (`ConfigurationIsolationTests.cs:42-50`); ConfigureHost/OrdersDb/PaymentService configured-by (`TopologyIntegrationTests.cs:79-110`). Fixture `appsettings.Development.json` currently declares `Services:CatalogService`, not `ShippingService`, so CDC-45 holds.

---

## Discrimination Sensor

| Mutation | File:line | Description | Killed? |
| -------- | --------- | ----------- | ------- |
| — | — | Not run | SKIPPED |

**Sensor depth**: skipped
**Result**: SKIPPED (standing user request for csharp2md, same as `symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`, `knowledge-taxonomy-contract`, `engine-bootstrap`, `factual-storage`, `roslyn-observation-extraction`, `entrypoints-boundaries-contracts`, `call-linking-flow-frontiers`, and `persistence-knowledge`). No git worktree, no file mutation, no Stryker.

Static gap analysis only (`dotnet-test:test-gap-analysis` step 4 without 4b live mutation; labelled unverified):

- Promoting ShippingService because Development.json still declared `Services:ShippingService` would fail `TopologyIntegrationTests.cs:146-158` (candidate remains, no confirmed, no frontier). T42 renamed that key to `CatalogService`. Unverified (static reasoning).
- Leaving the superseded PaymentService candidate would fail `TopologyIntegrationTests.cs:128-130` `Assert.DoesNotContain` candidates. Unverified (static reasoning).
- Emitting `address` for the OrdersDb connection string would fail `ConfigurationDocumentReaderTests.cs:148` and secret-absence `ConfigurationIsolationTests.cs:42-50`. Unverified (static reasoning).
- `EvidenceMethod.Configured` on `included-in` / `configured-by` / confirmed `targets` is construction-killed by Domain `RequireSufficientEvidence` (taxonomy minimum Configured). Same pattern as EBC-07.

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
| Spec-anchored outcome check (asserted values match spec) | PASS (CDC-04/CDC-07/P2 Independent Test flagged) |
| Per-layer Coverage Expectation met (inventory + extraction + classification 1:1 ACs; e2e happy+edge; storage round-trip; CLI package) | PASS |
| Every test maps to a spec requirement - no unclaimed tests | PASS |
| Documented guidelines followed: `AGENTS.md` / `CLAUDE.md` (net10.0, Workspaces.MSBuild 5.6.0, no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults`, SyntheticSolution-only versioned fixture, LocalCorpus skip when clones absent, standing sensor skip) | PASS |

Diff `32e6a10^..HEAD` stays on fixture applications/worker/appsettings, inventory configuration-document classification, project-metadata and configuration emitters, topology/configuration builders and emitters, ComponentPass rewrite, ConfigurationPass, SnapshotAccumulator.RemoveCandidate, Storage round-trip, matching tests, and mechanical `CDC-` trait allowlists. No Domain descriptor table or `contracts/taxonomy-registry.json` byte changes (CDC-53). No `Microsoft.Build.*` and no `MSBuildLocator`. Target framework `net10.0`; Roslyn package remains `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0.

T32 emits promotion; T34 registers `ConfigurationPass` in `PipelineStages.CreateDefault` (`PipelineStages.cs:16-27`). PackageValidator indexing of Component/ExternalSystem/ConfigurationBinding is Storage mapping used by T41, not a new fact family. T10 retargeted slnx membership: `SolutionFileReaderTests.cs:21-28` five listed paths; `InventoryFactsTests.cs:59-65` four Project facts (DoesNotExist omitted).

Spot-check (P1 client address + grouping): CDC-43/44/45 e2e assert PaymentService confirmed-and-no-candidate, NotificationService candidate+frontier, ShippingService candidate-only. CDC-09/11/19/21 e2e assert three components, two DUs, shared included-in both. Payload fields (`output-kind`, `project-reference`, `key`, `resolution`, `address`) are asserted on value, not mere call occurrence. Not trait-only.

`dotnet-test:assertion-quality` / `test-anti-patterns` on the 5D classification/extraction tests: no assertion-free tests; no `Thread.Sleep`; no always-true asserts; no swallowed exceptions. Equality + collection + negative asserts are the dominant mix.

---

## Edge Cases

- [x] Uncompilable listed project (Acme.Broken): no metadata observation; diagnostics unchanged (CDC-05) — `ProjectMetadataEmitterTests.cs:161-163`
- [x] Out-of-solution project reference: diagnostic, no observation (CDC-07) — `ProjectMetadataEmitterTests.cs:244-248`
- [x] Unreached library: unresolved `included-in`, no confirmed (CDC-22) — `ComponentPassTests.cs:205-212`
- [x] No application in the ledger: no DeploymentUnit, unresolved per component (CDC-23) — `ComponentPassTests.cs:233-235`
- [x] Empty ledger / no project-metadata: no topology, no throw (CDC-18) — `ComponentPassTests.cs:55-62`
- [x] Transitive reach, diamond, cycle (CDC-13) — `TopologyModelBuilderTests.cs:49-54`, `:78-82`, `:102-105`
- [x] Two `appsettings*.json` files, same key not merged (CDC-35 no-merge) — `ConfigurationModelBuilderTests.cs:57-63`; e2e CatalogService binding `TopologyIntegrationTests.cs:63-67`
- [x] Malformed JSON: diagnostic, no observations, no abort (CDC-31) — `ConfigurationDocumentReaderTests.cs:171-177`
- [x] Path escape via symlink: rejected (CDC-34) — `ConfigurationDocumentReaderTests.cs:274-278`
- [x] Prefix/suffix/case-insensitive client or store names: no `configured-by`, no `targets` promote (CDC-41, CDC-48) — `ConfigurationModelBuilderTests.cs:196-199`, `:335-336`
- [x] Literal Services key without `address`: Leave, not Promote — `ConfigurationModelBuilderTests.cs:317-318`
- [x] Observation-free symbol: no owner, no `belongs-to` (CDC-16) — `TopologyEmitterTests.cs:58-61`

---

## Gate Check

- **Gate command**: `dotnet build && dotnet test tests/Csharp2Md.Domain.Tests/Csharp2Md.Domain.Tests.csproj && dotnet test tests/Csharp2Md.Analysis.Tests/Csharp2Md.Analysis.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Storage.Tests/Csharp2Md.Storage.Tests.csproj && dotnet test tests/Csharp2Md.Cli.Tests/Csharp2Md.Cli.Tests.csproj --filter "Category!=LocalCorpus" && dotnet test tests/Csharp2Md.Projection.Tests/Csharp2Md.Projection.Tests.csproj`
- **Result**: 1342 passed, 0 failed, 0 skipped among selected tests
- **Per-project passed counts**:
  - `Csharp2Md.Domain.Tests`: 549 passed
  - `Csharp2Md.Analysis.Tests`: 590 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Storage.Tests`: 173 passed
  - `Csharp2Md.Cli.Tests`: 27 passed (`Category!=LocalCorpus`)
  - `Csharp2Md.Projection.Tests`: 3 passed
- **Test count before feature**: 1211 tests (1208 passing / 3 failing Phase 0)
- **Test count after feature**: 1342 passing (0 failing on the serial gate)
- **Delta**: +131 executed tests vs the 1211 baseline; +134 vs the 1208 pre-feature passing count. Increase; no silent deletions. Phase 0 (T1–T3) repaired the three pre-existing failures.
- **Skipped tests**: none among the filtered gate. `LocalCorpusAnalyzeTests` excluded by `Category!=LocalCorpus`.
- **Failures**: none on the serial Category!=LocalCorpus gate.

---

## LocalCorpus

`fixtures/eShop` is absent. `fixtures/eShopOnContainers` is absent. No `.sln` / `.slnx` clone to run.

Post-gate LocalCorpus run skipped. Not a feature FAIL. Clones were not added to git.

---

## Fix Plans

None. CDC-04, CDC-07, and the P2 Independent Test are recorded as spec-precision gaps, not uncovered ACs. Tests assert the specified core outcomes (one observation identity, unanalyzed-reference diagnostic with no observation, coverage counts without `%`).

---

## Requirement Traceability Update

Report-only. `spec.md` was not edited (Verifier commit scope).

| Requirement | Previous Status | New Status |
| ----------- | --------------- | ---------- |
| CDC-01..CDC-58 | In Tasks | Verified in this report |

---

## Summary

**Overall**: Ready

**Spec-anchored check**: 58/58 ACs matched spec outcome | 3 spec-precision gaps flagged
**Sensor**: SKIPPED (standing skip)
**Gate**: 1342 passed, 0 failed

**What works**: Evidence-based components (deployable / private-use / shared), two Orders deployment units, configuration bindings and all four `configured-by` triples, PaymentService confirmed `targets`, NotificationService frontier, ShippingService left as candidate, secret redaction, clone-path byte identity, taxonomy bytes unchanged.

**Issues found**: Spec-precision only (CDC-04 multi-TFM WHEN tested as re-emit; CDC-07 diagnostic names the unanalyzed path only; P2 Independent Test not asserted on published `diagnostics.json`).

**Next steps**: Distill the spec-precision lessons. Feature is ready for the orchestrator to mark done after `validate_state.py`.
