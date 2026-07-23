---
name: ph5-aws-deployment
description: PH-5 AWS cloud-deployment (UAT+prod) intake is IN the Tamheed package (Proposed); awaiting operator ADR ratification + 6 decisions before P20 executes.
metadata: 
  node_type: memory
  type: project
  originSessionId: 09e80e06-889a-47da-8fea-606c28731ff2
  modified: 2026-07-23T17:38:00.741Z
---

**PH-5 "Cloud hosting & environments" — intake COMPLETE in the package, execution NOT started.** On 2026-07-23 the operator asked to deploy ACMP to AWS (staging/UAT + production) on EC2 + Docker Compose. The plan (revision 2, approved after an adversarial review that caught 5 fatal defects in rev 1) lives at `C:\Users\ahammo\.claude\plans\cozy-scribbling-music.md`.

**Now in the package (all via MCP, gate 7/7 `ready:true`, review.html refreshed):** phase `PH-5` (Proposed, sort 5); slices `SL-020`…`SL-027` (P20 landing-zone · P21 cloud compose + Keycloak→MSSQL · P22 S3 · P23 backup/DR · P24 CD/ECR · P25 provisioning/TLS · P26 user-seeding · P27 testing/cutover); `DEC-030`; `SC-002`; ADRs `ADR-0034` (EC2 host, amends ADR-0013/CON-001/CON-003/INV-003) · `ADR-0035` (S3 supersedes ADR-0014 MinIO clause) · `ADR-0036` (Keycloak on SQL Server, amends ADR-0015, retires OQ-038, **restores** INV-002/CON-002) · `ADR-0037` (subdomains + ECR digest promotion) — **all Proposed**; `DEF-014` (MSSQL_PID:Developer licence, `docker-compose.yml:23`) + `DEF-015` (hardcoded bucket const in 3 Meetings handlers); `AC-075`…`AC-085` (bound to slices, verdicts **Pending** — that's why rollup Pending went 1→12); `RISK-013`…`RISK-024`; `OQ-059`…`OQ-063`. Progress `PE-131`.

**Design decisions locked (operator answers):** subdomains `acmp.anas7ammo.dev` / `uat.acmp.anas7ammo.dev` (zero source changes — apiClient is origin-relative, authConfig uses window.location.origin); reuse MinIO .NET SDK + IAM user key for S3 (gated by U1 spike, fallback AWSSDK.S3); UAT on-demand; **us-east-1** (cheapest, $0.0416/hr t3.medium vs $0.0502 ME regions — Pricing API); consolidate 8→4/5 containers.

**Hard facts verified against the LIVE account (`565393059398`, domain `anas7ammo.dev`, zone `Z00837029D8Y00HHFWDA`):** account created **2024-11-02** (Account API) ⇒ **12-month free tier expired 2025-11-02, none left** (only "Always Free" entries). SQL Server container floor = **2 GiB RAM** (MS Learn, verbatim) ⇒ free-tier t3.micro (1 GiB) can't host it. Cost ≈ **$45/mo on-demand, ~$35 with a 1-yr RI**. Currently **operating as root** (`arn:…:root`) — P20 must move to IAM admin + MFA first (budget actions can't restrain root).

**3 spike gates before dependent slices (unproven-by-design, fallbacks pre-authorized):** U1 = MinIO SDK vs real S3 (SigV4/region/multipart/presign; README only claims "S3-compatible", never AWS) → fallback AWSSDK.S3. U2 = Keycloak 26 on `KC_DB=mssql` in **prod mode** behind `/kc/` + full PKCE (repo only ever ran KC on Postgres + start-dev). U3 = FTS under `MSSQL_PID:Express` on the custom image + Arabic word-breaker + Developer→Express on existing volume.

**Landmines baked into the plan:** RCSI `ALTER` must be **guarded** (`DATABASEPROPERTYEX`) or it blocks on every redeploy; `keycloak_svc` needs `db_owner` on the keycloak DB (Liquibase DDL) — does NOT reopen ADR-0031 (that DENY is in the `Acmp` DB); `backup.sh:30` `WITH COMPRESSION` **fails on Express** (drop it); a standalone `docker-compose.cloud.yml` (base file untouched so `main` e2e can't regress — Compose can't subtract a service); no image-publish path exists today (P24 builds CD from scratch); realm-export localhost URIs are **load-bearing for e2e** (keep them, only ngrok URIs are stale).

**BLOCKED on operator before P20 executes any AWS change:** (Q1) 1-yr Reserved Instance for prod? (Q2) accept 8 slices? (Q3) **ratify the 4 Proposed ADRs** — required. (Q4) Express both envs? (Q5) Webex in UAT? (Q6) SSH-from-IP vs SSM. Working tree has uncommitted package changes (`tamheed-package/`, `review.html`, `csv/`) — not yet committed/branched (INV-013). See [[p19-release-readiness]].
