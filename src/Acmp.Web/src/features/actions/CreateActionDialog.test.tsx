import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CreateActionDialog } from './CreateActionDialog';
import { ApiError } from '../../api/apiClient';

const nav = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => nav,
}));

const create = vi.hoisted(() => vi.fn().mockResolvedValue({ key: 'ACT-2026-050' }));
vi.mock('../../api/actions', async (orig) => ({
  ...(await orig<typeof import('../../api/actions')>()),
  useCreateAction: () => ({ mutateAsync: create, isPending: false }),
}));

vi.mock('../../api/members', () => ({
  useMembers: () => ({
    data: [
      { publicId: 'p1', keycloakUserId: 'kc-omar', fullName: 'Omar H', email: 'o@a.gov', role: 'Member', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
      { publicId: 'p2', keycloakUserId: 'kc-old', fullName: 'Inactive Ivy', email: 'i@a.gov', role: 'Member', status: 'Inactive', isActive: false, isVotingEligible: false, streams: [] },
    ],
  }),
}));

const source = { sourceType: 'Decision' as const, sourceId: 'dec-guid-1', sourceKey: 'DECN-2026-008' };
function setup() {
  return render(
    <MemoryRouter>
      <CreateActionDialog open onClose={vi.fn()} source={source} />
    </MemoryRouter>,
  );
}
async function fillOwnerAndDate(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'Owner' }));
  await user.click(screen.getByRole('option', { name: /Omar H/ }));
  await user.click(screen.getByRole('button', { name: 'Due date' }));
  await user.click(screen.getByRole('gridcell', { name: '15' }));
}

describe('CreateActionDialog', () => {
  beforeEach(() => vi.clearAllMocks());

  it('shows the locked source and only active members as owners', async () => {
    const user = userEvent.setup();
    setup();
    expect(screen.getByText('DECN-2026-008')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Owner' }));
    expect(screen.getByRole('option', { name: /Omar H/ })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Inactive Ivy/ })).not.toBeInTheDocument();
  });

  it('validates title, owner and due date before submitting', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Create action' }));
    expect(screen.getByText('A title is required.')).toBeInTheDocument();
    expect(screen.getByText('An owner is required.')).toBeInTheDocument();
    expect(screen.getByText('A due date is required.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('creates the action from the decision source, mirrored, then navigates to it', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Title/), 'Produce migration ADR');
    await user.type(screen.getByLabelText(/Description/), 'Cover rollback');
    await fillOwnerAndDate(user);
    await user.click(screen.getByRole('button', { name: 'High' }));
    await user.click(screen.getByRole('button', { name: 'Create action' }));

    expect(create).toHaveBeenCalledTimes(1);
    const payload = create.mock.calls[0][0];
    expect(payload).toMatchObject({
      title: { en: 'Produce migration ADR', ar: 'Produce migration ADR' },
      description: { en: 'Cover rollback', ar: 'Cover rollback' },
      priority: 'High',
      ownerUserId: 'kc-omar',
      ownerName: 'Omar H',
      sourceType: 'Decision',
      sourceId: 'dec-guid-1',
      sourceKey: 'DECN-2026-008',
      meetingKey: null,
    });
    expect(payload.dueDate).toMatch(/^\d{4}-\d{2}-15T00:00:00Z$/);
    expect(nav).toHaveBeenCalledWith('/actions/ACT-2026-050');
  });

  it('defaults priority to Normal and omits an empty description', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Title/), 'Quick follow-up');
    await fillOwnerAndDate(user);
    await user.click(screen.getByRole('button', { name: 'Create action' }));
    const payload = create.mock.calls[0][0];
    expect(payload.priority).toBe('Normal');
    expect(payload.description).toBeNull();
  });

  it('surfaces a server error and does not navigate', async () => {
    create.mockRejectedValueOnce(new ApiError(400, { title: 'Bad request' }));
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Title/), 'X');
    await fillOwnerAndDate(user);
    await user.click(screen.getByRole('button', { name: 'Create action' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Bad request');
    expect(nav).not.toHaveBeenCalled();
  });
});
