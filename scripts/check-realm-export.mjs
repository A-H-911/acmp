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

if (errors.length > 0) {
  console.error('✖ realm-export check failed:');
  for (const e of errors) console.error('  - ' + e);
  process.exit(1);
}
console.log(`✔ realm-export: acmp realm, ${CANONICAL.length} roles, role + group mappers emit to the ID token.`);
