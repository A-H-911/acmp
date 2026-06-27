/*
 * WCAG 2.2 AA contrast gate for the design tokens the Topics screens rely on.
 *
 * jsdom can't compute rendered colour, so the component axe tests disable the
 * `color-contrast` rule. For a token-driven design system the durable way to
 * machine-verify contrast is to check every text/background token PAIR the
 * screens actually use, in BOTH themes, against the AA thresholds — which is
 * what this test does. It reads tokens.css directly, so a token colour change
 * that drops a pair below AA fails CI deterministically (no browser needed).
 */
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

// Vitest runs with cwd = the web project root; tokens.css is the design-token SoT.
const css = readFileSync(resolve(process.cwd(), 'src/styles/tokens.css'), 'utf8');

/** Pull `--name: #hex` declarations out of a single CSS rule block. */
function block(selector: string): Record<string, string> {
  const start = css.indexOf(selector);
  const open = css.indexOf('{', start);
  const close = css.indexOf('}', open);
  const out: Record<string, string> = {};
  for (const m of css.slice(open + 1, close).matchAll(/--([\w-]+):\s*(#[0-9a-fA-F]{3,8})/g)) {
    out[m[1]] = m[2];
  }
  return out;
}
const LIGHT = block(':root');
const DARK = { ...LIGHT, ...block('[data-theme="dark"]') }; // dark overrides the themeable subset

function luminance(hex: string): number {
  let h = hex.replace('#', '');
  if (h.length === 3) h = h.split('').map((c) => c + c).join('');
  const chan = [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16) / 255);
  const lin = (c: number) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
  return 0.2126 * lin(chan[0]) + 0.7152 * lin(chan[1]) + 0.0722 * lin(chan[2]);
}
function contrast(fg: string, bg: string): number {
  const a = luminance(fg);
  const b = luminance(bg);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

const TEXT = 4.5; // normal text (WCAG 1.4.3)

// [foreground token, background token, min ratio, where it appears in the Topics screens]
const PAIRS: ReadonlyArray<readonly [string, string, number, string]> = [
  ['text', 'surface', TEXT, 'card body text (table/list/kanban)'],
  ['text', 'bg-app', TEXT, 'detail title/body/timeline text on the page'],
  ['text', 'subtle', TEXT, 'comment body bubble'],
  ['text', 'sunken', TEXT, 'kanban column title'],
  ['text-2', 'surface', TEXT, 'owner/type/age/urgency/meta on cards'],
  ['text-2', 'bg-app', TEXT, 'header subtitle, count, detail section labels, timeline meta'],
  ['text-2', 'subtle', TEXT, 'drop hint, footer note, detail key chip, rel sub'],
  ['text-2', 'sunken', TEXT, 'avatar initials'],
  ['text-2', 'primary-tint', TEXT, 'selected type-card description'],
  ['text-3', 'surface', TEXT, 'field hints, fieldset sub-help, char counter, move-current'],
  ['accent', 'surface', TEXT, 'record keys, links'],
  ['accent', 'primary-tint', TEXT, 'selected card title, system tokens'],
  ['st-neutral-fg', 'st-neutral-bg', TEXT, 'neutral status chip'],
  ['st-info-fg', 'st-info-bg', TEXT, 'info status chip + detail stream chips'],
  ['st-sched-fg', 'st-sched-bg', TEXT, 'scheduled status chip'],
  ['st-warn-fg', 'st-warn-bg', TEXT, 'returned/warn status chip'],
  ['st-success-fg', 'st-success-bg', TEXT, 'done status chip'],
  ['st-danger-fg', 'st-danger-bg', TEXT, 'urgent pill / danger chip'],
  ['st-danger-fg', 'surface', TEXT, 'urgent urgency text, SLA-breached age'],
  ['primary-fg', 'primary', TEXT, 'avatar initials, primary buttons'],
];

describe('Topics token contrast (WCAG 2.2 AA)', () => {
  for (const [name, tokens] of [['light', LIGHT], ['dark', DARK]] as const) {
    describe(name, () => {
      for (const [fg, bg, min, where] of PAIRS) {
        it(`--${fg} on --${bg} ≥ ${min}:1 (${where})`, () => {
          const f = tokens[fg];
          const b = tokens[bg];
          expect(f, `missing --${fg} in ${name}`).toBeTruthy();
          expect(b, `missing --${bg} in ${name}`).toBeTruthy();
          expect(contrast(f, b)).toBeGreaterThanOrEqual(min);
        });
      }
    });
  }
});
