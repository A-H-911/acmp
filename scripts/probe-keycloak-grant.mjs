#!/usr/bin/env node
/*
 * OQ-070 — establish the MINIMUM realm-management grant for ACMP's Keycloak service account.
 *
 * ADR-0038 requires the minimum scope and explicitly NOT realm-admin, and says the set is to be
 * PROVEN rather than assumed from documentation — "a narrower grant being refused is a signal to
 * look at the refusal, not to widen it". A stub transport cannot answer the question at all: the
 * behaviour under test is Keycloak's own authorization, which only a real Keycloak evaluates.
 *
 * This exercises EXACTLY the eight admin calls KeycloakAdminClient.cs makes and nothing else — the
 * list is derived from the adapter, not from the ADR prose, because the grant has to cover what the
 * code does rather than what the design said it would do. Calls 4-6 are the ones worth watching:
 * they go through Keycloak's role-mapper resource, and a grant that covers create + password but
 * not role mapping fails ONLY on FR-157, so the invite looks healthy while role assignment 500s.
 *
 * A PASSING RUN IS NOT EVIDENCE OF MINIMALITY. It is evidence of sufficiency. Minimality is proven
 * by re-running with a narrower grant and seeing a 403 — which is why the output names the failing
 * call and its status rather than just exiting non-zero.
 *
 * The probe user is FIXED and REUSED, never deleted: DEF-029 makes deleting a Keycloak user
 * strand its ACMP member row forever, and DEC-039 says UAT is never reset. Re-runs converge on the
 * same account and leave it disabled.
 *
 * Usage:
 *   node scripts/probe-keycloak-grant.mjs \
 *     --base http://localhost:8099/kc --realm acmp --client acmp-admin-svc --secret <secret>
 */

const args = Object.fromEntries(
  process.argv.slice(2).flatMap((a, i, all) => (a.startsWith('--') ? [[a.slice(2), all[i + 1]]] : [])),
);
const BASE = (args.base ?? 'http://localhost:8080').replace(/\/+$/, '');
const REALM = args.realm ?? 'acmp';
const CLIENT = args.client ?? 'acmp-admin-svc';
const SECRET = args.secret;
const PROBE_USER = args.user ?? 'kc-grant-probe@acmp.local';

if (!SECRET) {
  console.error('probe-keycloak-grant: --secret is required (the service account client secret).');
  process.exit(2);
}

const results = [];
let token;

/** One admin call. Records the outcome instead of throwing, so a 403 names itself rather than
 *  aborting the run — the point is to learn WHICH call a narrower grant refuses. */
async function call(n, label, method, path, body) {
  const url = `${BASE}/admin/realms/${REALM}${path}`;
  const init = { method, headers: { Authorization: `Bearer ${token}` } };
  if (body !== undefined) {
    init.headers['Content-Type'] = 'application/json';
    init.body = JSON.stringify(body);
  }
  const res = await fetch(url, init);
  const ok = res.status >= 200 && res.status < 300;
  results.push({ n, label, method, path, status: res.status, ok });
  return res;
}

async function authenticate() {
  const res = await fetch(`${BASE}/realms/${REALM}/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ grant_type: 'client_credentials', client_id: CLIENT, client_secret: SECRET }),
  });
  if (!res.ok) {
    console.error(`✖ client_credentials failed: ${res.status} ${await res.text()}`);
    console.error('  The client must exist, be confidential, and have serviceAccountsEnabled=true.');
    process.exit(1);
  }
  token = (await res.json()).access_token;
}

/** The probe user, created if absent. Never deleted (DEF-029); left disabled by call 8. */
async function ensureProbeUser() {
  const create = await call(1, 'create user', 'POST', '/users', {
    username: PROBE_USER,
    email: PROBE_USER,
    firstName: 'Grant',
    lastName: 'Probe',
    enabled: true,
    emailVerified: false,
  });

  if (create.status === 409) {
    // Already there from a previous run. Look it up — and note this GET is an extra permission the
    // adapter itself never needs, so a failure here is a PROBE limitation, not a grant finding.
    const res = await fetch(`${BASE}/admin/realms/${REALM}/users?username=${encodeURIComponent(PROBE_USER)}&exact=true`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    const found = res.ok ? await res.json() : [];
    if (found[0]?.id) {
      // Re-enable so the disable at the end is a real transition rather than a no-op.
      await fetch(`${BASE}/admin/realms/${REALM}/users/${found[0].id}`, {
        method: 'PUT',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ enabled: true }),
      });
      return found[0].id;
    }
    console.error('✖ probe user exists but could not be looked up — cannot continue.');
    process.exit(1);
  }

  if (!create.ok) return null;
  return create.headers.get('location')?.split('/').filter(Boolean).pop() ?? null;
}

async function main() {
  await authenticate();

  const id = await ensureProbeUser();
  if (!id) {
    report();
    process.exit(1);
  }

  // 2 — temporary password (AC-088: must change at first login).
  await call(2, 'set temporary password', 'PUT', `/users/${id}/reset-password`, {
    type: 'password',
    value: 'Probe-' + Math.abs(Date.now() % 1e9) + '!aA',
    temporary: true,
  });

  const mappings = `/users/${id}/role-mappings/realm`;

  // 3-6 — the role-mapper resource. THE PART MOST LIKELY TO REFUSE under a narrow grant.
  const current = await call(3, 'read realm role mappings', 'GET', mappings);
  const held = current.ok ? await current.json() : [];

  const avail = await call(4, 'read assignable realm roles', 'GET', `${mappings}/available`);
  const available = avail.ok ? await avail.json() : [];

  const member = available.find((r) => r.name === 'Member') ?? held.find((r) => r.name === 'Member');
  if (member) {
    await call(5, 'add realm role mapping', 'POST', mappings, [{ id: member.id, name: member.name }]);
    await call(6, 'remove realm role mapping', 'DELETE', mappings, [{ id: member.id, name: member.name }]);
  } else {
    results.push({ n: 5, label: 'add realm role mapping', status: 'SKIPPED', ok: false });
    results.push({ n: 6, label: 'remove realm role mapping', status: 'SKIPPED', ok: false });
  }

  // 7 — forced sign-out (AC-090). Succeeds on a user with no sessions; the permission is the point.
  await call(7, 'force sign-out', 'POST', `/users/${id}/logout`);

  // 8 — disable (FR-159 guest expiry). Disable, never delete (DEF-029).
  await call(8, 'disable user', 'PUT', `/users/${id}`, { enabled: false });

  report();
  process.exit(results.every((r) => r.ok) ? 0 : 1);
}

function report() {
  console.log(`\nclient=${CLIENT} realm=${REALM} base=${BASE}`);
  for (const r of results.sort((a, b) => a.n - b.n)) {
    console.log(`  ${r.ok ? '✔' : '✖'} ${r.n}. ${r.label.padEnd(28)} ${r.method ?? ''} ${String(r.status)}`);
  }
  const refused = results.filter((r) => !r.ok);
  if (refused.length === 0) {
    console.log('\nAll 8 calls SUFFICIENT under this grant. That is NOT proof it is minimal —');
    console.log('re-run with a narrower grant; the set is minimal when removing anything refuses.');
  } else {
    console.log(`\n${refused.length} call(s) REFUSED under this grant:`);
    for (const r of refused) console.log(`  ${r.n}. ${r.label} -> ${r.status}`);
    console.log('A refusal is a signal to read it, not to widen to realm-admin (ADR-0038).');
  }
}

await main();
