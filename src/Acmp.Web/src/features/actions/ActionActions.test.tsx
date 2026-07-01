import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ActionActions } from './ActionActions';
import { ApiError } from '../../api/apiClient';
import type { ActionDetail, ActionStatus } from '../../api/actions';

/* The 7 lifecycle mutation hooks are stubbed to spies so we assert the payload each button sends.
   useAuth is stubbed so we drive role + owner gating directly. */
const m = vi.hoisted(() => ({
  start: vi.fn().mockResolvedValue(undefined),
  unblock: vi.fn().mockResolvedValue(undefined),
  verify: vi.fn().mockResolvedValue(undefined),
  block: vi.fn().mockResolvedValue(undefined),
  cancel: vi.fn().mockResolvedValue(undefined),
  progress: vi.fn().mockResolvedValue(undefined),
  complete: vi.fn().mockResolvedValue(undefined),
}));
vi.mock('../../api/actions', async (orig) => ({
  ...(await orig<typeof import('../../api/actions')>()),
  useStartAction: () => ({ mutateAsync: m.start, isPending: false }),
  useUnblockAction: () => ({ mutateAsync: m.unblock, isPending: false }),
  useVerifyAction: () => ({ mutateAsync: m.verify, isPending: false }),
  useBlockAction: () => ({ mutateAsync: m.block, isPending: false }),
  useCancelAction: () => ({ mutateAsync: m.cancel, isPending: false }),
  useUpdateActionProgress: () => ({ mutateAsync: m.progress, isPending: false }),
  useCompleteAction: () => ({ mutateAsync: m.complete, isPending: false }),
}));

const auth = vi.hoisted(() => ({ current: { roles: ['secretary'] as string[], userId: 'kc-sara' as string | undefined } }));
vi.mock('../../auth/AcmpAuthContext', async (orig) => ({
  ...(await orig<typeof import('../../auth/AcmpAuthContext')>()),
  useAuth: () => auth.current,
}));

const base: ActionDetail = {
  id: 'a1', key: 'ACT-2026-025', title: { en: 'Publish guide', ar: 'نشر الدليل' },
  status: 'Completed', priority: 'High', ownerUserId: 'kc-omar', ownerName: 'Omar H',
  dueDate: null, isOverdue: false, progressPct: 100,
  sourceType: 'Decision', sourceId: 'g1', sourceKey: 'DECN-2026-008', meetingKey: null,
  description: null, blockedReason: null, completionNote: null, cancelReason: null,
  completedByUserId: 'kc-omar', completedAt: '2026-06-01T00:00:00Z',
  verifiedByUserId: null, verifiedByName: null, verifiedAt: null, createdAt: '2026-02-14T00:00:00Z',
};
const act = (over: Partial<ActionDetail> = {}): ActionDetail => ({ ...base, ...over });

function setAuth(roles: string[], userId?: string) {
  auth.current = { roles, userId };
}
const inDialog = () => within(screen.getByRole('dialog'));

describe('ActionActions gating', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setAuth(['secretary'], 'kc-sara');
  });

  it('shows Verify + Cancel to a non-owner privileged user on a Completed action', () => {
    render(<ActionActions action={act()} />);
    expect(screen.getByRole('button', { name: 'Verify' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel action' })).toBeInTheDocument();
  });

  it('hides Verify from the action OWNER (SoD-1) but still allows managing it', () => {
    setAuth(['member'], 'kc-omar'); // owner = completer
    render(<ActionActions action={act()} />);
    expect(screen.queryByRole('button', { name: 'Verify' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel action' })).toBeInTheDocument();
  });

  it('hides Verify from the COMPLETER even when they are not the owner', () => {
    setAuth(['member'], 'kc-lina');
    render(<ActionActions action={act({ ownerUserId: 'kc-noura', completedByUserId: 'kc-lina' })} />);
    expect(screen.queryByRole('button', { name: 'Verify' })).not.toBeInTheDocument();
  });

  it('renders nothing for a role with no manage/verify rights', () => {
    setAuth(['reviewer'], 'kc-rev');
    const { container } = render(<ActionActions action={act()} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing for terminal states (Verified/Cancelled)', () => {
    const { container } = render(<ActionActions action={act({ status: 'Verified' })} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('offers exactly the InProgress transitions (Block/Update progress/Complete/Cancel, no Verify)', () => {
    render(<ActionActions action={act({ status: 'InProgress' as ActionStatus })} />);
    expect(screen.getByRole('button', { name: 'Block' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Update progress' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Complete' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel action' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Verify' })).not.toBeInTheDocument();
  });

  it('lets a Member manage only an action they OWN', () => {
    setAuth(['member'], 'kc-omar');
    const { rerender } = render(<ActionActions action={act({ status: 'Open', ownerUserId: 'kc-omar' })} />);
    expect(screen.getByRole('button', { name: 'Start' })).toBeInTheDocument();
    setAuth(['member'], 'kc-omar');
    rerender(<ActionActions action={act({ status: 'Open', ownerUserId: 'kc-someone-else' })} />);
    expect(screen.queryByRole('button', { name: 'Start' })).not.toBeInTheDocument();
  });
});

describe('ActionActions dialogs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setAuth(['secretary'], 'kc-sara');
  });

  it('verifies via a confirm dialog (no body)', async () => {
    const user = userEvent.setup();
    render(<ActionActions action={act()} />);
    await user.click(screen.getByRole('button', { name: 'Verify' }));
    await user.click(inDialog().getByRole('button', { name: 'Verify' }));
    expect(m.verify).toHaveBeenCalledWith({ id: 'a1' });
  });

  it('starts via a confirm dialog (Open action)', async () => {
    const user = userEvent.setup();
    render(<ActionActions action={act({ status: 'Open' })} />);
    await user.click(screen.getByRole('button', { name: 'Start' }));
    await user.click(inDialog().getByRole('button', { name: 'Start' }));
    expect(m.start).toHaveBeenCalledWith({ id: 'a1' });
  });

  it('unblocks via a confirm dialog (Blocked action)', async () => {
    const user = userEvent.setup();
    render(<ActionActions action={act({ status: 'Blocked' })} />);
    await user.click(screen.getByRole('button', { name: 'Unblock' }));
    await user.click(inDialog().getByRole('button', { name: 'Unblock' }));
    expect(m.unblock).toHaveBeenCalledWith({ id: 'a1' });
  });

  it('requires a reason to cancel, then sends it mirrored to both languages', async () => {
    const user = userEvent.setup();
    render(<ActionActions action={act()} />);
    await user.click(screen.getByRole('button', { name: 'Cancel action' }));
    // Confirm with an empty reason → validation error, no call.
    await user.click(inDialog().getByRole('button', { name: 'Cancel action' }));
    expect(screen.getByRole('alert')).toHaveTextContent('A reason is required.');
    expect(m.cancel).not.toHaveBeenCalled();
    await user.type(inDialog().getByRole('textbox'), 'No longer needed');
    await user.click(inDialog().getByRole('button', { name: 'Cancel action' }));
    expect(m.cancel).toHaveBeenCalledWith({ id: 'a1', reason: { en: 'No longer needed', ar: 'No longer needed' } });
  });

  it('blocks with a required reason (InProgress action)', async () => {
    const user = userEvent.setup();
    render(<ActionActions action={act({ status: 'InProgress' })} />);
    await user.click(screen.getByRole('button', { name: 'Block' }));
    await user.type(inDialog().getByRole('textbox'), 'Waiting on infra');
    await user.click(inDialog().getByRole('button', { name: 'Block' }));
    expect(m.block).toHaveBeenCalledWith({ id: 'a1', reason: { en: 'Waiting on infra', ar: 'Waiting on infra' } });
  });

  it('completes with an optional note (null when left blank)', async () => {
    const user = userEvent.setup();
    render(<ActionActions action={act({ status: 'InProgress' })} />);
    await user.click(screen.getByRole('button', { name: 'Complete' }));
    await user.click(inDialog().getByRole('button', { name: 'Complete' }));
    expect(m.complete).toHaveBeenCalledWith({ id: 'a1', completionNote: null });
  });

  it('updates progress with a validated percent', async () => {
    const user = userEvent.setup();
    render(<ActionActions action={act({ status: 'InProgress', progressPct: 40 })} />);
    await user.click(screen.getByRole('button', { name: 'Update progress' }));
    const input = inDialog().getByRole('spinbutton');
    await user.clear(input);
    await user.type(input, '150'); // out of range → error, no call
    await user.click(inDialog().getByRole('button', { name: 'Update progress' }));
    expect(screen.getByRole('alert')).toHaveTextContent('0 to 100');
    expect(m.progress).not.toHaveBeenCalled();
    await user.clear(input);
    await user.type(input, '60');
    await user.click(inDialog().getByRole('button', { name: 'Update progress' }));
    expect(m.progress).toHaveBeenCalledWith({ id: 'a1', progressPct: 60 });
  });

  it('surfaces a server error without closing the dialog', async () => {
    const user = userEvent.setup();
    m.verify.mockRejectedValueOnce(new ApiError(403, { title: 'Forbidden' }));
    render(<ActionActions action={act()} />);
    await user.click(screen.getByRole('button', { name: 'Verify' }));
    await user.click(inDialog().getByRole('button', { name: 'Verify' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Forbidden');
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});
