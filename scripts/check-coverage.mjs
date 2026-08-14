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
import { dirname, join } from 'node:path';

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

/*
 * ⚠ NEWEST REPORT PER TEST PROJECT, NOT ALL OF THEM (DEF-069). `dotnet test --collect` writes a NEW
 * timestamped directory on every run instead of replacing the last one, and this script used to sum
 * every report it could find. So running the gate locally twice scored an edited file against its
 * OLD self, and the worst historical reading won: it kept indicting a file at 50% after the only
 * coverable code had been MOVED OUT of it, which is what finally made the measurement suspect rather
 * than the code. A stale gate does not merely waste time — it DIRECTS work, and two changes were
 * made to satisfy that false reading before the reading itself was doubted.
 *
 * CI never saw it, because a fresh checkout has no TestResults at all. That is exactly why the fix
 * belongs here rather than in a documented cleanup step: the gate is wrong only for the person
 * following this project's own advice to run it before pushing, and telling them to remember
 * `rm -rf` relies on the attention that already failed.
 *
 * Keyed on the TestResults parent (one per test project), so a solution-wide run still measures
 * every project — it just measures each one ONCE, at its latest run.
 */
function newestPerProject(all) {
  const latest = new Map();
  for (const file of all) {
    // ⚠ dirname TWICE, not a path split on a hand-written separator class. The first attempt used
    // /[\/]/, which matches forward slashes only — and node's join() produces BACKSLASHES on
    // Windows, so every key collapsed to the empty string, every report landed in one bucket, and
    // the gate silently discarded four of five legitimate per-project reports and reported 84.70%.
    // It announced "ignoring 4 superseded report(s)", which is the only reason it was diagnosable.
    const project = dirname(dirname(file)); // .../<project>/TestResults/<guid>/coverage.cobertura.xml
    const mtime = statSync(file).mtimeMs;
    const held = latest.get(project);
    if (!held || mtime > held.mtime) latest.set(project, { file, mtime });
  }
  return [...latest.values()].map((v) => v.file);
}

const allReports = findReports(root);
const reports = newestPerProject(allReports);
if (allReports.length > reports.length) {
  console.log(
    `[check-coverage] ignoring ${allReports.length - reports.length} superseded report(s) from earlier runs (DEF-069)`,
  );
}
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
