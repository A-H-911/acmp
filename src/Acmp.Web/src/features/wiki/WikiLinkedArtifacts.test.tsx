import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import { WikiLinkedArtifacts } from './WikiLinkedArtifacts';
import { renderWithAuth } from '../../test/render';
import type { RelationshipEdge } from '../../api/traceability';

vi.mock('../../api/traceability', async (orig) => ({
  ...(await orig<typeof import('../../api/traceability')>()),
  useArtifactRelationships: vi.fn(),
}));
import { useArtifactRelationships } from '../../api/traceability';

const mock = useArtifactRelationships as unknown as Mock;
const edge = (over: Partial<RelationshipEdge>): RelationshipEdge => ({
  id: 'e1', relType: 'References', direction: 'Outgoing', otherType: 'Topic',
  otherId: 'g1', otherKey: 'TOP-2026-001', otherTitle: 'Some topic', notes: null, ...over,
});

describe('WikiLinkedArtifacts (WK10)', () => {
  beforeEach(() => mock.mockReset());

  it('renders nothing when the document has no linked artifacts', () => {
    mock.mockReturnValue({ data: { outgoing: [], incoming: [] } });
    renderWithAuth(<WikiLinkedArtifacts documentId="d1" />);
    expect(screen.queryByText('Linked artifacts')).toBeNull();
  });

  it('links routable artifacts and renders non-routable ones as plain rows', () => {
    mock.mockReturnValue({
      data: {
        outgoing: [edge({ id: 'e1', otherType: 'Topic', otherKey: 'TOP-2026-001', otherTitle: 'Routable topic' })],
        incoming: [edge({ id: 'e2', otherType: 'Adr', otherKey: 'ADR-2026-003', otherTitle: 'Non-routable ADR', direction: 'Incoming' })],
      },
    });
    renderWithAuth(<WikiLinkedArtifacts documentId="d1" />);
    expect(screen.getByText('Linked artifacts')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /TOP-2026-001/ })).toHaveAttribute('href', '/topics/TOP-2026-001');
    // The ADR has no detail route → a plain row, never a dead link.
    expect(screen.queryByRole('link', { name: /ADR-2026-003/ })).toBeNull();
    expect(screen.getByText('ADR-2026-003')).toBeInTheDocument();
  });
});
