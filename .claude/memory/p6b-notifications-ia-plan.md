---
name: p6b-notifications-ia-plan
description: Resumable plan for P6b (Notifications IA round-2 reconcile) — design read + drift + scope fork captured; build not started.
metadata: 
  node_type: memory
  type: project
  originSessionId: 564d6d69-166c-4871-a67b-63e284604a98
---

Round-2 reconcile **P6b (Notifications IA)** — **BUILT & shipped** 2026-06-30 on branch `feat/p6b-notifications-ia` (PR opened off `main`, not self-merged; independent of unmerged #53). Bell popover + full inbox reconciled to `ACMP.dc.html` L92–131 + L706–739; backend delta = `read-all` now emits `Notifications.AllRead` audit (reverses P6e no-audit; single mark-read stays un-audited). **type = existing `Category`, key derived from `DeepLink` last segment — no migration, no DTO change** (operator pre-authorised derive-over-column). DV-02 blessed, DV-05 confirmed, RD-09 no-prefs. FE 395 green + per-file lines ≥95%; BE 420+5 green; EN/AR parity; dev-stub VR (EN-light + AR-RTL-dark) matches. **Next = Decisions/Minutes** (the `MeetingMinutes` placeholder points there) + remaining round-2 targets. Follows [[p6a-meeting-ia-plan]]. See [[exact-design-fidelity-visual-loop]], [[i18n-parity-not-completeness]], [[coverage-and-e2e-mandate]].

**Original plan (for reference) below — superseded by the shipped state above.**

**Design authority (corrects the Usage Map text):**
- **Bell popover** = `ACMP.dc.html` L92–131. Inbox **full page** (`/notifications`) = `ACMP.dc.html` L706–739 (the `isNotifPage` block) — NOT System States. `ACMP System States.dc.html` `isNotif` = the **preferences page → DO NOT BUILD (RD-09)**; also do not wire the profile "Notification preferences" link.

**Current code:** `components/shell/NotificationCenter.tsx` (bell popover — wired live, role="region", header + count, plain list, "See all"; **predates the design** — its own comment says "no .dc.html exists"). `pages/NotificationsPage.tsx` (inbox; has Unread/All toggle = DV-05, Load-more = DV-02, `useInfiniteNotifications`). `api/notifications.ts` (`useNotifications`, `useInfiniteNotifications`, `useMarkNotificationRead` single; NotificationItem = {id,titleEn/Ar,bodyEn/Ar,deepLink,isRead,createdAt} — **no `type`/artifact `key` field**).

**Drift to reconcile (bell popover):** add **Mark-all-read**, **Unread/All segmented tabs in the popover**, loading **skeleton** (shimmer), per-item **mark-read button** (dot is a button, `n_markread`), footer **"View all" + chevron**, switch role region→**dialog** (design uses role=dialog + click-away scrim). **Inbox page:** header **Mark-all-read** button, **Unread/All underline tabs w/ counts** (reconcile the existing toggle), row anatomy = type-icon · **TYPE label + artifact key (deep-link) + time** · message · inline **Mark read** button; empty = check-circle card.

**SCOPE — DECIDED = (B) Full reconcile incl. backend** (operator, 2026-06-30):
- Add `POST /api/notifications/read-all` (mark every unread for the current user read) — handler + `IAuditSink.EmitAsync` after SaveChanges + endpoint under the notifications policy; FE `useMarkAllNotificationsRead` invalidating the feed. Filter by `ICurrentUser.UserId` (no IDOR).
- Add **`type`** (notification category enum/string for the TYPE label + tone) and the artifact **`key`** (e.g. TOP-/MTG-/DECN-…, the deep-link target's runtime key) to the Notifications **read model + NotificationDto** so inbox/popover rows match the design exactly. Migration forward-only; backfill/derive from existing rows. Update `api/notifications.ts` NotificationItem type + both UIs.
- Then the FE reconcile: bell popover (Mark-all-read · Unread/All tabs · skeleton · per-item mark-read · View-all+chevron · role=dialog) + inbox page (header Mark-all · Unread/All underline tabs w/ counts · row = type-icon · TYPE label + key + time · message · inline Mark read · check-circle empty).
- **≥95% per-file for BOTH FE and BE.** Do NOT build the preferences page (RD-09); don't wire the profile prefs link.
*(Option A — FE-only honest-defer — was rejected.)*

**DV confirmations:** DV-02 (Load-more vs infinite) → **bless** (design list has no infinite scroll; op-approved). DV-05 (Unread/All) → **confirmed by design** (design ships Unread/All tabs) — keep, mark resolved.

**DoD (standard footer):** unit+integration tests; full suite green; ≥95% per-file (FE; +BE if option B); EN/AR parity + RTL; axe-clean; progress-log + acceptance-audit (resolve DV-02/DV-05; RD-09 note); conventional commits; small PR; visual fidelity to `ACMP.dc.html` popover+inbox (guardrail 14); dev-stub VR (EN-light + AR-RTL-dark) or live if stack up. Branch off `main` (independent of unmerged #53; notif code doesn't touch the meeting shell).
