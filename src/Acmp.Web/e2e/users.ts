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

export const E2E_USERS: Record<'secretary' | 'chairman' | 'member', E2eUser> = {
  secretary: { username: 'e2e-secretary', firstName: 'E2E', lastName: 'Secretary', email: 'e2e-secretary@acmp.test', realmRole: 'Secretary' },
  chairman: { username: 'e2e-chairman', firstName: 'E2E', lastName: 'Chairman', email: 'e2e-chairman@acmp.test', realmRole: 'Chairman' },
  member: { username: 'e2e-member', firstName: 'E2E', lastName: 'Member', email: 'e2e-member@acmp.test', realmRole: 'Member' },
};

export type E2eRole = keyof typeof E2E_USERS;
