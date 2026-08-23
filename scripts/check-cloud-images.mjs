#!/usr/bin/env node
// DEF-019 guard: the cloud compose file must only reference image tags that CI actually publishes.
//
// P24's publish job and P21's cloud compose were written independently and disagreed. api, worker
// and sqlserver-fts are environment-AGNOSTIC and published as a plain <sha>, promoted between
// environments by digest. `web` is NOT: ADR-0037 bakes VITE_OIDC_AUTHORITY into the bundle at build
// time, so CI publishes web:<sha>-uat and web:<sha>-prod and there is NO plain web:<sha>. The
// compose file nonetheless used one shared ${ACMP_IMAGE_TAG} for all four, so a deploy pinned to a
// commit would pull three images and then fail on web with ImageNotFoundException — on the box,
// mid-deploy. Verified against the live registry before the fix.
//
// This pins BOTH halves of that contract so they cannot drift apart again silently:
//   1. compose: web reads ACMP_WEB_TAG; the environment-agnostic three read ACMP_IMAGE_TAG.
//   2. ci.yml:  web is pushed with -uat AND -prod suffixes; the other three are pushed unsuffixed.

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const compose = readFileSync(join(root, 'deploy', 'docker-compose.cloud.yml'), 'utf8');
const ci = readFileSync(join(root, '.github', 'workflows', 'ci.yml'), 'utf8');

// Which tag variable each ACMP image must read. `web` is deliberately the odd one out.
const PER_ENV = 'ACMP_WEB_TAG';
const SHARED = 'ACMP_IMAGE_TAG';
const expected = { web: PER_ENV, api: SHARED, worker: SHARED, 'sqlserver-fts': SHARED };

const errors = [];

// --- 1. compose side -----------------------------------------------------------------------
// Matches e.g. `image: ${ACMP_REGISTRY:-acmp}/web:${ACMP_WEB_TAG:-${ACMP_IMAGE_TAG:-latest}}`.
// Only the FIRST ${...} in the tag position matters — that is the variable an operator sets.
const seen = new Set();
for (const line of compose.split(/\r?\n/)) {
  const m = line.match(/^\s*image:\s*\$\{ACMP_REGISTRY[^}]*\}\/([a-z-]+):\$\{([A-Z_]+)/);
  if (!m) continue;
  const [, image, variable] = m;
  seen.add(image);
  const want = expected[image];
  if (!want) { errors.push(`unknown ACMP image "${image}" in cloud compose — add it to this check`); continue; }
  if (variable !== want) {
    errors.push(
      `cloud compose: ${image} reads \${${variable}} but must read \${${want}}.\n` +
      (want === PER_ENV
        ? `    web is published per environment (<sha>-uat / <sha>-prod, ADR-0037); a plain <sha> tag does not exist.`
        : `    ${image} is environment-agnostic and published as a plain <sha>.`));
  }
}
for (const image of Object.keys(expected)) {
  if (!seen.has(image)) errors.push(`cloud compose: no image line found for "${image}"`);
}

// --- 2. publish side -----------------------------------------------------------------------
// The compose expectations above are only correct while CI still tags this way.
const publish = ci.slice(ci.indexOf('\n  publish:'));
const pushes = [...publish.matchAll(/\/acmp\/([a-z-]+):\$(?:SHA|\{[^}]*\})([^"\s]*)/g)]
  .reduce((acc, [, image, suffix]) => ((acc[image] ??= new Set()).add(suffix), acc), {});

for (const [image, want] of Object.entries(expected)) {
  const suffixes = pushes[image];
  if (!suffixes) { errors.push(`ci.yml publish job pushes no "${image}" image`); continue; }
  const list = [...suffixes].sort().join(', ') || '(none)';
  if (want === PER_ENV) {
    for (const s of ['-uat', '-prod'])
      if (!suffixes.has(s)) errors.push(`ci.yml: ${image} is not published with a "${s}" tag (got: ${list})`);
  } else if (!suffixes.has('')) {
    errors.push(`ci.yml: ${image} must be published with an unsuffixed <sha> tag (got: ${list})`);
  }
}

if (errors.length) {
  console.error('check-cloud-images: the cloud compose and the CI publish job disagree (DEF-019).\n');
  for (const e of errors) console.error(`  - ${e}`);
  console.error('\nA deploy pinned to a commit would fail pulling an image that was never published.');
  process.exit(1);
}
console.log(`check-cloud-images: OK — ${Object.keys(expected).length} images, compose and publish agree.`);
