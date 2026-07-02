import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CreateRiskDialog } from './CreateRiskDialog';
import { ApiError } from '../../api/apiClient';

const nav = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => nav,
}));

const create = vi.hoisted(() => vi.fn().mockResolvedValue({ key: 'RSK-2026-012' }));
vi.mock('../../api/risks', async (orig) => ({
  ...(await orig<typeof import('../../api/risks')>()),
  useCreateRisk: () => ({ mutateAsync: create, isPending: false }),
}));

vi.mock('../../api/members', () => ({
  useMembers: () => ({
    data: [
      { publicId: 'p1', keycloakUserId: 'kc-omar', fullName: 'Omar H', email: 'o@a.gov', role: 'Member', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
      { publicId: 'p2', keycloakUserId: 'kc-old', fullName: 'Inactive Ivy', email: 'i@a.gov', role: 'Member', status: 'Inactive', isActive: false, isVotingEligible: false, streams: [] },
    ],
  }),
}));

vi.mock('../../api/topics', () => ({
  useBacklog: () => ({ data: { items: [{ id: 'g-topic', key: 'TOP-2026-014', title: 'Adopt Keycloak', type: 'Standard', status: 'Accepted', urgency: 'Normal', scope: 'x', streams: [], ownerId: null, ownerName: null, priority: 1, ageDays: 3, slaBreached: false, createdAt: '2026-02-01T00:00:00Z' }], total: 1, page: 1, pageSize: 200, totalPages: 1 } }),
}));

function setup() {
  return render(
    <MemoryRouter>
      <CreateRiskDialog open onClose={vi.fn()} />
    </MemoryRouter>,
  );
}
async function fillOwnerAndTopic(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'Owner' }));
  await user.click(screen.getByRole('option', { name: /Omar H/ }));
  await user.click(screen.getByRole('button', { name: 'Linked topic' }));
  await user.click(screen.getByRole('option', { name: /TOP-2026-014/ }));
}

describe('CreateRiskDialog (P10b)', () => {
  beforeEach(() => vi.clearAllMocks());

  it('shows the topic picker and only active members as owners', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Owner' }));
    expect(screen.getByRole('option', { name: /Omar H/ })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Inactive Ivy/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Linked topic' }));
    expect(screen.getByRole('option', { name: /TOP-2026-014/ })).toBeInTheDocument();
  });

  it('validates title, owner and linked topic before submitting', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Create risk' }));
    expect(screen.getByText('A title is required.')).toBeInTheDocument();
    expect(screen.getByText('An owner is required.')).toBeInTheDocument();
    expect(screen.getByText('A linked topic is required.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('creates the risk against the topic subject, mirrored, then navigates to it', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Title/), 'Dual-running auth migration');
    await user.type(screen.getByLabelText(/Mitigation plan/), 'Stage the cutover with rollback');
    await fillOwnerAndTopic(user);
    await user.click(screen.getByRole('button', { name: 'Create risk' }));

    expect(create).toHaveBeenCalledTimes(1);
    const payload = create.mock.calls[0][0];
    expect(payload).toMatchObject({
      title: { en: 'Dual-running auth migration', ar: 'Dual-running auth migration' },
      description: null,
      likelihood: 'Medium',
      impact: 'High',
      ownerUserId: 'kc-omar',
      ownerName: 'Omar H',
      subjectType: 'Topic',
      subjectId: 'g-topic',
      subjectKey: 'TOP-2026-014',
      initialMitigation: { en: 'Stage the cutover with rollback', ar: 'Stage the cutover with rollback' },
    });
    expect(nav).toHaveBeenCalledWith('/risks/RSK-2026-012');
  });

  it('defaults likelihood/impact and omits an empty mitigation plan', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Title/), 'Vendor lock-in');
    await fillOwnerAndTopic(user);
    await user.click(screen.getByRole('button', { name: 'Create risk' }));
    const payload = create.mock.calls[0][0];
    expect(payload.likelihood).toBe('Medium');
    expect(payload.impact).toBe('High');
    expect(payload.initialMitigation).toBeNull();
  });

  it('surfaces a server error and does not navigate', async () => {
    create.mockRejectedValueOnce(new ApiError(400, { title: 'Bad request' }));
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Title/), 'X');
    await fillOwnerAndTopic(user);
    await user.click(screen.getByRole('button', { name: 'Create risk' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Bad request');
    expect(nav).not.toHaveBeenCalled();
  });
});
