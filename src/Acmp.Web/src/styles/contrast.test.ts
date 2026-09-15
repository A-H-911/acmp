/*
 * WCAG 2.2 AA contrast gate for the design tokens.
 *
 * jsdom can't compute rendered colour, so the component axe tests disable the
 * `color-contrast` rule. For a token-driven design system the durable way to
 * machine-verify contrast is to check every text/background token PAIR the
 * screens actually use, in BOTH themes, against the AA thresholds — which is
 * what this test does. It reads tokens.css directly, so a token colour change
 * that drops a pair below AA fails CI deterministically (no browser needed).
 * Since AC-172 it holds both thresholds: 4.5:1 for text (1.4.3) and 3:1 for UI
 * components (1.4.11). The RENDERED check is e2e/rtl-a11y.spec.ts, which runs
 * axe's color-contrast rule on every route in both themes and both languages.
 */
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

// Vitest runs with cwd = the web project root; tokens.css is the design-token SoT.
const css = readFileSync(resolve(process.cwd(), 'src/styles/tokens.css'), 'utf8');

/*
 * Pull `--name: #hex` declarations out of a single CSS rule block.
 *
 * MATCHED AS A RULE HEAD — selector followed by optional whitespace and `{` — not by plain indexOf.
 * indexOf also matches the selector inside PROSE, and tokens.css line 4 documents
 * `[data-theme="dark"]` in its own header comment. So `block('[data-theme="dark"]')` located that
 * comment, took the next `{` — which opens `:root` — and returned the LIGHT palette. Every "dark"
 * contrast case below has therefore been grading the light tokens since this file was written: a
 * whole half of a WCAG gate, green for the wrong reason. Found 2026-08-10 by the sync assertion
 * added for the OS-default palette, which compared dark against dark and got light.
 */
function block(selector: string): Record<string, string> {
  const head = new RegExp(`${selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\s*\\{`);
  const found = head.exec(css);
  if (!found) throw new Error(`contrast.test: no rule found for selector ${selector}`);
  const start = found.index;
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

/*
 * The OS-default dark palette is a SECOND copy of the same tokens, inside
 * `@media (prefers-color-scheme: dark)`, because plain CSS cannot share one declaration block across
 * a media boundary. Two hand-maintained copies of a colour palette drift, and only one of them was
 * reachable by the contrast checks below — so the copy most users would actually see could quietly
 * fall out of AA while CI stayed green.
 *
 * Asserting they are IDENTICAL makes the duplication safe and extends every contrast case below to
 * both blocks at once. Note `block()` matches on first-indexOf, and this selector contains the
 * literal `[data-theme="dark"]` — it only resolves correctly because the media rule is placed AFTER
 * the explicit one in tokens.css. Keep that order.
 */
const OS_DARK = block(':root:not([data-theme="light"]):not([data-theme="dark"])');

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
  // DEF-186: --text-3 was checked only on --surface, where it passed, while the app paints it on every
  // surface below - 147 rendered failures in the light theme. Every surface it sits on is a row now.
  ['text-3', 'subtle', TEXT, 'search keyboard hint, filter hints'],
  ['text-3', 'bg-app', TEXT, 'breadcrumbs, tab counts, page meta'],
  ['text-3', 'sunken', TEXT, 'disabled button label (kept legible though WCAG exempts it)'],
  ['text-3', 'header', TEXT, 'nav sub-labels, header hints'],
  // AC-172's states (normal, hover, focused): the hover fills and the danger button.
  ['primary-fg', 'primary-hover', TEXT, 'primary button, hover'],
  ['text', 'subtle', TEXT, 'secondary button, hover'],
  ['accent', 'primary-tint', TEXT, 'ghost button, hover'],
  ['primary-fg', 'st-danger-dot', TEXT, 'danger button label (DEF-187: #fff on the danger fill)'],
  // DEF-190: retired records and dimmed graph nodes are muted with the subtle fill instead of opacity, so what they
  // hold is paired on it (text, text-2 and text-3 on subtle are above).
  ['accent', 'subtle', TEXT, 'links and keys in a retired ADR, invariant or decision body'],
];

const UI = 3; // UI components and their boundaries (WCAG 1.4.11)

/** Every surface token a border, a focus ring or muted text can sit on. */
const SURFACES = ['surface', 'subtle', 'bg-app', 'sunken', 'header'] as const;
/** The border tokens, from the softest to the strongest weight. */
const BORDERS = ['border-soft', 'border', 'border-strong'] as const;

/*
 * AC-172 (DEC-199 h1): EVERY border token against EVERY surface, at 3:1, in both palettes - plus the focus
 * ring and the focused-input border (normal and focused states of every control). Before DEC-199 the border
 * tokens sat at 1.08-2.01:1; the operator ruled the full restyle over a controls-only token.
 */
const UI_PAIRS: ReadonlyArray<readonly [string, string, number, string]> = [
  ...BORDERS.flatMap((b) => SURFACES.map((s) => [b, s, UI, `a ${b} line on --${s}`] as const)),
  ...SURFACES.map((s) => ['focus', s, UI, 'the focus ring'] as const),
  ...SURFACES.map((s) => ['accent', s, UI, 'a focused input or select border'] as const),
];

/*
 * AC-172 (DEC-199 h2): DISABLED controls follow WCAG 2.2 AA's exemption for inactive user-interface
 * components (SC 1.4.3 and 1.4.11 both exclude them). Recorded here so the absence of a disabled pair reads as
 * a decision, not a gap - the way this file's history says an absence must be proven, not assumed.
 */
const DISABLED_EXEMPT: ReadonlyArray<readonly [string, string]> = [
  ['.btn:disabled', 'inactive component - WCAG 2.2 SC 1.4.3 / 1.4.11 exemption (DEC-199 h2); its --text-3 label is still paired above'],
  ['.choice input:disabled + .choice-box', 'inactive component drawn at 50% opacity - WCAG 2.2 SC 1.4.11 exemption (DEC-199 h2)'],
  ['.toggle input:disabled + .toggle-track', 'inactive component - WCAG 2.2 SC 1.4.11 exemption (DEC-199 h2)'],
  ['.select-trigger:disabled', 'inactive component - WCAG 2.2 SC 1.4.3 / 1.4.11 exemption (DEC-199 h2); its --text-3 label is still paired above'],
];

describe('OS-default dark palette stays in sync with the explicit one', () => {
  it('parsed a real block (guards against a silent indexOf mismatch)', () => {
    // If the selector ever stops matching, `block()` returns {} and the equality check below would
    // pass vacuously — comparing nothing to nothing. Assert it found tokens first.
    expect(Object.keys(OS_DARK).length).toBeGreaterThan(10);
  });

  it('is identical to [data-theme="dark"], so every contrast case covers both', () => {
    expect(OS_DARK).toEqual(block('[data-theme="dark"]'));
  });
});

describe('Token contrast (WCAG 2.2 AA): text and UI components', () => {
  for (const [name, tokens] of [['light', LIGHT], ['dark', DARK]] as const) {
    describe(name, () => {
      for (const [fg, bg, min, where] of [...PAIRS, ...UI_PAIRS]) {
        it(`--${fg} on --${bg} ≥ ${min}:1 (${where})`, () => {
          const f = tokens[fg];
          const b = tokens[bg];
          expect(f, `missing --${fg} in ${name}`).toBeTruthy();
          expect(b, `missing --${bg} in ${name}`).toBeTruthy();
          expect(contrast(f, b), `--${fg} ${f} on --${bg} ${b} in ${name}`).toBeGreaterThanOrEqual(min);
        });
      }
    });
  }
});

describe('AC-172: the border tokens keep their order of weight, and --text-3 its muted level', () => {
  // Weight = contrast against the page surface: soft < normal < strong in BOTH palettes (lighter lines on
  // the light page, darker lines on the dark one), so raising all three to 3:1 did not flatten them.
  for (const [name, tokens] of [['light', LIGHT], ['dark', DARK]] as const) {
    it(`${name}: soft < normal < strong`, () => {
      const [soft, normal, strong] = BORDERS.map((b) => contrast(tokens[b], tokens.surface));
      expect(soft).toBeLessThan(normal);
      expect(normal).toBeLessThan(strong);
    });
    it(`${name}: --text-3 stays quieter than --text-2`, () => {
      expect(contrast(tokens['text-3'], tokens.surface)).toBeLessThan(contrast(tokens['text-2'], tokens.surface));
    });
  }

  it('the UI-component table covers every border on every surface, the focus ring and the focused border', () => {
    // The anti-vacuity guard this file's history demands: a table that lost its rows would pass silently.
    expect(UI_PAIRS).toHaveLength(BORDERS.length * SURFACES.length + 2 * SURFACES.length);
  });
});

describe('AC-172: disabled controls follow WCAG 2.2 AA exemption for inactive components (DEC-199 h2)', () => {
  it('records each exempt disabled state, with the reason, and each still exists in the stylesheet', () => {
    const sheets = ['forms.css', 'components.css'].map((f) => readFileSync(resolve(process.cwd(), 'src/styles', f), 'utf8')).join('\n');
    expect(DISABLED_EXEMPT.length).toBeGreaterThan(0);
    for (const [selector, reason] of DISABLED_EXEMPT) {
      expect(reason, selector).toMatch(/WCAG 2\.2 SC 1\.4\.(3|11)/);
      expect(sheets.includes(selector), `${selector} is no longer in forms.css/components.css - update the exemption list`).toBe(true);
    }
  });
});
