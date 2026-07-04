import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import { renderWithAuth } from '../../test/render';
import { RelationshipsAside } from './RelationshipsAside';
import type { TypeGroup } from './traceMeta';

const groups: TypeGroup[] = [
  { key: 'dep:DependsOn', labelKey: 'deps.kind.DependsOn', dir: 'up', items: [{ key: 'TOP-22', title: 'Pagination', href: '/topics/TOP-22' }] },
  { key: 'rel:Decision', labelKey: 'trace.type.Decision', artifactType: 'Decision', dir: 'down', items: [{ key: 'DECN-8', title: 'Approve', href: '/decisions/DECN-8' }, { key: 'ADR-3', title: 'ADR', href: null }] },
];

describe('RelationshipsAside (P10f)', () => {
  it('renders group headings, direction badges, counts, and navigable + routeless items', () => {
    renderWithAuth(<RelationshipsAside groups={groups} total={3} loading={false} onOpenGraph={() => {}} />);
    expect(screen.getByText('Depends on')).toBeInTheDocument();
    expect(screen.getByText('Decision')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /DECN-8/ })).toHaveAttribute('href', '/decisions/DECN-8');
    // routeless item is plain text, not a link
    expect(screen.queryByRole('link', { name: /ADR-3/ })).not.toBeInTheDocument();
    expect(screen.getByText('ADR-3')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument(); // total badge
  });

  it('collapses and expands a group via its toggle button', async () => {
    const user = userEvent.setup();
    renderWithAuth(<RelationshipsAside groups={groups} total={3} loading={false} onOpenGraph={() => {}} />);
    const toggle = screen.getByRole('button', { name: /Depends on/ });
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('link', { name: /TOP-22/ })).not.toBeInTheDocument();
  });

  it('invokes onOpenGraph from the footer button', async () => {
    const onOpenGraph = vi.fn();
    const user = userEvent.setup();
    renderWithAuth(<RelationshipsAside groups={groups} total={3} loading={false} onOpenGraph={onOpenGraph} />);
    await user.click(screen.getByRole('button', { name: /Open dependency graph/ }));
    expect(onOpenGraph).toHaveBeenCalledOnce();
  });

  it('shows loading and empty states', () => {
    const { rerender } = renderWithAuth(<RelationshipsAside groups={[]} total={0} loading onOpenGraph={() => {}} />);
    expect(screen.getByRole('status')).toBeInTheDocument();
    rerender(<RelationshipsAside groups={[]} total={0} loading={false} onOpenGraph={() => {}} />);
    expect(screen.getByText('No typed relationships or dependencies yet.')).toBeInTheDocument();
  });

  it('is axe-clean', async () => {
    const { container } = renderWithAuth(<RelationshipsAside groups={groups} total={3} loading={false} onOpenGraph={() => {}} />);
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
