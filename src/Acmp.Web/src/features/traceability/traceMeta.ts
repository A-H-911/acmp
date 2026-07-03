/*
 * Pure presentation logic for the traceability panel (P10e). Merges the two edge sources —
 * typed Relationship edges (/api/traceability) and governed Dependency edges
 * (/api/dependencies/artifact) — into one Upstream / Downstream / Related view (AC-062).
 *
 * Direction axis: the design specifies a relation→direction mapping only for the 4 dependency
 * kinds (Lists&Registers `relMeta`) and 7 far-artifact types (Traceability&Deps `groupDefs`),
 * NOT for all 16 RelationshipTypes. REL_DIR below is therefore a curated FE heuristic derived
 * from each type's documented Source→Target reading (RelationshipType.cs) — a no-reference
 * grouping axis (guardrail #14). It only decides which section a row sits in; the relation label
 * + navigable link carry the real semantics (AC-062 asks for type + id + title + link, not verbs).
 *
 * The far artifact's lifecycle status is NOT shown — reading it would cross a module boundary
 * (ADR-0001). Dependency edges instead carry their own self-describing `isBlocker` (ADR-0019),
 * surfaced as a "Blocked" pill. Routeless target types (ADR/System/Diagram/…) render as plain
 * text (no dead links), honestly flagged.
 */
import type { ArtifactRelationships, ArtifactType, RelationshipType } from '../../api/traceability';
import type { ArtifactDependencies, DependencyEndpointType, DependencyKind } from '../../api/dependencies';

export type PanelDir = 'up' | 'down' | 'related';

/** Design `relMeta`: DependsOn/BlockedBy = upstream, Blocks = downstream, RelatesTo = related. */
const DEP_DIR: Record<DependencyKind, PanelDir> = {
  DependsOn: 'up',
  BlockedBy: 'up',
  Blocks: 'down',
  RelatesTo: 'related',
};

/** Curated heuristic (see file header): target-relative-to-source for an OUTGOING edge. */
const REL_DIR: Record<RelationshipType, PanelDir> = {
  DecidedBy: 'down',
  RecordedAs: 'down',
  Produces: 'down',
  Mitigates: 'related',
  Addresses: 'related',
  Supersedes: 'up',
  Governs: 'down',
  Violates: 'related',
  DependsOn: 'up',
  Informs: 'down',
  IllustratedBy: 'related',
  References: 'related',
  DerivedFrom: 'up',
  Implements: 'up',
  Blocks: 'down',
  Resolves: 'related',
};

/** Flip perspective for an inbound/incoming edge (viewed artifact is the target end). */
function invert(d: PanelDir): PanelDir {
  return d === 'up' ? 'down' : d === 'down' ? 'up' : 'related';
}

/**
 * Artifact type → route segment. Only types with a real detail route are clickable; the rest
 * (Adr/System/Invariant/Diagram/Agenda/MoM/ResearchMission/Finding/Recommendation/Document)
 * render as non-navigable text until their routes land — never a dead link (honest, flagged).
 */
const ROUTE: Partial<Record<ArtifactType | DependencyEndpointType, string>> = {
  Topic: 'topics',
  Decision: 'decisions',
  Action: 'actions',
  Risk: 'risks',
  Meeting: 'meetings',
  Vote: 'votes',
  Dependency: 'dependencies',
};

export function hrefFor(type: string, key: string): string | null {
  const seg = ROUTE[type as ArtifactType];
  return seg ? `/${seg}/${key}` : null;
}

/** One merged row on the panel. `relLabel` is an i18n key (deps.kind.* or trace.rel.*). */
export interface PanelRow {
  id: string;
  dir: PanelDir;
  source: 'dep' | 'rel';
  relLabel: string;
  otherKey: string;
  otherTitle: string;
  href: string | null;
  blocked: boolean;
}

/**
 * Merge the traceability + dependency edges of one artifact into direction-grouped rows.
 * Either source may be undefined (e.g. a Risk detail passes no dependency data — there is no
 * DependencyEndpointType.Risk). Rows are stable-ordered: dependency edges first, then relationships.
 */
export function buildPanelRows(
  rels: ArtifactRelationships | undefined,
  deps: ArtifactDependencies | undefined,
): Record<PanelDir, PanelRow[]> {
  const grouped: Record<PanelDir, PanelRow[]> = { up: [], down: [], related: [] };

  const pushDep = (e: ArtifactDependencies['outbound'][number], outbound: boolean) => {
    const base = DEP_DIR[e.kind];
    const dir = outbound ? base : invert(base);
    grouped[dir].push({
      id: e.id,
      dir,
      source: 'dep',
      relLabel: `deps.kind.${e.kind}`,
      otherKey: e.otherKey,
      otherTitle: e.otherTitle,
      href: hrefFor(e.otherType, e.otherKey),
      blocked: e.isBlocker,
    });
  };
  deps?.outbound.forEach((e) => pushDep(e, true));
  deps?.inbound.forEach((e) => pushDep(e, false));

  const pushRel = (e: ArtifactRelationships['outgoing'][number]) => {
    const base = REL_DIR[e.relType];
    const dir = e.direction === 'Outgoing' ? base : invert(base);
    grouped[dir].push({
      id: e.id,
      dir,
      source: 'rel',
      relLabel: `trace.rel.${e.relType}`,
      otherKey: e.otherKey,
      otherTitle: e.otherTitle,
      href: hrefFor(e.otherType, e.otherKey),
      blocked: false,
    });
  };
  rels?.outgoing.forEach(pushRel);
  rels?.incoming.forEach(pushRel);

  return grouped;
}

/** Total edge count across all directions — drives the panel's empty state. */
export function panelRowCount(g: Record<PanelDir, PanelRow[]>): number {
  return g.up.length + g.down.length + g.related.length;
}
