---
name: webex-coverage-gate-async-exclusion
description: RESOLVED — coverlet.runsettings used to exclude CompilerGeneratedAttribute (dropping async method-body coverage), making async-heavy files (whole Webex module) read far below 95%; the exclusion was removed and the exposed real gaps were tested to green.
metadata:
  type: project
---

**RESOLVED (2026-07-08, P13 coverage-config slice).** `CompilerGeneratedAttribute` was REMOVED from
`coverlet.runsettings` `<ExcludeByAttribute>` (kept `GeneratedCodeAttribute` for real source-gen/EF
scaffolding). The gate now honestly measures async bodies. Removing it exposed 19 genuinely-under-covered
files (Webex module + a few auth/infra/query handlers) that the exclusion had hidden; all were covered with
real tests to reach the ≥95%/file gate (global 99.64%, 0 files under). Notable fixes: `MigrationRunner`
retry loop extracted to a `RunAsync(contexts, logger, migrate, delay)` seam + `AddWebexIfPresent` seam so
it's unit-testable without a real SQL Server / 5s waits (only the one relational-only `await RunAsync` boot
line stays uncovered → 96.7%); record decl lines need a `with { }` (copy-ctor), not just `new`+property
reads; coverlet buffers response content at `SendAsync` (ResponseContentRead) so a throw-on-read HttpContent
can't reach a downstream `SafeBody` catch. Historical context below (why it read low) is kept for reference.

---

`node scripts/check-coverage.mjs .` (the ≥95%/file backend gate, ADR-0016) reports the **Webex module**
files far below threshold — `WebexApiClient` ~40%, `WebexEndpoints` ~59%, `HangfireWebexJobScheduler` 33%,
`WebexTokenService` 85%, `WebexSignature` 93% — even though those files have passing dedicated tests.

**This is a measurement artifact, not a real coverage gap, and it is PRE-EXISTING** (baseline on
`feat/p13-webex-integration`, before the P13 PR-B webhook work, fails the gate with the identical 5 files).

**Root cause:** `coverlet.runsettings` has `<ExcludeByAttribute>...CompilerGeneratedAttribute...</ExcludeByAttribute>`.
Async methods compile to `[CompilerGenerated]` state machines (`<Method>d__N.MoveNext`) — where the async
body's coverage lives. Coverlet excludes them, but the async method's *declaration* line stays coverable-
but-unhit → async-heavy files read artificially low. The Webex module is almost entirely `async` logic
reached only by **unit tests** (mocked `HttpClient`), so it's hit hardest; most other modules clear 95% on
their sync portions + integration (`WebApplicationFactory`) reach. Proof: dropping `CompilerGeneratedAttribute`
from the exclusion jumps `WebexApiClient` 6/15→103/128 and `WebexWebhookRegistrar` 0/4→18/25 in one run.

**Verification that new Webex tests DO run** (coverlet just doesn't credit them): `Acmp.Application.Tests`
count is 724 at baseline vs 734 with PR-B's 10 new tests — they execute and pass; only the coverage
attribution is wrong. All non-Webex files sit at 99%+.

**Implications / how to apply:**
- Do NOT chase Webex-module coverage numbers from the local gate, and do NOT slap `[ExcludeFromCodeCoverage]`
  on real Webex code to "fix" it — the tests exist and pass; the tool is miscounting.
- The proper fix is an infra/config task: stop excluding async state machines (scope or remove
  `CompilerGeneratedAttribute` from `coverlet.runsettings`), then add tests for the few genuine error/DTO
  branches that remain, so the Webex module clears 95% honestly. Touches the whole gate — treat as its own slice.
- The P13 branch is unpushed / never CI-verified on coverage, which is why this was latent. See
  [[ci-gates-run-locally-pre-push]] and [[p13-webex-integration-plan]].
