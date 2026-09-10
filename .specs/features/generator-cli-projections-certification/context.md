# Generator CLI, Projections and Certification — User Decisions

Captured during Discuss inside Specify, 2026-08-27. These four decisions were the
gray areas whose different answers would have produced materially different work.
Everything else in `spec.md` is an agent default logged in Assumptions & Open Questions.

---

## D-01 — The completion gate is a dual gate, not an eShop gate

**Question**: the completion gate requires the eShop LLM-readiness audit to move from
"não apto" to PASS, but `fixtures/eShop` and `fixtures/eShopOnContainers` are gitignored
local clones. CI never has them, so an eShop-only gate is unreproducible.

**Decision**: dual gate.

- A new versioned, C#-only certification corpus reproduces **every** regression the audit
  found (B1–B5, I1–I5) and every excluded-asset case, and runs in CI as the mandatory gate.
- The eShop / eShopOnContainers clones become an **optional** `LocalCorpus` re-run of the
  same objective checklist, executed only when the expected `.sln` exists, and never a CI
  failure when absent.

**Consequence**: the Definition of Done is measurable in CI. The standing constraint
"the only versioned analysis fixture is `fixtures/SyntheticSolution`" is amended by this
feature to admit a second versioned corpus; `.specs/STATE.md` must record that amendment.

---

## D-02 — Byte and token ceilings are derived from a declared reading budget

**Question**: the numeric per-artifact ceiling must be calibrated and justified, not
chosen arbitrarily — but the corpus that exposed 169 MiB / 21 MiB / 16 MiB payloads is
not in CI.

**Decision**: the ceiling is **derived**, not measured off eShop.

1. The feature declares a per-scenario reading budget in tokens and file reads.
2. It measures the package's own bytes-per-token ratio on its canonical JSON.
3. It derives the per-artifact byte ceiling from that budget by a published calculation.
4. The versioned scale input proves no published artifact exceeds the derived ceiling and
   that the three high-cardinality payload kinds split.
5. An eShop measurement, when the clone exists, is recorded as **evidence of scale** — it
   never becomes the source of the number.

**Consequence**: the number is reproducible from the package itself, and CI can re-derive it.

---

## D-03 — Three CLI verbs; engine certification is a test suite, not a command

**Question**: where does labeled-corpus precision/recall live relative to
`analyze` / `validate` / `compose`?

**Decision**: three verbs only.

- `analyze` produces the package and publishes **run** certification into it.
- `validate` audits an already-published package without re-analyzing the solution.
- `compose` recomposes already-published packages into a batch.
- **Engine** certification runs as a test suite against the labeled corpora and publishes a
  versioned report into the repository. It is not a user-facing command and the labeled
  corpora are not product surface.

**Consequence**: AD-009's separation of engine certification from run certification is
preserved at the CLI boundary, and no test corpus leaks into the shipped surface.

---

## D-04 — Unsupported documents leave the inventory entirely

**Question**: the audit found 549 `unsupported-document` diagnostics. How far does the
supported-document policy reach?

**Decision**: a document outside the policy is **excluded from the inventory**.

- No `Document` fact, no `source/` projection, no structural relation, no individual
  `unsupported-document` diagnostic.
- At most one **aggregated** diagnostic carrying the excluded count and the excluded
  extensions.
- Accepted and excluded counts and bytes are measured and published per category.
- An explicit allowlist re-includes additional documents without returning to unrestricted
  enumeration.

**Consequence**: this supersedes `ROSE-04`, `ROSE-05`, `ROSE-06` and `RP-07` where they
require indiscriminate inventory or projection. "Every analyzed document" is redefined as
"every document accepted by the supported-document policy".

---

## Deferred Ideas

Surfaced during Execute; out of scope for the task that found them, not acted on.

- **`InvokesPass.ConcreteImplementors` over-matches** (found in T23, 2026-09-09): implementor
  lookup matches by metadata name + parameter shape + arity only, so the certification
  corpus's interface-dispatch fixture produces a spurious self-referencing candidate link
  alongside the correct one. Pre-existing, not introduced by this feature. Not fixed —
  outside T23's file scope — and T23's tests were written to not silently accept it as
  correct. Candidate for a follow-up `fix(analysis)` task if a future workstream needs exact
  implementor-set precision.
- **Requirement traceability gap for GCPC-019** (found during Phase 3+4 batch, 2026-09-09):
  T15 (`SymbolFacets.cs`) is the only task listing GCPC-019 in its `**Requirement**:` field,
  but T15 only adds the facet — the behavioral AC ("publish an `EntryPoint` fact only for a
  callable with positive evidence") is actually closed by T17's promotion-predicate change.
  T17's own `**Requirement**:` field doesn't re-list GCPC-019, so per the per-task marking
  rule the traceability table still shows it `Pending` even though the behavior is live and
  tested. This is a `tasks.md` authoring gap, not a functional gap. **Resolved** by the
  orchestrator in commit `0fd25b4` (flipped to `Verified` on the strength of T17's tests).
- **The live `analyze` pipeline does not enforce the real byte ceiling** (found in Phase 6+7
  batch, T37, 2026-09-09): `CeilingCalculator` (T33) derives a real ~32 KiB ceiling and
  `LayoutPlanner`/`PublishedPackageView` (T34-T36) can shard against any ceiling passed in,
  fully proven under an explicit test ceiling. But `PublishedPackageView.From(WireDocument)`'s
  single-argument overload — the one `PublicationPipeline.Publish` and `PackagePublisher`
  actually call for every live `analyze` run — passes `int.MaxValue`, not the derived value,
  because passing the real ceiling broke a wide swath of pre-existing Analysis-layer tests
  that assume every family stays a single unsplit artifact. `design.md` line 234 lists
  `LayoutPlanner`'s dependencies as `CeilingCalculator, InternTableBuilder` — i.e. the design
  intends the real ceiling to always be live, not an opt-in. No task in `tasks.md` explicitly
  says "wire the derived ceiling as the enforced default" — T52 ("Expose the allowlist and
  budget options on analyze") is the closest fit (it depends on T33 and touches
  `CommandFactory.cs`) but its own Done-when only checks that a *supplied* budget reaches
  provenance and rejects bad input, not that the default pipeline enforces it. **This is the
  literal defect the feature exists to fix** (the audit's 169 MiB `contains.json`) — GCPC-038
  stays `Pending` until it's closed for real. The orchestrator is folding "make the derived
  ceiling the live default, fixing whatever Analysis-layer fallout results by rewriting those
  tests to be shard-aware rather than assuming one flat file" into the T52 batch dispatch
  rather than leaving it implicit. If that turns out too large for one task, split it into its
  own follow-up task before the Completion Gate — do not let T66 close the roadmap with this
  still open.
  **Resolved** in T52's commit: `PublicationPipeline.Publish` now derives the ceiling from
  `CeilingCalculator.Derive(readingBudgetTokens, maxFileReadsPerScenario)` (defaulting to the
  same declared defaults absent a CLI override) and plans every family against it, replacing the
  hardcoded `int.MaxValue`; `ManifestBuilder`/`ProvenanceDto` publish that same ceiling and an
  allowlist digest in `manifest.json`'s provenance. `analyze` gained `--allowlist`,
  `--reading-budget-tokens` and `--max-file-reads-per-scenario`, validated before any analysis
  begins (a non-positive budget or an out-of-root allowlist entry exits `1` and publishes
  nothing). Closing this for real (rather than leaving `PublishedPackageView.From(WireDocument)`'s
  unsplit default live) surfaced three further bugs no prior task had a way to find, because
  nothing before T52 ever made a real `analyze` run actually shard a family:
  (1) `CatalogProjector.AddUnknowns` threw `InvalidOperationException` (`.Single()` on an empty
  sequence) the moment `relations/unresolved.json` actually split, because it looked for the
  literal unsplit key instead of resolving each unknown's shard-aware citation;
  (2) `PostingProjector`'s `AddIndexed`-built `postings/unknowns.json` and `postings/frontiers.json`
  silently cited the unsplit base key at the record's *document*-order ordinal even when the family
  was split, producing wrong citations `ProjectionValidator` would reject once decoded against a
  real shard; (3) T48's own `validate` reconstructed its `PublishedPackageView` with the unsplit
  default ceiling regardless of what ceiling the package was actually published under, so
  re-validating a real sharded package failed with a false `projection-key`. Fixed by: giving
  `LayoutPlan` per-record citation arrays for candidates, unresolved records and open frontiers
  (mirroring the existing `RelationLocations` for confirmed relations) and exposing them on
  `PublishedPackageView` as `TryLocateCandidate`/`TryLocateUnresolved`/`TryLocateFrontier`;
  rewriting `CatalogProjector.AddUnknowns` and `PostingProjector`'s two `AddIndexed` call sites to
  resolve through those instead of a hardcoded key; and having `validate` re-plan with the ceiling
  read from the package's own published provenance. Proven by
  `tests/Csharp2Md.Cli.Tests/ComposeCommandTests.cs`'s two-solution batch (the only fixture in the
  suite large enough to actually shard `relations/unresolved.json`) and by rewriting eight
  Analysis-layer tests across six files to merge shards instead of assuming one flat file per
  family — see T52's deviation note in `tasks.md` for the full list. A distinct, still-open gap in
  `RetrievalGuideProjector`'s own prose (not a crash, and not proven by any current test) is
  recorded as its own new Deferred Idea immediately below, not folded into this resolution.
- **`RetrievalScenarioRunner` is not wired into the live `analyze`/`validate` pipeline** (found
  in Phase 8 batch, T47, 2026-09-10): `RetrievalScenarioRunner.Run` (GCPC-052..GCPC-054) is
  fully implemented and tested against real published packages through both
  `StagedFragmentArtifactSource` (in-memory, matching the fragments an `analyze` run stages)
  and `PackageDirectoryArtifactSource` (on disk, matching what `validate` will read), and
  `ScenarioReport.ToMeasurementRecords()` round-trips correctly through the `measurements.json`
  envelope shape (`MeasurementRecordDto` gained nine nullable fields for this — see T47's
  deviation note in `tasks.md`). But nothing in `PublicationPipeline.Publish` or
  `CommandFactory` actually calls `RetrievalScenarioRunner.Run` and folds its
  `ToMeasurementRecords()` into the `measurements.json` a real `analyze` run publishes, or into
  what a real `validate` run reports. This mirrors the T37 ceiling-wiring gap exactly: the
  component is correct and proven in isolation, but nothing in this batch's file scope
  (`src/Csharp2Md.Storage/Retrieval/RetrievalScenarioRunner.cs`) reaches the CLI. Phase 9's CLI
  work (`analyze`/`validate`/`compose`, `CommandFactory`) is the natural place to wire this in —
  fold "run the scenario runner during `analyze` publication and during `validate`, publishing
  its records into `measurements.json`" into that batch's dispatch, the same way T37's gap was
  folded into T52. GCPC-052..GCPC-054 are marked `Verified` on the strength of the runner's own
  tests (the requirement text is about the runner's behavior, not the CLI verb), but the
  Independent Test for "Executable retrieval guide" (running every scenario against the
  *published* certification-corpus package) and the Definition of Done bullet about
  `retrieval.md` scenarios being "executed automatically" both need this wiring to be live
  before the Completion Gate — do not let T66 close the roadmap with this still open either.
  **Resolved** in T48's commit: `PublicationPipeline.Publish` now runs `RetrievalScenarioRunner`
  against the fragments a real `analyze` is about to write (gated on a real `retrieval.md` being
  among them, so no projector test double across Storage/Projection regresses) and folds its
  measurement records into `measurements.json`; `validate` runs the same scenarios read-only from
  the package on disk and reports them without republishing anything. Proven by
  `tests/Csharp2Md.Cli.Tests/ValidateCommandTests.cs`'s
  `Analyze_FoldsRetrievalScenarioMeasurementsIntoMeasurementsJson_AndValidateReportsThem`. Closing
  this end-to-end also surfaced and fixed two independent pre-existing `PackageValidator` bugs
  (deferred-artifact byte-size cardinality, and a false-positive `//`/`///` comment-marker match in
  `IsAbsoluteFilesystemPath`) that had never been exercised before `validate` became the first
  caller to re-scan a real published package's own `source/` fragments end to end — see T48's
  deviation note in `tasks.md` for both.
- **`RetrievalGuideProjector`'s "is this family recognized" checks assume no confirmed-relation,
  candidate, unresolved or frontier family is ever sharded** (found in T52, 2026-09-10, while
  wiring the derived ceiling as `analyze`'s live default -- context.md's other T37 entry, resolved
  in this same commit): `AppendRelationsSection` and `AppendDisposition` in
  `src/Csharp2Md.Projection/Guides/RetrievalGuideProjector.cs` both test `slots.Contains(artifactKey)`
  against the family's unsplit base key (e.g. `"relations/confirmed/contains.json"`,
  `"relations/candidates.json"`) to decide whether to tell the reader "select its posting bucket ...
  then read `<key>`" or "no such relation is recognized in this package" / "none is recognized in
  this package". Once T52 makes the derived ~32 KiB ceiling `analyze`'s live default, a family that
  is large enough to shard no longer has that exact base key as a real slot (only
  `<stem>.<bucket>.json` shard keys do), so the guide prints the *wrong* prose for it -- claiming a
  relation kind or a disposition class is entirely absent from the package when it is actually
  present, just sharded. This is a **false negative in generated prose**, not a crash: it never
  throws (unlike the two bugs T52 did fix in `CatalogProjector.AddUnknowns` and
  `PostingProjector`'s `AddIndexed`, both of which threw or mis-cited before this same commit fixed
  them), and no test in the current suite -- including the 2-solution batch that proved the
  `CatalogProjector`/`PostingProjector` fixes -- happens to shard a *confirmed relation, candidate,
  unresolved or frontier* family large enough to expose it (only `Unresolved` sharded in that
  fixture, and `AppendDisposition`'s prose for it happened to still read correctly by coincidence in
  that specific run). Left open because fixing it correctly means reworking four call sites
  (`AppendRelationsSection`'s `RelationKinds` loop, `AppendDisposition`'s three callers, plus
  whatever `AppendSourceSection`-style check applies) to test family presence by stem/prefix rather
  than exact slot equality, and there is no existing failing test to drive or verify that rewrite --
  attempting it without one risks a silent, unverified regression under time pressure. A follow-up
  task should add a fixture (or a `ScaleInputGenerator`-produced input, matching the pattern
  `output-and-retrieval.md`'s scale scenario already uses) that forces a confirmed-relation,
  candidate, unresolved or frontier family to shard, assert the guide's prose for that family is
  still correct, watch it fail against the current code, then fix the four call sites.
  **Confirmed, with a concrete size symptom, at T63** (2026-09-10): `ScaleInputGenerator`
  (`tests/Csharp2Md.Storage.Tests/Scale/ScaleInputGenerator.cs`) is exactly the fixture this entry
  asked for, and it exposed a second, more severe symptom than the prose-correctness one described
  above -- once `contains`/`belongs-to`/observation families actually split into many shards under
  the real ~32 KiB ceiling, `RetrievalGuideProjector`'s own `retrieval.md` grows past that same
  ceiling, because its "## 2. Select a postings bucket" section (`PostingHints`, driven off
  `view.Slots`) lists one line per *shard key* rather than one line per posting family. T63 did not
  attempt the four-call-site rework this entry already scoped out (still a Projection-layer
  production change, still outside a test-only task's file scope, still without dedicated tests to
  drive it) -- it excludes `retrieval.md` from its own "every file within ceiling" check instead,
  with the reasoning inline at the exclusion. The fixture this entry asked for now exists and
  reproduces the failure on demand; closing the four call sites is still open.
- **`Csharp2Md.Projection.ShardWriter`'s bucketing is a single fixed-depth 256-bucket hash, not
  adaptive like `LayoutPlanner`'s own family splitting** (found in T63, 2026-09-10, while calibrating
  `ScaleInputGenerator`): design.md F9 already named this precisely -- "`ShardWriter` buckets on the
  first byte of `sha256(factId)` -- 256 buckets, no recursion -- and is called only by the catalog and
  posting projectors" -- and framed it as a fact both `LayoutPlanner`'s adaptive depth and this
  limitation drive the layout planner's design, but did not itself say the postings path was ever
  brought up to the same adaptive standard. It was not: `PostingProjector` groups posting entries by
  the *target* (or source) fact id of a relation, one list per id, then hands each family's whole set
  of groups to `ShardWriter.Write` for bucketing -- but the unit of bucketing is the *group key*
  (the fact id), never a group's own entries, so one fact id with a large fan-in or fan-out (a single
  project owning thousands of documents, a single shared component thousands of symbols belong to)
  puts its *entire* posting list in one shard, and that one shard cannot itself be split further
  today. `ScaleInputGenerator`'s first draft made exactly this mistake -- one shared `Component` as
  every synthetic symbol's `belongs-to` target, and one shared `Project` as every synthetic
  document's `contains` source -- and both `postings/incoming.json` and (by the same mechanism)
  `postings/outgoing.json` exceeded the ceiling in a single bucket, independent of and in addition to
  the `contains`/`belongs-to` relation-family sharding `LayoutPlanner` already handles correctly. Not
  fixed here: `ScaleInputGenerator` instead spreads its synthetic Documents and Symbols across 25
  Projects and 25 Components (`ScaleInputGenerator.FanoutGroups`) so no single posting group grows
  large enough to expose it, which is a legitimate calibration choice for proving GCPC-038/GCPC-039's
  *relation-family* sharding (T63's actual scope) but leaves this a real, unaddressed scaling gap for
  any future package where one fact id's fan-in or fan-out is itself large -- for example a real
  project that legitimately owns thousands of documents. Closing it means making
  `Csharp2Md.Projection.ShardWriter`'s own bucket depth adaptive (mirroring
  `LayoutPlanner.PlanFamily`'s prefix-extension loop), a production change with its own test burden,
  out of scope for T63 as a test-only task. A follow-up task should add a fixture with one
  deliberately high-fan-in fact id and prove `ShardWriter` splits its posting group into more than
  one shard, watch it fail against the current code, then generalize the bucketing.
- **`ContractPass` never promotes a same-project handled event to a `Contract` fact** (found in T54,
  2026-09-10): `ContractPass.IsSharedAcrossProjects` (pre-existing code, not touched by any task in
  this feature) requires a message type to cross a project boundary via its producer or its consumer
  before minting a `Contract` fact. T4's fixture (`Certification.Messaging/ContractShapes.cs`)
  deliberately keeps `OrderCreated`'s publisher (`OrderPublisher`) and handler
  (`OrderCreatedEventHandler`) both inside `Certification.Messaging` — matching T4's own "Done when"
  description, "a published event with a handler in the same solution" — so under the current rule it
  never becomes a `Contract` fact at all: a real `analyze` run of the certification corpus publishes
  no `facts/contract.json` whatsoever (confirmed by direct inspection of the published package, not
  inferred from source reading alone). T54 worked around this for its own labeled corpus by using
  `fixtures/SyntheticSolution/Acme.Orders`'s already cross-project `OrderPlaced`
  (`OrderService.PlaceOrderAsync` -> `OrderPlacedWorker`, proven by the pre-existing
  `ContractRelationIntegrationTests`) as the "positive" contract label instead — see T54's deviation
  note in `tasks.md`. T57 (GCPC-091, in this same batch) explicitly requires "the T4 fixture's handled
  event... to reach both its producer and its consumer in one posting hop from the contract identity",
  which is unsatisfiable while this gap stands. This is now T57's problem to resolve when reached
  (likely requiring a scoped `ContractPass.cs` change, or a reassessment of whether the same-project
  restriction is intentional pre-existing behavior this feature should leave alone) — not folded into
  T54 because it is outside T54's own file scope and Done-when.
  **Resolved (as "leave alone")** in T57: `ContractPass.IsSharedAcrossProjects` and the uniform
  `"request"` payload role for every messaging binding are both pre-existing, deliberately tested
  behavior from the completed `entrypoints-boundaries-contracts` workstream --
  `ContractPassTests.Execute_EventTypeDeclaredInSameProjectAsPublisherAndHandler_DoesNotCreateContract`
  (EBC-25) and `Execute_OutboundAndInboundMessagingForSharedEventType_CreatesContractBindingsAndRevision`
  (EBC-21/22/26, asserting `PayloadRole == "request"` for both the outbound and inbound binding)
  respectively. Two candidate fixes were tried and reverted because each broke one of those two tests:
  keying `PostingProjector.AddContractRole` off `BoundaryOperation.Direction` for every binding broke
  RP-25's `ContractDataAccessPostingTests` (whose fixture deliberately names operations opposite their
  payload role, to prove role decides, not direction or name); assigning messaging outbound bindings
  `PayloadRole = "response"` in `ContractPass` broke the EBC-21/22/26 test above. T57's actual fix is
  additive and scoped to `Protocol == "messaging"` only (see T57's commit and the doc comment on
  `AddContractRole`), so neither pre-existing behavior changed. T4's `OrderCreated` genuinely can never
  reach GCPC-091's producer/consumer proof while EBC-25 stands; T57 proved GCPC-091 with a hand-built
  messaging fixture instead (mirroring T54's substitution), and left T4's `OrderCreated` as the
  GCPC-089 "unresolved event" case only, unchanged.
- **`ContractPass`/`RelationPass` publish no discrete candidate or unresolved record for a message
  operation that is simply unhandled** (found in T57, 2026-09-10): GCPC-092 requires an unproven
  contract identity to be "published as candidate or unresolved... and SHALL NOT publish it as a
  contract." `RelationPass.EmitUnresolved`'s messaging branch (`RelationPass.cs:121-156`) already adds
  an `UnresolvedRecord(kind: UsesContract)` for a published message whose type argument is `null` or
  anonymous (an unnameable payload) -- but T4's `OrderShipped` (a normally-named type published with
  simply no handler anywhere) hits none of the existing `AddUnresolved`/`AddCandidate` call sites in
  either `ContractPass.cs` or `RelationPass.cs`: it silently produces no `Contract`, no
  `ContractBinding`, no `UnresolvedRecord` and no `CandidateLink` at all. The only place its absence is
  accounted for today is `ContractAccounting`'s aggregate numeric report (T27/T29, GCPC-088), which
  correctly counts it in the "unresolved" bucket of `contract_coverage` -- but that is a count, not a
  discrete per-item record the way `InvokesPass`'s disposition ledger (GCPC-011..018) or
  `PersistenceEmitter`'s unresolved nodes are. T57 proved (via a hand-built fixture, not a live
  `analyze` of the T4 corpus, since `ContractPass` itself does not reach this state today) that if an
  `UnresolvedRecord(kind: UsesContract)` existed for an unhandled message operation,
  `PostingProjector` would correctly surface it through `postings/unknowns.json` and never fabricate a
  producer or consumer entry for it -- proving the projection side is ready. Closing this for real
  requires an Analysis-layer change (a new branch in `RelationPass.EmitUnresolved`, or in
  `ContractPass` itself, for "published, zero inbound handlers, not already anonymous/null") that is
  out of T57's own file scope (`PostingProjector.cs`). A follow-up task should add this branch and
  prove it against T4's real `OrderShipped` end to end.
  **Investigated and left deferred at T64** (2026-09-10, per the batch prompt's explicit instruction to
  check whether the audit's readiness matrix actually depends on this gap before starting T64): read
  the real audit, `artifacts/verifications/llm-readiness-s-cb7a4be0b1a084f3b59e9c2f1e3906f1.md` (the
  file spec.md's Problem Statement names), in full. Its "Matriz de aptidão" table has exactly ten rows;
  the six T64 must move to PASS are the ones rated PARCIAL or FAIL: Navegação orientada por LLM,
  Legibilidade semântica, Escala/contexto, Cobertura factual, Certificação da execução, Confiabilidade
  de classificação, and the roll-up Prontidão geral. The audit's own contract-coverage finding --
  "Cobertura de contratos não demonstrada" (I2), the finding this exact gap is the last sliver of -- is
  filed under "Achados importantes não bloqueadores isoladamente" (important findings, explicitly *not*
  blocking in isolation), not under any FAIL row. None of the six PASS-required rows' own cited evidence
  mentions contracts: "Cobertura factual" FAILED because the audit's four *mandatory* metrics
  (entry_point, linked_call, contract, persistence) were `0/0` -- a computation-existence problem
  `ValidationAndCoverageStage` (T25-T31) already closed, independent of whether any one contract
  population member has a discrete per-item record; "Certificação da execução" FAILED on
  `not_evaluated` (GCPC-001, also already closed); "Confiabilidade de classificação" FAILED on the
  `EntryPoint` false positive and the unaccounted invocation chain (GCPC-019..025, GCPC-011..018,
  both already closed), not on contracts. GCPC-088's own Independent Test only requires the recognized
  message-operation count to equal the sum of the per-outcome counts -- an aggregate reconciliation
  `ContractAccounting` already satisfies correctly today (T57 confirmed this by direct inspection).
  GCPC-092's own text does ask for a discrete candidate-or-unresolved *record*, which is real and still
  open, but it is P2 scope (the "Contract and message-operation accounting" story), not one of the six
  P1 readiness rows T64's own Done-when names. Conclusion: this gap does not block T64, and T64
  proceeds without closing it. Left deferred, not silently dropped -- the follow-up description above
  stands unchanged.
