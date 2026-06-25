import { describe, it, expect } from 'vitest';
import { buildNav, accessFor } from './navModel';

describe('navModel role filtering', () => {
  it('shows the secretary the full committee + governance areas', () => {
    const nav = buildNav(['secretary']);
    const keys = nav.flatMap((g) => g.items.map((i) => i.key));
    expect(keys).toContain('backlog');
    expect(keys).toContain('decisions');
    expect(keys).toContain('submit');
    expect(keys).not.toContain('admin'); // admin is administrator-only
  });

  it('shows the administrator only admin + audit (no committee content)', () => {
    const nav = buildNav(['administrator']);
    const keys = nav.flatMap((g) => g.items.map((i) => i.key));
    expect(keys).toEqual(expect.arrayContaining(['home', 'reports', 'audit', 'admin']));
    expect(keys).not.toContain('backlog');
    expect(keys).not.toContain('decisions');
  });

  it('marks view-only areas without granting full access', () => {
    const nav = buildNav(['reviewer']);
    const backlog = nav.flatMap((g) => g.items).find((i) => i.key === 'backlog');
    expect(backlog?.access).toBe('view');
  });

  it('drops groups that have no visible items', () => {
    const nav = buildNav(['guest']);
    // Guest sees session/agenda(view)/diagrams(view)/home — never the System group.
    expect(nav.some((g) => g.labelKey === 'navGroup.system')).toBe(false);
  });

  it('accessFor takes the highest level across multiple roles', () => {
    // chairman has 'view' on backlog, secretary has 'full' → full wins.
    expect(accessFor('backlog', ['chairman', 'secretary'])).toBe('full');
    expect(accessFor('admin', ['secretary'])).toBe('none');
  });
});
