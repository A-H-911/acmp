/*
 * Pure presentation mappers for Risk read models (register + detail).
 * Status/exposure tone map the wire enum names onto the shared StatusChip tones;
 * meaning is carried by the localized label + a colored dot, never colour alone
 * (WCAG 1.4.1). The heat grid position + colour follow "ACMP Lists & Registers.dc.html"
 * (heatCellsFor / buildDetail matrix). Exposure is the server-projected band — this
 * file only maps it to a colour, it never derives it from likelihood × impact.
 */
import type { StatusTone } from '../../components/ui/StatusChip';
import type { RiskStatus, RiskLevel, RiskExposure } from '../../api/risks';

// Open/Mitigating/Closed match the design's riskStatus map exactly (danger/warn/neutral). Accepted
// and Escalated have NO visual in "ACMP Lists & Registers.dc.html" (its riskStatus lists only 3) —
// no-reference tone choices: Escalated → danger (an active alarm, reads urgent), Accepted → info
// (a deliberate terminal decision, distinct from Open/Closed); flagged in the progress log.
const STATUS_TONE: Record<RiskStatus, StatusTone> = {
  Open: 'danger',
  Mitigating: 'warn',
  Closed: 'neutral',
  Accepted: 'info',
  Escalated: 'danger',
};

export function statusTone(status: RiskStatus): StatusTone {
  return STATUS_TONE[status] ?? 'neutral';
}

// Design expSem: Critical/High → danger, Medium → warn, Low → success.
export function exposureTone(exposure: RiskExposure): StatusTone {
  if (exposure === 'Critical' || exposure === 'High') return 'danger';
  if (exposure === 'Medium') return 'warn';
  return 'success';
}

// Design lvlColor for the Prob./Impact cells. RiskLevel is Low/Medium/High only — Critical is an
// Exposure band, never a probability/impact, so it is intentionally absent here (architect note).
export function levelColor(level: RiskLevel): string {
  if (level === 'High') return 'var(--st-danger-fg)';
  if (level === 'Medium') return 'var(--st-warn-fg)';
  return 'var(--text-2)';
}

/** Grid index of a level on the 3×3 heat matrix (design lvlIdx). */
const LVL_IDX: Record<RiskLevel, number> = { Low: 0, Medium: 1, High: 2 };

/**
 * The 9 background colours for the register's mini heat grid (row-major, 3×3), matching the design's
 * heatCellsFor: the single cell at (x = lvlIdx[prob], yTop = 2 − lvlIdx[impact]) is painted with the
 * exposure dot colour; every other cell is --sunken. Colour comes from the projected Exposure band.
 */
export function heatCells(prob: RiskLevel, impact: RiskLevel, exposure: RiskExposure): string[] {
  const x = LVL_IDX[prob];
  const yTop = 2 - LVL_IDX[impact];
  const on = `var(--st-${exposureTone(exposure)}-dot)`;
  const cells: string[] = [];
  for (let r = 0; r < 3; r++) {
    for (let c = 0; c < 3; c++) {
      cells.push(c === x && r === yTop ? on : 'var(--sunken)');
    }
  }
  return cells;
}

/** A large exposure-matrix cell: filled (on) cell = exposure bg + border, off = surface + border. */
export interface MatrixCell {
  bg: string;
  bd: string;
}

/**
 * The detail drill-in's LARGE 3×3 exposure matrix (design buildDetail kind==='risks'): the on-cell is
 * tinted with the exposure band's bg + dot border; off-cells are plain surface + border.
 */
export function exposureMatrix(prob: RiskLevel, impact: RiskLevel, exposure: RiskExposure): MatrixCell[][] {
  const x = LVL_IDX[prob];
  const yTop = 2 - LVL_IDX[impact];
  const tone = exposureTone(exposure);
  const rows: MatrixCell[][] = [];
  for (let r = 0; r < 3; r++) {
    const row: MatrixCell[] = [];
    for (let c = 0; c < 3; c++) {
      const on = c === x && r === yTop;
      row.push({
        bg: on ? `var(--st-${tone}-bg)` : 'var(--surface)',
        bd: on ? `var(--st-${tone}-dot)` : 'var(--border)',
      });
    }
    rows.push(row);
  }
  return rows;
}

/** Two-letter avatar fallback from a display name. */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}

/** RiskStatus values (lifecycle order) — the register's Status filter options. All 5 (i18n both locales). */
export const RISK_STATUSES: RiskStatus[] = ['Open', 'Mitigating', 'Escalated', 'Accepted', 'Closed'];

/** Exposure bands (high → low) — the register's Exposure filter options. */
export const RISK_EXPOSURES: RiskExposure[] = ['Critical', 'High', 'Medium', 'Low'];

/** RiskLevel values — the create form's Likelihood / Impact select options. */
export const RISK_LEVELS: RiskLevel[] = ['Low', 'Medium', 'High'];
