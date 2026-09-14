# 01 — Expose safe pipeline failure details

**What to build:** When an unexpected non-cancellation exception makes a solution unpublished, expose a minimal safe diagnostic through the existing analysis result and CLI error path. The user must see the failing pipeline stage, root exception type, and a useful single-line sanitized message without enabling a debug mode or weakening atomic publication.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] A non-cancellation exception produces an unpublished solution outcome whose existing failing-stage field identifies the stage and whose detail contains the root exception type.
- [ ] The detail includes a useful root message only after sanitization and is always a single line.
- [ ] Sanitization removes suspected secrets, absolute paths, source excerpts, and line breaks; when no safe message remains, the exception type is still reported.
- [ ] Standard error contains the safe detail exactly once, while the existing stdout summary and unpublished exit code remain unchanged.
- [ ] Stack traces, inner-exception chains, exception data, environment values, and raw syntax text are not emitted.
- [ ] Cancellation keeps its existing behavior and is not reported as an unexpected pipeline failure.
- [ ] A failed retry still aborts staging and preserves any previously committed package byte-for-byte.
- [ ] Discriminating pipeline, CLI, security, cancellation, and atomic-publication tests cover the externally visible behavior.
- [ ] No CLI option, logging framework, telemetry sink, remote reporting, schema field, or package artifact is introduced.
