// check-i18n-terms.mjs — NFR-039's second clause as a CI gate (WBS-40.1 / DW-069 step (c)).
//
// "No term shall have more than one AR translation within the application" is checked against the canonical
// glossary (src/Acmp.Web/src/i18n/glossary.json), never against the bundles alone: a label with two renderings
// is a defect ONLY when the glossary does not allow both. DEC-156 d2 chose full coverage — every short English
// label with more than one Arabic rendering must be covered by a ruled glossary term or a reviewed
// allowedVariants entry — and DEC-169 authored those entries, so this gate is green by construction on the
// day it lands and turns red the first time a translation drifts from what the operator ruled.
//
// Three rules, all failing closed:
//   1. every glossary term is ruled (status canonical or ruled) and carries Arabic;
//   2. on every surface whose English label is a glossary term (or alias), the Arabic is one the term allows;
//   3. every other short label with more than one rendering has an allowedVariants entry listing all of them.
// Plus trap 31: refuse to pass over an implausibly small corpus, and `--self-test` first injects a known
// divergence into an in-memory copy and requires the check to catch it (LL-013: a passing check proves nothing
// until it has been shown to fail on a positive).
import { loadBundles, groupByLabel, termArabic, termLabels } from './lib/i18n-terms.mjs';

const MIN_PAIRED_KEYS = 1000;
const MIN_TERMS = 20;

function violations(en, ar, glossary) {
  const out = [];
  const { paired, groups } = groupByLabel(en, ar);
  if (paired.length < MIN_PAIRED_KEYS) out.push(`only ${paired.length} keys paired (floor ${MIN_PAIRED_KEYS}) — the bundles or the pairing are broken`);
  if (glossary.terms.length < MIN_TERMS) out.push(`only ${glossary.terms.length} glossary terms (floor ${MIN_TERMS})`);

  const termByLabel = new Map();
  for (const t of glossary.terms) {
    if (!['canonical', 'ruled'].includes(t.status) || termArabic(t).length === 0) out.push(`glossary term "${t.en}" is not ruled (status ${t.status}, ar ${JSON.stringify(t.ar)})`);
    for (const l of termLabels(t)) termByLabel.set(l, t);
  }
  const variants = new Map(glossary.allowedVariants.map((v) => [v.en.toLowerCase(), new Set(v.ar)]));

  for (const [label, m] of groups) {
    const term = termByLabel.get(label.toLowerCase());
    if (term) {
      const allowed = new Set(termArabic(term));
      for (const [rendering, keys] of m) if (!allowed.has(rendering)) out.push(`"${label}" (glossary term ${term.en}) renders as "${rendering}" on ${keys.join(', ')} — allowed: ${[...allowed].join(' | ')}`);
      continue;
    }
    if (m.size < 2) continue;
    const allowed = variants.get(label.toLowerCase());
    if (!allowed) out.push(`"${label}" has ${m.size} renderings and no allowedVariants entry: ${[...m].map(([r, k]) => `"${r}" (${k.join(', ')})`).join('; ')}`);
    else for (const [rendering, keys] of m) if (!allowed.has(rendering)) out.push(`"${label}" renders as "${rendering}" on ${keys.join(', ')} — not in its allowedVariants (${[...allowed].join(' | ')})`);
  }
  return out;
}

const { en, ar, glossary } = loadBundles();

if (process.argv.includes('--self-test')) {
  // Inject DEF-037's shape: one surface of a ruled glossary term gets a rendering the term does not allow.
  const { groups } = groupByLabel(en, ar);
  const t = glossary.terms.find((x) => x.status === 'ruled' && termArabic(x).length && groups.has(x.en));
  const target = t && [...groups.get(t.en).values()][0][0];
  if (!target) { console.error('self-test: no surface found for a ruled term'); process.exit(1); }
  const mutated = JSON.parse(JSON.stringify(ar));
  const parts = target.split('.');
  let o = mutated;
  for (const p of parts.slice(0, -1)) o = o[p];
  o[parts.at(-1)] = 'SELF-TEST-DIVERGENCE';
  const caught = violations(en, mutated, glossary).filter((v) => v.includes('SELF-TEST-DIVERGENCE'));
  if (caught.length !== 1) { console.error(`self-test FAILED: injected divergence on ${target} was not reported exactly once (${caught.length})`); process.exit(1); }
  console.log(`self-test ok: injected divergence on ${target} was caught`);
}

const found = violations(en, ar, glossary);
if (found.length) {
  console.error(`check-i18n-terms: ${found.length} violation(s) against glossary.json (NFR-039):`);
  for (const v of found) console.error(`  - ${v}`);
  process.exit(1);
}
const { paired, groups } = groupByLabel(en, ar);
console.log(`check-i18n-terms ok: ${paired.length} keys paired, ${groups.size} short labels, ${glossary.terms.length} glossary terms, ${glossary.allowedVariants.length} allowed-variant entries`);
