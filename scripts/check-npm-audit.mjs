#!/usr/bin/env node
// Frontend dependency CVE gate (mirror of check-vulns.mjs for npm). Fails CI on any HIGH or
// CRITICAL npm advisory EXCEPT those in ALLOW below — each allow entry is a specific advisory
// triaged as not-applicable to ACMP, with a reason and a date. Replaces the bare
// `npm audit --audit-level=high`, which has no triage mechanism, so a single unfixable-yet-
// not-applicable transitive CVE could otherwise only be cleared by weakening the gate wholesale.
//
// Run from src/Acmp.Web (the CI `working-directory`): node ../../scripts/check-npm-audit.mjs
import { execFileSync } from 'node:child_process';

const BLOCK = new Set(['high', 'critical']);

// advisory GHSA id -> why it does not apply to ACMP. Keep this SMALL and justified; prefer a real
// dependency fix whenever one exists. Revisit when react-router-dom ships a patched line.
const ALLOW = new Map([
  ['GHSA-qwww-vcr4-c8h2',
    'React Router "RSC Mode CSRF" — ACMP is a client-rendered Vite SPA with NO React Server ' +
    'Components (see src/auth/authConfig.ts: "no SSR here"), so the RSC request path this advisory ' +
    'concerns does not exist. No react-router-dom release depends on the patched react-router@8.3.0 ' +
    '(dom is still 7.x), so there is no clean forward fix. Triaged 2026-08-02 (PH-5).'],
]);

const NPM = process.platform === 'win32' ? 'npm.cmd' : 'npm';   // npm is a .cmd shim on Windows
let raw;
try {
  // --json is the stable machine format. npm audit exits non-zero when it finds anything, so we
  // must not let execFileSync throw on that — capture stdout regardless (see catch).
  // shell only on Windows, where npm is a .cmd shim that Node refuses to spawn shell-less. The
  // argv is a fixed constant (no interpolation), so there is no injection surface either way.
  raw = execFileSync(NPM, ['audit', '--json'],
    { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, shell: process.platform === 'win32' });
} catch (err) {
  // Non-zero exit is EXPECTED when advisories exist; the JSON is still on stdout.
  raw = err.stdout?.toString() ?? '';
  if (!raw) { console.error('[check-npm-audit] `npm audit --json` produced no output:', err.message); process.exit(2); }
}

let data;
try { data = JSON.parse(raw); }
catch (e) { console.error('[check-npm-audit] could not parse npm audit JSON:', e.message); process.exit(2); }

const ghsaOf = (via) => (typeof via === 'object' && via?.url) ? (via.url.match(/GHSA-[0-9a-z-]+/i)?.[0] ?? null) : null;

const blocking = new Set();
const usedAllows = new Set();
for (const [name, v] of Object.entries(data.vulnerabilities ?? {})) {
  if (!BLOCK.has(v.severity)) continue;
  // Collect the concrete advisory ids behind this (possibly transitive) finding.
  const ids = (v.via ?? []).map(ghsaOf).filter(Boolean);
  if (ids.length === 0) {
    // A purely transitive node whose `via` is another package name — allowed only if EVERY
    // upstream advisory is allowed. We approximate by deferring to the named advisories above;
    // if none resolve here, treat the package as covered iff it has no direct advisory id.
    continue;
  }
  for (const id of ids) {
    if (ALLOW.has(id)) { usedAllows.add(id); continue; }
    blocking.add(`${name} [${v.severity}] ${id}`);
  }
}

if (blocking.size > 0) {
  console.error(`[check-npm-audit] ${blocking.size} un-allowlisted High/Critical npm advisor${blocking.size === 1 ? 'y' : 'ies'} — blocking:`);
  for (const line of [...blocking].sort()) console.error('  - ' + line);
  console.error('Fix: upgrade the dependency, or (only if genuinely not applicable) add a justified entry to scripts/check-npm-audit.mjs ALLOW.');
  process.exit(1);
}

// Stale-allowlist hygiene: if an allowlisted advisory no longer appears, say so (do not fail) so
// the exception can be removed once a real fix lands.
for (const id of ALLOW.keys())
  if (!usedAllows.has(id)) console.warn(`[check-npm-audit] NOTE: allowlisted ${id} no longer present — consider removing its exception.`);

const n = usedAllows.size;
console.log(`[check-npm-audit] no un-allowlisted High/Critical npm advisories${n ? ` (${n} triaged-not-applicable exception${n === 1 ? '' : 's'} in effect)` : ''}.`);
