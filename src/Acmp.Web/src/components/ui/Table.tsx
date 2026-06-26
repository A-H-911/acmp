import type { ReactNode } from 'react';
import { Icon } from '../icons';

export type SortDir = 'asc' | 'desc';

export interface Column<T> {
  id: string;
  header: ReactNode;
  cell: (row: T) => ReactNode;
  sortable?: boolean;
  align?: 'start' | 'end';
  /** CSS width for this column's <col> (e.g. "160px", "1.5fr" is NOT valid here — use px/%). */
  width?: string;
}

interface TableProps<T> {
  columns: Column<T>[];
  rows: T[];
  getRowKey: (row: T) => string;
  /** Accessible table name (visually-hidden caption). */
  caption: string;
  sort?: { by: string; dir: SortDir };
  onSortChange?: (id: string) => void;
  /** Optional filter/action toolbar rendered above the header. */
  toolbar?: ReactNode;
}

/**
 * Column-driven, semantic data table (Design System §09): sortable headers with
 * aria-sort, optional toolbar, hover rows. Real <table> markup for screen-reader
 * grid semantics; stays readable in RTL and dark via tokens + logical properties.
 */
export function Table<T>({ columns, rows, getRowKey, caption, sort, onSortChange, toolbar }: TableProps<T>) {
  return (
    <div className="table-wrap">
      {toolbar && <div className="table-toolbar">{toolbar}</div>}
      <table className="table">
        <caption className="visually-hidden">{caption}</caption>
        <colgroup>
          {columns.map((c) => (
            <col key={c.id} style={c.width ? { width: c.width } : undefined} />
          ))}
        </colgroup>
        <thead className="table-head">
          <tr>
            {columns.map((c) => {
              const isSorted = sort?.by === c.id;
              const ariaSort = c.sortable
                ? isSorted
                  ? sort!.dir === 'asc'
                    ? 'ascending'
                    : 'descending'
                  : 'none'
                : undefined;
              return (
                <th
                  key={c.id}
                  className="table-hcell"
                  scope="col"
                  aria-sort={ariaSort}
                  style={{ textAlign: c.align ?? 'start' }}
                >
                  {c.sortable && onSortChange ? (
                    <button type="button" className={`table-sort ${isSorted ? 'active' : ''}`} onClick={() => onSortChange(c.id)}>
                      {c.header}
                      {isSorted && <Icon name={sort!.dir === 'asc' ? 'arrowUp' : 'arrowDown'} size={12} aria-hidden />}
                    </button>
                  ) : (
                    c.header
                  )}
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={getRowKey(row)} className="table-row">
              {columns.map((c) => (
                <td key={c.id} className="table-cell" style={{ textAlign: c.align ?? 'start' }}>
                  {c.cell(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
