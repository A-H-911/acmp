#!/usr/bin/env node
/*
 * Slice-review slate generator.  Usage:  node scripts/gen-slice-review-slate.mjs SL-033
 *                                       node scripts/gen-slice-review-slate.mjs --scan
 *
 * --scan is DW-089's check (WBS-40.23, ruled by DEC-177): it renders nothing and lists every shipped
 * item whose title names no acceptance criterion. See the SCAN block below for its rules.
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
 * be trustworthy. Everything is read at run time from the tamheed exports under
 * tamheed-package/exports/, written by the MCP tool `entity_export` (DEC-135 d1, ruled compliant
 * by DEC-139 d1 — the store read is performed BY the tool and this file quotes its output).
 *
 * ⛔ EXPORT IMMEDIATELY BEFORE GENERATING. An export is a point-in-time copy and a stale slate
 * reads exactly like a current one. The generator refuses a partial or mixed-digest snapshot and
 * prints the exact entity_export call to run; see scripts/lib/package-export.mjs.
 */
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadSnapshot, ExportError } from './lib/package-export.mjs';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');

const SCAN = process.argv[2] === '--scan';
const SLICE = SCAN ? null : process.argv[2];
if (!SCAN && (!SLICE || !/^SL-\d+$/.test(SLICE))) {
  console.error('usage: node scripts/gen-slice-review-slate.mjs <SL-nnn> | --scan');
  process.exit(2);
}

const FAMILIES = [
  'wbs_items',
  'acceptance_criteria',
  'requirements',
  'deferred_work',
  'decisions',
  'audit_verdicts',
  'slices',
];

let snapshot;
try {
  snapshot = loadSnapshot(ROOT, FAMILIES);
} catch (e) {
  if (e instanceof ExportError) {
    console.error(`FATAL: ${e.message}`);
    process.exit(2);
  }
  throw e;
}
const { families, digest: DIGEST } = snapshot;
const byId = (rows) => Object.fromEntries(rows.map((r) => [r.id, r]));

const wbs = families.wbs_items;
const acs = byId(families.acceptance_criteria);
const reqs = byId(families.requirements);
const dws = byId(families.deferred_work);
const decs = byId(families.decisions);
const verdicts = families.audit_verdicts;
const slices = byId(families.slices);

const fatal = (msg) => {
  console.error(`FATAL: ${msg}`);
  process.exit(2);
};

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
 *
 * ⛔ DEF-160 RENDERS AN ITEM'S custom_attributes BUT DELIBERATELY DOES NOT WIDEN THIS TO READ AC- IDS
 * FROM THEM. Doing so would change which criteria the slate treats as verdict-bearing for an item, and
 * no ruling says it should. The title stays the one place a completion record names its criteria;
 * --scan (DEC-177) holds rows to exactly that.
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
/* ⚠ DEF-142, second arm. This scanned `title` ALONE, and for an instrument item that omits the one
   record the operator most needs. WBS-29's title cites DEC-079/115/116/118 — decisions it merely
   REFERENCES — while DEC-134, the decision that COMMISSIONED it, lives in `source_span`, which is
   where this store puts provenance by convention (SC-047 and WBS-30 do the same). The slate therefore
   rendered four decisions and silently dropped the deciding one: a page that looks complete with its
   most load-bearing record missing, which is exactly the "slate with a hole" this file's header calls
   the dangerous shape because it reads exactly like a whole one.
   ⛔ SCOPED NARROWLY ON PURPOSE. `fields` defaults to title-only, so every existing call — including
   the DW- lookup that feeds `d`, the "row it closed" — keeps its current behaviour and no previously
   generated slate changes. Only the criterion-less DEC- lookup opts into source_span. */
const citedRows = (item, prefix, table, fields = ['title']) =>
  [...new Set(
    fields
      .flatMap((f) => (item[f] ?? '').match(new RegExp(`\\b${prefix}-\\d+\\b`, 'g')) ?? []),
  )]
    .map((id) => table[id])
    .filter(Boolean);

const latestVerdict = (acId) => {
  const v = verdicts.filter((x) => x.ac_id === acId);
  if (!v.length) fatal(`no audit verdict recorded for ${acId}`);
  return v[v.length - 1];
};

const byNumericId = (a, b) => a.id.localeCompare(b.id, undefined, { numeric: true });

/* ---- SCAN: DW-089, built by WBS-40.23, ruled by DEC-177 ------------------------------------------
   The generator refuses an item that names no criterion only when somebody asks it for a slate, which
   is after merge and after verdicts; an item nobody reviews is never noticed at all (DW-089). This
   mode checks every shipped item up front instead.
   SUBJECT: every work item at Review or Implemented whose slice has at least one acceptance criterion
   bound (acceptance_criteria.slice_id, any status). Items that never shipped are outside DW-089.
   FINDING (DEC-177 z1): the item's TITLE names no AC-, read by criteriaOf, so the scan and the slate
   cannot drift apart. DW-089's literal reading: a criterion-less item with a recorded DW-/DEC- reason
   is still a finding, and so is a roll-up parent.
   EXIT (DEC-177 z2): 1 on any finding, Implemented rows included; 2 on a fault. Measured over digest
   87ef5e58 on 2026-09-11: 32 subject items, 17 findings. The scan therefore fails on today's register
   by the operator's choice, until those rows are amended.
   ⚠ THE FLOOR tests the SUBJECT count, not the finding count. A scan that finds nothing among zero
   items reads exactly like a clean one (DW-089's own warning), so fewer than SCAN_FLOOR subject items
   is a fault, not a pass. Set at half the 32 measured on 2026-09-11; the subject set only grows as
   items ship, so tripping it means the export or the predicate is wrong. */
const SCAN_FLOOR = 16;
if (SCAN) {
  const boundSlices = new Set(families.acceptance_criteria.map((a) => a.slice_id).filter(Boolean));
  const subject = wbs
    .filter((w) => ['Review', 'Implemented'].includes(w.lifecycle_status) && boundSlices.has(w.slice_id))
    .sort(byNumericId);
  if (subject.length < SCAN_FLOOR) {
    fatal(`scan subject is ${subject.length} item(s), below the floor of ${SCAN_FLOOR} — the export or the predicate is wrong, and a clean result over it would mean nothing`);
  }
  const findings = subject.filter((w) => criteriaOf(w).length === 0);
  console.log(`scanned ${subject.length} item(s) at Review/Implemented in slices with bound acceptance criteria (package digest ${DIGEST})`);
  for (const w of findings) console.log(`FINDING  ${w.id}  ${w.slice_id}  ${w.lifecycle_status}  title names no AC-`);
  console.log(`${findings.length} finding(s)`);
  process.exit(findings.length ? 1 : 0);
}

if (!slices[SLICE]) fatal(`slice ${SLICE} not found`);

/* Items awaiting the operator: Review is done-claimed by the agent, never a verdict. */
const items = wbs.filter((w) => w.slice_id === SLICE && w.lifecycle_status === 'Review');
if (!items.length) fatal(`no work items in ${SLICE} are at Review — there is nothing to adjudicate`);

const esc =(s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
/* Preserve the register's own paragraphing; never reflow the evidence. */
const paras = (s) => esc(s).split('\n\n').map((p) => `<p>${p.replace(/\n/g, '<br>')}</p>`).join('');
const block = (label, text) => (text ? `<div class="f"><h4>${esc(label)}</h4>${paras(text)}</div>` : '');

/* DEF-160: an item's completion record can live in its own custom_attributes (WBS-40.1's did, because
   re-sending a long title by hand is what LL-001 forbids), and this page used to render every column
   but that one. One block per key, each value quoted as stored; a value that is not a string is shown
   as its JSON. A blob that does not parse as a JSON object is quoted whole rather than dropped.
   Nothing is printed for an item with no attributes, so no empty block. A key whose stored value is
   the empty string says so rather than vanishing: block() omits falsy text, and a silently dropped
   key reads exactly like one that was never there. */
const attributeBlocks = (w) => {
  const raw = w.custom_attributes;
  if (!raw) return '';
  let obj = raw;
  if (typeof raw === 'string') {
    try { obj = JSON.parse(raw); } catch { obj = null; }
  }
  const entries = obj && typeof obj === 'object' && !Array.isArray(obj)
    ? Object.entries(obj).map(([k, v]) => [k, typeof v === 'string' ? v : JSON.stringify(v, null, 2)])
    : [['custom_attributes (not a JSON object), as stored', typeof raw === 'string' ? raw : JSON.stringify(raw)]];
  if (!entries.length) return '';
  const rendered = entries.map(([k, v]) => block(k, v || '(the stored value is an empty string)'));
  return `
    <h3>The work item's own attributes &mdash; ${esc(w.id)}</h3>
    <p class="meta">The row's own <code>custom_attributes</code>, one block per key, each quoted verbatim. For an
    item with no acceptance criterion this may be the only place its completion record lives.</p>
    ${rendered.join('\n    ')}`;
};

const sections = items
  .sort(byNumericId)
  .map((w) => {
    const criteria = criteriaOf(w);
    const criterionLess = criteria.length === 0;
    /* Asserted here so a criterion with no verdict aborts the whole slate rather than rendering a
       section the operator could rule on without evidence. */
    criteria.forEach((c) => latestVerdict(c.id));

    /* DEF-117's fail-closed half. A criterion-less item is adjudicable only if the store carries the
       reason; with neither a DW- nor a DEC- row to quote there is genuinely nothing on the page. */
    const decsCited = criterionLess ? citedRows(w, 'DEC', decs, ['title', 'source_span']) : [];
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
    /* ⚠ DEF-142 — AN INSTRUMENT ITEM NAMES NO REQUIREMENT AT ALL, AND THAT IS NOT A GAP. WBS-29 was
       commissioned by DEC-134 d1 to make a future DEF-129 occurrence answerable; its own row says it
       "diagnoses nothing", and it satisfies no FR/NFR by construction, so there is nothing for the title
       scrape to find. The old unconditional guard fatalled on it — which SILENTLY RE-NARROWED what
       DEF-117's fix had opened eighteen lines above, in the very same commit (964ab01a). DEF-117's
       stated fail-closed condition is the citedRows test above (nothing in the store records the
       reason), and WBS-29 PASSES it by citing DEC-134; this guard then killed it anyway. WBS-27,
       WBS-27.1, WBS-27.2 and WBS-28 share the shape and were adjudicated OUTSIDE the slate because of
       it, with nothing recording the exception.
       So: a criterion-less item may name ZERO requirements and renders with no requirement section.
       ⛔ THE REFUSAL IS NARROWED, NOT REMOVED — an item with no criterion AND no cited DEC-/DW- row
       still fatals at the citedRows test, and a criterion-BEARING item whose criteria name no
       requirement is still a real fault and still fatals here. Over-narrowing this would look exactly
       like a passing suite, which is why the DEF-142 render case is PAIRED with a calibration that
       differs from it only in whether a reason exists, and asserts the refusal still fires. */
    const reqIds = criterionLess
      ? [...new Set((w.title.match(/\b(?:FR|NFR)-\d+\b/g) ?? []))].slice(0, 1)
      : [...new Set(criteria.map((c) => c.requirement_id))];
    if (!criterionLess && !reqIds.length) {
      fatal(`${w.id} carries acceptance criteria but none of them names a requirement`);
    }
    const qs = reqIds.map((id) => reqs[id] ?? fatal(`requirement ${id} (of ${w.id}) not found`));
    /* The DW- row is optional: not every work item closes one. Absent is fine; wrong is not.
       A criterion-less item gets EVERY cited row, because the reason may sit in any of them. */
    const d = criterionLess ? null : dwsCited[0];
    return `
  <section>
    <h2>${esc(w.id)} <span class="st">${esc(w.lifecycle_status)}</span></h2>
    <p class="meta">${[
      criterionLess
        ? '<b>no acceptance criterion</b>'
        : criteria.map((c) => {
            const cv = latestVerdict(c.id);
            return `${esc(c.id)} verdict <b>${esc(cv.verdict)}</b> (${esc(cv.id)})`;
          }).join(' &middot; '),
      /* DEF-142: spread rather than join-into-a-fixed-separator. With zero requirements the old form
         emitted a dangling " &middot; " — cosmetic, but this page is the acceptance bar. */
      ...(qs.length ? qs.map((q) => `${esc(q.id)} <b>${esc(q.lifecycle_status)}</b>`) : ['<b>no requirement</b>']),
      ...(d ? [`${esc(d.id)} <b>${esc(d.lifecycle_status)}</b>`] : []),
    ].filter(Boolean).join(' &middot; ')}</p>

    ${criterionLess ? `<div class="lead"><p><b>This item carries NO acceptance criterion, and that is
    deliberate rather than missing.</b> Nothing below is a verdict record, so there is no <code>Met</code>
    to lean on: you are adjudicating the completion record itself, and the recorded reason for the
    criterion's absence, both quoted verbatim from the store. If that reason does not persuade you, the
    right outcome is to leave the item at <b>Review</b> and say what evidence would settle it.</p>${
      qs.length ? '' : `<p><b>It also names no requirement, which is what an INSTRUMENT item looks like</b>
    &mdash; work commissioned to make a fault answerable rather than to satisfy an <code>FR-</code> or
    <code>NFR-</code>. There is no requirement section below because there is no requirement, not because
    one could not be found. <b>The decision that commissioned it is quoted in full instead, and that is
    the thing to adjudicate</b>: was this the right instrument to build, and does its completion record
    show it actually delivering? (<code>DEF-142</code>.)</p>`}</div>` : ''}

    <h3>The work item &mdash; ${esc(w.id)}</h3>
    ${block('As stored', w.title)}
    ${attributeBlocks(w)}

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
<p>Every block is quoted verbatim from the tamheed exports under <code>tamheed-package/exports/</code>,
written by the MCP tool <code>entity_export</code>, by the generator that
produced this page (LL-011). The generator exits non-zero rather than print an empty block. An item may
name any number of acceptance criteria, including none &mdash; but a criterion-less item is refused
unless the store also carries a <code>DW-</code> or <code>DEC-</code> row recording why, which is then
quoted in full below it.</p>
<p><b>Package digest at export:</b> <code>${esc(DIGEST)}</code> &mdash; this is the package digest,
not a file hash, and every family on this page carried it (the generator refuses a mixed snapshot).
Run <code>package_verify()</code>: the same digest means nothing in the package has changed since
this page was generated, and a different one means the page is stale.</p>
</div>
${sections}
</body></html>`;

const out = join(ROOT, 'tamheed-package', 'slice-review-slate.html');
writeFileSync(out, html, 'utf8');
console.log(`wrote ${out}`);
console.log(`${SLICE}: ${items.length} item(s) at Review — ${items.map((i) => i.id).join(', ')}`);
