import { Icon } from '../icons';

interface PaginationLabels {
  nav: string;
  previous: string;
  next: string;
}

interface PaginationProps {
  page: number;
  pageCount: number;
  onPageChange: (page: number) => void;
  /** Caller-localized accessible names (the library carries no strings). */
  labels: PaginationLabels;
}

/**
 * Pagination (Design System §11). Directionality is handled in CSS (.pagination-prev/next)
 * so the chevrons point to the correct logical edge in both LTR and RTL.
 */
export function Pagination({ page, pageCount, onPageChange, labels }: PaginationProps) {
  if (pageCount <= 1) return null;
  const pages = Array.from({ length: pageCount }, (_, i) => i + 1);
  return (
    <nav className="pagination" aria-label={labels.nav}>
      <button
        type="button"
        className="pagination-btn pagination-prev"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
        aria-label={labels.previous}
      >
        <Icon name="chevron" size={14} aria-hidden />
      </button>
      {pages.map((p) => (
        <button
          key={p}
          type="button"
          className="pagination-page"
          aria-current={p === page ? 'page' : undefined}
          onClick={() => onPageChange(p)}
        >
          {p}
        </button>
      ))}
      <button
        type="button"
        className="pagination-btn pagination-next"
        disabled={page >= pageCount}
        onClick={() => onPageChange(page + 1)}
        aria-label={labels.next}
      >
        <Icon name="chevron" size={14} aria-hidden />
      </button>
    </nav>
  );
}
