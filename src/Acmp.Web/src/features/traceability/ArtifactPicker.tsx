/*
 * Shared artifact picker (P10e) — a two-step (type → artifact) selector used by both the create-
 * dependency and create-relationship dialogs to choose an edge endpoint.
 *
 * Scope honesty: Topic, Action, Risk, and Document have a FE list source (useBacklog /
 * useActionsRegister / useRisksRegister / useWikiDocuments). The other ArtifactTypes have no register
 * hook yet, so they are NOT pickable — a dialog passes only the subset it can offer. This is why
 * contextual create (From pre-seeded from the launching artifact) is the primary path. A future
 * cross-artifact search endpoint widens the pickable set without touching this.
 *
 * Document is offered by the RELATIONSHIP dialog only (so a wiki page can be linked from the artifact
 * that cites it — ADR-0029); it is NOT a DependencyEndpointType, so the dependency dialog must never
 * pass it. Topic/Action/Risk names are identical in DependencyEndpointType and ArtifactType, so the
 * emitted `type` string is valid in whichever enum the parent dialog targets.
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Field } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { useBacklog } from '../../api/topics';
import { useActionsRegister } from '../../api/actions';
import { useRisksRegister } from '../../api/risks';
import { useWikiDocuments } from '../../api/wiki';

/** The artifact types this slice can list (and therefore pick). */
export type PickableType = 'Topic' | 'Action' | 'Risk' | 'Document';

export interface PickedArtifact {
  type: PickableType;
  id: string;
  key: string;
  title: string;
}

// ponytail: one large page per type — a low-traffic committee register (≤ a few hundred rows), so a
// single pageSize=200 read beats wiring search-as-you-type. Upgrade to a typeahead if any register
// outgrows one page.
const PAGE = 200;

interface Props {
  label: string;
  pickableTypes: PickableType[];
  value: PickedArtifact | null;
  onChange: (v: PickedArtifact | null) => void;
  error?: string;
}

export function ArtifactPicker({ label, pickableTypes, value, onChange, error }: Props) {
  const { t, i18n } = useTranslation();
  const [type, setType] = useState<PickableType | ''>(value?.type ?? (pickableTypes.length === 1 ? pickableTypes[0] : ''));

  // Register reads (rules of hooks: declared unconditionally). The wiki-documents read is gated to when
  // Document is actually offered, so the dependency dialog never fetches wiki pages.
  const topics = useBacklog({ pageSize: PAGE, includeClosed: true });
  const actions = useActionsRegister({ pageSize: PAGE });
  const risks = useRisksRegister({ pageSize: PAGE });
  const docs = useWikiDocuments({}, pickableTypes.includes('Document'));

  const pickLoc = (v: { en: string; ar: string }) => (i18n.language === 'ar' ? v.ar : v.en);

  const optionsFor = (chosen: PickableType): { id: string; key: string; title: string }[] => {
    if (chosen === 'Topic') return (topics.data?.items ?? []).map((x) => ({ id: x.id, key: x.key, title: x.title }));
    if (chosen === 'Action') return (actions.data?.items ?? []).map((x) => ({ id: x.id, key: x.key, title: pickLoc(x.title) }));
    if (chosen === 'Document') return (docs.data?.items ?? []).map((x) => ({ id: x.id, key: x.key, title: pickLoc(x.title) }));
    return (risks.data?.items ?? []).map((x) => ({ id: x.id, key: x.key, title: pickLoc(x.title) }));
  };

  const artifactOptions = type ? optionsFor(type).map((a) => ({ value: a.id, label: `${a.key} — ${a.title}` })) : [];
  const typeOptions = pickableTypes.map((pt) => ({ value: pt, label: t(`trace.type.${pt}`) }));

  const onTypeChange = (v: string) => {
    setType(v as PickableType);
    onChange(null); // clear the artifact when the type changes
  };

  const onArtifactChange = (id: string) => {
    if (!type) return;
    const a = optionsFor(type).find((x) => x.id === id);
    onChange(a ? { type, id: a.id, key: a.key, title: a.title } : null);
  };

  return (
    <div className="dep-picker" role="group" aria-label={label}>
      <span className="dep-picker-label">{label}</span>
      <div className="dep-picker-row">
        <Field label={t('trace.picker.type')} required>
          {(p) => (
            <Select
              id={p.id}
              options={typeOptions}
              value={type}
              onChange={onTypeChange}
              placeholder={t('trace.picker.typePh')}
              ariaLabel={t('trace.picker.type')}
            />
          )}
        </Field>
        <Field label={t('trace.picker.artifact')} required error={error}>
          {(p) => (
            <Select
              id={p.id}
              options={artifactOptions}
              value={value?.id ?? ''}
              onChange={onArtifactChange}
              placeholder={type ? t('trace.picker.artifactPh') : t('trace.picker.pickTypeFirst')}
              ariaLabel={t('trace.picker.artifact')}
              disabled={!type}
              aria-invalid={p['aria-invalid']}
              aria-describedby={p['aria-describedby']}
            />
          )}
        </Field>
      </div>
    </div>
  );
}
