/*
 * Agenda builder (P6c) — THE design screen ("ACMP Agenda & Meeting.dc.html", the
 * isAgenda block). Composes the shared library (Button, Dialog, StatusChip,
 * Select, states). Read by key (GET /api/meetings/{key}); every agenda mutation is by the
 * meeting's Guid id and returns the updated AgendaDto (the query is invalidated to re-render).
 *
 * P6d: the page breadcrumb + the Agenda/Meeting tab switcher are now owned by MeetingPage
 * (this renders inside the "Agenda builder" tab), so this component no longer renders its
 * own breadcrumb — that prevents a duplicate trail.
 *
 * Design↔behavior reconciliations (visual SoT = design; behavior SoT = the package):
 *  - LEFT column is labeled "Scheduled topics" in the design but is sourced from the
 *    PREPARED backlog (GET /api/topics?status=Prepared): topics only become Scheduled when
 *    the agenda is published. Label kept, source = Prepared. Pool items already on the
 *    agenda are filtered out (they'd otherwise appear in both columns pre-publish).
 *  - Reorder API is ±1 only (delta ∈ {+1,-1}; AC-044). The move up/down buttons are the
 *    real reorder path (keyboard + pointer accessible) and announce via an aria-live region.
 *    Pointer drag FROM the pool = Add; pointer drag WITHIN the agenda nudges one step toward
 *    the drop target (always a single ±1 move — never N chained calls). Native drag is
 *    progressive enhancement (jsdom can't exercise it); the buttons carry the behavior.
 *  - The design's "Preview" button is mock chrome → rendered disabled (coming soon).
 *  - The design "cycles" a presenter avatar; we replace it with an accessible Select picker
 *    sourced from GET /api/members. ASM: a member's publicId is the presenterUserId the
 *    agenda expects (the two id spaces aren't cross-verifiable from the SPA).
 *  - The publish dialog's "notify groups" checkboxes are mock chrome — the backend notifies
 *    ALL committee members unconditionally, so we show one honest static line instead.
 *  - Meeting/topic titles are single-language user content; only chrome is i18n'd. Dates are
 *    Gregorian, localized via Intl (guardrail 9).
 *
 * `readOnly` (P6 meeting-detail IA): the agenda is editable ONLY while the meeting is Scheduled
 * AND its agenda is still Draft. Once published/locked or the meeting starts/concludes/cancels,
 * MeetingPage renders this in VIEWER mode — the pool, drag, steppers, presenter picker, move and
 * remove controls and the Publish action all disappear; items render read-only. This is the fix
 * for the bug where a started meeting still showed an editable builder (the backend rejected the
 * edits, but the UI invited them).
 */
import { useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  useMeetingDetail,
  usePreparedTopics,
  useAddAgendaItem,
  useRemoveAgendaItem,
  useMoveAgendaItem,
  useSetTimebox,
  useAssignPresenter,
  usePublishAgenda,
  type AgendaItem,
} from '../../api/meetings';
import { useMembers } from '../../api/members';
import type { TopicSummary } from '../../api/topics';
import { ApiError } from '../../api/apiClient';
import { Button } from '../../components/ui/Button';
import { Dialog } from '../../components/ui/Dialog';
import { Select } from '../../components/ui/Select';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { agendaTone } from './agendaStatus';
import './meetings.css';

const TIMEBOX_STEP = 5;
const MIN_TIMEBOX = 5;
const DEFAULT_TIMEBOX = 15;

function minutesBetween(startIso: string, endIso: string): number {
  return Math.max(0, Math.round((new Date(endIso).getTime() - new Date(startIso).getTime()) / 60000));
}

export function AgendaBuilder({ readOnly = false }: { readOnly?: boolean } = {}) {
  const { key } = useParams();
  const { t, i18n } = useTranslation();
  const meetingQuery = useMeetingDetail(key);
  const preparedQuery = usePreparedTopics();
  const members = useMembers();

  const meeting = meetingQuery.data;
  const meetingId = meeting?.id;

  // Mutations (all keyed to invalidate this meeting's detail).
  const addItem = useAddAgendaItem(key);
  const removeItem = useRemoveAgendaItem(key);
  const moveItem = useMoveAgendaItem(key);
  const setTimebox = useSetTimebox(key);
  const assignPresenter = useAssignPresenter(key);
  const publish = usePublishAgenda(key);

  const [search, setSearch] = useState('');
  const [publishOpen, setPublishOpen] = useState(false);
  const [announce, setAnnounce] = useState('');
  // Native-drag payload (refs so drag doesn't trigger re-renders). jsdom can't drive these.
  const dragPool = useRef<TopicSummary | null>(null);
  const dragItem = useRef<AgendaItem | null>(null);

  const fmtDate = (iso: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));

  if (meetingQuery.isLoading) {
    return <section className="page"><LoadingState /></section>;
  }
  if (meetingQuery.isError || !meeting || !meetingId) {
    const notFound = meetingQuery.error instanceof ApiError && meetingQuery.error.status === 404;
    return (
      <section className="page">
        {notFound ? (
          <EmptyState icon="calendar" title={t('meetings.notFound.title')} body={t('meetings.notFound.body')} />
        ) : (
          <ErrorState title={t('meetings.error.title')} body={t('meetings.error.body')} onRetry={() => meetingQuery.refetch()} />
        )}
      </section>
    );
  }

  const agenda = meeting.agenda;
  const items = agenda?.items ?? [];
  const agendaStatus = agenda?.status ?? 'Draft';
  const published = agenda?.status === 'Published';

  // Budget: server-summed used minutes vs the meeting's scheduled duration.
  const total = minutesBetween(meeting.scheduledStart, meeting.scheduledEnd);
  const used = agenda?.totalTimeboxMinutes ?? 0;
  const remaining = total - used;
  const over = used > total;
  const usedPct = total > 0 ? Math.min(100, Math.round((used / total) * 100)) : 0;

  // Pool = Prepared topics not already placed on this agenda, filtered by the search box.
  const placed = new Set(items.map((i) => i.topicId));
  const q = search.trim().toLowerCase();
  const pool = (preparedQuery.data?.items ?? [])
    .filter((tp) => !placed.has(tp.id))
    .filter((tp) => !q || tp.title.toLowerCase().includes(q) || tp.key.toLowerCase().includes(q));

  const onAdd = (tp: TopicSummary) => {
    if (placed.has(tp.id)) return;
    addItem.mutate({
      meetingId,
      topicId: tp.id,
      topicKey: tp.key,
      topicTitle: tp.title,
      urgent: tp.urgency !== 'Normal',
      timeboxMinutes: DEFAULT_TIMEBOX,
    });
    setAnnounce(t('meetings.announce.added', { key: tp.key }));
  };

  const onMove = (item: AgendaItem, delta: 1 | -1) => {
    moveItem.mutate({ meetingId, topicId: item.topicId, delta });
    // Announce synchronously — the live order re-renders from the invalidated query.
    setAnnounce(t(delta < 0 ? 'meetings.announce.movedUp' : 'meetings.announce.movedDown', { key: item.topicKey }));
  };

  const onTimebox = (item: AgendaItem, dir: 1 | -1) => {
    const minutes = Math.max(MIN_TIMEBOX, item.timeboxMinutes + dir * TIMEBOX_STEP);
    if (minutes === item.timeboxMinutes) return;
    setTimebox.mutate({ meetingId, topicId: item.topicId, minutes });
  };

  const onPresenter = (item: AgendaItem, userId: string) => {
    const m = members.data?.find((x) => x.publicId === userId);
    if (!m) return;
    assignPresenter.mutate({ meetingId, topicId: item.topicId, presenterUserId: m.publicId, presenterName: m.fullName });
  };

  const onRemove = (item: AgendaItem) => {
    removeItem.mutate({ meetingId, topicId: item.topicId });
    setAnnounce(t('meetings.announce.removed', { key: item.topicKey }));
  };

  // Native drag: pool→agenda = Add; within-agenda = single ±1 nudge toward the drop target.
  const onAgendaDrop = () => {
    const tp = dragPool.current;
    dragPool.current = null;
    if (tp) onAdd(tp);
  };
  const onItemDrop = (target: AgendaItem) => {
    const src = dragItem.current;
    dragItem.current = null;
    if (src && src.topicId !== target.topicId) onMove(src, src.order < target.order ? 1 : -1);
  };

  const presenterOptions = (members.data ?? []).map((m) => ({ value: m.publicId, label: m.fullName }));

  return (
    <section className="page mt-builder">
      <div className="visually-hidden" aria-live="polite">{announce}</div>

      <div className="mt-builder-head">
        <div>
          <div className="mt-builder-titlerow">
            <h1 className="page-title">{meeting.title}</h1>
            <StatusChip tone={agendaTone(agendaStatus)} label={t(`meetings.agendaStatus.${agendaStatus}`, { defaultValue: agendaStatus })} />
          </div>
          <div className="mt-builder-meta">
            <span><Icon name="calendar" size={14} aria-hidden /> {fmtDate(meeting.scheduledStart)}</span>
            <span><Icon name="clock" size={14} aria-hidden /> {t('meetings.lengthMin', { count: total })}</span>
          </div>
        </div>
        {!readOnly && (
          <div className="mt-builder-actions">
            <Button variant="secondary" disabled title={t('meetings.comingSoon')}>
              <Icon name="download" size={15} aria-hidden /> {t('meetings.preview')}
            </Button>
            <Button onClick={() => setPublishOpen(true)} disabled={published || items.length === 0}>
              <Icon name="send" size={15} aria-hidden /> {t('meetings.publishNotify')}
            </Button>
          </div>
        )}
      </div>

      <BudgetBar used={used} total={total} remaining={remaining} over={over} usedPct={usedPct} />

      {readOnly ? (
        <AgendaPreview items={items} usedMinutes={used} />
      ) : (
        <div className="mt-grid">
          <PoolColumn
            pool={pool}
            count={pool.length}
            search={search}
            onSearch={setSearch}
            onAdd={onAdd}
            loading={preparedQuery.isLoading}
            dragRef={dragPool}
          />

          <section
            className="mt-agenda"
            aria-label={t('meetings.agendaItems')}
            onDragOver={(e) => e.preventDefault()}
            onDrop={onAgendaDrop}
          >
            <div className="mt-agenda-head">
              <h2 className="mt-col-title">
                {t('meetings.agendaOrder')} <span className="mt-muted">· {t('meetings.itemCount', { count: items.length })}</span>
              </h2>
              <span className="mt-drag-hint"><Icon name="grip" size={13} aria-hidden /> {t('meetings.dragHint')}</span>
            </div>

            {items.length === 0 ? (
              <EmptyState icon="viewKanban" title={t('meetings.agendaEmpty.title')} body={t('meetings.agendaEmpty.body')} />
            ) : (
              <ol className="mt-agenda-list">
                {items.map((item, i) => (
                  <AgendaItemRow
                    key={item.topicId}
                    item={item}
                    index={i + 1}
                    isFirst={i === 0}
                    isLast={i === items.length - 1}
                    presenterOptions={presenterOptions}
                    onMove={onMove}
                    onTimebox={onTimebox}
                    onPresenter={onPresenter}
                    onRemove={onRemove}
                    dragRef={dragItem}
                    onItemDrop={onItemDrop}
                  />
                ))}
              </ol>
            )}
          </section>
        </div>
      )}

      <Dialog
        open={publishOpen}
        onClose={() => setPublishOpen(false)}
        title={t('meetings.publish.title')}
        description={t('meetings.publish.sub')}
        icon={<Icon name="send" size={18} aria-hidden />}
        footer={
          <>
            <Button variant="secondary" onClick={() => setPublishOpen(false)}>{t('meetings.cancel')}</Button>
            <Button
              loading={publish.isPending}
              onClick={() => publish.mutate({ meetingId }, { onSuccess: () => setPublishOpen(false) })}
            >
              {t('meetings.publish.confirm')}
            </Button>
          </>
        }
      >
        <div className="mt-publish-summary">
          <span>{t('meetings.publish.itemsTotal')}</span>
          <span className="mt-publish-figure">
            <b>{t('meetings.itemCount', { count: items.length })}</b> · {t('meetings.minutes', { count: used })}
          </span>
        </div>
        <p className="mt-publish-notify">
          <Icon name="bell" size={14} aria-hidden /> {t('meetings.publish.notifyAll')}
        </p>
      </Dialog>
    </section>
  );
}

const TIGHT_BUFFER_MIN = 10;

function BudgetBar({ used, total, remaining, over, usedPct }: { used: number; total: number; remaining: number; over: boolean; usedPct: number }) {
  const { t } = useTranslation();
  const buffer = Math.max(0, remaining);
  const tight = !over && buffer <= TIGHT_BUFFER_MIN && buffer > 0;
  // Three-tier model matching the design: Fits (success) · Tight fit (warn) · Over (danger).
  const tone: StatusTone = over ? 'danger' : tight ? 'warn' : 'success';
  const chipLabel = over ? t('meetings.budget.over', { count: -remaining }) : tight ? t('meetings.budget.tight') : t('meetings.budget.fits');
  const remainLabel = over ? t('meetings.budget.overRemain', { count: -remaining }) : t('meetings.budget.buffer', { count: buffer });
  const bufferPct = total > 0 ? Math.max(0, Math.round((buffer / total) * 100)) : 0;
  const fillClass = over ? 'over' : tight ? 'tight' : '';
  return (
    <div className={`mt-budget ${over ? 'over' : ''}`}>
      <div className="mt-budget-row">
        <div className="mt-budget-label">
          <span className="mt-budget-title">{t('meetings.budget.title')}</span>
          <StatusChip tone={tone} label={chipLabel} />
        </div>
        <div className="mt-budget-figures">
          <b>{used}</b> / {t('meetings.minutes', { count: total })} · <span className={`mt-budget-remain ${tone}`}>{remainLabel}</span>
        </div>
      </div>
      <div className="mt-budget-track" role="progressbar" aria-valuenow={used} aria-valuemin={0} aria-valuemax={total} aria-label={t('meetings.budget.title')}>
        <span className={`mt-budget-fill ${fillClass}`} style={{ inlineSize: `${usedPct}%` }} />
        {bufferPct > 0 && <span className="mt-budget-buffer" style={{ inlineSize: `${bufferPct}%` }} />}
      </div>
    </div>
  );
}

function PoolColumn({
  pool,
  count,
  search,
  onSearch,
  onAdd,
  loading,
  dragRef,
}: {
  pool: TopicSummary[];
  count: number;
  search: string;
  onSearch: (v: string) => void;
  onAdd: (tp: TopicSummary) => void;
  loading: boolean;
  dragRef: React.MutableRefObject<TopicSummary | null>;
}) {
  const { t } = useTranslation();
  return (
    <section className="mt-pool" aria-label={t('meetings.backlogPool')}>
      <div className="mt-pool-head">
        <div className="mt-pool-titlerow">
          <h2 className="mt-col-title">{t('meetings.scheduledTopics')}</h2>
          <span className="mt-pool-count">{count}</span>
        </div>
        <span className="mt-pool-search">
          <Icon name="search" size={14} aria-hidden />
          <input
            type="search"
            aria-label={t('meetings.searchTopics')}
            placeholder={t('meetings.searchTopics')}
            value={search}
            onChange={(e) => onSearch(e.target.value)}
          />
        </span>
      </div>
      <div className="mt-pool-list">
        {loading ? (
          <LoadingState />
        ) : pool.length === 0 ? (
          <EmptyState icon="checkCircle" title={t('meetings.poolEmpty.title')} body={t('meetings.poolEmpty.body')} />
        ) : (
          pool.map((tp) => {
            const urgent = tp.urgency !== 'Normal';
            return (
              <div
                key={tp.id}
                className="mt-pool-card"
                draggable
                /* v8 ignore start -- native HTML5 drag source (pool → agenda): jsdom can't run
                   the drag transfer; the click-to-add path is unit-tested, drag covered by S6 E2E. */
                onDragStart={() => {
                  dragRef.current = tp;
                }}
                onDragEnd={() => {
                  dragRef.current = null;
                }}
                /* v8 ignore stop */
              >
                <Icon name="grip" size={15} className="mt-pool-grip" aria-hidden />
                <div className="mt-pool-body">
                  <div className="mt-pool-keyrow">
                    <span className="mt-key">{tp.key}</span>
                    {urgent && (
                      <span className="mt-urgent-ic">
                        <Icon name="warnTriangle" size={12} aria-hidden />
                        <span className="visually-hidden">{t('meetings.urgent')}</span>
                      </span>
                    )}
                  </div>
                  <div className="mt-pool-title">{tp.title}</div>
                  <div className="mt-pool-foot">
                    <span className="mt-muted">{t(`topics.type.${tp.type}`, { defaultValue: tp.type })}</span>
                    <Button size="sm" variant="secondary" className="mt-pool-add" onClick={() => onAdd(tp)} aria-label={t('meetings.addTopic', { key: tp.key })}>
                      <Icon name="plus" size={12} aria-hidden /> {t('meetings.add')}
                    </Button>
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>
    </section>
  );
}

function AgendaItemRow({
  item,
  index,
  isFirst,
  isLast,
  presenterOptions,
  onMove,
  onTimebox,
  onPresenter,
  onRemove,
  dragRef,
  onItemDrop,
}: {
  item: AgendaItem;
  index: number;
  isFirst: boolean;
  isLast: boolean;
  presenterOptions: { value: string; label: string }[];
  onMove: (item: AgendaItem, delta: 1 | -1) => void;
  onTimebox: (item: AgendaItem, dir: 1 | -1) => void;
  onPresenter: (item: AgendaItem, userId: string) => void;
  onRemove: (item: AgendaItem) => void;
  dragRef: React.MutableRefObject<AgendaItem | null>;
  onItemDrop: (target: AgendaItem) => void;
}) {
  const { t } = useTranslation();
  return (
    <li
      className="mt-item"
      draggable
      /* v8 ignore start -- native HTML5 drag-reorder: jsdom does not run the drag data
         transfer, so these handlers fire only in a real browser. The keyboard reorder
         (Move up/down) is unit-tested; the pointer-drag path is covered by S6 E2E. */
      onDragStart={() => {
        dragRef.current = item;
      }}
      onDragEnd={() => {
        dragRef.current = null;
      }}
      onDragOver={(e) => e.preventDefault()}
      onDrop={(e) => {
        e.stopPropagation();
        onItemDrop(item);
      }}
      /* v8 ignore stop */
    >
      <div className="mt-item-rail">
        <span className="mt-item-index" aria-hidden="true">{index}</span>
        <Icon name="grip" size={16} className="mt-item-grip" aria-hidden />
      </div>

      <div className="mt-item-body">
        <div className="mt-item-keyrow">
          <span className="mt-key">{item.topicKey}</span>
          {item.urgent && (
            <span className="mt-urgent-pill">
              <Icon name="warnTriangle" size={10} aria-hidden /> {t('meetings.urgent')}
            </span>
          )}
        </div>
        <div className="mt-item-title">{item.topicTitle}</div>
        <div className="mt-item-controls">
          <span className="mt-timebox">
            <Icon name="clock" size={14} aria-hidden /> {t('meetings.timebox')}
            <span className="mt-stepper">
              <button type="button" className="mt-step" onClick={() => onTimebox(item, -1)} aria-label={t('meetings.decTime', { key: item.topicKey })}>
                <Icon name="minus" size={13} aria-hidden />
              </button>
              <span className="mt-step-val">{t('meetings.minShort', { count: item.timeboxMinutes })}</span>
              <button type="button" className="mt-step" onClick={() => onTimebox(item, 1)} aria-label={t('meetings.incTime', { key: item.topicKey })}>
                <Icon name="plus" size={13} aria-hidden />
              </button>
            </span>
          </span>
          <span className="mt-presenter">
            <Icon name="user" size={14} aria-hidden /> {t('meetings.presenter')}
            <span className="mt-presenter-pick">
              <Select
                ariaLabel={t('meetings.presenterFor', { key: item.topicKey })}
                placeholder={t('meetings.presenterPick')}
                value={item.presenterUserId ?? ''}
                onChange={(v) => onPresenter(item, v)}
                options={presenterOptions}
              />
            </span>
          </span>
        </div>
      </div>

      <div className="mt-item-tools">
        <button type="button" className="mt-tool" onClick={() => onMove(item, -1)} disabled={isFirst} aria-label={t('meetings.moveUp', { key: item.topicKey })}>
          <Icon name="chevronUp" size={13} aria-hidden />
        </button>
        <button type="button" className="mt-tool" onClick={() => onMove(item, 1)} disabled={isLast} aria-label={t('meetings.moveDown', { key: item.topicKey })}>
          <Icon name="chevronDown" size={13} aria-hidden />
        </button>
        <button type="button" className="mt-tool mt-tool-danger" onClick={() => onRemove(item)} aria-label={t('meetings.removeItem', { key: item.topicKey })}>
          <Icon name="x" size={13} aria-hidden />
        </button>
      </div>
    </li>
  );
}

/*
 * Agenda VIEWER (read-only) — design "Agenda preview" card (ACMP Meetings.dc.html isOverview,
 * ~L263). One card, flat rows split by border-soft, a 22px round number, title-over-presenter,
 * and a mono timebox. NOT the editable builder row (no steppers/picker/move/remove).
 *
 * Reconciliation (visual SoT = design; this is a deliberate deviation): the design preview row
 * shows only number/title/presenter/timebox — NO topic key. We re-add the topic key on the
 * secondary line because a Locked/Closed agenda is becoming an official, traceable record and the
 * canonical TOP-YYYY-### must stay visible (CLAUDE.md traceability). Urgent/icons stay dropped.
 */
function AgendaPreview({ items, usedMinutes }: { items: AgendaItem[]; usedMinutes: number }) {
  const { t } = useTranslation();
  /* v8 ignore start -- defensive: AgendaPreview only renders for a Locked/Closed agenda,
     which by the publish invariant always has ≥1 item, so the empty branch is unreachable
     in real flows (kept as a guard). */
  if (items.length === 0) {
    return (
      <section className="mt-agenda" aria-label={t('meetings.agendaItems')}>
        <EmptyState icon="viewKanban" title={t('meetings.agendaEmpty.title')} body={t('meetings.agendaEmptyView.body')} />
      </section>
    );
  }
  /* v8 ignore stop */
  return (
    <section className="mt-preview" aria-label={t('meetings.agendaItems')}>
      <div className="mt-preview-head">
        <h2 className="mt-preview-title">{t('meetings.agendaView')}</h2>
        <span className="mt-preview-meta">
          {t('meetings.itemCount', { count: items.length })} · {t('meetings.minutes', { count: usedMinutes })}
        </span>
      </div>
      <ol className="mt-preview-list">
        {items.map((item, i) => (
          <li key={item.topicId} className="mt-preview-item">
            <span className="mt-preview-num" aria-hidden="true">{i + 1}</span>
            <span className="mt-preview-body">
              <span className="mt-preview-itemtitle">{item.topicTitle}</span>
              <span className="mt-preview-sub">
                <span className="mt-preview-key">{item.topicKey}</span>
                <span className="mt-preview-dot" aria-hidden="true">·</span>
                <span className="mt-preview-presenter">{item.presenterName ?? t('meetings.presenterUnset')}</span>
              </span>
            </span>
            <span className="mt-preview-box">{t('meetings.minShort', { count: item.timeboxMinutes })}</span>
          </li>
        ))}
      </ol>
    </section>
  );
}
