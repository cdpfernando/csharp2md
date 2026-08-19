# T43 detector test research

## Scope

Extend the six existing factual detector suites. Production code is unchanged.

## Requirement checklist

- FACT-43: ASP.NET Core framework-confirmed endpoints, authorization, filters, health checks, entrypoints, and partial routes.
- FACT-44: DI lifetime, keyed, `typeof`, generic, factory, and expansion facts.
- FACT-45: HTTP client method, client name, headers, timeout, and partial expressions.
- FACT-46: confirmed gRPC, messaging, and compile-time framework/type evidence only.
- FACT-47: unproven runtime targets remain null with a reason.
- FACT-48: project and package references remain compile-time only.
- FACT-49/58: descriptors and every detector have behavioral positive, negative, and lookalike coverage.

## Verified gaps

- ASP.NET MVC and Minimal API variants: POST/PUT/DELETE/PATCH/HEAD/OPTIONS and MapPut/MapDelete/MapPatch/MapMethods/Map.
- DI: keyed transient and `typeof` implementation registration.
- HTTP: byte-array/stream methods, dynamic header name, and dynamic named client.
- Messaging: synchronous Publish and asynchronous Subscribe.
- Compile-time: direct project-reference provenance/resolution and exact resolution for both reference kinds.

## Conventions

- xUnit 2 on VSTest; detector integration tests use `[Trait("Category", "Integration")]`.
- Existing theory data drives a real Roslyn compilation and asserts emitted facts, evidence, provenance, resolution, nullable targets, and reasons.
- Compile-time reference facts deliberately have no source evidence because their origin is evaluated project data.
