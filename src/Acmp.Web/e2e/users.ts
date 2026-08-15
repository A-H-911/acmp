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
  /**
   * The committee realm role to assign, matching a role in realm-export.json.
   *
   * OPTIONAL, and the absence is a FIXTURE STATE RATHER THAN A GAP: AC-003 is about what the system
   * does with a validated token that carries no ACMP role claim at all, so an account with no
   * committee role is the subject under test, not a half-seeded user.
   */
  readonly realmRole?: string;
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

/*
 * AC-003's subject — a REAL Keycloak account holding NO committee realm role.
 *
 * ⚠ DELIBERATELY OUTSIDE E2E_USERS, so it is not a member of E2eRole. loginAs() waits for the
 * committee dashboard and for the login CTA to disappear; this account is REFUSED provisioning, and
 * giving it a name that typechecks against loginAs would invite exactly the helper whose
 * post-conditions do not hold for it. Keeping it out of the union makes that a compile error instead
 * of a 30-second timeout that says nothing about the cause.
 *
 * ⚠ AND IT IS SAFE AGAINST THE DEF-045 HAZARD NOTED ABOVE, which is why this list may grow here at
 * all: the hazard is that a login PROVISIONS a CommitteeMember and shifts absolute counts in other
 * specs. This login's provisioning call is refused, so no member row is ever created — the one
 * seeded user in this file that cannot move any count.
 */
export const E2E_ROLELESS_USER: E2eUser = {
  username: 'e2e-roleless',
  firstName: 'E2E',
  lastName: 'Roleless',
  email: 'e2e-roleless@acmp.test',
};
