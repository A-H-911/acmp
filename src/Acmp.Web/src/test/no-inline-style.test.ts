import { describe, it, expect } from 'vitest';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

/*
 * WBS-40.5 / DW-022 (DEC-174): no `style` prop in the SPA's JSX. A static value is a CSS class in the
 * design system; a data-driven one is a CSS custom property set through `cssVars` (src/lib/cssVars.ts)
 * and consumed by the stylesheet. The operator chose the full cut over a reduced count, so this guard
 * holds it at ZERO — one stray `style={{…}}` is the first step back to per-element literals.
 *
 * Hygiene, not security: `style-src 'self'` never depended on this (DW-022's own row).
 */

const SRC = join(__dirname, '..');

function productTsx(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) return productTsx(full);
    return full.endsWith('.tsx') && !full.endsWith('.test.tsx') ? [full] : [];
  });
}

/** A JSX `style=` attribute: preceded by whitespace, followed by `=`. `className=`, `--style` and a
 *  `styles.css` import do not match. */
const STYLE_PROP = /(^|\s)style\s*=\s*[{"']/;

export function hasStyleProp(line: string): boolean {
  return STYLE_PROP.test(line);
}

describe('no inline style props in the SPA (DEC-174)', () => {
  it('has ZERO style props in product .tsx', () => {
    const offenders: string[] = [];
    for (const file of productTsx(SRC)) {
      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, i) => {
          if (hasStyleProp(line)) offenders.push(`${file.slice(SRC.length + 1)}:${i + 1}: ${line.trim()}`);
        });
    }
    expect(
      offenders,
      'Use a CSS class for a static value, or cssVars() + var(--x) in the stylesheet for a data-driven one:\n\n' +
        offenders.join('\n'),
    ).toEqual([]);
  });

  it('classifies lines correctly, so the guard is neither vacuous nor a nuisance', () => {
    expect(hasStyleProp('<div style={{ color: "red" }}>')).toBe(true);
    expect(hasStyleProp('  <li ref={setNodeRef} style={style}>')).toBe(true);
    expect(hasStyleProp('      style={{ insetInlineStart: ln.x }}')).toBe(true);
    expect(hasStyleProp('<div style="color:red">')).toBe(true);

    expect(hasStyleProp('<div className="x" ref={cssVars({ "--w": "54px" })} />')).toBe(false);
    expect(hasStyleProp("import './styles.css';")).toBe(false);
    expect(hasStyleProp('el.style.setProperty(name, value);')).toBe(false);
    expect(hasStyleProp('const style = compute();')).toBe(false);
  });

  it('scans a non-empty set of components', () => {
    expect(productTsx(SRC).length).toBeGreaterThan(50);
  });
});
