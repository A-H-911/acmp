#!/usr/bin/env node
// Self-test for check-fts-pin.mjs: each exit code is reached for the reason it names, on throwaway trees.
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const SCRIPT = new URL('./check-fts-pin.mjs', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
const DIGEST = `sha256:${'a'.repeat(64)}`;
const DOCKERFILE = 'FROM mcr.microsoft.com/mssql/server:2022\n';
const sha = (s) => createHash('sha256').update(s).digest('hex');

function run(pin, dockerfile = DOCKERFILE) {
  const root = mkdtempSync(join(tmpdir(), 'fts-pin-'));
  mkdirSync(join(root, 'deploy'));
  if (pin !== undefined) writeFileSync(join(root, 'deploy/fts-test-image.json'), typeof pin === 'string' ? pin : JSON.stringify(pin));
  writeFileSync(join(root, 'deploy/Dockerfile.sqlserver'), dockerfile);
  const r = spawnSync(process.execPath, [SCRIPT, '--root', root], { encoding: 'utf8' });
  rmSync(root, { recursive: true, force: true });
  return r;
}

const image = `ghcr.io/a-h-911/acmp-sqlserver-fts-private@${DIGEST}`;
const cases = [
  ['match prints the ref', run({ dockerfile_sha256: sha(DOCKERFILE), image }), 0, (r) => r.stdout.trim() === image],
  ['changed Dockerfile names the per-bump step', run({ dockerfile_sha256: sha(DOCKERFILE), image }, DOCKERFILE + 'RUN true\n'), 1,
    (r) => r.stdout === '' && r.stderr.includes('publish-fts-test-image.yml')],
  ['a tag instead of a digest is malformed', run({ dockerfile_sha256: sha(DOCKERFILE), image: 'ghcr.io/a-h-911/x:latest' }), 2, (r) => r.stdout === ''],
  ['a short hash is malformed', run({ dockerfile_sha256: 'abc', image }), 2, (r) => r.stdout === ''],
  ['missing pin is malformed, not a mismatch', run(undefined), 2, (r) => r.stdout === ''],
  ['invalid JSON is malformed', run('{ nope'), 2, (r) => r.stdout === ''],
];

let failed = 0;
for (const [name, r, code, ok] of cases) {
  const pass = r.status === code && ok(r);
  if (!pass) failed++;
  console.log(`${pass ? 'ok  ' : 'FAIL'} ${name} (exit ${r.status}, expected ${code})${pass ? '' : `\n${r.stderr}`}`);
}
console.log(`${cases.length - failed}/${cases.length} passed`);
process.exit(failed ? 1 : 0);
