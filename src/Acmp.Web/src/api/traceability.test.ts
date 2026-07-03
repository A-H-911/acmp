import { describe, it, expect, afterEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useArtifactRelationships, useCreateRelationship, useTraceGraph, RELATIONSHIP_TYPES, ARTIFACT_TYPES } from './traceability';
import { makeQueryWrapper, stubFetch, lastBody } from '../test/queryHarness';

/* Real traceability hooks vs a stubbed fetch — assert URL building, enabling, and create body. */
afterEach(() => vi.unstubAllGlobals());

const urlOf = (spy: ReturnType<typeof stubFetch>) => String(spy.mock.calls.at(-1)![0]);

describe('canonical enum arrays', () => {
  it('carry all 16 relationship types and all 16 artifact types', () => {
    expect(RELATIONSHIP_TYPES).toHaveLength(16);
    expect(ARTIFACT_TYPES).toHaveLength(16);
    expect(new Set(RELATIONSHIP_TYPES).size).toBe(16);
    expect(new Set(ARTIFACT_TYPES).size).toBe(16);
  });
});

describe('useArtifactRelationships', () => {
  it('is disabled until both type and id are present, then reads the panel', async () => {
    const spy = stubFetch(() => ({ jsonBody: { outgoing: [], incoming: [] } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(
      ({ t, i }: { t?: 'Decision'; i?: string }) => useArtifactRelationships(t, i),
      { wrapper, initialProps: {} as { t?: 'Decision'; i?: string } },
    );
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ t: 'Decision', i: 'g-9' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/traceability/Decision/g-9');
  });
});

describe('useTraceGraph', () => {
  it('is disabled without a resolved id, then reads the depth-bounded subgraph', async () => {
    const spy = stubFetch(() => ({ jsonBody: { focusType: 'Topic', focusId: 'g-1', depth: 3, nodes: [], edges: [], partial: false } }));
    const { wrapper } = makeQueryWrapper();
    const { rerender, result } = renderHook(
      ({ i }: { i?: string }) => useTraceGraph('Topic', i, 3),
      { wrapper, initialProps: {} as { i?: string } },
    );
    await new Promise((r) => setTimeout(r, 0));
    expect(spy).not.toHaveBeenCalled();
    rerender({ i: 'g-1' });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(urlOf(spy)).toBe('/api/traceability/graph/Topic/g-1?depth=3');
  });
});

describe('useCreateRelationship', () => {
  it('POSTs the create body and returns the new id', async () => {
    const spy = stubFetch(() => ({ status: 201, jsonBody: { id: 'edge-1' } }));
    const { wrapper } = makeQueryWrapper();
    const { result } = renderHook(() => useCreateRelationship(), { wrapper });
    const input = {
      sourceType: 'Topic' as const, sourceId: 'a', sourceKey: 'TOP-1', sourceTitle: 'A',
      targetType: 'Decision' as const, targetId: 'b', targetKey: 'DECN-1', targetTitle: 'B',
      relType: 'DecidedBy' as const, notes: 'why',
    };
    const res = await result.current.mutateAsync(input);
    expect(res.id).toBe('edge-1');
    const call = spy.mock.calls.at(-1)!;
    expect(String(call[0])).toBe('/api/traceability');
    expect((call[1] as RequestInit).method).toBe('POST');
    expect(lastBody(spy)).toEqual(input);
  });
});
