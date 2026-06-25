import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { SideNav } from './SideNav';
import { renderWithAuth } from '../../test/render';

describe('SideNav role filtering (FR-024)', () => {
  it('shows the secretary committee nav but not Administration', () => {
    renderWithAuth(<SideNav />, { roles: ['secretary'] });
    expect(screen.getByRole('link', { name: /Backlog/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Administration/i })).not.toBeInTheDocument();
  });

  it('shows the administrator the admin area but not the committee backlog', () => {
    renderWithAuth(<SideNav />, { roles: ['administrator'] });
    expect(screen.getByRole('link', { name: /Administration/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Backlog/i })).not.toBeInTheDocument();
  });

  it('marks a view-only area with a read-only indicator', () => {
    renderWithAuth(<SideNav />, { roles: ['reviewer'] });
    // Reviewer has view access to Backlog → the read-only eye icon is present.
    const backlog = screen.getByRole('link', { name: /Backlog/i });
    expect(backlog.querySelector('[title="Read-only"]')).toBeTruthy();
  });
});
