---
name: p15-audit-remediation
description: "P15 Research & Knowledge design-fidelity + i18n audit remediation — batches, locked decisions, ADR-0029"
metadata: 
  node_type: memory
  type: project
  originSessionId: da70e27a-e641-43fd-83ae-30292e279005
---

P15 (Research & Knowledge) was correctness-green but the fidelity audit found INV-014/INV-009 defects. Fixed on branch `fix/p15-audit-remediation` in 5 batches, all gates green (FE 1050 tests/parity 1768/oxlint/build/cov exit0; BE build/format/cov 99.67%).

- **B1** `--serif` token + `@fontsource/ibm-plex-serif` (LTR-scoped: `[dir=rtl]` flips `--serif`→`--font-arabic`); `categoryLabel(c,t)` in [[i18n-parity-not-completeness]] extracted to wikiMeta, applied in WikiReadingView breadcrumb.
- **B2** M4 subtitle; **m5** `UpdatedAt` on `ResearchMissionSummaryDto` + `"updated"` sort arm → register column Created→**Updated** (`updatedAt ?? createdAt`); m19 `arrowRight` `dir-flip`; m16 DMY.
- **B3** M3 wiki-local 7-icon toolbar via pure `markdownInsert.ts` util (shared MarkdownEditor untouched, 6 surfaces safe); WK8 debounced localStorage draft autosave; **WK10 = ADR-0029 (operator chose option C)** replaced `TraceabilityPanel` with bespoke read-only `WikiLinkedArtifacts` card (reuses `useArtifactRelationships('Document',id)`); to preserve linking, `Document` made a **pickable relationship target** (`ArtifactPicker` + `CreateRelationshipDialog` only, NOT dependency dialog; `useWikiDocuments` gated by `pickableTypes.includes('Document')`) → wiki page linked *from the citing artifact*, bidirectional; m15 muted version chip; m17 AR read-time Arabic-Indic (`ar-u-nu-arab`); m18 History ungated to all readers.
- **B4** m1 glyph file→template; m13 filtered-empty variant; reconciled stale orphaned `admin.sub.templates` desc.
- **B5** m22 search status localized via `searchMeta.ts` `(type)→ns` map + raw fallback (Decisions has no status-ns → raw); m6 `/wiki` nav "Knowledge / Wiki"→"Knowledge"; **m20 pencil path already matches design — no-op**.

**Locked decisions:** force-match mockup; DMY = P15-scoped `lib/p15Date.ts` `formatDmy` (NOT app-wide, tech-debt logged). Un-recoverable minors (m2/7/8/9 CSS tokens, m4/11/12 copy) reconstructed conservatively (value-preserving token adoption). New tested units: `p15Date.ts`, `markdownInsert.ts`, `WikiLinkedArtifacts.tsx`, `searchMeta.ts`. Follows [[phase-prompt-standard-footer]], [[exact-design-fidelity-visual-loop]]. See [[p15-research-knowledge-plan]].
