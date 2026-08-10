import { describe, it, expect } from 'vitest';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

/*
 * RTL guard: no HORIZONTALLY ASYMMETRIC corner radii anywhere in the stylesheets.
 *
 * ACMP is bilingual and Arabic is RTL, and DEC-022 puts that on CSS LOGICAL PROPERTIES rather than a
 * mirrored stylesheet. A logical border side paired with a physical radius silently breaks that: the
 * border flips in RTL and the rounding does not.
 *
 * That is exactly what wiki.css did — `border-inline-end` with `border-radius: 12px 0 0 12px` — so the
 * two panes rounded on their inner seam and went square on the outside, the inverse of the design. It
 * was the only screen in the product doing it, and nothing caught it: it is valid CSS, it type-checks,
 * it passes lint, and it renders perfectly in English. Only an Arabic reader sees it.
 *
 * WHY THE RULE IS ASYMMETRY, NOT "more than one value". The first draft of this guard flagged every
 * multi-value shorthand and immediately caught `border-radius: 5px 5px 0 0` on a report chart column —
 * both TOP corners rounded. That is vertically asymmetric but horizontally SYMMETRIC, so it mirrors
 * identically in RTL and is perfectly correct. A guard that cries wolf on correct code gets suppressed,
 * which is worse than no guard. So: expand the shorthand and compare left against right.
 *
 * Asserting ZERO rather than a count, deliberately: a count-based budget passes for the wrong reason
 * the moment someone adds an instance and updates the number.
 */

const CSS_ROOT = join(__dirname, '..');

function cssFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) return cssFiles(full);
    return full.endsWith('.css') ? [full] : [];
  });
}

/**
 * True when a `border-radius` shorthand rounds the left and right sides differently, i.e. it encodes
 * a physical direction and will NOT mirror in RTL.
 *
 * CSS expands 1-4 values to [top-left, top-right, bottom-right, bottom-left]:
 *   1 value  -> all four equal            -> symmetric
 *   2 values -> TL/BR = a, TR/BL = b      -> asymmetric unless a === b
 *   3 values -> TL = a, TR/BL = b, BR = c -> asymmetric unless a === c
 *   4 values -> as written
 * The elliptical `/` form is split off and only the horizontal radii are judged.
 */
export function isDirectional(shorthand: string): boolean {
  // Collapse whitespace INSIDE parentheses first, so `var(--radius-md, 8px)` and `calc(4px + 2px)`
  // each count as ONE value. Splitting naively on whitespace read the var()'s fallback as a second
  // corner and flagged a perfectly symmetric token — the second false positive this guard produced
  // while being written, and the reason it now normalises before it judges.
  const collapsed = shorthand.replace(/\(([^()]*)\)/g, (m) => m.replace(/\s+/g, ''));
  const horizontal = collapsed.split('/')[0].trim();
  const parts = horizontal.split(/\s+/).filter(Boolean);
  if (parts.length < 2) return false;

  const [a, b, c, d] = parts;
  const [tl, tr, br, bl] =
    parts.length === 2 ? [a, b, a, b] :
    parts.length === 3 ? [a, b, c, b] :
    [a, b, c, d];

  const norm = (v: string) => (parseFloat(v) === 0 ? '0' : v);
  return norm(tl) !== norm(tr) || norm(bl) !== norm(br);
}

const DECL = /border-radius\s*:\s*([^;{}]+)/;

describe('RTL: stylesheets use logical corner radii (DEC-022)', () => {
  it('has ZERO direction-encoding border-radius shorthands', () => {
    const offenders: string[] = [];

    for (const file of cssFiles(CSS_ROOT)) {
      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, i) => {
          const trimmed = line.trim();
          if (trimmed.startsWith('/*') || trimmed.startsWith('*')) return;
          const m = DECL.exec(line);
          if (m && isDirectional(m[1])) {
            offenders.push(`${file.slice(CSS_ROOT.length + 1)}:${i + 1}: ${trimmed}`);
          }
        });
    }

    // Printed, not merely counted, so a failure names the file and line AND says what to use instead —
    // a guard that only reports "0 expected, 1 received" gets suppressed rather than fixed.
    expect(
      offenders,
      `Direction-encoding corner radii break RTL. Use logical corners instead:\n` +
        `  border-start-start-radius / border-start-end-radius\n` +
        `  border-end-start-radius   / border-end-end-radius\n\n` +
        offenders.join('\n'),
    ).toEqual([]);
  });

  it('classifies radii correctly, so the guard is neither vacuous nor a nuisance', () => {
    // The real wiki.css bug, verbatim — must be caught.
    expect(isDirectional('12px 0 0 12px')).toBe(true);
    expect(isDirectional('0 12px 12px 0')).toBe(true);
    // Horizontally symmetric — must NOT be caught. This is the report column bar that the first,
    // blunter version of this guard falsely flagged.
    expect(isDirectional('5px 5px 0 0')).toBe(false);
    expect(isDirectional('8px')).toBe(false);
    expect(isDirectional('50%')).toBe(false);
    expect(isDirectional('4px 4px')).toBe(false);          // 2 values, a === b after normalising
    expect(isDirectional('4px 8px')).toBe(true);           // 2 values, TL/BR differ from TR/BL
    expect(isDirectional('6px 6px 2px')).toBe(true);       // 3 values, TL !== BR
    // Tokens and calc() are single values however much whitespace they contain.
    expect(isDirectional('var(--radius-md, 8px)')).toBe(false);
    expect(isDirectional('calc(4px + 2px)')).toBe(false);
    // The elliptical form is judged on its HORIZONTAL radii only.
    expect(isDirectional('10px / 20px')).toBe(false);
  });

  it('scans a non-empty set of stylesheets', () => {
    // The other half of "cannot pass vacuously": if the walk ever stops finding files, the zero
    // assertion above would still pass while checking nothing at all.
    expect(cssFiles(CSS_ROOT).length).toBeGreaterThan(3);
  });
});
