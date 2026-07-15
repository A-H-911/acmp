import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithAuth } from '../../test/render';
import { ArtifactPicker, type PickedArtifact } from './ArtifactPicker';

vi.mock('../../api/topics', () => ({ useBacklog: vi.fn() }));
vi.mock('../../api/actions', () => ({ useActionsRegister: vi.fn() }));
vi.mock('../../api/risks', () => ({ useRisksRegister: vi.fn() }));
vi.mock('../../api/wiki', async (orig) => ({
  ...(await orig<typeof import('../../api/wiki')>()),
  useWikiDocuments: vi.fn(),
}));
import { useBacklog } from '../../api/topics';
import { useActionsRegister } from '../../api/actions';
import { useRisksRegister } from '../../api/risks';
import { useWikiDocuments } from '../../api/wiki';

const mockTopics = useBacklog as unknown as Mock;
const mockActions = useActionsRegister as unknown as Mock;
const mockRisks = useRisksRegister as unknown as Mock;
const mockDocs = useWikiDocuments as unknown as Mock;

describe('ArtifactPicker (P10e)', () => {
  beforeEach(() => {
    mockTopics.mockReturnValue({ data: { items: [{ id: 't1', key: 'TOP-1', title: 'Gateway' }] } });
    mockActions.mockReturnValue({ data: { items: [{ id: 'a1', key: 'ACT-1', title: { en: 'Do it', ar: 'افعلها' } }] } });
    mockRisks.mockReturnValue({ data: { items: [{ id: 'r1', key: 'RSK-1', title: { en: 'Risk', ar: 'خطر' } }] } });
    mockDocs.mockReturnValue({ data: { items: [{ id: 'd1', key: 'DOC-1', title: { en: 'Governance page', ar: 'صفحة الحوكمة' } }] } });
  });

  it('picks a type then an artifact and emits the endpoint snapshot', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    renderWithAuth(<ArtifactPicker label="To entity" pickableTypes={['Topic', 'Action', 'Risk']} value={null} onChange={onChange} />, { roles: ['secretary'] });

    // Choose the type → selection clears.
    await user.click(screen.getByRole('button', { name: 'Type' }));
    await user.click(screen.getByRole('option', { name: 'Topic' }));
    expect(onChange).toHaveBeenLastCalledWith(null);

    // Choose the artifact → emits {type,id,key,title}.
    await user.click(screen.getByRole('button', { name: 'Artifact' }));
    await user.click(screen.getByRole('option', { name: /TOP-1/ }));
    expect(onChange).toHaveBeenLastCalledWith<[PickedArtifact]>({ type: 'Topic', id: 't1', key: 'TOP-1', title: 'Gateway' });
  });

  it('picks a Document (wiki page) target, resolving its localized title (ADR-0029)', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    renderWithAuth(<ArtifactPicker label="To" pickableTypes={['Document']} value={null} onChange={onChange} />, { roles: ['secretary'] });
    // Single pickable type auto-selects, so the artifact select is immediately enabled.
    await user.click(screen.getByRole('button', { name: 'Artifact' }));
    await user.click(screen.getByRole('option', { name: /DOC-1/ }));
    expect(onChange).toHaveBeenLastCalledWith({ type: 'Document', id: 'd1', key: 'DOC-1', title: 'Governance page' });
  });

  it('resolves a LocalizedText title (Action) to a plain string in the snapshot', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    renderWithAuth(<ArtifactPicker label="To" pickableTypes={['Action']} value={null} onChange={onChange} />, { roles: ['secretary'] });
    // Single pickable type auto-selects, so the artifact select is immediately enabled.
    await user.click(screen.getByRole('button', { name: 'Artifact' }));
    await user.click(screen.getByRole('option', { name: /ACT-1/ }));
    expect(onChange).toHaveBeenLastCalledWith({ type: 'Action', id: 'a1', key: 'ACT-1', title: 'Do it' });
  });
});
