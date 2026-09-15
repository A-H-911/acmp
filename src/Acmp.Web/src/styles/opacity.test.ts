/*
 * AC-173 (DEF-188, DEF-190): NO STYLESHEET DIMS CONTENT WITH `opacity`.
 *
 * Group opacity fades an element's text together with its background, so a muted line that meets AA on its own
 * token pair stops meeting it the moment its container is dimmed - and no token-pair test can see that, because
 * the tokens never change. The design used the device for retired records, inactive tiles and de-highlighted graph
 * nodes; the sweep found one of them at 2.91:1 (DEF-188) and a scan found six more (DEF-190). They are muted with
 * tokens now, and this test keeps the device from coming back.
 *
 * What is still allowed, and why: a DISABLED control (WCAG 2.2 exempts inactive components, DEC-199 h2); a HOVER or
 * DRAG state (transient, not the resting look); an animation KEYFRAME (a pulse or shimmer, not a resting state); a
 * decorative ICON (not text). Anything else with 0 < opacity < 1 fails here by file and selector.
 */
import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';

const SRC = resolve(process.cwd(), 'src');

function cssFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return cssFiles(path);
    return name.endsWith('.css') ? [path] : [];
  });
}

interface OpacityRule {
  file: string;
  selector: string;
  opacity: number;
  inKeyframes: boolean;
}

/** Every `opacity: <number>` declaration with the selector (or keyframe step) of the block it sits in. */
function opacityRules(file: string, source: string): OpacityRule[] {
  const text = source.replace(/\/\*[\s\S]*?\*\//g, '');
  const out: OpacityRule[] = [];
  const heads: string[] = [];
  let buf = '';
  for (const ch of text) {
    if (ch === '{') {
      heads.push(buf.trim());
      buf = '';
    } else if (ch === '}') {
      const selector = heads.pop() ?? '';
      const inKeyframes = [...heads, selector].some((h) => h.startsWith('@keyframes'));
      for (const m of buf.matchAll(/(?:^|;)\s*opacity\s*:\s*([0-9.]+)/g)) {
        out.push({ file, selector, opacity: parseFloat(m[1]), inKeyframes });
      }
      buf = '';
    } else {
      buf += ch;
    }
  }
  return out;
}

const EXEMPT: ReadonlyArray<readonly [RegExp, string]> = [
  [/:disabled|\[disabled\]|\[aria-disabled/, 'disabled control'],
  [/:hover|\.dragging\b/, 'hover or drag state'],
  [/(^|[\s>+~])svg\b|icon\b/, 'decorative icon'],
];

/** Why a rule may dim, or null if it may not. Every part of a selector group must be exempt on its own. */
function exemption(rule: OpacityRule): string | null {
  if (rule.opacity <= 0 || rule.opacity >= 1) return 'not a partial opacity';
  if (rule.inKeyframes) return 'animation keyframe';
  const reasons = rule.selector.split(',').map((part) => EXEMPT.find(([re]) => re.test(part.trim()))?.[1] ?? null);
  return reasons.every((r) => r !== null) ? reasons[0] : null;
}

const RULES = cssFiles(SRC).flatMap((f) => opacityRules(relative(SRC, f).replace(/\\/g, '/'), readFileSync(f, 'utf8')));
const partial = RULES.filter((r) => r.opacity > 0 && r.opacity < 1);

describe('AC-173: no stylesheet dims content with opacity', () => {
  it('found the opacity rules it judges (guards against a scan that read nothing)', () => {
    // The app has a dozen-odd disabled states and two keyframe animations using partial opacity; a scan that
    // finds none has stopped reading the stylesheets, and would pass the assertion below by judging nothing.
    expect(partial.length).toBeGreaterThan(10);
    expect(partial.some((r) => r.inKeyframes)).toBe(true);
    expect(partial.some((r) => exemption(r) === 'disabled control')).toBe(true);
  });

  it('dims nothing outside a disabled control, a hover or drag state, a keyframe or a decorative icon', () => {
    const offenders = partial.filter((r) => exemption(r) === null).map((r) => `${r.file}: ${r.selector} { opacity: ${r.opacity} }`);
    expect(offenders, 'mute with design tokens instead (DEF-190, ADR-0045)').toEqual([]);
  });

  // The six states DEF-190 named, by name, so a regression on any of them reads as that state.
  for (const [file, selector] of [
    ['features/governance/governance.css', '.adr-body.adr-body-muted'],
    ['features/decisions/decisions.css', '.dec-body-muted'],
    ['features/meetings/minutes.css', '.mom-doc-muted'],
    ['features/actions/actions.css', '.act-toggle-on'],
    ['features/voting/voting.css', '.vote-closed-sub'],
    ['features/traceability/graph.css', '.ig-node--dim'],
    ['styles/administration.css', '.adm-tile-muted'],
  ] as const) {
    it(`${selector} (${file}) is muted without opacity`, () => {
      const source = readFileSync(join(SRC, file), 'utf8');
      expect(source.includes(selector), `${selector} is no longer in ${file} - update this list`).toBe(true);
      const dimmed = opacityRules(file, source).filter((r) => r.selector === selector && r.opacity < 1);
      expect(dimmed, `${selector} sets opacity again`).toEqual([]);
    });
  }
});

describe('the opacity guard itself', () => {
  const sample = `
    /* .commented { opacity: .2 } */
    .btn:disabled { opacity: .5; }
    .card:hover { opacity: .8 }
    .kb-card.dragging { opacity: .4; border: 0 }
    .tnode .tnode-icon { opacity: .75 }
    @keyframes pulse { 0%, 100% { opacity: 1 } 50% { opacity: 0.4 } }
    @media (min-width: 10px) { .muted { color: red; opacity: 0.6; } }
    .hidden { opacity: 0 }
    .a:disabled, .b { opacity: .5 }
    .fade { transition: opacity .2s; }
  `;
  const rules = opacityRules('sample.css', sample);

  it('reads selectors, keyframe steps and media-nested rules, and skips comments and transitions', () => {
    expect(rules.map((r) => `${r.selector}=${r.opacity}${r.inKeyframes ? ' (kf)' : ''}`)).toEqual([
      '.btn:disabled=0.5',
      '.card:hover=0.8',
      '.kb-card.dragging=0.4',
      '.tnode .tnode-icon=0.75',
      '0%, 100%=1 (kf)',
      '50%=0.4 (kf)',
      '.muted=0.6',
      '.hidden=0',
      '.a:disabled, .b=0.5',
    ]);
  });

  it('flags exactly the resting, non-exempt dims - including one hiding in a selector group', () => {
    expect(rules.filter((r) => exemption(r) === null).map((r) => r.selector)).toEqual(['.muted', '.a:disabled, .b']);
  });
});
