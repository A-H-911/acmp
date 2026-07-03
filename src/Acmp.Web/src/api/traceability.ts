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
