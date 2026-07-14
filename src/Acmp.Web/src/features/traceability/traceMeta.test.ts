import { describe, it, expect } from 'vitest';
import { buildPanelRows, buildTypeGroups, panelRowCount, hrefFor, relDirection, typeColor, GRAPH_FOCUS_TYPES } from './traceMeta';
import type { ArtifactRelationships } from '../../api/traceability';
import type { ArtifactDependencies } from '../../api/dependencies';

const rels = (over: Partial<ArtifactRelationships> = {}): ArtifactRelationships => ({ outgoing: [], incoming: [], ...over });
const deps = (over: Partial<ArtifactDependencies> = {}): ArtifactDependencies => ({ outbound: [], inbound: [], ...over });

const depEdge = (o: Partial<ArtifactDependencies['outbound'][number]> = {}) => ({
  id: 'd1', key: 'DPN-1', otherType: 'Topic' as const, otherId: 'o', otherKey: 'TOP-9', otherTitle: 'Other',
  kind: 'DependsOn' as const, status: 'Open' as const, isBlocker: false, ...o,
});
const relEdge = (o: Partial<ArtifactRelationships['outgoing'][number]> = {}) => ({
  id: 'r1', relType: 'Produces' as const, direction: 'Outgoing' as const, otherType: 'Action' as const,
  otherId: 'o', otherKey: 'ACT-9', otherTitle: 'Do', notes: null, ...o,
});

describe('hrefFor', () => {
  it('routes the known artifact types and returns null for the routeless ones', () => {
    expect(hrefFor('Topic', 'TOP-1')).toBe('/topics/TOP-1');
    expect(hrefFor('Decision', 'DECN-1')).toBe('/decisions/DECN-1');
    expect(hrefFor('Action', 'ACT-1')).toBe('/actions/ACT-1');
    expect(hrefFor('Risk', 'RSK-1')).toBe('/risks/RSK-1');
    expect(hrefFor('Meeting', 'MTG-1')).toBe('/meetings/MTG-1');
    expect(hrefFor('Dependency', 'DPN-1')).toBe('/dependencies/DPN-1');
    expect(hrefFor('Adr', 'ADR-1')).toBeNull();
    expect(hrefFor('System', 'SVC-1')).toBeNull();
  });
});

describe('buildPanelRows — dependency direction', () => {
  it('outbound DependsOn is upstream; inbound DependsOn inverts to downstream', () => {
    const g = buildPanelRows(rels(), deps({ outbound: [depEdge({ kind: 'DependsOn' })], inbound: [depEdge({ id: 'd2', kind: 'DependsOn' })] }));
    expect(g.up.map((r) => r.id)).toEqual(['d1']);
    expect(g.down.map((r) => r.id)).toEqual(['d2']);
  });

  it('outbound Blocks is downstream; RelatesTo stays related either way', () => {
    const g = buildPanelRows(rels(), deps({ outbound: [depEdge({ id: 'b', kind: 'Blocks' })], inbound: [depEdge({ id: 'rel', kind: 'RelatesTo' })] }));
    expect(g.down.map((r) => r.id)).toEqual(['b']);
    expect(g.related.map((r) => r.id)).toEqual(['rel']);
  });

  it('carries the blocked flag and the localizable relation label key', () => {
    const g = buildPanelRows(rels(), deps({ outbound: [depEdge({ isBlocker: true })] }));
    expect(g.up[0].blocked).toBe(true);
    expect(g.up[0].relLabel).toBe('deps.kind.DependsOn');
    expect(g.up[0].source).toBe('dep');
    expect(g.up[0].href).toBe('/topics/TOP-9');
  });
});

describe('buildPanelRows — relationship direction', () => {
  it('uses the base direction for outgoing and inverts for incoming', () => {
    // Produces base = down. Outgoing → down; incoming → up.
    const g = buildPanelRows(
      rels({ outgoing: [relEdge({ relType: 'Produces', direction: 'Outgoing' })], incoming: [relEdge({ id: 'r2', relType: 'Produces', direction: 'Incoming' })] }),
      deps(),
    );
    expect(g.down.map((r) => r.id)).toEqual(['r1']);
    expect(g.up.map((r) => r.id)).toEqual(['r2']);
  });

  it('labels relationship rows with the trace.rel.* key and routes the far endpoint', () => {
    const g = buildPanelRows(rels({ outgoing: [relEdge()] }), deps());
    expect(g.down[0].relLabel).toBe('trace.rel.Produces');
    expect(g.down[0].source).toBe('rel');
    expect(g.down[0].href).toBe('/actions/ACT-9');
    expect(g.down[0].blocked).toBe(false);
  });
});

describe('buildPanelRows — merge + count', () => {
  it('merges both sources and counts across all directions; handles undefined inputs', () => {
    const g = buildPanelRows(rels({ outgoing: [relEdge()] }), deps({ outbound: [depEdge()] }));
    expect(panelRowCount(g)).toBe(2);
    const empty = buildPanelRows(undefined, undefined);
    expect(panelRowCount(empty)).toBe(0);
  });
});

describe('impact-graph meta', () => {
  it('typeColor covers known types and falls back for unknown', () => {
    expect(typeColor('Topic')).toBe('var(--accent)');
    expect(typeColor('Risk')).toBe('var(--st-danger-dot)');
    expect(typeColor('Nonsense')).toBe('var(--text-3)');
  });

  it('relDirection reads dep vs rel vocabularies and falls back to related', () => {
    expect(relDirection('dep', 'DependsOn')).toBe('up');
    expect(relDirection('dep', 'Blocks')).toBe('down');
    expect(relDirection('rel', 'Produces')).toBe('down');
    expect(relDirection('rel', 'DerivedFrom')).toBe('up');
    expect(relDirection('rel', 'Nonsense')).toBe('related');
  });

  it('GRAPH_FOCUS_TYPES is the routable-as-focus types (incl. ResearchMission for W16)', () => {
    expect([...GRAPH_FOCUS_TYPES]).toEqual(['Topic', 'Decision', 'Action', 'Risk', 'ResearchMission']);
  });
});

describe('buildTypeGroups (aside)', () => {
  it('groups dependency edges by KIND and relationship edges by far TYPE', () => {
    const groups = buildTypeGroups(
      rels({ outgoing: [relEdge({ relType: 'DecidedBy', otherType: 'Decision', otherKey: 'DECN-8', otherTitle: 'Approve' })] }),
      deps({ outbound: [depEdge({ kind: 'DependsOn', otherType: 'Topic', otherKey: 'TOP-9' })] }),
    );
    const depGroup = groups.find((g) => g.key === 'dep:DependsOn')!;
    expect(depGroup.labelKey).toBe('deps.kind.DependsOn');
    expect(depGroup.dir).toBe('up');
    expect(depGroup.items[0]).toEqual({ key: 'TOP-9', title: 'Other', href: '/topics/TOP-9' });
    const relGroup = groups.find((g) => g.key === 'rel:Decision')!;
    expect(relGroup.labelKey).toBe('trace.type.Decision');
    expect(relGroup.artifactType).toBe('Decision');
    expect(relGroup.dir).toBe('down');
    // dependency groups sort before relationship groups.
    expect(groups[0].key).toBe('dep:DependsOn');
  });

  it('a far type with edges of conflicting directions collapses to "related"', () => {
    const groups = buildTypeGroups(
      rels({
        outgoing: [relEdge({ relType: 'Produces', otherType: 'Action', otherKey: 'ACT-1' })], // down
        incoming: [relEdge({ id: 'r2', relType: 'Produces', direction: 'Incoming', otherType: 'Action', otherKey: 'ACT-2' })], // inverts → up
      }),
      deps(),
    );
    const relGroup = groups.find((g) => g.key === 'rel:Action')!;
    expect(relGroup.dir).toBe('related');
    expect(relGroup.items).toHaveLength(2);
  });

  it('is empty when both sources are empty/undefined', () => {
    expect(buildTypeGroups(undefined, undefined)).toEqual([]);
  });
});
