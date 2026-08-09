import { E2E_PASSWORD, E2E_USERS, type E2eUser } from './users';

/*
 * S6 global-setup (ADR-0016 §2). Runs once before the E2E suite:
 *   1. Wait until the compose stack is reachable (web SPA + Keycloak realm).
 *   2. Seed deterministic per-role test users via the Keycloak admin REST API,
 *      with a fixed password and NO required actions — so the real auth-code +
 *      PKCE login completes unattended. Idempotent (safe to re-run).
 *
 * The prod realm export is never modified; these users live only in the running
 * Keycloak instance for the test run. The bundled realm ships a single admin with
 * UPDATE_PASSWORD required + directAccessGrants off, so it cannot drive E2E — this
 * is the "verify the auth-seed mechanism first" outcome from ADR-0016 §2.
 */
const KC_BASE = process.env.E2E_KEYCLOAK_URL ?? 'http://localhost:8085';
const WEB_BASE = process.env.E2E_WEB_URL ?? 'http://localhost:8088';
const REALM = 'acmp';

/*
 * PRODUCTION REFUSAL. PE-185 recorded, after the first UAT run, that "the governance rows the run
 * wrote are hash-chained and immutable and REMAIN in the database permanently — acceptable for UAT,
 * and exactly why the suite must stay incapable of pointing at production." That property was
 * asserted in prose and never built: E2E_WEB_URL accepted any host, so a single environment
 * variable stood between this suite and the production system of record.
 *
 * What a prod run would do is not recoverable by any cleanup. It seeds fixed-password accounts whose
 * password is committed in e2e/users.ts, and core-loop.spec.ts then submits a topic and schedules a
 * meeting — real governance rows. The audit chain is hash-chained and append-only (INV-005) and a
 * CommitteeMember can be deactivated but never removed (DEF-029), so the accounts can be disabled
 * afterwards while the rows they created stay in the record forever.
 *
 * Matched on the HOST of both URLs, not on a substring of either: a check like
 * url.includes('acmp.anas7ammo.dev') passes the uat subdomain straight through, because the uat host
 * CONTAINS the prod host. ACMP_E2E_ALLOW_PROD exists only so the refusal itself stays testable.
 */
const PROD_HOSTS = ['acmp.anas7ammo.dev'];

function hostOf(url: string): string {
  try {
    return new URL(url).host.toLowerCase();
  } catch {
    return '';
  }
}

function refuseProduction(): void {
  if (process.env.ACMP_E2E_ALLOW_PROD === '1') {
    console.warn('[e2e] production refusal DISABLED via ACMP_E2E_ALLOW_PROD=1');
    return;
  }
  for (const [label, url] of [
    ['E2E_WEB_URL', WEB_BASE],
    ['E2E_KEYCLOAK_URL', KC_BASE],
  ] as const) {
    const host = hostOf(url);
    if (host && PROD_HOSTS.includes(host)) {
      throw new Error(
        `[e2e] REFUSING TO RUN: ${label} points at the production host "${host}".\n` +
          '      This suite seeds fixed-password accounts (password committed in e2e/users.ts) and\n' +
          '      writes governance rows. ACMP audit events are hash-chained and append-only and a\n' +
          '      member row can be deactivated but never deleted, so nothing it writes to production\n' +
          '      can be undone. Run it against UAT, and verify production with deploy/scripts/smoke.sh.',
      );
    }
  }
}

refuseProduction();
const ADMIN_USER = process.env.KC_BOOTSTRAP_ADMIN_USERNAME ?? 'admin';
const ADMIN_PASS = process.env.KC_BOOTSTRAP_ADMIN_PASSWORD ?? 'admin';

async function waitFor(label: string, url: string, timeoutMs = 180_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  let lastErr = '';
  while (Date.now() < deadline) {
    try {
      const res = await fetch(url);
      if (res.ok) {
        console.log(`[e2e] ${label} ready (${url})`);
        return;
      }
      lastErr = `status ${res.status}`;
    } catch (err) {
      lastErr = err instanceof Error ? err.message : String(err);
    }
    await new Promise((r) => setTimeout(r, 3_000));
  }
  throw new Error(`[e2e] ${label} not ready after ${timeoutMs}ms (${url}) — last: ${lastErr}`);
}

async function adminToken(): Promise<string> {
  const res = await fetch(`${KC_BASE}/realms/master/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: 'admin-cli',
      username: ADMIN_USER,
      password: ADMIN_PASS,
    }),
  });
  if (!res.ok) throw new Error(`[e2e] admin token failed: ${res.status} ${await res.text()}`);
  return ((await res.json()) as { access_token: string }).access_token;
}

async function findUserId(token: string, username: string): Promise<string | undefined> {
  const res = await fetch(`${KC_BASE}/admin/realms/${REALM}/users?username=${encodeURIComponent(username)}&exact=true`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`[e2e] user lookup failed: ${res.status} ${await res.text()}`);
  const users = (await res.json()) as Array<{ id: string }>;
  return users[0]?.id;
}

async function realmRole(token: string, name: string): Promise<{ id: string; name: string }> {
  const res = await fetch(`${KC_BASE}/admin/realms/${REALM}/roles/${encodeURIComponent(name)}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`[e2e] realm role '${name}' not found: ${res.status}`);
  return (await res.json()) as { id: string; name: string };
}

async function seedUser(token: string, u: E2eUser): Promise<void> {
  const payload = {
    username: u.username,
    enabled: true,
    emailVerified: true,
    firstName: u.firstName,
    lastName: u.lastName,
    email: u.email,
    requiredActions: [] as string[],
    credentials: [{ type: 'password', value: E2E_PASSWORD, temporary: false }],
  };

  const create = await fetch(`${KC_BASE}/admin/realms/${REALM}/users`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  // 201 = created; 409 = already exists (re-run) → reset the mutable bits below.
  if (!create.ok && create.status !== 409) {
    throw new Error(`[e2e] create user ${u.username} failed: ${create.status} ${await create.text()}`);
  }

  const id = await findUserId(token, u.username);
  if (!id) throw new Error(`[e2e] user ${u.username} missing after upsert`);

  if (create.status === 409) {
    // Existing user: re-assert password + clear any required actions so login is unattended.
    await fetch(`${KC_BASE}/admin/realms/${REALM}/users/${id}`, {
      method: 'PUT',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled: true, requiredActions: [], emailVerified: true }),
    });
    await fetch(`${KC_BASE}/admin/realms/${REALM}/users/${id}/reset-password`, {
      method: 'PUT',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ type: 'password', value: E2E_PASSWORD, temporary: false }),
    });
  }

  const role = await realmRole(token, u.realmRole);
  const assign = await fetch(`${KC_BASE}/admin/realms/${REALM}/users/${id}/role-mappings/realm`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify([{ id: role.id, name: role.name }]),
  });
  if (!assign.ok) throw new Error(`[e2e] role assign ${u.username}→${u.realmRole} failed: ${assign.status}`);
  console.log(`[e2e] seeded ${u.username} (${u.realmRole})`);
}

export default async function globalSetup(): Promise<void> {
  await waitFor('Keycloak realm', `${KC_BASE}/realms/${REALM}`);
  await waitFor('Web SPA', `${WEB_BASE}/`);

  const token = await adminToken();
  for (const u of Object.values(E2E_USERS)) {
    await seedUser(token, u);
  }
}
