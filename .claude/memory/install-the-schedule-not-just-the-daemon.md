---
name: install-the-schedule-not-just-the-daemon
description: The bootstrap installed cron but left the crontab a manual runbook step — provisioning the mechanism is not provisioning the behaviour.
metadata: 
  node_type: memory
  type: project
  originSessionId: 0facad2f-a83f-45b0-a92d-7db892b90d5c
  modified: 2026-08-09T18:05:08.765Z
---

`08-bootstrap-box.sh` installed `cronie` and `systemctl enable --now crond`, then left
`crontab deploy/scripts/crontab.example` as a **manual runbook step** (README step 6 /
cloud-provisioning §8). A box rebuilt from the scripts alone therefore had a running
cron daemon and an **empty crontab** — identical to a scheduled box from every angle
except the one that matters.

This is **DEF-026 half-fixed**: that defect was "AL2023 ships no cron", and the fix
installed the daemon while its own comment argued *"a documented prerequisite is what
gets skipped"* — and then documented this one.

**Consequence that nearly shipped:** editing `crontab.example` to add OQ-068 (b)'s
`@reboot` backup would have produced a line that **never reached any box**. Fixed
2026-08-09 (PR #206): the bootstrap now runs `crontab -u root deploy/scripts/crontab.example`
and prints the table. `crontab <file>` **replaces**, so it converges — and hand-added
lines on the box are discarded, which is the intended direction.

**Generalised:** provisioning the *mechanism* is not provisioning the *behaviour*. Ask
separately whether the thing is installed and whether it is **scheduled/wired to run**.
Sibling of [[controls-must-detect-and-tell]] — there the control could detect but not
tell; here it could run but was never asked to.

**Verify it the same way**, not by trusting the bootstrap's exit code:
`diff <(crontab -u root -l) /opt/acmp/deploy/scripts/crontab.example` — eyeballing
cannot distinguish "the bootstrap installed it" from "a manual crontab was already
there". Confirmed `CRONTAB_EXACT_MATCH` on 2026-08-09.
