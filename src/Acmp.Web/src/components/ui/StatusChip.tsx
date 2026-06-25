/*
 * Status chip — the one way status is shown across ACMP (docs/14 §5).
 * Meaning is carried by the LABEL plus a colored dot, never by color alone
 * (WCAG 1.4.1 non-text contrast). `tone` selects the semantic role; `label`
 * is already-localized text supplied by the caller.
 */
export type StatusTone = 'neutral' | 'info' | 'scheduled' | 'warn' | 'success' | 'danger';

interface StatusChipProps {
  tone: StatusTone;
  label: string;
}

export function StatusChip({ tone, label }: StatusChipProps) {
  return (
    <span className={`status-chip ${tone}`}>
      <span className="status-chip-dot" aria-hidden="true" />
      {label}
    </span>
  );
}
