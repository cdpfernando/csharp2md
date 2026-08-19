# T43 detector test plan

1. Extend existing ASP.NET, DI, HTTP, messaging, and compile-time theory/test data with the verified gaps.
2. Add `DetectorMatrixTests` as an executable audit that binds every detector rule family to positive, negative, and lookalike behavioral evidence.
3. Run focused detector tests, review assertions against FACT-43 through FACT-49 and FACT-58, run the full gate, then record the final review in `.testagent/status.md`.

# T44 security-boundary test plan

1. Add marker-backed CLI tests for syntax-only and invalid-output boundaries.
2. Cover fallback, generator opt-in/failure, analyzer exclusion, and process-tree cancellation at production interfaces.
3. Run focused and full gates, then record the assertion review.
