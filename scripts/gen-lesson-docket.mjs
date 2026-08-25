#!/usr/bin/env node
/*
 * Generate the operator's lesson-confirmation docket from data/lessons.jsonl.
 *
 * WHY THIS EXISTS. LL-011 (Approved, pinned): an identifier is a POINTER, not a reference. The
 * operator once refused an interview because the slate cited ~40 records by id alone, handing the
 * retrieval work to the person the artifact exists to serve, at the moment they are deciding.
 * The remediation was that anything they read to DECIDE must carry each record's own text where it
 * cites it — quoted from the JSONL by a generator, never paraphrased and never re-typed.
 *
 * So this is that generator, committed rather than improvised, so the next lessons interview is
 * compliant by default instead of by remembering. Every character of every field below is read from
 * the canonical JSONL and HTML-escaped; nothing is retyped.
 *
 * Paths resolve from this file's own location, never the cwd (trap 29).
 *
 *   node scripts/gen-lesson-docket.mjs [outfile]      # default: docket written beside the package
 */

import { readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const LESSONS = path.join(REPO_ROOT, 'tamheed-package', 'data', 'lessons.jsonl')

const rows = readFileSync(LESSONS, 'utf8').trim().split('\n').map((l) => JSON.parse(l))
const pending = rows.filter((r) => r.lifecycle_status === 'Proposed')

if (pending.length === 0) {
  console.error('No Proposed lessons — nothing to adjudicate.')
  process.exit(1)
}

const esc = (s) =>
  String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

/** Blank-line-separated blocks become paragraphs; single newlines stay inside one. */
const paras = (s) =>
  String(s ?? '')
    .split(/\n\s*\n/)
    .map((p) => p.trim())
    .filter(Boolean)
    .map((p) => `<p>${esc(p).replace(/\n/g, '<br>')}</p>`)
    .join('\n')

/** First sentence of the statement, for the summary docket. */
const gist = (s) => {
  const t = String(s ?? '').replace(/\s+/g, ' ').trim()
  const cut = t.search(/[.:]\s/)
  return esc(cut > 40 ? t.slice(0, cut + 1) : t.slice(0, 160) + (t.length > 160 ? '…' : ''))
}

const FIELDS = [
  ['statement', 'The lesson'],
  ['context', 'What happened'],
  ['recommendation', 'What to do instead'],
  ['rationale', 'Why it generalises'],
  ['impact_if_followed', 'If followed'],
  ['impact_if_ignored', 'If ignored'],
]

const entries = pending
  .map((r) => {
    const blocks = FIELDS.filter(([k]) => r[k])
      .map(
        ([k, label]) => `
        <div class="field">
          <h3>${esc(label)}</h3>
          ${paras(r[k])}
        </div>`,
      )
      .join('')
    return `
      <article class="motion" id="${esc(r.id)}">
        <header class="motion-head">
          <div class="eyebrow">
            <span class="id">${esc(r.id)}</span>
            <span class="chip chip-kind">${esc(r.kind)}</span>
            ${r.category ? `<span class="chip">${esc(r.category)}</span>` : ''}
            <span class="chip chip-pending">${esc(r.lifecycle_status)}</span>
          </div>
          <h2>${esc(r.title)}</h2>
        </header>
        ${blocks}
      </article>`
  })
  .join('')

const docketRows = pending
  .map(
    (r) => `
        <tr>
          <td class="id">${esc(r.id)}</td>
          <td>${gist(r.statement)}</td>
          <td class="mono">${esc(r.kind)}</td>
          <td class="mono">${esc(r.category ?? '—')}</td>
        </tr>`,
  )
  .join('')

const html = `<title>ACMP Lessons Docket</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Archivo:wght@500;600;700&family=IBM+Plex+Mono:wght@400;500&family=Source+Serif+4:opsz,wght@8..60,400;8..60,600&display=swap">
<style>
  :root {
    --paper:#e8ebee; --surface:#fafbfc; --ink:#14171a; --muted:#59616b;
    --rule:#ccd3d9; --rule-strong:#9aa5ae;
    --accent:#9a6008; --accent-soft:#f0e2c6;
    --pending:#9a6008;
    --shadow:0 1px 0 rgba(20,23,26,.04), 0 8px 24px -18px rgba(20,23,26,.5);
  }
  @media (prefers-color-scheme: dark) {
    :root:not([data-theme="light"]) {
      --paper:#121518; --surface:#1a1e23; --ink:#e4e8eb; --muted:#98a2ac;
      --rule:#282e35; --rule-strong:#3c454e;
      --accent:#e2a84a; --accent-soft:#3a2f1a;
      --pending:#e2a84a;
      --shadow:0 1px 0 rgba(0,0,0,.3), 0 10px 30px -20px #000;
    }
  }
  :root[data-theme="dark"] {
    --paper:#121518; --surface:#1a1e23; --ink:#e4e8eb; --muted:#98a2ac;
    --rule:#282e35; --rule-strong:#3c454e;
    --accent:#e2a84a; --accent-soft:#3a2f1a;
    --pending:#e2a84a;
    --shadow:0 1px 0 rgba(0,0,0,.3), 0 10px 30px -20px #000;
  }

  * { box-sizing:border-box; }
  body {
    margin:0; background:var(--paper); color:var(--ink);
    font-family:"Source Serif 4", Georgia, serif;
    font-size:17px; line-height:1.62;
    -webkit-font-smoothing:antialiased;
  }
  .wrap { max-width:52rem; margin:0 auto; padding:clamp(2rem,5vw,4.5rem) clamp(1.1rem,4vw,2rem) 6rem; }

  .masthead { border-bottom:2px solid var(--rule-strong); padding-bottom:1.6rem; margin-bottom:2.2rem; }
  .kicker {
    font-family:"IBM Plex Mono", ui-monospace, monospace;
    font-size:.7rem; letter-spacing:.16em; text-transform:uppercase;
    color:var(--accent); margin:0 0 .8rem;
  }
  h1 {
    font-family:Archivo, "Helvetica Neue", sans-serif; font-weight:700;
    font-size:clamp(1.9rem,5vw,2.9rem); line-height:1.08; letter-spacing:-.02em;
    margin:0 0 .7rem; text-wrap:balance;
  }
  .standfirst { margin:0; color:var(--muted); font-size:1.06rem; max-width:42rem; }

  .note {
    background:var(--surface); border:1px solid var(--rule); border-left:3px solid var(--accent);
    padding:1.1rem 1.3rem; margin:2.2rem 0; box-shadow:var(--shadow);
  }
  .note h2 {
    font-family:Archivo, sans-serif; font-size:.78rem; font-weight:600;
    letter-spacing:.12em; text-transform:uppercase; color:var(--muted); margin:0 0 .6rem;
  }
  .note p { margin:0 0 .7rem; font-size:.95rem; }
  .note p:last-child { margin-bottom:0; }
  .note ul { margin:.2rem 0 0; padding-left:1.1rem; font-size:.95rem; }
  .note li { margin-bottom:.35rem; }

  .table-scroll { overflow-x:auto; margin:2.2rem 0 3rem; }
  table { border-collapse:collapse; width:100%; min-width:34rem; font-size:.92rem; }
  caption {
    text-align:left; font-family:Archivo, sans-serif; font-size:.78rem; font-weight:600;
    letter-spacing:.12em; text-transform:uppercase; color:var(--muted); padding-bottom:.7rem;
  }
  th {
    text-align:left; font-family:Archivo, sans-serif; font-weight:600; font-size:.72rem;
    letter-spacing:.09em; text-transform:uppercase; color:var(--muted);
    border-bottom:1px solid var(--rule-strong); padding:.45rem .8rem .45rem 0;
  }
  td { border-bottom:1px solid var(--rule); padding:.72rem .8rem .72rem 0; vertical-align:top; }
  tr:last-child td { border-bottom:none; }
  .id, .mono {
    font-family:"IBM Plex Mono", ui-monospace, monospace;
    font-variant-numeric:tabular-nums; font-size:.86em;
  }
  .id { color:var(--accent); font-weight:500; white-space:nowrap; }

  .motion { border-top:1px solid var(--rule-strong); padding-top:2.2rem; margin-top:3.4rem; }
  .motion-head { margin-bottom:1.5rem; }
  .eyebrow { display:flex; flex-wrap:wrap; gap:.45rem; align-items:center; margin-bottom:.75rem; }
  .eyebrow .id { font-size:.85rem; letter-spacing:.04em; }
  .chip {
    font-family:"IBM Plex Mono", ui-monospace, monospace;
    font-size:.66rem; letter-spacing:.08em; text-transform:uppercase;
    border:1px solid var(--rule-strong); color:var(--muted);
    padding:.16rem .48rem; border-radius:2px;
  }
  .chip-pending { color:var(--pending); border-color:var(--pending); background:var(--accent-soft); }
  .motion h2 {
    font-family:Archivo, "Helvetica Neue", sans-serif; font-weight:600;
    font-size:clamp(1.15rem,2.6vw,1.42rem); line-height:1.25; letter-spacing:-.012em;
    margin:0; text-wrap:balance;
  }

  .field { margin-bottom:1.6rem; }
  .field h3 {
    font-family:Archivo, sans-serif; font-size:.72rem; font-weight:600;
    letter-spacing:.13em; text-transform:uppercase; color:var(--muted);
    margin:0 0 .5rem; padding-bottom:.3rem; border-bottom:1px solid var(--rule);
  }
  .field p { margin:0 0 .85rem; }
  .field p:last-child { margin-bottom:0; }

  footer {
    margin-top:4rem; padding-top:1.4rem; border-top:1px solid var(--rule);
    font-family:"IBM Plex Mono", ui-monospace, monospace;
    font-size:.74rem; color:var(--muted); line-height:1.7;
  }
  @media (prefers-reduced-motion:reduce) { * { animation:none !important; transition:none !important; } }
</style>

<div class="wrap">
  <header class="masthead">
    <p class="kicker">ACMP · lessons register · operator confirmation</p>
    <h1>${pending.length} lessons are waiting on your word</h1>
    <p class="standfirst">Each was recorded by the executing agent after execution taught it something. None of them binds anything yet. Only the ones you approve will.</p>
  </header>

  <section class="note">
    <h2>Before you read</h2>
    <p><strong>An approved lesson is immutable.</strong> The store refuses an edit to one — a change means writing a new lesson that supersedes it. So approving is the expensive direction, and rejecting costs nothing but the row.</p>
    <p><strong>Approving is not the same as agreeing a finding happened.</strong> These sentences were written by the agent, not by you. Selecting a finding is not approving the wording it was written up in.</p>
    <ul>
      <li><strong>Approve and pin</strong> — binds every future session and is written into the note the agent loads at startup. Reserve it for the ones worth interrupting someone with.</li>
      <li><strong>Approve</strong> — binds, but stays out of the startup note.</li>
      <li><strong>Reject</strong> — the row stays as history and binds nothing.</li>
      <li><strong>Refine</strong> — tell me what is wrong with the sentence and I will rewrite it for another pass.</li>
    </ul>
    <p>Every word below is printed straight from the register — nothing paraphrased, nothing retyped. You should not need to open anything else to decide.</p>
  </section>

  <div class="table-scroll">
    <table>
      <caption>The docket</caption>
      <thead>
        <tr><th scope="col">Row</th><th scope="col">In one line</th><th scope="col">Kind</th><th scope="col">Category</th></tr>
      </thead>
      <tbody>${docketRows}
      </tbody>
    </table>
  </div>
${entries}

  <footer>
    Generated from tamheed-package/data/lessons.jsonl by scripts/gen-lesson-docket.mjs.<br>
    ${pending.length} of ${rows.length} rows in the register are Proposed; the rest are Approved or Superseded and are not on this docket.
  </footer>
</div>
`

const out = process.argv[2] ?? path.join(REPO_ROOT, 'tamheed-package', 'lesson-docket.html')
writeFileSync(out, html, 'utf8')
console.log(`wrote ${out}`)
console.log(`pending: ${pending.map((r) => r.id).join(', ')}`)
