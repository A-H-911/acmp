import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import MembersPage from './MembersPage';
import { renderWithAuth } from '../test/render';
import type { Member } from '../api/members';

/*
 * FR-156 / FR-157 — the Members area (OQ-069, resolved by DEC-041; INV-014 divergence recorded as
 * SC-008). This is where the roster, the invite and the role editor live now that Administration is
 * no longer their home: both requirements say "Administrator OR Secretary" and the server has always
 * honoured that, while /admin admits only the Administrator.
 *
 * The user-detail sub-state assertions moved here from AdministrationPage.test.tsx unchanged — the
 * components were MOVED, not redesigned, so the behaviour the design fixes must still hold.
 */
vi.mock('../api/members', () => ({
  useMembers: vi.fn(),
  useInviteUser: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useAssignRoles: () => ({ mutate: vi.fn(), reset: vi.fn(), isPending: false, isError: false, isSuccess: false }),
}));
import { useMembers } from '../api/members';
const mockUseMembers = useMembers as unknown as Mock;

const MEMBERS: Member[] = [
  {
    publicId: '1', keycloakUserId: 'kc-fixture', fullName: 'Khalid A', email: 'khalid@acmp.gov', role: 'Secretary',
    status: 'Active', isActive: true, isVotingEligible: true,
    streams: [{ publicId: 's1', code: 'architecture', nameEn: 'Architecture', nameAr: 'الهيكلة' }],
  },
];

function renderPage(roles: Parameters<typeof renderWithAuth>[1] = { roles: ['secretary'] }) {
  return renderWithAuth(<MembersPage />, roles);
}

describe('MembersPage (FR-156 / FR-157, OQ-069)', () => {
  beforeEach(() => {
    mockUseMembers.mockReset();
    mockUseMembers.mockReturnValue({ data: MEMBERS, isLoading: false, isError: false });
  });

  it('renders the roster for a SECRETARY — the role the requirements name and /admin refused', () => {
    renderPage();
    expect(screen.getByRole('heading', { name: 'Members' })).toBeInTheDocument();
    expect(screen.getByText('Keycloak is the source of truth')).toBeInTheDocument();
    expect(screen.getByText('Khalid A')).toBeInTheDocument();
  });

  it('renders the same roster for an Administrator', () => {
    renderPage({ roles: ['administrator'] });
    expect(screen.getByText('Khalid A')).toBeInTheDocument();
  });

  it('opening a user detail replaces the roster (the design userdetail sub-state)', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole('button', { name: 'View user detail' }));
    expect(screen.getByText('Back to users')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Back to users' }));
    expect(screen.getByText('Khalid A')).toBeInTheDocument();
  });

  it('the user detail carries the invite and the role editor — both requirements, one surface', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole('button', { name: 'View user detail' }));

    // FR-156's invite (AC-088) and FR-157's role assignment (AC-089) are the two affordances this
    // area exists to make reachable; a Secretary seeing neither is the defect OQ-069 recorded.
    expect(screen.getByLabelText(/Email address/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /committee role/i })).toBeInTheDocument();
  });
});
