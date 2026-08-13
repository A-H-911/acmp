import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StreamAssignmentPanel } from './StreamAssignmentPanel';
import { useStreams, useAssignStreams, type Member, type StreamRef } from '../../api/members';
import i18n from '../../i18n';

const CORE: StreamRef = { publicId: 's1', code: 'core', nameEn: 'Core', nameAr: 'الأساسي', isWildcard: false };
const GOV: StreamRef = { publicId: 's2', code: 'government', nameEn: 'Government', nameAr: 'الحكومي', isWildcard: false };
const WILDCARD: StreamRef = { publicId: 'sw', code: 'all-streams', nameEn: 'All streams', nameAr: 'كل المسارات', isWildcard: true };

vi.mock('../../api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/members')>()),
  useStreams: vi.fn(),
  useAssignStreams: vi.fn(),
}));

const mockStreams = useStreams as unknown as ReturnType<typeof vi.fn>;
const mockAssign = useAssignStreams as unknown as ReturnType<typeof vi.fn>;

const member = (streams: StreamRef[] = []): Member => ({
  publicId: 'm1', keycloakUserId: 'kc-1', fullName: 'Omar H', email: 'o@acmp.gov',
  role: 'Member', status: 'Active', isActive: true, isVotingEligible: true, streams,
});

describe('StreamAssignmentPanel (ADR-0042 step 3)', () => {
  let mutateAsync: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mutateAsync = vi.fn().mockResolvedValue(undefined);
    mockStreams.mockReturnValue({ data: [CORE, GOV, WILDCARD], isLoading: false, isError: false });
    mockAssign.mockReturnValue({ mutateAsync, isPending: false, isError: false, isSuccess: false });
    return i18n.changeLanguage('en');
  });

  // ⚠ THE WILDCARD IS OFFERED HERE and hidden from the topic picker. "All streams" says a PERSON is
  // unrestricted; a topic claiming it would assert universal scope (ADR-0042 clause 4).
  it('offers the wildcard, which the topic picker does not', () => {
    render(<StreamAssignmentPanel member={member()} />);

    expect(screen.getByRole('button', { name: 'All streams' })).toBeInTheDocument();
  });

  it('pre-selects the streams the member already holds', () => {
    render(<StreamAssignmentPanel member={member([GOV])} />);

    expect(screen.getByRole('button', { name: 'Government' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Core' })).toHaveAttribute('aria-pressed', 'false');
  });

  // ⚠ ADR-0042 clause (2): the roster must show a LOUD no-stream state. Once step 7 wires the
  // requirement this is not a status — the member can write nothing at all.
  it('warns, as an alert, when no stream is selected', () => {
    render(<StreamAssignmentPanel member={member()} />);

    expect(screen.getByRole('alert')).toHaveTextContent(/No stream assigned/i);
  });

  // Driven by the LIVE selection, not the saved member, so the consequence is visible BEFORE saving.
  it('raises the warning when the administrator clears the last stream', async () => {
    const user = userEvent.setup();
    render(<StreamAssignmentPanel member={member([CORE])} />);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Core' }));

    expect(screen.getByRole('alert')).toHaveTextContent(/No stream assigned/i);
  });

  // The endpoint REPLACES the set, so what is sent must be the whole selection, by PublicId.
  it('sends the whole selection as stream PublicIds', async () => {
    const user = userEvent.setup();
    render(<StreamAssignmentPanel member={member([CORE])} />);

    await user.click(screen.getByRole('button', { name: 'Government' }));
    await user.click(screen.getByRole('button', { name: /Save streams/i }));

    expect(mutateAsync).toHaveBeenCalledWith({ publicId: 'm1', streamPublicIds: ['s1', 's2'] });
  });

  it('can assign the wildcard', async () => {
    const user = userEvent.setup();
    render(<StreamAssignmentPanel member={member()} />);

    await user.click(screen.getByRole('button', { name: 'All streams' }));
    await user.click(screen.getByRole('button', { name: /Save streams/i }));

    expect(mutateAsync).toHaveBeenCalledWith({ publicId: 'm1', streamPublicIds: ['sw'] });
  });

  // Clearing every stream must be SENDABLE. It is a legitimate administrative act, and refusing it
  // in the client would make the loud warning meaningless — you could never reach the state it warns
  // about, and an administrator correcting a wrong assignment would be stuck.
  it('allows saving an empty selection', async () => {
    const user = userEvent.setup();
    render(<StreamAssignmentPanel member={member([CORE])} />);

    await user.click(screen.getByRole('button', { name: 'Core' }));
    await user.click(screen.getByRole('button', { name: /Save streams/i }));

    expect(mutateAsync).toHaveBeenCalledWith({ publicId: 'm1', streamPublicIds: [] });
  });

  it('disables save until something actually changes', async () => {
    const user = userEvent.setup();
    render(<StreamAssignmentPanel member={member([CORE])} />);
    const save = screen.getByRole('button', { name: /Save streams/i });
    expect(save).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Government' }));

    expect(save).toBeEnabled();
  });

  it('surfaces a refusal instead of hiding the control', async () => {
    const user = userEvent.setup();
    mutateAsync.mockRejectedValue(new Error('nope'));
    render(<StreamAssignmentPanel member={member([CORE])} />);

    await user.click(screen.getByRole('button', { name: 'Government' }));
    await user.click(screen.getByRole('button', { name: /Save streams/i }));

    expect(await screen.findByText(/Something went wrong/i)).toBeInTheDocument();
  });

  // ⚠ A save that succeeded must SAY it succeeded. Without this the only feedback is the button
  // going disabled again, which is indistinguishable from a change that never applied.
  it('confirms a completed save, naming the member', () => {
    mockAssign.mockReturnValue({ mutateAsync, isPending: false, isError: false, isSuccess: true });
    render(<StreamAssignmentPanel member={member([CORE])} />);

    expect(screen.getByRole('status')).toHaveTextContent(/Streams updated for Omar H/i);
  });

  // ...and must NOT claim success while the administrator has unsaved edits on screen.
  it('withdraws the confirmation once the selection is edited again', async () => {
    const user = userEvent.setup();
    mockAssign.mockReturnValue({ mutateAsync, isPending: false, isError: false, isSuccess: true });
    render(<StreamAssignmentPanel member={member([CORE])} />);
    expect(screen.getByRole('status')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Government' }));

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  // Loading and failure must SAY so rather than render an empty chip row: an administrator would
  // otherwise read "this committee has no streams" and conclude there is nothing to assign.
  it('says it is loading rather than showing an empty chip row', () => {
    mockStreams.mockReturnValue({ data: undefined, isLoading: true, isError: false });
    render(<StreamAssignmentPanel member={member()} />);

    expect(screen.getByText('Loading…')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Core' })).not.toBeInTheDocument();
  });

  it('announces a taxonomy load failure and offers no chips to click', () => {
    mockStreams.mockReturnValue({ data: undefined, isLoading: false, isError: true });
    render(<StreamAssignmentPanel member={member()} />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Save streams/i })).not.toBeInTheDocument();
  });

  it('labels chips in Arabic when the locale is Arabic', async () => {
    await i18n.changeLanguage('ar');
    render(<StreamAssignmentPanel member={member()} />);

    expect(screen.getByRole('button', { name: 'كل المسارات' })).toBeInTheDocument();
  });
});
