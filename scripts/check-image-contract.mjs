#!/usr/bin/env node
/*
 * Image contract gate — WBS-26.3 (DW-090 / NFR-054).  Usage:
 *   node scripts/check-image-contract.mjs --project <compose-project-name>
 *
 * WHY THIS EXISTS.  NFR-054 has TWO clauses and its own recorded verification method names a CI check for
 * both: "CI: docker image inspect after build; assert size <= 500 MB; base image layer verified".  Measured
 * across all three workflow files and every script in scripts/ (2026-08-29, re-verified 2026-08-31):
 * NOTHING asserted image size and NOTHING asserted the base layer.  So the requirement's substance has been
 * satisfied since DW-080 phase B while the requirement itself could not record Met.
 *
 * THE BASE HALF IS THE HALF THAT MATTERS AND IS THE EASY ONE TO OMIT.  A size check ALONE would have passed
 * on the old Debian base at 326 MB — reporting compliance with a MINIMAL-BASE clause while sitting on the
 * base that clause excludes.  Worse, DEC-090's spike measured that plain chiseled and plain alpine ship no
 * ICU, and that a .NET app on them starts, exits 0, and throws only when a non-invariant culture is TOUCHED
 * — which this API never does.  A digest bump that silently dropped the -extra suffix would therefore render
 * a Hijri date instead of a Gregorian one in Arabic, with NO exception and NO log line, and would pass every
 * other gate we have.  This is the only mechanical guard against that.
 *
 * TWO ASSERTIONS, AND NEITHER ALONE IS SUFFICIENT — this is the part to understand before editing.
 *   (A) BASE REFERENCE.  The Dockerfile's FROM repo:tag for the stage must equal the committed expectation
 *       in EXPECTED below.  Catches -extra being dropped, or a swap back to Debian.
 *   (B) LAYER PREFIX.  The PULLED base image's RootFS.Layers must be a PREFIX of the BUILT image's.
 *       Catches an artifact that did not actually come from the base its Dockerfile names — a stale or
 *       leftover image inspected by mistake, a wrong --target, an image built elsewhere.
 * (B) alone CANNOT see -extra disappearing: change the FROM to plain chiseled and (B) happily pulls plain
 * chiseled, whose layers are then a perfect prefix.  It passes, and the exact failure this row was filed
 * about ships.  (A) alone is circular — DEC-102 d3 declined it as a WHOLE check, because it verifies the
 * SOURCE that built the image rather than the image, and would pass even if the published artifact came
 * from somewhere else.  Together they are neither circular nor blind.  Keep both.
 *
 * WHY THE EXPECTATION IS COMMITTED HERE AND NOT DERIVED FROM THE DOCKERFILE.  Deriving it would make (A) a
 * tautology — the file compared against itself.  The cost is deliberate: changing a base repo:tag requires
 * editing this table, which is a human deciding to change bases rather than a diff sliding past review.  A
 * DIGEST bump within the same tag needs no edit (Dependabot stays unblocked), because (A) compares repo:tag
 * and (B) pulls whatever digest the Dockerfile pins.
 *
 * FAILS CLOSED (trap 31).  Fewer services checked than expected is an ERROR, not a quiet pass; a missing
 * image is an ERROR, never a skip; a stage whose FROM cannot be parsed is an ERROR.  A gate that inspects
 * the wrong artifact — or nothing at all — is worse than no gate, because it is credited.  LL-013: a clean
 * result from a scanner that never had a subject proves nothing, so this one reports its subject count.
 */
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');

// NFR-054's cap. DECIMAL megabytes, deliberately: it is the stricter reading of "500 MB" (500 MiB would
// allow ~24 MB more), and a compliance cap should never be the looser interpretation of its own words.
const MAX_BYTES = 500 * 1000 * 1000;

// NFR-054 names ACMP's own application images: api, worker AND web.  DW-090's original sketch said "api and
// worker"; DEC-102 d1 corrected it — a check covering two of the three would evidence two-thirds of the
// requirement it exists to make evidenceable.  The database image is excluded from the SIZE cap by NFR-054
// itself (mssql/server exceeds it before ACMP adds the FTS package AC-061 requires) and is not built here.
const EXPECTED = [
  {
    service: 'api',
    dockerfile: 'deploy/Dockerfile.backend',
    stage: 'api',
    base: 'mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra',
  },
  {
    service: 'worker',
    dockerfile: 'deploy/Dockerfile.backend',
    stage: 'worker',
    base: 'mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra',
  },
  {
    service: 'web',
    dockerfile: 'deploy/Dockerfile.web',
    stage: 'final',
    base: 'nginxinc/nginx-unprivileged:1.31-alpine',
  },
];

const args = process.argv.slice(2);
const projectIdx = args.indexOf('--project');
const project = projectIdx >= 0 ? args[projectIdx + 1] : undefined;
if (!project) {
  console.error('usage: node scripts/check-image-contract.mjs --project <compose-project-name>');
  process.exit(2);
}

const docker = (...a) => execFileSync('docker', a, { encoding: 'utf8' }).trim();
const mb = (bytes) => (bytes / 1_000_000).toFixed(1);

/** The FROM ref for one build stage, as WRITTEN — used for (A) and to pull for (B). */
function fromRefForStage(dockerfile, stage) {
  const text = readFileSync(join(ROOT, dockerfile), 'utf8');
  const re = new RegExp(`^FROM\\s+(\\S+)\\s+AS\\s+${stage}\\s*$`, 'im');
  const m = text.match(re);
  if (!m) throw new Error(`${dockerfile}: no "FROM ... AS ${stage}" line found — cannot verify its base`);
  return m[1];
}

const failures = [];
let checked = 0;

for (const { service, dockerfile, stage, base } of EXPECTED) {
  const image = `${project}-${service}`;
  const label = `${service} (${image})`;

  let built;
  try {
    built = JSON.parse(docker('image', 'inspect', image));
  } catch {
    // Never a skip.  An absent image means the build step did not produce what this gate names, and a gate
    // that shrugs at that reports compliance it never measured.
    failures.push(
      `${label}: image not found — the build step did not produce it, or the compose project name is wrong`,
    );
    continue;
  }
  checked += 1;

  const size = built[0].Size;
  if (size > MAX_BYTES) failures.push(`${label}: ${mb(size)} MB exceeds the ${mb(MAX_BYTES)} MB cap (NFR-054)`);
  else console.log(`  ${label}: ${mb(size)} MB <= ${mb(MAX_BYTES)} MB`);

  // (A) base REFERENCE — the repo:tag the Dockerfile names must be the one we expect.
  const ref = fromRefForStage(dockerfile, stage);
  const refNoDigest = ref.split('@')[0];
  if (refNoDigest !== base) {
    failures.push(
      `${label}: ${dockerfile} stage "${stage}" builds FROM "${refNoDigest}", expected "${base}". If this ` +
        `base change is intended, update EXPECTED in this script deliberately — the -extra suffix is ` +
        `load-bearing (DEC-090: a base without it ships no ICU, starts fine, and renders the wrong Arabic calendar).`,
    );
    continue; // (B) against an unexpected base would only confirm the wrong thing.
  }

  // (B) layer PREFIX — the built artifact must actually sit on that base, not merely claim to.
  docker('pull', '--quiet', ref);
  const baseLayers = JSON.parse(docker('image', 'inspect', ref))[0].RootFS.Layers;
  const builtLayers = built[0].RootFS.Layers;
  const isPrefix = baseLayers.length > 0 && baseLayers.every((l, i) => builtLayers[i] === l);
  if (!isPrefix) {
    failures.push(
      `${label}: the built image's layers do not start with "${ref}"'s (${baseLayers.length} base layers vs ` +
        `${builtLayers.length} built) — it was not built from that base`,
    );
  } else {
    console.log(
      `  ${label}: base "${refNoDigest}" verified — ${baseLayers.length} of ${builtLayers.length} layers match`,
    );
  }
}

// Fails closed.  A pass over fewer subjects than expected is the shape of a green control that never looked.
if (checked !== EXPECTED.length) {
  failures.push(`inspected ${checked} of ${EXPECTED.length} expected images — refusing to pass on a partial set`);
}

if (failures.length) {
  console.error(`\n[check-image-contract] FAILED (${failures.length}):`);
  for (const f of failures) console.error(`  - ${f}`);
  process.exit(1);
}
console.log(
  `[check-image-contract] ${checked} image(s) within ${mb(MAX_BYTES)} MB and on their expected base (NFR-054)`,
);
