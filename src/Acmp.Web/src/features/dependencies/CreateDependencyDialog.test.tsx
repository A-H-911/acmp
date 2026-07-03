import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createElement } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CreateDependencyDialog } from './CreateDependencyDialog';
import { ApiError } from '../../api/apiClient';

const nav = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => nav,
}));

const create = vi.hoisted(() => vi.fn().mockResolvedValue({ key: 'DPN-2026-009' }));
vi.mock('../../api/dependencies', async (orig) => ({
  ...(await orig<typeof import('../../api/dependencies')>()),
  useCreateDependency: () => ({ mutateAsync: create, isPending: false }),
}));

// A controllable ArtifactPicker stub — each render is a button that emits `picks.next` on click.
const picks = vi.hoisted(() => ({ next: { type: 'Action', id: 'to-1', key: 'ACT-9', title: 'Do it' } as { type: string; id: string; key: string; title: string } }));
vi.mock('../traceability/ArtifactPicker', () => ({
  ArtifactPicker: ({ label, onChange, error }: { label: string; onChange: (v: unknown) => void; error?: string }) =>
    createElement(
      'div',
      null,
      createElement('button', { type: 'button', onClick: () => onChange(picks.next) }, `pick:${label}`),
      error ? createElement('span', null, error) : null,
    ),
}));

const FROM = { type: 'Topic' as const, id: 'from-1', key: 'TOP-2026-014', title: 'Gateway migration' };
function setup(from?: typeof FROM) {
  return render(
    <MemoryRouter>
      <CreateDependencyDialog open onClose={vi.fn()} from={from} />
    </MemoryRouter>,
  );
}

describe('CreateDependencyDialog (P10e)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    picks.next = { type: 'Action', id: 'to-1', key: 'ACT-9', title: 'Do it' };
  });

  it('contextual create: locks the From end, submits the edge body, and navigates to the new key', async () => {
    const user = userEvent.setup();
    setup(FROM);
    expect(screen.getByText('TOP-2026-014')).toBeInTheDocument(); // locked From
    await user.click(screen.getByText('pick:To entity'));
    await user.click(screen.getByRole('button', { name: 'Create dependency' }));
    expect(create).toHaveBeenCalledWith({
      fromType: 'Topic', fromId: 'from-1', fromKey: 'TOP-2026-014', fromTitle: 'Gateway migration',
      toType: 'Action', toId: 'to-1', toKey: 'ACT-9', toTitle: 'Do it',
      kind: 'DependsOn', note: null,
    });
    expect(nav).toHaveBeenCalledWith('/dependencies/DPN-2026-009');
  });

  it('requires a target before submitting', async () => {
    const user = userEvent.setup();
    setup(FROM);
    await user.click(screen.getByRole('button', { name: 'Create dependency' }));
    expect(screen.getByText('A target artifact is required.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('rejects a self-loop (From === To)', async () => {
    picks.next = { type: 'Topic', id: 'from-1', key: 'TOP-2026-014', title: 'Gateway migration' };
    const user = userEvent.setup();
    setup(FROM);
    await user.click(screen.getByText('pick:To entity'));
    await user.click(screen.getByRole('button', { name: 'Create dependency' }));
    expect(screen.getByText('An artifact cannot depend on itself.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('register mode: renders a From picker when no contextual artifact is supplied', () => {
    setup();
    expect(screen.getByText('pick:From entity')).toBeInTheDocument();
    expect(screen.getByText('pick:To entity')).toBeInTheDocument();
  });

  it('surfaces a submit error and does not navigate', async () => {
    create.mockRejectedValueOnce(new ApiError(409, { title: 'Conflict' }));
    const user = userEvent.setup();
    setup(FROM);
    await user.click(screen.getByText('pick:To entity'));
    await user.click(screen.getByRole('button', { name: 'Create dependency' }));
    expect(await screen.findByText('Conflict')).toBeInTheDocument();
    expect(nav).not.toHaveBeenCalled();
  });
});
