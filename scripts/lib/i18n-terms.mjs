// i18n-terms.mjs — the one pairing the slate generator, the slate applier and the term check all share
// (WBS-40.1 / DW-069). DW-069's method, restated so every instrument counts the same way: pair every key
// present in BOTH bundles, keep English labels of MAX_WORDS words or fewer, group by the English string,
// and list the distinct Arabic renderings with the keys that carry each.
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
export const I18N = resolve(ROOT, 'src/Acmp.Web/src/i18n');
export const MAX_WORDS = 4; // DW-069's "short label" bound

export const flat = (o, p = '', acc = {}) => {
  for (const [k, v] of Object.entries(o)) {
    const key = p ? `${p}.${k}` : k;
    if (v && typeof v === 'object' && !Array.isArray(v)) flat(v, key, acc);
    else acc[key] = String(v);
  }
  return acc;
};

export function loadBundles() {
  const enRaw = readFileSync(resolve(I18N, 'locales/en.json'), 'utf8');
  const arRaw = readFileSync(resolve(I18N, 'locales/ar.json'), 'utf8');
  const glossaryRaw = readFileSync(resolve(I18N, 'glossary.json'), 'utf8');
  return { enRaw, arRaw, glossaryRaw, en: JSON.parse(enRaw), ar: JSON.parse(arRaw), glossary: JSON.parse(glossaryRaw) };
}

/** label -> Map<arabic rendering, sorted keys[]>, over keys present in both bundles, short labels only. */
export function groupByLabel(en, ar) {
  const E = flat(en);
  const A = flat(ar);
  const paired = Object.keys(E).filter((k) => k in A).sort();
  const groups = new Map();
  for (const k of paired) {
    const label = E[k].trim();
    if (!label || label.split(/\s+/).length > MAX_WORDS) continue;
    const m = groups.get(label) ?? new Map();
    const rendering = A[k].trim();
    m.set(rendering, [...(m.get(rendering) ?? []), k]);
    groups.set(label, m);
  }
  return { paired, groups };
}

/** The Arabic renderings a glossary term allows, as an array (a canonical term has one). */
export const termArabic = (t) => (Array.isArray(t.ar) ? t.ar : t.ar ? [t.ar] : []);

/** Every English label a glossary term covers, lower-cased. */
export const termLabels = (t) => [t.en, ...(t.aliases ?? [])].map((s) => s.toLowerCase());
