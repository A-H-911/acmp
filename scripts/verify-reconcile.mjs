#!/usr/bin/env node
/*
 * DEF-065 — verify that the reconciliation actually did what its response claims.
 *
 * WHY THIS EXISTS AND WHY IT RE-READS THE DATABASE. The whole DEF-065 lesson is that a control
 * which reports success without being checked is how the outage arrives: the step-5 backfill
 * "succeeded" against ~1 row and would have looked healthy while 25 people were locked out. So this
 * does NOT trust the API response — it re-derives every claim from committee_members and
 * member_streams, which is the same discipline findings_18 §1 recorded after a hand-typed repair
 * corrupted a row that a generator had produced correctly.
 *
 * Two checks, and the second is the one with teeth:
 *   1. the response is a PARTITION — the buckets sum to identityAccounts. A bare `created` count
 *      cannot be told apart from a run that silently skipped half the realm.
 *   2. every member row holds at least one stream. That is the property the whole feature exists
 *      for; a row created without one is refused every stream-scoped write from its first login.
 *
 * Usage:
 *   node scripts/verify-reconcile.mjs --response reconcile-response.json --connection "<conn str>"
 *   node scripts/verify-reconcile.mjs --response reconcile-response.json        # arithmetic only
 *
 * Exit 0 = verified. Exit 1 = something to read before closing DEF-065.
 */

import { readFileSync } from 'node:fs';

const args = Object.fromEntries(
  process.argv.slice(2).flatMap((a, i, all) => (a.startsWith('--') ? [[a.slice(2), all[i + 1]]] : [])),
);

if (!args.response) {
  console.error('verify-reconcile: --response <file.json> is required (the POST /api/members/reconcile body).');
  process.exit(2);
}

const BUCKETS = [
  'created',
  'alreadyProvisioned',
  'skippedDisabled',
  'skippedNoCommitteeRole',
  'skippedDuplicateEmail',
];

const problems = [];
const notes = [];

// ---- check 1: the response is a partition -------------------------------------------------- //

const body = JSON.parse(readFileSync(args.response, 'utf8'));
const missing = BUCKETS.filter((b) => typeof body[b] !== 'number');
if (missing.length || typeof body.identityAccounts !== 'number') {
  // A missing bucket is not a rounding error — it means the handler's shape changed and this
  // script is now measuring something else. Fail loudly rather than summing what happens to exist.
  problems.push(`response is missing numeric field(s): ${[...missing, ...(typeof body.identityAccounts === 'number' ? [] : ['identityAccounts'])].join(', ')}`);
} else {
  const sum = BUCKETS.reduce((t, b) => t + body[b], 0);
  if (sum !== body.identityAccounts) {
    problems.push(
      `buckets do NOT partition the realm: ${BUCKETS.map((b) => `${b}=${body[b]}`).join(' + ')} = ${sum}, but identityAccounts=${body.identityAccounts}`,
    );
  } else {
    notes.push(`partition OK: ${sum} accounts across ${BUCKETS.length} buckets`);
  }
  if (body.created === 0 && body.alreadyProvisioned > 0) {
    notes.push('created=0 with alreadyProvisioned>0 — this is the idempotent second run, not a failure');
  }
  if (body.skippedDuplicateEmail > 0) {
    problems.push(`skippedDuplicateEmail=${body.skippedDuplicateEmail} — two Keycloak accounts share one email (DEF-045's shape). Do not merge by hand; decide which account is real.`);
  }
}

// ---- check 2: the database agrees ----------------------------------------------------------- //

let databaseRead = false;
if (!args.connection) {
  notes.push('no --connection given: arithmetic only, the DATABASE was not re-read (the half that matters)');
} else {
  databaseRead = true;
  // mssql is already a dependency of the deploy tooling path; imported lazily so the arithmetic
  // check still runs on a machine without it.
  const sql = (await import('mssql')).default;
  const pool = await sql.connect(args.connection);
  try {
    const { recordset } = await pool.request().query(`
      SELECT
        (SELECT COUNT(*) FROM membership.committee_members) AS members,
        (SELECT COUNT(*) FROM membership.committee_members m
           WHERE NOT EXISTS (SELECT 1 FROM membership.member_streams ms
                              WHERE ms.CommitteeMemberId = m.Id)) AS membersWithNoStream,
        (SELECT COUNT(*) FROM membership.streams WHERE IsWildcard = 1) AS wildcards
    `);
    const row = recordset[0];
    notes.push(`database: ${row.members} member rows, ${row.wildcards} wildcard stream(s)`);

    if (row.wildcards !== 1) {
      problems.push(`expected exactly 1 wildcard stream, found ${row.wildcards} — with none, every row the run created holds nothing`);
    }
    if (row.membersWithNoStream > 0) {
      // THE assertion. Anyone here is refused every stream-scoped write from their first login.
      problems.push(
        `${row.membersWithNoStream} member row(s) hold NO stream — each is refused every stream-scoped write. ` +
        `If they signed in BETWEEN the deploy and the run, that is the known residual: assign streams by hand ` +
        `(ADR-0043 clause 2). If not, the reconciliation did not do its job.`,
      );
    } else {
      notes.push('every member row holds at least one stream — the property DEF-065 exists for');
    }
  } finally {
    await pool.close();
  }
}

// ---- report --------------------------------------------------------------------------------- //

for (const n of notes) console.log(`  · ${n}`);

// ⚠ A PASS THAT COULD NOT HAVE FAILED IS NOT A PASS. Without --connection this script checks only
// that the response's own numbers add up — it never looks at a member row, so it cannot detect the
// one thing DEF-065 is about. Reporting VERIFIED there would be a hollow green of exactly the kind
// this project recorded when risk-liveness "passed" only because its predicate could not be
// evaluated. Incomplete is a distinct outcome from verified, and it exits non-zero so it can never
// be pasted as the evidence that closes DEF-065.
if (problems.length === 0 && !databaseRead) {
  console.error('\n⚠ INCOMPLETE — arithmetic only. Re-run with --connection before closing DEF-065:');
  console.error('  the response adding up proves the handler counted consistently, not that a single');
  console.error('  member row exists or holds a stream. That check needs the database.');
  process.exit(1);
}
if (problems.length === 0) {
  console.log('\n✔ Reconciliation VERIFIED against the database. Safe to close DEF-065 and DEF-038 with this as evidence.');
  process.exit(0);
}
console.error(`\n✖ ${problems.length} problem(s) — do NOT close DEF-065 yet:`);
for (const p of problems) console.error(`  - ${p}`);
process.exit(1);
