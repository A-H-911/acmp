#!/usr/bin/env node
/*
 * FTS test-image pin check - WBS-41.2 (DEC-182 f2/f4, ADR-0047 e2).  Usage:
 *   node scripts/check-fts-pin.mjs [--root <repo root>]
 *
 * deploy/fts-test-image.json pins the pre-built SQL Server FTS image the tests pull, together with the sha256
 * of the deploy/Dockerfile.sqlserver it was built from (both printed by publish-fts-test-image.yml's summary).
 * A consumer may pull the pinned image ONLY while the Dockerfile still hashes to that value; otherwise the
 * image no longer is that file, and pulling it would test something other than what the branch ships.
 *
 *   exit 0  the Dockerfile matches the pin; the image reference is printed on stdout (for $(...) in a workflow)
 *   exit 1  the Dockerfile changed since the image was published; the per-bump step is printed on stderr
 *   exit 2  the pin is missing or malformed - never read as a match, never read as a mismatch
 *
 * The image must be pinned by DIGEST: a tag can be moved, a digest cannot, and the pin is only a pin if it
 * names bytes. tests/Acmp.Integration.Tests/FtsImage.cs makes the same comparison in C#, so the test suite
 * does not depend on node. Self-test: scripts/test-check-fts-pin.mjs.
 */
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const args = process.argv.slice(2);
const root = args[0] === '--root' ? args[1] : join(dirname(fileURLToPath(import.meta.url)), '..');
const PIN = 'deploy/fts-test-image.json';
const DOCKERFILE = 'deploy/Dockerfile.sqlserver';

const fail = (code, msg) => { process.stderr.write(`check-fts-pin: ${msg}\n`); process.exit(code); };

let pin;
try { pin = JSON.parse(readFileSync(join(root, PIN), 'utf8')); } catch (e) { fail(2, `cannot read ${PIN}: ${e.message}`); }
if (!/^[0-9a-f]{64}$/.test(pin?.dockerfile_sha256 ?? '')) fail(2, `${PIN}: dockerfile_sha256 must be 64 lowercase hex characters`);
if (!/^ghcr\.io\/[a-z0-9-]+\/[a-z0-9._-]+@sha256:[0-9a-f]{64}$/.test(pin?.image ?? '')) {
  fail(2, `${PIN}: image must be ghcr.io/<owner>/<name>@sha256:<digest> - pinned by digest, not by tag`);
}

let actual;
try { actual = createHash('sha256').update(readFileSync(join(root, DOCKERFILE))).digest('hex'); } catch (e) { fail(2, `cannot read ${DOCKERFILE}: ${e.message}`); }

if (actual !== pin.dockerfile_sha256) {
  fail(1, [
    `${DOCKERFILE} changed since the pinned FTS test image was published (sha256 ${actual}, pin ${pin.dockerfile_sha256}).`,
    `The per-bump step (DEC-182 f4): dispatch the publish workflow on THIS branch, then commit the pin it prints to ${PIN}:`,
    '  gh workflow run publish-fts-test-image.yml --ref <this branch>',
    'A deliberate candidate build (integration-on-candidate.yml) sets ACMP_FTS_BUILD=1 instead of pulling.',
  ].join('\n'));
}

process.stderr.write(`check-fts-pin: ${DOCKERFILE} matches the pin; image ${pin.image}\n`);
process.stdout.write(`${pin.image}\n`);
