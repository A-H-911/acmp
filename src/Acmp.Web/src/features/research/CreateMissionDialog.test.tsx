import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CreateMissionDialog } from './CreateMissionDialog';
import { ApiError } from '../../api/apiClient';

const nav = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => nav,
}));

const create = vi.hoisted(() => vi.fn().mockResolvedValue({ key: 'RSCH-2026-006' }));
vi.mock('../../api/research', async (orig) => ({
  ...(await orig<typeof import('../../api/research')>()),
  useCreateMission: () => ({ mutateAsync: create, isPending: false }),
}));

// Stub the react-query-backed TemplatePicker to a plain apply button (its own behaviour is covered in
// features/templates/TemplatePicker.test.tsx); keeps this suite query-free.
vi.mock('../templates/TemplatePicker', () => ({
  TemplatePicker: ({ onApply, hasContent }: { onApply: (b: string) => void; hasContent?: boolean }) => (
    <button type="button" disabled={hasContent} onClick={() => onApply('TEMPLATE BODY')}>apply-template</button>
  ),
}));

vi.mock('../../api/topics', () => ({
  useBacklog: () => ({ data: { items: [{ id: 'g-topic', key: 'TOP-2026-014', title: 'Adopt Keycloak', type: 'Standard', status: 'Accepted', urgency: 'Normal', scope: 'x', streams: [], ownerId: null, ownerName: null, priority: 1, timesDeferred: 0, ageDays: 3, slaBreached: false, createdAt: '2026-02-01T00:00:00Z' }], total: 1, page: 1, pageSize: 200, totalPages: 1 } }),
}));

function setup() {
  return render(
    <MemoryRouter>
      <CreateMissionDialog open onClose={vi.fn()} />
    </MemoryRouter>,
  );
}

describe('CreateMissionDialog (P15b)', () => {
  beforeEach(() => vi.clearAllMocks());

  it('has no owner picker — owner is derived from the creator server-side', () => {
    setup();
    expect(screen.queryByRole('button', { name: 'Owner' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Owner/)).not.toBeInTheDocument();
  });

  it('validates title and question before submitting', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Create mission' }));
    expect(screen.getByText('A title is required.')).toBeInTheDocument();
    expect(screen.getByText('A research question is required.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('offers the optional topic picker', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Linked topic' }));
    expect(screen.getByRole('option', { name: /TOP-2026-014/ })).toBeInTheDocument();
  });

  it('creates the mission mirrored (title + question), with topic + keystone, then navigates to it', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/^Title/), 'Evaluate a unified IdP');
    await user.type(screen.getByLabelText(/Research question/), 'Does one IdP cut maintenance?');
    await user.type(screen.getByLabelText(/Keystone package reference/), 'KS-2026-014');
    await user.click(screen.getByRole('button', { name: 'Linked topic' }));
    await user.click(screen.getByRole('option', { name: /TOP-2026-014/ }));
    await user.click(screen.getByRole('button', { name: 'Create mission' }));

    expect(create).toHaveBeenCalledTimes(1);
    expect(create.mock.calls[0][0]).toEqual({
      title: { en: 'Evaluate a unified IdP', ar: 'Evaluate a unified IdP' },
      question: { en: 'Does one IdP cut maintenance?', ar: 'Does one IdP cut maintenance?' },
      keystonePackageRef: 'KS-2026-014',
      sourceTopicId: 'g-topic',
    });
    expect(nav).toHaveBeenCalledWith('/research/RSCH-2026-006');
  });

  it('nulls the empty optional fields (no topic, no keystone)', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/^Title/), 'Observability baseline');
    await user.type(screen.getByLabelText(/Research question/), 'Where are the gaps?');
    await user.click(screen.getByRole('button', { name: 'Create mission' }));
    expect(create.mock.calls[0][0]).toMatchObject({ keystonePackageRef: null, sourceTopicId: null });
  });

  it('pre-fills the research question from a chosen template (FR-120)', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'apply-template' }));
    await user.type(screen.getByLabelText(/^Title/), 'Templated mission');
    await user.click(screen.getByRole('button', { name: 'Create mission' }));
    expect(create.mock.calls[0][0]).toMatchObject({ question: { en: 'TEMPLATE BODY', ar: 'TEMPLATE BODY' } });
  });

  it('surfaces a server error and does not navigate', async () => {
    create.mockRejectedValueOnce(new ApiError(400, { title: 'Bad request' }));
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByLabelText(/^Title/), 'X');
    await user.type(screen.getByLabelText(/Research question/), 'Y');
    await user.click(screen.getByRole('button', { name: 'Create mission' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Bad request');
    expect(nav).not.toHaveBeenCalled();
  });
});
