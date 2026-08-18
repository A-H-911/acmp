/*
 * Topic detail (P5b) — matches the "ACMP Backlog & Topic" design (detail screen).
 * Composes the shared library (Breadcrumb, Tabs, StatusChip, Tag, Button, states).
 * Read by key (GET /api/topics/{key}); the comment POST is by the DTO's Guid id.
 *
 * Design↔behavior reconciliations (visual SoT = design; behavior SoT = package):
 *  - Single-language title — the design's "alt-language title" line is dropped (P5a decision).
 *  - The relationships sidebar is intentionally EMPTY in P5: topic→decision/ADR/action/risk links land
 *    in later phases. The aside renders its header + an empty state, no fabricated links.
 *  - "Add to agenda" (needs a Meeting → P6) and "Edit" (AC-034 edit flow → a follow-up slice) are rendered
 *    as disabled affordances; the read view + comment posting are this slice's live behavior.
 *  - Attachments are surfaced in Overview when present (real topic data, uploaded at submit) — the design's
 *    static overview omitted them.
 *  - Dates are Gregorian, localized via Intl (guardrail 9).
 */
import { useContext, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  useTopicDetail, useAddTopicComment, useUploadTopicAttachment, usePrepareTopic,
  useReactivateTopic, useCloseTopic, useReopenTopic, useConvertTopic, type TopicDetail as Topic,
} from '../../api/topics';
import { ApiError } from '../../api/apiClient';
import { Dialog } from '../../components/ui/Dialog';
import { Tabs } from '../../components/ui/Tabs';
import { StatusChip } from '../../components/ui/StatusChip';
import { Tag, Badge } from '../../components/ui/Chip';
import { Button } from '../../components/ui/Button';
import { Textarea } from '../../components/ui/Field';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, initials, TOPIC_TYPE_VALUES } from './topicMeta';
import { TraceabilityPanel } from '../traceability/TraceabilityPanel';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import './topics.css';

const TABS = ['overview', 'comments', 'attachments', 'votes', 'history'] as const;

function useDateFmt() {
  const { i18n } = useTranslation();
  return (iso: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));
}

export function TopicDetail() {
  const { key } = useParams();
  const { t } = useTranslation();
  const { data, isLoading, isError, error, refetch } = useTopicDetail(key);
  const [tab, setTab] = useState<string>('overview');

  if (isLoading) {
    return <section className="page"><LoadingState /></section>;
  }
  if (isError || !data) {
    const notFound = error instanceof ApiError && error.status === 404;
    return (
      <section className="page">
        {notFound ? (
          <EmptyState title={t('detail.notFoundTitle')} body={t('detail.notFoundBody')} />
        ) : (
          <ErrorState onRetry={() => refetch()} />
        )}
      </section>
    );
  }

  const topic = data;
  const tabs = TABS.map((id) => ({
    id,
    label:
      id === 'comments' ? (
        <>
          {t('detail.tab.comments')} <Badge count={topic.comments.length} />
        </>
      ) : id === 'attachments' && topic.attachments.length > 0 ? (
        <>
          {t('detail.tab.attachments')} <Badge count={topic.attachments.length} />
        </>
      ) : (
        t(`detail.tab.${id}`)
      ),
  }));

  return (
    <section className="page">
      <DetailHeader topic={topic} />

      <div className="dt-grid">
        <div className="dt-main">
          <Tabs items={tabs} value={tab} onValueChange={setTab} ariaLabel={topic.key} />
          {tab === 'overview' && <Overview topic={topic} />}
          {tab === 'comments' && <Discussion topic={topic} />}
          {tab === 'attachments' && <Attachments topic={topic} />}
          {tab === 'votes' && <Votes />}
          {tab === 'history' && <History topic={topic} />}
        </div>
        <TraceabilityPanel traceType="Topic" depType="Topic" id={topic.id} artifactKey={topic.key} title={topic.title} />
      </div>
    </section>
  );
}

function DetailHeader({ topic }: { topic: Topic }) {
  const { t } = useTranslation();
  const fmt = useDateFmt();
  const urgent = topic.urgency !== 'Normal';
  const prepare = usePrepareTopic(topic.key);
  const reactivate = useReactivateTopic(topic.key);
  const close = useCloseTopic(topic.key);
  const reopen = useReopenTopic(topic.key);
  const navigate = useNavigate();
  const [prepareErr, setPrepareErr] = useState<string | null>(null);
  const [reopenOpen, setReopenOpen] = useState(false);
  const [reopenReason, setReopenReason] = useState('');
  const convert = useConvertTopic(topic.key);
  const [convertOpen, setConvertOpen] = useState(false);
  const [convertReason, setConvertReason] = useState('');
  const [convertType, setConvertType] = useState('');

  // FR-160/FR-161/FR-045 — the lifecycle EXITS. Each of these transitions existed on the aggregate
  // with no caller (DEF-084): a Decided topic could never be closed, a Deferred one never came back,
  // and a Rejected or Closed one could never be reopened. Same show-and-enforce rule as prepare —
  // the button is offered on the right status and the backend refuses the wrong role.
  const onLifecycle = (run: () => void) => {
    setPrepareErr(null);
    run();
  };
  const lifecycleError = (e: unknown) =>
    setPrepareErr(e instanceof ApiError && e.status === 403 ? t('topics.prepare.forbidden') : t('topics.prepare.error'));
  // W4 (AC-035): move an Accepted topic into the agenda pool. Show-and-enforce — the button is offered
  // for any Accepted topic and the backend 403s a non-owner/non-Secretary, surfaced inline.
  const onPrepare = () => {
    setPrepareErr(null);
    prepare.mutate(topic.id, {
      onError: (e) =>
        setPrepareErr(e instanceof ApiError && e.status === 403 ? t('topics.prepare.forbidden') : t('topics.prepare.error')),
    });
  };
  return (
    <div className="dt-head">
      <div className="dt-head-main">
        <div className="dt-chips">
          <span className="dt-key">{topic.key}</span>
          <StatusChip tone={statusTone(topic.status)} label={t(`topics.status.${topic.status}`)} />
          {urgent && (
            <span className="dt-urgent">
              <Icon name="warnTriangle" size={12} aria-hidden /> {t(`topics.urgency.${topic.urgency}`)}
            </span>
          )}
          {/* FR-163 / C-AUTHZ-04. ⚠ ICON + TEXT, never colour alone (WCAG 1.4.1 / NFR-034): the whole
              point of this badge is that a reader must not have to infer "restricted" from a hue. */}
          {topic.restricted && (
            <span className="dt-restricted">
              <Icon name="lock" size={12} aria-hidden /> {t('topics.restricted.badge')}
            </span>
          )}
        </div>
        <h1 className="dt-title">{topic.title}</h1>
        <div className="dt-meta">
          <span className="dt-owner">
            {topic.ownerName ? (
              <>
                <span className="bk-avatar" aria-hidden="true">{initials(topic.ownerName)}</span>
                {t('detail.owner')} <b>{topic.ownerName}</b>
              </>
            ) : (
              <span className="bk-muted">{t('topics.unassigned')}</span>
            )}
          </span>
          <span className="dt-created">
            <Icon name="calendar" size={14} aria-hidden /> {t('detail.created', { date: fmt(topic.createdAt) })}
          </span>
        </div>
      </div>
      <div className="dt-actions">
        {topic.status === 'Accepted' && (
          <Button onClick={onPrepare} loading={prepare.isPending}>
            <Icon name="checkCircle" size={15} aria-hidden /> {t('topics.prepare.button')}
          </Button>
        )}
        {/* AC-110 — the affordance the revisit date never had. */}
        {topic.status === 'Deferred' && (
          <Button
            onClick={() => onLifecycle(() => reactivate.mutate(topic.id, { onError: lifecycleError }))}
            loading={reactivate.isPending}
          >
            <Icon name="checkCircle" size={15} aria-hidden /> {t('topics.reactivate.button')}
          </Button>
        )}
        {/* AC-109 — Decided was a permanent resting state until this existed. */}
        {topic.status === 'Decided' && (
          <Button
            onClick={() => onLifecycle(() => close.mutate(topic.id, { onError: lifecycleError }))}
            loading={close.isPending}
          >
            <Icon name="checkCircle" size={15} aria-hidden /> {t('topics.close.button')}
          </Button>
        )}
        {/* AC-113 / FR-030 — convert to another type. Shares the Decided status with Close, so this
            header carries two lifecycle actions at once; the crowding was checked in a browser, not
            just in JSDOM. No .dc.html covers a topic-type conversion affordance (the Usage Map maps
            topic detail to "ACMP Backlog & Topic.dc.html", whose only "convert" hits are the
            research→topic and decision→ADR flows), so this is a NO-REFERENCE COMPOSITION built from
            the design system and the verified PR #289 button pattern (INV-014). */}
        {topic.status === 'Decided' && (
          <Button onClick={() => { setConvertReason(''); setConvertType(''); setConvertOpen(true); }}>
            <Icon name="refresh" size={15} aria-hidden /> {t('topics.convert.button')}
          </Button>
        )}
        {/* AC-112 / FR-045 — approved in the original plan, unbuilt until now. */}
        {(topic.status === 'Rejected' || topic.status === 'Closed') && (
          <Button onClick={() => { setReopenReason(''); setReopenOpen(true); }}>
            <Icon name="warnTriangle" size={15} aria-hidden /> {t('topics.reopen.button')}
          </Button>
        )}
        <Button disabled title={t('topics.comingSoon')}>
          <Icon name="calendar" size={15} aria-hidden /> {t('detail.addAgenda')}
        </Button>
        {/* AC-034: live as of DEC-045. Shown for every non-immutable topic and NOT role-gated here —
            the server decides what this actor may change (the submitter edits their own pre-Accept
            topic; a Secretary edits metadata after). Hiding it by role would also hide it from the
            owner, who is resolved per-topic by ABAC rather than by any claim the SPA can read. */}
        {!['Decided', 'Closed', 'Converted'].includes(topic.status) && (
          <Button variant="secondary" onClick={() => navigate(`/topics/${topic.key}/edit`)}>{t('detail.edit')}</Button>
        )}
        {prepareErr && <span className="dt-prepare-err" role="alert">{prepareErr}</span>}
        {/*
          AC-112 — the justification is MANDATORY (FR-044's rule), so the confirm stays disabled
          until one is typed rather than letting the server refuse a request the UI could have
          prevented.
        */}
        <Dialog
          open={reopenOpen}
          onClose={() => setReopenOpen(false)}
          icon={<Icon name="warnTriangle" size={20} aria-hidden />}
          title={t('topics.reopen.title')}
          description={t('topics.reopen.subtitle')}
          footer={
            <>
              <Button variant="secondary" onClick={() => setReopenOpen(false)}>{t('common.cancel')}</Button>
              <Button
                variant="primary"
                loading={reopen.isPending}
                disabled={reopenReason.trim().length === 0}
                onClick={() =>
                  onLifecycle(() =>
                    reopen.mutate(
                      { topicId: topic.id, reason: reopenReason.trim() },
                      { onSuccess: () => setReopenOpen(false), onError: lifecycleError },
                    ))}
              >
                {t('topics.reopen.confirm')}
              </Button>
            </>
          }
        >
          {/*
            The design system's own `.field-label` / `.textarea`, NOT bespoke classes. The first
            version hand-rolled both and the visual check caught two defects a green suite could
            not: `var(--radius-2)` does not exist so the corners fell back to SQUARE against a
            rounded system, and without `min-inline-size: 0` the textarea collapsed to 34px inside
            the dialog's flex body. `.textarea` already carries both, plus focus and invalid states.
          */}
          <label className="field-label" htmlFor="reopen-reason">{t('topics.reopen.label')}</label>
          <textarea
            id="reopen-reason"
            className="textarea"
            rows={3}
            value={reopenReason}
            onChange={(e) => setReopenReason(e.target.value)}
          />
        </Dialog>
        {/*
          AC-113 / FR-030. BOTH inputs are mandatory: the target type (there is no sensible default —
          defaulting would let a mis-click retire a Decided topic into the wrong type) and the reason,
          which the requirement names explicitly. Same design-system classes as the reopen dialog —
          `.field-label` / `.textarea` / `.input`, never bespoke CSS (DW-031).
        */}
        <Dialog
          open={convertOpen}
          onClose={() => setConvertOpen(false)}
          icon={<Icon name="refresh" size={20} aria-hidden />}
          title={t('topics.convert.title')}
          description={t('topics.convert.subtitle')}
          footer={
            <>
              <Button variant="secondary" onClick={() => setConvertOpen(false)}>{t('common.cancel')}</Button>
              <Button
                variant="primary"
                loading={convert.isPending}
                disabled={convertType === '' || convertReason.trim().length === 0}
                onClick={() =>
                  onLifecycle(() =>
                    convert.mutate(
                      { topicId: topic.id, targetType: convertType, reason: convertReason.trim() },
                      {
                        // The topic under the user is now Converted and terminal, so staying here
                        // would show a dead record. Navigate to the successor the server just made.
                        onSuccess: (created) => { setConvertOpen(false); navigate(`/topics/${created.key}`); },
                        onError: lifecycleError,
                      },
                    ))}
              >
                {t('topics.convert.confirm')}
              </Button>
            </>
          }
        >
          {/*
            Each label+control is wrapped in `.field`, the design system's own grouping class, because
            `.field + .field { margin-block-start: var(--sp-4) }` is what separates consecutive fields.
            The reopen dialog omits it and looks fine only because it has a SINGLE field — with two,
            the visual check showed the second label sitting flush against the select above it. Found
            by looking at it; JSDOM reports both labels present either way.
          */}
          <div className="field">
            <label className="field-label" htmlFor="convert-type">{t('topics.convert.typeLabel')}</label>
            <select
              id="convert-type"
              className="input"
              value={convertType}
              onChange={(e) => setConvertType(e.target.value)}
            >
              <option value="">{t('topics.convert.typePlaceholder')}</option>
              {/* The current type is excluded: converting a topic to what it already is is refused by
                  the server, so offering it would be an option that can only fail. */}
              {TOPIC_TYPE_VALUES.filter((v) => v !== topic.type).map((v) => (
                <option key={v} value={v}>{t(`topics.type.${v}`)}</option>
              ))}
            </select>
          </div>
          <div className="field">
            <label className="field-label" htmlFor="convert-reason">{t('topics.convert.label')}</label>
            <textarea
              id="convert-reason"
              className="textarea"
              rows={3}
              value={convertReason}
              onChange={(e) => setConvertReason(e.target.value)}
            />
          </div>
        </Dialog>
      </div>
    </div>
  );
}

function Section({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="dt-section">
      <h3 className="dt-section-label">{label}</h3>
      {children}
    </div>
  );
}

function Overview({ topic }: { topic: Topic }) {
  const { t } = useTranslation();
  return (
    <div className="dt-overview">
      <Section label={t('detail.sec.description')}><p className="dt-body">{topic.description}</p></Section>
      <Section label={t('detail.sec.justification')}><p className="dt-body">{topic.justification}</p></Section>
      <div className="dt-two">
        <Section label={t('detail.sec.streams')}>
          <div className="bk-streams">{topic.streams.length ? topic.streams.map((s) => <Tag key={s} tone="info">{s}</Tag>) : <span className="bk-muted">—</span>}</div>
        </Section>
        <Section label={t('detail.sec.systems')}>
          <div className="bk-streams">{topic.systems.length ? topic.systems.map((s) => <Tag key={s}>{s}</Tag>) : <span className="bk-muted">—</span>}</div>
        </Section>
      </div>
    </div>
  );
}

function Attachments({ topic }: { topic: Topic }) {
  const { t } = useTranslation();
  const fmt = useDateFmt();
  const upload = useUploadTopicAttachment(topic.key);
  const inputRef = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);

  const onFiles = (list: FileList | null) => {
    if (!list) return;
    for (const f of Array.from(list)) upload.mutate({ topicId: topic.id, file: f });
  };

  return (
    <div className="dt-attach">
      <div
        className={`sub-drop ${over ? 'over' : ''}`}
        onDragOver={(e) => { e.preventDefault(); setOver(true); }}
        onDragLeave={() => setOver(false)}
        onDrop={(e) => { e.preventDefault(); setOver(false); onFiles(e.dataTransfer.files); }}
      >
        <input ref={inputRef} type="file" multiple aria-label={t('submit.dropFiles')} className="visually-hidden" onChange={(e) => onFiles(e.target.files)} />
        <div className="sub-drop-ic" aria-hidden="true"><Icon name="upload" size={19} /></div>
        <button type="button" className="sub-drop-btn" onClick={() => inputRef.current?.click()} disabled={upload.isPending}>
          {t('submit.dropFiles')}
        </button>
        <div className="sub-drop-hint">{t('submit.dropHint')}</div>
      </div>
      {topic.attachments.length === 0 ? (
        <p className="bk-muted dt-attach-empty">{t('detail.attach.empty')}</p>
      ) : (
        <ul className="sub-files dt-attach-list">
          {topic.attachments.map((a) => (
            <li key={a.id} className="sub-file">
              <span className="sub-file-ic" aria-hidden="true"><Icon name="doc" size={15} /></span>
              <span className="sub-file-main">
                <span className="sub-file-name" dir="ltr">{a.fileName}</span>
                <span className="sub-file-meta">{a.uploadedByName} · {fmt(a.uploadedAt)}</span>
              </span>
              {/* Download needs a presigned-URL endpoint not exposed in this DTO yet → inert, flagged. */}
              <button type="button" className="dt-attach-dl" aria-label={t('detail.attach.download')} disabled title={t('topics.comingSoon')}>
                <Icon name="download" size={14} aria-hidden />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function Votes() {
  const { t } = useTranslation();
  return (
    <div className="dt-votes">
      <EmptyState icon="decision" title={t('detail.votes.emptyTitle')} body={t('detail.votes.emptyBody')} />
    </div>
  );
}

function Discussion({ topic }: { topic: Topic }) {
  const { t } = useTranslation();
  const fmt = useDateFmt();
  const [body, setBody] = useState('');
  const me = useContext(AcmpAuthContext)?.initials ?? '';
  const addComment = useAddTopicComment(topic.key);
  const post = () => {
    const text = body.trim();
    if (!text) return;
    addComment.mutate({ topicId: topic.id, body: text }, { onSuccess: () => setBody('') });
  };
  return (
    <div className="dt-discussion">
      {topic.comments.length === 0 ? (
        <p className="bk-muted">{t('detail.noComments')}</p>
      ) : (
        <ul className="dt-comments">
          {topic.comments.map((c) => (
            <li key={c.id} className="dt-comment">
              <span className="bk-avatar" aria-hidden="true">{initials(c.authorName)}</span>
              <div className="dt-comment-main">
                <div className="dt-comment-head"><b>{c.authorName}</b><span className="bk-muted">{fmt(c.postedAt)}</span></div>
                <div className="dt-comment-body">{c.body}</div>
              </div>
            </li>
          ))}
        </ul>
      )}
      <div className="dt-compose-row">
        <span className="bk-avatar dt-compose-av" aria-hidden="true">{me}</span>
        <div className="dt-compose">
          <Textarea
            rows={3}
            aria-label={t('detail.commentLabel')}
            placeholder={t('detail.commentPlaceholder')}
            value={body}
            onChange={(e) => setBody(e.target.value)}
          />
          <div className="dt-compose-foot">
            <Button onClick={post} disabled={!body.trim()} loading={addComment.isPending}>{t('detail.post')}</Button>
          </div>
        </div>
      </div>
    </div>
  );
}

function History({ topic }: { topic: Topic }) {
  const { t } = useTranslation();
  const fmt = useDateFmt();
  if (topic.history.length === 0) return <p className="bk-muted">{t('detail.noHistory')}</p>;
  return (
    <ol className="dt-timeline">
      {topic.history.map((h, i) => (
        <li key={i} className="dt-tl-item">
          <span className={`dt-tl-dot tone-${statusTone(h.to)}`} aria-hidden="true" />
          <div className="dt-tl-main">
            <div className="dt-tl-text">
              {h.from ? `${t(`topics.status.${h.from}`)} → ${t(`topics.status.${h.to}`)}` : t(`topics.status.${h.to}`)}
              {h.reason && <span className="dt-tl-reason"> — {h.reason}</span>}
            </div>
            <div className="dt-tl-meta">{h.actorName} · {fmt(h.occurredAt)}</div>
          </div>
        </li>
      ))}
    </ol>
  );
}

