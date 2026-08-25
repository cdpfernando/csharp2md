# Agent Guidance: csharp2md

IMPORTANT: Prefer retrieval-led reasoning over pretraining for any .NET work.
Workflow: skim repo patterns -> consult skills by exact name -> implement smallest-change -> note conflicts.

This project writes C# **against the Roslyn API surface**, which is large and unfamiliar. That is where fabricated APIs are most likely. Consult skills and real docs before writing Roslyn calls; never invent a member signature.

## Two skill packs, clear precedence

- **`dotnet-skills:*`** — community pack. Owns **how C# is authored here**: style, type design, project layout, packaging.
- **`dotnet-test:*` / `dotnet-msbuild:*` / `dotnet-nuget:*`** — official `dotnet/skills`. Owns **test quality, build/MSBuild diagnosis, CPM**.

On overlap: community pack wins for authoring style; official pack wins for test analysis and build diagnosis.

## Routing (invoke by exact name)

**Authoring C#**
`dotnet-skills:csharp-coding-standards`, `:csharp-type-design-performance`, `:csharp-api-design`, `:csharp-nullable-reference-types`, `:csharp-concurrency-patterns`, `:r3-reactive-extensions`

> Known defect: the `csharp-coding-standards` validated value-object snippet does not compile — a validating ctor duplicates the primary ctor (CS0111). Use the explicit-property form.

**Project, packaging, serialization**
`dotnet-skills:project-structure`, `:package-management`, `:local-tools`, `:serialization`, `:ilspy-decompile`
`dotnet-nuget:convert-to-cpm` — central package management (supports `.slnx`)

**Testing**
`dotnet-skills:snapshot-testing` (Verify), `:testcontainers`
`dotnet-test:run-tests`, `dotnet-test:scaffold-dotnet-test-project`
`dotnet-test:filter-syntax` — filter expressions differ between **VSTest and MTP**; xUnit v3 runs on MTP. Verify before relying on `--filter`.
`dotnet-test:assertion-quality`, `:test-anti-patterns`, `:test-smell-detection`, `:test-gap-analysis`, `:coverage-analysis`, `:find-untested-sources`

**Build / MSBuild diagnosis**
`dotnet-msbuild:binlog-failure-analysis` (+ bundled `binlog` MCP server), `:binlog-generation`
`dotnet-msbuild:check-bin-obj-clash`, `:directory-build-organization`, `:property-patterns`, `:item-management`
`dotnet-msbuild:build-perf-diagnostics`, `:eval-performance`

**DI / config / data** (unused so far in this project)
`dotnet-skills:microsoft-extensions-dependency-injection`, `:microsoft-extensions-configuration`, `:efcore-patterns`, `:database-performance`

**Web / Aspire / email** (not applicable to this CLI tool; listed so the mapping is not lost)
`dotnet-skills:aspire-service-defaults`, `:aspire-integration-testing`, `:aspire-configuration`, `:aspire-mailpit-integration`, `:mjml-email-templates`, `:verify-email-snapshots`, `:playwright-blazor`, `:playwright-ci-caching`

## Quality gates

Routing-gated, same as every other section: request/task → applicable route above (Authoring C#, Testing,
Build/MSBuild diagnosis, or an executing `tlc-spec-driven` item) → skill selected → that skill's own
recommendations apply. A row below only fires for work already in play through one of those routes — it is
not a standing trigger on its own, and a skill named only here, with no route match, is not invoked
speculatively.

| When | Skill |
| --- | --- |
| After substantial new / refactored / LLM-authored code | `dotnet-skills:slopwatch` |
| After tests added or changed in complex code | `dotnet-skills:crap-analysis`, `dotnet-test:crap-score` |
| Before declaring a test suite done | `dotnet-test:assertion-quality`, `dotnet-test:test-anti-patterns` |
| Before the spec-driven Verifier runs | `dotnet-test:test-gap-analysis` (complements the discrimination sensor) |

## Specialist agents

`dotnet-skills:dotnet-concurrency-specialist`, `:dotnet-performance-analyst`, `:dotnet-benchmark-designer`, `:akka-net-specialist`, `:docfx-specialist`
`dotnet-skills:roslyn-incremental-generator-specialist` — **source generators only**; not general Roslyn analysis, which is what this project does.

## Project rules

- The target architecture is normative in `CONTEXT.md`, `docs/architecture/`, `.specs/STATE.md`, and `architecture-knowledge-engine-roadmap.md`, in that order. Existing source and schemas are legacy implementation, not a source for future product semantics.
- Spec-driven work lives in `.specs/features/`, but no replacement feature spec exists yet. Create a feature only when its roadmap workstream is explicitly started. Execute it through `.agents/skills/tlc-spec-driven/SKILL.md` and its referenced files/scripts.
- Do not load or recreate superseded specs, tickets, ADR prose, relation taxonomies, CLI contracts or output schemas from Git history unless the user explicitly requests historical analysis. The roadmap's compact disposition table is the only legacy mapping in the working tree.
- The generator owns factual production and directly navigable retrieval projections. Query engines, `kb`, QMD, embeddings, wiki compilation and business-rule interpretation are deferred downstream concerns.
- Target framework is `net10.0`; Roslyn is `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.6.0. Do **not** reference `Microsoft.Build.*` and do **not** call `MSBuildLocator.RegisterDefaults()` — 4.9+ loads projects via an out-of-process BuildHost (AD-003).
- `tlc-spec-driven`'s discrimination sensor (the mutation-testing step inside Execute/Validate) is a standing **skip** for this project, overriding the skill's own "never optional, never prompted" default. The user runs Stryker manually and does not want the sensor's fault-injection pass run by the agent. Every feature done so far (`symbol-index`, `relation-collector`, `data-access-discovery`, `relation-resolver`) has skipped it; do not re-ask or re-decide per feature — note the skip in the new feature's `tasks.md` header (same phrasing as the prior features) and move on. Every other Verifier step (spec-anchored coverage check, gate check, code-quality check) still runs as documented.
