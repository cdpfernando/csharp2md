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

Every factual ID uses the versioned grammar `id1:<type>;key=value;...`. Keys are fixed per type and emitted in their declared order. Values use UTF-8 percent-encoding with uppercase hexadecimal escapes; empty values are invalid unless a type explicitly permits them. The `id1` grammar version is independent of package and factual-schema versions.

- Paths are normalized relative paths with forward slashes, no `.` or `..` segment, drive, UNC prefix, or absolute root. Path and non-path components compare with ordinal, case-sensitive semantics; the system does not case-fold or normalize Unicode.
- Project ID: `id1:project;path=<relative-project-path>`.
- Target ID: `id1:target;project=<project-id>;tfm=<target-framework>`.
- Document ID: `id1:document;project=<project-id>;path=<relative-document-path>`.
- Resolved symbol ID: `id1:symbol;target=<target-id>;doc=<documentation-comment-id>` when `GetDocumentationCommentId()` is available; otherwise `doc` holds the canonical semantic fallback signature.
- A fallback signature is trivia-free and source-location-free. It uses the symbol kind, fully-qualified containing namespace/type chain, metadata names and generic arity, fully-qualified parameter/type arguments, and `ref`, `out`, or `in` modifiers when applicable. It excludes parameter names, whitespace, source text, line ranges, and spans.
- Syntactic symbol ID: `id1:syntactic-symbol;project=<project-id>;document=<relative-document-path>;kind=<declaration-kind>;signature=<normalized-declaration-signature>`. Its signature retains declaration tokens relevant to identity but excludes trivia and source locations.
- Component IDs use their kind and canonically sorted owner/project identities. Relation IDs use owner scope, relation kind, normalized claim fingerprint, and a one-based ordinal only among otherwise identical claims in that scope. The ordinal distinguishes repeated equivalent occurrences without using a source position.
- Diagnostic IDs use stage, scope, code, and a normalized message/data fingerprint. Detector IDs use a lower-ASCII reverse-DNS-style name; detector version belongs in provenance, not in the detector ID.
- `span-start` and line ranges are evidence locations only and never identity components.
- `ArtifactReference` is not a fact identity. It is `facts/<type>/<first-two-lowercase-hex-sha256>/<lowercase-full-sha256>.json`, where SHA-256 receives the UTF-8 bytes of the canonical Fact ID. If two different Fact IDs yield the same reference, validation fails deterministically with an artifact-reference collision; no order-dependent suffix is assigned.

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
| FACT-01 | P1: Safe syntax-only default | T6 defines the syntax-only/untrusted default; T21's zero-option CLI test and T44's real-CLI marker tests confirm it holds end to end | Verified |
| FACT-02 | P1: Safe syntax-only default | T44 real-CLI marker tests prove default and explicit syntax-only never invoke dotnet | Verified |
| FACT-03 | P1: Safe syntax-only default | T13's `InertInventory` inventories broken/unrestored projects inertly; `AnalysisEngineTests.AnalyzeAsync_BrokenUnrestoredProject_UsesCompleteSyntaxFallback` (T20) proves syntactic facts and Markdown are still emitted for a project with a missing SDK | Verified |
| FACT-04 | P1: Safe syntax-only default | T44 proves trust rejection preserves nested output sentinels | Verified |
| FACT-05 | P1: Safe syntax-only default | T44 proves generator opt-in rejection preserves nested output sentinels | Verified |
| FACT-06 | P1: Safe syntax-only default | T44 proves zero, negative, and unparsable timeouts preserve nested output sentinels | Verified |
| FACT-07 | P1: Safe syntax-only default | T6 sets the 10-minute default; T3's timeout probe and T22's per-service evaluator enforce it independently per service | Verified |
| FACT-08 | P1: Factual fragments | T6 defines `AnalysisRequest`/`AnalysisResult` as the analysis module's sole external contract (AD-013); every later task (T13-T46) added internal seams only, never expanding this surface | Verified |
| FACT-09 | P1: Factual fragments | T7-T12 implement `FactResolution`, `FactProvenance`, `Evidence`, structured diagnostics, and every specialized fact family; T13-T40 build the entire pipeline on these types without extension | Verified |
| FACT-10 | P1: Factual fragments | T7 stable identity grammar implemented; extraction/enrichment closes in T15/T26; T42 verifies relocation and preceding-edit stability | Verified |
| FACT-11 | P1: Factual fragments | T11 validated-fragment boundary implemented; T16 persists only validated fragments; T17 projects Markdown only from them; T20's engine wires the exact validate-then-persist-then-project sequence | Verified |
| FACT-12 | P1: Factual fragments | T11 implements duplicate/reference rejection; `AnalysisEngineTests.AnalyzeAsync_StructuralValidationFailure_ReturnsExitOneAndOmitsFragment` (T20) proves the engine exits `1` and omits the fragment | Verified |
| FACT-13 | P1: Factual fragments | T9 state and T11 exact error-symbol rejection implemented; T42 verifies error symbols retain syntactic identity and never become exact | Verified |
| FACT-14 | P1: Factual fragments | T8 constraints implemented; T11's twenty-two discovered cases cover every invalid document/path/range and its valid lookalike | Verified |
| FACT-15 | P1: Factual fragments | T8-T10 provenance contracts implemented; T11's cases cover missing runtime evidence/provenance; T39's compile-time detector confirms the non-runtime exemption | Verified |
| FACT-16 | P1: Factual fragments | T11 compile-time-only validation implemented; T39's `CompileTimeReferenceDetectorTests` hand-constructs a project-reference fact in a runtime partition and confirms `FactValidator` rejects it with `C2M-FV-006` | Verified |
| FACT-17 | P1: Factual fragments | T9 nullable representation and T11 unresolved-reason validation implemented; T34-T39's runtime detectors exercise it on every relation kind | Verified |
| FACT-18 | P1: Deterministic output | T19 skeleton and T40 factual relation/component projection implemented; T45 independently confirms the two roots' complete `raw/` file sets match | Verified |
| FACT-19 | P1: Deterministic output | T12 canonical source-generated JSON implemented; T45 proves byte-identical canonical UTF-8/LF JSON across two independent absolute roots with independently recomputed fragment hashes | Verified |
| FACT-20 | P1: Deterministic output | T45 reconstructs a representative document's source bytes exclusively from its persisted document factual fragment | Verified |
| FACT-21 | P1: Deterministic output | T14 source section partition implemented; T42 verifies exact reconstruction after location movement | Verified |
| FACT-22 | P1: Deterministic output | T18 emits schema-version-two frontmatter; T45's approved snapshot confirms only the specified fields with a resolvable `facts_ref` | Verified |
| FACT-23 | P1: Deterministic output | T19 writes the manifest skeleton; T45 independently validates every manifest fragment reference resolves and every hash matches exact artifact bytes | Verified |
| FACT-24 | P1: Deterministic output | T45 analyzes the same fixture from two unrelated absolute roots and proves byte-identical `raw/` trees except `raw/log.md`, with neither absolute root leaking into any artifact | Verified |
| FACT-25 | P1: Deterministic output | T19 omits dependencies.json; T40 derives dependencies.mmd from validated factual relations; T41 removes the remaining v2 graph writers; T45 confirms `dependencies.json` stays absent and `dependencies.mmd` is built from the validated relation set across both roots | Verified |
| FACT-26 | P2: Semantic enrichment | T2 viability proven; T22's `DotnetMsBuildEvaluator` issues one target-free outer query plus one query per canonical TFM through `ProcessStartInfo.ArgumentList` | Verified |
| FACT-27 | P2: Semantic enrichment | T2 import-path-only preprocessing viability proven; T22 parses import paths from a deleted preprocess file and never persists the expanded XML | Verified |
| FACT-28 | P2: Semantic enrichment | T2 `ArgumentList` and T3 evaluator lifecycle proven; T22's production evaluator uses `ProcessStartInfo.ArgumentList` for every invocation | Verified |
| FACT-29 | P2: Semantic enrichment | T2 inert extension inventory proven; T22 inventories evaluated analyzer/generator paths without loading them | Verified |
| FACT-30 | P2: Semantic enrichment | T44 real-CLI marker extension remains unloaded without opt-in | Verified |
| FACT-31 | P2: Semantic enrichment | T44 real-CLI opt-in executes only the generator, records it, and scopes generator failure | Verified |
| FACT-32 | P2: Semantic enrichment | T25's `ProjectFactEnricher` populates every named field (SDK, imports, output type, TFMs, assembly/root namespace, compile items, references, constants, language version, nullable mode, compiled extensions) from target-scoped evaluation | Verified |
| FACT-33 | P2: Semantic enrichment | T4 post-sanitation binding proven; T26's `SymbolFactEnricher` resolves stable identity, bases, interfaces, implementations, overrides, attributes, and relevant type references | Verified |
| FACT-34 | P2: Semantic enrichment | T44 missing-SDK and failing-generator CLI runs retain syntax artifacts with scoped diagnostics | Verified |
| FACT-35 | P2: Semantic enrichment | T33's ten cases prove per-invocation detector isolation; T34-T39 integrate every concrete detector through that same host | Verified |
| FACT-36 | P2: Semantic enrichment | T44 recoverable missing-SDK and generator failures exit 0 | Verified |
| FACT-37 | P2: Components and relations | T30's seven cases prove indexes build once per solution and forbid repeated solution-wide searches; T40 projects from them | Verified |
| FACT-38 | P2: Components and relations | T31's `ProjectClassifier` applies `service/web-api` before `service/worker` before `tool/cli`; T40 projects the component index | Verified |
| FACT-39 | P2: Components and relations | T31 confirmed worker classification (hosted service, no HTTP endpoints) and T40 component index projection implemented | Verified |
| FACT-40 | P2: Components and relations | T31 confirmed remaining-executable → `tool/cli` classification and T40 component index projection implemented | Verified |
| FACT-41 | P2: Components and relations | T31's `Microsoft.NET.Test.Sdk` rule identifies `test-support`; T40 component index projection implemented | Verified |
| FACT-42 | P2: Components and relations | T32's `LibraryOwnershipClassifier` assigns exactly-one-consumer libraries privately via compile-time reachability; T40 projects component ownership | Verified |
| FACT-43 | P2: Components and relations | T34 confirmed ASP.NET Core detector implemented; T43 verifies every supported endpoint and metadata variant against emitted facts | Verified |
| FACT-44 | P2: Components and relations | T35 dependency-injection relation facts implemented; T43 verifies keyed transient and `typeof` implementation paths | Verified |
| FACT-45 | P2: Components and relations | T36 HTTP relation facts implemented; T43 verifies byte-array/stream and dynamic-header paths | Verified |
| FACT-46 | P2: Components and relations | T37-T39 confirmed gRPC, messaging, and compile-time relation facts implemented; T43 closes their positive/negative/lookalike audit | Verified |
| FACT-47 | P2: Components and relations | T34-T39 retain null targets with explicit reasons for unproven runtime destinations; T43 asserts this for every runtime detector family | Verified |
| FACT-48 | P2: Components and relations | T39 compile-time references implemented; T43 verifies their exact legal partition and provenance contract | Verified |
| FACT-49 | P2: Components and relations | T10 versioned contracts and T33 host enforcement implemented; T43 audit confirms all concrete detector descriptors through emitted provenance | Verified |
| FACT-50 | P2: Diagnostics and coverage | T9 resolution algebra implemented; T27 recomputes it exactly per the normative table on every merge; T28 projects it into persisted coverage | Verified |
| FACT-51 | P2: Diagnostics and coverage | T19 writes `raw/facts/diagnostics.json`; T28's canonical coverage projection supplies scoped, deterministic diagnostics for evaluation, workspace, compilation, document, validation, generator, and detector events | Verified |
| FACT-52 | P2: Diagnostics and coverage | T19 writes `raw/facts/coverage.json`; T28 projects every inventoried project/target/document/detector with applicable fact level, attempt, resolution, and diagnostic references | Verified |
| FACT-53 | P2: Diagnostics and coverage | T13 inventories every project/document inertly, guaranteeing a scope to report even absent a semantic result; T28's twenty-six cases cover every applicability/attempt/resolution outcome, including not-applicable and unattempted | Verified |
| FACT-54 | P2: Diagnostics and coverage | T19/T28's audit log summarizes requested/effective analysis, trust, restore, isolation, extensions, diagnostics, and coverage counts, carrying only the one permitted timestamp and never machine facts | Verified |
| FACT-55 | P3: Migration and release | T41's `MigrationLedgerTests` binds all 428 baseline rows to an executable v3 replacement and proves every named v2 production path is absent from the assembly | Verified |
| FACT-56 | P3: Migration and release | T12 representative factual snapshot implemented; T17/T18 close Markdown/frontmatter snapshots; T45 adds an approved end-to-end Markdown snapshot from a real multi-file fixture | Verified |
| FACT-57 | P3: Migration and release | T42 language matrix verifies explicit syntax and semantic facts for every required language shape | Verified |
| FACT-58 | P3: Migration and release | T34-T39 detector suites plus T43's audit and 20 discriminating cases cover every priority detector family with positive, negative, and lookalike assertions | Verified |
| FACT-59 | P3: Migration and release | T44 proves CLI safety, fallback, extension, timeout, and structural-output boundaries | Verified |
| FACT-60 | P3: Migration and release | T46 sets `Directory.Build.props`'s `<Version>` to `3.0.0`; a real packed/installed tool run confirms the package's nupkg version and the manifest's `tool_version` field | Verified |
| FACT-61 | P3: Migration and release | T1-T46's build/format/test gates are all green (1,158 tests, 0 failed); T47 runs the release quality audits; closes only once the mandatory independent Verifier returns PASS | Implementing |
| FACT-62 | P2: Semantic enrichment | T44 validates the production evaluator process cancellation boundary | Verified |
| FACT-63 | P2: Semantic enrichment | T44 records parent/child PIDs and proves both gone when cancellation returns | Verified |
| FACT-64 | P2: Semantic enrichment | T44 real-CLI marker proves an analyzer is not constructed during generator execution | Verified |
| FACT-65 | P2: Semantic enrichment | T44 asserts semantic fallback manifest requested/effective mode, restore, and isolation fields | Verified |
| FACT-66 | P2: Components and relations | T31 confirmed non-test-library → `library` classification and T40 component index projection implemented | Verified |
| FACT-67 | P2: Components and relations | T32's multi-consumer case confirms `shared-dependency` classification and T40 component index projection implemented | Verified |
| FACT-68 | P2: Components and relations | T32's zero-consumer case confirms a library is retained as its own standalone component and T40 component index projection implemented | Verified |
| FACT-69 | P2: Diagnostics and coverage | T8-T9 diagnostic references shaped; T27's fourteen cases prove diagnostic references are unioned and propagated on every merge outcome; T28 projects them into persisted coverage | Verified |
| FACT-70 | P3: Migration and release | T12 factual schema version 2 implemented; T18 closes frontmatter schema version 2; T46 confirms both from a real packed/installed run and closes the package version | Verified |

**ID format:** `FACT-[NN]`.

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 70 total, 70 mapped to approved tasks in `tasks.md`, 0 unmapped; 69 Verified, 1 (FACT-61) Implementing pending the mandatory Verifier.

---

## Success Criteria

The initial v3 roadmap is complete when all sixteen outcomes below are independently evidenced:

- [x] Markdown is projected exclusively from validated persisted facts. (T17, confirmed end-to-end by T45)
- [x] Syntax-only fallback emits useful artifacts for every eligible source file without executable analysis. (T13, T20, T44)
- [x] Project, target, document, symbol, component, and relation identities are stable across absolute roots. (T7, T42, T45)
- [x] Relations are partitioned, evidence-backed, provenance-backed, and navigable to relative source lines. (T34-T40)
- [x] ASP.NET Core facts cover controllers, Minimal APIs, authorization, filters, health checks, and entrypoints. (T34, T43)
- [x] DI facts cover lifetimes, factories, open generics, multiple implementations, keyed services, and local expansions. (T35, T43)
- [x] HTTP, gRPC, event/messaging, and direct-reference facts require confirmed framework or type evidence. (T36-T39, T43)
- [x] Project and component classifications follow the technical rules in FACT-38 through FACT-42. (T31, T32, T40)
- [x] Source-span coverage reconstructs every input source byte exactly once. (T14, T42, T45)
- [x] Structured coverage distinguishes exact, partial, syntactic, unresolved, not-applicable, and unattempted scopes. (T28)
- [x] Structured diagnostics preserve every scoped degradation without converting fallback into run failure. (T28, T29, T44)
- [x] Factual validation rejects all invalid identity, evidence, resolution, provenance, relation-kind, and unresolved-target cases. (T11)
- [x] Outputs are byte-identical across unchanged inputs and different absolute roots except `raw/log.md`. (T45)
- [x] The manifest records requested/effective analysis, trust, restore status, isolation, extensions, versions, hashes, coverage, and fragment indexes. (T19, T40, independently re-validated by T45)
- [x] The CLI enforces explicit trust and generator consent before touching output. (T21, T44)
- [ ] Package version `3.0.0`, `schema_version: 2`, the migrated 428-test baseline, and new adversarial tests all pass (T46, T41-T45); closes once the independent verifier also returns PASS.
