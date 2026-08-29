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
 * It also refuses an item that names NO acceptance criterion and NO record of why — see criteriaOf.
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
const decs = byId(load('decisions'));
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
 * ⚠ DEF-117 — IT USED TO FAIL CLOSED ON ZERO TOO, ARGUING THAT "an item sitting at Review naming no
 * criterion is genuinely unreviewable". That is a claim about EVERY criterion-less item and it was
 * false within three hours of being written: WBS-25.1 merged as #325 with a measured size and CVE
 * delta, a deciding DEC- row and two residual DW- rows, and names no criterion ON PURPOSE, because
 * DW-090 records that NFR-054's verification method names a CI check that does not exist. The only
 * thing absent is the criterion; the REASON for its absence is itself recorded and adjudicable.
 *
 * So zero is now renderable — but only when the reason is IN THE STORE. An item naming no criterion
 * and no DW-/DEC- row that could carry the reason really is a page with nothing on it, and that is
 * the true statement the old comment over-generalised from. Returning [] here is not a relaxation:
 * the caller re-checks and still exits non-zero when nothing explains the absence.
 */
const criteriaOf = (item) => {
  const found = [...new Set((item.title.match(/\bAC-\d+\b/g) ?? []))]
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  for (const id of found) {
    if (!acs[id]) fatal(`${item.id} names ${id}, which is not in the register`);
  }
  return found.map((id) => acs[id]);
};

/* Every row of a named family the item cites, in citation order, skipping ids the register does not
   hold — an unresolvable id is a typo in prose, not a missing record, and fatal() belongs to the
   rows the page structurally needs. */
const citedRows = (item, prefix, table) =>
  [...new Set((item.title.match(new RegExp(`\\b${prefix}-\\d+\\b`, 'g')) ?? []))]
    .map((id) => table[id])
    .filter(Boolean);

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
    const criterionLess = criteria.length === 0;
    /* Asserted here so a criterion with no verdict aborts the whole slate rather than rendering a
       section the operator could rule on without evidence. */
    criteria.forEach((c) => latestVerdict(c.id));

    /* DEF-117's fail-closed half. A criterion-less item is adjudicable only if the store carries the
       reason; with neither a DW- nor a DEC- row to quote there is genuinely nothing on the page. */
    const decsCited = criterionLess ? citedRows(w, 'DEC', decs) : [];
    const dwsCited = citedRows(w, 'DW', dws);
    if (criterionLess && !dwsCited.length && !decsCited.length) {
      fatal(`${w.id} names no acceptance criterion and no DW-/DEC- row that records why; nothing here can be adjudicated`);
    }

    /* ⚠ DEF-119 — THIS USED TO READ THE REQUIREMENT FROM THE FIRST CRITERION AND ABORT IF THE OTHERS
       DISAGREED ("one header cannot describe both"). That guard protected the TEMPLATE's assumption
       that one header sufficed, never a real invariant, and it left DEF-116 half-fixed: WBS-24.5 is
       one of the two rows DEF-116 was filed about, and its three criteria answer to three different
       requirements (FR-155, NFR-059, NFR-060), so the widened predicate still aborted on it. DEF-116
       was verified against WBS-24.8, whose two criteria happen to share one requirement, so the guard
       never fired. Render one section per DISTINCT requirement instead; a criterion-less item has no
       criterion to read one from, so its own citation is the only source. */
    const reqIds = criterionLess
      ? [...new Set((w.title.match(/\b(?:FR|NFR)-\d+\b/g) ?? []))].slice(0, 1)
      : [...new Set(criteria.map((c) => c.requirement_id))];
    if (!reqIds.length) fatal(`${w.id} names no requirement and carries no criterion that names one`);
    const qs = reqIds.map((id) => reqs[id] ?? fatal(`requirement ${id} (of ${w.id}) not found`));
    /* The DW- row is optional: not every work item closes one. Absent is fine; wrong is not.
       A criterion-less item gets EVERY cited row, because the reason may sit in any of them. */
    const d = criterionLess ? null : dwsCited[0];
    return `
  <section>
    <h2>${esc(w.id)} <span class="st">${esc(w.lifecycle_status)}</span></h2>
    <p class="meta">${criterionLess
      ? '<b>no acceptance criterion</b>'
      : criteria.map((c) => {
          const cv = latestVerdict(c.id);
          return `${esc(c.id)} verdict <b>${esc(cv.verdict)}</b> (${esc(cv.id)})`;
        }).join(' &middot; ')} &middot;
      ${qs.map((q) => `${esc(q.id)} <b>${esc(q.lifecycle_status)}</b>`).join(' &middot; ')}${d ? ` &middot; ${esc(d.id)} <b>${esc(d.lifecycle_status)}</b>` : ''}</p>

    ${criterionLess ? `<div class="lead"><p><b>This item carries NO acceptance criterion, and that is
    deliberate rather than missing.</b> Nothing below is a verdict record, so there is no <code>Met</code>
    to lean on: you are adjudicating the completion record itself, and the recorded reason for the
    criterion's absence, both quoted verbatim from the store. If that reason does not persuade you, the
    right outcome is to leave the item at <b>Review</b> and say what evidence would settle it.</p></div>` : ''}

    <h3>The work item &mdash; ${esc(w.id)}</h3>
    ${block('As stored', w.title)}

    ${qs.map((q) => `
    <h3>The requirement &mdash; ${esc(q.id)}</h3>
    ${block('As stored', q.statement || q.title)}`).join('\n')}

    ${criteria.map((c) => {
      const cv = latestVerdict(c.id);
      return `
    <h3>The acceptance criterion &mdash; ${esc(c.id)} <span class="meta">(answers to ${esc(c.requirement_id)})</span></h3>
    ${block('Title', c.title)}
    ${block('Criterion, as stored', c.statement)}

    <h3>The verdict &mdash; ${esc(cv.id)}</h3>
    <p class="meta">verified_by <b>${esc(cv.verified_by)}</b> &middot; method <b>${esc(cv.verification_method)}</b> &middot; against_commit <code>${esc(cv.against_commit)}</code></p>
    ${block('Evidence, as stored', cv.evidence)}`;
    }).join('\n')}
    ${d ? `<h3>The deferred-work row it closed &mdash; ${esc(d.id)}</h3>${block('As stored', d.title)}` : ''}
    ${criterionLess ? dwsCited.map((r) => `
    <h3>Deferred-work row it cites &mdash; ${esc(r.id)} <span class="st">${esc(r.lifecycle_status)}</span></h3>
    ${block('As stored', r.title)}
    ${block('Activation trigger, as stored', r.activation_trigger)}`).join('\n') : ''}
    ${criterionLess ? decsCited.map((r) => `
    <h3>The deciding decision &mdash; ${esc(r.id)} <span class="st">${esc(r.lifecycle_status)}</span></h3>
    ${block('Title, as stored', r.title)}
    ${block('Decision, as stored', r.decision)}
    ${block('Rationale, as stored', r.rationale)}`).join('\n') : ''}
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
produced this page (LL-011). The generator exits non-zero rather than print an empty block. An item may
name any number of acceptance criteria, including none &mdash; but a criterion-less item is refused
unless the store also carries a <code>DW-</code> or <code>DEC-</code> row recording why, which is then
quoted in full below it.</p>
</div>
${sections}
</body></html>`;

const out = join(ROOT, 'tamheed-package', 'slice-review-slate.html');
writeFileSync(out, html, 'utf8');
console.log(`wrote ${out}`);
console.log(`${SLICE}: ${items.length} item(s) at Review — ${items.map((i) => i.id).join(', ')}`);
