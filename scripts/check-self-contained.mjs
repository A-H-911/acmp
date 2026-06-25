#!/usr/bin/env node
// Self-contained lint (CON-001, tightened by ADR-0015): the docker-compose stack must reference
// NO external runtime hostname in v1. The only allowed external host is Webex (Phase 2 SaaS adapter).
// Everything else a service talks to must be an in-stack service. Fails CI on any stray external host.
//
// It scans runtime config values (URLs, JDBC URLs, SQL `Server=`) for hostnames and checks each
// against: the compose file's own service names + loopback + the single allowed external (Webex).
// `image:` lines are ignored — container registries are a build-time pull, not a runtime dependency.

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const composePath = join(root, 'deploy', 'docker-compose.yml');
const text = readFileSync(composePath, 'utf8');
const lines = text.split(/\r?\n/);

// Service names = allowed internal hosts (declared under top-level `services:`).
const services = new Set();
let inServices = false;
for (const line of lines) {
  if (/^[a-z]/i.test(line)) inServices = line.startsWith('services:');
  else if (inServices) {
    const m = line.match(/^ {2}([a-z0-9][a-z0-9_-]*):\s*$/i);
    if (m) services.add(m[1].toLowerCase());
  }
}

const LOOPBACK = new Set(['localhost', '127.0.0.1', 'host-gateway', 'host.docker.internal']);
const isAllowed = (host) => {
  const h = host.toLowerCase();
  if (services.has(h)) return true;            // in-stack service
  if (LOOPBACK.has(h)) return true;            // loopback / docker host
  if (h.endsWith('.localhost')) return true;   // browser loopback alias (issuer host)
  if (h === 'webex.com' || h.endsWith('.webex.com')) return true; // ONLY allowed external (Phase 2)
  return false;
};

// Extract hostnames from scheme URLs, JDBC URLs, and SQL `Server=` values, skipping image refs.
const hostPatterns = [
  /(?:https?|jdbc:[a-z0-9]+):\/\/([a-z0-9.\-]+)/gi,
  /Server=([a-z0-9.\-]+)/gi,
];

const violations = [];
lines.forEach((line, i) => {
  if (/^\s*image:/.test(line) || /^\s*#/.test(line)) return; // build-time / comment
  for (const re of hostPatterns) {
    for (const m of line.matchAll(re)) {
      const host = m[1];
      if (!isAllowed(host)) violations.push({ line: i + 1, host, text: line.trim() });
    }
  }
});

if (violations.length > 0) {
  console.error('✖ Self-contained lint failed (ADR-0015 / CON-001): external runtime host(s) found.');
  console.error('  Allowed: in-stack services, loopback/*.localhost, and *.webex.com (Phase 2) only.\n');
  for (const v of violations) console.error(`  deploy/docker-compose.yml:${v.line}  →  ${v.host}\n    ${v.text}`);
  process.exit(1);
}

console.log(`✔ Self-contained: ${services.size} in-stack services, no external runtime hosts (Webex Phase 2 only).`);
