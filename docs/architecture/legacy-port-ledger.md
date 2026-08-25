# Legacy port ledger

Recorded immediately before engine-bootstrap Phase 8 deleted these trees. `Last commit` is `git log -1 --format=%H` on the former path at that moment. Later workstreams retrieve the listed SHA rather than searching history blind.

| Area | Former path | Responsibility | Last commit | Re-establish in |
| --- | --- | --- | --- | --- |
| Core assembly | `src/Csharp2Md.Core` | Legacy pipeline that mixed orchestration, Roslyn, persistence, validation and Markdown rendering | `6cec37e63d4bd673860041f212c9133128765277` | not re-established |
| Analysis engine and request | `src/Csharp2Md.Core/Analysis` | Bound inventory, Roslyn, markdown output and the old option surface into one engine | `582cc8de2a3c6f5ff45d3b91a249541b6b086cc4` | not re-established |
| MSBuild evaluation and Roslyn compilation | `src/Csharp2Md.Core/Analysis/Semantics` | Out-of-process MSBuild evaluation and Roslyn compilation adapters (`DotnetMsBuildEvaluator`, `SemanticCompilationAdapter`) | `93837df778f8a07db96200e010a7b39062342573` | workstream 4 |
| Fact store and serializers | `src/Csharp2Md.Core/Facts` | In-process fact storage, identity, validation and JSON serialization | `1482fa6a9c97eea63d9c87c89f38141b752f222a` | workstream 3 |
| Wire schemas | `schemas` | JSON schemas for facts and topic frontmatter | `1482fa6a9c97eea63d9c87c89f38141b752f222a` | workstream 3 |
| Markdown projector and retrieval aggregates | `src/Csharp2Md.Core/Projection` | Markdown projection and retrieval-index aggregate writers | `6cec37e63d4bd673860041f212c9133128765277` | workstream 6 |
| Retrieval-index benchmarks | `benchmarks/Csharp2Md.RetrievalIndex.Benchmarks` | BenchmarkDotNet project for the compact retrieval index | `d1263e479511aed7923244360aec0c497ddb8707` | workstream 6 |
| Discovery | `src/Csharp2Md.Core/Discovery` | Solution project-path enumeration, project identity and service catalog | `82a5fe6617f61b66da6756686876f47275c64bdd` | workstream 4 |
| Detection | `src/Csharp2Md.Core/Detection` | Detector host and DI/contract detectors | `f6128cec6ee070ea58e1d7f286ecad07133430b9` | workstream 5D |
| Configuration | `src/Csharp2Md.Core/Configuration` | Config index and service-name resolution | `82a5fe6617f61b66da6756686876f47275c64bdd` | workstream 5D |
| Manifests | `src/Csharp2Md.Core/Manifests` | Manifest load and JSON serialization | `5baabda1a97f9aa25b679c2a4db415c1dea3c2c9` | workstream 3 |
| Output | `src/Csharp2Md.Core/Output` | Output path resolution and file writing | `d452bef4f0214ba453f2e2a3610b5d031fc1327b` | workstream 6 |
| Rendering | `src/Csharp2Md.Core/Rendering` | XML-doc prose rendering | `d452bef4f0214ba453f2e2a3610b5d031fc1327b` | workstream 6 |
| Topic | `src/Csharp2Md.Core/Topic` | Topic scaffolding, frontmatter and run-log writers | `a5cb6ca7bb204990c671f51e0f4a86befbf1348b` | not re-established |
| Core tests | `tests/Csharp2Md.Core.Tests` | 1,093 legacy test attributes bound to deleted contracts | `7d5225998046330aba7cc832663deaf748585203` | not re-established |
| Roslyn sanitation probes | `tests/Csharp2Md.Core.Tests/Analysis/Viability/RoslynSanitationProbeTests.cs` | Proves analyzer and generator assemblies are stripped before compilation | `3336bc0b904a2dc2eaff69c1b85bf74ab92b05f5` | workstream 4 |
| CLI security-boundary tests | `tests/Csharp2Md.Core.Tests/Cli/V3SecurityBoundaryTests.cs` | Proves syntax-only runs do not invoke MSBuild and untrusted semantic analysis is rejected | `c40544ab8f2f76a48eadb1a157b1bfca582905ae` | workstream 8 |
