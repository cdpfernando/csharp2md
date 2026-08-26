# Roslyn Observation Extraction Context

**Gathered:** 2026-08-25
**Spec:** `.specs/features/roslyn-observation-extraction/spec.md`
**Status:** Ready for design

---

## Feature Boundary

Workstream 4 of the architecture-knowledge-engine roadmap. It replaces the Inventory, Semantic Analysis, and Observation Extraction stubs so `analyze --solution … --output …` publishes structural facts and an immutable C# observation ledger through the storage port workstream 3 already shipped.

It does not classify architecture, contracts, persistence, or configuration facts. It does not emit Markdown, catalogs, postings, source-projection files, or a batch manifest. It does not restore `--trust` or `--include-source-generators`. Non-C# adapters (appsettings, Docker, Kubernetes, protobuf, OpenAPI, SQL files) are inventory-only.

---

## Implementation Decisions

### Observation ledger completeness

- Extractors emit all ten registry observation kinds from C#.
- Always-when-bindable kinds (`Invocation`, `ObjectCreation`, `TypeUsage`, `BaseType`, `AttributeUsage`) emit for every bindable occurrence.
- Registered-context kinds (`Assignment`, `Configuration`, `RouteDeclaration`, `MessageOperation`, `DataAccess`) emit only in the compiled contexts this workstream declares.
- Non-C# files become `Document` facts. This workstream does not extract specialized observations from them.
- Classification and Promotion, Retrieval Projection, and Batch Composition stay stubs.

### Trust and generators

- Semantic MSBuild/Roslyn is the default `analyze` path. Pointing `--solution` at a tree is the trust grant.
- Source generators stay stripped. Analyzers never run.
- `--trust`, `--include-source-generators`, and `--analysis-timeout` stay absent (STOR-52).
- This workstream does not ship a syntax-only mode.

### Authorized root

- Discussed default was the directory of each `.sln`/`.slnx`.
- The versioned fixture lists sibling projects (`../Acme.Shared.Contracts`, `../Acme.Broken`) from `Acme.Orders.slnx`. A directory-only root would reject declared projects.
- Locked rule: the authorized root is the smallest directory that contains the solution file and every project path the solution lists that exists on disk.
- Symlinks and files whose resolved path escapes that root are rejected. A missing listed project is a diagnostic, not a root expansion.
- Batch-wide common ancestor of all `--solution` paths is still rejected. No `--root` option.

### Analysis variants

- Every `TargetFramework` declared by each project is an analysis variant.
- One configuration per run: the solution’s requested configuration, `Debug` when none is stated.
- No Debug×Release matrix. No primary-TFM-only cut.
- The `environment` axis of `AnalysisVariantId` is the logical name `local`. Define symbols come from the project’s `DefineConstants` for that variant.

### Agent's Discretion

- How extractors are hosted inside Analysis (one visitor vs several), physical type names, and how `PipelineContext` accumulates the snapshot.
- How MSBuildWorkspace is constructed, provided AD-003 holds (Workspaces.MSBuild 5.6.0, no `Microsoft.Build.*`, no `MSBuildLocator.RegisterDefaults()`).
- How a Document source digest is stored when the document has zero observations, provided every observation from a document carries that document’s SHA-256 digest.
- How multi-TFM extraction unions observation identities (Domain observations have no variant axis). The package must not emit two observations with one identity.

### Declined / Undiscussed Gray Areas → Assumptions

- Structural `contains` is emitted only when `derived_from` can cite a real observation. `Solution contains Project` is not emitted; Project and Document identities already nest under the solution.
- Symbol facets in this workstream are `Callable` when the symbol is a method, constructor, local function, or identifiable lambda. `Controller`, `Handler`, `Repository`, `Client`, and `Service` wait for later classifiers.
- Source fidelity is relative path, one-based spans, and SHA-256 document digests. Byte-faithful source files stay workstream 6 (STOR-46).
- Tests and generated documents are inventoried and observed. Excluding them from runtime graphs is workstream 6.
- Workspace identity is the logical name `default` until a later workstream adds an explicit workspace option.

---

## Specific References

Guided discuss, 2026-08-25. User accepted all four recommended forks, then asked to create context.

---

## Deferred Ideas

- Non-C# compiled adapters: workstreams 5A, 5C, 5D.
- Generator opt-in and syntax-only security-boundary CLI tests: workstream 8.
- Source locators, Markdown, catalogs, postings: workstream 6.
- Classifiers and promotion: workstreams 5A–5D.
- Batch manifest and cross-solution composition: workstream 7.
