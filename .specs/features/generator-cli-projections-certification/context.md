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
