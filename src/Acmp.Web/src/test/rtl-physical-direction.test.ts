import { describe, it, expect } from 'vitest';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

/*
 * RTL guard #2: no PHYSICAL left/right direction encoded in the stylesheets.
 *
 * AC-101 (superseding AC-041) asks that "no LTR flow artifacts are detected (no left-anchored text
 * blocks, misaligned icons, or cropped RTL text)". DEC-051 d2 settled HOW that verb is performed:
 * property-level detectors in the style of rtl-logical-css.test.ts, NOT pixel baselines — on this
 * project's own measured experience that the property guard catches real defects (the wiki.css corner
 * radii) while the capture-only sweep has caught none.
 *
 * ⚠ THIS FILE IS THE DETECTOR FOR THE FIRST TWO ARTIFACTS THE AC NAMES, AND IT SAYS SO RATHER THAN
 * IMPLYING MORE. A left-anchored text block is `text-align: left` or `float: left`; a misaligned icon
 * is a physical margin/padding/border side or a physical inset. Both are mechanically detectable in
 * the CSS, which is where they are WRITTEN. "Cropped RTL text" is a rendering outcome and is NOT
 * detected here — see the note at the bottom of this comment.
 *
 * WHY ZERO IS AN HONEST TARGET RATHER THAN AN ASPIRATION: measured before this guard was written, the
 * whole stylesheet tree already contains ZERO of every family below, across 27 files. DEC-022 put the
 * UI on logical properties from P3 and that discipline has held. So this guard does not ask anyone to
 * fix anything — it LOCKS IN a property that is already true and fails the day it stops being true,
 * which is the only moment a regression is cheap to fix.
 *
 * ⚠ `translateX()` IS DELIBERATELY NOT FLAGGED, and the omission is the considered half. The common
 * `left: 50%; transform: translateX(-50%)` centering idiom is horizontally SYMMETRIC — it centers
 * identically in RTL — so flagging every translateX would cry wolf on correct code, and a guard that
 * cries wolf gets suppressed rather than fixed (the lesson rtl-logical-css.test.ts paid for twice
 * while it was being written). The physical INSET half of that idiom is still caught below, and
 * `inset-inline-start: 50%` is exactly equivalent for centering, so nothing correct is made harder.
 *
 * ⚠ WHAT THIS DOES NOT COVER, stated so a reader does not infer a sweep that did not happen: cropped
 * RTL text, and any artifact that only exists once a browser has laid the page out. Those need a
 * rendering instrument, and JSDOM does not render.
 */

const CSS_ROOT = join(__dirname, '..');

function cssFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) return cssFiles(full);
    return full.endsWith('.css') ? [full] : [];
  });
}

/** Each rule names the offending pattern and the logical property that replaces it exactly. */
const RULES: ReadonlyArray<{ readonly pattern: RegExp; readonly use: string }> = [
  { pattern: /\btext-align\s*:\s*(left|right)\b/, use: 'text-align: start | end' },
  { pattern: /\bfloat\s*:\s*(left|right)\b/, use: 'float: inline-start | inline-end' },
  { pattern: /\bmargin-(left|right)\s*:/, use: 'margin-inline-start / margin-inline-end' },
  { pattern: /\bpadding-(left|right)\s*:/, use: 'padding-inline-start / padding-inline-end' },
  { pattern: /\bborder-(left|right)(-(width|style|color))?\s*:/, use: 'border-inline-start / border-inline-end' },
  // Anchored to the start of a declaration so `background-position: left` and the `left` keyword in
  // any other value are not swept up — only a real physical inset is.
  { pattern: /(^|[;{])\s*(left|right)\s*:/, use: 'inset-inline-start / inset-inline-end' },
];

/** Every rule this line violates, as human-readable "what to use instead" strings. */
export function violations(line: string): string[] {
  return RULES.filter((r) => r.pattern.test(line)).map((r) => r.use);
}

describe('RTL: stylesheets encode no physical left/right (AC-101, DEC-051 d2)', () => {
  it('has ZERO physical-direction declarations', () => {
    const offenders: string[] = [];

    for (const file of cssFiles(CSS_ROOT)) {
      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, i) => {
          const trimmed = line.trim();
          if (trimmed.startsWith('/*') || trimmed.startsWith('*') || trimmed.startsWith('//')) return;
          const found = violations(line);
          if (found.length > 0) {
            offenders.push(`${file.slice(CSS_ROOT.length + 1)}:${i + 1}: ${trimmed}  ->  use ${found.join(' / ')}`);
          }
        });
    }

    // Printed, not merely counted: a failure that says "0 expected, 3 received" gets suppressed, and
    // one that names the file, the line and the replacement gets fixed.
    expect(
      offenders,
      `Physical left/right does not mirror in Arabic (DEC-022 puts this UI on logical properties).\n` +
        `Each offender below names the logical property that replaces it exactly:\n\n` +
        offenders.join('\n'),
    ).toEqual([]);
  });

  it('classifies declarations correctly, so the guard is neither vacuous nor a nuisance', () => {
    // The artifacts AC-101 names, in the form they are actually written.
    expect(violations('  text-align: left;')).toHaveLength(1);
    expect(violations('  float: right;')).toHaveLength(1);
    expect(violations('  margin-left: 8px;')).toHaveLength(1);
    expect(violations('  padding-right: var(--space-2);')).toHaveLength(1);
    expect(violations('  border-left: 1px solid var(--line);')).toHaveLength(1);
    expect(violations('  border-right-color: red;')).toHaveLength(1);
    expect(violations('  left: 0;')).toHaveLength(1);
    expect(violations('.a { right: 12px; }')).toHaveLength(1);

    // Correct code that a blunter guard would falsely flag. Each of these is why the patterns are
    // shaped the way they are rather than a bare search for "left".
    expect(violations('  text-align: center;')).toEqual([]);
    expect(violations('  margin-inline-start: 8px;')).toEqual([]);
    expect(violations('  padding-inline: 12px;')).toEqual([]);
    expect(violations('  border-inline-end: 1px solid var(--line);')).toEqual([]);
    expect(violations('  inset-inline-start: 0;')).toEqual([]);
    expect(violations('  background-position: left center;')).toEqual([]); // `left` as a VALUE
    expect(violations('  --nav-left-pad: 4px;')).toEqual([]); // a custom property that merely reads left-ish
    expect(violations('  transform: translateX(-50%);')).toEqual([]); // symmetric centering, deliberately allowed
  });

  it('scans a non-empty set of stylesheets', () => {
    // The other half of "cannot pass vacuously": if the walk ever stops finding files, the zero
    // assertion above would still pass while checking nothing at all.
    expect(cssFiles(CSS_ROOT).length).toBeGreaterThan(3);
  });
});
