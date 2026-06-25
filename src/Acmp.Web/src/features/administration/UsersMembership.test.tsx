import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
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

describe('UsersMembership (AC-059)', () => {
  beforeEach(() => mockUseMembers.mockReset());

  it('renders a directory row per member with role sourced from Keycloak', () => {
    result({ data: MEMBERS });
    renderWithAuth(<UsersMembership />, { roles: ['administrator'] });

    expect(screen.getByText('Khalid A')).toBeInTheDocument();
    expect(screen.getByText('khalid@acmp.gov')).toBeInTheDocument();
    expect(screen.getByText('Secretary')).toBeInTheDocument();
    expect(screen.getByText('Auditor')).toBeInTheDocument();
    // Roles are read-only — every row marks its source.
    expect(screen.getAllByText('from Keycloak')).toHaveLength(2);
  });

  it('shows the Keycloak read-only governance banner', () => {
    result({ data: MEMBERS });
    renderWithAuth(<UsersMembership />, { roles: ['administrator'] });
    expect(screen.getByText('Roles are read-only')).toBeInTheDocument();
  });

  it('marks voting eligibility and a member with no streams as an observer', () => {
    result({ data: MEMBERS });
    renderWithAuth(<UsersMembership />, { roles: ['administrator'] });
    expect(screen.getByText('Voting-eligible')).toBeInTheDocument();
    expect(screen.getByText('Observer')).toBeInTheDocument();
  });

  it('exposes a table with the four directory columns', () => {
    result({ data: MEMBERS });
    renderWithAuth(<UsersMembership />, { roles: ['administrator'] });
    expect(screen.getAllByRole('columnheader')).toHaveLength(4);
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
});
