/*
 * AC-079 leg 3 / DEF-023 verification: does a real human actually get in?
 *
 *   node uat-login-probe.mjs [https://uat.acmp.anas7ammo.dev]
 *
 * DEF-023 exists because every automated check this project has passed on a box where login was
 * impossible — six healthy containers, a valid certificate, a correct issuer, /api/ answering 401 —
 * because not one of them performed a browser round-trip. So this probe is deliberately the thing
 * none of those were: a real browser, the real PKCE redirect, real credentials, and the real
 * JIT-provisioning call. It asserts the ROLE the API assigned, not merely that a page rendered.
 *
 * Lives here rather than in e2e/ because the e2e suite targets its own disposable stack (its
 * global-setup provisions fixtures); this drives a deployed environment and provisions nothing.
 * It must sit inside src/Acmp.Web so `@playwright/test` resolves from node_modules.
 *
 * Re-runnable: the seeded accounts carry UPDATE_PASSWORD, so the first pass changes the password and
 * later passes would fail with the temp one. It tries the temp password, then the rotated one.
 */
import { chromium } from '@playwright/test';

const BASE = (process.argv[2] ?? 'https://uat.acmp.anas7ammo.dev').replace(/\/$/, '');
const TEMP_PASSWORD = process.env.ACMP_SEED_PASSWORD ?? 'ChangeMe_Acmp#2026';
// Deterministic so a second run can log in with it. Not a secret: these are UAT fixtures on a
// throwaway environment, and the value never leaves this file (INV-007 covers real credentials).
const NEW_PASSWORD = process.env.ACMP_UAT_PASSWORD ?? 'Uat_Acmp#2026_Rotated';

const EXPECTED = {
  chairman: 'Chairman',
  secretary: 'Secretary',
  member: 'Member',
  auditor: 'Auditor',
};

let failures = 0;
const ok = (m) => console.log(`  \x1b[32mPASS\x1b[0m ${m}`);
const bad = (m) => { failures++; console.log(`  \x1b[31mFAIL\x1b[0m ${m}`); };

async function probe(browser, username, expectedRole) {
  console.log(`\n\x1b[1m[probe] ${username} (expect role ${expectedRole})\x1b[0m`);
  // A fresh context per user: sessionStorage holds the tokens, and a shared one would let user N+1
  // inherit user N's session and "pass" without ever authenticating.
  const ctx = await browser.newContext({ ignoreHTTPSErrors: false });
  const page = await ctx.newPage();

  const consoleErrors = [];
  page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });

  // The JIT-provisioning response IS the evidence: it proves the API accepted the token AND says
  // which committee role it resolved. Captured rather than inferred from the UI.
  let profile = null;
  page.on('response', async (r) => {
    if (r.url().includes('/api/members/me') && r.request().method() === 'POST' && r.ok()) {
      profile = await r.json().catch(() => null);
    }
  });

  try {
    // NO .catch() ANYWHERE on navigation. PE-175: swallowing a failed goto turned an exact answer
    // into an unexplained 30s timeout on a selector further down.
    await page.goto(BASE, { waitUntil: 'domcontentloaded', timeout: 45_000 });
    await page.locator('.login-cta').click({ timeout: 30_000 });
    await page.waitForURL(/\/realms\/acmp\/protocol\/openid-connect\/auth/, { timeout: 45_000 });
    ok('Keycloak accepted the authorization request and served the login form');

    await page.locator('#username').fill(username);
    await page.locator('#password').fill(TEMP_PASSWORD);
    await page.locator('#kc-login').click();
    await page.waitForLoadState('domcontentloaded');

    // Wrong password => still on the login form with an alert. Retry with the rotated one so the
    // probe survives its own side effect.
    if (await page.locator('#kc-login').count()) {
      await page.locator('#username').fill(username);
      await page.locator('#password').fill(NEW_PASSWORD);
      await page.locator('#kc-login').click();
      await page.waitForLoadState('domcontentloaded');
    }

    // UPDATE_PASSWORD is a required action on every seeded account (AC-079 leg 2).
    if (await page.locator('#password-new').count()) {
      ok('UPDATE_PASSWORD required action is enforced at first login');
      await page.locator('#password-new').fill(NEW_PASSWORD);
      await page.locator('#password-confirm').fill(NEW_PASSWORD);
      await page.locator('#kc-form-buttons input[type=submit], button[type=submit]').first().click();
    }

    // Playwright hands the predicate a URL OBJECT, not a string — `u.includes` is not a function.
    // And /auth/callback does NOT count as landed: the SPA is still exchanging the code there, so
    // accepting it raced the JIT-provisioning call and made the first (coldest) user look like a
    // failure. Wait for the route the SPA actually navigates to once the session exists.
    await page.waitForURL((u) => u.origin === BASE && !u.pathname.startsWith('/kc/')
      && !u.pathname.startsWith('/auth/') && u.pathname !== '/login', { timeout: 60_000 });
    await page.waitForFunction(() => !document.querySelector('.login-cta'), null, { timeout: 45_000 });
    ok(`authenticated session established (landed on ${new URL(page.url()).pathname})`);

    // Give the SPA's provisioning call a moment if it has not already landed.
    if (!profile) await page.waitForTimeout(3_000);

    if (!profile) {
      bad('no successful POST /api/members/me — JIT provisioning did not happen');
    } else if (profile.role !== expectedRole) {
      bad(`JIT-provisioned as "${profile.role}", expected "${expectedRole}"`);
    } else {
      ok(`JIT-provisioned as ${profile.role} (publicId ${profile.publicId})`);
    }

    // DEF-023's second, unassessed observation: our nginx sets the CSP at SERVER level, so it also
    // applies to Keycloak's own login theme under /kc/. Report rather than assert — the login above
    // either worked or it did not, and that is the verdict. This tells us whether it is still noisy.
    // Silent renew goes through a hidden SAME-ORIGIN iframe pointed at /kc/ (automaticSilentRenew,
    // no silent_redirect_uri). Our nginx used to add X-Frame-Options: DENY there on top of
    // Keycloak's SAMEORIGIN, which resolves to deny — so the renew would fail and the user would be
    // logged out at token expiry instead of renewed. Drive the iframe rather than reason about it.
    const framed = await page.evaluate(async (base) => {
      const f = document.createElement('iframe');
      f.style.display = 'none';
      f.src = `${base}/kc/realms/acmp/protocol/openid-connect/login-status-iframe.html`;
      const done = new Promise((res) => {
        f.onload = () => { try { res(!!f.contentDocument?.body); } catch { res(false); } };
        f.onerror = () => res(false);
        setTimeout(() => res(false), 10_000);
      });
      document.body.appendChild(f);
      return done;
    }, BASE);
    framed ? ok('the /kc/ silent-renew iframe loads (X-Frame-Options permits same-origin framing)')
           : bad('the /kc/ iframe is BLOCKED — automaticSilentRenew cannot work; sessions will drop at token expiry');

    const csp = consoleErrors.filter((e) => /content security policy/i.test(e));
    console.log(csp.length ? `  \x1b[33mNOTE\x1b[0m ${csp.length} CSP console error(s): ${csp[0].slice(0, 160)}`
                           : '  \x1b[32mPASS\x1b[0m no CSP violations reported during the login round-trip');
  } catch (err) {
    bad(`${username}: ${err.message.split('\n')[0]}`);
    await page.screenshot({ path: `probe-${username}-failure.png` }).catch(() => {});
    console.log(`       url at failure: ${page.url()}`);
  } finally {
    await ctx.close();
  }
}

const browser = await chromium.launch();
console.log(`\x1b[1m[probe] target ${BASE}\x1b[0m`);
for (const [user, role] of Object.entries(EXPECTED)) await probe(browser, user, role);
await browser.close();

console.log(`\n\x1b[1m[probe] RESULT: ${failures === 0 ? 'all users logged in' : `${failures} failure(s)`}\x1b[0m`);
process.exit(failures === 0 ? 0 : 1);
