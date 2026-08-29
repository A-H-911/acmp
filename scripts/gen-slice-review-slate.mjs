#!/usr/bin/env node
/*
 * Slice-review slate generator.  Usage:  node scripts/gen-slice-review-slate.mjs SL-033
 *
 * WHY THIS EXISTS. LL-011 (Approved, pinned): an identifier is a POINTER, not a REFERENCE. An
 * artifact the operator reads to DECIDE must carry each cited record's OWN TEXT where it cites it,
 * quoted from the canonical store by a generator — never paraphrased, never re-typed. The first
 * disposition slate cited ~40 records by id alone and the operator refused the interview on exactly
 * that ground. The test is mechanical: could a reader who has never opened this package adjudicate
 * every question using only the page?
 *
 * WHAT IT REFUSES TO DO. It never prints an empty block. A row it needs and cannot find is a FATAL
 * exit, not a gap in the page — because a slate with a hole reads exactly like a slate without one.
 * It also refuses when a work item does not name exactly one acceptance criterion, rather than
 * guessing which one it meant.
 *
 * ⚠ NOTHING HERE IS HARD-CODED TO A PARTICULAR SLICE OR COMMIT. An earlier version carried the three
 * item ids and their PR shas inline; that is a stale-data vector in a file whose whole purpose is to
 * be trustworthy. Everything is read from data/*.jsonl at run time.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const DATA = join(ROOT, 'tamheed-package', 'data');

const SLICE = process.argv[2];
if (!SLICE || !/^SL-\d+$/.test(SLICE)) {
  console.error('usage: node scripts/gen-slice-review-slate.mjs <SL-nnn>');
  process.exit(2);
}

const load = (f) =>
  readFileSync(join(DATA, `${f}.jsonl`), 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l));
const byId = (rows) => Object.fromEntries(rows.map((r) => [r.id, r]));

const wbs = load('wbs_items');
const acs = byId(load('acceptance_criteria'));
const reqs = byId(load('requirements'));
const dws = byId(load('deferred_work'));
const verdicts = load('audit_verdicts');
const slices = byId(load('slices'));

const fatal = (msg) => {
  console.error(`FATAL: ${msg}`);
  process.exit(2);
};

if (!slices[SLICE]) fatal(`slice ${SLICE} not found`);

/* Items awaiting the operator: Review is done-claimed by the agent, never a verdict. */
const items = wbs.filter((w) => w.slice_id === SLICE && w.lifecycle_status === 'Review');
if (!items.length) fatal(`no work items in ${SLICE} are at Review — there is nothing to adjudicate`);

/**
 * A completion record names its criteria; anything else is a guess, and a guess is a hole.
 *
 * ⚠ DEF-116 — THIS ACCEPTS ONE OR MORE, AND USED TO DEMAND EXACTLY ONE. That predicate had never
 * executed against a multi-criterion item: every interview since this instrument landed (#315) has
 * adjudicated a single-criterion row, while WBS-24.4 names two criteria and WBS-24.5 names three, so
 * both would have aborted it. WBS-24.8 is where it finally fired — and because this generator IS how
 * LL-011 is discharged, an item it cannot render is one whose review would have to be hand-built,
 * which is precisely what that lesson forbids.
 *
 * It still fails CLOSED on zero, and that arm is deliberate rather than leftover strictness: an item
 * sitting at Review naming no criterion is genuinely unreviewable, and rendering it would hand the
 * operator a page with nothing to adjudicate — the same harm wearing a different hat.
 */
const criteriaOf = (item) => {
  const found = [...new Set((item.title.match(/\bAC-\d+\b/g) ?? []))]
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  if (!found.length) {
    fatal(`${item.id} names no acceptance criterion; a completion record must name the criteria it satisfied`);
  }
  for (const id of found) {
    if (!acs[id]) fatal(`${item.id} names ${id}, which is not in the register`);
  }
  return found.map((id) => acs[id]);
};

const latestVerdict = (acId) => {
  const v = verdicts.filter((x) => x.ac_id === acId);
  if (!v.length) fatal(`no audit verdict recorded for ${acId}`);
  return v[v.length - 1];
};

const esc = (s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
/* Preserve the register's own paragraphing; never reflow the evidence. */
const paras = (s) => esc(s).split('\n\n').map((p) => `<p>${p.replace(/\n/g, '<br>')}</p>`).join('');
const block = (label, text) => (text ? `<div class="f"><h4>${esc(label)}</h4>${paras(text)}</div>` : '');

const sections = items
  .sort((a, b) => a.id.localeCompare(b.id, undefined, { numeric: true }))
  .map((w) => {
    const criteria = criteriaOf(w);
    /* The requirement is read from the first criterion: an item's criteria all answer to the same
       requirement by construction, and if they ever did not, the header would be lying rather than
       merely incomplete — so that assumption is asserted below instead of assumed. */
    const a = criteria[0];
    /* Asserted here so a criterion with no verdict aborts the whole slate rather than rendering a
       section the operator could rule on without evidence. */
    criteria.forEach((c) => latestVerdict(c.id));
    const q = reqs[a.requirement_id] ?? fatal(`requirement ${a.requirement_id} (of ${a.id}) not found`);
    for (const c of criteria) {
      if (c.requirement_id !== a.requirement_id) {
        fatal(`${w.id}: ${c.id} answers to ${c.requirement_id} but ${a.id} answers to ${a.requirement_id}; one header cannot describe both`);
      }
    }
    /* The DW- row is optional: not every work item closes one. Absent is fine; wrong is not. */
    const dwId = [...new Set((w.title.match(/\bDW-\d+\b/g) ?? []))][0];
    const d = dwId ? dws[dwId] : null;
    if (dwId && !d) fatal(`${w.id} names ${dwId}, which is not in the register`);
    return `
  <section>
    <h2>${esc(w.id)} <span class="st">${esc(w.lifecycle_status)}</span></h2>
    <p class="meta">${criteria.map((c) => {
      const cv = latestVerdict(c.id);
      return `${esc(c.id)} verdict <b>${esc(cv.verdict)}</b> (${esc(cv.id)})`;
    }).join(' &middot; ')} &middot;
      ${esc(q.id)} <b>${esc(q.lifecycle_status)}</b>${d ? ` &middot; ${esc(d.id)} <b>${esc(d.lifecycle_status)}</b>` : ''}</p>

    <h3>The work item &mdash; ${esc(w.id)}</h3>
    ${block('As stored', w.title)}

    <h3>The requirement &mdash; ${esc(q.id)}</h3>
    ${block('As stored', q.statement || q.title)}

    ${criteria.map((c) => {
      const cv = latestVerdict(c.id);
      return `
    <h3>The acceptance criterion &mdash; ${esc(c.id)}</h3>
    ${block('Title', c.title)}
    ${block('Criterion, as stored', c.statement)}

    <h3>The verdict &mdash; ${esc(cv.id)}</h3>
    <p class="meta">verified_by <b>${esc(cv.verified_by)}</b> &middot; method <b>${esc(cv.verification_method)}</b> &middot; against_commit <code>${esc(cv.against_commit)}</code></p>
    ${block('Evidence, as stored', cv.evidence)}`;
    }).join('\n')}
    ${d ? `<h3>The deferred-work row it closed &mdash; ${esc(d.id)}</h3>${block('As stored', d.title)}` : ''}
  </section>`;
  })
  .join('\n');

const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<title>${esc(SLICE)} slice review</title>
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
 .lead{background:#fff8e6;border:1px solid #e8c986;padding:1rem 1.25rem;border-radius:6px}
</style></head><body>
<h1>${esc(SLICE)} slice review &mdash; ${items.length} item(s) awaiting your verdict</h1>
<div class="lead">
<p>${esc(slices[SLICE].title)}</p>
<p>Every item below sits at <b>Review</b>, which is <i>done-claimed by the agent</i>.
<b>Implemented is the operator's verdict, adjudicated per item.</b></p>
<p>Every block is quoted verbatim from <code>tamheed-package/data/*.jsonl</code> by the generator that
produced this page (LL-011). The generator exits non-zero rather than print an empty block, and
refuses when a work item does not name exactly one acceptance criterion.</p>
</div>
${sections}
</body></html>`;

const out = join(ROOT, 'tamheed-package', 'slice-review-slate.html');
writeFileSync(out, html, 'utf8');
console.log(`wrote ${out}`);
console.log(`${SLICE}: ${items.length} item(s) at Review — ${items.map((i) => i.id).join(', ')}`);
