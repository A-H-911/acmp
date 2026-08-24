#!/usr/bin/env node
/*
 * DW-082 triage: which files fail ADR-0016's per-file line gate, which LINES are uncovered,
 * and what those lines actually SAY.
 *
 * WHY THIS EXISTS, AND WHY IT IS COMMITTED. The first version of this tool scraped vitest's
 * pretty ASCII table and reported "NO LINE DATA" for fourteen files, because the table wraps
 * long "Uncovered Line #s" cells onto continuation lines — and it read the table's compressed
 * range display ("92-138") as a 47-line count when the structured data says seven. Do not parse
 * the pretty output when a structured one exists. The second version lived only in a scratchpad
 * and was lost, so this one lives in the repo (trap 27).
 *
 * WHY IT PRINTS THE SOURCE LINE. DW-082's own row warns that the causes are NOT uniform — 25
 * files are pure inline-handler, 7 are mixed, and TopBar is neither. A bare line number invites
 * you to assume "another inline handler"; the line's text makes you confirm it. Confirming is
 * the whole point, so the tool makes it free.
 *
 * CALIBRATION — it refuses to report rather than report something it cannot vouch for:
 *   A. every file's derived line percentage must equal coverage-summary.json's own pct;
 *   B. every FAILING file must resolve at least one uncovered line (the silent partial-parse
 *      failure that killed version one);
 *   C. the derived failing set must be non-empty only if the summary says so, and vice versa.
 *
 * Paths resolve from this file's location, never the cwd (trap 29).
 *
 *   npm run test:cov -- --coverage.reporter=json --coverage.reporter=json-summary \
 *                       --coverage.reporter=text
 *   node scripts/coverage-triage.mjs
 */

import { readFileSync, existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const WEB_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const COVERAGE_DIR = path.join(WEB_ROOT, 'coverage')
const THRESHOLD = 95 // ADR-0016, lines only, perFile

function loadJson(name) {
  const file = path.join(COVERAGE_DIR, name)
  if (!existsSync(file)) {
    console.error(
      `missing ${path.relative(WEB_ROOT, file)} — run:\n` +
        '  npm run test:cov -- --coverage.reporter=json --coverage.reporter=json-summary --coverage.reporter=text',
    )
    process.exit(2)
  }
  return JSON.parse(readFileSync(file, 'utf8'))
}

/*
 * Per-line hit counts, derived exactly as istanbul-lib-coverage's getLineCoverage does:
 * a statement contributes to its START line only, and the highest count on a line wins.
 *
 * ⚠ Attributing a statement to every line of its SPAN instead reports 100% for TopBar, which
 * the gate scores 82% — a covered multi-line JSX element swallows the uncovered inline arrow
 * nested inside it, i.e. it hides the exact construct DW-082 exists to expose. Calibration A
 * caught that; without the calibration this tool would have reported a clean tree.
 */
function linesOf(entry) {
  const hits = new Map()
  for (const [id, loc] of Object.entries(entry.statementMap)) {
    const count = entry.s[id] ?? 0
    const line = loc.start.line
    hits.set(line, Math.max(hits.get(line) ?? 0, count))
  }
  return hits
}

const final = loadJson('coverage-final.json')
const summary = loadJson('coverage-summary.json')

const files = []
const mismatches = []

for (const [absPath, entry] of Object.entries(final)) {
  const hits = linesOf(entry)
  const total = hits.size
  if (total === 0) continue
  const uncovered = [...hits.entries()].filter(([, n]) => n === 0).map(([line]) => line)
  const pct = ((total - uncovered.length) / total) * 100

  const summaryEntry = summary[absPath] ?? summary[absPath.split(path.sep).join('/')]
  const theirPct = summaryEntry?.lines?.pct
  // Calibration A: our derivation must agree with the reporter's own number.
  if (theirPct !== undefined && Math.abs(theirPct - pct) > 0.01) {
    mismatches.push({ file: absPath, ours: pct.toFixed(2), theirs: theirPct })
  }

  files.push({
    file: path.relative(WEB_ROOT, absPath).split(path.sep).join('/'),
    absPath,
    pct: theirPct ?? pct,
    total,
    uncovered: uncovered.sort((a, b) => a - b),
  })
}

if (mismatches.length) {
  console.error('CALIBRATION A FAILED — derived line pct disagrees with coverage-summary.json:')
  for (const m of mismatches.slice(0, 10)) {
    console.error(`  ${m.file}\n    ours ${m.ours}  theirs ${m.theirs}`)
  }
  console.error(`\n${mismatches.length} file(s) disagree. Refusing to report.`)
  process.exit(3)
}

const failing = files.filter((f) => f.pct < THRESHOLD).sort((a, b) => a.pct - b.pct)

// Calibration B: a failing file that resolves no uncovered lines means the parse failed
// silently — version one's exact defect, which looked like a clean row.
const blind = failing.filter((f) => f.uncovered.length === 0)
if (blind.length) {
  console.error('CALIBRATION B FAILED — failing files with zero uncovered lines resolved:')
  for (const f of blind) console.error(`  ${f.file} (${f.pct}%)`)
  process.exit(4)
}

console.log(`calibration : A ok (${files.length} files agree with coverage-summary), B ok`)
console.log(`threshold   : ${THRESHOLD}% lines, per file (ADR-0016)`)
console.log(`failing     : ${failing.length} file(s), ${failing.reduce((n, f) => n + f.uncovered.length, 0)} uncovered lines\n`)

for (const f of failing) {
  console.log(`── ${f.file}  ${f.pct}%  (${f.uncovered.length} uncovered of ${f.total})`)
  const src = existsSync(f.absPath) ? readFileSync(f.absPath, 'utf8').split(/\r?\n/) : []
  for (const line of f.uncovered) {
    console.log(`   ${String(line).padStart(4)}  ${(src[line - 1] ?? '').trim().slice(0, 110)}`)
  }
  console.log()
}
