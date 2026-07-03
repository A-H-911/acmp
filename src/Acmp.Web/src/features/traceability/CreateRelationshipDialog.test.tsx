import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createElement } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CreateRelationshipDialog } from './CreateRelationshipDialog';
import { ApiError } from '../../api/apiClient';

const create = vi.hoisted(() => vi.fn().mockResolvedValue({ id: 'edge-1' }));
vi.mock('../../api/traceability', async (orig) => ({
  ...(await orig<typeof import('../../api/traceability')>()),
  useCreateRelationship: () => ({ mutateAsync: create, isPending: false }),
}));

const picks = vi.hoisted(() => ({ next: { type: 'Action', id: 'to-1', key: 'ACT-9', title: 'Do it' } as { type: string; id: string; key: string; title: string } }));
vi.mock('./ArtifactPicker', () => ({
  ArtifactPicker: ({ label, onChange, error }: { label: string; onChange: (v: unknown) => void; error?: string }) =>
    createElement(
      'div',
      null,
      createElement('button', { type: 'button', onClick: () => onChange(picks.next) }, `pick:${label}`),
      error ? createElement('span', null, error) : null,
    ),
}));

const SOURCE = { type: 'Topic' as const, id: 'src-1', key: 'TOP-2026-040', title: 'Adopt Keycloak' };
const onClose = vi.fn();
function setup() {
  return render(
    <MemoryRouter>
      <CreateRelationshipDialog open onClose={onClose} source={SOURCE} />
    </MemoryRouter>,
  );
}

describe('CreateRelationshipDialog (P10e, AC-063)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    picks.next = { type: 'Action', id: 'to-1', key: 'ACT-9', title: 'Do it' };
  });

  it('locks the source, submits the typed edge (default References), and closes on success', async () => {
    const user = userEvent.setup();
    setup();
    expect(screen.getByText('TOP-2026-040')).toBeInTheDocument(); // locked source
    await user.click(screen.getByText('pick:Target'));
    await user.click(screen.getByRole('button', { name: 'Create relationship' }));
    expect(create).toHaveBeenCalledWith({
      sourceType: 'Topic', sourceId: 'src-1', sourceKey: 'TOP-2026-040', sourceTitle: 'Adopt Keycloak',
      targetType: 'Action', targetId: 'to-1', targetKey: 'ACT-9', targetTitle: 'Do it',
      relType: 'References', notes: null,
    });
    expect(onClose).toHaveBeenCalled();
  });

  it('requires a target before submitting', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Create relationship' }));
    expect(screen.getByText('A target artifact is required.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('rejects a self-loop (target === source)', async () => {
    picks.next = { type: 'Topic', id: 'src-1', key: 'TOP-2026-040', title: 'Adopt Keycloak' };
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByText('pick:Target'));
    await user.click(screen.getByRole('button', { name: 'Create relationship' }));
    expect(screen.getByText('An artifact cannot relate to itself.')).toBeInTheDocument();
    expect(create).not.toHaveBeenCalled();
  });

  it('surfaces a submit error and stays open', async () => {
    create.mockRejectedValueOnce(new ApiError(403, { title: 'Forbidden' }));
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByText('pick:Target'));
    await user.click(screen.getByRole('button', { name: 'Create relationship' }));
    expect(await screen.findByText('Forbidden')).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });
});
