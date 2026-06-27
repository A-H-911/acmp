# CHANGE-001 — ACMP Self-Hosts Keycloak; All Runtime Dependencies Bundled

**Date:** 2026-06-25 · **Authority:** ADR-0015 (secretary-directed) · **Status:** applied to the planning package; **Claude Code change-prompt below** to roll into the in-flight build.

This is the **mechanism record** for a change to a settled decision, made while the execution agent (Claude Code) is mid-**P4**. It exists so the change is traceable end-to-end (no silent drift), per the package rule "*any change to a settled decision requires an ADR*."

---

## 1. What changed and why

- **Trigger:** **ASM-001** (`docs/41-raid.md`) — *"the org's Keycloak will be accessible… ACMP does not need to manage a separate IdP"* — is now **FALSE**. The organization has **no Keycloak**. ASM-001's own documented mitigation was *"Keycloak must be provisioned as part of the Compose stack."*
- **Directive:** the platform must **own all of its runtime dependencies** (strengthen CON-001 to **zero external runtime services** in v1).
- **Decision:** **ADR-0015** — ACMP **self-hosts Keycloak** (bundled container + ACMP-owned realm) and **bundles SQL Server** too. The OIDC contract is unchanged.

## 2. Decisions taken (2026-06-25)

| # | Question | Decision |
|---|----------|----------|
| Q1 | Scope of "own all dependencies" | **Bundle both Keycloak and SQL Server** → zero external runtime services. |
| Q2 | Keycloak's datastore (vs ADR-0003 "SQL only") | **Decide at a PH-0 spike** (`OQ-038`): Postgres-for-Keycloak vs Keycloak-on-the-bundled-SQL-Server. App data stays SQL-Server-only. |
| Q3 | User provisioning | **Manual via the self-hosted Keycloak admin console.** ACMP only consumes identity → **Membership / P4 unaffected.** |
| Q4 | Rollout (P4 in flight) | **Finish P4, then apply this change-set** (P4 code is unaffected). |

## 3. What does NOT change (so P4 is safe)

The application-side identity contract is identical: **OIDC authorization-code + PKCE**, **roles from realm-role/group claims**, **no self-registration**, **per-topic ABAC in ACMP**. P4 (Membership: claim→role mapping, ABAC, permission-matrix) needs **no rework**. What changes is *infrastructure and ownership*, not app logic.

## 4. New open decisions (added to `docs/42-open-decisions.md`)

- **OQ-038** — Keycloak datastore: dedicated Postgres container vs Keycloak-on-the-bundled-SQL-Server (PH-0 spike; default: Postgres-for-Keycloak).
- **OQ-039** — Future upstream federation: should ACMP's Keycloak be designed to later broker/federate to an org IdP if one appears? (Deferred; default: no in v1, keep realm broker-capable.)
- **OQ-040** — Bundled SQL Server **production edition/licensing**: Express (free; 10 GB/DB, memory caps; verify columnstore + FTS) vs Standard. Resolve at deploy (P18); default: confirm with the secretary/IT.

## 5. Impacted artifacts (change-set)

**Decisions:** `adr/ADR-0015` (new); `adr/ADR-0004` (federation aspect superseded — banner); `adr/ADR-0013` (amended — "two exceptions" withdrawn); `adr/README.md` (index).

**Canon:** `README.md` + `docs/README.md` (CON-001 §, the "two allowed exceptions" → bundled, identity row, §A resolved-decisions); `CLAUDE.md` (identity line + settled-stack); `.context/brief-digest.md` (CON-001 implication).

**Docs:** `01` (CON-001 implication), `15` (CON-001 table + arch diagram label), `24` + `25` (IdP now ACMP-owned; authN strength owned by ACMP; new attack surface), `33` (constraints + compose: add `keycloak` + `sqlserver` + realm bootstrap + KC store; backup/health), `34` (repo structure: add `deploy/keycloak/`), `08` (Keycloak + SQL in availability scope), `41` (ASM-001 → resolved-false; RISK-001 → closed; CON-001 row), `42` (OQ-038/039/040; OQ-003 now ACMP-realm), `39` (US-001 wording), `43` (ops ownership), `23` (build-vs-buy note).

**Handoff:** `execution-handoff/agent-guardrails.md` (allowed-external-deps line), `initial-prompt.md`, `phase-prompts.md` (P1 compose list + realm bootstrap), `claude-code-execution-package.md`, plus the operator docs `HANDOFF-RUNBOOK.md` / `BUILD-STEPS.md` (compose service lists).

## 6. Claude Code change-prompt (paste AFTER P4 is complete, before P5)

```
Heads-up: a settled architecture decision changed. Before continuing, read in full:
/adr/ADR-0015-self-hosted-keycloak-all-dependencies-owned.md and
/execution-handoff/CHANGE-001-keycloak-ownership.md.

Summary: ASM-001 (org provides Keycloak) is FALSE — the org has no Keycloak. Per ADR-0015, ACMP now
SELF-HOSTS Keycloak as a bundled container with an ACMP-owned realm, and SQL Server is also bundled —
v1 has ZERO external runtime services. The OIDC contract is unchanged (authz-code + PKCE, roles from
realm-role/group claims, no self-registration), and user provisioning stays manual in the Keycloak
admin console — so your P4 Membership code does NOT need rework.

Do this:
P4 is already complete and needs NO rework (the identity contract is unchanged). Run this INFRA CHANGE-SLICE now, before P5 (small commits; raise no new ADR — ADR-0015 covers it):
   - Add `keycloak` and `sqlserver` services to deploy/docker-compose.yml (dev + prod). The v1 stack is
     now: api, web, keycloak, sqlserver, seq, minio.
   - Add deploy/keycloak/ with a realm bootstrap (realm-export.json): the ACMP realm, an OIDC client for
     the SPA/BFF (authz-code + PKCE), the 8 canonical roles as realm roles + groups (Chairman, Secretary,
     Member, Reviewer, Auditor, Administrator, Submitter, Guest/Presenter), and an initial bootstrap admin.
     Point ACMP's KEYCLOAK_AUTHORITY at the in-stack keycloak.
   - Keycloak datastore = OQ-038 (PH-0 spike): try Postgres-for-Keycloak vs Keycloak-on-the-bundled-SQL
     -Server; pick what runs cleanly; record the result. Keep ACMP APPLICATION data SQL-Server-only (ADR-0003).
   - Tighten the self-contained lint: the only allowed external hostname is Webex (Phase 2).
   - Add Keycloak's store + realm to backup/restore and health checks (now in the 99.9% scope).
- Surface OQ-040 (bundled SQL Server prod edition/licensing) for human confirmation at deploy; OQ-039
   (future upstream federation) is deferred.
- Update /docs/_progress/progress-log.md and acceptance-audit.md.

Confirm back: your understanding, that P4 needs no rework, and your plan for the infra change-slice.
Do NOT start the infra work until I confirm.
```

## 6a. Review prompt (paste after the change-slice is built, before P5)

```
Review the Keycloak-ownership change-slice before I advance to P5. Do NOT write feature code — audit only and report a verdict.
1. Stack: a clean `docker compose up` brings up api, web, keycloak, sqlserver, seq, minio HEALTHY; show the service list + health states.
2. Realm: the ACMP realm imported from deploy/keycloak/ has the OIDC client (authz-code + PKCE) and all 8 roles as realm roles + groups (Chairman, Secretary, Member, Reviewer, Auditor, Administrator, Submitter, Guest/Presenter) + an initial admin; a login round-trip lands a user with correctly mapped roles.
3. Self-contained: the compose file + config reference NO external runtime hostname (only Webex, Phase 2); KEYCLOAK_AUTHORITY points at the in-stack keycloak; ACMP application data is still SQL-Server-only (ADR-0003); Keycloak's own store follows the OQ-038 decision and is recorded.
4. Resilience: Keycloak's datastore + realm are covered by backup/restore and have health checks (now in the 99.9% scope).
5. Traceability: ADR-0015 honored; OQ-038 result recorded; OQ-039/OQ-040 noted; progress-log + acceptance-audit updated; the permission-matrix tests still pass (identity contract unchanged).
6. Output a table (item → verdict → evidence/gap) and a GO / NO-GO with the specific gaps. Don't fix anything yet — just report.
```

## 7. Verification checklist

- `grep -ri "federate\|two .*exception\|not own an idp\|external.*keycloak"` across the package returns only **historical** mentions inside ADR-0004/0013 bodies (under their amendment banners) — no live guidance says "federate to org Keycloak."
- `docker compose` service list everywhere reads **api / web / keycloak / sqlserver / seq / minio**.
- ADR index shows ADR-0015 Accepted; ADR-0004 "federation aspect superseded"; ADR-0013 "amended".
- `docs/42` contains OQ-038/039/040; `docs/41` ASM-001 marked resolved-false and RISK-001 closed.
