#!/usr/bin/env node
// Guard: the bundled Keycloak realm export must expose committee roles to the SPA. The React app reads
// roles from the ID token (oidc-client-ts `user.profile`), so the realm-role + group mappers MUST emit
// to the ID token (`id.token.claim=true`). If they don't, role-based UI (nav gating, the profile menu's
// role label) silently degrades — a bug we already hit. This is the regression guard (ADR-0015).

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const realm = JSON.parse(readFileSync(join(root, 'deploy', 'keycloak', 'realm-export.json'), 'utf8'));

const errors = [];
if (realm.realm !== 'acmp') errors.push(`realm name is "${realm.realm}", expected "acmp"`);

const CANONICAL = ['Chairman', 'Secretary', 'Member', 'Reviewer', 'Auditor', 'Administrator', 'Submitter', 'Guest'];
const roleNames = (realm.roles?.realm ?? []).map((r) => r.name);
for (const r of CANONICAL) if (!roleNames.includes(r)) errors.push(`missing realm role: ${r}`);

const client = (realm.clients ?? []).find((c) => c.clientId === 'acmp-web');
if (!client) errors.push('client "acmp-web" not found');
const mappers = client?.protocolMappers ?? [];
for (const name of ['realm-roles', 'groups']) {
  const m = mappers.find((x) => x.name === name);
  if (!m) { errors.push(`protocol mapper "${name}" missing`); continue; }
  if (m.config?.['id.token.claim'] !== 'true')
    errors.push(`mapper "${name}" must set id.token.claim=true (the SPA reads roles from the ID token)`);
}

// ---------------------------------------------------------------------------------------------
// DEF-048 — the ADR-0038 service account's grant.
//
// Until now nothing mechanical read it at all, so "minimum realm-management scope, never
// realm-admin" was a rule stated in an ADR and enforced by nobody. The likeliest failure is a 403
// on the role-mapping path, and the obvious fix for a 403 is to widen the grant — which would have
// shipped green through every gate in this repository.
//
// The grant lives in deploy/keycloak/admin-client.env because BOTH this check and the reconciler
// that applies it must read the same value; a check that read a different file would pass while
// the deployed grant drifted. Parsed as KEY=value, never by searching prose.
const adminEnvPath = join(root, 'deploy', 'keycloak', 'admin-client.env');
const adminEnv = Object.fromEntries(
  readFileSync(adminEnvPath, 'utf8')
    .split(/\r?\n/)
    .filter((l) => /^[A-Z_]+=/.test(l))
    .map((l) => {
      const i = l.indexOf('=');
      return [l.slice(0, i), l.slice(i + 1).trim().replace(/^"(.*)"$/, '$1')];
    }),
);

const PROVEN_GRANT = ['manage-users']; // OQ-070, proven on UAT 2026-08-12 — see admin-client.env.
const granted = (adminEnv.ACMP_KC_ADMIN_ROLES ?? '').split(/\s+/).filter(Boolean);
const forbidden = (adminEnv.ACMP_KC_ADMIN_FORBIDDEN_ROLES ?? '').split(/\s+/).filter(Boolean);

if (!adminEnv.ACMP_KC_ADMIN_CLIENT_ID) errors.push('admin-client.env: ACMP_KC_ADMIN_CLIENT_ID is not set');

// EXACT set, not a superset. A superset check is what lets a grant grow one role at a time.
const extra = granted.filter((r) => !PROVEN_GRANT.includes(r));
const missing = PROVEN_GRANT.filter((r) => !granted.includes(r));
if (extra.length > 0)
  errors.push(
    `admin-client.env grants ${extra.map((r) => `"${r}"`).join(', ')} beyond the proven minimum [${PROVEN_GRANT.join(', ')}]. ` +
      'Re-run scripts/probe-keycloak-grant.mjs and show which call is refused without it (OQ-070) — a refusal is a signal to read, not to widen.',
  );
if (missing.length > 0) errors.push(`admin-client.env is missing the proven role(s): ${missing.join(', ')}`);

// Assert ZERO occurrences of the forbidden role, never a count (baselines-as-numbers).
for (const bad of ['realm-admin', 'admin']) {
  if (granted.includes(bad)) errors.push(`admin-client.env grants "${bad}" — ADR-0038 forbids it outright`);
}
if (!forbidden.includes('realm-admin'))
  errors.push('admin-client.env: ACMP_KC_ADMIN_FORBIDDEN_ROLES must name realm-admin, so the reconciler revokes it if it ever appears');

if (errors.length > 0) {
  console.error('✖ realm-export check failed:');
  for (const e of errors) console.error('  - ' + e);
  process.exit(1);
}
console.log(`✔ realm-export: acmp realm, ${CANONICAL.length} roles, role + group mappers emit to the ID token.`);
console.log(`✔ admin service account "${adminEnv.ACMP_KC_ADMIN_CLIENT_ID}": grant is exactly [${granted.join(', ')}], realm-admin forbidden.`);
