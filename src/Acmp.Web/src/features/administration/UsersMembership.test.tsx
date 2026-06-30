import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import { UsersDirectory, UserDetail } from './UsersMembership';
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

function renderDirectory(onView = vi.fn()) {
  result({ data: MEMBERS });
  renderWithAuth(<UsersDirectory onView={onView} />, { roles: ['administrator'] });
}

describe('UsersDirectory (AC-059)', () => {
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

  it('does not render the provision/invite affordances (ADR-0015 / OQ-042: manual Keycloak provisioning)', () => {
    renderDirectory();
    expect(screen.queryByRole('button', { name: /provision/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/Provision via Keycloak/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/invit/i)).not.toBeInTheDocument();
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

  it('keeps the assignments count an honest dash (no count API yet)', () => {
    renderDirectory();
    // One dash per member row in the assignments cell.
    expect(screen.getAllByTitle(/Topic and action assignments/)).toHaveLength(2);
  });

  it('renders the membership add-committee affordance as inert (stream editing → later phase)', () => {
    renderDirectory();
    const add = screen.getAllByRole('button', { name: 'Add committee' });
    expect(add).toHaveLength(2);
    add.forEach((b) => expect(b).toBeDisabled());
  });

  it('calls onView with the member when the row view button is clicked', async () => {
    const user = userEvent.setup();
    const onView = vi.fn();
    renderDirectory(onView);
    const view = screen.getAllByRole('button', { name: 'View user detail' });
    expect(view).toHaveLength(2);
    await user.click(view[0]);
    expect(onView).toHaveBeenCalledWith(MEMBERS[0]);
  });

  it('shows the empty state when there are no members', () => {
    result({ data: [] });
    renderWithAuth(<UsersDirectory onView={vi.fn()} />, { roles: ['administrator'] });
    expect(screen.getByText('No members yet')).toBeInTheDocument();
  });

  it('shows the loading state while fetching', () => {
    result({ isLoading: true });
    renderWithAuth(<UsersDirectory onView={vi.fn()} />, { roles: ['administrator'] });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows an error state with retry on failure', () => {
    result({ isError: true });
    renderWithAuth(<UsersDirectory onView={vi.fn()} />, { roles: ['administrator'] });
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('is axe-clean on the directory (WCAG 2.2 AA structure/ARIA)', async () => {
    renderDirectory();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});

describe('UserDetail (read-only, no invite per ADR-0015)', () => {
  it('renders the read-only detail with memberships and no invite flow', async () => {
    const onBack = vi.fn();
    renderWithAuth(<UserDetail member={MEMBERS[0]} isArabic={false} onBack={onBack} />, { roles: ['administrator'] });

    expect(screen.getByText('Back to users')).toBeInTheDocument();
    expect(screen.getByText('Committee & stream memberships')).toBeInTheDocument();
    expect(screen.getByText('Architecture')).toBeInTheDocument(); // his stream membership
    expect(screen.getByText(/Role is read-only/)).toBeInTheDocument();
    expect(screen.queryByText(/invit/i)).not.toBeInTheDocument();

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Back to users' }));
    expect(onBack).toHaveBeenCalled();
  });

  it('shows an honest empty memberships note for an observer', () => {
    renderWithAuth(<UserDetail member={MEMBERS[1]} isArabic={false} onBack={vi.fn()} />, { roles: ['administrator'] });
    expect(screen.getByText('No committee or stream memberships.')).toBeInTheDocument();
  });
});
