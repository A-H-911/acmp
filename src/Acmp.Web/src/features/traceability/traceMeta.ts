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
import { ARTIFACT_TYPES, type ArtifactRelationships, type ArtifactType, type RelationshipType } from '../../api/traceability';
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

/*
 * ── Impact-graph shared meta (P10f) ──────────────────────────────────────────────────────────────
 * Colour by ARTIFACT TYPE (node dots + legend) and by DIRECTION (edges). The design coloured edges by
 * a toy 8-key relation map; over the real 16-RelationshipType / 4-DependencyKind vocabulary we colour
 * edges by their up/down/related direction instead (fewer colours, reuses REL_DIR/DEP_DIR — architect
 * ruling, guardrail #14 generalization). Node type needs the full 16-type map the design lacked.
 */

/** Full 16-ArtifactType (+System dep endpoint) colour map; unknown → muted text. */
export const NODE_TYPE_COLOR: Record<string, string> = {
  Topic: 'var(--accent)',
  Meeting: 'var(--st-sched-dot)',
  Agenda: 'var(--st-sched-dot)',
  MinutesOfMeeting: 'var(--st-sched-dot)',
  Vote: 'var(--st-info-dot)',
  Decision: 'var(--st-success-dot)',
  Action: 'var(--st-warn-dot)',
  Risk: 'var(--st-danger-dot)',
  Dependency: 'var(--st-neutral-dot)',
  Adr: 'var(--st-info-dot)',
  Invariant: 'var(--st-info-dot)',
  Diagram: 'var(--st-sched-dot)',
  ResearchMission: 'var(--st-neutral-dot)',
  Finding: 'var(--st-neutral-dot)',
  Recommendation: 'var(--st-neutral-dot)',
  Document: 'var(--st-neutral-dot)',
  System: 'var(--st-neutral-dot)',
};

export function typeColor(type: string): string {
  return NODE_TYPE_COLOR[type] ?? 'var(--text-3)';
}

/** Direction → edge / badge colour. */
export const DIR_COLOR: Record<PanelDir, string> = {
  up: 'var(--st-warn-dot)',
  down: 'var(--st-info-dot)',
  related: 'var(--st-neutral-dot)',
};

/** An impact-graph edge's direction, from its source ("rel"|"dep") + relation name (a plain string). */
export function relDirection(source: 'rel' | 'dep', rel: string): PanelDir {
  if (source === 'dep') return DEP_DIR[rel as DependencyKind] ?? 'related';
  return REL_DIR[rel as RelationshipType] ?? 'related';
}

/** One collapsible group in the 320px Relationships aside. */
export interface TypeGroup {
  key: string;
  /** i18n key for the group heading (deps.kind.* for dependency groups, trace.type.* for rel groups). */
  labelKey: string;
  /** ArtifactType for a relationship group (drives the icon); undefined for a dependency-kind group. */
  artifactType?: ArtifactType;
  dir: PanelDir;
  items: { key: string; title: string; href: string | null }[];
}

const DEP_KIND_ORDER: DependencyKind[] = ['DependsOn', 'BlockedBy', 'Blocks', 'RelatesTo'];

/**
 * Artifact types that can be a graph FOCUS via a deep link — they have both a detail route and a
 * by-key hook to cold-resolve the GUID. Other types can still be a focus via warm navigation (the
 * clicked node already carries its GUID), but not via a bare URL.
 */
export const GRAPH_FOCUS_TYPES: readonly ArtifactType[] = ['Topic', 'Decision', 'Action', 'Risk'];

/**
 * Build the aside's group-by-TYPE view from the two 1-hop panel reads (AC-062 seams, already warm in
 * cache on the warm-nav path). Grouping rule (architect ruling): dependency edges group by their KIND
 * (Depends on / Blocked by / Blocks / Relates to); relationship edges group by the FAR artifact TYPE —
 * so a Topic can sit under both "Depends on" and "Blocks", matching the design's mixed groups. A rel
 * group's direction is uniform when its edges agree, else 'related' (honest catch-all, flagged).
 */
export function buildTypeGroups(
  rels: ArtifactRelationships | undefined,
  deps: ArtifactDependencies | undefined,
): TypeGroup[] {
  const depByKind = new Map<DependencyKind, TypeGroup['items']>();
  const pushDep = (e: ArtifactDependencies['outbound'][number]) => {
    const list = depByKind.get(e.kind) ?? [];
    list.push({ key: e.otherKey, title: e.otherTitle, href: hrefFor(e.otherType, e.otherKey) });
    depByKind.set(e.kind, list);
  };
  deps?.outbound.forEach(pushDep);
  deps?.inbound.forEach(pushDep);

  const relByType = new Map<ArtifactType, { items: TypeGroup['items']; dirs: Set<PanelDir> }>();
  const pushRel = (e: ArtifactRelationships['outgoing'][number]) => {
    const base = REL_DIR[e.relType];
    const dir = e.direction === 'Outgoing' ? base : invert(base);
    const g = relByType.get(e.otherType) ?? { items: [], dirs: new Set<PanelDir>() };
    g.items.push({ key: e.otherKey, title: e.otherTitle, href: hrefFor(e.otherType, e.otherKey) });
    g.dirs.add(dir);
    relByType.set(e.otherType, g);
  };
  rels?.outgoing.forEach(pushRel);
  rels?.incoming.forEach(pushRel);

  const groups: TypeGroup[] = [];
  DEP_KIND_ORDER.forEach((kind) => {
    const items = depByKind.get(kind);
    if (items?.length) groups.push({ key: `dep:${kind}`, labelKey: `deps.kind.${kind}`, dir: DEP_DIR[kind], items });
  });
  ARTIFACT_TYPES.forEach((type) => {
    const g = relByType.get(type);
    if (g?.items.length) {
      groups.push({
        key: `rel:${type}`,
        labelKey: `trace.type.${type}`,
        artifactType: type,
        dir: g.dirs.size === 1 ? [...g.dirs][0] : 'related',
        items: g.items,
      });
    }
  });
  return groups;
}
