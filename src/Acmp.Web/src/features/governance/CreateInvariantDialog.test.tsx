import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CreateInvariantDialog } from './CreateInvariantDialog';
import { ApiError } from '../../api/apiClient';

const nav = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => nav,
}));

const create = vi.hoisted(() => vi.fn().mockResolvedValue({ key: 'AIV-2026-012' }));
vi.mock('../../api/invariants', async (orig) => ({
  ...(await orig<typeof import('../../api/invariants')>()),
  useCreateInvariant: () => ({ mutateAsync: create, isPending: false }),
}));

vi.mock('../../api/members', () => ({
  useMembers: () => ({
    data: [
      { publicId: 'p1', keycloakUserId: 'kc-khalid', fullName: 'Khalid A', email: 'k@a.gov', role: 'Secretary', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
      { publicId: 'p2', keycloakUserId: 'kc-old', fullName: 'Inactive Ivy', email: 'i@a.gov', role: 'Member', status: 'Inactive', isActive: false, isVotingEligible: false, streams: [] },
    ],
  }),
}));

function setup() {
  return render(
    <MemoryRouter>
      <CreateInvariantDialog open onClose={vi.fn()} />
    </MemoryRouter>,
  );
}
async function pickOwner(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'Owner' }));
  await user.click(screen.getByRole('option', { name: /Khalid A/ }));
}

describe('CreateInvariantDialog (P11d)', () => {
  beforeEach(() => vi.clearAllMocks());

  it('offers only active members as owners', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Owner' }));
    expect(screen.getByRole('option', { name: /Khalid A/ })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Inactive Ivy/ })).not.toBeInTheDocument();
  });

  it('validates statement, rationale and owner before submitting', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Save invariant' }));
    expect(screen.getByText('A statement is required.')).toBeInTheDocument();
    expect(screen.getByText('A rationale is required.')).toBeInTheDocument();
    expect(screen.getByText('An owner is required.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('creates the invariant (born Draft) with defaults, mirrored, then navigates to it', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Statement/), 'Every service authenticates via the standard IdP');
    await user.type(screen.getByLabelText(/Rationale/), 'Centralized identity is auditable');
    await pickOwner(user);
    await user.click(screen.getByRole('button', { name: 'Save invariant' }));

    expect(create).toHaveBeenCalledTimes(1);
    expect(create.mock.calls[0][0]).toMatchObject({
      category: 'Security',
      scope: 'Platform',
      statement: { en: 'Every service authenticates via the standard IdP', ar: 'Every service authenticates via the standard IdP' },
      rationale: { en: 'Centralized identity is auditable', ar: 'Centralized identity is auditable' },
      exceptionsPolicy: null,
      ownerUserId: 'kc-khalid',
      ownerName: 'Khalid A',
    });
    expect(nav).toHaveBeenCalledWith('/invariants/AIV-2026-012');
  });

  it('sends the chosen category and scope', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Statement/), 'No shared mutable schema');
    await user.type(screen.getByLabelText(/Rationale/), 'Ownership boundaries');
    await user.click(screen.getByRole('button', { name: 'Category' }));
    await user.click(screen.getByRole('option', { name: 'Data' }));
    await user.click(screen.getByRole('button', { name: 'Scope' }));
    await user.click(screen.getByRole('option', { name: 'Organization-wide' }));
    await pickOwner(user);
    await user.click(screen.getByRole('button', { name: 'Save invariant' }));

    const payload = create.mock.calls[0][0];
    expect(payload.category).toBe('Data');
    expect(payload.scope).toBe('OrgWide');
  });

  it('surfaces a server error and does not navigate', async () => {
    create.mockRejectedValueOnce(new ApiError(400, { title: 'Bad request' }));
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/Statement/), 'X');
    await user.type(screen.getByLabelText(/Rationale/), 'Y');
    await pickOwner(user);
    await user.click(screen.getByRole('button', { name: 'Save invariant' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Bad request');
    expect(nav).not.toHaveBeenCalled();
  });
});
