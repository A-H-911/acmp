#!/usr/bin/env node
// P16b / C-SUP-02 (OQ-027): fail CI on any HIGH or CRITICAL NuGet vulnerability across the solution.
// Moderate/Low are reported (via `dotnet list`) but do not block, per the Definition of Done. Parses the
// stable `--format json` output (not the human table) so the gate is robust to formatting changes.
import { execFileSync } from 'node:child_process';

const BLOCK = new Set(['High', 'Critical']);

let raw;
try {
  // execFileSync (no shell) — the argv is a fixed constant, no interpolation, no injection surface.
  raw = execFileSync('dotnet',
    ['list', 'acmp.sln', 'package', '--vulnerable', '--include-transitive', '--format', 'json'],
    { encoding: 'utf8', maxBuffer: 128 * 1024 * 1024 });
} catch (err) {
  console.error('[check-vulns] `dotnet list --vulnerable` failed:', err.message);
  process.exit(2);
}

const data = JSON.parse(raw.slice(raw.indexOf('{')));
const blocking = new Set();
for (const project of data.projects ?? [])
  for (const fw of project.frameworks ?? [])
    for (const pkg of [...(fw.topLevelPackages ?? []), ...(fw.transitivePackages ?? [])])
      for (const v of pkg.vulnerabilities ?? [])
        if (BLOCK.has(v.severity))
          blocking.add(`${pkg.id} ${pkg.resolvedVersion} [${v.severity}] ${v.advisoryurl ?? ''}`.trim());

if (blocking.size > 0) {
  console.error(`[check-vulns] ${blocking.size} High/Critical NuGet vulnerabilit${blocking.size === 1 ? 'y' : 'ies'} — blocking:`);
  for (const line of [...blocking].sort()) console.error('  - ' + line);
  console.error('Fix: pin a patched version (see Directory.Build.props) or upgrade the parent package.');
  process.exit(1);
}

console.log('[check-vulns] no High/Critical NuGet vulnerabilities (Moderate/Low allowed by DoD)');
