// apply-i18n-term-slate.mjs — WBS-40.1 / DW-069 step (b) → (c): turn the operator's rulings into the glossary.
//
// Reads the filled slate (tamheed-package/docs/i18n-term-slate.md — every `Ruling:` line is the OPERATOR's,
// DEC-156 d3 / DEC-169) and writes:
//   * src/Acmp.Web/src/i18n/glossary.json — each section-A term gets its ruled Arabic (`ar`: one string for a
//     canonical ruling, an array for an allowed-variants ruling), `status: ruled`, and the ruling text; each
//     section-B label becomes an `allowedVariants` entry;
//   * src/Acmp.Web/src/i18n/locales/ar.json — ONLY where a ruling says a listed rendering is wrong: a
//     `canonical:` ruling replaces every other listed rendering with the canonical one on the keys the slate
//     names, and a `defect: X — replace with Y - …` clause replaces X with Y on X's keys. Nothing else in the
//     bundle is touched.
//
// ⛔ EVERY ARABIC STRING WRITTEN HERE IS COPIED OUT OF THE SLATE. None is typed in this file, and the script
// refuses a ruling that names a rendering the slate does not list for that term (a typo in a ruling must fail,
// not silently become a new translation). The slate's header hashes are checked against the bundles it was
// generated from, so rulings are never applied over a different corpus than the one they were taken on.
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { I18N, ROOT, loadBundles } from './lib/i18n-terms.mjs';

const slatePath = resolve(ROOT, 'tamheed-package/docs/i18n-term-slate.md');
const sha = (s) => createHash('sha256').update(s).digest('hex').slice(0, 12);
const { enRaw, arRaw, glossaryRaw, ar, glossary } = loadBundles();
const slate = readFileSync(slatePath, 'utf8');

// The slate names the corpus it was generated from; refuse to apply over anything else (DEC-168 r3).
const hashes = [...slate.matchAll(/sha256 ([0-9a-f]{12})/g)].map((m) => m[1]);
if (hashes.length !== 3 || hashes[0] !== sha(enRaw) || hashes[1] !== sha(arRaw) || hashes[2] !== sha(glossaryRaw)) {
  console.error(`slate header hashes ${hashes.join('/')} do not match the bundles on disk (${sha(enRaw)}/${sha(arRaw)}/${sha(glossaryRaw)}) — regenerate and re-rule, or apply on the corpus the rulings were taken on`);
  process.exit(1);
}

// Parse the slate: sections A/B, one term per `### `, renderings as `- \`ar\` — keys` (indented in A), one Ruling.
const terms = [];
let section = null;
let cur = null;
for (const line of slate.split('\n')) {
  if (line.startsWith('## A.')) section = 'A';
  else if (line.startsWith('## B.')) section = 'B';
  else if (line.startsWith('### ')) {
    cur = { label: line.slice(4).replace(/^`|`$/g, ''), section, renderings: new Map(), ruling: null };
    terms.push(cur);
  } else if (cur && /^\s*- `/.test(line) && !line.trimStart().startsWith('- label')) {
    const rendering = line.split('`')[1];
    const keys = line.slice(line.indexOf('` — ') + 4).split(', ').map((k) => k.trim()).filter(Boolean);
    cur.renderings.set(rendering, [...(cur.renderings.get(rendering) ?? []), ...keys]);
  } else if (cur && line.startsWith('Ruling:')) cur.ruling = line.slice('Ruling:'.length).trim();
}

const unruled = terms.filter((t) => !t.ruling);
if (unruled.length) {
  console.error(`refusing: ${unruled.length} term(s) have no ruling — ${unruled.map((t) => t.label).join(', ')}`);
  process.exit(1);
}

const setPath = (obj, path, value) => {
  const parts = path.split('.');
  let o = obj;
  for (const p of parts.slice(0, -1)) o = o[p];
  if (typeof o[parts.at(-1)] !== 'string') throw new Error(`${path} is not a string leaf in ar.json`);
  o[parts.at(-1)] = value;
};

const bundleEdits = [];
const rulings = new Map();
for (const t of terms) {
  const listed = [...t.renderings.keys()];
  const has = (s) => t.renderings.has(s);
  const allowed = [];
  let canonical = null;
  // Split on `;` only where a clause keyword follows: a note may itself contain a semicolon.
  for (const clause of t.ruling.split(/;\s*(?=canonical:|allowed:|defect:|skip\b)/).map((c) => c.trim()).filter(Boolean)) {
    // The LAST em dash separates the clause from its note: a rendering may itself contain one
    // (`إمكانية التتبّع — بالاتجاهين`), a note written by the applier's own vocabulary never does.
    const cut = clause.lastIndexOf(' — ');
    const head = cut < 0 ? clause : clause.slice(0, cut);
    const note = cut < 0 ? '' : clause.slice(cut + 3);
    if (head.startsWith('canonical:')) {
      canonical = head.slice('canonical:'.length).trim().replace(/\s*\(.*\)$/, '');
      if (listed.length && !has(canonical)) throw new Error(`${t.label}: canonical "${canonical}" is not a listed rendering`);
      allowed.push(canonical);
    } else if (head.startsWith('allowed:')) {
      for (const a of head.slice('allowed:'.length).split('|').map((s) => s.trim())) {
        if (!has(a)) throw new Error(`${t.label}: allowed "${a}" is not a listed rendering`);
        allowed.push(a);
      }
    } else if (head.startsWith('defect:')) {
      const bad = head.slice('defect:'.length).trim();
      if (!has(bad)) throw new Error(`${t.label}: defect "${bad}" is not a listed rendering`);
      const m = note.match(/replace with (.+?) - /);
      if (m) {
        const good = m[1].trim();
        for (const k of t.renderings.get(bad)) bundleEdits.push([k, bad, good]);
        allowed.push(good);
      }
    } else if (!head.startsWith('skip')) throw new Error(`${t.label}: unknown clause "${clause}"`);
  }
  if (canonical) for (const r of listed) if (r !== canonical && !allowed.includes(r)) for (const k of t.renderings.get(r)) bundleEdits.push([k, r, canonical]);
  rulings.set(t.label, { section: t.section, allowed: [...new Set(allowed)], canonical, text: t.ruling });
}

// glossary.json: section-A rulings onto the terms; section-B rulings into allowedVariants.
const byLabel = new Map();
for (const t of glossary.terms) for (const n of [t.en, ...(t.aliases ?? [])]) byLabel.set(n.toLowerCase(), t);
for (const [label, r] of rulings) {
  if (r.section !== 'A') continue;
  const t = byLabel.get(label.toLowerCase());
  if (!t) throw new Error(`section A term "${label}" is not in glossary.json`);
  if (t.status === 'canonical') continue; // the two preset terms keep their authority
  t.ar = r.canonical && r.allowed.length === 1 ? r.canonical : r.allowed;
  t.status = 'ruled';
  t.ruling = r.text;
}
glossary.allowedVariants = [...rulings]
  .filter(([, r]) => r.section === 'B')
  .map(([label, r]) => ({ en: label, ar: r.allowed, ruling: r.text }));
const unruledTerms = glossary.terms.filter((t) => t.status === 'proposed');
if (unruledTerms.length) throw new Error(`glossary still has proposed terms: ${unruledTerms.map((t) => t.en).join(', ')}`);

writeFileSync(resolve(I18N, 'glossary.json'), JSON.stringify(glossary, null, 2) + '\n', 'utf8');
for (const [key, from, to] of bundleEdits) setPath(ar, key, to);
if (bundleEdits.length) writeFileSync(resolve(I18N, 'locales/ar.json'), JSON.stringify(ar, null, 2) + '\n', 'utf8');
console.log(`glossary.json: ${glossary.terms.length} terms ruled, ${glossary.allowedVariants.length} allowedVariants; ar.json edits: ${bundleEdits.length}`);
for (const [key, from, to] of bundleEdits) console.log(`  ${key}: ${from} -> ${to}`);
