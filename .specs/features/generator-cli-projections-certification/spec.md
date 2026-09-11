# Generator CLI, Projections and Certification Specification

## Problem Statement

Workstreams 1 through 7 produce a package that is structurally intact and self-contained, and an independent LLM-readiness audit of a real `eShopOnContainers` run (`artifacts/verifications/llm-readiness-s-cb7a4be0b1a084f3b59e9c2f1e3906f1.md`) confirms it: the manifest closes the package exactly, no internal link is broken, source retrieval by locator works, and all fourteen redacted documents carry consistent sidecars. The same audit returns **NÃO APTO** for autonomous LLM consumption. `run-certification.json` says `{"status":"not_evaluated"}`; all four mandatory coverage metrics are `0/0`; a `private` helper (`CatalogController.ChangeUriPlaceholder`) was published as an `EntryPoint`; a real traversal from `OrdersController.GetOrderAsync` lost every observed invocation without producing a candidate, an unresolved record or an open frontier; `relations/confirmed/contains.json` alone is 169.28 MiB inside a 289.75 MiB package; `retrieval.md` sends the agent into multi-megabyte payloads instead of catalogs; and the manifest reports `count: 0` for all 1,859 projection artifacts.

None of those are documentation defects. Each one lets an LLM mistake the absence of a fact for the absence of behaviour, or invent an entry surface that does not exist. Publishing a structurally valid package is not the same as certifying it, and this workstream is where that difference stops being implicit.

This is the last roadmap row. It delivers the final `analyze` / `validate` / `compose` CLI, a computed run certification with verifiable denominators, an engine certification on independently labeled corpora, provenance in the manifest, measured scale budgets with sharded high-cardinality payloads, an executable retrieval guide, and an explicit supported-document policy replacing indiscriminate enumeration — and it **fixes** every defect the audit proved, under discriminating tests, rather than merely reporting them.

## Goals

- [ ] Replace `not_evaluated` with a computed run-certification status of `passed`, `degraded` or `failed`, defined so a structurally valid publication can never be presented as semantically certified while coverage is unevaluated.
- [ ] Publish `entry_point`, `linked_call`, `contract` and `persistence` coverage with numerator, denominator, exclusions, unknowns and degradation reasons, and forbid any solution-level recall percentage without labeled ground truth.
- [ ] Give every recognized invocation an explicit, auditable disposition — confirmed, candidate, unresolved, open frontier, or a justified counted exclusion — so no invocation disappears silently.
- [ ] Stop promoting callables with no proven entry capability to `EntryPoint`, and prove the `CatalogController.ChangeUriPlaceholder` regression closed on a versioned fixture.
- [ ] Certify the engine, not only the run: independently authored labeled corpora with positives, negatives and lookalikes per classifier, measured against the normative precision and recall thresholds.
- [ ] Eliminate monolithic high-cardinality canonical payloads, under a per-artifact byte ceiling derived from a declared per-scenario reading budget and published with the calculation that produced it.
- [ ] Make every documented retrieval scenario start at a catalog, cover every relation kind and proof state in the correct artifact, and be executed and measured automatically.
- [ ] Publish generator provenance and useful cardinality in the manifest, separated from timestamps and runtime measurements.
- [ ] Replace indiscriminate project-directory enumeration with an explicit supported-document policy, and measure what it accepts and excludes.
- [ ] Close the migration: move the objective LLM-readiness checklist from FAIL to PASS on a versioned corpus in CI, with the local eShop corpora as optional confirmation.

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Query engine, factual database, `kb`, QMD, embeddings, semantic search | AD-011; deferred downstream. The package must be navigable by ordinary file reading |
| Wiki compilation, flows-as-pages, business-rule interpretation | AD-005 and AD-011; downstream LLM layer |
| Incremental analysis, snapshot diff, batch-to-batch diff, caching | Roadmap "Deferred after generator completion" |
| Dynamic plugin loading, semantic support for languages other than C# | Roadmap "Deferred after generator completion" |
| New fact families, fact types, observation kinds, facet axes or relation triples | Workstream 1 is closed; this workstream consumes the registry, it does not extend it |
| Numeric confidence scores on facts or relations | AD-010; proof states are the explanation |
| Merging component, deployment-unit or contract identities across solutions | AD-008 and workstream 7; shared identity is asserted only when proven |
| Proving an HTTP cross-solution destination by traversing configuration base addresses | Deferred by workstream 7; it raises the proof bar past exact key equality and no gate here requires it |
| A user-facing `certify` command or a `--certify-engine` flag | D-03: engine certification is a repository test suite, not product surface |
| Running diagnostic analyzers, or enabling source generators without separate consent | AD-003; the trust boundary is unchanged |
| Committing `fixtures/eShop` or `fixtures/eShopOnContainers` to git | Standing constraint; those corpora stay optional local clones |
| Restoring any legacy CLI option, output layout, ID grammar or relation kind | AD-002 and AD-012 |

---

## Contract dependencies

This feature reads only the contracts below from completed workstreams. It supersedes named requirements where the audit proved them insufficient; every supersession is stated, never silent.

| Source | Contract consumed | Change made here |
| --- | --- | --- |
| `knowledge-taxonomy-contract` | `contracts/taxonomy-registry.json`, relation matrix, proof-state axes, version axes | Fact families, fact types, observation kinds and relation triples are consumed unchanged. The `symbol-facet` axis gains one value so entry capability is expressible, so the registry artifact and its declaration are regenerated together under the AD-013 drift gate and `taxonomy_version` moves to 2 |
| `factual-storage` | `manifest`, `coverage`, `run_certification`, `diagnostics`, `quarantine` and `measurements` envelopes and their JSON Schemas; transactional staging, manifest-last publication, quarantine and abort semantics (STOR-04, STOR-06, STOR-16, STOR-31, STOR-32, STOR-43) | `coverage` and `run_certification` gain computed content and a defined status vocabulary; the manifest gains provenance and a useful count; timestamps stay in `measurements`, preserving STOR-43 |
| `roslyn-observation-extraction` | Authorized-root inventory, structural facts, observation ledger, determinism guarantees (ROSE-01..ROSE-10, ROSE-52..ROSE-54) | **Supersedes ROSE-04, ROSE-05 and ROSE-06** where they require a `Document` fact and an `unsupported` outcome for every file in a project directory |
| `entrypoints-boundaries-contracts` | Entry-point, boundary-operation and contract classification (EBC-05..EBC-26) | **Supersedes EBC-05 and EBC-08** where a `ControllerBase` descendant's callable becomes an `EntryPoint` without proven entry capability; **extends EBC-06** so inbound HTTP publishes `http_method` and `route` |
| `call-linking-flow-frontiers` | Invocation disposition semantics: confirmed `invokes` (CLLF-01, CLLF-02), interface and abstract targets become candidates and never a confirmed edge (CLLF-07, CLLF-09), unresolved plus open frontier (CLLF-08, CLLF-11..CLLF-14) | Consumed as the normative answer for interface-to-implementation dispatch. **Supersedes CLLF-20** where framework calls are *silently* skipped: the skip stays, the silence does not |
| `persistence-knowledge` | Data stores, objects, fields, operations and their coverage denominators | Consumed unchanged; supplies the `persistence_coverage` population |
| `components-deployments-configuration` | Components, deployment units, configuration bindings, `missing-project` and `malformed-configuration-document` diagnostics | Diagnostic emission is corrected for solution folders and for configuration syntax the .NET configuration provider accepts |
| `retrieval-projections` | Projector port inside `Commit()` (AD-019), catalogs, postings, declaration locators, `source/`, Markdown, `retrieval.md`, projection validation, redaction (RP-01..RP-57) | **Supersedes RP-07** so `source/` covers accepted documents rather than every inventoried document; **extends RP-18..RP-24** with compact derived labels; **calibrates RP-52**, whose ceiling was explicitly left to this workstream |
| `multi-solution-composition` | `batch-manifest.json`, identity-derived package directories, proven cross-solution correlations, partial-scope declaration (MSC-01..MSC-40) | Consumed unchanged; supplies the batch surface that `compose` operates on and that batch certification qualifies |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here. Nothing is left silently unclear. Rows marked `y` were confirmed by the user during Discuss and are recorded in `context.md`.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Completion gate corpus | A new versioned, C#-only certification corpus reproduces every audit regression and runs in CI as the mandatory gate; the eShop clones re-run the same objective checklist optionally, as `LocalCorpus` | D-01. An eShop-only gate is unreproducible because the clones are gitignored; a versioned corpus makes the Definition of Done measurable in CI without weakening it | y |
| Second versioned fixture | This feature amends the standing constraint "the only versioned analysis fixture is `fixtures/SyntheticSolution`" to admit the certification corpus, and records the amendment in `.specs/STATE.md` | D-01 requires a second versioned corpus; the constraint exists to keep large third-party clones out of git, which a purpose-built C#-only corpus does not violate | y |
| Byte ceiling calibration | Derived from a declared per-scenario reading budget and the package's own measured bytes-per-token ratio, published with the calculation; an eShop measurement is evidence of scale, never the source of the number | D-02. A number derived from the package is reproducible in CI; a number measured off a gitignored clone is not | y |
| Declared per-scenario reading budget | 100,000 tokens of read content and 25 file reads per documented retrieval scenario; no single artifact a scenario must read in full exceeds one eighth of that token budget | Agent default giving Design a concrete target. It leaves a scenario comfortably inside a single context window while allowing several hops, and one eighth stops one file from consuming a whole scenario | n |
| CLI surface | Three verbs — `analyze`, `validate` and `compose`. Engine certification is a repository test suite publishing a versioned report, not a command and not a flag | D-03. AD-009 separates engine certification from run certification; making it a verb would expose test corpora as product surface | y |
| Supported-document policy reach | A document outside the policy produces no `Document` fact, no `source/` artifact, no structural relation and no individual diagnostic; at most one aggregated diagnostic names the excluded count and extensions | D-04. This is what removes the 549 individual diagnostics and the inflated structural inventory at once | y |
| Default supported set | `.cs`; each project's `.csproj`; build and configuration files the generator actually consumes; `appsettings*.json`; generated redaction sidecars | D-04 plus the stated C#-only baseline. Every entry has a registered consumer today | y |
| Conditional supported set | An additional extension is admitted only when an active classifier declares it consumes it — for example `.proto`, `.sql`, Dockerfile, Compose and Kubernetes manifests | D-04. It ties the policy to declared capability instead of a hand-maintained list, and matches the supported-source list in `architecture-knowledge-engine.md` | y |
| Default excluded set | `.ts`, `.tsx`, `.js`, `.jsx` and `.map`; archives; images, fonts, CSS and static assets; package locks and frontend dependencies; binaries, certificates and `.pfx`; any document with no registered consumer | D-04. None carries evidence any classifier consumes, and together they dominate the 549 diagnostics | y |
| Scale input | The over-ceiling input is generated deterministically at test time from a versioned generator specification, not committed as output bytes | Agent default. Committing a fixture large enough to reproduce a 169 MiB payload is unacceptable in git; a deterministic generator proves the same split behaviour and stays reviewable | n |
| Run-certification status vocabulary | Exactly `passed`, `degraded` and `failed`. `not_evaluated` is removed from the vocabulary and never published | Agent default following AD-009. A status meaning "we did not look" is indistinguishable to a consumer from "we looked and it was fine" | n |
| Zero-population metrics | A metric whose recognizable population is empty publishes `not_applicable` with a stated reason; a run in which every mandatory metric is `not_applicable` is `degraded`, never `passed` | Agent default. This is the rule that makes certifying a `0/0` package impossible while still letting a library-only solution publish honestly | n |
| Overlapping dispositions | An occurrence may hold both an unresolved record and an open frontier where the taxonomy requires it (CLLF-13); coverage counts that occurrence exactly once | Agent default derived from the taxonomy. Forbidding the overlap would contradict 5B; counting it twice would inflate the denominator | n |
| Framework-call disposition | A call bound outside the analyzed solution scope is a counted, categorized exclusion carried in the coverage envelope, not a per-occurrence diagnostic and not a silent drop | Agent default superseding the silence in CLLF-20. 691 diagnostics already overwhelm triage; an aggregate keeps the accounting complete without a twenty-thousand-row envelope | n |
| Interface-to-implementation expected outcome | For `OrdersController.GetOrderAsync` to `IOrderQueries.GetOrderAsync` to `OrderQueries.GetOrderAsync`, the required result is one `CandidateLink` of kind `invokes` per concrete implementing symbol in solution scope, and no confirmed `invokes` to the interface member | Derived from CLLF-07 and CLLF-09, not invented. The audit's defect is that the package published *nothing*, not that it published a candidate | n |
| Entry capability evidence | An `EntryPoint` requires positive evidence that the callable can start an execution — an externally reachable declaration plus framework-recognized entry evidence — expressed as an outcome, with the mechanism left to Design | Agent default. Naming a facet or a Roslyn accessibility call here would prescribe implementation the spec has no business fixing | n |
| Legitimate conventional actions | A conventional framework action with sufficient evidence stays an `EntryPoint` even without an explicit route declaration; only callables with no proven entry capability are demoted | Agent default. The EBC-08 diagnostic case is legitimate; the defect is the missing capability check, not the missing route | n |
| Registry and version axes | The `symbol-facet` axis gains `externally-reachable`; the wire record encoding is interned; the document policy and classifier corrections all move their own version axis. `schema_version`, `taxonomy_version`, `extractor_set_version` and `classifier_set_version` each advance to 2; `observation_schema_version` stays 1 | Design found that accessibility is carried nowhere today, so entry capability is not expressible without a facet, and that the 169 MiB payload is unreachable by sharding alone. AD-002 authorizes the break and AD-013's gate only forbids drift between declaration and artifact, not a deliberate joint revision | y |
| Provenance content | Generator version, build or commit identity, the five contract version axes, and the deterministic parameters that shaped the package — byte ceiling, document-policy version and allowlist digest | Agent default derived from the version axes in `taxonomy.md` and audit gate 8. These are exactly the values a consumer needs to decide whether two packages are comparable | n |
| Provenance determinism | Two runs of the same generator build over the same input publish byte-identical provenance; timestamps and durations stay in the measurements envelope | Agent default preserving STOR-43 and the standing determinism constraint | n |
| Manifest cardinality | Every manifest entry publishes the artifact's top-level entry count and its byte size, and validation proves the count equals the artifact's actual entries | Agent default answering audit finding I5. A `count: 0` on an artifact holding records is worse than no field at all | n |
| Exit-code space | `0` success with certification passed; `1` invalid invocation; `2` partial composition; `3` degraded; `4` certification failed; `5` structural corruption; `6` incompatible provenance or contract version | Agent default. One code per outcome class the user named, extending the existing `0`, `1` and `2` meanings rather than renumbering them | n |
| Label provenance | Every compact label published in a catalog or page is derived from a value proven present in the authoritative payload, cited by artifact key and ordinal, and validated before publication | Agent default extending RP-33, RP-34 and RP-44 to the new fields, so labels cannot become a second, softer authority | n |
| Configuration parsing policy | The accepted syntax is stated explicitly — comments, trailing commas and the documented variants — and matches what the .NET configuration provider accepts; anything outside it stays diagnosed as malformed | Agent default. Loosening the parser without a stated policy would let ambiguous data be promoted, which the user forbade | n |
| `compose` input | `compose` reads already-published packages and their manifests; it does not open a solution, run Roslyn or re-analyze | Agent default following the user's `validate` constraint and the deferred idea recorded by workstream 7 | n |
| Batch certification | A batch is certifiable only when every required solution is published, provenance-compatible and individually certifiable; otherwise the batch declares incomplete scope and is not certified | Agent default combining the composition rule in `output-and-retrieval.md` with the run-certification vocabulary | n |

**Open questions:** none — all resolved or logged above.

---

## Implicit-requirement dimensions sweep

Large and Complex scope: every dimension resolves to a requirement or an explicit `N/A because …`.

| Dimension | Coverage |
| --- | --- |
| Input validation and bounds | GCPC-026..GCPC-035 supported-document policy and allowlist; GCPC-063..GCPC-073 CLI argument and package validation; GCPC-036..GCPC-045 artifact ceilings |
| Failure and partial-failure states | GCPC-006..GCPC-010 degradation semantics; GCPC-069..GCPC-073 exit codes; GCPC-112..GCPC-114 batch certification and partial composition |
| Idempotency, retry, duplicate handling | GCPC-108..GCPC-110 determinism across runs, clone paths and input order; GCPC-060 provenance determinism; GCPC-042 shard assignment stability |
| Auth boundaries and rate limits | N/A because the generator is a local in-process tool with no network surface and no multi-tenant caller. The boundary that does exist is the authorized root and the trust boundary, covered by GCPC-082..GCPC-086 |
| Concurrency and ordering | GCPC-108..GCPC-110; input order and enumeration order change neither IDs, bytes nor shard assignment |
| Data lifecycle and expiry | N/A because the package is a whole-output publication with no retention, TTL or archival semantics; replacement is the atomic commit already specified by STOR-16 and STOR-31 |
| Observability | GCPC-001..GCPC-010 coverage and certification envelopes; GCPC-056..GCPC-062 provenance and manifest cardinality; GCPC-033..GCPC-035 aggregated exclusion diagnostics and measured category totals |
| External-dependency failure | GCPC-016..GCPC-018 framework and external calls as terminal effects or counted exclusions; GCPC-103..GCPC-106 missing projects and malformed configuration documents |
| State-transition integrity | GCPC-006..GCPC-010 the passed, degraded and failed transition rules; GCPC-011..GCPC-015 invocation disposition exhaustiveness and single counting |

---

## User Stories

### P1: Certified execution with verifiable denominators ⭐ MVP

**User Story**: As an LLM consuming a package, I want a computed run certification and four coverage metrics with real denominators, so that I can tell "there is no such behaviour" from "we did not look".

**Why P1**: Audit blocker B1. Every other outcome in this feature is unreadable while the package says `not_evaluated` and `0/0`; nothing downstream can be trusted without it.

**Acceptance Criteria**:
1. WHEN a run publishes a package THEN the system SHALL publish a run-certification status of exactly `passed`, `degraded` or `failed`, and SHALL NOT publish `not_evaluated`. (GCPC-001)
2. WHEN the system publishes each of `entry_point`, `linked_call`, `contract` and `persistence` coverage THEN it SHALL publish that metric's numerator, denominator, exclusion count, unknown count and degradation reasons. (GCPC-002)
3. WHEN a coverage metric is published THEN its denominator SHALL be the population named for it in `docs/architecture/quality-and-security.md`, and every member of that denominator SHALL be individually enumerable from artifacts in the same publication. (GCPC-003)
4. WHEN a coverage metric is published THEN the system SHALL publish, for each degradation reason, the count of denominator members it accounts for. (GCPC-004)
5. The system SHALL NOT publish a solution-level recall percentage, a precision percentage or any derived accuracy ratio in the run-certification or coverage envelope. (GCPC-005)
6. WHEN every mandatory metric is evaluated, no derived fact is quarantined, no classifier conflict affects a covered area and no degradation reason is recorded THEN the system SHALL publish the run-certification status `passed`. (GCPC-006)
7. WHEN every mandatory metric is evaluated and at least one degradation reason, unknown or unsupported capability that can hide entry points, contracts or persistence is recorded THEN the system SHALL publish the status `degraded`. (GCPC-007)
8. IF a derived fact is quarantined, a classifier conflict affects a covered area, or a recognized invocation ends with no disposition THEN the system SHALL publish the status `failed`. (GCPC-008)
9. IF a mandatory metric's recognizable population is empty THEN the system SHALL publish that metric as `not_applicable` with a stated reason, and SHALL NOT publish it as a satisfied ratio. (GCPC-009)
10. IF every mandatory metric is `not_applicable` THEN the system SHALL publish the status `degraded` and SHALL NOT publish `passed`. (GCPC-010)

**Independent Test**: Analyze the certification corpus and read the published `coverage.json` and `run-certification.json` from disk. For each metric, independently enumerate the denominator population from the published facts, observations and relations, and assert it equals the published denominator; assert numerator plus exclusions plus unknowns never exceeds it. Then analyze a library-only solution with no entry points and assert `entry_point_coverage` is `not_applicable` with a reason and the run status is `degraded`, not `passed`.

---

### P1: Complete invocation accounting ⭐ MVP

**User Story**: As an LLM tracing a flow, I want every recognized invocation to carry an explicit disposition, so that a traversal that stops tells me why it stopped instead of ending in silence.

**Why P1**: Audit blocker B3. A real entry point lost all three of its observed invocations, so the package looked complete while the flow had vanished.

**Acceptance Criteria**:
1. WHEN the system recognizes an invocation occurrence THEN it SHALL publish exactly one disposition for that occurrence among confirmed relation, candidate link, unresolved record, open frontier and counted justified exclusion. (GCPC-011)
2. The system SHALL publish an invocation-accounting record whose totals per disposition sum exactly to the count of recognized invocation occurrences in the same publication. (GCPC-012)
3. IF a recognized invocation occurrence carries no disposition THEN the system SHALL publish the run-certification status `failed` and SHALL name the unaccounted occurrence. (GCPC-013)
4. WHERE the taxonomy requires an occurrence to hold both an unresolved record and an open frontier, the system SHALL publish both and SHALL count that occurrence exactly once in `linked_call_coverage`. (GCPC-014)
5. The system SHALL NOT count one invocation occurrence in more than one coverage numerator, denominator or exclusion total. (GCPC-015)
6. WHEN an invocation binds to a callable outside the analyzed solution scope THEN the system SHALL record it as a counted exclusion carrying a declared exclusion category, and SHALL NOT emit a per-occurrence diagnostic for it. (GCPC-016)
7. WHEN an invocation crosses a boundary whose continuation is not statically demonstrable THEN the system SHALL record it as a terminal effect with a declared cause, reachable from the occurrence. (GCPC-017)
8. WHEN a callable invokes an interface or abstract member for which one or more concrete implementing symbols exist in solution scope THEN the system SHALL publish one candidate link of kind `invokes` per concrete implementing symbol and SHALL NOT publish a confirmed `invokes` to the interface member. (GCPC-018)

**Independent Test**: In the certification corpus, reproduce the audit's chain as a controller action that calls an injected interface method whose single implementation is in another project of the same solution, plus two framework calls equivalent to `Ok` and `NotFound`. After analyze, read the published relation, candidate, unresolved, frontier and coverage artifacts and assert: exactly one candidate link from the action to the concrete implementation; no confirmed `invokes` to the interface member; both framework calls present as counted exclusions in the declared category; and the invocation-accounting totals equal the recognized occurrence count with no occurrence in two totals.

---

### P1: Proven entry capability ⭐ MVP

**User Story**: As an LLM answering "what can call into this service", I want only callables with proven entry capability published as entry points, so that I do not describe a private helper as an external surface.

**Why P1**: Audit blocker B2, and the only proven false positive in the package. A fabricated entry surface is worse than a missing one.

**Acceptance Criteria**:
1. The system SHALL publish an `EntryPoint` fact only for a callable with positive evidence that an execution can begin at it. (GCPC-019)
2. WHEN a callable's declaration is not externally reachable THEN the system SHALL NOT publish an `EntryPoint` fact for it, regardless of its declaring type. (GCPC-020)
3. WHEN a callable is a helper or supporting member of a framework-recognized type but carries no framework entry evidence THEN the system SHALL NOT publish an `EntryPoint` fact for it. (GCPC-021)
4. WHEN a conventional framework action carries sufficient entry evidence but no explicit route declaration THEN the system SHALL publish the `EntryPoint` and SHALL record the missing-route diagnostic, preserving the existing behaviour for that case. (GCPC-022)
5. WHEN an `EntryPoint` fact is published THEN the system SHALL publish the evidence that proved its entry capability, cited by artifact key and ordinal. (GCPC-023)
6. IF a callable's entry capability cannot be determined THEN the system SHALL publish it as a candidate or unresolved record and SHALL NOT publish it as a confirmed `EntryPoint`. (GCPC-024)
7. The system SHALL reproduce the entry-point regression in the versioned certification corpus and SHALL NOT require the local eShop clone to detect it. (GCPC-025)

**Independent Test**: Add to the certification corpus a controller carrying, in one type, a public routed action, a public conventional action with no route attribute, and a private helper method equivalent to `CatalogController.ChangeUriPlaceholder`. After analyze, read the published architecture facts and assert exactly two `EntryPoint` facts, that neither names the private helper, that the conventional action is present with its missing-route diagnostic, and that each published entry point cites resolvable entry evidence.

---

### P1: Supported-document policy ⭐ MVP

**User Story**: As a maintainer analyzing a repository that contains a frontend, I want the generator to inventory only documents it actually consumes, so that the package is not dominated by assets no classifier reads.

**Why P1**: 549 of 691 diagnostics were `unsupported-document`, and the same enumeration inflated the structural inventory and the `source/` projection. Every scale and readability outcome in this feature depends on it.

**Acceptance Criteria**:
1. The system SHALL publish an explicit supported-document policy naming the accepted document classes, the conditionally accepted classes and the excluded classes. (GCPC-026)
2. WHEN a document matches the supported-document policy THEN the system SHALL inventory it, publish its `Document` fact and publish its `source/` artifact. (GCPC-027)
3. WHEN a document does not match the supported-document policy THEN the system SHALL publish no `Document` fact, no `source/` artifact, no structural relation and no individual `unsupported-document` diagnostic for it. (GCPC-028)
4. WHERE an extension is accepted only conditionally, the system SHALL admit it only when an active classifier declares that it consumes that extension. (GCPC-029)
5. WHERE the caller supplies an explicit document allowlist, the system SHALL admit the listed documents without admitting any other excluded class. (GCPC-030)
6. The system SHALL treat the phrase "every analyzed document" in every published contract and projection as meaning every document accepted by the supported-document policy. (GCPC-031)
7. The system SHALL publish a `source/` artifact for every accepted document and SHALL NOT publish one for any excluded document. (GCPC-032)
8. WHEN at least one document is excluded THEN the system SHALL publish at most one aggregated diagnostic carrying the excluded count and the excluded extensions. (GCPC-033)
9. WHEN a run completes THEN the system SHALL publish the accepted and excluded document count and byte total per policy category. (GCPC-034)
10. The system SHALL NOT remove from the package any configuration, deployment, contract or persistence evidence that an active classifier consumes as a result of applying the policy. (GCPC-035)

**Independent Test**: Add to the certification corpus a C#-only project carrying alongside its sources a TypeScript file, a `.js` file with a `.map`, an image, a `.zip`, a `package-lock.json` and a `.pfx` certificate. After analyze, read the published manifest, structural facts and `source/` listing and assert none of the six excluded files appears in any of them, that exactly one aggregated exclusion diagnostic names all six extensions, that no individual `unsupported-document` diagnostic exists, and that the project's `.cs`, `.csproj` and `appsettings.json` are present with their configuration facts intact.

---

### P1: Bounded payloads and measured budgets ⭐ MVP

**User Story**: As an LLM with a finite context window, I want every published artifact to fit a declared budget, so that following the package's own guidance cannot exhaust my context before I reach the fact.

**Why P1**: Audit blocker B4. A single 169.28 MiB artifact makes the package unusable by the consumer it was built for.

**Acceptance Criteria**:
1. The system SHALL publish a declared per-scenario reading budget in tokens and file reads, and a declared per-artifact byte ceiling. (GCPC-036)
2. The system SHALL derive the per-artifact byte ceiling from the declared per-scenario reading budget and the package's measured bytes-per-token ratio, and SHALL publish that calculation with its inputs. (GCPC-037)
3. The system SHALL publish no artifact whose byte size exceeds the declared per-artifact ceiling. (GCPC-038)
4. WHEN facts, observations, confirmed relations, candidates, unresolved records, open frontiers, catalogs or postings would exceed the ceiling THEN the system SHALL split them into shards, each within the ceiling. (GCPC-039)
5. WHEN an artifact is split THEN the system SHALL preserve every record's identity, its deterministic ordering, its payload hash and the resolvability of every citation that addressed it. (GCPC-040)
6. WHEN a citation addresses a record in a split artifact THEN reading the cited artifact at the cited ordinal SHALL yield the record the citation claimed. (GCPC-041)
7. WHEN the same input is analyzed twice THEN the system SHALL assign every record to the same shard. (GCPC-042)
8. The system SHALL NOT create a directory keyed by a high-cardinality fact identity. (GCPC-043)
9. WHEN the scale input is analyzed THEN the system SHALL split the `contains` relation payload, the `belongs-to` relation payload and the invocation observation payload, and SHALL publish no artifact exceeding the ceiling. (GCPC-044)
10. WHEN a run completes THEN the system SHALL publish the byte size and token estimate of the largest artifact of each role. (GCPC-045)

**Independent Test**: Generate the deterministic scale input sized so that `contains`, `belongs-to` and invocation observations each exceed the derived ceiling by at least one order of magnitude. After analyze, walk every file in the package and assert none exceeds the published ceiling, that all three payloads are sharded, that no directory is named after a fact identity, and that every catalog and posting citation still resolves to the record it claims. Re-run and assert identical shard assignment.

---

### P1: Executable retrieval guide ⭐ MVP

**User Story**: As an LLM opening a package for the first time, I want a guide whose every documented path I can actually walk within budget, so that following it produces a complete answer rather than an exhausted context.

**Why P1**: Audit blocker B5. The guide currently directs the agent into the exact payloads that make the package unusable, and omits most relation kinds.

**Acceptance Criteria**:
1. WHEN `retrieval.md` documents locating an identity THEN it SHALL direct the reader to the appropriate catalog and SHALL NOT direct the reader to a canonical payload artifact. (GCPC-046)
2. The system SHALL document in `retrieval.md` how to select a postings bucket and resolve a record by ordinal without reading an entire canonical payload. (GCPC-047)
3. The system SHALL document a retrieval path for each of `executes`, `implements-operation`, `invokes`, `uses-contract`, `accesses-data`, `operates-on` and `targets`, each naming the artifact that holds it. (GCPC-048)
4. The system SHALL document a separate retrieval path for candidate links, unresolved records and open frontiers, each naming the artifact that holds it. (GCPC-049)
5. The system SHALL NOT instruct the reader to open an artifact in full when an index, catalog or posting addresses the same record. (GCPC-050)
6. The system SHALL document a stopping rule for a terminal effect, a traversal cycle, an open frontier, an unsupported capability and an exhausted reading budget. (GCPC-051)
7. WHEN a documented scenario is executed THEN the system SHALL measure and publish the files read, hops taken, bytes and tokens consumed, relevant facts returned and irrelevant records read. (GCPC-052)
8. WHEN a documented scenario is executed THEN it SHALL reach its stated endpoint within the declared per-scenario reading budget. (GCPC-053)
9. The system SHALL execute every documented retrieval path automatically against the published package and SHALL fail the run when a documented path does not resolve. (GCPC-054)
10. IF `retrieval.md` names an artifact key absent from the same publication THEN the system SHALL abort the publication and name the offending key. (GCPC-055)

**Independent Test**: After analyzing the certification corpus, run every scenario documented in the published `retrieval.md` as an automated walk that starts at the catalog the guide names, follows only the artifacts it names, and stops by the rules it states. Assert every scenario reaches its endpoint, that each scenario's measured reads, bytes and tokens are published and within budget, and that no scenario reads an artifact addressable by a posting it skipped.

---

### P1: Provenance and manifest cardinality ⭐ MVP

**User Story**: As a consumer holding two packages, I want the manifest to tell me which generator built each one and how large each artifact is, so that I can decide whether they are comparable and what a read will cost.

**Why P1**: Audit gate 8 and finding I5. Provenance could not be established from the output at all, and every projection artifact reported `count: 0`.

**Acceptance Criteria**:
1. WHEN a package is published THEN the manifest SHALL carry the generator version and a reproducible build or commit identity. (GCPC-056)
2. WHEN a package is published THEN the manifest SHALL carry the schema, taxonomy, observation-schema, extractor-set and classifier-set versions. (GCPC-057)
3. WHEN a package is published THEN the manifest SHALL carry the deterministic parameters that shaped it, including the per-artifact byte ceiling, the document-policy version and the allowlist digest. (GCPC-058)
4. The system SHALL keep timestamps and runtime measurements out of the manifest and provenance content and SHALL publish them only in the measurements envelope. (GCPC-059)
5. WHEN the same generator build analyzes the same input twice THEN the system SHALL publish byte-identical provenance content. (GCPC-060)
6. WHEN a manifest entry is published THEN it SHALL carry that artifact's top-level entry count and byte size, and the count SHALL equal the artifact's actual top-level entry count. (GCPC-061)
7. WHEN a package is published THEN every file inside the package directory SHALL be reachable from the manifest. (GCPC-062)

**Independent Test**: Analyze the certification corpus twice from the same build, read both manifests from disk, and assert the provenance content is byte-identical, that no timestamp appears in it, and that every declared version axis is present. For every manifest entry, open the cited artifact and assert its top-level entry count and byte size equal the manifest's values; then walk the package directory and assert every file appears in the manifest exactly once.

---

### P1: Final CLI surface ⭐ MVP

**User Story**: As an operator, I want `analyze`, `validate` and `compose` with stable exit codes, so that I can audit a package I already have and drive the generator from a script without parsing prose.

**Why P1**: The roadmap names the final CLI as this workstream's outcome, and `validate` is the only way a consumer can check a package it did not produce.

**Acceptance Criteria**:
1. The system SHALL expose exactly three subcommands: `analyze`, `validate` and `compose`. (GCPC-063)
2. WHEN `validate` runs against a published package THEN it SHALL read only that package and SHALL NOT open a solution, load a project or run semantic analysis. (GCPC-064)
3. WHEN `validate` runs THEN it SHALL detect an invalid schema, an incorrect payload hash, a dangling reference, an ordinal outside its artifact's bounds, an invalid source locator, a broken internal link and incompatible provenance. (GCPC-065)
4. WHEN `validate` detects a defect THEN it SHALL name the artifact key, the defect class and the offending value. (GCPC-066)
5. WHEN `validate` runs THEN it SHALL report the package's published certification status and SHALL NOT recompute coverage from the solution. (GCPC-067)
6. WHEN `compose` runs over published packages THEN it SHALL produce the batch manifest and composition artifacts without re-analyzing any solution. (GCPC-068)
7. WHEN a command completes successfully with certification `passed` THEN the system SHALL exit with code `0`. (GCPC-069)
8. WHEN a command completes with certification `degraded` THEN the system SHALL exit with code `3`, and WHEN it completes with certification `failed` THEN with code `4`. (GCPC-070)
9. IF a package or publication is structurally corrupt THEN the system SHALL exit with code `5`, and IF provenance or a contract version is incompatible THEN with code `6`. (GCPC-071)
10. IF at least one solution in a batch is unpublished THEN the system SHALL exit with code `2` and SHALL leave every committed package unmodified. (GCPC-072)
11. IF the invocation is invalid THEN the system SHALL exit with code `1` and SHALL publish nothing. (GCPC-073)

**Independent Test**: Publish a package with `analyze`, then run `validate` against it in a process with no access to the original solution directory and assert it exits `0`. Mutate a copy of that package once per defect class — corrupt a schema, alter a payload byte so its hash mismatches, delete a referenced fact, raise an ordinal past its array bound, move a locator span past end of file, break a Markdown link, and change the provenance version — and assert `validate` names each defect and returns the mapped exit code. Assert `compose` produces the same batch artifacts from published packages as `analyze` did, with no solution present.

---

### P1: Engine certification on labeled corpora ⭐ MVP

**User Story**: As a maintainer, I want classifier precision and recall measured against ground truth authored independently of the classifiers, so that a green test suite cannot be mistaken for a correct engine.

**Why P1**: AD-009 and the engine gates in `quality-and-security.md`. Without independent ground truth there is no evidence the classifiers are right, only that they are consistent with themselves.

**Acceptance Criteria**:
1. The system SHALL publish labeled corpora recording, for each labeled item, its source, its expected present, absent or unresolved state and the rationale for that expectation. (GCPC-074)
2. The system SHALL author every ground-truth label independently of classifier implementation and SHALL NOT derive a label from classifier output. (GCPC-075)
3. The system SHALL include positive, negative and lookalike items for each certified classifier. (GCPC-076)
4. WHEN engine certification runs THEN it SHALL report precision and recall per certified area against the labeled corpora. (GCPC-077)
5. WHEN engine certification runs THEN it SHALL apply the normative thresholds: entry points 99 percent precision and 95 percent recall; linked calls 99 and 90; contracts 99 and 95; persistence 99 and 90. (GCPC-078)
6. IF a certified area falls below its threshold THEN engine certification SHALL fail and SHALL name the area, the measured value and the failing items. (GCPC-079)
7. The system SHALL keep engine certification separate from run certification and SHALL NOT publish precision or recall in a run's package. (GCPC-080)
8. The system SHALL publish the engine-certification report as a versioned repository artifact and SHALL NOT expose engine certification as a CLI subcommand. (GCPC-081)

**Independent Test**: Author the labeled corpora as data files carrying expected outcomes and rationales, reviewed against the source, with no reference to any classifier's output. Run engine certification and assert each area's precision and recall are computed from those files and compared to the normative thresholds. Then flip one lookalike's expectation to the wrong value and assert certification fails naming that item — proving the measurement reads ground truth rather than restating classifier output.

---

### P1: Security and fidelity under new projections ⭐ MVP

**User Story**: As a repository owner, I want the new labels, catalogs and shards to carry no secret, so that making the package readable does not make it dangerous.

**Why P1**: Zero-tolerance gate in `quality-and-security.md`. Every new projection surface in this feature is a new place a redacted value could resurface.

**Acceptance Criteria**:
1. The system SHALL preserve the redaction model of sidecar declaration, original hash, published hash and ordinal-sorted redacted spans. (GCPC-082)
2. The system SHALL NOT publish a secret value, a hash of an individual secret value or an unredacted secret excerpt in any fact, observation, index, diagnostic, catalog, posting, label, page or manifest entry. (GCPC-083)
3. WHEN a compact label is derived from a payload value THEN the system SHALL prove that value is not inside a declared redacted span before publishing the label. (GCPC-084)
4. The system SHALL require separate consent before enabling source generators and SHALL NOT run diagnostic analyzers. (GCPC-085)
5. WHEN a coverage, provenance or accounting envelope is published THEN the system SHALL NOT include a credential, connection string, token, certificate or authorization value in it. (GCPC-086)

**Independent Test**: Place in the certification corpus a configuration document whose secret value would become a label under the new catalog rules. After analyze, scan every published byte of every artifact for the secret's literal value and for its individual hash, and assert neither appears anywhere; assert the label for that identity is either absent or derived from a non-redacted value; and assert the sidecar still declares both hashes and the redacted span.

---

### P1: Migration completion gate ⭐ MVP

**User Story**: As the product owner, I want an objective, reproducible checklist that says the migration is complete, so that "done" is a measurement rather than an opinion.

**Why P1**: This is the roadmap's terminal condition. Without it the feature can be declared finished while the audit verdict is unchanged.

**Acceptance Criteria**:
1. The system SHALL publish an objective LLM-readiness checklist whose criteria correspond to the audit's readiness matrix. (GCPC-115)
2. WHEN the checklist is evaluated against a package built from the certification corpus THEN every criterion SHALL report PASS. (GCPC-116)
3. The system SHALL reproduce every regression the audit found in a versioned fixture covered by a continuous-integration test. (GCPC-117)
4. The system SHALL NOT require the local eShop or eShopOnContainers clone for any mandatory gate. (GCPC-118)
5. WHERE a local eShop or eShopOnContainers clone with its expected solution file is present, the system SHALL evaluate the same checklist against it and report the result. (GCPC-119)
6. WHEN the documented retrieval scenarios are satisfied within budget with no query engine, and provenance, budgets and independent validation are all published, THEN the system SHALL report the migration as complete. (GCPC-120)

**Independent Test**: Run the checklist evaluator against a package built from the certification corpus and assert every criterion the audit rated PARTIAL or FAIL — semantic legibility, scale, factual coverage, run certification, classification reliability and overall readiness — now reports PASS with cited evidence. Assert the evaluator runs with the eShop clones absent, and that when a clone is present the same evaluator runs against it and reports without failing the build on absence.

---

### P2: Contract and message-operation accounting

**User Story**: As an LLM asking who produces and who consumes a message, I want every message operation and payload slot to reach an explicit outcome, so that no contract is not the same claim as no messaging.

**Why P2**: Audit finding I2. The package held 62 message-operation observations and six outbound messaging operations but published no contract, catalog or posting — provable as an accounting gap, not yet as a false negative.

**Acceptance Criteria**:
1. WHEN the system recognizes a message operation or a boundary payload slot THEN it SHALL publish for it exactly one outcome among contract binding, candidate, unresolved record and declared exclusion. (GCPC-087)
2. The system SHALL publish the count of recognized message operations and payload slots alongside the count reaching each outcome, and those counts SHALL sum to the recognized total. (GCPC-088)
3. The system SHALL NOT present the absence of a contract as the absence of messaging in any envelope, catalog or page. (GCPC-089)
4. The system SHALL NOT create a contract from name similarity, structural similarity, path or prefix. (GCPC-090)
5. WHEN a contract identity is proven shared THEN the system SHALL make its producers and consumers recoverable from postings in one hop from that contract. (GCPC-091)
6. IF a payload slot's contract identity cannot be proven THEN the system SHALL publish it as candidate or unresolved with a declared cause and SHALL NOT publish it as a contract. (GCPC-092)

**Independent Test**: In the certification corpus, include a published event with a handler in the same solution, a published event with no handler, and two same-named payload types in unrelated projects. After analyze, assert the first yields a contract whose producers and consumers are both reachable in one posting hop; the second yields an explicit unresolved or excluded outcome and not a contract; the two same-named types yield no shared contract; and the recognized message-operation count equals the sum of the per-outcome counts.

---

### P2: Legible catalogs and pages

**User Story**: As a reader scanning a catalog, I want compact labels drawn from the factual authority, so that I can pick the right identity without decoding a percent-encoded ID.

**Why P2**: Audit finding I1. Determinism and authority are preserved today at a large cost in search noise, but the package is usable without labels while it is unusable without certification.

**Acceptance Criteria**:
1. WHEN a catalog entry or Markdown page is published THEN it SHALL carry compact labels for the component, type, method, protocol, verb and route values proven for that identity. (GCPC-093)
2. The system SHALL derive every published label from a value present in the authoritative payload and SHALL cite the artifact key and ordinal it came from. (GCPC-094)
3. The system SHALL keep the canonical identity as the authority and SHALL NOT treat a label or a Markdown page as authoritative. (GCPC-095)
4. The system SHALL NOT publish a Markdown page whose only descriptive title is a fact type followed by an encoded identity. (GCPC-096)
5. IF a published label, link, ordinal, locator or repeated value disagrees with the authoritative payload THEN the system SHALL abort the publication and name the offender. (GCPC-097)
6. WHERE a label's underlying value is not proven, the system SHALL omit that label and SHALL NOT substitute an inferred value. (GCPC-098)

**Independent Test**: After analyzing the certification corpus, parse every published catalog entry and Markdown page, resolve every label to the artifact key and ordinal it cites, and assert the cited payload holds that exact value. Assert no page title consists solely of a fact type plus an encoded identity. Then publish a page whose label is altered by one character and assert the publication aborts naming that page and value.

---

### P2: HTTP verb and route as first-class fields

**User Story**: As an LLM answering which endpoint serves a request, I want the verb and route in their own fields, so that I do not have to parse a composite key to learn what was already observed.

**Why P2**: Audit finding I3. All fifteen HTTP boundary operations left `http_method` and `route` empty while carrying both inside `protocol_operation_key`.

**Acceptance Criteria**:
1. WHEN a boundary operation's HTTP verb and route are both proven THEN the system SHALL publish them in the HTTP method and route fields of that operation. (GCPC-099)
2. The system SHALL NOT make the protocol operation key the only published means of discovering a verb or route that was already observed. (GCPC-100)
3. WHEN a Markdown page or catalog entry presents an HTTP boundary operation THEN it SHALL present the published verb and route values and SHALL NOT synthesize either. (GCPC-101)
4. IF only one of the verb and the route is proven THEN the system SHALL publish the proven field, SHALL leave the other unset and SHALL record the operation as candidate or unresolved for the unproven part. (GCPC-102)

**Independent Test**: In the certification corpus, include an inbound action with an explicit verb attribute and route template, an inbound action with a verb but a route derived only from convention, and an outbound client call with both. After analyze, read the published architecture facts and assert the first and third publish both fields, that the second publishes the verb and records the route as unresolved, and that each corresponding catalog entry and page shows the same values as the payload.

---

### P2: Configuration and inventory triage

**User Story**: As a maintainer reading diagnostics, I want them to name real problems, so that a solution folder is not reported as a missing project and valid configuration is not reported as malformed.

**Why P2**: Audit finding I4. Twenty-six `missing-project` entries include solution folders and twenty-one `malformed-configuration-document` entries may be syntax the .NET configuration provider accepts.

**Acceptance Criteria**:
1. WHEN a solution entry is a solution folder THEN the system SHALL NOT diagnose it as a missing project. (GCPC-103)
2. WHEN a listed project path is genuinely absent THEN the system SHALL continue to diagnose it, naming the referencing solution and the missing path. (GCPC-104)
3. The system SHALL publish an explicit configuration-parsing policy stating its treatment of comments, trailing commas and the supported document variants, matching what the .NET configuration provider accepts. (GCPC-105)
4. IF a configuration document is outside the published parsing policy THEN the system SHALL continue to diagnose it as malformed. (GCPC-106)
5. The system SHALL NOT promote a configuration binding from a document whose structure is ambiguous under the published parsing policy. (GCPC-107)

**Independent Test**: Build a solution in the certification corpus containing a solution folder, a genuinely missing project reference, an `appsettings.json` with comments and a trailing comma, and an `appsettings.json` with an unterminated object. After analyze, assert no diagnostic names the solution folder, that the missing project is diagnosed naming both identities, that the tolerant document yields its configuration bindings with no malformed diagnostic, and that the unterminated document is still diagnosed and yields no binding.

---

### P2: Determinism, isolation and batch certification

**User Story**: As a maintainer, I want the whole package — shards, provenance and composition included — to be reproducible, so that comparing two runs compares the input rather than the environment.

**Why P2**: Standing engineering constraint. Every prior workstream asserted it; this one adds shards, labels and provenance, each of which could break it.

**Acceptance Criteria**:
1. WHEN the same solution is analyzed twice THEN the system SHALL produce byte-identical factual, projection, provenance and composition artifacts. (GCPC-108)
2. WHEN the same repository is analyzed from two different absolute paths THEN the system SHALL produce byte-identical identities and artifact bytes. (GCPC-109)
3. WHEN the same solutions are presented in a different input or enumeration order THEN the system SHALL produce byte-identical identities and artifact bytes. (GCPC-110)
4. WHEN a batch contains more than one solution THEN the system SHALL keep each solution's semantics isolated and SHALL NOT let one solution's facts enter another's package. (GCPC-111)
5. WHEN every required solution in a batch is published, provenance-compatible and individually certifiable THEN the system SHALL certify the batch. (GCPC-112)
6. IF a required solution is unpublished, provenance-incompatible or not certifiable THEN the system SHALL declare the batch scope incomplete, SHALL name the reason and SHALL NOT certify the batch. (GCPC-113)
7. WHEN a batch declares incomplete scope THEN the system SHALL leave every committed per-solution package unmodified and individually valid. (GCPC-114)

**Independent Test**: Analyze the certification corpus and a second solution together, twice, from two parent directories with the `--solution` arguments reversed, and assert every file in both packages and in the composition is byte-identical across runs. Then repeat with one solution forced to fail, and assert the batch declares incomplete scope with a named reason, is not certified, and that the successful solution's package bytes match the fully successful run exactly.

---

## Edge Cases

- IF a recognized invocation occurrence would receive two dispositions not required by the taxonomy THEN the system SHALL publish the run as `failed` and SHALL name the occurrence and both dispositions (GCPC-011, GCPC-013).
- IF every document in a project is excluded by the supported-document policy THEN the system SHALL publish the project fact with no documents and SHALL count the whole project in the aggregated exclusion totals (GCPC-028, GCPC-034).
- IF the derived byte ceiling would be smaller than the largest indivisible single record THEN the system SHALL publish that record in a shard of its own, SHALL record a degradation reason and SHALL NOT truncate the record (GCPC-038, GCPC-039).
- IF a documented retrieval scenario has no instance in the analyzed input THEN the system SHALL mark that scenario not exercised with a reason and SHALL NOT report it as passed (GCPC-053, GCPC-054).
- IF a `validate` target directory holds no manifest THEN the system SHALL exit with code `1` and SHALL leave the directory unchanged (GCPC-064, GCPC-073).
- IF a package's provenance names a generator version newer than the running one THEN `validate` SHALL exit with code `6` and SHALL NOT report a certification status for it (GCPC-071).
- IF a labeled-corpus item is unreachable because its fixture no longer contains the labeled construct THEN engine certification SHALL fail naming that item rather than silently dropping it from the denominator (GCPC-079).
- WHEN a solution produces no facts at all THEN the system SHALL publish the manifest, the registry and the provenance, SHALL publish every mandatory metric as `not_applicable` and SHALL publish the status `degraded` (GCPC-009, GCPC-010, GCPC-056).
- IF an accepted document's bytes cannot be read THEN the system SHALL record a degradation reason against the affected coverage metrics and SHALL NOT silently exclude the document under the supported-document policy (GCPC-007, GCPC-028).
- WHEN a conventional action's route is proven only by convention THEN the system SHALL publish the verb, record the route as unresolved and still publish the entry point (GCPC-022, GCPC-102).

---

## Non-functional requirements

| Area | Requirement | Covered by |
| --- | --- | --- |
| Scale | No published artifact exceeds the derived ceiling; high-cardinality payloads shard deterministically | GCPC-038, GCPC-039, GCPC-042, GCPC-044 |
| Retrieval cost | Each documented scenario completes within 100,000 read tokens and 25 file reads, measured and published | GCPC-036, GCPC-052, GCPC-053 |
| Determinism | Identities and bytes are independent of absolute path, input order and enumeration order, provenance included | GCPC-060, GCPC-108, GCPC-109, GCPC-110 |
| Performance baseline | Peak memory, per-stage time, file count, factual and index bytes, largest artifact and per-scenario cost are recorded in the measurements envelope | GCPC-045, GCPC-052, GCPC-059 |
| Security | No secret reaches any published artifact, including new labels and shards | GCPC-083, GCPC-084, GCPC-086 |
| Trust boundary | Local execution only; semantic analysis requires trusted input; generators need separate consent; analyzers never run | GCPC-085 |
| Compatibility | The taxonomy registry stays byte-identical; version axes and provenance decide comparability; incompatibility is an exit code, not a silent read | GCPC-057, GCPC-058, GCPC-071 |

## Failure and degradation semantics

| Condition | Outcome | Requirement |
| --- | --- | --- |
| Legitimate candidate, unresolved record or open frontier | Published with its cause; run may still be `passed` | GCPC-006, GCPC-014 |
| Degradation reason, unknown, or unsupported capability that can hide covered areas | Run `degraded`; exit `3`; package published | GCPC-007, GCPC-070 |
| Every mandatory metric `not_applicable` | Run `degraded`, never `passed` | GCPC-009, GCPC-010 |
| Quarantined derived fact, classifier conflict on a covered area, unaccounted invocation | Run `failed`; exit `4`; remaining valid artifacts still published | GCPC-008, GCPC-013, GCPC-070 |
| Identity collision, invalid hash, dangling reference, structural corruption | Publication aborted or package rejected; exit `5`; last valid package preserved | GCPC-065, GCPC-071 |
| Incompatible provenance or contract version | Refused; exit `6`; no certification status reported | GCPC-071 |
| One solution of a batch unpublished | Batch scope incomplete and uncertified; exit `2`; committed packages untouched | GCPC-072, GCPC-113, GCPC-114 |
| Invalid invocation | Nothing published; exit `1` | GCPC-073 |

## Observability and envelopes

| Envelope | Content added or changed here | Requirement |
| --- | --- | --- |
| `manifest` | Provenance, per-artifact entry count and byte size, full reachability | GCPC-056..GCPC-062 |
| `coverage` | Numerator, denominator, exclusions, unknowns, per-reason counts, `not_applicable` with reason | GCPC-002..GCPC-004, GCPC-009 |
| `run_certification` | `passed`, `degraded` or `failed`, with the reasons that produced it | GCPC-001, GCPC-006..GCPC-008 |
| Invocation accounting | Per-disposition totals summing to recognized occurrences, with exclusion categories | GCPC-012, GCPC-016 |
| Contract accounting | Recognized message operations and payload slots against per-outcome counts | GCPC-088 |
| Document policy | Accepted and excluded counts and bytes per category; at most one aggregated exclusion diagnostic | GCPC-033, GCPC-034 |
| `measurements` | Timestamps, durations, largest artifact per role, per-scenario retrieval cost | GCPC-045, GCPC-052, GCPC-059 |
| `diagnostics` | Solution folders removed from `missing-project`; configuration policy applied; individual `unsupported-document` removed | GCPC-028, GCPC-103, GCPC-106 |
| Engine certification report | Per-area precision and recall against thresholds, versioned in the repository | GCPC-077, GCPC-078, GCPC-081 |

---

## Test Coverage Matrix

| Layer | Scope | Requirements covered |
| --- | --- | --- |
| Unit | Certification status transitions, denominator arithmetic, disposition exhaustiveness, ceiling derivation, shard assignment, policy matching, exit-code mapping | GCPC-001, GCPC-006..GCPC-011, GCPC-015, GCPC-026, GCPC-029, GCPC-037, GCPC-042, GCPC-069..GCPC-073 |
| Integration | Publication through `Commit()` of coverage, provenance, accounting, sharded payloads, labels and the guide; abort paths | GCPC-002..GCPC-005, GCPC-012..GCPC-014, GCPC-038..GCPC-041, GCPC-055..GCPC-062, GCPC-093..GCPC-098 |
| End-to-end | `analyze` then `validate` then `compose` over the certification corpus; each `validate` defect class; every documented retrieval scenario walked and measured | GCPC-046..GCPC-054, GCPC-063..GCPC-068, GCPC-115..GCPC-120 |
| Labeled corpus | Independently authored ground truth with positives, negatives and lookalikes per classifier; precision and recall against thresholds | GCPC-074..GCPC-081 |
| Regression | Private-helper entry point; interface-to-implementation chain; framework-call exclusion; message operation without contract; excluded frontend and binary assets; solution folder; tolerant configuration | GCPC-016..GCPC-025, GCPC-028, GCPC-032, GCPC-087..GCPC-092, GCPC-103..GCPC-107 |
| Determinism | Two runs, two absolute paths, reversed input order, byte comparison across factual, projection, provenance and composition artifacts | GCPC-060, GCPC-108..GCPC-111 |
| Security | Secret literal and individual hash absent from every published byte; label derived only from non-redacted values; sidecar integrity | GCPC-082..GCPC-086 |
| Performance and scale | Deterministic over-ceiling scale input; no artifact above the ceiling; three named payloads split; per-scenario budget measured | GCPC-036, GCPC-043..GCPC-045, GCPC-052, GCPC-053 |
| Batch | Multi-solution isolation, batch certification, incomplete scope, committed packages untouched | GCPC-112..GCPC-114 |
| LocalCorpus (optional) | The same objective checklist re-run against `fixtures/eShop` or `fixtures/eShopOnContainers` when the expected solution file exists; skipped, never failed, when absent | GCPC-118, GCPC-119 |

---

## Requirement Traceability

| Requirement ID | Story | Origin | Phase | Status |
| --- | --- | --- | --- | --- |
| GCPC-001 | P1: Certified execution | Audit B1; AD-009 | Design | Verified |
| GCPC-002 | P1: Certified execution | quality-and-security.md run coverage | Design | Verified |
| GCPC-003 | P1: Certified execution | quality-and-security.md denominator table | Design | Verified |
| GCPC-004 | P1: Certified execution | Audit B1 | Design | Verified (vacuous) |
| GCPC-005 | P1: Certified execution | AD-009; quality-and-security.md | Design | Verified |
| GCPC-006 | P1: Certified execution | AD-009 | Design | Verified |
| GCPC-007 | P1: Certified execution | quality-and-security.md degradation | Design | Verified |
| GCPC-008 | P1: Certified execution | quality-and-security.md degradation; STOR-32 | Design | Verified |
| GCPC-009 | P1: Certified execution | Audit B1 zero denominators | Design | Verified |
| GCPC-010 | P1: Certified execution | Audit B1 zero denominators | Design | Verified |
| GCPC-011 | P1: Invocation accounting | Audit B3; audit gate 2 | Design | Verified |
| GCPC-012 | P1: Invocation accounting | Audit gate 2 | Design | Verified |
| GCPC-013 | P1: Invocation accounting | Audit B3 | Design | Verified |
| GCPC-014 | P1: Invocation accounting | CLLF-13; taxonomy.md evidence dimensions | Design | Verified |
| GCPC-015 | P1: Invocation accounting | AD-009 | Design | Verified |
| GCPC-016 | P1: Invocation accounting | Supersedes CLLF-20 silence | Design | Verified |
| GCPC-017 | P1: Invocation accounting | CONTEXT.md Open Frontier; CLLF-11 | Design | Verified |
| GCPC-018 | P1: Invocation accounting | CLLF-07 and CLLF-09; audit B3 | Design | Verified |
| GCPC-019 | P1: Entry capability | Audit B2; CONTEXT.md Entry Point | Design | Verified |
| GCPC-020 | P1: Entry capability | Audit B2; supersedes EBC-05 | Design | Verified |
| GCPC-021 | P1: Entry capability | Audit B2; supersedes EBC-05 | Design | Verified |
| GCPC-022 | P1: Entry capability | Preserves EBC-08 | Design | Verified |
| GCPC-023 | P1: Entry capability | AD-004 evidence before promotion | Design | Verified |
| GCPC-024 | P1: Entry capability | AD-010 proof states | Design | Verified |
| GCPC-025 | P1: Entry capability | Audit gate 3; D-01 | Design | Verified |
| GCPC-026 | P1: Document policy | D-04; audit I4 | Design | Verified |
| GCPC-027 | P1: Document policy | D-04; supersedes ROSE-04 | Design | Verified |
| GCPC-028 | P1: Document policy | D-04; supersedes ROSE-05 and ROSE-06 | Design | Verified |
| GCPC-029 | P1: Document policy | D-04; architecture-knowledge-engine.md supported sources | Design | Verified |
| GCPC-030 | P1: Document policy | D-04 allowlist | Design | Verified |
| GCPC-031 | P1: Document policy | D-04 redefinition | Design | Verified |
| GCPC-032 | P1: Document policy | D-04; supersedes RP-07 | Design | Verified |
| GCPC-033 | P1: Document policy | D-04; audit I4 | Design | Verified |
| GCPC-034 | P1: Document policy | D-04 measurement | Design | Verified |
| GCPC-035 | P1: Document policy | D-04 safeguard | Design | Verified |
| GCPC-036 | P1: Bounded payloads | D-02; output-and-retrieval.md scale constraints | Design | Verified |
| GCPC-037 | P1: Bounded payloads | D-02 | Design | Verified |
| GCPC-038 | P1: Bounded payloads | Audit B4; calibrates RP-52 | Design | Verified |
| GCPC-039 | P1: Bounded payloads | Audit B4; output-and-retrieval.md | Design | Verified |
| GCPC-040 | P1: Bounded payloads | Audit gate 4 | Design | Verified |
| GCPC-041 | P1: Bounded payloads | RP-20 and RP-27 preserved after split | Design | Verified |
| GCPC-042 | P1: Bounded payloads | RP-53 and RP-54 | Design | Verified |
| GCPC-043 | P1: Bounded payloads | output-and-retrieval.md scale constraints | Design | Verified |
| GCPC-044 | P1: Bounded payloads | Audit inventory: contains, belongs-to, invocation | Design | Verified |
| GCPC-045 | P1: Bounded payloads | quality-and-security.md performance | Design | Verified |
| GCPC-046 | P1: Retrieval guide | Audit B5 | Design | Verified |
| GCPC-047 | P1: Retrieval guide | Audit B4 and B5 | Design | Verified |
| GCPC-048 | P1: Retrieval guide | Audit B5; output-and-retrieval.md scenarios | Design | Verified |
| GCPC-049 | P1: Retrieval guide | Audit B5; AD-010 | Design | Verified |
| GCPC-050 | P1: Retrieval guide | Audit B4 | Design | Verified |
| GCPC-051 | P1: Retrieval guide | output-and-retrieval.md scenario 7 | Design | Verified |
| GCPC-052 | P1: Retrieval guide | Audit gate 9; output-and-retrieval.md | Design | Verified |
| GCPC-053 | P1: Retrieval guide | D-02 budget | Design | Verified |
| GCPC-054 | P1: Retrieval guide | Audit gate 9 | Design | Verified |
| GCPC-055 | P1: Retrieval guide | RP-42 extended to the guide | Design | Verified |
| GCPC-056 | P1: Provenance | Audit scope note and gate 8 | Design | Verified |
| GCPC-057 | P1: Provenance | taxonomy.md version axes | Design | Verified |
| GCPC-058 | P1: Provenance | D-02; D-04 | Design | Verified |
| GCPC-059 | P1: Provenance | STOR-43; output-and-retrieval.md | Design | Verified |
| GCPC-060 | P1: Provenance | Standing determinism constraint | Design | Verified |
| GCPC-061 | P1: Provenance | Audit I5 | Design | Verified |
| GCPC-062 | P1: Provenance | AD-007 | Design | Verified |
| GCPC-063 | P1: CLI surface | Roadmap row 8; D-03 | Design | Verified |
| GCPC-064 | P1: CLI surface | User constraint on validate | Design | Verified |
| GCPC-065 | P1: CLI surface | quality-and-security.md zero-tolerance gates | Design | Verified |
| GCPC-066 | P1: CLI surface | quality-and-security.md degradation | Design | Verified |
| GCPC-067 | P1: CLI surface | AD-009 | Design | Verified |
| GCPC-068 | P1: CLI surface | Workstream 7 deferred compose idea | Design | Verified |
| GCPC-069 | P1: CLI surface | Roadmap row 8 | Design | Verified |
| GCPC-070 | P1: CLI surface | AD-009 status vocabulary | Design | Verified |
| GCPC-071 | P1: CLI surface | quality-and-security.md corruption path | Design | ⚠️ Partial |
| GCPC-072 | P1: CLI surface | MSC-09 and MSC-15 | Design | Verified |
| GCPC-073 | P1: CLI surface | Existing CLI contract | Design | Verified |
| GCPC-074 | P1: Engine certification | quality-and-security.md corpora | Design | Verified |
| GCPC-075 | P1: Engine certification | quality-and-security.md ground truth | Design | Verified |
| GCPC-076 | P1: Engine certification | quality-and-security.md corpora | Design | Verified |
| GCPC-077 | P1: Engine certification | AD-009 | Design | Verified |
| GCPC-078 | P1: Engine certification | quality-and-security.md engine gates | Design | Verified |
| GCPC-079 | P1: Engine certification | Roadmap completion condition | Design | Verified |
| GCPC-080 | P1: Engine certification | AD-009 | Design | Verified |
| GCPC-081 | P1: Engine certification | D-03 | Design | Verified |
| GCPC-082 | P1: Security and fidelity | AD-021 | Design | Verified |
| GCPC-083 | P1: Security and fidelity | quality-and-security.md zero-tolerance gates | Design | Verified |
| GCPC-084 | P1: Security and fidelity | AD-021 plus the new label surface | Design | Verified |
| GCPC-085 | P1: Security and fidelity | AD-003 | Design | Verified |
| GCPC-086 | P1: Security and fidelity | quality-and-security.md security | Design | Verified |
| GCPC-087 | P2: Contract accounting | Audit I2 | Design | ❌ Needs Fix |
| GCPC-088 | P2: Contract accounting | Audit I2; AD-009 | Design | Verified |
| GCPC-089 | P2: Contract accounting | Audit I2 | Design | Verified |
| GCPC-090 | P2: Contract accounting | taxonomy.md contracts | Design | Verified |
| GCPC-091 | P2: Contract accounting | output-and-retrieval.md postings | Design | Verified |
| GCPC-092 | P2: Contract accounting | AD-010 | Design | ❌ Needs Fix |
| GCPC-093 | P2: Legible projections | Audit I1 | Design | Verified |
| GCPC-094 | P2: Legible projections | RP-34; AD-007 | Design | Verified |
| GCPC-095 | P2: Legible projections | AD-007; output-and-retrieval.md Markdown | Design | Verified |
| GCPC-096 | P2: Legible projections | Audit I1 | Design | Verified |
| GCPC-097 | P2: Legible projections | RP-44 extended to labels | Design | Verified |
| GCPC-098 | P2: Legible projections | RP-33 | Design | Verified |
| GCPC-099 | P2: HTTP verb and route | Audit I3; extends EBC-06 | Design | Verified |
| GCPC-100 | P2: HTTP verb and route | Audit I3 | Design | Verified |
| GCPC-101 | P2: HTTP verb and route | Audit I3; RP-33 | Design | Verified |
| GCPC-102 | P2: HTTP verb and route | AD-010 | Design | Verified |
| GCPC-103 | P2: Configuration triage | Audit I4 | Design | Verified |
| GCPC-104 | P2: Configuration triage | ROSE-11; CDC missing-project | Design | Verified |
| GCPC-105 | P2: Configuration triage | Audit I4 | Design | Verified |
| GCPC-106 | P2: Configuration triage | Audit I4 | Design | Verified |
| GCPC-107 | P2: Configuration triage | AD-004 | Design | Verified |
| GCPC-108 | P2: Determinism and batch | Standing determinism constraint | Design | Verified |
| GCPC-109 | P2: Determinism and batch | ROSE-52; MSC-05 | Design | Verified |
| GCPC-110 | P2: Determinism and batch | ROSE-54; MSC-06 | Design | Verified |
| GCPC-111 | P2: Determinism and batch | AD-008 | Design | Verified |
| GCPC-112 | P2: Determinism and batch | output-and-retrieval.md composition | Design | Verified |
| GCPC-113 | P2: Determinism and batch | output-and-retrieval.md partial composition | Design | Verified |
| GCPC-114 | P2: Determinism and batch | MSC-09 and MSC-15 | Design | Verified |
| GCPC-115 | P1: Completion gate | Audit readiness matrix | Design | Verified |
| GCPC-116 | P1: Completion gate | Audit verdict; D-01 | Design | Verified |
| GCPC-117 | P1: Completion gate | D-01 | Design | Verified |
| GCPC-118 | P1: Completion gate | Standing LocalCorpus constraint | Design | Verified |
| GCPC-119 | P1: Completion gate | Standing LocalCorpus constraint | Design | Verified |
| GCPC-120 | P1: Completion gate | Roadmap completion section | Design | Verified |

**ID format:** `GCPC-NNN`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 120 total, 0 mapped to tasks, 120 unmapped ⚠️ (Tasks phase not started)

---

## Definition of Done

Measurable. Every item is checked by a test or a script, not by inspection.

- [ ] No published package contains the string `not_evaluated`; every run publishes `passed`, `degraded` or `failed` and the status matches the rules in GCPC-006 through GCPC-010.
- [ ] Each of the four mandatory coverage metrics publishes a denominator that an independent test re-derives from the same publication and finds equal.
- [ ] The invocation-accounting totals equal the recognized invocation-occurrence count on the certification corpus, with no occurrence counted twice.
- [ ] The private-helper regression, the interface-to-implementation regression, the framework-call exclusion, the message-operation-without-contract case, the excluded-asset case, the solution-folder case and the tolerant-configuration case each exist in a versioned fixture with a CI test that fails when the defect is reintroduced.
- [ ] Engine certification runs against independently authored labeled corpora and meets every normative threshold; flipping one ground-truth label makes it fail.
- [ ] No file in a published package exceeds the derived per-artifact ceiling, and the three named high-cardinality payloads split under the scale input.
- [ ] Every scenario documented in the published `retrieval.md` is executed automatically, reaches its endpoint, and reports files, hops, bytes, tokens, relevant facts and noise within the declared budget.
- [ ] The manifest carries provenance and a per-artifact count and byte size that a test proves equal to the artifact's real content, and every file in the package is reachable from it.
- [ ] `validate` audits a published package with no access to the original solution and returns the mapped exit code for each of the seven defect classes.
- [ ] `compose` produces the batch artifacts from published packages with no solution present.
- [ ] The secret literal and its individual hash appear in no published byte of any artifact, labels included.
- [ ] Two runs from two absolute paths with reversed input order produce byte-identical factual, projection, provenance and composition artifacts.
- [ ] The objective LLM-readiness checklist reports PASS on every criterion for a package built from the certification corpus, with the eShop clones absent.
- [ ] `.specs/STATE.md` records the amended versioned-fixture constraint and the decisions this feature introduces.
- [ ] `validate_state.py generator-cli-projections-certification` is clean and the full test suite passes, excluding `Category=LocalCorpus`.

## Success Criteria

- [ ] The audit's readiness matrix moves from six FAIL and one PARTIAL to PASS on every row, evaluated on a corpus that lives in git.
- [ ] An LLM can answer "which entry point starts this operation, what does it traverse, what contracts and data does it touch, and where does the evidence stop" using only published files, inside one context window.
- [ ] No published claim of completeness rests on a denominator the consumer cannot re-derive from the same package.
- [ ] The temporary usage restrictions in the audit's "Regra de uso temporário" section become unnecessary and are retired.
