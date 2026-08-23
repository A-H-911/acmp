#!/usr/bin/env node
// NFR-035 / DW-042 — "no hardcoded display strings in components".
//
// ⚠ WHY THIS EXISTS WHEN check-i18n.mjs ALREADY PASSES. That script compares en.json and ar.json against
// EACH OTHER: key parity and mojibake. It is genuinely about i18n and it is genuinely green, which is
// exactly what makes it misleading here — it CANNOT SEE A COMPONENT. A developer who types a label
// straight into JSX ships an untranslatable string while the two resource files stay in PERFECT parity,
// because the string never entered them. NFR-035 has two clauses and only the first was guarded.
//
// WHAT IT FLAGS: text nodes rendered between JSX tags — `<span>Save</span>`. Those are display strings and
// belong in a resource file behind t().
//
// WHAT IT DELIBERATELY DOES NOT FLAG, because the false-positive rate is what decides whether a gate like
// this survives its first week:
//   - expressions: `{t('x')}`, `{count}` — no literal text node at all
//   - attributes: className, data-testid, role, aria-* — not display text (aria-label IS display text, but
//     it is an attribute and needs a different matcher; DW-042's successor can widen this)
//   - punctuation, digits, and single characters: separators like `·`, `/`, `—`
//   - anything inside a test file or a throwaway visual-verify harness
//
// Usage: node scripts/check-hardcoded-strings.mjs [--report]
//   default   fail (exit 1) if any finding is outside the allowlist
//   --report  print findings and always exit 0 (for measuring before gating)

import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// ⚠ RESOLVED FROM THIS FILE, NOT FROM cwd. The CI frontend job runs with working-directory
// src/Acmp.Web, so a cwd-relative path resolves to src/Acmp.Web/src/Acmp.Web/src and the scan either
// crashes or, worse, reports a clean tree over ZERO files - a passing gate with no subject (LL-007).
// Caught by running the script the way CI runs it rather than only from the repo root.
const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const ROOT = join(REPO_ROOT, 'src/Acmp.Web/src');
const REPORT_ONLY = process.argv.includes('--report');

// Files whose literal text is not display text. Each entry must say WHY, or it is a suppression rather
// than a triage — the .gitleaks.toml lesson (DEF-081): an allowlist that exempts a whole class silently
// turns a green gate into no gate at all.
const ALLOWLIST = [
  // (empty — populated only with a stated reason)
];

/** Text that is display-bearing: contains a run of 2+ letters. `·`, `/`, `12`, `—` are not. */
const HAS_WORD = /[A-Za-z؀-ۿ]{2,}/;

/**
 * A JSX text node: `>` … `<` with no braces or angle brackets between them. Braces are excluded because
 * `{t('save')}` is an expression, not a literal, and that is the correct shape we are steering toward.
 *
 * ⚠ THE LOOKAROUNDS ARE NOT DECORATION — WITHOUT THEM THIS TOOL MEASURES ITSELF. The naive `>([^<>{}]*)<`
 * happily matches straight through TypeScript comparison code: `day >= 1 && dayNum <` reads as a text node
 * containing "= 1 && dayNum". The first run of this script reported five findings and ALL FIVE were that
 * bug, not a hardcoded string. So: the opening `>` must CLOSE A TAG (not be part of `>=`, `=>`, `<<`, `->`
 * or `!==`), and the closing `<` must OPEN one (followed by a letter or `/`).
 */
const TEXT_NODE = /(?<![=!<>\-])>([^<>{}]*)<(?=[A-Za-z/])/g;

/** Operator soup that survived the lookarounds — a text node never contains these. */
const LOOKS_LIKE_CODE = /&&|\|\||=>|[=<>!]=|\?\s|return/;

function* walk(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) yield* walk(path);
    else if (name.endsWith('.tsx') && !name.includes('.test.') && !name.startsWith('vr-')) yield path;
  }
}

const files = [...walk(ROOT)];
// LL-007 again, as a guard rather than a comment: an empty or tiny file set would print
// "no hardcoded strings" and exit 0 over nothing at all.
if (files.length < 50) {
  console.error(`[check-hardcoded-strings] only ${files.length} component(s) found under ${ROOT} - `
    + 'refusing to report a clean tree over a subject this small.');
  process.exit(2);
}

const findings = [];
for (const file of files) {
  const rel = relative(REPO_ROOT, file).replace(/\\/g, '/');
  if (ALLOWLIST.some((a) => rel.includes(a))) continue;

  const lines = readFileSync(file, 'utf8').split('\n');
  lines.forEach((line, i) => {
    // Skip comment lines outright: prose in a comment is not rendered.
    const trimmed = line.trim();
    if (trimmed.startsWith('//') || trimmed.startsWith('*') || trimmed.startsWith('/*')) return;

    for (const m of line.matchAll(TEXT_NODE)) {
      const text = m[1].trim();
      if (text && HAS_WORD.test(text) && !LOOKS_LIKE_CODE.test(text)) {
        findings.push({ file: rel, line: i + 1, text: text.slice(0, 60) });
      }
    }
  });
}

if (findings.length === 0) {
  console.log('[check-hardcoded-strings] no hardcoded JSX display strings found');
  process.exit(0);
}

console.log(`[check-hardcoded-strings] ${findings.length} hardcoded display string(s) in JSX:`);
for (const f of findings.slice(0, 60)) console.log(`  ${f.file}:${f.line}  "${f.text}"`);
if (findings.length > 60) console.log(`  … and ${findings.length - 60} more`);

if (REPORT_ONLY) {
  console.log('[check-hardcoded-strings] --report: not failing');
  process.exit(0);
}
console.log('NFR-035: UI strings belong in the react-i18next resource files, behind t().');
process.exit(1);
