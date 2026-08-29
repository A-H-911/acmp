#!/usr/bin/env node
/*
 * Asserts that every repository path cited by the ASVS L2 evidence pack still exists.
 * Usage:  node scripts/check-asvs-pack-paths.mjs
 *
 * WHY THIS EXISTS. The pack (tamheed-package/docs/asvs-l2-evidence-pack.md, WBS-25.2 / DW-079) is
 * handed to an EXTERNAL ASSESSOR as a map of where ACMP's controls live. A path that has moved is
 * expensive in a way an internal doc's dead link is not: the assessor bills for the search, and a
 * confidently-wrong anchor reads exactly like a correct one. Drafting the pack, one of ten paths
 * written from memory was wrong (useIdleSignOut.ts is under src/auth/, not src/hooks/), which is the
 * whole argument for checking mechanically rather than carefully.
 *
 * WHAT IT DOES NOT DO. It asserts EXISTENCE, never sufficiency. Whether the code at a path implements
 * the control it is cited for is the assessment itself, and no script can stand in for that. The pack
 * says so in its own words; this checker is deliberately the weaker, decidable half.
 *
 * ⚠ IT FAILS CLOSED ON AN EMPTY SUBJECT (trap 31, DEF-078's shape). A checker that finds no paths and
 * exits 0 reports a clean tree over nothing at all — the same failure as a healthcheck that evaluates
 * zero checks. Below MIN_PATHS it exits non-zero and says why.
 *
 * ⚠ PATHS ARE RESOLVED FROM THIS FILE'S OWN LOCATION, never from cwd (trap 29): CI runs scripts from
 * more than one working directory, and a cwd-relative scanner can silently resolve to an empty set.
 */
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const PACK = join(ROOT, 'tamheed-package', 'docs', 'asvs-l2-evidence-pack.md');

/* Fewer than this many citations means the pack was gutted, renamed, or the extraction broke —
   all of which must be loud. The pack cites well over twenty; twelve is a floor, not a target. */
const MIN_PATHS = 12;

if (!existsSync(PACK)) {
  console.error(`FATAL: the evidence pack is missing at ${PACK}`);
  process.exit(2);
}

const text = readFileSync(PACK, 'utf8');

/* Only inline-code spans are candidates, so prose never enters the set. A citation is anything that
   looks like a repo path: a known top-level directory, or a dotfile/workflow at the root. */
const spans = [...text.matchAll(/`([^`\n]+)`/g)].map((m) => m[1].trim());
// A glob is excluded deliberately and narrowly: the pack legitimately writes a wildcard C# path when
// describing the SCOPE OF A SWEEP, which is prose about a set, not a citation of a file. Only the
// wildcard earns the exemption — a literal path that has moved must still be caught.
// (Written as line comments on purpose: a glob's closing wildcard-slash ends a block comment early.)
const looksLikePath = (s) =>
  !s.includes('*') &&
  (/^(src|tests|scripts|deploy|docs|tamheed-package|e2e)\//.test(s) ||
    /^\.(github\/|gitleaks)/.test(s));

const cited = [...new Set(spans.filter(looksLikePath))].sort();

if (cited.length < MIN_PATHS) {
  console.error(
    `FATAL: only ${cited.length} path citation(s) found in the pack, below the floor of ${MIN_PATHS}. ` +
      `A clean result over an implausibly small set is not a clean result — refusing to report success.`
  );
  process.exit(2);
}

const missing = cited.filter((p) => !existsSync(join(ROOT, p)));

for (const p of cited) console.log(`${missing.includes(p) ? 'MISS' : 'ok  '}  ${p}`);
console.log('');

if (missing.length) {
  console.error(
    `FAIL: ${missing.length} of ${cited.length} cited path(s) do not exist. An assessor reading this ` +
      `pack would be sent to a file that is not there.`
  );
  process.exit(1);
}

console.log(`check-asvs-pack-paths: OK — ${cited.length} cited paths, all present.`);
