#!/usr/bin/env node
// Guards two deployment-documentation defects that were fixed together and can both silently return.
// A doc is fixed once and drifts again; nothing in CI read the runbooks at all before this.
//
// 1. THE BUDGET CEILING. `deploy/aws/_common.sh` defines BUDGET_LIMIT_USD, which is what
//    00-account.sh actually creates the AWS budget with. It was raised 60 -> 100 on 2026-08-09 to
//    fund an always-on production instance (amending ADR-0034), and five places went on stating
//    $60 -- including aws/README.md's table of what 00-account.sh creates, which is the first thing
//    an operator reads. A stale ceiling is not cosmetic here: the 50/80/100% alert thresholds and
//    the 100% IAM-deny brake are all derived from it, so a reader doing the arithmetic from the doc
//    concludes the deny fires at $60 and mis-times the whole spend model.
//
//    This asserts AGREEMENT WITH THE DEFINITION SITE, not the absence of the string "$60". A check
//    keyed on the stale value passes for the wrong reason the next time the budget moves -- the
//    baseline-as-number trap that has already produced two false-green checks on this project.
//
//    `_common.sh` itself is exempt BY PATH, because the definition site is exactly where the
//    amendment history belongs ("...would have sat permanently above the old $60 budget's 80%
//    notification..."). Exempting it by that PHRASE instead would rebind the check to prose and
//    break the moment someone rewords the comment -- the failure mode that let contrast.test.ts
//    grade the light palette as dark for its entire life.
//
// 2. `promote.sh` IN A CLOUD RUNBOOK. `deploy/scripts/promote.sh` is the P18b ON-PREM warm-standby
//    failover script: it restores a backup onto a standby VM and stops before DNS cutover. Its
//    premise -- a standby VM -- does not exist in the cloud topology. `promote-image.sh` is the
//    cloud one, and its own header says so explicitly, having been written to prevent this exact
//    confusion. cloud-backup-dr.md told operators to run promote.sh for a cloud rollback anyway:
//    on a cloud box it would have restored a backup over an intact database during what was only
//    ever meant to be an image re-point.
//
//    Scoped to `deploy/runbooks/cloud-*.md` so the ON-PREM runbook (runbooks/README.md), where
//    promote.sh is correct, stays legal. It matches the INVOCABLE PATH rather than the bare name,
//    so a cloud doc can still explain in prose why promote.sh is the wrong script without tripping
//    the rule that says so -- structure, not substring.

import { readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, posix, relative, sep } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const deployDir = join(root, 'deploy');

// --- the definition site -----------------------------------------------------------------------
const commonPath = join(deployDir, 'aws', '_common.sh');
const budgetMatch = readFileSync(commonPath, 'utf8').match(
  /^BUDGET_LIMIT_USD="\$\{ACMP_BUDGET_LIMIT_USD:-(\d+)\}"/m,
);
if (!budgetMatch) {
  console.error(
    'check-runbook-drift: could not read BUDGET_LIMIT_USD from deploy/aws/_common.sh.\n' +
      '    That assignment is the source of truth this check compares the docs against; if its\n' +
      '    shape changed, update the pattern here rather than dropping the check.',
  );
  process.exit(1);
}
const budget = budgetMatch[1];

// Files that legitimately state a figure other than the current ceiling, by PATH not by phrase.
const BUDGET_EXEMPT = new Set([posix.join('deploy', 'aws', '_common.sh')]);

// A STATEMENT OF THE CEILING, not any dollar sign near the word "budget". These docs are full of
// per-line cost arithmetic that legitimately mentions the budget while quoting a different number
// ("2 x t3.medium is ~$76/mo and blows the budget", "~$32/mo -- over half the budget"), and an
// account-wide "$N is the budget" check flags all of it. Only two shapes actually assert the
// ceiling: the figure attached to the word, and the word followed by "is"/"of" and the figure.
// "budget ... at $N" is deliberately NOT a shape -- that is how the unrelated `My Monthly Cost
// Budget` at $10 is described, and it is a different budget, not a stale copy of this one.
// Backticks and ** survive because these figures are written as markdown (`$60`/mo, **$60/mo**).
const CEILING = /\$\**(\d+(?:\.\d+)?)`?\**(?:\/mo(?:nth)?)?\**\s+budget|budget\s+(?:is|of)\s+~?\**\$(\d+(?:\.\d+)?)/gi;

const SCANNED = /\.(md|sh|ya?ml)$/;
const files = readdirSync(deployDir, { recursive: true, withFileTypes: true })
  .filter((e) => e.isFile() && SCANNED.test(e.name))
  .map((e) => relative(root, join(e.parentPath, e.name)).split(sep).join(posix.sep))
  .sort();

const errors = [];

for (const file of files) {
  const lines = readFileSync(join(root, file), 'utf8').split(/\r?\n/);

  lines.forEach((line, i) => {
    const at = `${file}:${i + 1}`;

    // 1. Every stated budget ceiling must be the one the account is actually created with.
    if (!BUDGET_EXEMPT.has(file)) {
      for (const m of line.matchAll(CEILING)) {
        const amount = m[1] ?? m[2];
        if (amount !== budget) {
          errors.push(
            `${at}: states $${amount} on a budget line, but deploy/aws/_common.sh sets ` +
              `BUDGET_LIMIT_USD=${budget}.\n` +
              `    ${line.trim()}`,
          );
        }
      }
    }

    // 2. The on-prem failover script must not be invocable from a cloud runbook.
    if (/^deploy\/runbooks\/cloud-.*\.md$/.test(file) && /deploy\/scripts\/promote\.sh/.test(line)) {
      errors.push(
        `${at}: invokes deploy/scripts/promote.sh, which is the P18b ON-PREM warm-standby\n` +
          `    failover script -- it restores a backup and its premise (a standby VM) does not\n` +
          `    exist in the cloud topology. Use deploy/scripts/promote-image.sh, which re-points\n` +
          `    an environment at a commit's published images without touching the database.\n` +
          `    ${line.trim()}`,
      );
    }
  });
}

if (errors.length) {
  console.error('check-runbook-drift: deployment docs disagree with the deployment itself.\n');
  for (const e of errors) console.error(`  - ${e}`);
  console.error(
    '\nAn operator following these docs would mis-time the spend model, or restore a backup\n' +
      'over an intact production database during what should have been an image re-point.',
  );
  process.exit(1);
}
console.log(
  `check-runbook-drift: OK — ${files.length} files, budget ceiling $${budget} stated consistently, ` +
    'no on-prem failover script invoked from a cloud runbook.',
);
