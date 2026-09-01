---
name: def109-the-host-is-the-unit-that-leaks
description: DEF-109 is diagnosed — every WebApplicationFactory host in Acmp.Api.Tests is retained forever; how it was measured, and the two instrument traps that nearly buried it.
metadata:
  type: project
---

**`DEF-109` IS DIAGNOSED (2026-09-02, `DEC-111` d1) AND CARRIED, NOT FIXED.** Evidence `PE-771`,
remedy `DW-096`, lessons `LL-047`/`LL-048` (both **Proposed** — not binding until confirmed).

⭐⭐⭐ **THE UNIT THAT LEAKS IS THE HOST.** Identical work — 80 requests — retains **137 MB over
twenty `AcmpWebApplicationFactory` hosts and 8 MB over one**. Twenty disposed factories held only
by `WeakReference` are **20 of 20 alive after a forced full GC**; ~**6.9–8.6 MB retained per host**;
the suite builds ~**293** hosts → 2.0–2.5 GB on a 7 GB runner shared with four other test projects
and Testcontainers. Not the request, not the store.

⚠⚠⚠ **THE INSTRUMENT DECIDED THE ANSWER, AND THE PREVIOUS PASS'S COULD NOT HAVE FALSIFIED ANYTHING**
(`LL-047`). `PE-769` sampled **working set** on a 64 GB box — where an unpressured GC makes a leak and
lazy collection identical — so its unchanged curve was equally consistent with *hypothesis wrong* and
*instrument blind*. **Always: `GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
GC.GetTotalMemory(true)`.** Ask what a NEGATIVE result would mean BEFORE running the experiment.

⚠⚠ **A ROOT-PATH TOOL NAMES *A* PATH, NEVER *THE* CAUSE** (`LL-048`). SOS `gcroot` printed
`DefaultPartitionedRateLimiter.RunTimer` — plausible, project-owned, exactly where a cause belongs.
**Removing it changed nothing** (20/20 still rooted). Stripping ALL of OpenTelemetry cut retention
141→95 MB and **freed zero hosts**. The footer said it: `Found 505 unique roots`. Real path is
structural — process-global roots → an `ExecutionContext` captured at **host startup** →
`HostFactoryResolver+HostingListener` → `Mvc.Testing.DeferredHostBuilder` → the factory. **Read the
root COUNT before the path; two failed bisects mean stop enumerating and vary the QUANTITY.**

⛔ **THE INMEMORY-STORE HYPOTHESIS IS REFUTED and the release was PROVED to execute first** (`LL-013`).
The working seam needs **no live provider**: at dispose, fresh options-only `DbContext` per store NAME +
`Database.EnsureDeleted()` (EF deletes by name — any options-only context type works). Proof: the store
reads **5 rows alive / 5 after dispose without / 0 with**. Result: 345→335 MB = **3%**. `PE-769`'s version
never ran at all (`ObjectDisposedException: IServiceProvider` — the provider is gone in `Dispose(bool)`).

⚠ **`PE-768` CORRECTED TWICE:** 256 hosts is an undercount (+33 `WithIdentityProvider()` +4 xUnit ≈ 293),
and *"there is NO `IClassFixture` anywhere"* is **false** — `EndpointAuthorizationCoverageTests` and the
three Webex classes all use one.

## Tooling that made this possible (install once, reuse)
`dotnet tool install --global dotnet-gcdump dotnet-dump`. A test can dump **its own** process by spawning
the collector as a child on `Environment.ProcessId`. `dotnet-gcdump report <file>` prints types by size in
the terminal (the ×20 counts are what revealed twenty live host graphs); `dotnet-dump analyze <dmp> -c
"dumpheap -type X" -c "gcroot <addr>" -c exit` gives root paths non-interactively.
⚠ **Windows python reads `/tmp/x` as `C:\tmp\x`** — pass `C:/Users/…/AppData/Local/Temp` explicitly.
⚠ `python -c "…"` is shimmed and breaks; write a script file. Add
`sys.stdout=io.TextIOWrapper(sys.stdout.buffer,encoding='utf-8',errors='replace')` or cp1252 kills it.
