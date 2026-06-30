#!/usr/bin/env node
// S7 (ADR-0016 §1) — backend per-file + global line-coverage gate.
//
// coverlet's own threshold is per-assembly only, so this enforces the ADR basis: ≥95% LINES
// per FILE and globally, on assertable product code. The coverlet.runsettings exclusions
// (Migrations, *DbContextFactory, MinioFileStore, Program, generated) are already applied at
// collection time, so every file present here is in-scope.
//
// It UNIONS line hits across every per-project cobertura report (a line counts as covered if any
// test project hit it), which is the true merged coverage — the same thing ReportGenerator does,
// without needing the extra tool. Exit 1 (with the offending files) if anything is under target.
//
// Usage: node scripts/check-coverage.mjs [searchRoot=.] [threshold=95]
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

const root = process.argv[2] ?? '.';
const THRESHOLD = Number(process.argv[3] ?? 95);

/** Recursively collect every coverage.cobertura.xml under root. */
function findReports(dir) {
  const out = [];
  for (const entry of readdirSync(dir)) {
    if (entry === 'node_modules' || entry === '.git') continue;
    const full = join(dir, entry);
    const st = statSync(full);
    if (st.isDirectory()) out.push(...findReports(full));
    else if (entry === 'coverage.cobertura.xml') out.push(full);
  }
  return out;
}

const reports = findReports(root);
if (reports.length === 0) {
  console.error(`[check-coverage] no coverage.cobertura.xml found under ${root}`);
  process.exit(1);
}

// filename -> Map(lineNumber -> hits), unioned (max hits) across all reports.
const files = new Map();

for (const report of reports) {
  const xml = readFileSync(report, 'utf8');
  // Walk class/line tokens in document order, attributing each line to the current class file.
  const combined = /<class\b[^>]*\bfilename="([^"]+)"|<line\b[^>]*\bnumber="(\d+)"[^>]*\bhits="(\d+)"/g;
  let current = null;
  let m;
  while ((m = combined.exec(xml)) !== null) {
    if (m[1] !== undefined) {
      current = m[1];
      if (!files.has(current)) files.set(current, new Map());
    } else if (current) {
      const line = Number(m[2]);
      const hits = Number(m[3]);
      const lines = files.get(current);
      lines.set(line, Math.max(lines.get(line) ?? 0, hits));
    }
  }
}

let totalLines = 0;
let totalCovered = 0;
const failures = [];
for (const [file, lines] of files) {
  const total = lines.size;
  if (total === 0) continue;
  let covered = 0;
  for (const hits of lines.values()) if (hits > 0) covered++;
  totalLines += total;
  totalCovered += covered;
  const pct = (covered / total) * 100;
  if (pct < THRESHOLD) failures.push({ file, pct, covered, total });
}

const globalPct = totalLines === 0 ? 0 : (totalCovered / totalLines) * 100;

console.log(`[check-coverage] ${files.size} files, global line coverage ${globalPct.toFixed(2)}% (target ${THRESHOLD}%)`);
if (failures.length > 0) {
  failures.sort((a, b) => a.pct - b.pct);
  console.error(`[check-coverage] ${failures.length} file(s) below ${THRESHOLD}% lines:`);
  for (const f of failures) console.error(`  ${f.pct.toFixed(2).padStart(6)}%  (${f.covered}/${f.total})  ${f.file}`);
}
if (globalPct < THRESHOLD) console.error(`[check-coverage] global ${globalPct.toFixed(2)}% is below ${THRESHOLD}%`);

process.exit(failures.length > 0 || globalPct < THRESHOLD ? 1 : 0);
