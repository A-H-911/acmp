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

/*
 * DEF-034. Both halves of that defect were invisible to the suite above, which only ever renders
 * SINGLE-role principals — and for a single role `roles[0]` is right by coincidence. It took a real
 * five-role user on production to expose it.
 *
 * The claim order below is deliberately hostile: 'auditor' first, which is exactly what Keycloak
 * returned for that account, and exactly what the old code displayed.
 */
describe('SideNav "viewing as" badge (DEF-034)', () => {
  it('shows the HIGHEST-PRIVILEGE role, not the first claim in the array', () => {
    renderWithAuth(<SideNav />, { roles: ['auditor', 'secretary'] });
    const badge = screen.getByText(/viewing as/i);
    expect(badge).toHaveTextContent(/Secretary/i);
    expect(badge).not.toHaveTextContent(/Auditor/i);
  });

  it('renders the TRANSLATED label, never a raw claim string', () => {
    // The old key was `roles.${role}`; the locale defines `role.` (singular), so the lookup always
    // missed and i18next fell through to its defaultValue — printing the bare lowercase claim.
    // Asserting the capitalised label is what distinguishes a real translation from that fallback.
    renderWithAuth(<SideNav />, { roles: ['auditor'] });
    const badge = screen.getByText(/viewing as/i);
    expect(badge).toHaveTextContent('Auditor');
    expect(badge.textContent).not.toContain('auditor');
  });
});
