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
