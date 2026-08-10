/*
 * Canonical committee roles (README §C) and the Keycloak claim → role mapping.
 *
 * Roles are SOURCED FROM Keycloak (realm-role / group claims) — never set in
 * the SPA (ADR-0004, guardrail 4). This module only translates claim strings
 * into the canonical role set the UI gates navigation on. Full server-side
 * claim→role mapping + ABAC enforcement is P4; nav gating here hides UI only.
 */
export const COMMITTEE_ROLES = [
  'chairman',
  'secretary',
  'member',
  'reviewer',
  'auditor',
  'administrator',
  'submitter',
  'guest',
] as const;

export type CommitteeRole = (typeof COMMITTEE_ROLES)[number];

const ROLE_SET = new Set<string>(COMMITTEE_ROLES);

/*
 * Keycloak emits roles in varied shapes: a bare role ("chairman"), an
 * ACMP-prefixed realm role ("acmp-chairman"), or a group path
 * ("/acmp/chairman"). "coordinator" is the legacy key for Secretary
 * (renamed 2026-06-25) — accepted as an alias so existing realms keep working.
 */
const ALIASES: Record<string, CommitteeRole> = { coordinator: 'secretary' };

function normalizeClaim(raw: string): CommitteeRole | null {
  const leaf = raw.toLowerCase().replace(/^\/?(acmp[/-])?/, '').replace(/^.*\//, '');
  if (ROLE_SET.has(leaf)) return leaf as CommitteeRole;
  return ALIASES[leaf] ?? null;
}

/** Map raw Keycloak claim strings to the distinct canonical roles they denote. */
export function rolesFromClaims(claims: readonly string[] | undefined): CommitteeRole[] {
  if (!claims) return [];
  const found = new Set<CommitteeRole>();
  for (const c of claims) {
    const role = normalizeClaim(c);
    if (role) found.add(role);
  }
  return [...found];
}

/*
 * The single role to SHOW when a principal holds several — highest privilege wins.
 *
 * THE ORDER OF THE ARRAY IS NOT A PRIMARY ROLE, and three call sites used to assume it was. The set
 * above is built by iterating raw claims, so `roles[0]` is whatever Keycloak happened to return
 * first. It is correct by coincidence for a single-role account, which is why it survived until the
 * first multi-role user logged in and the shell told a Secretary he was acting as an Auditor
 * (DEF-034).
 *
 * COMMITTEE_ROLES is declared in the same order as the server's CommitteeRole enum (chairman=0 …
 * guest=7), so scanning it reproduces CommitteeRoleResolver.PrimaryRole ("lowest enum wins")
 * exactly. That equivalence is the reason this is safe to compute on the client at all — if the two
 * orderings ever diverge, this silently disagrees with the server, so keep them together.
 *
 * Deliberately NOT read from MemberProfileDto.Role, which the server already computes correctly:
 * the response of POST /api/members/me is discarded today, so consuming it would make the shell wait
 * on an async call and render a placeholder on first paint, and would need PascalCase normalising.
 * This is synchronous and exact.
 */
export function primaryRoleOf(roles: readonly CommitteeRole[]): CommitteeRole {
  return COMMITTEE_ROLES.find((r) => roles.includes(r)) ?? 'guest';
}
