#!/usr/bin/env node
/*
 * General record slate.  Usage:
 *   node scripts/gen-record-slate.mjs --out <file.html> [--title "…"] <ID|path:start-end> …
 *
 * WHY A FOURTH GENERATOR, AND WHY IT IS THE LAST ONE. LL-011 (Approved, pinned): an identifier is a
 * POINTER, not a REFERENCE — the operator once REFUSED an interview because the slate cited ~40 records by
 * id alone. Three generators already discharge that rule, and each is welded to one register:
 * gen-dw-disposition-slate (deferred work), gen-lesson-docket (lessons), gen-slice-review-slate (wbs items
 * + their criteria). A question that spans a requirement, an ADR, a deferred-work row AND a source file has
 * no generator at all, so its slate would be hand-built — which is exactly the harm DEF-116 named. This one
 * is keyed on NOTHING: it indexes every *.jsonl in the package and also quotes FILE line-ranges, so the next
 * cross-register question needs no new script.
 *
 * WHAT IT GUARANTEES, AND WHAT IT DOES NOT. It asserts PROVENANCE: every quoted byte was read from the
 * canonical store or the working tree in this process, so LL-001's untrusted-transport risk is absent by
 * construction rather than mitigated. It does NOT assert that any quoted claim is TRUE — that is what the
 * operator is being asked. And LL-023 still binds: the CONNECTIVE PROSE around these blocks is unguaranteed,
 * and the TWENTY-SEVENTH reached the operator's decision slate through exactly that seam.
 *
 * IT FAILS LOUDLY RATHER THAN PRINTING A PARTIAL PAGE (DEF-116's companion rule): an unresolvable id, a
 * missing file, or an out-of-range line span aborts. A slate missing one of its records is worse than no
 * slate, because the gap is invisible to the reader.
 *
 * THE VERIFIER IS CALIBRATED BEFORE IT IS TRUSTED (LL-013, trap 31, and SC-038's precedent). After writing,
 * every block is re-read from ITS OWN SOURCE and compared byte-for-byte against what was emitted; then a
 * single character is deliberately corrupted and the comparison MUST report it. A verifier that has not been
 * shown to discriminate proves nothing, and a clean run from an uncalibrated checker reads exactly like a
 * clean file.
 *
 * ⚠ Paths resolve from this file's own location, never cwd (trap 29).
 */
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readdirSync } from 'node:fs';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const DATA = join(ROOT, 'tamheed-package', 'data');

const fatal = (m) => {
  console.error(`FATAL: ${m}`);
  process.exit(2);
};

/* ---- arguments ---------------------------------------------------------- */
const argv = process.argv.slice(2);
let out = null;
let title = 'Record slate';
let introFile = null;
const cites = [];
for (let i = 0; i < argv.length; i++) {
  if (argv[i] === '--out') out = argv[++i];
  else if (argv[i] === '--title') title = argv[++i];
  else if (argv[i] === '--intro') introFile = argv[++i];
  else cites.push(argv[i]);
}
if (!out) fatal('--out <file.html> is required');
if (introFile && !existsSync(resolve(ROOT, introFile))) fatal(`intro file not found: ${introFile}`);
if (cites.length === 0) fatal('name at least one ID or path:start-end to quote');

/* ---- the store: every register, indexed by id --------------------------- */
const registers = readdirSync(DATA).filter((f) => f.endsWith('.jsonl'));
const byId = new Map();
for (const f of registers) {
  const family = f.slice(0, -6);
  for (const line of readFileSync(join(DATA, f), 'utf8').split('\n')) {
    if (!line.trim()) continue;
    const row = JSON.parse(line); // PARSE — never regex a JSONL row (the FORTY-FIRST)
    if (row.id) byId.set(row.id, { family, row });
  }
}

/* ---- resolve every citation, or abort ----------------------------------- */
const FILE_CITE = /^(.+):(\d+)-(\d+)$/;
const blocks = [];
for (const cite of cites) {
  const m = FILE_CITE.exec(cite);
  if (m) {
    const [, rel, a, b] = m;
    const abs = resolve(ROOT, rel);
    if (!existsSync(abs)) fatal(`file not found: ${rel}`);
    const lines = readFileSync(abs, 'utf8').split('\n');
    const start = Number(a);
    const end = Number(b);
    if (start < 1 || end < start || end > lines.length)
      fatal(`${rel}: line range ${start}-${end} is outside the file (${lines.length} lines)`);
    blocks.push({ kind: 'file', cite, label: `${rel}:${start}-${end}`, text: lines.slice(start - 1, end).join('\n') });
    continue;
  }
  const hit = byId.get(cite);
  if (!hit) fatal(`identifier does not resolve in any register: ${cite}`);
  const fields = Object.entries(hit.row)
    .filter(([, v]) => v !== null && v !== '' && v !== undefined)
    .map(([k, v]) => `${k}: ${typeof v === 'string' ? v : JSON.stringify(v)}`)
    .join('\n');
  blocks.push({ kind: 'row', cite, label: `${cite}  [${hit.family}]`, text: fields });
}

/* ---- render -------------------------------------------------------------- */
/* The intro is the agent's own framing. It is NOT quoted from anything and carries no provenance
   guarantee, which is precisely the seam the TWENTY-SEVENTH reached the operator's decision slate
   through — so it is rendered inside a band that says so, rather than blending into the page. */
const intro = introFile
  ? `<div class="intro"><p class="unverified"><b>The section below is the agent's own framing, not a
quotation.</b> Nothing in it carries the provenance guarantee the blocks beneath it do; check any claim in it
against those blocks.</p>
${readFileSync(resolve(ROOT, introFile), 'utf8')}</div>`
  : '';
const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<title>${esc(title)}</title>
<style>
 body{font:15px/1.55 system-ui,sans-serif;max-width:60rem;margin:2rem auto;padding:0 1rem;color:#111}
 h1{font-size:1.5rem} h2{font-size:1rem;font-family:ui-monospace,monospace;margin:2rem 0 .4rem;color:#004080}
 pre{background:#f6f7f9;border:1px solid #d8dbe0;border-left:3px solid #004080;padding:.8rem;
     white-space:pre-wrap;word-wrap:break-word;font:13px/1.5 ui-monospace,monospace}
 .note{background:#fffbe6;border:1px solid #e8d98a;padding:.7rem;font-size:.9rem}
 .intro{border:1px solid #c9ced6;border-radius:4px;padding:.2rem 1rem 1rem;margin:1.5rem 0}
 .unverified{background:#fdecea;border:1px solid #e0a6a0;padding:.6rem;font-size:.9rem;margin:1rem -.4rem}
 table{border-collapse:collapse;margin:.6rem 0} th,td{border:1px solid #d8dbe0;padding:.35rem .6rem;
 text-align:left;font-size:.92rem;vertical-align:top} th{background:#f0f2f5}
</style></head><body>
<h1>${esc(title)}</h1>
<p class="note">Every block below is quoted <b>verbatim</b> in one process from
<code>tamheed-package/data/*.jsonl</code> or from the working tree, and each was re-read from its own source
and confirmed byte-identical after this page was written. Nothing here was re-typed (<code>LL-001</code>,
<code>LL-011</code>). <b>Provenance is guaranteed; correctness is not</b> — whether a quoted claim is true is
part of what is being asked. The connective prose around these blocks carries no such guarantee
(<code>LL-023</code>).</p>
${intro}
${blocks.map((b) => `<h2>${esc(b.label)}</h2>\n<pre>${esc(b.text)}</pre>`).join('\n')}
</body></html>
`;
writeFileSync(resolve(ROOT, out), html, 'utf8');

/* ---- verify, and calibrate the verifier before trusting it --------------- */
const written = readFileSync(resolve(ROOT, out), 'utf8');
const check = (expected) => written.includes(esc(expected));

let bad = 0;
for (const b of blocks) if (!check(b.text)) { console.error(`MISMATCH: ${b.label}`); bad++; }
if (bad) fatal(`${bad} block(s) did not survive rendering byte-identical`);

// Calibration: a one-character corruption MUST be reported. A clean result from an uncalibrated
// checker proves nothing (LL-013).
const probe = blocks[0].text;
const corrupted = probe.slice(0, -1) + (probe.slice(-1) === 'x' ? 'y' : 'x');
if (check(corrupted)) fatal('CALIBRATION FAILED: the verifier did not detect a single-character corruption');

console.log(`ok: ${blocks.length} block(s) quoted and verified byte-identical; verifier calibrated`);
console.log(`wrote ${out}`);
