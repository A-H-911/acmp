import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CreateAdrDialog } from './CreateAdrDialog';
import { ApiError } from '../../api/apiClient';

const nav = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => nav,
}));

const create = vi.hoisted(() => vi.fn().mockResolvedValue({ id: 'g-1', key: 'ADR-2026-012' }));
const propose = vi.hoisted(() => vi.fn().mockResolvedValue(undefined));
vi.mock('../../api/adrs', async (orig) => ({
  ...(await orig<typeof import('../../api/adrs')>()),
  useCreateAdr: () => ({ mutateAsync: create, isPending: false }),
  useProposeAdr: () => ({ mutateAsync: propose, isPending: false }),
}));

function setup() {
  return render(
    <MemoryRouter>
      <CreateAdrDialog open onClose={vi.fn()} />
    </MemoryRouter>,
  );
}
async function fillRequired(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/Title/), 'Keycloak as the standard IdP');
  await user.type(screen.getByLabelText(/Context/), 'Fragmented auth across streams.');
  await user.type(screen.getByLabelText(/Decision/), 'Adopt Keycloak, realm per stream.');
}
async function pickStatus(user: ReturnType<typeof userEvent.setup>, label: string) {
  await user.click(screen.getByRole('button', { name: 'Status' }));
  await user.click(screen.getByRole('option', { name: label }));
}

describe('CreateAdrDialog (P11b)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    create.mockResolvedValue({ id: 'g-1', key: 'ADR-2026-012' });
    propose.mockResolvedValue(undefined);
  });

  it('validates title, context and decision before submitting', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Save ADR' }));
    expect(screen.getByText('A title is required.')).toBeInTheDocument();
    expect(screen.getByText('A context is required.')).toBeInTheDocument();
    expect(screen.getByText('A decision is required.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('creates a mirrored Draft, then chains propose (default status) and navigates', async () => {
    const user = userEvent.setup();
    setup();
    await fillRequired(user);
    await user.type(screen.getByLabelText(/Positive consequences/), 'Unified SSO.');
    await user.click(screen.getByRole('button', { name: 'Save ADR' }));

    expect(create).toHaveBeenCalledTimes(1);
    expect(create.mock.calls[0][0]).toMatchObject({
      title: { en: 'Keycloak as the standard IdP', ar: 'Keycloak as the standard IdP' },
      context: { en: 'Fragmented auth across streams.', ar: 'Fragmented auth across streams.' },
      decisionText: { en: 'Adopt Keycloak, realm per stream.', ar: 'Adopt Keycloak, realm per stream.' },
      decisionDrivers: null,
      consequencesPositive: { en: 'Unified SSO.', ar: 'Unified SSO.' },
      consequencesNegative: null,
      options: null,
    });
    expect(propose).toHaveBeenCalledWith('g-1');
    expect(nav).toHaveBeenCalledWith('/adrs/ADR-2026-012');
  });

  it('creates a Draft without proposing when the author picks Draft', async () => {
    const user = userEvent.setup();
    setup();
    await fillRequired(user);
    await pickStatus(user, 'Draft');
    await user.click(screen.getByRole('button', { name: 'Save ADR' }));
    expect(create).toHaveBeenCalledTimes(1);
    expect(propose).not.toHaveBeenCalled();
    expect(nav).toHaveBeenCalledWith('/adrs/ADR-2026-012');
  });

  it('surfaces a server error on create and does not navigate', async () => {
    create.mockRejectedValueOnce(new ApiError(400, { title: 'Bad request' }));
    const user = userEvent.setup();
    setup();
    await fillRequired(user);
    await user.click(screen.getByRole('button', { name: 'Save ADR' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Bad request');
    expect(nav).not.toHaveBeenCalled();
  });

  it('does not re-create the ADR when retrying after a failed propose (no duplicate)', async () => {
    propose.mockRejectedValueOnce(new ApiError(409, { title: 'Conflict' }));
    const user = userEvent.setup();
    setup();
    await fillRequired(user);
    // First attempt: create succeeds, propose fails → error shown, no navigate.
    await user.click(screen.getByRole('button', { name: 'Save ADR' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Conflict');
    expect(create).toHaveBeenCalledTimes(1);
    expect(nav).not.toHaveBeenCalled();
    // Retry: propose now succeeds — create is NOT called again (guarded by createdRef).
    await user.click(screen.getByRole('button', { name: 'Save ADR' }));
    expect(create).toHaveBeenCalledTimes(1);
    expect(propose).toHaveBeenCalledTimes(2);
    expect(nav).toHaveBeenCalledWith('/adrs/ADR-2026-012');
  });
});
