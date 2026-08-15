# csharp2md + LLMWiki Phase 1: KB Structuring Specification

## Problem Statement

`csharp2md` generates a flat tree of Markdown documents with 100% source fidelity, but that output cannot be ingested as an LLMWiki topic: there is no frontmatter, no `topic.yaml`, no topic conventions document, and no auditable generation log. The dependency graph exists in `dependencies.json` but sits outside LLMWiki's workflows. Phase 1 changes csharp2md's output layout so every run produces a well-formed LLMWiki topic, establishing the scaffold that Phase 2 (service-level graph resolution) fills in and Phase 3 (compiled wiki synthesis) consumes.

## Goals

- [ ] Every run writes an LLMWiki topic rooted at `raw/`, with source documents under `raw/codebase/` mirroring the input tree
- [ ] Every generated `.md` carries a schema-valid YAML frontmatter block with identity, analysis-context, and Phase 2 stub fields
- [ ] `raw/topic.yaml` and `raw/CLAUDE.md` describe the topic, its provenance, and its conventions
- [ ] `raw/log.md` records the run: timestamp, reconstructed invocation, statistics, and Phase 2/3 placeholder sections
- [ ] `--topic` and `--domain` let the tool produce a correct topic for any codebase, not just one

## Out of Scope

Explicitly excluded from Phase 1. Documented to prevent scope creep.

| Feature | Reason |
| --- | --- |
| Service-level graph resolution (`source_service`, `targetService` binding) | Phase 2; depends on a ServiceRegistry and target-matching heuristics not yet designed. Phase 1 emits the stub fields only. |
| Compiled wiki synthesis (Service-Map.md, HTTP-Dependencies.md, Event-Flow.md) | Phase 3; depends on Phase 2's resolved graph and on `kb compile`, absent from kb v0.0.10 |
| Invoking `kb lint` / `kb search` / `kb ingest` from csharp2md | Phase 1 generates schema-compliant output deterministically and validates it against its own JSON schema. kb tooling is a separate, manual verification step. |
| Backward compatibility with the v1 flat output layout | Explicitly waived (user decision, 2026-08-15). The `raw/` layout replaces the v1 layout unconditionally; existing tests that assert root-level output paths are updated as part of this feature. |
| Performance optimization or token-counting study | Deferred to post-Phase-3; current output volume is acceptable |
| Externalizing `file_type` / `tags` heuristics to a YAML rules file | Deferred until the rules are calibrated on more than one real codebase. Phase 1 implements them in C#. |
| Dynamic plugin architecture for LLMWiki integration | Future; Phase 1 is static scaffold generation, consistent with AD-004 |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here — nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Topic layout | `raw/` holds everything Phase 1 generates; `wiki/` is reserved for Phase 3 compiled articles and is not created in Phase 1 | LLMWiki convention: `raw/` is ingested source, `wiki/` is post-compilation output. Creating an empty `wiki/` would signal a phase that has not happened. | y (user, RDD) |
| Layout activation | Unconditional — every run writes the `raw/` layout; there is no opt-in flag | User decision (2026-08-15): the v1 flat layout is superseded, not preserved alongside. One layout means one set of tests and no branch in the writer. | y (user, 2026-08-15) |
| Frontmatter injection point | Injected inline while each document is written, as a decorator over the rendered Markdown — not as a second pass that re-reads written files | AD-001's pipeline renders and writes each document then discards it; a rewrite pass over every file would contradict that. AD-002 requires semantic additions to be additive decorators, which this is. | y (user, 2026-08-15) |
| Frontmatter schema | The block defined in the Frontmatter Schema section below. It is the single normative definition; no acceptance criterion restates the field list. | The prior draft carried three divergent field lists across ACs, schema, and success criteria. One definition, referenced everywhere, removes that class of drift. | y |
| `created_at` / `csharp2md_version` fields | Dropped from frontmatter | Decided in `2026-08-15-csharp2md-frontmatter-schema.md` (Q1). A per-file timestamp would also make output non-deterministic across runs. | y (per schema doc, Q1) |
| `topic` and `domain` values | Set via `--topic` and `--domain` CLI options. Defaults: `topic` = slug of the input directory name; `domain` = `system-design`. | Hardcoding `arquitetura-software/eshop` would make the tool correct for exactly one codebase. Defaults keep zero-config runs working, consistent with the `cli-directory-input` feature. Supersedes the prior Out-of-Scope row that froze the CLI surface. | y (user, 2026-08-15) |
| Frontmatter on index documents | The per-service and root `index.md` files receive the same frontmatter block with `source_kind: codebase-index` and `file_type: index` | A `.md` file without frontmatter inside an ingested topic is exactly what `kb lint` flags. One block shape means one validator. | y |
| Validation mechanism | Frontmatter is validated by round-tripping the emitted block through YamlDotNet's deserializer and checking that required fields are non-empty, over a closed `Frontmatter` record whose `file_type` is an enum. `schemas/frontmatter.schema.json` is published as the external contract and kept in sync by a test, but is never executed at runtime. | No JSON Schema validator exists in the dependency set, and adding one to check a shape the type system already enforces buys little for real supply-chain surface. A round-trip additionally catches the YAML escaping failures WIKI-07 targets, which a schema check would pass straight through. | y (user, 2026-08-15) |
| Schema-validation failure policy | Log the file path and the specific error, continue generating remaining files, and exit `1` at the end of the run | Log-and-continue was decided in the schema doc (Q3A); the non-zero exit keeps CI honest instead of burying failures in a log. Distinct from project-load degradation, which continues to exit `0` with warnings per the v1 spec. | y (user, 2026-08-15) |
| `source_service` / `analysis_status` | Every document gets `source_service: null` and `analysis_status: pending` in Phase 1 | Phase 2 resolves them by rewriting frontmatter; documents are regenerable, so a full rewrite is acceptable (schema doc, Q4/Q5). | y (per schema doc, Q5) |
| `dependencies.json` placement | Copied unchanged to `raw/dependencies.json` | Raw data; Phase 2 enriches it in place | y (RDD, T3) |
| Ownership marker placement | `.csharp2md-output` stays at the **output root**, not inside `raw/` | `OutputWriter.PrepareRun` reads the marker at the output root before deleting anything; moving it would break the `--force` safety contract shipped in `cli-directory-input`. | y |
| Determinism | Two runs over unchanged input produce byte-identical output **except** the timestamp line in `raw/log.md` | Frontmatter carries no timestamp (see `created_at` row), so the log is the only nondeterministic artifact. Stating the exception makes the criterion testable. | y |
| Validation corpus | Every acceptance criterion and success criterion is validated against `fixtures/SyntheticSolution` alone. No external codebase — eShopOnContainers included — is a reference point for this feature. | User decision (2026-08-15). eShopOnContainers is not in this repository, so any threshold measured against it is unverifiable here and unverifiable in CI. A criterion that cannot be run is not a criterion. | y (user, 2026-08-15) |
| Fixture coverage gap | Closed. The fixture originally exercised only `index`, `interface`, `service`, and `class` of the eleven `file_type` values and two of six tag rules; six documents were added (2026-08-15) bringing it to 11/11 and 6/6, per the Fixture Expectations table. | Grounding validation in the fixture (row above) would otherwise be a vacuous gate: seven `file_type` rules and four tag rules would ship with no test exercising them. WIKI-23 keeps the property enforced as the rules evolve. | y |
| Constraint on extending the fixture | New fixture files added for heuristic coverage introduce **no new dependency signals** — no `IHttpClientFactory.CreateClient`, no `ClientBase` subclass, no `PublishAsync`/`Subscribe` call, no new project reference. | `fixtures/SyntheticSolution` is load-bearing for the v1 dependency-graph tests. A new file that emits an edge changes `dependencies.json` and the Mermaid diagram under tests that assert against them. Type shape is free; outbound calls are not. | y |
| Fixture manifest | `fixtures/SyntheticSolution/manifest.json` is untracked and points at an absolute local eShopOnContainers path. It is not used by any test — `FixtureManifest` writes manifests at runtime — and is deleted as part of this feature. | Leaving a checked-out file that names an external codebase contradicts the fixture-only validation decision and is a trap for the next reader. | y |
| kb tooling | `kb lint` / `kb search` are optional external verification, never invoked by csharp2md and never a gate | Keeps Phase 1 independent of kb's install state; schema compliance is checked against the project's own JSON schema instead | y (user clarification) |

**Roadmap decision references:**
- **D1** — Structure before graph: a ready-made kb-compatible scaffold prevents rework when Phase 2 data arrives
- **D2** — No `kb compile` in Phase 1: kb v0.0.10 does not implement it; that is Phase 3
- **D3** — Calibrate during real ingestion rather than designing ahead for hypothetical gaps
- **D4** — Phase 3 is optional: raw documents are already useful (search, lint, navigation)

**Open questions:** None — all resolved or logged above.

---

## Frontmatter Schema

This section is the normative definition of the frontmatter block. Acceptance criteria reference it rather than restating fields.

```yaml
---
# IDENTITY & LOCATION
title: "OrderService"
source_kind: codebase-file        # codebase-file | codebase-index
source_path: "Acme.Orders/OrderService.cs"
domain: system-design             # from --domain
topic: acme-shop                  # from --topic

# ANALYSIS CONTEXT
language: csharp
file_type: service                # see enum below
tags: [persistence, async-patterns]

# TRACEABILITY
created_by: csharp2md

# PHASE 2 STUBS
source_service: null
analysis_status: pending
---
```

**Field rules:**

| Field | Rule |
| --- | --- |
| `title` | The first of these that resolves: (1) the top-level type whose name equals the file name without extension; (2) the first top-level type whose containing namespace starts with the project's root namespace; (3) the first top-level type in source order; (4) the file name without extension, with a warning. Tier 2 exists because this fixture — and real codebases — declare framework stand-ins alongside the type the file is about: `Acme.Orders/PaymentsGrpcClient.cs` declares `Grpc.Core.ClientBase` before `Acme.Orders.PaymentsClient` and its file name matches neither, so tiers 1 and 3 would both title it after the stand-in. Tier 3 still governs `Acme.Shared.Contracts/Events.cs`, where both records share the project namespace and the first in source order (`OrderPlaced`) wins. |
| `source_kind` | `codebase-file` for source-derived documents; `codebase-index` for generated `index.md` files. |
| `source_path` | The document's path beneath `raw/codebase/` with the trailing `.md` removed, forward-slash separated — that is, `<service-name>/<service-relative-path>`. Service-relative rather than input-root-relative because that is what the v1 writer already mirrors; the two coincide only when a service's folder is named after the service, which a manifest `name` override breaks. |
| `domain` | Verbatim value of `--domain` (default `system-design`). |
| `topic` | Verbatim value of `--topic` (default: slug of the input directory name). |
| `language` | Always `csharp`. |
| `file_type` | One of `configuration`, `controller`, `handler`, `service`, `data-access`, `enum`, `interface`, `filter`, `extension`, `class`, `index`. Derived per the heuristics below. Records carry no dedicated value; they resolve through the same rules and fall through to `class` unless another rule matches. |
| `tags` | Array of strings, possibly empty, derived per the heuristics below. Emitted in a stable sorted order. |
| `created_by` | Always `csharp2md`. |
| `source_service` | Always `null` in Phase 1. |
| `analysis_status` | Always `pending` in Phase 1. |

`schemas/frontmatter.schema.json` is the published form of this table — the contract external consumers (LLMWiki, Phase 2) read. It is **not** what WIKI-12 executes: the runtime check is a YAML round-trip plus required-field validation over a closed record, and a test keeps the schema file in sync with that record. See the Assumptions row on validation mechanism.

### `file_type` Derivation

Rules are evaluated top to bottom; the first match wins.

Rules apply to the document's `title` type (the type selected by the `title` rule above).

| Pattern | Result |
| --- | --- |
| Generated index document | `index` |
| File name is `Startup.cs` or `Program.cs` | `configuration` |
| Declaration is an enum | `enum` |
| Declaration is an interface | `interface` |
| Type inherits a base type whose name ends in `Controller`, or the type's own name ends in `Controller` | `controller` |
| Type implements `IIntegrationEventHandler` (any arity), or inherits or implements a type whose name ends in `EventHandler` | `handler` |
| Type inherits `DbContext`, implements a type matching `IRepository*`, inherits `RepositoryBase`, or the type's own name ends in `Repository` | `data-access` |
| Type implements a type matching `IService*`, inherits `ServiceBase`, or the type's own name ends in `Service` | `service` |
| Type implements `IOperationFilter` | `filter` |
| Type is `static` and declares at least one extension method | `extension` |
| No rule matched | `class` |

Declaration-kind rules (`enum`, `interface`) precede name and base-type rules because the kind is unambiguous. Every other rule accepts a name-based signal alongside the type-based one: the fixture's `OrderService` implements no interface and `PaymentsService` inherits `Payments.PaymentsBase`, so a purely base-type rule would classify both as `class` — the name is the only evidence available, and pretending otherwise would leave `service` unprovable.

Ambiguity is resolved by rule order, not by error. When two or more rules match, the run emits a warning naming the file and the rules that matched, and uses the first.

`appsettings*.json` gets no rule: the pipeline renders only `*.cs` documents, so a `.json` file never becomes a Markdown document and never receives frontmatter. It is read by the dependency detectors and is otherwise out of this feature's reach.

### `tags` Derivation

Applied additively; a document may carry several tags or none.

| Pattern found in the document's source | Tag |
| --- | --- |
| `WebHost.Create*`, `IWebHostBuilder`, `WebApplication.CreateBuilder` | `bootstrapping` |
| `AddScoped`, `AddSingleton`, `AddTransient`, `AddHostedService` | `dependency-injection` |
| An identifier containing `EventBus`, `IIntegrationEventHandler`, or a member named `Publish`/`PublishAsync`/`Subscribe`/`SubscribeAsync` | `event-driven` |
| `DbContext`, `DbSet`, `IQueryable`, a type name ending in `Repository` | `persistence` |
| `async` modifier, `await` expression, or a `Task`/`Task<T>` return type | `async-patterns` |
| Type inherits a base type whose name ends in `Controller`, or the type's own name ends in `Controller` | `api-endpoint` |

The `event-driven` rule matches bare `Subscribe` as well as `SubscribeAsync` because `IEventBus` in the fixture declares `Subscribe<TEvent>`, not `SubscribeAsync`; an `Async`-only pattern would miss the one real subscription in the repository.

### Fixture Expectations

Every rule above is exercised by a document in `fixtures/SyntheticSolution`. This table is the specification of the committed expectations file WIKI-23 requires; the test suite asserts derived values against it.

| Document | `title` (tier) | `file_type` | `tags` |
| --- | --- | --- | --- |
| `Acme.Orders/Program.cs` | `Program` (1) | `configuration` | `bootstrapping`, `dependency-injection` |
| `Acme.Orders/Hosting/ServiceCollectionExtensions.cs` | `ServiceCollectionExtensions` (1) | `extension` | `dependency-injection` |
| `Acme.Orders/Api/OrdersController.cs` | `OrdersController` (1) | `controller` | `api-endpoint` |
| `Acme.Orders/Api/SwaggerOperationDefaultsFilter.cs` | `SwaggerOperationDefaultsFilter` (1) | `filter` | — |
| `Acme.Orders/Data/OrderDbContext.cs` | `OrderDbContext` (1) | `data-access` | `persistence` |
| `Acme.Orders/Events/OrderPlacedEventHandler.cs` | `OrderPlacedEventHandler` (1) | `handler` | `event-driven`, `async-patterns` |
| `Acme.Orders/OrderService.cs` | `OrderService` (1) | `service` | `event-driven`, `async-patterns` |
| `Acme.Orders/PaymentsGrpcClient.cs` | `PaymentsClient` (2) | `class` | `async-patterns` |
| `Acme.Orders/Properties/AssemblyInfo.cs` | `AssemblyInfo` (4, with warning) | `class` | — |
| `Acme.Payments/PaymentsService.cs` | `PaymentsService` (1) | `service` | `event-driven`, `async-patterns` |
| `Acme.Shared.Contracts/Events.cs` | `OrderPlaced` (3) | `class` | — |
| `Acme.Shared.Contracts/IEventBus.cs` | `IEventBus` (1) | `interface` | `event-driven`, `async-patterns` |
| `Acme.Shared.Contracts/OrderStatus.cs` | `OrderStatus` (1) | `enum` | — |
| each `index.md` | the indexed service or root | `index` | — |

Coverage: 11 of 11 `file_type` values, 6 of 6 tag rules, and all four `title` tiers. Tier 4 needs a document that declares no type at all, which `Acme.Orders/Properties/AssemblyInfo.cs` supplies — `Acme.Broken/Broken.cs` cannot, because a project that fails to compile contributes no documents.

The framework stand-ins these documents rely on (`WebApplication`, `IServiceCollection`, `ControllerBase`, `DbContext`/`DbSet`, `IIntegrationEventHandler`, `IOperationFilter`) are declared inside the fixture rather than referenced as packages, following the precedent `PaymentsGrpcClient.cs` set for `Grpc.Core.ClientBase`: the fixture must restore and build without external dependencies. None of them introduces a dependency signal — verified by running the pipeline and confirming all 11 graph edges still cite only `OrderService.cs`, `PaymentsService.cs`, and the two `.csproj` files.

---

## User Stories

### P1: Repackage csharp2md output as an LLMWiki-compatible raw topic ⭐ MVP

**User Story**: As a developer documenting a C#/.NET codebase, I want `csharp2md`'s output to be a standard LLMWiki topic with metadata, so I can ingest, search, and lint it with LLMWiki's tooling without restructuring anything by hand.

**Why P1**: This is the minimal vertical slice. The layout and schema are stable and codebase-independent, and they do not depend on service-graph resolution. Once produced, the topic is immediately useful and gives Phase 2 a known-good scaffold to fill in.

**Acceptance Criteria**:

1. WHEN a run completes successfully THEN the system SHALL write every generated artifact beneath a `raw/` directory within the output root. <!-- event-driven -->
2. WHEN documents are written THEN each source-derived document SHALL be written to `raw/codebase/<service-name>/<service-relative-path>.md`, preserving the mirroring the v1 pipeline already performs — the existing output tree moved beneath `raw/codebase/`, not re-rooted. <!-- event-driven -->
3. WHEN aggregate artifacts are written THEN `raw/dependencies.json` and `raw/dependencies.mmd` SHALL be written at the root of `raw/`, and each `index.md` SHALL be written at the root of the tree it indexes within `raw/codebase/`. <!-- event-driven -->
4. The system SHALL write the `.csharp2md-output` ownership marker at the output root, outside `raw/`. <!-- ubiquitous -->
5. The system SHALL prefix every generated `.md` file with a YAML frontmatter block, delimited by a leading and trailing `---` line, placed before the document's `# ` heading. <!-- ubiquitous -->
6. The system SHALL populate every frontmatter block per the Frontmatter Schema section, with every required field present and no field left empty. <!-- ubiquitous -->
7. WHEN a frontmatter block is emitted THEN it SHALL parse without error under a standard YAML 1.2 parser, with string values quoted and escaped so that no source-derived value (a type name containing `:`, `"`, or `#`) breaks the block. <!-- event-driven -->
8. The system SHALL leave the rendered Markdown body byte-identical to what it would emit without frontmatter. <!-- ubiquitous -->
9. WHEN a run completes THEN every source document that the pipeline rendered SHALL be present under `raw/codebase/`, with none dropped. <!-- event-driven -->
10. WHEN a run completes THEN `raw/topic.yaml` SHALL exist, containing `slug` (the `--topic` value), `title`, and a `description` stating the topic was generated by csharp2md. <!-- event-driven -->
11. WHEN a run completes THEN `raw/CLAUDE.md` SHALL exist, documenting the ingestion source (csharp2md and its version), the frontmatter schema, the directory conventions, and a note that Phase 2 resolves `source_service` and `analysis_status`. <!-- event-driven -->
12. IF a generated frontmatter block fails validation — it does not round-trip through a YAML parser, or a required field is present but empty — THEN the system SHALL report the file path and the specific validation error on stderr, continue generating the remaining documents, and exit with code `1`. <!-- unwanted-behavior -->
13. The system SHALL derive `title`, `file_type`, and `tags` from the syntax tree alone, so that a document whose project loaded degraded — no semantic model, unresolved base types — derives exactly what the same document derives in a healthy project. <!-- ubiquitous -->
14. IF `--topic` receives a value that does not match `^[a-z0-9]+(-[a-z0-9]+)*(/[a-z0-9]+(-[a-z0-9]+)*)*$` THEN the system SHALL reject it on stderr and exit with code `1` before any output is written. <!-- unwanted-behavior -->
15. WHERE `--topic` is omitted the system SHALL derive it by lowercasing the input directory's name, replacing each run of non-alphanumeric characters with a single `-`, and trimming leading and trailing `-`. <!-- optional-feature -->
16. WHERE `--domain` is omitted the system SHALL use `system-design`. <!-- optional-feature -->
17. WHEN a run completes THEN the system SHALL print a summary reporting the number of documents written, the number of frontmatter validation failures, and the output topic path. <!-- event-driven -->
23. The system's `file_type` and `tags` heuristics SHALL each be exercised by at least one file in `fixtures/SyntheticSolution`, with the expected `file_type` and `tags` for every fixture document recorded in a committed expectations file that the test suite asserts against. <!-- ubiquitous -->

**Independent Test**:
1. Run `csharp2md fixtures/SyntheticSolution --output ./test-phase1 --topic acme-shop`.
2. Confirm `test-phase1/.csharp2md-output`, `test-phase1/raw/topic.yaml`, `raw/CLAUDE.md`, `raw/dependencies.json`, `raw/dependencies.mmd`, and `raw/log.md` all exist, and that `raw/codebase/` mirrors the fixture's folder structure.
3. Parse the frontmatter of every generated `.md` with YamlDotNet; confirm all round-trip and that no required field is empty.
4. Confirm `topic: acme-shop` and `domain: system-design` on every document.
5. Confirm the derived `file_type` for every fixture document equals the committed expectations file, and that the union of derived values covers all eleven enum members.
6. Confirm every `title` in the Fixture Expectations table, including the two fallback tiers: `PaymentsGrpcClient.cs` titles `PaymentsClient` (tier 2, not the `Grpc.Core.ClientBase` stand-in declared above it) and `Events.cs` titles `OrderPlaced` (tier 3).
7. Confirm the derived `tags` for every fixture document equal the expectations table, and that their union covers all six tag rules.
8. Strip the frontmatter from each output document and diff against the pre-Phase-1 rendered body; confirm they are byte-identical.
9. Confirm `Acme.Broken` — which does not compile — still produces a schema-valid frontmatter block with `file_type: class` and a warning naming the file.
10. Re-run with no arguments changed; confirm every file is byte-identical except the timestamp line in `raw/log.md`.

---

### P2: Auditable generation log

**User Story**: As an operator running `csharp2md` in CI or repeating runs locally, I want a log documenting what was generated, when, and by which invocation, so I can audit changes and see what Phase 2 still has to resolve.

**Why P2**: Auditability is required for the topic to be trusted as a regenerated artifact, and the placeholder sections give Phase 2 a defined place to record resolution decisions rather than restructuring the log later. It is not P1 only because the topic is ingestible without it.

**Acceptance Criteria**:

18. WHEN a run completes THEN the system SHALL write `raw/log.md` containing an ISO 8601 UTC timestamp and the invocation reconstructed from the resolved arguments. <!-- event-driven -->
19. WHEN `raw/log.md` is written THEN it SHALL report the count of documents generated, the count of dependency edges in `dependencies.json`, the count of services analyzed, the count of frontmatter validation failures, and the absolute output topic path. <!-- event-driven -->
20. WHEN `raw/log.md` is written THEN it SHALL contain empty `## Graph Resolution` and `## Calibration Notes` sections reserved for Phase 2. <!-- event-driven -->
21. WHEN a run produced one or more frontmatter validation failures THEN `raw/log.md` SHALL list each failing file path with its validation error. <!-- event-driven -->
22. The system SHALL write `raw/log.md` on every completed run, including runs that exit `1` because of validation failures, so a failed generation remains auditable. <!-- ubiquitous -->

**Independent Test**:
1. Run Phase 1 generation against the fixture; open `raw/log.md`.
2. Confirm it carries an ISO 8601 UTC timestamp, the reconstructed invocation, all five statistics, and both empty placeholder sections.
3. Re-run and confirm the timestamp and statistics reflect the new run and no content from the prior run survives.
4. Introduce a fixture file that forces a validation failure; confirm the run exits `1`, `raw/log.md` still exists, and the failing path and error appear in it.

---

## Edge Cases

- IF a frontmatter block fails schema validation THEN the run reports the file and error, continues, and exits `1`. (WIKI-12)
- IF a document declares no top-level type THEN `title` falls back to the file name without extension and a warning is emitted. (Frontmatter Schema, `title` rule)
- IF a document declares several top-level types, possibly across several namespaces, THEN `title` is the type whose name matches the file name — `PaymentsClient` for `PaymentsGrpcClient.cs`, not the `Grpc.Core.ClientBase` stand-in declared above it — and `file_type` is derived from that same type. (Frontmatter Schema, `title` and `file_type` rules)
- IF a document declares only records, as `Acme.Shared.Contracts/Events.cs` does, THEN `file_type` is `class`; there is no `record` value. (`file_type` Derivation)
- IF two or more `file_type` rules match THEN the first rule in table order wins and a warning names the file and the competing rules. (`file_type` Derivation)
- IF the output directory is non-empty and carries no `.csharp2md-output` marker THEN the run requires `--force`, unchanged from the `cli-directory-input` contract. (WIKI-04)
- IF a project fails to restore but still loads THEN its documents receive the same frontmatter they would receive healthy, because derivation is syntax-only; the run's exit code stays governed by the existing degradation contract (`0` with warnings), not by this feature. `Acme.Payments` is this case: `PaymentsService : Payments.PaymentsBase` classifies as `service` with no restore, since the rule reads the declared name. (WIKI-13)
- IF a project cannot compile at all THEN it contributes no documents and therefore no frontmatter — the v1 pipeline already drops them at [AnalysisPipeline.cs:207](src/Csharp2Md.Core/Pipeline/AnalysisPipeline.cs#L207). `Acme.Broken` is this case. (WIKI-13)
- IF two services in the manifest share a name THEN both are processed and the collision is recorded in `raw/log.md`; Phase 2 resolves the ambiguity. (inherited from the v1 spec's P1-18)
- IF a type name or XML doc summary contains YAML metacharacters (`:`, `"`, `#`, leading `-`) THEN the value is quoted and escaped so the block still parses. (WIKI-07)

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| WIKI-01 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-02 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-03 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-04 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-05 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-06 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-07 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-08 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-09 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-10 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-11 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-12 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-13 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-14 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-15 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-16 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-17 | P1: Repackage as LLMWiki topic | Design | Pending |
| WIKI-18 | P2: Auditable generation log | Design | Pending |
| WIKI-19 | P2: Auditable generation log | Design | Pending |
| WIKI-20 | P2: Auditable generation log | Design | Pending |
| WIKI-21 | P2: Auditable generation log | Design | Pending |
| WIKI-22 | P2: Auditable generation log | Design | Pending |
| WIKI-23 | P1: Repackage as LLMWiki topic | Design | Pending |

**ID format:** `WIKI-[NN]`. The `P1-NN` space is already owned by `.specs/features/csharp2md/spec.md` and cited in shipped source comments, so this feature uses a distinct prefix — as `cli-directory-input` did with `CLI-NN`.

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 23 total, 0 mapped to tasks yet, 23 pending.

---

## Success Criteria

All criteria are measured against `fixtures/SyntheticSolution`. No external codebase is a reference point.

- [ ] Every generated `.md` in `raw/` round-trips through a YAML parser with all required fields non-empty, and `schemas/frontmatter.schema.json` still matches the `Frontmatter` record
- [ ] The output layout matches the contract in WIKI-01 through WIKI-04, and the `.csharp2md-output` marker still governs `--force` behavior
- [ ] `file_type` matches the committed expectations file for 100% of fixture documents, and the fixture exercises all eleven enum members
- [ ] `tags` are derived with no per-file configuration, and the fixture exercises all six tag rules
- [ ] `title` matches the Fixture Expectations table on every document, including the tier-2 and tier-3 fallbacks
- [ ] The pipeline's dependency graph over the fixture is unchanged by the heuristic-coverage documents: 11 edges, none citing a file other than `OrderService.cs`, `PaymentsService.cs`, or a `.csproj`
- [ ] `source_service` is `null` and `analysis_status` is `pending` on every document
- [ ] `raw/log.md` carries the timestamp, reconstructed invocation, all five statistics, and both Phase 2 placeholder sections
- [ ] Two consecutive runs over unchanged input are byte-identical except the timestamp line in `raw/log.md`
- [ ] A run with a forced frontmatter validation failure exits `1`, still writes `raw/log.md`, and names the failing file
- [ ] A run against a project that fails to load still produces schema-valid frontmatter for its documents
