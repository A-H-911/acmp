/**
 * Shared reader for the four slate generators (WBS-31, ruled compliant by DEC-139 d1).
 *
 * WHY THIS EXISTS. DEC-135 d1 makes the tamheed MCP tools the only path to the package, reads
 * included, and a `node` process has no MCP client. tamheed 4.7.0's `entity_export` closes that
 * gap: the server runs a read tool in-process and writes its WHOLE result to
 * tamheed-package/exports/<family>.json. The generator quotes THAT — the tool's own output —
 * so nothing here ever opens data/*.jsonl.
 *
 * ⛔ AN EXPORT IS A POINT-IN-TIME COPY. Export immediately before generating and never reuse one
 * across sessions (DEC-139 d4 gitignores exports/ for exactly this reason). A stale slate reads
 * exactly like a current one, which is the failure mode these generators exist to prevent.
 *
 * ⭐ THE DIGEST IS THE PACKAGE DIGEST, NOT A FILE HASH. Measured 2026-09-07: seven exports of
 * completely different families all carried `bab5b31f…`, and `package_verify()` returned the same
 * value. So two things follow, and both are enforced below:
 *   - every family in one slate MUST carry the same digest, or they are not one snapshot;
 *   - printing it in the slate header lets the operator run `package_verify()` and confirm that
 *     nothing has changed since the export.
 */
import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

/**
 * JSONL family name -> entity_query type. The generators are written against family names
 * (that is what data/*.jsonl was called), so this is the one place the correspondence lives.
 *
 * Every value below was checked on 2026-09-07 against the server's own type list, obtained by asking
 * entity_query for a type that does not exist and reading the 37 it names back. There is deliberately
 * no test file for this map: its ONLY consumer is exportHint()'s error text, so a wrong value produces
 * an unhelpful hint, never a wrong slate. Say so rather than implying coverage that does not exist.
 */
export const FAMILY_TYPE = {
  acceptance_criteria: 'acceptance-criterion',
  adrs: 'adr',
  assumptions: 'assumption',
  audit_verdicts: 'audit-verdict',
  constraints: 'constraint',
  decisions: 'decision',
  defects: 'defect',
  deferred_work: 'deferred-work',
  dependencies: 'dependency',
  document_sections: 'document-section',
  invariants: 'invariant',
  kpis: 'kpi',
  lessons: 'lesson',
  milestones: 'milestone',
  narrative_documents: 'narrative-document',
  omissions: 'omission',
  open_questions: 'open-question',
  phases: 'phase',
  progress_entries: 'progress-entry',
  requirements: 'requirement',
  risks: 'risk',
  scope_changes: 'scope-change',
  skills: 'skill',
  slices: 'slice',
  stakeholders: 'stakeholder',
  tests: 'test',
  trace_edges: 'trace-edge',
  wbs_items: 'wbs-item',
};

export const EXPORT_LIMIT = 5000;

export const exportsDir = (root) => join(root, 'tamheed-package', 'exports');
export const exportPath = (root, family) => join(exportsDir(root), `${family}.json`);

/** The exact call that produces a missing export — a control must detect AND tell. */
export function exportHint(family) {
  const type = FAMILY_TYPE[family];
  if (!type) return `entity_export("${family}.json", args={"type": "<type>", "limit": ${EXPORT_LIMIT}})`;
  return `entity_export("${family}.json", args={"type": "${type}", "limit": ${EXPORT_LIMIT}})`;
}

class ExportError extends Error {}

const bad = (msg) => {
  throw new ExportError(msg);
};

/**
 * Read one exported family. Returns { rows, digest }.
 *
 * Fails closed on every way an export can be wrong, because a SHORT read is the dangerous one:
 * it produces a slate with a hole, which reads exactly like a slate without one.
 *
 * ⚠ The file's `result` carries ok/count/total/next_after but NOT `partial` — that field is on the
 * tool's return value only. Read from the file, not assumed: count === total && next_after === null
 * is the file-side equivalent, and asserting `partial === false` here would be a check that can
 * never pass.
 */
export function loadFamily(root, family) {
  const p = exportPath(root, family);
  if (!existsSync(p)) {
    bad(
      `missing export for '${family}'\n` +
        `  expected: ${p}\n` +
        `  run this FIRST, then re-run the generator:\n    ${exportHint(family)}\n` +
        `  (exports/ is gitignored by DEC-139 d4 — it is regenerated, never committed)`,
    );
  }

  let doc;
  try {
    doc = JSON.parse(readFileSync(p, 'utf8'));
  } catch (e) {
    bad(`export for '${family}' is not valid JSON (${p}): ${e.message}`);
  }

  const env = doc.tamheed_export;
  const res = doc.result;
  if (!env || !res) bad(`export for '${family}' is not a tamheed export envelope (${p})`);
  if (res.ok !== true) bad(`export for '${family}' records a failed read: ${JSON.stringify(res).slice(0, 300)}`);

  const rows = res.rows;
  if (!Array.isArray(rows)) bad(`export for '${family}' has no rows array (${p})`);

  // The short-read guard. All three of these must agree or the export is partial.
  if (res.next_after !== null) {
    bad(
      `export for '${family}' is PARTIAL — next_after=${JSON.stringify(res.next_after)}.\n` +
        `  re-export with a limit above total (${res.total}):\n    ${exportHint(family)}`,
    );
  }
  if (res.count !== res.total) {
    bad(
      `export for '${family}' is PARTIAL — count ${res.count} of total ${res.total}.\n` +
        `  re-export with a limit above total:\n    ${exportHint(family)}`,
    );
  }
  if (rows.length !== res.count) {
    bad(`export for '${family}' is inconsistent — ${rows.length} rows but count says ${res.count}`);
  }

  const digest = env.digest;
  if (!digest) bad(`export for '${family}' carries no digest (${p})`);

  return { rows, digest };
}

/**
 * Load several families as one snapshot. Returns { families: {name: rows}, digest }.
 *
 * ⛔ REFUSES A MIXED SNAPSHOT. The digest is the PACKAGE digest, so two families exported either
 * side of a write carry different ones — and a slate built from those quotes two different states
 * of the store while looking entirely consistent. Nothing else can detect that.
 */
export function loadSnapshot(root, names) {
  const families = {};
  const seen = new Map();
  for (const name of names) {
    const { rows, digest } = loadFamily(root, name);
    families[name] = rows;
    if (!seen.has(digest)) seen.set(digest, []);
    seen.get(digest).push(name);
  }
  if (seen.size > 1) {
    const groups = [...seen.entries()].map(([d, fs]) => `    ${d.slice(0, 12)}… ${fs.join(', ')}`).join('\n');
    bad(
      `exports are NOT one snapshot — ${seen.size} different package digests:\n${groups}\n` +
        `  a family was exported either side of a package write. Re-export ALL of them now, together.`,
    );
  }
  return { families, digest: [...seen.keys()][0] };
}

export { ExportError };
