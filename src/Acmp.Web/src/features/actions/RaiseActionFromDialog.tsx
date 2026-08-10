/*
 * "Raise an action from…" — the source chooser behind the Actions register's primary CTA.
 *
 * WHY A CHOOSER AND NOT A FIELD IN THE CREATE DIALOG. An ActionItem carries a NON-NULLABLE
 * (SourceType, SourceId): every action is raised FROM a governance artifact, and ActionItem.SourceKey
 * exists specifically as "snapshot for the Linked column" the register renders. Until now the only way
 * in was from a source page (a decision, a meeting), so the design's `primary: New action` header CTA
 * on the Actions register ("ACMP Lists & Registers.dc.html") had nothing to open. This supplies the
 * missing step and then hands off to CreateActionDialog UNCHANGED, so the dialog its two existing
 * callers depend on keeps its tests and its behaviour.
 *
 * WHY LISTS AND NOT GLOBAL SEARCH. Search looked like the obvious backing, but every ISearchProvider
 * returns empty for an empty query — it is a type-ahead, not a browse — and its Meetings-side provider
 * indexes MINUTES (ArtifactType "MoMs"), whose PublicId is not a meeting's. Each type's own register
 * hook already returns {id, key, title}, which is exactly the (SourceId, SourceKey) pair the command
 * wants, and it browses.
 *
 * SCOPE: Topic · Decision · Meeting. ActionSourceType also allows Condition and Risk, which have no
 * list or lookup to pick from yet — deferred deliberately rather than inventing two cross-module
 * contracts here.
 *
 * Each type's candidates live in their own small component so only the SELECTED type fetches; three
 * hooks in one body would query all three registers every time the dialog opened.
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Segmented } from '../../components/ui/Segmented';
import { Icon } from '../../components/icons';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { useBacklog } from '../../api/topics';
import { useDecisionsRegister, type LocalizedText } from '../../api/decisions';
import { useMeetings } from '../../api/meetings';
import type { ActionSource } from './CreateActionDialog';

/** The source types a user can actually pick today (see SCOPE above). */
export type PickableSourceType = 'Topic' | 'Decision' | 'Meeting';

interface Candidate {
  id: string;
  key: string;
  label: string;
}

/** Cap the fetch: this is a picker, not a register. ≤20 users, low volume (CON scale ceiling). */
const MAX_CANDIDATES = 100;

function matches(c: Candidate, filter: string): boolean {
  const f = filter.trim().toLowerCase();
  if (!f) return true;
  return c.label.toLowerCase().includes(f) || c.key.toLowerCase().includes(f);
}

function CandidateList({
  candidates, isLoading, isError, onRetry, filter, onPick,
}: {
  candidates: Candidate[];
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  filter: string;
  onPick: (c: Candidate) => void;
}) {
  const { t } = useTranslation();
  if (isLoading) return <LoadingState />;
  if (isError) return <ErrorState onRetry={onRetry} />;

  const shown = candidates.filter((c) => matches(c, filter));
  if (shown.length === 0) return <EmptyState icon="search" title={t('actions.raise.noneTitle')} body={t('actions.raise.noneBody')} />;

  return (
    <ul className="raise-src-list">
      {shown.map((c) => (
        <li key={c.id}>
          {/* A button, not a row click: the picker must be reachable and operable by keyboard. */}
          <button type="button" className="raise-src-item" onClick={() => onPick(c)}>
            <span className="raise-src-key">{c.key}</span>
            <span className="raise-src-label">{c.label}</span>
          </button>
        </li>
      ))}
    </ul>
  );
}

function TopicCandidates({ filter, onPick }: { filter: string; onPick: (c: Candidate) => void }) {
  const { data, isLoading, isError, refetch } = useBacklog({ pageSize: MAX_CANDIDATES });
  return (
    <CandidateList
      candidates={(data?.items ?? []).map((x) => ({ id: x.id, key: x.key, label: x.title }))}
      isLoading={isLoading} isError={isError} onRetry={() => refetch()} filter={filter} onPick={onPick}
    />
  );
}

function DecisionCandidates({ filter, onPick }: { filter: string; onPick: (c: Candidate) => void }) {
  const { i18n } = useTranslation();
  const { data, isLoading, isError, refetch } = useDecisionsRegister({ limit: MAX_CANDIDATES });
  const pick = (l: LocalizedText) => (i18n.language === 'ar' ? l.ar : l.en);
  return (
    <CandidateList
      candidates={(data ?? []).map((x) => ({ id: x.id, key: x.key, label: pick(x.title) }))}
      isLoading={isLoading} isError={isError} onRetry={() => refetch()} filter={filter} onPick={onPick}
    />
  );
}

function MeetingCandidates({ filter, onPick }: { filter: string; onPick: (c: Candidate) => void }) {
  const { data, isLoading, isError, refetch } = useMeetings();
  return (
    <CandidateList
      candidates={(data ?? []).map((x) => ({ id: x.id, key: x.key, label: x.title }))}
      isLoading={isLoading} isError={isError} onRetry={() => refetch()} filter={filter} onPick={onPick}
    />
  );
}

export function RaiseActionFromDialog({
  open, onClose, onPicked,
}: {
  open: boolean;
  onClose: () => void;
  onPicked: (source: ActionSource) => void;
}) {
  const { t } = useTranslation();
  const [type, setType] = useState<PickableSourceType>('Decision');
  const [filter, setFilter] = useState('');

  const handlePick = (c: Candidate) =>
    onPicked({ sourceType: type, sourceId: c.id, sourceKey: c.key });

  return (
    <Dialog
      open={open}
      onClose={onClose}
      icon={<Icon name="action" size={20} aria-hidden />}
      title={t('actions.raise.title')}
      description={t('actions.raise.subtitle')}
      footer={<Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>}
    >
      <Segmented
        ariaLabel={t('actions.raise.typeLabel')}
        value={type}
        onValueChange={(id) => { setType(id as PickableSourceType); setFilter(''); }}
        items={[
          { id: 'Decision', label: t('actions.source.Decision') },
          { id: 'Meeting', label: t('actions.source.Meeting') },
          { id: 'Topic', label: t('actions.source.Topic') },
        ]}
      />

      <input
        type="search"
        className="raise-src-filter"
        aria-label={t('actions.raise.filterLabel')}
        placeholder={t('actions.raise.filterLabel')}
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
      />

      {/* Only the selected type mounts, so only it fetches. */}
      {type === 'Decision' && <DecisionCandidates filter={filter} onPick={handlePick} />}
      {type === 'Meeting' && <MeetingCandidates filter={filter} onPick={handlePick} />}
      {type === 'Topic' && <TopicCandidates filter={filter} onPick={handlePick} />}
    </Dialog>
  );
}
