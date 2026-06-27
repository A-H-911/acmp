import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import { UsersMembership } from './UsersMembership';
import { renderWithAuth } from '../../test/render';
import type { Member } from '../../api/members';

vi.mock('../../api/members', () => ({ useMembers: vi.fn() }));
import { useMembers } from '../../api/members';

const mockUseMembers = useMembers as unknown as Mock;

function result(over: Partial<ReturnType<typeof useMembers>>) {
  mockUseMembers.mockReturnValue({ data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over });
}

const MEMBERS: Member[] = [
  {
    publicId: '1', fullName: 'Khalid A', email: 'khalid@acmp.gov', role: 'Secretary',
    status: 'Active', isActive: true, isVotingEligible: true,
    streams: [{ publicId: 's1', code: 'architecture', nameEn: 'Architecture', nameAr: 'الهندسة' }],
  },
  {
    publicId: '2', fullName: 'Audit Office', email: 'audit@acmp.gov', role: 'Auditor',
    status: 'Active', isActive: true, isVotingEligible: false, streams: [],
  },
];

function renderDirectory() {
  result({ data: MEMBERS });
  renderWithAuth(<UsersMembership />, { roles: ['administrator'] });
}

describe('UsersMembership (AC-059)', () => {
  beforeEach(() => mockUseMembers.mockReset());

  it('renders a directory row per member with role sourced from Keycloak', () => {
    renderDirectory();
    expect(screen.getByText('Khalid A')).toBeInTheDocument();
    expect(screen.getByText('khalid@acmp.gov')).toBeInTheDocument();
    expect(screen.getByText('Secretary')).toBeInTheDocument();
    expect(screen.getByText('Auditor')).toBeInTheDocument();
    // Roles are read-only — every row marks its source.
    expect(screen.getAllByText('from Keycloak')).toHaveLength(2);
  });

  it('shows the Keycloak read-only governance banner', () => {
    renderDirectory();
    expect(screen.getByText('Roles are read-only')).toBeInTheDocument();
  });

  it('renders the seven Administration sub-tabs with only Users enabled (matches the design)', () => {
    renderDirectory();
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(7);
    const users = screen.getByRole('tab', { name: /Users & Membership/ });
    expect(users).toHaveAttribute('aria-selected', 'true');
    // The other six are later-phase placeholders — present but disabled.
    expect(tabs.filter((t) => (t as HTMLButtonElement).disabled)).toHaveLength(6);
  });

  it('does not render the provision/invite affordances (ADR-0015: manual Keycloak provisioning)', () => {
    renderDirectory();
    expect(screen.queryByRole('button', { name: /provision/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/Provision via Keycloak/i)).not.toBeInTheDocument();
  });

  it('marks a member with no streams as an observer', () => {
    renderDirectory();
    expect(screen.getByText('Observer')).toBeInTheDocument();
    expect(screen.getAllByText('Voting-eligible')).toHaveLength(2); // label per row
  });

  it('exposes a table with the five directory columns (matches the design layout)', () => {
    renderDirectory();
    expect(screen.getAllByRole('columnheader')).toHaveLength(5);
  });

  it('renders the read-only voting-eligibility switch per member', () => {
    renderDirectory();
    const switches = screen.getAllByRole('switch');
    expect(switches).toHaveLength(2);
    expect(switches[0]).toHaveAttribute('aria-checked', 'true'); // Khalid: voting-eligible
    expect(switches[1]).toHaveAttribute('aria-checked', 'false'); // Audit Office: not
    switches.forEach((s) => expect(s).toHaveAttribute('aria-disabled', 'true'));
  });

  it('renders the membership add-committee affordance as inert (stream editing → later phase)', () => {
    renderDirectory();
    const add = screen.getAllByRole('button', { name: 'Add committee' });
    expect(add).toHaveLength(2);
    add.forEach((b) => expect(b).toBeDisabled());
  });

  it('opens a read-only user detail from the row view button (no invite)', async () => {
    const user = userEvent.setup();
    renderDirectory();
    const view = screen.getAllByRole('button', { name: 'View user detail' });
    expect(view).toHaveLength(2);
    await user.click(view[0]); // Khalid

    expect(screen.getByText('Back to users')).toBeInTheDocument();
    expect(screen.getByText('Committee & stream memberships')).toBeInTheDocument();
    expect(screen.getByText('Architecture')).toBeInTheDocument(); // his stream membership
    // Read-only: role source note, no invite/provision flow.
    expect(screen.getByText(/Role is read-only/)).toBeInTheDocument();
    expect(screen.queryByText(/invit/i)).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Back to users' }));
    expect(screen.getByText('Roles are read-only')).toBeInTheDocument(); // back on the list
  });

  it('shows the empty state when there are no members', () => {
    result({ data: [] });
    renderWithAuth(<UsersMembership />, { roles: ['administrator'] });
    expect(screen.getByText('No members yet')).toBeInTheDocument();
  });

  it('shows the loading state while fetching', () => {
    result({ isLoading: true });
    renderWithAuth(<UsersMembership />, { roles: ['administrator'] });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows an error state with retry on failure', () => {
    result({ isError: true });
    renderWithAuth(<UsersMembership />, { roles: ['administrator'] });
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('is axe-clean on the directory and the detail (WCAG 2.2 AA structure/ARIA)', async () => {
    const user = userEvent.setup();
    renderDirectory();
    let results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);

    await user.click(screen.getAllByRole('button', { name: 'View user detail' })[0]);
    results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
