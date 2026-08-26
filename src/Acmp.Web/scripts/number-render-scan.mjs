#!/usr/bin/env node
/*
 * NFR-037 (DW-068 / WBS-24.4) — inventory of numbers rendered BARE in JSX child position.
 *
 * Numbers reach a screen two ways here. Values handed to `t()` are localized centrally by the i18n
 * formatter module (`src/i18n/index.ts`), so they need no per-site work and are not this script's
 * subject. The other way is a numeric expression written straight into JSX, which nothing central
 * can reach — that is what this lists.
 *
 * ⚠ WHAT THIS IS AND IS NOT. It is TRIAGE — a worklist, not a coverage proof. It reads one line at
 * a time and cannot know a type, so it recognises a number by NAME. The first version keyed on
 * count/total/length and reported 24 sites while silently missing the entire reports family, whose
 * numbers are called `s.value`, `c.value` and `card.kpi` — a true number about the wrong set. The
 * completeness check for this requirement is a rendered page in Arabic: any ASCII digit in visible
 * text is either a miss or one of the named exceptions in AC-147.
 *
 * Two calibrations must pass before it will report anything (a clean scan over the wrong file set
 * reads exactly like a clean scan over the right one):
 *   A. the tree is plausible  — refuses under 50 source files
 *   B. it can see a positive  — a synthetic bare render is injected and must be found
 *
 * Run from anywhere: paths resolve from this file, not from cwd.
 */
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const SRC = join(dirname(fileURLToPath(import.meta.url)), '..', 'src');
const MIN_FILES = 50;

/*
 * Names that read as a quantity on screen. Deliberately wide; false positives are cheap to dismiss.
 *
 * ⚠ TWO PATTERNS, and the split is not cosmetic. Compound names are matched as SUBSTRINGS because
 * `\bcount\b` does not match `findingCount` — camelCase has no word boundary in front of the second
 * word, and a first attempt at this regex silently LOST `hiddenCount` and `r.findingCount` while
 * appearing to widen the net. Only the genuinely ambiguous short names take word boundaries.
 */
const NUMISH_PART = /(count|total|length|size|bytes|minutes|seconds|hours|days|weeks|pct|percent|tally|votes|value|version|quorum|index|score|exposure|kpi|remaining|eligible)/i;
const NUMISH_WORD = /\b(n|num|mins|rate|sum|min|max|avg|needed|cast|shown)\b/i;

function sourceFiles(dir) {
  const out = [];
  for (const entry of readdirSync(dir)) {
    const p = join(dir, entry);
    if (statSync(p).isDirectory()) out.push(...sourceFiles(p));
    else if (/\.tsx$/.test(entry) && !/\.test\.tsx$/.test(entry)) out.push(p);
  }
  return out;
}

/** Numeric-looking expressions sitting in JSX child position on one line. */
function scanLine(line) {
  const hits = [];
  for (const m of line.matchAll(/[>}]\s*\{([^{}]+)\}\s*[<{]/g)) {
    const expr = m[1].trim();
    if (/^['"`]/.test(expr) || expr.includes('=>') || /^t\(/.test(expr)) continue; // literals, callbacks, i18n keys
    if (NUMISH_PART.test(expr) || NUMISH_WORD.test(expr)) hits.push(expr);
  }
  return hits;
}

// --- Calibration B: prove the matcher can see a bare render before trusting a clean one on real code.
const PROBE = '        <span className="x">{group.items.length}</span>';
if (scanLine(PROBE).length !== 1) {
  console.error('CALIBRATION B FAILED: the matcher did not find an injected bare numeric render.');
  process.exit(2);
}
// ...that a camelCase quantity is still seen — the regression that silently cost two real sites...
if (scanLine('        <span className="x">{r.findingCount}</span>').length !== 1) {
  console.error('CALIBRATION B FAILED: the matcher missed a camelCase quantity (findingCount).');
  process.exit(2);
}
// ...and that it does NOT claim a plain text node, or every line would be a "finding".
if (scanLine('        <span className="x">{t(\'topics.title\')}</span>').length !== 0) {
  console.error('CALIBRATION B FAILED: the matcher flagged an i18n key as a bare number.');
  process.exit(2);
}

const files = sourceFiles(SRC);
// --- Calibration A: a clean result over an empty or truncated tree is not a clean result.
if (files.length < MIN_FILES) {
  console.error(`CALIBRATION A FAILED: only ${files.length} source files found under ${SRC} (expected >= ${MIN_FILES}).`);
  console.error('A gate with no subject must fail, not pass.');
  process.exit(2);
}

let found = 0;
for (const f of files) {
  const rel = relative(SRC, f).split(String.fromCharCode(92)).join('/');
  readFileSync(f, 'utf8')
    .split('\n')
    .forEach((line, i) => {
      for (const expr of scanLine(line)) {
        found += 1;
        console.log(`src/${rel}:${i + 1}  ${expr}`);
      }
    });
}
console.log(`\n${found} bare numeric render(s) across ${files.length} source files (calibrations A and B passed).`);
