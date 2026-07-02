/*
 * Pure presentation mappers for Action read models (register + detail).
 * Status tone maps the 6 canonical ActionStatus wire names (docs/12 §7) onto the
 * shared StatusChip tones; meaning is carried by the localized label + a colored
 * dot, never colour alone (WCAG 1.4.1). Progress-bar colour follows the design's
 * thresholds in "ACMP Lists & Registers.dc.html".
 */
import type { StatusTone } from '../../components/ui/StatusChip';
import type { ActionStatus } from '../../api/actions';

// Open/InProgress/Blocked/Completed/Verified match the design's actStatus map exactly. Cancelled has
// no visual in "ACMP Lists & Registers.dc.html" (its actStatus lists only 5) — a no-reference tone
// choice: `danger` (terminal red) so a cancelled action reads visually distinct from Open (neutral),
// rather than colliding with it; flagged in the progress log.
const STATUS_TONE: Record<ActionStatus, StatusTone> = {
  Open: 'neutral',
  InProgress: 'info',
  Blocked: 'danger',
  Completed: 'success',
  Verified: 'scheduled',
  Cancelled: 'danger',
};

export function statusTone(status: ActionStatus): StatusTone {
  return STATUS_TONE[status] ?? 'neutral';
}

/** Register progress-bar colour (design `pctColor`, 4-way): ≥100 green, ≥50 accent, ≥25 warn, else danger. */
export function progressColor(pct: number): string {
  if (pct >= 100) return 'var(--st-success-dot)';
  if (pct >= 50) return 'var(--accent)';
  if (pct >= 25) return 'var(--st-warn-dot)';
  return 'var(--st-danger-dot)';
}

/** Detail progress-bar colour (design detail `pctColor`, 3-way): ≥100 green, ≥50 accent, else warn. */
export function progressColorDetail(pct: number): string {
  if (pct >= 100) return 'var(--st-success-dot)';
  if (pct >= 50) return 'var(--accent)';
  return 'var(--st-warn-dot)';
}

/** Two-letter avatar fallback from a display name. */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}

/** ActionStatus values in lifecycle order — the register's Status filter options. */
export const ACTION_STATUSES: ActionStatus[] = ['Open', 'InProgress', 'Blocked', 'Completed', 'Verified', 'Cancelled'];

/** The W14 lifecycle transitions a user can trigger (docs/12 §7 — one POST endpoint each). */
export type ActionTransition = 'start' | 'block' | 'unblock' | 'progress' | 'complete' | 'cancel' | 'verify';

/**
 * Which transitions each status permits — mirrors the ActionItem domain guards EXACTLY (RequireStatus):
 *   Open        → Start · Update progress · Cancel
 *   InProgress  → Block · Update progress · Complete · Cancel
 *   Blocked     → Unblock · Update progress · Cancel
 *   Completed   → Verify · Cancel
 *   Verified/Cancelled → terminal (none)
 * A wrong button would hit a domain 400/409, so the UI shows only what the state allows. Role/owner
 * gating (docs/10 rows 14–15 + SoD-1 on `verify`) is layered on top at render time.
 */
export const ALLOWED_TRANSITIONS: Record<ActionStatus, ActionTransition[]> = {
  Open: ['start', 'progress', 'cancel'],
  InProgress: ['block', 'progress', 'complete', 'cancel'],
  Blocked: ['unblock', 'progress', 'cancel'],
  Completed: ['verify', 'cancel'],
  Verified: [],
  Cancelled: [],
};
