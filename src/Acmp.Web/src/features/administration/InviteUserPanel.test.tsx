import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InviteUserPanel } from './InviteUserPanel';
import { renderWithAuth } from '../../test/render';

// ADR-0043 step 4 — the panel now reads the taxonomy for its REQUIRED stream field. Stubbed so this
// suite stays query-free while rendering the REAL chips, which is what these tests then click.
vi.mock('../../api/members', () => ({
  useInviteUser: vi.fn(),
  streamName: (s: { nameEn: string; nameAr: string }, isArabic: boolean) => (isArabic ? s.nameAr : s.nameEn),
  useStreams: () => ({
    data: [
      { publicId: 's1', code: 'core', nameEn: 'Core', nameAr: 'الأساسي', isWildcard: false },
      { publicId: 'sw', code: 'all-streams', nameEn: 'All streams', nameAr: 'كل المسارات', isWildcard: true },
    ],
    isLoading: false,
    isError: false,
  }),
}));
import { useInviteUser } from '../../api/members';

const mockUseInviteUser = useInviteUser as unknown as Mock;

function invite(over: Partial<{ mutate: unknown; isPending: boolean; isError: boolean }> = {}) {
  const mutate = vi.fn();
  mockUseInviteUser.mockReturnValue({ mutate, isPending: false, isError: false, ...over });
  return mutate as Mock;
}

const INVITED = {
  publicId: 'p1',
  fullName: 'New Person',
  email: 'new@acmp.gov',
  status: 'Invited',
  temporaryPassword: 'T3mp-Pass-Xyz',
};

describe('InviteUserPanel (FR-156 / AC-088)', () => {
  // Without this, a "was it called?" assertion can see the previous test's clicks.
  beforeEach(() => vi.clearAllMocks());

  it('submits the trimmed email and full name', async () => {
    const mutate = invite();
    renderWithAuth(<InviteUserPanel />);

    await userEvent.type(screen.getByLabelText(/Email address/), '  new@acmp.gov  ');
    await userEvent.type(screen.getByLabelText(/Full name/), '  New Person  ');
    await userEvent.click(screen.getByRole('button', { name: 'Core' }));
    await userEvent.click(screen.getByRole('button', { name: /send invitation/i }));

    expect(mutate).toHaveBeenCalledWith(
      { email: 'new@acmp.gov', fullName: 'New Person', streamPublicIds: ['s1'] },
      expect.anything(),
    );
  });

  // ⚠ THREE requirements now, not two. ADR-0043 clause (2) makes the stream REQUIRED: an invite
  // without one creates a member who, once step 7 wires stream scope, can write nothing at all. The
  // email+name step is asserted STILL DISABLED so the stream is proven to be doing the gating,
  // rather than the test passing because the other two fields happened to be empty.
  it('keeps submit disabled until email, name AND a stream are given', async () => {
    invite();
    renderWithAuth(<InviteUserPanel />);
    const submit = screen.getByRole('button', { name: /send invitation/i });

    expect(submit).toBeDisabled();
    await userEvent.type(screen.getByLabelText(/Email address/), 'a@b.com');
    expect(submit).toBeDisabled();

    await userEvent.type(screen.getByLabelText(/Full name/), 'A B');
    expect(submit).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Core' }));
    expect(submit).toBeEnabled();
  });

  // ⚠ DEC-044: the wildcard is SELECTABLE but must never be pre-selected. All 26 existing members
  // start on it via the step-5 backfill, so if invites defaulted to it too, stream scope would never
  // restrict anyone and the control would be decorative.
  it('offers the wildcard but leaves it unselected', () => {
    invite();
    renderWithAuth(<InviteUserPanel />);

    expect(screen.getByRole('button', { name: 'All streams' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: 'Core' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('can invite someone as unrestricted by choosing the wildcard', async () => {
    const mutate = invite();
    renderWithAuth(<InviteUserPanel />);

    await userEvent.type(screen.getByLabelText(/Email address/), 'a@b.com');
    await userEvent.type(screen.getByLabelText(/Full name/), 'A B');
    await userEvent.click(screen.getByRole('button', { name: 'All streams' }));
    await userEvent.click(screen.getByRole('button', { name: /send invitation/i }));

    expect(mutate).toHaveBeenCalledWith(
      { email: 'a@b.com', fullName: 'A B', streamPublicIds: ['sw'] },
      expect.anything(),
    );
  });

  it('shows a pending label while the invite is in flight', () => {
    invite({ isPending: true });
    renderWithAuth(<InviteUserPanel />);

    expect(screen.getByRole('button', { name: /inviting/i })).toBeDisabled();
  });

  it('surfaces a failure as an alert instead of failing silently', () => {
    invite({ isError: true });
    renderWithAuth(<InviteUserPanel />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('reveals the temporary password ONCE and replaces the form', async () => {
    const mutate = vi.fn((_body, opts) => opts.onSuccess(INVITED));
    mockUseInviteUser.mockReturnValue({ mutate, isPending: false, isError: false });
    renderWithAuth(<InviteUserPanel />);

    await userEvent.type(screen.getByLabelText(/Email address/), 'new@acmp.gov');
    await userEvent.type(screen.getByLabelText(/Full name/), 'New Person');
    await userEvent.click(screen.getByRole('button', { name: 'Core' }));
    await userEvent.click(screen.getByRole('button', { name: /send invitation/i }));

    expect(screen.getByText(INVITED.temporaryPassword)).toBeInTheDocument();
    // The form is GONE, not merely reset: leaving it live invites a second submission while a
    // credential is on screen, and the password would be lost behind the new result with no way
    // to recover it — there is no second chance to read it.
    expect(screen.queryByLabelText(/Email address/)).not.toBeInTheDocument();
  });

  it('copies the password to the clipboard and confirms it', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText } });
    const mutate = vi.fn((_body, opts) => opts.onSuccess(INVITED));
    mockUseInviteUser.mockReturnValue({ mutate, isPending: false, isError: false });
    renderWithAuth(<InviteUserPanel />);

    await userEvent.type(screen.getByLabelText(/Email address/), 'new@acmp.gov');
    await userEvent.type(screen.getByLabelText(/Full name/), 'New Person');
    await userEvent.click(screen.getByRole('button', { name: 'Core' }));
    await userEvent.click(screen.getByRole('button', { name: /send invitation/i }));
    await userEvent.click(screen.getByRole('button', { name: 'Copy' }));

    expect(writeText).toHaveBeenCalledWith(INVITED.temporaryPassword);
    expect(await screen.findByRole('button', { name: 'Copied' })).toBeInTheDocument();
  });

  it('returns to an empty form for the next invite, discarding the password', async () => {
    const mutate = vi.fn((_body, opts) => opts.onSuccess(INVITED));
    mockUseInviteUser.mockReturnValue({ mutate, isPending: false, isError: false });
    renderWithAuth(<InviteUserPanel />);

    await userEvent.type(screen.getByLabelText(/Email address/), 'new@acmp.gov');
    await userEvent.type(screen.getByLabelText(/Full name/), 'New Person');
    await userEvent.click(screen.getByRole('button', { name: 'Core' }));
    await userEvent.click(screen.getByRole('button', { name: /send invitation/i }));
    await userEvent.click(screen.getByRole('button', { name: /invite someone else/i }));

    // The credential is not recoverable once dismissed — which is the point of "shown once".
    expect(screen.queryByText(INVITED.temporaryPassword)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/Email address/)).toHaveValue('');
  });

  it('does not submit when the form is submitted with empty fields', async () => {
    const mutate = invite();
    renderWithAuth(<InviteUserPanel />);

    // Guarding the handler as well as the button: a form can be submitted by Enter, and a disabled
    // button is presentation rather than a rule.
    screen.getByRole('button', { name: /send invitation/i }).closest('form')!.requestSubmit();

    expect(mutate).not.toHaveBeenCalled();
  });
});
