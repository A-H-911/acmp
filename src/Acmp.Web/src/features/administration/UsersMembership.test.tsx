import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import { UsersDirectory, UserDetail } from './UsersMembership';
import { renderWithAuth } from '../../test/render';
import type { Member } from '../../api/members';

// UserDetail renders InviteUserPanel (FR-156) and RoleAssignmentPanel (FR-157), so the module mock
// has to cover their hooks or the detail tests fail on an undefined hook rather than on anything
// real. The panels' own behaviour is asserted against the wire in their own suites — this file is
// the screen, so it only proves they are mounted.
vi.mock('../../api/members', () => ({
  useMembers: vi.fn(),
  useInviteUser: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
  useAssignRoles: () => ({ mutate: vi.fn(), reset: vi.fn(), isPending: false, isError: false, isSuccess: false }),
}));
import { useMembers } from '../../api/members';

const mockUseMembers = useMembers as unknown as Mock;

function result(over: Partial<ReturnType<typeof useMembers>>) {
  mockUseMembers.mockReturnValue({ data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over });
}

const MEMBERS: Member[] = [
  {
    publicId: '1', keycloakUserId: 'kc-fixture', fullName: 'Khalid A', email: 'khalid@acmp.gov', role: 'Secretary',
    status: 'Active', isActive: true, isVotingEligible: true,
    streams: [{ publicId: 's1', code: 'architecture', nameEn: 'Architecture', nameAr: 'الهيكلة' }],
  },
  {
    publicId: '2', keycloakUserId: 'kc-fixture', fullName: 'Audit Office', email: 'audit@acmp.gov', role: 'Auditor',
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
    // The lock note survives ADR-0038: roles are writable now, but Keycloak is still where they
    // live, and the directory row remains read-only — the editor is one level in, on the detail.
    expect(screen.getAllByText('from Keycloak')).toHaveLength(2);
  });

  // ⚠ THIS WAS NAMED "shows the Keycloak read-only governance banner" AND ASSERTED "Roles are
  // read-only". Both were true until ADR-0038, which supersedes ADR-0015 §Q3 and gives the user
  // detail a working role editor (SC-004) — so the banner was making a claim the product no longer
  // honours, and the test was holding it in place. The name changes with the assertion, because a
  // name that describes the old rule is exactly how the last supersession went unnoticed.
  it('shows the Keycloak source-of-truth banner (roles are writable since ADR-0038)', () => {
    renderDirectory();
    expect(screen.getByText('Keycloak is the source of truth')).toBeInTheDocument();
    // The half that did NOT change: identity is federated and nobody signs themselves up.
    expect(screen.getByText(/no self-registration/i)).toBeInTheDocument();
  });

  // ⚠ WAS "(ADR-0015 / OQ-042: manual Keycloak provisioning)" and asserted no "invit" text at all.
  // OQ-042 resolved to "deep-link to the Keycloak admin console only, carrying no in-app
  // account-creation form", and ADR-0038 overtakes that resolution exactly as it overtakes
  // ADR-0015 §Q3 — the register still said otherwise, which is the SC-004 pattern a second time.
  // What is STILL true is narrower and is what this now asserts: the DIRECTORY hosts no invite
  // control and never got the design's "Provision via Keycloak" button. The invite lives one level
  // in, on the user detail, and the banner now points there — so a mention of it here is correct.
  it('keeps the directory itself free of provisioning controls (the invite lives on the user detail)', () => {
    renderDirectory();
    expect(screen.queryByRole('button', { name: /provision/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/Provision via Keycloak/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /invite/i })).not.toBeInTheDocument();
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

// ⚠ THIS BLOCK USED TO ASSERT "no invite flow per ADR-0015", and that was CORRECT until today.
// ADR-0015 §Q3 states plainly: "User provisioning is manual via the self-hosted Keycloak admin
// console. ACMP does not integrate the Keycloak Admin API in v1." ADR-0038 (Approved 2026-08-11)
// REVERSES that clause — the rest of ADR-0015, self-hosting Keycloak and bundling every runtime
// dependency, is untouched and still holds. The assertion is therefore removed because the decision
// behind it changed, not because it became inconvenient; SC-004 records the supersession.
describe('UserDetail (invite + role assignment per ADR-0038, superseding ADR-0015 §Q3)', () => {
  beforeEach(() => result({ data: MEMBERS }));

  it('renders the detail with memberships, the role editor and the invite section', async () => {
    const onBack = vi.fn();
    renderWithAuth(<UserDetail member={MEMBERS[0]} isArabic={false} onBack={onBack} />, { roles: ['administrator'] });

    expect(screen.getByText('Back to users')).toBeInTheDocument();
    expect(screen.getByText('Committee & stream memberships')).toBeInTheDocument();
    expect(screen.getByText('Architecture')).toBeInTheDocument(); // his stream membership
    // ⚠ WAS `expect(screen.getByText(/Role is read-only/))`. The padlock said the role was managed
    // in Keycloak and could not be changed here; FR-157 changes it here. Keycloak is still where it
    // is STORED, which is all the replacement claims.
    expect(screen.getByText('stored in Keycloak')).toBeInTheDocument();
    // §(8) puts the invite section at the foot of this view; FR-157's editor sits above it.
    expect(screen.getByText('Invite a new user')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save role' })).toBeInTheDocument();

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: 'Back to users' }));
    expect(onBack).toHaveBeenCalled();
  });

  it('shows an honest empty memberships note for an observer', () => {
    renderWithAuth(<UserDetail member={MEMBERS[1]} isArabic={false} onBack={vi.fn()} />, { roles: ['administrator'] });
    expect(screen.getByText('No committee or stream memberships.')).toBeInTheDocument();
  });

  // The container passes the member it snapshotted when the row was clicked, so after a role change
  // the head would keep showing the OLD role and a successful change would read as a failed one.
  // The detail re-reads the roster cache the assignment invalidates.
  it('shows the refreshed role after the roster reports the change, not the clicked snapshot', () => {
    result({ data: [{ ...MEMBERS[0], role: 'Reviewer' }, MEMBERS[1]] });
    renderWithAuth(<UserDetail member={MEMBERS[0]} isArabic={false} onBack={vi.fn()} />, { roles: ['administrator'] });

    expect(screen.getAllByText('Reviewer').length).toBeGreaterThan(0);
    expect(screen.queryByText('Secretary')).not.toBeInTheDocument(); // the stale snapshot's role
  });
});
