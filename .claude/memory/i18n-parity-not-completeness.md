---
name: i18n-parity-not-completeness
description: check-i18n.mjs verifies EN/AR key parity only — it does NOT catch enum values missing from both locales.
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 6adb98fe-238d-4673-8691-6525ebd78ddb
---

`scripts/check-i18n.mjs` checks only that en.json and ar.json have the **same keys** (parity). It does NOT verify that every backend enum value has a translation. When a status/enum value is missing from BOTH locales, parity still passes, and the UI silently renders the raw English enum name via i18next's `defaultValue` fallback (e.g. AR meetings list showed `Locked`/`Closed`).

**Why:** parity ≠ completeness; `defaultValue` turns a missing key into a silent wrong-render, not an error.

**How to apply:** whenever adding or touching a C# enum that the SPA renders (AgendaStatus, MeetingStatus, etc.), add EVERY enum value to both `en.json` and `ar.json` in the same commit — don't trust the parity gate to flag the gap. Cross-check the enum's members against the i18n block by hand. Related: [[exact-design-fidelity-visual-loop]].
