# csharp2md v3: Factual Model and Semantic Analysis Specification

## Problem Statement

`csharp2md` currently requires Roslyn project loading, enriches Markdown directly, and emits a single dependency graph whose entries can overstate what the analysis actually proved. This makes the default run unsuitable for untrusted code, makes partial semantic failures difficult to represent honestly, and couples downstream consumers to presentation rather than durable facts.

Version 3 replaces that contract with a partitioned, validated factual model. Syntax-only analysis becomes the safe default; trusted semantic analysis becomes an explicit opt-in; Markdown, relations, indexes, diagnostics, and coverage are projections of persisted factual fragments while the pipeline remains sequential and memory-bounded.

## Goals

- [ ] Make syntax-only analysis the default and prove that it starts no executable analysis adapter or code from the analyzed solution.
- [ ] Persist deterministic, schema-versioned factual fragments with stable identities, evidence, provenance, resolution quality, diagnostics, and coverage.
- [ ] Project source-faithful Markdown exclusively from document facts while preserving the span-coverage invariant.
- [ ] Add trusted semantic analysis with project-, target-, document-, and fact-level fallback to syntax.
- [ ] Replace heuristic dependency edges with evidence-backed, navigable compile-time and runtime relations.
- [ ] Classify technical components from confirmed project and framework facts without inferring business responsibility.
- [ ] Publish the incompatible output contract as version `3.0.0` with `schema_version: 2`.
- [ ] Preserve the behavioral coverage represented by the 428-test Phase 1 baseline while adding positive, negative, lookalike, degradation, safety, and determinism tests.

## Out of Scope

Explicitly excluded from the initial v3 release.

| Feature | Reason |
| --- | --- |
| Complete global call graph | The initial factual model records selected type and framework relations, not every call edge. |
| Persisted control-flow graphs or generalized data-flow analysis | The initial detectors need selected `IOperation` evidence only. |
| Advanced metrics and architectural snapshot comparison | Coverage in v3 describes extraction and degradation, not longitudinal architecture analytics. |
| Cloud-resource inventory or actual deployment state | Source artifacts cannot prove deployed infrastructure or runtime state. |
| Deterministic inference of business responsibility | Component classification remains technical and evidence-based. |
| Effective configuration values, secrets, or expanded preprocessed MSBuild XML | These outputs would create disclosure risk; preprocessing is used only to identify imports and is discarded. |
| Hard CPU or memory isolation for trusted semantic execution | v3 records `isolation: none`; untrusted code must use syntax-only mode. |
| Persistent cache and hash-based invalidation | Deferred until the factual schemas and relation derivations stabilize. |
| Parallel project analysis | Sequential execution remains the default until an explicit memory budget exists. |
| LLM enrichment | A later optional process may consume facts and must carry separate provenance. |
| Compatibility adapter for v2 `dependencies.json` | The v3 contract is deliberately incompatible and replaces it with partitioned relations. |

---

## Assumptions & Open Questions

Every ambiguity is resolved or recorded here; nothing is left silently unclear.

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Branch base | `feat/csharp2md-v3` starts directly at the verified head of `feat/llmwiki-phase1`; the Phase 1 PR may merge later | The user explicitly authorized the feature branch as the base on 2026-08-17, and Git ancestry will remain clean once Phase 1 merges. | y (user, 2026-08-17) |
| Initial v3 boundary | Increments 0 through 3 in the supplied roadmap are required for v3 completion | The supplied acceptance statement names the factual foundation, essential semantics, priority detectors, classifications, diagnostics, and trust policy as the complete roadmap. | y (user brief) |
| Default analysis mode | `syntax-only` with `Untrusted` trust | It is the only mode appropriate for arbitrary code when hard resource isolation is absent. | y (user brief) |
| Semantic authorization | `semantic` requires the exact trust value `trusted-solution`; omission or any other value is rejected before output preparation | Trust is an informed, explicit boundary rather than an inferred property of a path. | y (user brief) |
| Source generators | They execute only when semantic mode, trusted-solution, and `--include-source-generators` are all present | Generators are executable solution extensions and need a second explicit opt-in. | y (user brief) |
| Analyzers | Analyzer references are inventoried but removed from compilations and never executed in the initial v3 release | Analyzer execution is not required by the requested facts and expands the code-execution surface. | y (user brief) |
| Semantic project evaluation | Use `dotnet msbuild` property/item queries without targets or restore, once per target framework; preprocessing identifies imports only | This preserves explicit process control and avoids executing build targets while still observing evaluated project state. | y (user brief) |
| Memory model | Materialize one project/document factual fragment at a time, persist and render it, then discard it; retain only catalogs and aggregate summaries | This refines AD-001 without building a complete in-memory solution model. | y (user brief) |
| Rendering authority | Persisted document facts are the sole input to Markdown projection | A single factual authority prevents Markdown and JSON from disagreeing. | y (user brief) |
| Source fidelity | Every source byte belongs to exactly one structural Markdown section; facts and frontmatter remain non-source decorators | This preserves AD-002's tested safety invariant while replacing direct semantic enrichment. | y (user brief) |
| Fact resolution | `Exact`, `Partial`, `Syntactic`, `Unresolved`, and `NotApplicable` describe evidence quality; configuration resolution is a separate concept | Reusing the existing `ResolutionKind` would conflate configuration lookup with semantic certainty. | y (user brief) |
| Stable identity | Project-relative paths, target frameworks, documentation-comment IDs or canonical signatures, and normalized syntactic signatures form IDs; span start is location only | IDs must survive absolute-root relocation and unrelated edits before a declaration. | y (user brief) |
| Runtime destinations | Missing targets remain null and carry `unresolved_reason`; logical names are not converted into fictional services | This supersedes AD-005 and reports uncertainty honestly. | y (user brief) |
| Failure policy | Recoverable evaluation, workspace, compilation, document, or detector failures degrade only their scope and exit `0`; structural factual-contract failures exit `1` | A fallback-backed run remains useful, while invalid persisted facts cannot be trusted. | y (user brief) |
| Timeout | `--analysis-timeout` defaults to 10 minutes per service; expiry terminates the full process tree and degrades that service | The supplied brief sets this operational bound and scope. | y (user brief) |
| Output determinism | All generated files are byte-identical across unchanged inputs and different absolute roots except `raw/log.md` | Paths are relative, JSON is canonical, and timestamps are confined to the audit log. | y (user brief) |
| Retry and duplicate handling | No automatic retry; rerunning deterministically replaces an owned output using the existing output-safety contract | Build evaluation failures are not known to be transient, and retries could execute trusted extensions twice. | y (bounded default) |
| Authentication and rate limiting | N/A because csharp2md is a local CLI with no network service or multi-user authorization boundary | The relevant security boundary is local trust consent, covered by explicit mode validation. | y (scope-derived) |
| Data lifecycle | No new archival or expiry behavior; each run owns only its selected output directory and follows the existing marker/`--force` rules | Factual artifacts are regenerable local output, not durable application state. | y (scope-derived) |
| Concurrency and ordering | Services, projects, and document fragments are processed deterministically and sequentially; aggregate lists use canonical ordering | This protects the memory bound and makes output reproducible. | y (user brief) |
| Observability | Persist structured diagnostics, coverage, requested/effective modes, trust, restore status, extensions, hashes, and fragment indexes; retain human audit details in `log.md` | Consumers need to distinguish absence, degradation, and non-applicability without parsing prose warnings. | y (user brief) |

**Open questions:** none — all supplied decisions are captured above or bounded by an explicit default.

---

## Normative Contracts

### Analysis modes and trust

| CLI selection | Effective behavior |
| --- | --- |
| no analysis options | `SyntaxOnly` + `Untrusted` |
| `--analysis syntax-only` | Syntax parsing and inert file/configuration inventory only |
| `--analysis semantic --trust trusted-solution` | Trusted MSBuild evaluation and semantic enrichment with analyzers removed |
| previous row plus `--include-source-generators` | Trusted semantic enrichment with source generators enabled |

`--include-source-generators` is invalid in every other combination. `--analysis-timeout` is a positive duration and applies independently to each service.

### Fact resolution

| Value | Meaning |
| --- | --- |
| `exact` | Every applicable assertion in the fact is semantically confirmed and no participating symbol is an error symbol. |
| `partial` | The fact combines confirmed and degraded, unresolved, or expression-preserving evidence. |
| `syntactic` | The fact is supported only by syntax or inert project-file evidence. |
| `unresolved` | A relevant candidate exists but its identity or target cannot be established. |
| `not-applicable` | No resolution-dependent assertion applies to the fact. |

Document resolution is `exact` when all applicable facts are exact, `syntactic` when all applicable facts are syntactic, `unresolved` when all resolution candidates lack targets, `partial` for mixed qualities, and `not-applicable` when no fact requires resolution.

### Stable identities

- Project ID: normalized forward-slash path of the project file relative to the analysis root.
- Target ID: project ID plus target framework.
- Resolved symbol ID: target ID plus `GetDocumentationCommentId()` when available, otherwise a canonical symbol signature.
- Syntactic symbol ID: project ID plus relative document path, declaration kind, and normalized declaration signature.
- Document ID: project ID plus normalized relative document path.
- `span-start` and line ranges are evidence locations only and never identity components.

### Output layout

```text
raw/
  facts/
    manifest.json
    solutions.json
    projects/<project-id>.json
    documents/<document-id>.json
    symbols/<project-id>.json
    relations/
      compile-time.json
      inheritance.json
      dependency-injection.json
      http.json
      grpc.json
      events.json
    diagnostics.json
    coverage.json
  codebase/...
  dependencies.mmd
  topic.yaml
  CLAUDE.md
  log.md
```

Fact JSON uses UTF-8, LF, canonical property and collection ordering, source-generated serialization, relative paths, and no timestamps. `raw/facts/manifest.json` carries schema/tool/detector versions, content hashes, requested and effective modes, trust, `restore_performed: false`, `isolation: none`, inventoried and loaded extensions, coverage, and a fragment index.

Frontmatter schema version 2 contains only `schema_version`, document identity, `project_id`, `component_ids`, classification, analysis summary, summarized diagnostics, and `facts_ref`. Topic, domain, and generator version live only in `topic.yaml` or the factual manifest.

---

## User Stories

### P1: Safe syntax-only analysis by default ⭐ MVP

**User Story**: As a developer analyzing an arbitrary C# repository, I want the default run to inspect source inertly so that generating documentation does not execute code from the repository.

**Why P1**: This is the safety boundary for every zero-configuration run and the foundation on which all fallback behavior depends.

**Acceptance Criteria**:

1. The system SHALL use `SyntaxOnly` and `Untrusted` when `--analysis` and `--trust` are omitted. <!-- FACT-01 -->
2. WHILE syntax-only mode is effective the system SHALL not start `dotnet msbuild`, `MSBuildWorkspace`, analyzers, source generators, dynamically loaded plugins, or any other executable analysis adapter. <!-- FACT-02 -->
3. WHEN syntax-only mode analyzes an eligible C# file THEN the system SHALL emit its syntactic document and declaration facts even when its project cannot restore or compile. <!-- FACT-03 -->
4. IF semantic mode is requested without `--trust trusted-solution` THEN the system SHALL report the trust requirement and exit `1` before preparing or modifying the output directory. <!-- FACT-04 -->
5. IF `--include-source-generators` is supplied without trusted semantic mode THEN the system SHALL report the invalid option combination and exit `1` before preparing or modifying the output directory. <!-- FACT-05 -->
6. IF `--analysis-timeout` is zero, negative, or unparsable THEN the system SHALL report the invalid value and exit `1` before preparing or modifying the output directory. <!-- FACT-06 -->
7. WHERE `--analysis-timeout` is omitted the system SHALL apply a 10-minute timeout independently to each service. <!-- FACT-07 -->

**Independent Test**: Run the real CLI over a fixture using instrumented executable-analysis seams. The zero-option and explicit syntax-only runs produce facts and Markdown with zero seam invocations; invalid trust, generator, and timeout combinations exit `1` and leave a sentinel output untouched.

---

### P1: Validated factual fragments with stable identity ⭐ MVP

**User Story**: As a tooling consumer, I want a versioned factual representation of the analyzed codebase so that I can distinguish what was observed, inferred, unresolved, or inapplicable without parsing Markdown.

**Why P1**: Facts become the source of truth for every presentation and aggregate artifact in v3.

**Acceptance Criteria**:

8. The analysis module SHALL expose `AnalysisRequest` and `AnalysisResult` as its single external request/response contract while keeping evaluation, Roslyn, persistence, and rendering seams internal. <!-- FACT-08 -->
9. The factual model SHALL represent `FactResolution`, versioned `FactProvenance`, relative-path line-range `Evidence`, structured diagnostics, and specialized solution, project, document, symbol, and relation facts. <!-- FACT-09 -->
10. The system SHALL use the stable identity rules in the Normative Contracts section and SHALL never include an absolute root or `span-start` in an identity. <!-- FACT-10 -->
11. WHEN a document is processed THEN the system SHALL validate and persist its factual fragment before projecting Markdown from that same fragment. <!-- FACT-11 -->
12. IF factual validation finds a duplicate ID or a reference to an absent fact THEN the system SHALL reject the affected factual output and make the run exit `1`. <!-- FACT-12 -->
13. IF an `exact` fact depends on `IErrorTypeSymbol` THEN the system SHALL reject the affected factual output and make the run exit `1`. <!-- FACT-13 -->
14. IF evidence is absolute, outside its document, has an invalid line range, or names a missing relative file THEN the system SHALL reject the affected factual output and make the run exit `1`. <!-- FACT-14 -->
15. IF a runtime relation lacks detector provenance or evidence THEN the system SHALL reject the affected factual output and make the run exit `1`. <!-- FACT-15 -->
16. IF a project or package reference is classified as a runtime relation THEN the system SHALL reject the affected factual output and make the run exit `1`. <!-- FACT-16 -->
17. IF a relation has no target and no non-empty `unresolved_reason` THEN the system SHALL reject the affected factual output and make the run exit `1`. <!-- FACT-17 -->

**Independent Test**: Serialize an analysis fragment containing each supported fact category, validate and round-trip it, then inject each invalid condition above and confirm validation fails with a diagnostic identifying the fact and rule.

---

### P1: Deterministic v3 output projected from facts ⭐ MVP

**User Story**: As a documentation consumer, I want Markdown and machine-readable artifacts to agree and remain reproducible so that navigation, reviews, and downstream indexing are trustworthy.

**Why P1**: A factual model is valuable only if the human-readable output is a faithful projection and the contract is stable across machines.

**Acceptance Criteria**:

18. WHEN a run completes THEN the system SHALL write the factual output tree and relation partitions exactly as defined in the Output layout contract. <!-- FACT-18 -->
19. The system SHALL encode fact JSON as UTF-8 with LF endings, canonical property and collection ordering, source-generated serialization, relative paths, and no timestamps. <!-- FACT-19 -->
20. WHEN a source document is rendered THEN the system SHALL derive its Markdown exclusively from its persisted document factual fragment. <!-- FACT-20 -->
21. The Markdown projector SHALL preserve every source byte exactly once across structural code sections and SHALL keep factual annotations outside source spans. <!-- FACT-21 -->
22. WHEN frontmatter is emitted THEN the system SHALL emit only the schema-version-2 fields defined in the Output layout contract and SHALL resolve `facts_ref` to the document's factual fragment. <!-- FACT-22 -->
23. WHEN the factual manifest is emitted THEN the system SHALL include every metadata, security, coverage, hash, version, and fragment-index field defined in the Output layout contract. <!-- FACT-23 -->
24. WHEN the same fixture is analyzed from two different absolute roots THEN the system SHALL produce byte-identical files in both outputs except for `raw/log.md`. <!-- FACT-24 -->
25. WHEN v3 artifacts are emitted THEN the system SHALL omit v2 `raw/dependencies.json` and SHALL build `raw/dependencies.mmd` from validated relation facts. <!-- FACT-25 -->

**Independent Test**: Copy the fixture to two unrelated absolute roots, run syntax-only analysis, compare the complete trees excluding `log.md`, validate all fragment references and hashes, reconstruct each Markdown document's source bytes, and confirm `dependencies.json` does not exist.

---

### P2: Trusted semantic enrichment with bounded degradation

**User Story**: As a developer who trusts a solution, I want optional semantic enrichment so that facts can carry confirmed symbol identities and framework behavior without making semantic success a prerequisite for documentation.

**Why P2**: Semantic confirmation substantially improves navigation and classification, but syntax-only output remains the universal baseline.

**Acceptance Criteria**:

26. WHERE trusted semantic mode is effective the system SHALL evaluate each project through `dotnet msbuild` property/item queries without restore or target execution and SHALL query each target framework independently. <!-- FACT-26 -->
27. WHERE trusted semantic mode is effective the system SHALL use preprocessing only to identify imported project files and SHALL not persist the expanded XML. <!-- FACT-27 -->
28. WHERE trusted semantic mode is effective the system SHALL invoke external evaluation with `ProcessStartInfo.ArgumentList`. <!-- FACT-28 -->
29. WHERE trusted semantic mode is effective the system SHALL inventory analyzer and generator references without executing them. <!-- FACT-29 -->
30. WHILE `--include-source-generators` is absent the system SHALL not execute source generators. <!-- FACT-30 -->
31. WHERE trusted semantic mode and `--include-source-generators` are both effective the system SHALL execute inventoried source generators and record each loaded extension and its diagnostics. <!-- FACT-31 -->
32. WHEN semantic evaluation succeeds THEN the system SHALL enrich project facts with declared SDK, evaluated imports, output type, target frameworks, assembly name, root namespace, compile items, references, constants, language version, nullable mode, and compiled extensions. <!-- FACT-32 -->
33. WHEN semantic binding succeeds THEN the system SHALL enrich symbol facts with stable symbol identity, bases, interfaces, implementations, overrides, attributes, and relevant type references. <!-- FACT-33 -->
34. IF project evaluation, workspace loading, compilation creation, semantic-model creation, or document binding fails THEN the system SHALL retain syntax facts, add a scoped diagnostic, downgrade only affected resolutions, and continue processing other scopes. <!-- FACT-34 -->
35. IF a detector fails for one scope THEN the system SHALL discard only that detector's incomplete result for that scope, add a diagnostic naming the detector, and continue other detectors and scopes. <!-- FACT-35 -->
36. WHEN recoverable semantic failures are covered by syntax fallback THEN the system SHALL complete with exit code `0`. <!-- FACT-36 -->
37. WHERE trusted semantic mode starts external evaluation the system SHALL enforce the configured timeout independently for the current service. <!-- FACT-62 -->
38. IF external evaluation times out or is cancelled THEN the system SHALL terminate its complete process tree before returning control to the pipeline. <!-- FACT-63 -->
39. WHERE trusted semantic mode obtains a compilation the system SHALL remove analyzer references before the compilation is used for fact extraction. <!-- FACT-64 -->
40. WHEN a run uses semantic fallback THEN the system SHALL record requested mode, effective resolution, `restore_performed: false`, and `isolation: none`. <!-- FACT-65 -->

**Independent Test**: Analyze healthy, missing-SDK/workload, incomplete-restore, invalid-reference, null-compilation, null-semantic-model, throwing-detector, and failing-generator fixtures. Each failure leaves the unaffected facts present, scopes its diagnostic correctly, and exits `0`; a timeout proves the child process tree is terminated.

---

### P2: Evidence-backed component and relation analysis

**User Story**: As an architect reading the generated topic, I want framework-confirmed components and navigable relations so that compile-time structure is not confused with runtime communication.

**Why P2**: These are the highest-value semantic interpretations and replace the ambiguous v2 dependency graph.

**Acceptance Criteria**:

41. The system SHALL construct reusable per-solution symbol and relation indexes once and SHALL not perform repeated solution-wide searches per document or detector. <!-- FACT-37 -->
42. WHEN classifying an executable project with confirmed HTTP endpoints THEN the system SHALL classify its root component as `service/web-api`. <!-- FACT-38 -->
43. WHEN classifying an executable project without confirmed HTTP endpoints but with a confirmed hosted service THEN the system SHALL classify its root component as `service/worker`. <!-- FACT-39 -->
44. WHEN classifying any remaining executable project THEN the system SHALL classify its root component as `tool/cli`. <!-- FACT-40 -->
45. WHEN classifying a test project THEN the system SHALL classify it as `test-support`. <!-- FACT-41 -->
46. WHEN classifying a non-test library project THEN the system SHALL classify it as `library`. <!-- FACT-66 -->
47. WHEN a library is reachable from exactly one executable root THEN the system SHALL assign it privately to that component. <!-- FACT-42 -->
48. WHEN a library is reachable from multiple executable roots THEN the system SHALL classify it as `shared-dependency`. <!-- FACT-67 -->
49. WHEN a library has no executable consumer THEN the system SHALL retain it as its own technical component. <!-- FACT-68 -->
50. WHEN the ASP.NET Core detector finds framework-confirmed controllers, actions, Minimal APIs, health checks, authorization, policies, filters, or entrypoints THEN the system SHALL emit versioned facts with navigable evidence and preserve irreducible route expressions as `partial`. <!-- FACT-43 -->
51. WHEN the dependency-injection detector finds confirmed registrations THEN the system SHALL emit lifetimes, factories, open generics, multiple implementations, keyed services, and navigable local registration expansions without collapsing distinct registrations. <!-- FACT-44 -->
52. WHEN the HTTP detector finds confirmed `IOperation`-backed client usage THEN the system SHALL emit named or typed client identity, method, route expression, base URL, headers, timeout, and logical destination supported by available evidence. <!-- FACT-45 -->
53. WHEN gRPC, messaging/event, or direct-reference detectors emit relations THEN the system SHALL use confirmed framework or type evidence and SHALL not classify relations from an unconfirmed name alone. <!-- FACT-46 -->
54. WHEN a runtime destination cannot be proved THEN the system SHALL leave its target null, set resolution to `unresolved` or `partial` as applicable, and provide `unresolved_reason` without creating a service identity from the logical name. <!-- FACT-47 -->
55. WHEN a project or package reference is emitted THEN the system SHALL place it exclusively in the `compile-time` relation partition. <!-- FACT-48 -->
56. WHEN any detector is registered THEN its descriptor SHALL include a stable detector ID, version, supported fact levels, and a `Detect` operation returning facts and diagnostics through the project- or document-granularity contract. <!-- FACT-49 -->

**Independent Test**: Run all priority detectors over fixtures containing positive, negative, and lookalike constructs. Confirm every positive fact has detector provenance and evidence, every lookalike is absent, project references occur only in compile-time relations, and unresolved logical destinations remain null with reasons.

---

### P2: Honest diagnostics, coverage, and resolution

**User Story**: As an operator, I want the output to explain what was analyzed and what degraded so that absence of facts is never mistaken for absence of behavior.

**Why P2**: Fallback without structured coverage would hide blind spots and make partial results misleading.

**Acceptance Criteria**:

57. WHEN a fragment is persisted THEN the system SHALL compute its resolution according to the Fact resolution contract. <!-- FACT-50 -->
58. WHEN a fragment's resolution is affected by a diagnostic THEN the system SHALL reference that diagnostic from the fragment. <!-- FACT-69 -->
59. WHEN a run completes THEN `raw/facts/diagnostics.json` SHALL contain deterministic, scoped diagnostics for evaluation, workspace, compilation, document, validation, generator, and detector events. <!-- FACT-51 -->
60. WHEN a run completes THEN `raw/facts/coverage.json` SHALL report each inventoried project, target, document, detector, applicable fact level, attempted status, resulting resolution, and diagnostic references. <!-- FACT-52 -->
61. IF a project or document was inventoried but no semantic result was produced THEN the system SHALL represent that scope in coverage and diagnostics rather than omit it. <!-- FACT-53 -->
62. WHEN a run completes THEN the human audit log SHALL summarize requested/effective analysis, trust, restore status, isolation, extensions, coverage, degradation, and structural validation outcome without becoming a source of machine facts. <!-- FACT-54 -->

**Independent Test**: Analyze a fixture in which each semantic layer fails independently. Confirm diagnostics and coverage distinguish not-applicable, not-attempted, syntactic fallback, unresolved candidates, partial enrichment, and exact facts, and confirm every inventoried scope remains visible.

---

### P3: Migration, verification, and incompatible release

**User Story**: As the maintainer, I want the v3 migration guarded by the existing behavior suite and adversarial fixtures so that the output break does not silently weaken source fidelity or detection quality.

**Why P3**: This work closes the release rather than defining one vertical runtime capability, but it is mandatory before publishing 3.0.0.

**Acceptance Criteria**:

63. The v3 test suite SHALL preserve the behavioral outcomes covered by all 428 tests at the Phase 1 baseline, updating obsolete output-shape expectations without deleting equivalent coverage. <!-- FACT-55 -->
64. The test suite SHALL snapshot representative factual fragments, schema-version-2 frontmatter, and Markdown projected from those fragments. <!-- FACT-56 -->
65. The test suite SHALL cover overloads, generics, records, interfaces, overrides, conditional compilation, and error symbols with spec-defined expected facts. <!-- FACT-57 -->
66. Every detector SHALL have positive, negative, and lookalike tests whose asserted outcomes come from this specification rather than implementation structure. <!-- FACT-58 -->
67. The release test suite SHALL prove the syntax-only execution boundary, trust-before-output boundary, generator opt-in boundary, scoped fallback behavior, factual validation rules, and cross-root determinism. <!-- FACT-59 -->
68. WHEN the v3 output contract is complete THEN the system SHALL report package version `3.0.0`. <!-- FACT-60 -->
69. WHEN the v3 output contract is complete THEN the system SHALL report factual and frontmatter `schema_version: 2`. <!-- FACT-70 -->
70. The release SHALL pass the repository build, formatting, complete test, factual-schema synchronization, and independent verifier discrimination gates before the feature is declared complete. <!-- FACT-61 -->

**Independent Test**: Run the full quality gate from a clean checkout, inspect the package metadata and both schemas, execute the security and cross-root tests, and verify that the independent discrimination sensor kills representative faults in safety, identity, resolution, validation, and source fidelity.

---

## Edge Cases

- IF a project declares several target frameworks THEN the system SHALL evaluate and identify each target independently without merging target-specific facts into an `exact` fact. (FACT-26)
- IF `GetDocumentationCommentId()` returns null THEN the system SHALL use a canonical signature and retain stable identity across absolute-root relocation. (FACT-10)
- IF Roslyn returns an error symbol THEN the system SHALL retain syntactic or unresolved evidence and SHALL not label the resulting fact `exact`. (FACT-13)
- IF a route or URL is an expression that cannot be reduced safely THEN the system SHALL preserve the expression and mark the relation `partial` rather than invent a literal. (FACT-43, FACT-45)
- IF several DI registrations exist for one service type THEN the system SHALL preserve every registration and its lifetime, key, implementation or factory evidence. (FACT-44)
- IF an executable project qualifies both as a web API and a worker THEN the system SHALL apply classification priority `service/web-api` before `service/worker`. (FACT-38, FACT-39)
- IF a generator fails after explicit opt-in THEN the system SHALL record the failure, retain pre-generator syntax facts, and continue with degraded semantic facts. (FACT-31, FACT-34)
- IF cancellation or timeout occurs while an external evaluator has descendants THEN the system SHALL terminate the complete process tree before continuing or returning. (FACT-28)
- IF two fragments propose the same stable ID THEN the system SHALL fail structural validation even when their payloads are identical. (FACT-12)
- IF a Dockerfile is inventoried THEN the system SHALL record only evidence of containerization and SHALL not infer active deployment. (Out of Scope)

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| FACT-01 | P1: Safe syntax-only default | T6 analysis contracts; engine/CLI enforcement closes in T20/T21 | Implementing |
| FACT-02 | P1: Safe syntax-only default | Specify | Pending |
| FACT-03 | P1: Safe syntax-only default | Specify | Pending |
| FACT-04 | P1: Safe syntax-only default | T6 typed request validation; engine/CLI enforcement closes in T20/T21 | Implementing |
| FACT-05 | P1: Safe syntax-only default | T6 typed request validation; engine/CLI enforcement closes in T20/T21 | Implementing |
| FACT-06 | P1: Safe syntax-only default | T6 typed request validation; engine/CLI enforcement closes in T20/T21 | Implementing |
| FACT-07 | P1: Safe syntax-only default | T6 analysis default; engine enforcement closes in T20 | Implementing |
| FACT-08 | P1: Factual fragments | T6 external analysis request/result contract | Implementing |
| FACT-09 | P1: Factual fragments | T7-T12 factual values, families, validation, and JSON contracts implemented | Implementing |
| FACT-10 | P1: Factual fragments | T7 stable identity grammar implemented; extraction/enrichment closes in T15/T26 | Implementing |
| FACT-11 | P1: Factual fragments | T11 validated-fragment boundary implemented; persistence/projection closes in T16/T17 | Implementing |
| FACT-12 | P1: Factual fragments | T11 duplicate/reference rejection implemented; engine exit behavior closes in T20 | Implementing |
| FACT-13 | P1: Factual fragments | T9 state and T11 exact error-symbol rejection implemented | Implementing |
| FACT-14 | P1: Factual fragments | T8 constraints and T11 document/path/range validation implemented | Implementing |
| FACT-15 | P1: Factual fragments | T8-T10 provenance contracts and T11 runtime enforcement implemented | Implementing |
| FACT-16 | P1: Factual fragments | T11 compile-time-only project/package reference validation implemented | Implementing |
| FACT-17 | P1: Factual fragments | T9 nullable representation and T11 unresolved-reason validation implemented | Implementing |
| FACT-18 | P1: Deterministic output | Specify | Pending |
| FACT-19 | P1: Deterministic output | T12 canonical source-generated JSON implemented; persistence/output closes in T16/T19 | Implementing |
| FACT-20 | P1: Deterministic output | Specify | Pending |
| FACT-21 | P1: Deterministic output | Specify | Pending |
| FACT-22 | P1: Deterministic output | Specify | Pending |
| FACT-23 | P1: Deterministic output | Specify | Pending |
| FACT-24 | P1: Deterministic output | Specify | Pending |
| FACT-25 | P1: Deterministic output | Specify | Pending |
| FACT-26 | P2: Semantic enrichment | T2 viability and T5 evaluated-compilation backend selected; production closes in T22 | Implementing |
| FACT-27 | P2: Semantic enrichment | T2 import-path-only preprocessing viability proven; production closes in T22 | Implementing |
| FACT-28 | P2: Semantic enrichment | T2 `ArgumentList` and T3 evaluator lifecycle proven; production closes in T22 | Implementing |
| FACT-29 | P2: Semantic enrichment | T2 inert extension inventory proven; production closes in T22 | Implementing |
| FACT-30 | P2: Semantic enrichment | T4/T5 disabled generator and sanitized compilation boundaries proven; production closes in T24 | Implementing |
| FACT-31 | P2: Semantic enrichment | T5 trusted generator-only execution and diagnostics proven; production closes in T24 | Implementing |
| FACT-32 | P2: Semantic enrichment | Specify | Pending |
| FACT-33 | P2: Semantic enrichment | T4 post-sanitation binding and target identity proven; production closes in T23/T26 | Implementing |
| FACT-34 | P2: Semantic enrichment | T4 scoped compilation fallback viability proven; production closes in T22-T29 | Implementing |
| FACT-35 | P2: Semantic enrichment | Specify | Pending |
| FACT-36 | P2: Semantic enrichment | Specify | Pending |
| FACT-37 | P2: Components and relations | T30 reusable target-aware indexes implemented; detector and aggregate integration closes in T33/T40 | Implementing |
| FACT-38 | P2: Components and relations | T31 confirmed web API classification implemented; component projection closes in T40 | Implementing |
| FACT-39 | P2: Components and relations | T31 confirmed worker classification implemented; component projection closes in T40 | Implementing |
| FACT-40 | P2: Components and relations | T31 confirmed CLI classification implemented; component projection closes in T40 | Implementing |
| FACT-41 | P2: Components and relations | T31 confirmed test-support classification implemented; component projection closes in T40 | Implementing |
| FACT-42 | P2: Components and relations | T32 private library ownership implemented; component projection closes in T40 | Implementing |
| FACT-43 | P2: Components and relations | Specify | Pending |
| FACT-44 | P2: Components and relations | Specify | Pending |
| FACT-45 | P2: Components and relations | Specify | Pending |
| FACT-46 | P2: Components and relations | Specify | Pending |
| FACT-47 | P2: Components and relations | Specify | Pending |
| FACT-48 | P2: Components and relations | Specify | Pending |
| FACT-49 | P2: Components and relations | T10 versioned detector contracts implemented; host enforcement closes in T33 | Implementing |
| FACT-50 | P2: Diagnostics and coverage | T9 resolution algebra implemented; merge/projection closes in T27/T28 | Implementing |
| FACT-51 | P2: Diagnostics and coverage | Specify | Pending |
| FACT-52 | P2: Diagnostics and coverage | Specify | Pending |
| FACT-53 | P2: Diagnostics and coverage | Specify | Pending |
| FACT-54 | P2: Diagnostics and coverage | Specify | Pending |
| FACT-55 | P3: Migration and release | T1 migration ledger established; T41/T47 close executable migration | Implementing |
| FACT-56 | P3: Migration and release | T12 representative factual snapshot implemented; Markdown/frontmatter snapshots close in T17/T18 | Implementing |
| FACT-57 | P3: Migration and release | Specify | Pending |
| FACT-58 | P3: Migration and release | Specify | Pending |
| FACT-59 | P3: Migration and release | Specify | Pending |
| FACT-60 | P3: Migration and release | Specify | Pending |
| FACT-61 | P3: Migration and release | Specify | Pending |
| FACT-62 | P2: Semantic enrichment | T3 per-service timeout viability proven; production closes in T22/T29 | Implementing |
| FACT-63 | P2: Semantic enrichment | T3 complete tree termination proven; production closes in T22/T29 | Implementing |
| FACT-64 | P2: Semantic enrichment | T4 sanitation and T5 analyzer-free generator driver proven; production closes in T23/T24 | Implementing |
| FACT-65 | P2: Semantic enrichment | Specify | Pending |
| FACT-66 | P2: Components and relations | T31 confirmed library classification implemented; component projection closes in T40 | Implementing |
| FACT-67 | P2: Components and relations | T32 shared-dependency ownership implemented; component projection closes in T40 | Implementing |
| FACT-68 | P2: Components and relations | T32 standalone library ownership implemented; component projection closes in T40 | Implementing |
| FACT-69 | P2: Diagnostics and coverage | T8-T9 diagnostic references shaped; merge/projection closes in T27/T28 | Implementing |
| FACT-70 | P3: Migration and release | T12 factual schema version 2 implemented; frontmatter/package closure in T18/T46 | Implementing |

**ID format:** `FACT-[NN]`.

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 70 total, 70 mapped to approved tasks in `tasks.md`, 0 unmapped; implementation has not started.

---

## Success Criteria

The initial v3 roadmap is complete when all sixteen outcomes below are independently evidenced:

- [ ] Markdown is projected exclusively from validated persisted facts.
- [ ] Syntax-only fallback emits useful artifacts for every eligible source file without executable analysis.
- [ ] Project, target, document, symbol, component, and relation identities are stable across absolute roots.
- [ ] Relations are partitioned, evidence-backed, provenance-backed, and navigable to relative source lines.
- [ ] ASP.NET Core facts cover controllers, Minimal APIs, authorization, filters, health checks, and entrypoints.
- [ ] DI facts cover lifetimes, factories, open generics, multiple implementations, keyed services, and local expansions.
- [ ] HTTP, gRPC, event/messaging, and direct-reference facts require confirmed framework or type evidence.
- [ ] Project and component classifications follow the technical rules in FACT-38 through FACT-42.
- [ ] Source-span coverage reconstructs every input source byte exactly once.
- [ ] Structured coverage distinguishes exact, partial, syntactic, unresolved, not-applicable, and unattempted scopes.
- [ ] Structured diagnostics preserve every scoped degradation without converting fallback into run failure.
- [ ] Factual validation rejects all invalid identity, evidence, resolution, provenance, relation-kind, and unresolved-target cases.
- [ ] Outputs are byte-identical across unchanged inputs and different absolute roots except `raw/log.md`.
- [ ] The manifest records requested/effective analysis, trust, restore status, isolation, extensions, versions, hashes, coverage, and fragment indexes.
- [ ] The CLI enforces explicit trust and generator consent before touching output.
- [ ] Package version `3.0.0`, `schema_version: 2`, the migrated 428-test baseline, new adversarial tests, and the independent verifier all pass.
