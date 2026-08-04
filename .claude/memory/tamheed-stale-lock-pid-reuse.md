---
name: tamheed-stale-lock-pid-reuse
description: "package_open fails on a stale data/.lock holding a bare PID; a naive \"is the PID alive?\" check LIES because of PID reuse — verify identity + start-time vs lock mtime."
metadata: 
  node_type: memory
  type: project
  originSessionId: 7e4bfcb3-7685-4d52-8331-f99b312f4342
  modified: 2026-08-04T13:44:54.744Z
---

`package_open("tamheed-package")` routinely fails with `data/.lock exists — another writer owns this package`. The tamheed MCP server **does not release its lock when it dies or cycles**, so this recurs constantly — it happened **twice in one session** on 2026-08-04 (the server cycled during a long CI poll and orphaned its own lock).

**The lock contains a bare PID and nothing else.** Checking only "is that PID running?" is unsafe and will actively mislead you:

- 2026-08-04, lock said `71948`. **PID 71948 was alive** — so the naive check said *do not touch it*. But that PID belonged to **VS Code (`Code`), started 10:18:56**, while the lock's mtime was **01:51:50** — 8.5 hours *earlier*. A process cannot write a file before it exists. **PID recycling.**

**Correct three-way liveness test** (PowerShell):
1. PID alive at all? If not → stale, done.
2. Process **identity** plausible? The writer is `python.exe` under `…\uv\cache\environments-v2\tamheed-server-*\Scripts\`, never `Code`/`node`/anything else.
3. Process **StartTime ≤ lock mtime**? If the process started *after* the lock was written, it is not the writer.

```powershell
$lock='C:\Users\ahammo\Repos\acmp\tamheed-package\data\.lock'
$id=(Get-Content $lock -Raw).Trim(); $m=(Get-Item $lock).LastWriteTime
$p=Get-Process -Id ([int]$id) -ErrorAction SilentlyContinue
if(!$p){"stale"}else{"$($p.ProcessName) start=$($p.StartTime) startedAfterLock=$($p.StartTime -gt $m)"}
```

Cross-check with `Get-CimInstance Win32_Process -Filter "Name LIKE '%python%'"` — if no live tamheed server has that PID, it's stale. Only then `rm` it. Operator standing instruction: **verify the PID is dead before removing.**

Related: [[ph5-aws-deployment]], [[tamheed-data-repair]].
