# Pacote de conhecimento útil e confiável — Re-Verification (iteration 2)

**Date**: 2026-09-17
**Spec**: `.specs/features/pacote-conhecimento-util-e-confiavel/spec.md`
**Diff range**: `6ac4b02..2457702` (T79) on branch `feature/simplif`; gate and corpus measurement run against `2457702`
**Verifier**: independent sub-agent (author ≠ verifier), read-only over the implementation

## Validation Verdict — PASS ✅

**Spec-anchored check**: 71 of 71 ACs + 5 of 5 edge cases match their spec-defined outcome. The one AC that
failed the previous pass (DEP-01, Component and Deployment Unit scope) is **closed, re-measured
independently on `fixtures/eShop` by this Verifier**. 8 non-blocking gaps are ranked below, two of them new.

---

## Scope of this pass, and what it supersedes

This report **supersedes the previous report, which returned a blocking verdict** (70/71 ACs; DEP-01 failed
at Component/DeploymentUnit scope with 138 of 144 possible component pairs, 95.8%, published on
`fixtures/eShop`). It is **iteration 2 of the bounded 3-iteration fix→re-verify loop** in
`references/validate.md`.

It is deliberately **not** a from-scratch re-derivation of all 71 ACs. The previous pass's evidence for the
other 70 ACs and all 5 edge cases stands and is cited rather than re-derived — nothing in T79 touches the
code those rows exercise. What this pass did independently:

1. Re-ran the **full gate** plus both corpus filters, from a clean Release build.
2. Re-ran the **exact DEP-01 measurement** the previous pass used: a real `analyze` of
   `fixtures/eShop/eShop.slnx` into a scratch directory outside the repository, decoded with a decoder
   written outside `Csharp2Md.Core` (Python, independent of the test tree), then deleted.
3. **Sanity pass** over every AC that could plausibly be affected by an entity-identity change
   (PKG-05, PKG-06, DEP-02..DEP-07, MET-04, STO-01/02/04/05/07, VAR-03/04/05, EDG-01) — by reading the
   changed code and confirming the mechanism, not by assuming the green suite proves it.
4. Read the whole T79 diff and judged each new test for non-vacuity against the pre-fix code.

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1–T64, T69–T79 | ✅ Done | T79's 8 Done-when boxes are all checked; each one is corroborated below |
| T65 | ⚠️ Complete with recorded shortfall | Unchanged — 1 box annotated "Not met as written"; this is PUB-08's `Partial` |
| T67 | ⚠️ Complete, boxes stale | Unchanged — 4 unchecked boxes, bookkeeping only (Gap 8) |
| T68 | ⏸️ Parked | Unchanged — user deprioritised file-size work |

T79's own Done-when claims were checked one by one:

| T79 Done-when | Verified how | Result |
| --- | --- | --- |
| `SymbolOwnership.Resolve` resolves by exact `SyntaxTree` identity, never path/name, falling back to the citing project | `src/Csharp2Md.Core/Analysis/Extraction/SymbolOwnership.cs:29-55` — `compilation.SyntaxTrees.Contains(tree)` then each `referenced.Compilation.SyntaxTrees.Contains(tree)`, else `null`; caller at `CausalRelationExtractor.cs:103` applies `?? input.Project` | ✅ |
| Callable/Symbol keys fold in the resolved owner | `IdentityPrimitives.cs:36-40` (new overload) used at `CausalRelationExtractor.cs:78` | ✅ |
| `DataStore`/`DataOperation`/`DataObject` get the same treatment via a shared helper | `ConfigurationPersistenceExtractor.cs:35`, `:60`, `ResolveOwnerAndLocator` at `:41-47`, applied at `:71-79` | ✅ |
| Declaration-site locator when owner ≠ citing project | `SymbolOwnership.cs:62-77`; used at `CausalRelationExtractor.cs:104-106` and `ConfigurationPersistenceExtractor.cs:44-46` | ✅ |
| Cross-component false positives on `fixtures/eShop` fall from 138/144 to 0/144 | **Re-measured by this Verifier** — 0 of 144 (table below) | ✅ |
| A committed `LocalCorpus` test holds it | `tests/Csharp2Md.Cli.Tests/LocalCorpusAnalyzeTests.cs:34,72-75`; observed to run (not skip) in the filtered gate | ✅ (one-sided — Gap 5) |
| Unit regressions pin both collision mechanisms | `CausalRelationExtractorTests.cs:234,266-268`; `ConfigurationPersistenceExtractorTests.cs:33,61-62` | ✅ |
| `ScopePairingTests`' Component evidence is no longer only a self-edge | `ScopePairingTests.cs:71-78` + `TwoComponentGraph()` at `:215-259` | ✅ as a pairing test; ⚠️ it does not discriminate the T79 defect (Gap 4) |

---

## Spec-Anchored Acceptance Criteria

### The AC this pass re-derived from scratch

| Criterion | Spec-defined outcome | Evidence + measurement | Result |
| --- | --- | --- | --- |
| **DEP-01** | *"agregar Confirmed Relations nos escopos Document, Project, Component e Deployment Unit usando somente pertencimento comprovado"* (`spec.md:85`) | **Document ✅** `ScopePairingTests.cs:19-29`. **Project ✅** `Cli.Tests/OracleProjectReferenceScoreTests.cs:86-90`, `:120-142` — 60 correct / 0 false positives / 0 test-policy leaks against the 87-edge oracle; re-run green this pass. **Component ✅ / Deployment Unit ✅** — measured directly on `fixtures/eShop` by this Verifier: **12 of 144 possible component pairs (8.3%), all 12 self-pairs, 0 cross-component pairs**, at both scopes. `Ordering.API`'s outgoing Component-scope set is exactly `{Ordering.API}`; none of ClientApp, HybridApp, WebhookClient, PaymentProcessor, OrderProcessor, WebApp, Catalog.API, Identity.API or Webhooks.API appears. Guarded by `LocalCorpusAnalyzeTests.cs:72-75` and by the unit regressions above. | ✅ **PASS** (was ❌ GAP) |

**The measurement, in full.** Read-only, outside the repository, deleted afterwards. Command:
`Csharp2Md.Cli.exe analyze --solution fixtures/eShop/eShop.slnx --output <scratch>` → exit 0, *"Knowledge
package committed and certified"*. The decoder is my own (base36 handle → sorted entity table → filter
`scope == 2` / `scope == 3` on `measures/dependencies.*.json`), not the test tree's.

| Measure | Before (previous pass) | Now |
| --- | --- | --- |
| Components | 12 | 12 |
| Distinct Component-scope pairs | **138 of 144 (95.8%)** | **12 of 144 (8.3%)** |
| Of those, self-pairs | 34 | 12 |
| Of those, **cross-component** | ~104 | **0** |
| Deployment-Unit pairs | 138 of 144, 34 self | 12 of 144, 12 self, 0 cross |
| `Ordering.API` outgoing Component set | 12 (all components, itself included) | **1 (itself)** |
| Component-scope rows by category | `InternalInvocation` 138, `StructuralTypeUse` 112, `Persistence` 44, `Http` 2, `Grpc` 2, `Messaging` 1, `Contract` 1 | `InternalInvocation` 12, `StructuralTypeUse` 12, `Persistence` 4, `Http` 2, `Grpc` 2, `Messaging` 1, `Contract` 1 (34 rows) |
| Bare `symbol:`/`callable:` entities from outside the source | 0 | 0 (T74 still holds) |

**Decoder validated against a known-good signal first**, as the previous pass did: Project-scope
`project-reference` edges out of `src/Ordering.API` decode to six real projects (EventBus,
EventBusRabbitMQ, IntegrationEventLogEF, Ordering.Domain, Ordering.Infrastructure, eShop.ServiceDefaults).
Re-deriving that set against the csproj itself turned up a separate, pre-existing finding — see Gap 1.

**Why this is a closure, not a coincidence.** The lifting code was *not* changed: `PackageBuilder.Pairs`
still emits `Cross(source.Components, target.Components)` (`PackageBuilder.cs:260-263`). What changed is the
membership that feeds it — `BuildMemberships` derives an entity's components from the projects of its
occurrences (`PackageBuilder.cs:326-334`), and T79 makes those occurrences name the declaring project
instead of the observing one, while owner-folded canonical keys stop physically distinct symbols from
merging into one entity whose membership then spans every host. That is exactly `design.md:218`'s stated
policy (*"Subir origem e destino … somente por mapas de pertencimento comprovado"*), fixed at the membership
map rather than by bounding the product — a legitimate and arguably better answer to the previous pass's
Fix 1 than the lifting-side cap it proposed.

### Non-vacuity of the new tests

Judged against the pre-fix code from the diff (the sensor is a standing project skip, so nothing was
reverted and re-run).

| Test | Would it fail pre-fix? | Why |
| --- | --- | --- |
| `CausalRelationExtractorTests.cs:234` `Extract_TopLevelEntryPointsInDifferentProjects_DoNotCollideIntoOneEntity` | **Yes** | Pre-fix `AddSymbol` keyed on `symbol.ToDisplayString()` alone; Roslyn renders both projects' entry points identically, so the keys were equal and `:268` `Assert.NotEqual(firstEntry.CanonicalKey, secondEntry.CanonicalKey)` fails. `:266` `Assert.Equal(firstEntry.QualifiedName, …)` pins the premise, so the case cannot pass for the wrong reason if Roslyn ever renders them differently. | 
| `ConfigurationPersistenceExtractorTests.cs:33` `…IdenticallyNamedDbContextInDifferentProjects_ProducesDistinctDataStoreEntities` | **Yes** | Pre-fix key was `CreateEntityKey(solution, DataStore, name)` with `name` the receiver's display string, identical in both projects. Same premise-pinning shape (`:61` asserts equal display names, `:62` asserts unequal keys). |
| `CausalRelationExtractorTests.cs:80` `…ToAMethodInAReferencedInSolutionProject_IsStillRetained` (strengthened) | **Yes** | `:115` `Assert.Equal(sharedProject.CanonicalKey, occurrence.Project.CanonicalKey)` — pre-fix `AddSymbol` always attributed the occurrence to `input.Project` (the caller). `:111` narrowed from `Assert.Contains` to `Assert.Single`, so a duplicate entity also fails. |
| `LocalCorpusAnalyzeTests.cs:34` `Analyze_eShop_ComponentScopeHasNoCrossComponentFalsePositive` | **Yes** | Pre-fix the same corpus published ~104 cross-component pairs. It **ran** in this pass's `Category=LocalCorpus` gate (the only 2 skips are the absent eShopOnContainers and Pitstop clones), and my independent decode confirms it is not vacuous *in fact*: 12 Component-scope pairs and 34 rows are published, so the set it filters is non-empty. It is one-sided *by construction* — see Gap 5. |
| `ScopePairingTests.cs:71` `Build_ComponentScopeAggregatesAGenuineCrossComponentRelation` | **No** | `PackageBuilder` is untouched by T79, so this case behaves identically before and after the fix. It is a sound and non-vacuous *pairing* assertion (one positive + two negatives on a genuine two-component fixture) and it does answer the previous pass's Gap 3 complaint that Component-scope evidence was only a self-edge — but it is **not** the regression guard for the T79 defect. See Gap 4. |

### The remaining 70 ACs + 5 edge cases — sanity pass, not re-derivation

The previous report's `file:line` evidence for these rows is unchanged and stands; it is cited there and
not repeated here. This pass checked only whether the identity/attribution change could have moved any of
them, mechanism by mechanism:

| AC | Why it could have been affected | Finding |
| --- | --- | --- |
| PKG-05 | Occurrences of a symbol cited from a test project are now attributed to the *declaring* project, so `NonTestOccurrences` (`PackageBuilder.cs:302-303`) no longer filters them by the citing project | **Unaffected, corpus-proven.** `OracleProjectReferenceScoreTests.cs:156-169` (zero `symbol:`/`callable:` entities from outside the analyzed source) and `:181-193` (zero entities naming a `.Testes` project) ran green across all six solutions this pass. |
| PKG-06 | `SymbolOwnership.DeclarationLocator` builds a *new* document path for a cross-project declaration | **Unaffected.** It uses the project's own directory + the file name (`SymbolOwnership.cs:71-76`), i.e. the identical flattening convention already used by `CausalRelationExtractor.ToLogicalPath` (`:278-288`) and `ArchitectureFactExtractor.ToLogicalDocumentPath` (`:298-317`). Document keys stay in the same space; `MarkdownRendererTests.cs:75-82` (every link resolves to a written artifact) and the real CLI journey run are green. |
| DEP-02 | — | Unaffected; category enum and per-category emission untouched. The eShop decode shows all 7 non-`project-reference` categories still present at Component scope. |
| DEP-03 | `nature` (direct/transitive) | Unaffected by T79, but a **new pre-existing precision finding** — Gap 1. |
| DEP-04 / DEP-05 | Aggregation and cross-scope reuse key off canonical keys | Unaffected; `DependencyAggregatorTests`, `ScopePairingTests.cs:104-128` green. |
| DEP-06 | — | Unchanged: still one `IsConfirmed` boolean where the spec names three kinds (Gap 6, carried over). |
| DEP-07 | Handles resolve through the entity table | Unaffected; key *content* changed, key *space* did not. `CompactDependencyReferenceTests` green. |
| MET-04 | Cross-component edge counts are computed from the Component-scope edges | **Improved, not broken**: `DirectMeasureCalculator.cs:14` counts `Source != Target` at Component scope, which on eShop is now 0 rather than an inflated number. Unit evidence unchanged. |
| STO-01/02 | Public IDs are SHA-256 over the canonical key | Unaffected: the derivation and grammar are unchanged; the new overload only lengthens the *input string* (`IdentityPrimitives.cs:39` prefixes `owner.LogicalRelativePath + ":"`). STO-01's external literal vectors use a `component:` identity, which the overload does not touch. |
| STO-04/05 | One stored entry per repeated identity | Unaffected by construction; more keys are now distinct, none are merged that were not. |
| STO-07 | Byte-identical repeat | Unaffected; the builder tests feed a fixed model. |
| VAR-03 | Compatible occurrences across variants must unify to one logical identity | **Checked by mechanism, not assumed**: the new key folds the *owner project*, which is variant-independent, so two variants of one project still produce one key; and a genuinely shared symbol resolves to the same owner from every caller, so it still unifies (this is exactly what `CausalRelationExtractorTests.cs:111` now asserts with `Assert.Single`). `LogicalEntityAccumulatorTests.cs:10-24` green. |
| VAR-04/05 | Occurrence collisions | Unaffected. A shared symbol observed from two callers yields occurrences with the same key, same owner and the same declaration locator, i.e. the idempotent path (`LogicalEntityAccumulatorTests.cs:63-74`), not the collision path. |
| EDG-01 | A confirmed relation must resolve source, target and evidence | **Checked explicitly**, because this was the one place the change could dangle a relation: `ConfigurationPersistenceExtractor.cs:60` builds the relation's *source* Callable key and had to keep matching `CausalRelationExtractor.AddSymbol`'s key for the same method. Both now qualify by `input.Project` for a locally-declared enclosing method, so they still agree. `RetainedGraphBuilderTests.cs:57-63` green. |
| All others (PUB-*, CRT-*, NAV-*, MET-01..03/05..08, EDG-02..05) | No mechanism links them to symbol ownership or entity keys | Unchanged; previous pass's evidence stands; all green in this pass's full gate. |

**Status**: ✅ 71/71 ACs and 5/5 edge cases match their spec-defined outcome. PUB-08 stays `Partial` and
CRT-04/CRT-05 stay `Unverified` — both by the spec's own label, both re-confirmed accurate (the two clones
are still absent and their cases skip by name).

---

## Ranked Gaps (none blocking)

### Gap 1 — NEW, Major: a transitively-resolved `ProjectReference` is published as a **Direct** dependency

Found while validating the decoder against a known-good signal, and **missed by the previous pass**, which
listed `EventBus` among `Ordering.API`'s "real references" and used that set as its own sanity check.

Measured on `fixtures/eShop`: **35 direct `ProjectReference` edges are published at Project scope; 27 are
declared in the corresponding `.csproj`; 8 are not.** All eight are the same shape — `X → src/EventBus/EventBus.csproj`,
`nature: 0` (Direct) — for `Basket.API`, `Catalog.API`, `OrderProcessor`, `Ordering.API`,
`Ordering.Infrastructure`, `PaymentProcessor`, `WebApp` and `Webhooks.API`. Only
`src/EventBusRabbitMQ/EventBusRabbitMQ.csproj:19` declares `..\EventBus\EventBus.csproj`; the other eight
reach it transitively. Recall is complete in the other direction: **0 declared references are missing**.

The mechanism is Roslyn's `Project.ProjectReferences` on an `MSBuildWorkspace`, which carries the resolved
(transitively flowed) reference set, not the literal `<ProjectReference>` items. This is **pre-existing and
untouched by T79** — Project-scope `ProjectReference` attribution is the one thing the fix explicitly does
not change, and the oracle held at 60/0/0.

**Why it is not an AC failure.** DEP-01 requires proven membership, and both endpoints' membership is
proven; DEP-03 requires the aggregated dependency to *declare* direct-or-transitive nature, and it does.
`spec.md` nowhere defines "direct" as "declared in the project file". So this is a **spec-precision gap**
under `validate.md`'s rule — but it contradicts `design.md:222` (*"Manter resultados transitivos separados
das arestas diretas"*), which is the stronger statement, and it means a consumer asking "what does
Ordering.API directly reference?" gets one wrong answer in six.

**Why no test catches it**: `fixtures/ArchitectureDependencyLab`'s oracle scores set equality against
`project-references.json` and still reads 60/0/0, so its six solutions evidently contain no
transitive-only pair of this shape. `fixtures/eShop` has no oracle.

**Fix task**: decide whether "Direct" means *declared* or *resolved*; if declared, read the root's own
`<ProjectReference>` items (or intersect Roslyn's set with them) and publish the rest as `Transitive`; then
extend the oracle corpus with a 2-hop chain so the distinction is scored. **Priority: Major.**

### Gap 2 — NEW, Major: the same defect shape T79 fixed is still live at a fourth site

T79 owner-qualified three of the four entity-emitting sites. `ArchitectureFactExtractor` was not touched and
still builds keys from bare display strings:

- `ArchitectureFactExtractor.cs:120` — `CreateEntityKey(solution, EntityKind.Symbol, typeSymbol.ToDisplayString())` for **every declared named type**
- `:143` and `:192` — `EntityKind.EntryPoint` from `method.ToDisplayString()` (a `static Main` in two projects with the same namespace/type name collides)
- `:151` — `EntityKind.BoundaryOperation` from `"http:" + method.ToDisplayString()`

Two projects declaring identically-named, identically-namespaced types (exactly the eShop-template scenario
T79 fixed one layer down) would still collide into one entity whose occurrences span both projects, whose
component membership therefore spans both components, and through which Component-scope lifting fans out
again. **Dormant on eShop** — my measurement shows 0 cross-component pairs — but nothing in the tree tests
it, and the corpus that could is the one that cannot discriminate Component scope at all (Gap 7).

A second-order consequence, worth recording because it is a silent semantic change: a type declared in
project A and used in project B now has **two distinct entity keys** — the unqualified one from
`ArchitectureFactExtractor` and the owner-qualified one from `CausalRelationExtractor` — where before T79
the two merged into one entity. No AC pins cross-extractor entity unification and the suite is green, but
the identity scheme is now inconsistent across extractors.

**Fix task**: apply the same owner qualification at `ArchitectureFactExtractor.cs:120,143,151,192` (the owner
there is always `input.Project`, since it walks that project's own trees, so it is a mechanical change), and
add a regression case in the shape of `Extract_TopLevelEntryPointsInDifferentProjects_…`. **Priority: Major.**

### Gap 3 — Major (carried over, unchanged): Component scope now carries **only** self-edges

The corrected projection publishes 12 Component-scope pairs on eShop and all 12 are self-pairs. eShop's real
inter-service edges (WebApp → Catalog.API, the gRPC and messaging hops) are not published as cross-component
dependencies, because a caller's HTTP/gRPC/messaging target entity and the callee's Boundary Operation are
different entities with different keys, so no relation has endpoints in two different components.

This is **not an AC failure** — no AC pins detection recall, and DEP-01 only requires aggregation by proven
membership — but it is what "correct" currently buys at that scope: the previous pass's complete graph
carried no information, and a graph of self-loops carries very little more. This is the same substance as
the previous pass's Gap 4, now with a sharper measurement behind it. **Priority: Major (spec/product, not code correctness).**

### Gap 4 — NEW, Minor: the new `ScopePairingTests` case is not the regression guard it reads as

`ScopePairingTests.cs:71-78` is cited in T79's Done-when as answering the previous pass's Gap 3. It is a
valid pairing test, but `PackageBuilder` is untouched by T79, so it would pass equally against the pre-fix
code. The actual guards for this defect are `LocalCorpusAnalyzeTests.cs:72-75` (corpus, `LocalCorpus`-gated,
therefore absent from CI on a machine without the clone) and the two extraction unit cases. Worth stating so
nobody later deletes the corpus case believing the unit case covers it. The two original self-edge cases at
`:54-62` also remain, still asserting a self-edge as correct behaviour — harmless now that `:71-78` sits
beside them, which is why the previous pass's Gap 3 is downgraded rather than closed. **Priority: Minor.**

### Gap 5 — NEW, Minor: the corpus density test is one-sided

`Analyze_eShop_ComponentScopeHasNoCrossComponentFalsePositive` asserts only that the cross-component set is
empty. An analysis that published **no** Component-scope dependency at all — the opposite failure, and a
plausible one given Gap 3 — would pass it. My decode shows it is not vacuous today (34 rows / 12 pairs are
published), but nothing in the test says so. **Fix**: add a lower bound in the same case, e.g. that
Component-scope rows exist and that each of the 12 components appears as the source of at least its own
self-pair. **Priority: Minor.**

### Gap 6 — Minor (carried over, unchanged): DEP-06 asserts one boolean where the spec names three kinds

`DependencyAggregatorTests.cs:12` — `Assert.Empty(DependencyAggregator.Aggregate([Item(confirmed:false)]))`.
Candidate, Unknown and Open Frontier are never distinguished. Already recorded at `tasks.md:1846`, still
open, untouched by T79. **Priority: Minor.**

### Gap 7 — Major (carried over, unchanged): the oracle corpus still cannot discriminate any scope above Project

`fixtures/ArchitectureDependencyLab`'s six solutions still yield one Component and one DeploymentUnit each,
and `oracle/scenarios.json`'s 63 scenarios still have no test consumer (only `project-references.json` is
scored). The instrument that should have caught Gap 1 of the previous pass, and that would catch Gap 2 above,
does not exist. `fixtures/CertificationCorpus` and `fixtures/PublicationResilience` remain committed with no
test consumer anywhere. **Priority: Major.**

### Gap 8 — Minor (carried over, unchanged): determinism against build state, and traceability hygiene

- STO-07's byte-stability is proven against a fixed in-memory model, never against build state, which
  `STATE.md` records as non-deterministic (SistemaA cold 80 vs warm 115 aggregated dependencies). STO-07's
  wording ("a mesma entrada *avaliada*") arguably scopes it to the builder. Unchanged.
- **80 of 606** `[Fact]`/`[Theory]`/`[LocalCorpusFact]` attribute sites carry no `Requirement` trait
  (measured this pass; the previous pass counted 78 by a slightly different method). CRT-07 and CRT-09 still
  have no trait anywhere. The misplacements listed in the previous report (`PackageBuilderTests.cs:11-13`,
  `PublicationSafetyScannerTests.cs:44`, `PackageValidatorTests.cs:18`) are unchanged.
  **T79's own four new cases all carry `Trait("Requirement", "DEP-01")`** — the regression is not growing.
- `OracleProjectReferenceScoreTests.ProjectReferences_HoldTheRecordedDefectBaseline` still names a "defect
  baseline" for a state that is correct at Project scope, and T67's four Done-when boxes are still stale.
  **Priority: Minor.**

---

## Previously-Ranked Gaps — status after this pass

| Previous gap | Then | Now |
| --- | --- | --- |
| Gap 1 — DEP-01 Component/DU publishes a near-complete graph (Blocker) | 138 of 144 pairs (95.8%), ~104 cross-component | **CLOSED.** 12 of 144 (8.3%), 0 cross-component, independently re-measured on the same corpus. `Ordering.API` claims nothing but itself. |
| Gap 2 — the oracle corpus cannot discriminate above Project (Major) | Open | **Unchanged** → Gap 7 here. The new two-component `ScopePairingTests` fixture adds unit-level discrimination, but the corpus itself is untouched. |
| Gap 3 — `ScopePairingTests` encodes the defect shape as expected (Major) | Open | **Largely addressed, downgraded to Minor** → Gap 4 here. The self-edge cases remain but a genuine cross-component case now sits beside them. |
| Gap 4 — seven of DEP-02's categories have no corpus-level accuracy measurement (Minor) | Open | **Unchanged in kind, sharper in evidence** → Gap 3 here: Component scope now publishes no cross-component edge at all on a 12-service corpus. |
| Gap 5 — STO-07 determinism against build state (Minor) | Open | **Unchanged** → Gap 8 here. |
| Gap 6 — trait and traceability precision (Minor) | Open | **Unchanged** → Gap 8 here; not growing. |

---

## Discrimination Sensor

**Skipped, per `AGENTS.md`.** The automated mutation/fault-injection sensor is a standing project-level skip
(the user runs Stryker manually), recorded in `tasks.md`'s Execution Protocol and in every prior feature.
This was not re-litigated and no mutation was applied to any tree.

In its place this pass did what the previous one did, and what T79's own gate note argues is stronger here:
**measured the shipped output against reality on the real corpus**, plus a per-test pre-fix analysis of every
new assertion (the non-vacuity table above), which is where Gap 4 came from. Gap 1 came from re-deriving a
signal the previous pass had accepted without checking it against the source of truth.

---

## Payload / Conjunction Rule

Spot-checked on the new code only (the rest is unchanged and was spot-checked last pass):

- Both new collision cases **assert the premise as well as the conclusion** — equal display name *and*
  unequal canonical key. A key-inequality-only test would pass trivially if Roslyn ever stopped colliding
  the display strings; these cannot.
- `CausalRelationExtractorTests.cs:111` was tightened from `Assert.Contains` to `Assert.Single`, so a
  duplicate entity now fails where it previously passed.
- `LocalCorpusAnalyzeTests.cs:72-75` asserts an exact count (zero) with the offending pairs interpolated
  into the failure message, matching this project's exact-equality-over-threshold convention.
- Counter-example: `ScopePairingTests.cs:71-78` is strong in form but discriminates a behaviour T79 did not
  change (Gap 4); and `LocalCorpusAnalyzeTests.cs:72-75` has no lower bound (Gap 5).

---

## Code Quality

| Principle | Status | Note |
| --- | --- | --- |
| Minimum code | ✅ | One new 77-line file, one new overload, two extractor call-site changes, one workspace plumbing method. No abstraction beyond what two callers share. |
| Surgical changes | ✅ | The five source files are exactly T79's declared `Where`. No unrelated file touched. |
| No scope creep | ✅ | `ReferencedCompilationsAsync` reuses compilations Roslyn already builds for the root's `CompilationReference`s; no new analysis pass. |
| Matches patterns | ✅ | Generalises T73's own "name the target's own project" pattern; `DeclarationLocator` reuses the codebase's existing path-flattening convention verbatim. |
| Spec-anchored outcome check | ✅ | 71/71; the one previously failing arm is measured, not asserted. |
| Per-layer coverage expectation | ⚠️ | The layer that publishes aggregate graph shape is now covered — but only by a `LocalCorpus`-gated case that cannot run in CI (Gap 5, Gap 7). |
| Every test maps to a requirement | ⚠️ | 80 of 606 attribute sites untraited (Gap 8); T79's own four are traited. |
| Documented guidelines followed | ✅ | `AGENTS.md`: `net10.0`, no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults()` — each enforced by `CoreTopologyTests`; sensor skip honoured and recorded. |
| Comment quality | ✅ | Each changed site carries a comment naming the defect, the AC and the measured evidence — unusually good, and the reason this re-verification could reason about pre-fix behaviour without reverting. |

---

## Edge Cases

- [x] EDG-01 — a relation without resolvable evidence is omitted or the plan rejected. Re-checked explicitly this pass (the cross-extractor Callable key had to stay aligned; it does).
- [x] EDG-02 — an over-budget applicable journey fails naming the journey and the measure.
- [x] EDG-03 — an over-limit package fails before the atomic swap.
- [x] EDG-04 — a single-variant dependency keeps its qualification without duplicating identities (still covered by composition, as recorded last pass).
- [x] EDG-05 — Markdown/machine divergence classifies the package corrupt and prevents commit.

---

## Gate Check

Run by this Verifier in the real working tree at `2457702`, from a clean Release build.

- **Build**: `dotnet build csharp2md.slnx --configuration Release` → exit 0, **0 warnings, 0 errors**.
- **Full**: `dotnet test csharp2md.slnx --configuration Release --no-build` → exit 0.
  - `Csharp2Md.Core.Tests`: **640 passed, 0 failed, 0 skipped** (640 total)
  - `Csharp2Md.Cli.Tests`: **97 passed, 0 failed, 2 skipped** (99 total)
  - **Total: 737 passed, 0 failed, 2 skipped**
- **Oracle filter**: `--filter "Category=OracleCorpus"` → **11 passed, 0 failed, 0 skipped**. Score held at
  60 correct / 0 false positives / 0 test-policy leaks (3+12+0+5+20+20 across the six solutions, matching
  `RecordedBaselines:59-66`). T79 changed nothing at Project scope, as claimed.
- **LocalCorpus filter**: `--filter "Category=LocalCorpus"` → **5 passed, 2 skipped, 0 failed** (was 4 + 2).
  The added pass is `Analyze_eShop_ComponentScopeHasNoCrossComponentFalsePositive`. The two skips are
  `Analyze_eShopOnContainers_CommitsWithinFileAndByteCeilings` and `Analyze_Pitstop_…`, skipped **by name**
  with the searched clone path in the reason, as CRT-07 requires.
- **Test-count delta vs the previous pass**: Core 637 → **640** (+3: the two extraction collision cases and
  the `ScopePairingTests` cross-component case); Cli 96 → **97** (+1: the eShop density case). Nothing was
  deleted; the one modified assertion (`CausalRelationExtractorTests.cs:111-115`) was **strengthened**, not
  weakened.
- **Failures**: none.
- **Working tree**: `git status --porcelain` identical before and after this verification
  (`M AGENTS.md`, `M docs/specs/pacote-conhecimento-util-e-confiavel.md` — both pre-existing and untouched).
  All scratch analysis output was written outside the repository and deleted.

---

## Fix Plans

### Fix 1 — Distinguish declared from resolved project references (Major)

- **Root cause**: Roslyn's `Project.ProjectReferences` carries the transitively flowed reference set; the
  extractor treats every entry as a direct `ProjectReference` relation with `nature: Direct`.
- **Fix task**: define "direct" (declared in the root's own `<ProjectReference>` items) and publish the rest
  as `Transitive`, per `design.md:222`.
- **Verify**: on `fixtures/eShop`, the direct `ProjectReference` set per project equals its csproj's items
  (27 of the current 35); the 8 `→ EventBus` edges become transitive or disappear.
- **Done when**: an oracle-scored corpus solution contains a 2-hop chain and the scorer distinguishes the two
  natures.

### Fix 2 — Owner-qualify `ArchitectureFactExtractor`'s Symbol/EntryPoint/BoundaryOperation keys (Major)

- **Root cause**: three key sites still use a bare display string; the same collision T79 closed elsewhere.
- **Fix task**: use the `CreateEntityKey(solution, kind, owner, name)` overload at
  `ArchitectureFactExtractor.cs:120,143,151,192` with `input.Project`; decide deliberately whether the
  Causal and Architecture views of one type should share an entity, and make them agree either way.
- **Done when**: a unit case proves two projects' identically-named declared types get distinct keys.

### Fix 3 — Give the corpus a multi-host solution and score `scenarios.json` (Major)

- Unchanged from the previous pass's Fix 2. It is the only thing that turns Gaps 2, 3 and 7 from
  "unmeasured" into "scored".

### Fix 4 — Tighten the corpus density case with a lower bound (Minor)

- Add to `LocalCorpusAnalyzeTests.cs:34` an assertion that Component-scope rows exist and that every
  component appears as a source, so an empty projection cannot pass.

### Fix 5 — Traceability hygiene (Minor)

- Unchanged from the previous pass's Fix 4: traits for the CRT-07/CRT-09 suites, the four misplacements,
  the `…HoldTheRecordedDefectBaseline` name, T67's stale boxes.

---

## Requirement Traceability Update

Proposed for `spec.md` — **not applied by this Verifier** (author ≠ verifier).

| Requirement | Current status in spec.md | Proposed status |
| --- | --- | --- |
| DEP-01 | `Complete — measured at every scope … 138 of 144 … T79 … now holds this at 0 of 144` | **`Complete`** — keep, but the note should read as a *current* measurement rather than a history: independently re-measured 2026-09-17 at `2457702` — 12 of 144 Component-scope pairs (8.3%), all self-pairs, **0 cross-component**, identical at Deployment Unit scope; Project scope 60/60 against the oracle. The 138/144 figure belongs in `STATE.md`'s history, not in the status cell. |
| DEP-03 | `Complete` | **`Complete` with a recorded precision note** — the `nature` field is declared as the AC requires, but a transitively-resolved `ProjectReference` is labelled `Direct` (8 of 35 edges on `fixtures/eShop`), which `design.md:222` says should be separated. Fix 1. |
| DEP-06 | `Complete` | **`Partial`** — unchanged recommendation from the previous pass: one `IsConfirmed` boolean, three named kinds never distinguished. |
| PUB-08 | `Partial` | `Partial` — unchanged, re-confirmed accurate. |
| CRT-04, CRT-05 | `Unverified` | `Unverified` — unchanged, clones still absent, cases skip by name. |
| All others (66) | `Complete` | `Complete` — confirmed; the previous pass's `file:line` evidence stands and nothing in T79 disturbs it. |

---

## Notes on fixtures

- `fixtures/eShop` present; its three `LocalCorpus` cases ran and passed. `fixtures/eShopOnContainers` and
  `fixtures/Pitstop` absent (empty directories); their cases skipped by name, as CRT-07 requires.
- `fixtures/CertificationCorpus` and `fixtures/PublicationResilience` remain committed with **no test
  consumer anywhere in the tree** — unchanged, recorded in `AGENTS.md`, folded into Gap 7.

---

## Summary

**Overall**: ✅ Ready, with 8 ranked non-blocking gaps (2 new)

**Spec-anchored check**: 71/71 ACs + 5/5 edge cases matched their spec-defined outcome.
**Sensor**: skipped per `AGENTS.md`; replaced by direct measurement of the shipped output plus a per-test
pre-fix non-vacuity analysis.
**Gate**: 737 passed, 0 failed, 2 justified skips; Release build 0 warnings / 0 errors; Oracle 60/0/0;
LocalCorpus 5 passed / 2 skipped.

**What changed since the previous pass.** T79 closes the blocker, and it closes it at the right layer: not by
capping the Component-scope product, but by making the membership map that feeds it true — which is what
`design.md:218` asked for all along. Re-measured on the same corpus by a decoder written outside the code
under test, `Ordering.API` now claims exactly one Component-scope dependency, itself, where it previously
claimed all twelve. The three root causes are each pinned by a unit case that would fail against the pre-fix
code, and the corpus case that would have caught the defect in the first place now exists.

**What this pass adds.** Two findings the previous pass did not make. One is a pre-existing Project-scope
precision defect it actually walked past: 8 of 35 published direct `ProjectReference` edges on `fixtures/eShop`
are transitive-only, published as `Direct` — the previous report cited that very set as evidence its decoder
was reading correctly. The other is that the defect T79 fixed at three sites is still live at a fourth
(`ArchitectureFactExtractor`'s Symbol/EntryPoint/BoundaryOperation keys), dormant on eShop and untested.
Neither fails an AC as written.

**What remains uncomfortable.** Component scope is now correct and nearly empty: 12 self-pairs and no
inter-component edge at all across a 12-service corpus. Correctness was the right thing to fix first, and no
AC pins recall — but nothing in the committed gates would notice if that projection went to zero, and the
corpus that should arbitrate it still has one component per solution.

**Next steps**: accept the feature; route Fixes 1–3 (Major) as follow-up tasks; Fixes 4–5 are hygiene.
PUB-08 stays `Partial`, CRT-04/CRT-05 stay `Unverified` until the clones return.
