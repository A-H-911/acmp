/*
 * AC-088 / AC-091 live leg: does an invite actually reach a real Keycloak?
 *
 *   node uat-invite-probe.mjs [https://uat.acmp.anas7ammo.dev]
 *
 * WHY THIS EXISTS, AND WHY NO EXISTING TEST COVERS IT. The whole ADR-0038 write path — invite, role
 * assignment, forced sign-out, guest disable — is registered ONLY when KeycloakAdmin:Enabled is
 * true. deploy/.env.example sets KEYCLOAK_ADMIN_ENABLED=false, so the CI e2e suite runs the full
 * seven-service stack with a real Keycloak and STILL never exercises it: IIdentityProvider is not
 * in the container at all there. Every backend test uses FakeIdentityProvider. So until an
 * environment runs with the flag on, "invite works" is a claim about a fake.
 *
 * This drives the real thing: a real PKCE login as Secretary, the real access token, the real
 * POST /api/members/invite, and then the roster read-back that proves a row exists. It provisions
 * nothing and seeds nothing — it uses accounts uat-login-probe.mjs already established.
 *
 * ⚠ IT LEAVES AN ACCOUNT BEHIND, ON PURPOSE. DEF-029: deleting a Keycloak user orphans its member
 * row forever, because KeycloakUserId is uniquely indexed and the row cannot be re-linked. Disable,
 * never delete. The probe therefore uses a timestamped address so re-runs never collide, and the
 * accounts it leaves are inert — an invited account holds NO role until one is granted.
 */
import { chromium } from '@playwright/test';

const BASE = (process.argv[2] ?? 'https://uat.acmp.anas7ammo.dev').replace(/\/$/, '');
const TEMP_PASSWORD = process.env.ACMP_SEED_PASSWORD ?? 'ChangeMe_Acmp#2026';
const NEW_PASSWORD = process.env.ACMP_UAT_PASSWORD ?? 'Uat_Acmp#2026_Rotated';
const USER = process.env.ACMP_PROBE_USER ?? 'secretary';

// Timestamped so a second run cannot collide with the first — and so the account it creates is
// obviously a probe artefact to anyone reading the roster later.
const STAMP = new Date().toISOString().replace(/[-:T]/g, '').slice(0, 14);
const INVITE_EMAIL = `probe.invite.${STAMP}@acmp.local`;
const INVITE_NAME = `Invite Probe ${STAMP}`;

let failures = 0;
const ok = (m) => console.log(`  \x1b[32mPASS\x1b[0m ${m}`);
const bad = (m) => { failures++; console.log(`  \x1b[31mFAIL\x1b[0m ${m}`); };

const browser = await chromium.launch();
const ctx = await browser.newContext();
const page = await ctx.newPage();

try {
  console.log(`\n\x1b[1m[invite probe] ${BASE} as ${USER}\x1b[0m`);

  // --- real PKCE login, same shape as uat-login-probe.mjs (no .catch on navigation, PE-175) ---
  await page.goto(BASE, { waitUntil: 'domcontentloaded', timeout: 45_000 });
  await page.locator('.login-cta').click({ timeout: 30_000 });
  await page.waitForURL(/\/realms\/acmp\/protocol\/openid-connect\/auth/, { timeout: 45_000 });

  await page.locator('#username').fill(USER);
  await page.locator('#password').fill(TEMP_PASSWORD);
  await page.locator('#kc-login').click();
  await page.waitForLoadState('domcontentloaded');
  if (await page.locator('#kc-login').count()) {
    await page.locator('#username').fill(USER);
    await page.locator('#password').fill(NEW_PASSWORD);
    await page.locator('#kc-login').click();
    await page.waitForLoadState('domcontentloaded');
  }
  if (await page.locator('#password-new').count()) {
    await page.locator('#password-new').fill(NEW_PASSWORD);
    await page.locator('#password-confirm').fill(NEW_PASSWORD);
    await page.locator('#kc-form-buttons input[type=submit], button[type=submit]').first().click();
  }
  await page.waitForURL((u) => u.origin === BASE && !u.pathname.startsWith('/kc/')
    && !u.pathname.startsWith('/auth/') && u.pathname !== '/login', { timeout: 60_000 });
  await page.waitForFunction(() => !document.querySelector('.login-cta'), null, { timeout: 45_000 });
  ok(`authenticated as ${USER}`);

  // The token comes from where the SPA actually keeps it, rather than being minted here — a probe
  // holding its own credential would prove something about the probe, not about the deployment.
  const token = await page.evaluate(() => {
    for (let i = 0; i < sessionStorage.length; i++) {
      const k = sessionStorage.key(i);
      if (k?.startsWith('oidc.user:')) return JSON.parse(sessionStorage.getItem(k)).access_token;
    }
    return null;
  });
  token ? ok('access token recovered from the SPA session') : bad('no oidc.user entry in sessionStorage');
  if (!token) throw new Error('cannot continue without a token');

  // --- the call this whole probe exists for ---
  const invite = await page.evaluate(async ({ t, email, name }) => {
    const r = await fetch('/api/members/invite', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json', Authorization: `Bearer ${t}` },
      body: JSON.stringify({ email, fullName: name }),
    });
    return { status: r.status, body: await r.text() };
  }, { t: token, email: INVITE_EMAIL, name: INVITE_NAME });

  let invitedId = null;
  if (invite.status !== 200) {
    bad(`POST /api/members/invite -> ${invite.status}: ${invite.body.slice(0, 400)}`);
  } else {
    invitedId = JSON.parse(invite.body).publicId;
    ok('POST /api/members/invite -> 200 against a REAL Keycloak');
    const dto = JSON.parse(invite.body);
    // Assert the SHAPE, never the value: the temporary password is returned exactly once (AC-088)
    // and printing it here would put a live credential in a session log.
    dto.publicId ? ok(`member row created (publicId ${dto.publicId})`) : bad('response carries no publicId');
    dto.temporaryPassword && dto.temporaryPassword.length > 8
      ? ok(`temporary password issued (${dto.temporaryPassword.length} chars, value deliberately not printed)`)
      : bad('no usable temporaryPassword in the response');
    dto.status === 'Invited' ? ok(`status is Invited (${dto.status})`) : bad(`status is "${dto.status}", expected Invited`);
  }

  // --- read-back: a response is a claim, the roster is the state ---
  const roster = await page.evaluate(async ({ t, email }) => {
    const r = await fetch('/api/members', { headers: { Accept: 'application/json', Authorization: `Bearer ${t}` } });
    const all = await r.json();
    return { status: r.status, total: all.length, hit: all.find((m) => m.email === email) ?? null };
  }, { t: token, email: INVITE_EMAIL });

  roster.hit
    ? ok(`roster read-back finds the invited member among ${roster.total} (status ${roster.hit.status}, role ${roster.hit.role})`)
    : bad(`roster read-back (${roster.status}, ${roster.total} members) does NOT contain ${INVITE_EMAIL}`);

  /*
   * AC-093's live leg. An INVITE alone cannot prove it: the AC asks for "what changed FROM and TO",
   * and a creation has no "from". A ROLE ASSIGNMENT is the governed identity change that exercises
   * both sides — and AV-136 records why that works at all, which is load-bearing rather than
   * incidental: AssignRolesHandler mirrors the new role onto the local member row BEFORE emitting,
   * because before/after are drained from the request-scoped EF capture buffer by (subjectType,
   * subjectId). An operation that wrote only to Keycloak would emit a row saying a role changed
   * while being unable to say from what.
   *
   * Member, not Administrator or Chairman: those are PrivilegedRoles and would need
   * confirmedPrivileged, which is AC-089's concern and not this probe's.
   */
  if (invitedId) {
    const assign = await page.evaluate(async ({ t, id }) => {
      const r = await fetch(`/api/members/${id}/roles`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json', Authorization: `Bearer ${t}` },
        body: JSON.stringify({ roles: ['Member'], confirmedPrivileged: false }),
      });
      return { status: r.status, body: await r.text() };
    }, { t: token, id: invitedId });

    assign.status >= 200 && assign.status < 300
      ? ok(`PUT /api/members/${invitedId}/roles -> ${assign.status} (a governed identity change, against a real Keycloak)`)
      : bad(`role assignment -> ${assign.status}: ${assign.body.slice(0, 300)}`);

    // Read the AUDIT STORE back, not the mutation's own response. AC-093 wants the ROW.
    const audit = await page.evaluate(async ({ t, id }) => {
      const r = await fetch(`/api/audit?action=Membership.RolesAssigned&pageSize=25`, {
        headers: { Accept: 'application/json', Authorization: `Bearer ${t}` },
      });
      const p = await r.json();
      return { status: r.status, total: p.total, hit: (p.items ?? []).find((e) => e.subjectId === id) ?? null };
    }, { t: token, id: invitedId });

    if (!audit.hit) {
      bad(`no Membership.RolesAssigned audit row for ${invitedId} (query ${audit.status}, ${audit.total} rows)`);
    } else {
      const e = audit.hit;
      ok(`audit row exists for the change: seq ${e.sequence}, hashVersion ${e.hashVersion}`);
      e.actor ? ok(`WHO acted: ${e.actorName ?? e.actor} (role ${e.actorRole})`) : bad('audit row names no actor');
      e.subjectType && e.subjectId ? ok(`ON WHOM: ${e.subjectType} ${e.subjectId}`) : bad('audit row names no subject');
      e.occurredAt ? ok(`WHEN: ${e.occurredAt}`) : bad('audit row carries no timestamp');
      // "from and to" — beforeJson may legitimately be sparse, but it must be PRESENT for a change
      // to an existing row, which is exactly what AV-136 said had never been verified end to end.
      e.afterJson ? ok(`TO: afterJson present (${e.afterJson.length} chars)`) : bad('afterJson missing — the row cannot say what it changed to');
      e.beforeJson ? ok(`FROM: beforeJson present (${e.beforeJson.length} chars)`) : bad('beforeJson missing — the row cannot say what it changed from');
    }

    // The chain itself, since AC-093 says "hash-chained". Verified over the WHOLE log, including
    // the row just written — a chain that validates only historically proves nothing about the write.
    const chain = await page.evaluate(async ({ t }) => {
      const r = await fetch('/api/audit/verify', { headers: { Accept: 'application/json', Authorization: `Bearer ${t}` } });
      return { status: r.status, body: await r.json() };
    }, { t: token });
    chain.body?.isValid
      ? ok('GET /api/audit/verify -> chain intact across the whole log, including the row just written')
      : bad(`chain verify failed: ${JSON.stringify(chain.body)}`);
  }
} catch (err) {
  bad(`probe threw: ${err.message}`);
} finally {
  await browser.close();
}

console.log(failures ? `\n\x1b[31m${failures} FAILURE(S)\x1b[0m` : '\n\x1b[32mINVITE PROBE PASSED\x1b[0m');
process.exit(failures ? 1 : 0);
