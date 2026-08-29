#!/usr/bin/env node
/*
 * Deferred-work disposition slate.  Usage:  node scripts/gen-dw-disposition-slate.mjs DW-086 DW-087 …
 * With no arguments it selects every `Open` row that NO decision has ever named.
 *
 * WHY THIS EXISTS. LL-011 (Approved, pinned): an identifier is a POINTER, not a REFERENCE. The first
 * disposition slate cited ~40 records by id alone and THE OPERATOR REFUSED THE INTERVIEW on exactly that
 * ground — an id is an index into a store the reader may not have open, so citing one hands the retrieval
 * work to the person the artifact exists to serve, at the moment they are deciding. The test is mechanical:
 * could a reader who has never opened this package adjudicate every question using only this page?
 *
 * DEF-116 established the companion rule: an item this kind of generator cannot render is one whose review
 * would have to be hand-built, which is the very harm LL-011 forbids. So this fails LOUDLY rather than
 * printing a partial page.
 *
 * WHAT IT PRINTS AND WHY EACH PART EARNS ITS PLACE:
 *   - the row's own title and activation_trigger, VERBATIM from data/deferred_work.jsonl — never summarised.
 *     A summary of a row is how DEC-064 d2's failure happened (the summary dropped half of what the row said).
 *   - every decision, ADR, scope-change and open-question that NAMES the row, with its status — so the
 *     operator can see whether they have ruled on this before (LL-002: run the interview every time, but do
 *     not re-ask a settled question without saying so).
 *   - an explicit PRIOR RULING banner when no decision has ever named the row, because "nobody has judged
 *     this" is the single most decision-relevant fact about an Open row and nothing else surfaces it.
 *
 * ⚠ IT ASSERTS PROVENANCE, NOT CORRECTNESS. Every quoted field is read from the canonical JSONL in this
 * process; nothing is re-typed, so LL-001's untrusted-transport risk is absent by construction rather than
 * mitigated. Whether the row's CLAIM is true is what the operator is being asked.
 *
 * ⚠ Paths resolve from this file's own location, never cwd (trap 29).
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const DATA = join(ROOT, 'tamheed-package', 'data');

const load = (f) =>
  readFileSync(join(DATA, `${f}.jsonl`), 'utf8')
    .split('\n')
    .filter(Boolean)
    .map((l) => JSON.parse(l));

const fatal = (m) => {
  console.error(`FATAL: ${m}`);
  process.exit(2);
};

const dw = load('deferred_work');
const byId = Object.fromEntries(dw.map((r) => [r.id, r]));

/* Registers searched for a mention of the row. A decision that NAMES a row is the only evidence in this
   store that a human has considered it — there is no "reviewed" field, which is why deferred-work-reviewed
   can never go green from reviewing. */
const REGS = [
  ['decision', load('decisions')],
  ['adr', load('adrs')],
  ['scope-change', load('scope_changes')],
  ['open-question', load('open_questions')],
];

const namedBy = (id) => {
  const out = [];
  for (const [kind, rows] of REGS) {
    for (const r of rows) {
      if (JSON.stringify(r).includes(id)) {
        out.push({ kind, id: r.id, status: r.lifecycle_status });
      }
    }
  }
  return out;
};

const ids = process.argv.slice(2);
if (!ids.length) {
  /* ⚠ LL-006 — A PROXY IS NOT THE ARTIFACT, AND THIS ONE WAS MEASURED AND REJECTED RATHER THAN REASONED
     ABOUT. The obvious auto-selection is "every Open row that no decision names", on the theory that a
     decision naming a row is the only evidence a human considered it. Run against this store it returns
     THIRTY-FIVE rows — and thirty-four of them WERE dispositioned, on 2026-08-20, by an interview that
     confirmed every then-open row in BULK and therefore named none of them individually.
     So the proxy does not measure "undispositioned"; it measures "not individually cited", and the two
     diverge by an entire register. There is no column that answers the real question, so this refuses to
     guess: a page listing thirty-four settled rows as though they awaited judgement is worse than no page,
     and DEF-116's rule is that this generator fails closed rather than rendering something misleading. */
  fatal(
    'no ids given, and this cannot be inferred. "Open" does NOT mean "never ruled on": the 2026-08-20\n' +
      '       interview confirmed every then-open row in bulk, naming none of them, so a decision-mention\n' +
      '       test reports 35 rows of which 34 are settled. Pass the ids you mean, chosen by reading the\n' +
      '       decision register for the period since a row was filed.'
  );
}

for (const id of ids) if (!byId[id]) fatal(`${id} is not in the deferred-work register`);

const esc = (s) =>
  String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
/* Preserve the register's own paragraphing; never reflow the evidence. */
const paras = (s) =>
  esc(s)
    .split('\n\n')
    .map((p) => `<p>${p.replace(/\n/g, '<br>')}</p>`)
    .join('');
const block = (label, text) =>
  text ? `<div class="f"><h4>${esc(label)}</h4>${paras(text)}</div>` : '';

const sections = ids
  .map((id) => {
    const r = byId[id];
    const prior = namedBy(id);
    let attrs = '';
    if (r.custom_attributes) {
      try {
        attrs = Object.entries(JSON.parse(r.custom_attributes))
          .map(([k, v]) => `<div class="f"><h4>${esc(k)}</h4><p>${esc(v)}</p></div>`)
          .join('');
      } catch {
        attrs = block('custom_attributes (not JSON)', r.custom_attributes);
      }
    }
    return `
  <section>
    <h2>${esc(r.id)} <span class="st">${esc(r.lifecycle_status)}</span>
        <span class="sev">${esc(r.severity)}</span></h2>

    ${
      prior.length
        ? `<p class="meta">Previously named by: ${prior
            .map((p) => `${esc(p.id)} <b>${esc(p.status)}</b>`)
            .join(' &middot; ')} &mdash; read those before ruling again.</p>`
        : `<div class="warn"><b>NO DECISION HAS EVER NAMED THIS ROW.</b> It was filed by an executing
           agent and has never been put to you. Carrying it is a legitimate outcome &mdash; but it should
           be a recorded judgement rather than an omission.</div>`
    }

    <h3>The row, as stored</h3>
    ${block('title', r.title)}

    <h3>Its activation trigger, as stored</h3>
    ${block('activation_trigger', r.activation_trigger)}

    ${attrs ? `<h3>How it was found</h3>${attrs}` : ''}

    <h3>Your options</h3>
    <ul>
      <li><b>Activate</b> &mdash; it becomes live work and needs a <code>WBS-</code> row in a slice, or it
          is invisible to every readiness view (<code>DEF-087</code>'s shape).</li>
      <li><b>Carry Open</b> &mdash; the trigger has not fired. This is a ruling, not a deferral of one.</li>
      <li><b>Close Won't-do</b> &mdash; the gap is accepted permanently, with the reasoning recorded.</li>
    </ul>
  </section>`;
  })
  .join('\n');

const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<title>Deferred-work disposition slate</title>
<style>
 body{font:15px/1.6 system-ui,sans-serif;max-width:56rem;margin:2rem auto;padding:0 1.25rem;color:#16202b}
 h1{font-size:1.6rem;border-bottom:2px solid #16202b;padding-bottom:.4rem}
 h2{font-size:1.25rem;margin-top:2.5rem;border-top:1px solid #d7dde4;padding-top:1.25rem}
 h3{font-size:.95rem;text-transform:uppercase;letter-spacing:.05em;color:#5a6b7d;margin:1.5rem 0 .4rem}
 h4{font-size:.85rem;color:#5a6b7d;margin:.9rem 0 .2rem;font-weight:600}
 .f{background:#f5f7f9;border-left:3px solid #c3ccd6;padding:.6rem .9rem;margin:.5rem 0;border-radius:0 4px 4px 0}
 .f p{margin:.5rem 0}.meta{color:#5a6b7d;font-size:.9rem}
 code{background:#eceff2;padding:.1rem .3rem;border-radius:3px;font-size:.9em}
 .st{font-size:.7rem;vertical-align:middle;padding:.15rem .5rem;border-radius:10px;background:#fde7c4;color:#7a4c00}
 .sev{font-size:.7rem;vertical-align:middle;padding:.15rem .5rem;border-radius:10px;background:#e6eaf0;color:#3d4a58}
 .lead{background:#fff8e6;border:1px solid #e8c986;padding:1rem 1.25rem;border-radius:6px}
 .warn{background:#fdecec;border:1px solid #e0a3a3;padding:.7rem 1rem;border-radius:6px;margin:.6rem 0}
</style></head><body>
<h1>Deferred-work disposition &mdash; ${ids.length} row(s) awaiting your judgement</h1>
<div class="lead">
<p>Every block below is quoted <b>verbatim</b> from <code>tamheed-package/data/deferred_work.jsonl</code> by
the generator that produced this page, never summarised &mdash; a summary of a row is how a decision once
dropped half of what its row said. Nothing here is re-typed, so the text you are reading is the text the
store holds.</p>
<p><b>Carrying a row is a legitimate ruling</b> and the <code>deferred-work-reviewed</code> advisory will
stay red either way: it selects <code>Open</code>, <code>Activated</code> and <code>Scheduled</code> alike
and has no "reviewed" field, so only <i>closing</i> a row removes it. A review's deliverable is the
recorded judgement, not a colour change.</p>
</div>
${sections}
</body></html>`;

const out = join(ROOT, 'tamheed-package', 'dw-disposition-slate.html');
writeFileSync(out, html, 'utf8');
console.log(`wrote ${out}`);
console.log(`${ids.length} row(s): ${ids.join(', ')}`);
for (const id of ids) {
  const p = namedBy(id);
  console.log(`  ${id}: ${p.length ? `named by ${p.map((x) => x.id).join(', ')}` : 'NEVER NAMED by any decision'}`);
}
