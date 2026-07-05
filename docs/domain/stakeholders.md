# 04 — Stakeholders & User-Role Analysis

**Purpose:** Map everyone who has a stake in ACMP — their goals, pains, and what they need from the platform — then map each stakeholder to the canonical global roles and per-topic capabilities, with a note on least-privilege enforcement and the rationale for rejecting self-registration.

---

## Part A: Stakeholder Analysis

### Stakeholder Table

| Stakeholder | Goals | Current pains (from PAIN-## catalog) | What they need from ACMP | Influence | Interest |
|---|---|---|---|---|---|
| **Chairman / VP** | Authoritative, defensible governance; national-scale confidence; fast, clear decision authority; override when needed | PAIN-10 (no vote record), PAIN-11 (override not formalized), PAIN-12 (no audit trail), PAIN-23 (secretary dependency) | Clear decision dashboard; override recorded as distinct event; full audit access; governance health at a glance; ability to escalate | **High** (ultimate decision authority; approves all decisions) | **High** (responsible for governance quality at national level) |
| **Secretary** | Efficient committee operations; reliable backlog; reduced manual workload; accurate MoM without 48-hour turnaround; no secretary bottleneck if temporarily absent | PAIN-01, PAIN-03, PAIN-14, PAIN-18, PAIN-23 (almost all pains) | Full backlog management; agenda builder with DnD; auto-drafted MoM; action reminder and escalation system; dashboard for open items; ability to add/invite users | **High** (runs daily committee operations; primary user) | **Critical** (every process failure lands on the Secretary) |
| **Committee Members** (Technical Stream Directors + senior engineers) | Clear, well-prepared topics; easy voting; visibility into past decisions; know their open actions | PAIN-03 (poorly prepared topics), PAIN-05 (no traceability), PAIN-10 (no vote record), PAIN-18 (actions not tracked), PAIN-21 (history lost) | Low-friction topic view; voting interface; action dashboard; ADR/decision history; notification on assigned actions | **Medium–High** (votes carry governance weight; influence decisions; raise topics) | **High** (decisions affect their stream's technical direction) |
| **iOS SME / Android SME** | Platform decisions affecting mobile are technically sound; their specialist input is captured | PAIN-06 (rationale lost), PAIN-08 (dependency blindness) | Ability to comment and contribute as a specialist; mobile-specific decision traceability | **Medium** (SME veto power on mobile decisions informally) | **High** for mobile-relevant decisions; low otherwise |
| **Stream Directors — Technical** | Cross-stream architectural alignment; decisions that do not block their stream; visibility into pending topics | PAIN-08 (dependency blindness), PAIN-09 (no backlog discipline) | Filtered backlog view by their stream; impact analysis for cross-stream decisions; notification when their stream is affected | **High** (governance authority over their stream; can escalate to committee) | **High** |
| **Stream Directors — Delivery / Business** | No last-minute architectural surprises; decisions translate to clear delivery impact | PAIN-05 (no traceability), PAIN-08 (dependency blindness) | Decision outcomes with delivery impact clearly stated; action owners and timelines visible | **Medium** (not voting members typically; stakeholders of decisions) | **Medium–High** (delivery timelines affected by architecture decisions) |
| **Stream Submitters** (engineering team members requesting committee review) | Easy, unambiguous submission; know their topic's status; not left in the dark | PAIN-02 (no topic ID), PAIN-03 (no schema), PAIN-09 (no backlog discipline) | Simple submission form with required fields; status tracking (`TOP-YYYY-###`); notification on outcome | **Low** (no vote; submit topics only) | **High** (the topic outcome directly affects their work) |
| **Auditors / Compliance** | Defensible, immutable records; verifiable vote counts and decision authority; complete governance trail | PAIN-10, PAIN-11, PAIN-12 (entire audit theme) | Read-only access to full audit log; vote records; decision history; MoM archive; no ability to alter records | **Medium** (may escalate compliance findings; can block releases) | **High** (the audit trail is their primary concern) |
| **Executives / VP as Sponsor** | Governance demonstrates organizational maturity; national platform is architecturally sound; no governance surprises | PAIN-24 (no dashboards), PAIN-12 (no audit) | High-level governance health dashboards; decision throughput; open risk visibility; no need to dig into operational detail | **High** (platform sponsor; ultimate accountability) | **Medium** (engaged at escalation or reporting level, not daily) |
| **Engineering teams as decision consumers** | Know what has been decided; understand constraints before starting implementation; avoid building in conflict with an architecture invariant | PAIN-07 (no ADRs), PAIN-21 (knowledge loss) | ADR repository browsable and searchable; architecture invariants visible; topic/decision status readable (notification when relevant decisions are made) | **Low** (consumers, not governors) | **High** (decisions directly constrain their implementation choices) |

---

### Stakeholder Map (Influence vs. Interest)

```
High Influence │ Chairman/VP        Stream Tech Directors
               │ Secretary        Executives/Sponsor
               │
               │ Committee Members  iOS/Android SMEs
               │
               │ Auditors/Compliance
               │
Low  Influence │ Delivery/Business  Stream Submitters   Engineering Teams
               └──────────────────────────────────────────
                Low Interest                        High Interest
```

**Engagement priority:** Chairman, Secretary, and Committee Members are primary users whose daily workflow ACMP replaces — they must be involved in acceptance testing. Executives are reporting consumers. Auditors validate the audit model. Stream Submitters and Engineering Teams are secondary users requiring low-friction touch-points (submission form + notifications + read access).

---

## Part B: User-Role Analysis

### Global Role Mapping (Canonical — README §C)

| Global Role | Stakeholder(s) | Access posture | Key capabilities |
|---|---|---|---|
| `Chairman` | VP / Chairman | Elevated: all committee actions + override | Vote override; chairman final approval; escalate; access full audit; read all topics regardless of stream scope |
| `Secretary` | The Secretary | Operational full access | Manage backlog; build/publish agenda; manage meetings; compile/approve MoM; create/manage members; configure voting; manage templates; send notifications; run reports |
| `Member` | Committee Members (Technical Directors, senior engineers, SMEs) | Standard member | View all topics in scope; participate in voting; add comments/feedback; view decisions and ADRs; manage own assigned actions; receive notifications |
| `Reviewer` | Invited specialist reviewers; SMEs for specific domains | Read + comment | View assigned topics; add structured comments; no vote; no backlog management |
| `Auditor` | Auditors / Compliance | Read-only, full breadth | Read all records including audit log, votes, decisions, MoM archive; cannot modify any record |
| `Administrator` | IT/Platform admin (may overlap with Secretary initially) | Platform config | User provisioning/deactivation; role assignment; system configuration; integration health monitoring; does not participate in governance |
| `Submitter` | Stream Submitters (engineering team members) | Narrow write + read own | Submit topics; view status of own submissions; receive outcome notifications; cannot access full backlog or vote |
| `Guest/Presenter` | External presenters, partner engineers (time-boxed) | Time-limited, narrow | Present in meeting context; view their own topic; add attachments; cannot vote; cannot view other topics; access expires after meeting closure |

### Per-Topic Capabilities (ABAC layer — README §C)

In addition to global roles, each topic may carry relationship-based per-topic capabilities. These are **additive** to global role permissions — they grant topic-scoped access that the global role alone does not provide.

| Per-topic capability | Granted to | Effect |
|---|---|---|
| `Owner` | The person responsible for a topic (set at submission or by Secretary) | Can edit topic fields; manage attachments; update status within permitted transitions; receive escalation notifications |
| `Assignee / Contributor` | Members or reviewers assigned to contribute to a specific topic | Can add notes, comments, and attachments to that topic; tracked as contributors in traceability |
| `Presenter` | The person presenting the topic in a meeting | Can upload presentation materials; marked in MoM; optionally given `Guest/Presenter` role if external |

### Authorization Model

Authorization = **global role policy** + **topic/stream scope** (ABAC).

Example: a `Member` has a global role permitting access to meeting views and voting. Their ABAC scope restricts which streams' topics are visible (e.g., a Technical Stream Director for Stream 3 may have `Member` global role but see topics in all streams if that is the committee's policy — or only their stream if restricted). The Secretary configures stream-scope rules at onboarding. The `Chairman` and `Auditor` roles always have cross-stream visibility regardless of scope.

Full permission matrix in `docs/domain/permission-role-matrix.md`.

---

### Why Self-Registration Is Rejected

**Decision: No public self-registration. Onboarding is invitation/provisioned only.** (ADR-0004)

| Constraint | Rationale |
|---|---|
| **Sensitive government system** | ACMP holds architecture decisions, vote records, and security-relevant governance artifacts for a national-scale platform. Unrestricted registration would expose these records to unauthorized parties. |
| **Enterprise SSO requirement** | The org operates Keycloak as its strategic identity provider. Users are expected to authenticate via enterprise credentials. A self-service registration flow would create a parallel credential store not managed by the org's IT governance. |
| **Least-privilege by design** | Every user's access is intentional and role-scoped. Registration without explicit role assignment would create zero-privilege accounts that must then be manually promoted — adding noise without value. |
| **Audit requirement** | Who has access to this system is itself an auditable fact. The `Administrator` role owns provisioning so every account creation is a traceable, authorized act. |
| **`Guest/Presenter` is time-boxed** | The only external access pattern is the `Guest/Presenter` role, which is explicitly time-limited to a single meeting and granted by the `Secretary` or `Administrator`. It does not require or allow self-registration. |

**Practical model:** New users are provisioned in Keycloak by the org's IT/admin team, with their global ACMP role supplied via Keycloak group/realm-role claims. The `Secretary` or `Administrator` manages committee membership, stream scope, and per-topic assignments within ACMP; they do not assign global roles internally — those are sourced from Keycloak claims. Authentication is via enterprise SSO (OIDC/Keycloak). No unprovisioned account can access the platform.

---

*Traceability: Stakeholder goals and pains cross-reference `docs/domain/pain-points.md` (PAIN-## IDs). Role definitions are canonical from `README.md §C` and are the authoritative input to `docs/domain/permission-role-matrix.md`. The self-registration rejection is formalized in `docs/adrs/adr-0004`. Stream-scope ABAC rules are specified further in `docs/domain/permission-role-matrix.md` and `docs/requirements/functional.md`.*
