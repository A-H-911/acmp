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
