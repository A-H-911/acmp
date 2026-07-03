/*
 * Traceability server state (P10e). Wraps GET /api/traceability/{type}/{id} (the artifact panel —
 * outgoing + incoming typed edges) and POST /api/traceability (create a typed edge). Deactivate lands
 * in a later slice (read + create this slice, mirroring the P10b split).
 *
 * A Relationship is a self-describing directed edge (ADR-0019): the far endpoint carries a create-time
 * key + title snapshot, so the FE never resolves it cross-module (ADR-0001). Enums travel as string
 * names, localized in the UI. The panel row carries NO far-artifact lifecycle status (that would be a
 * cross-module read) — the UI shows relType + direction + key + title + a navigable link (AC-062).
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';

/** ArtifactType (docs/30 §1.1) — the 16 governed artifact kinds that can be an edge endpoint. */
export type ArtifactType =
  | 'Topic'
  | 'Meeting'
  | 'Agenda'
  | 'MinutesOfMeeting'
  | 'Vote'
  | 'Decision'
  | 'Action'
  | 'Risk'
  | 'Dependency'
  | 'Adr'
  | 'Invariant'
  | 'Diagram'
  | 'ResearchMission'
  | 'Finding'
  | 'Recommendation'
  | 'Document';

/** RelationshipType (docs/30 §2.2) — the curated directed typed-edge vocabulary (16 values). */
export type RelationshipType =
  | 'DecidedBy'
  | 'RecordedAs'
  | 'Produces'
  | 'Mitigates'
  | 'Addresses'
  | 'Supersedes'
  | 'Governs'
  | 'Violates'
  | 'DependsOn'
  | 'Informs'
  | 'IllustratedBy'
  | 'References'
  | 'DerivedFrom'
  | 'Implements'
  | 'Blocks'
  | 'Resolves';

/** Read-time perspective relative to the viewed artifact. */
export type RelationshipDirection = 'Outgoing' | 'Incoming';

/** Every RelationshipType value (create dialog options + i18n-parity coverage). */
export const RELATIONSHIP_TYPES: RelationshipType[] = [
  'DecidedBy', 'RecordedAs', 'Produces', 'Mitigates', 'Addresses', 'Supersedes', 'Governs',
  'Violates', 'DependsOn', 'Informs', 'IllustratedBy', 'References', 'DerivedFrom', 'Implements',
  'Blocks', 'Resolves',
];

/** Every ArtifactType value (panel far-type labels + i18n-parity coverage). */
export const ARTIFACT_TYPES: ArtifactType[] = [
  'Topic', 'Meeting', 'Agenda', 'MinutesOfMeeting', 'Vote', 'Decision', 'Action', 'Risk',
  'Dependency', 'Adr', 'Invariant', 'Diagram', 'ResearchMission', 'Finding', 'Recommendation', 'Document',
];

export interface RelationshipEdge {
  id: string;
  relType: RelationshipType;
  direction: RelationshipDirection;
  otherType: ArtifactType;
  otherId: string;
  otherKey: string;
  otherTitle: string;
  notes: string | null;
}

export interface ArtifactRelationships {
  outgoing: RelationshipEdge[];
  incoming: RelationshipEdge[];
}

/** The traceability panel for one artifact (AC-062) — every ArtifactType is valid here. */
export function useArtifactRelationships(type: ArtifactType | undefined, id: string | undefined) {
  return useQuery({
    queryKey: ['traceability', 'artifact', type, id],
    queryFn: () => api<ArtifactRelationships>(`/traceability/${type}/${id}`),
    enabled: !!type && !!id,
  });
}

/*
 * Impact graph (P10f, FR-096). GET /api/traceability/graph/{type}/{id}?depth=1..3 returns the whole
 * depth-bounded subgraph around a focus artifact — the backend owns the BFS, the rel+dep union, the
 * Topic-scope cross-stream math, the blocked flag, dangling-edge filtering, and a MaxNodes=60 ceiling
 * (partial=true if capped or a node read failed). The SPA does NOT re-traverse; it lays out columns
 * from the signed tier and colours edges from source+direction. The FOCUS node carries empty key/title
 * (its identity is not on any edge) — the page injects those from the URL / router state.
 */

/** One node in the impact graph. Tier is signed: 0 = focus, negative = upstream, positive = downstream. */
export interface ImpactGraphNode {
  type: ArtifactType | 'System';
  id: string;
  key: string;
  title: string;
  tier: number;
  blocked: boolean;
  streams: string[];
}

/** One directed edge. source disambiguates rel (RelationshipType) from dep (DependencyKind) in `rel`. */
export interface ImpactGraphEdge {
  source: 'rel' | 'dep';
  rel: string;
  fromType: string;
  fromId: string;
  toType: string;
  toId: string;
  isBlocker: boolean;
  isCrossStream: boolean;
}

export interface ImpactGraph {
  focusType: string;
  focusId: string;
  depth: number;
  nodes: ImpactGraphNode[];
  edges: ImpactGraphEdge[];
  partial: boolean;
}

/**
 * The transitive impact subgraph for one focus artifact (FR-096). One query — the FE never re-BFSes.
 * Disabled until the focus GUID is resolved (warm = router state, cold = a by-key detail fetch).
 */
export function useTraceGraph(type: ArtifactType | undefined, id: string | undefined, depth: number) {
  return useQuery({
    queryKey: ['traceability', 'graph', type, id, depth],
    queryFn: () => api<ImpactGraph>(`/traceability/graph/${type}/${id}?depth=${depth}`),
    enabled: !!type && !!id,
  });
}

/** Create input — endpoint snapshots for both ends + the relationship type + optional notes. */
export interface CreateRelationshipInput {
  sourceType: ArtifactType;
  sourceId: string;
  sourceKey: string;
  sourceTitle: string;
  targetType: ArtifactType;
  targetId: string;
  targetKey: string;
  targetTitle: string;
  relType: RelationshipType;
  notes: string | null;
}

/**
 * Create a typed traceability edge (POST /api/traceability → 201 + { id }, AC-063). Chairman/Secretary
 * only (API-enforced). Invalidates the traceability family so any open panel re-renders with the edge.
 */
export function useCreateRelationship() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateRelationshipInput) =>
      api<{ id: string }>('/traceability', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['traceability'] }),
  });
}
