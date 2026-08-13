/*
 * Deterministic E2E test users (ADR-0016 §2). These are seeded into the running
 * Keycloak at global-setup via the admin API — they are NEVER added to the shipped
 * realm export (deploy/keycloak/realm-export.json), so production stays clean and
 * has no fixed-password accounts. Each maps to one ACMP committee realm role.
 */
export const E2E_PASSWORD = 'E2e!Passw0rd';

export interface E2eUser {
  readonly username: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly email: string;
  readonly realmRole: string; // matches a role in realm-export.json
}

/*
 * The three added for the role-matrix leg (AC-005/006/007) exist ONLY to be REFUSED. None of them
 * can reach committee content, which is the point: PermissionMatrixTests proves the deny matrix
 * against a synthetic TestAuthHandler, and these carry the same matrix through a real Keycloak
 * token instead — the leg those ACs were Partial for.
 *
 * ⚠ Adding seeded users to a suite is how DEF-045 cause 3 happened: a login provisions a
 * CommitteeMember, and any spec asserting an ABSOLUTE count then shifts under it. Checked before
 * adding these — the only remaining absolute count is ac043-reorder's `toHaveCount(2)`, which is
 * scoped to the run's own fixtures by a per-run stamp first, and p17b-meeting-vote was rewritten
 * to "the eligible are in, the ineligible is out". Re-check that the next time this list grows.
 */
export const E2E_USERS: Record<
  'secretary' | 'chairman' | 'member' | 'submitter' | 'auditor' | 'administrator',
  E2eUser
> = {
  secretary: { username: 'e2e-secretary', firstName: 'E2E', lastName: 'Secretary', email: 'e2e-secretary@acmp.test', realmRole: 'Secretary' },
  chairman: { username: 'e2e-chairman', firstName: 'E2E', lastName: 'Chairman', email: 'e2e-chairman@acmp.test', realmRole: 'Chairman' },
  member: { username: 'e2e-member', firstName: 'E2E', lastName: 'Member', email: 'e2e-member@acmp.test', realmRole: 'Member' },
  submitter: { username: 'e2e-submitter', firstName: 'E2E', lastName: 'Submitter', email: 'e2e-submitter@acmp.test', realmRole: 'Submitter' },
  auditor: { username: 'e2e-auditor', firstName: 'E2E', lastName: 'Auditor', email: 'e2e-auditor@acmp.test', realmRole: 'Auditor' },
  administrator: { username: 'e2e-administrator', firstName: 'E2E', lastName: 'Administrator', email: 'e2e-administrator@acmp.test', realmRole: 'Administrator' },
};

export type E2eRole = keyof typeof E2E_USERS;
