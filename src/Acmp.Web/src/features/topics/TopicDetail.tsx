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
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useTopicDetail, useAddTopicComment, useUploadTopicAttachment, type TopicDetail as Topic } from '../../api/topics';
import { ApiError } from '../../api/apiClient';
import { Tabs } from '../../components/ui/Tabs';
import { StatusChip } from '../../components/ui/StatusChip';
import { Tag, Badge } from '../../components/ui/Chip';
import { Button } from '../../components/ui/Button';
import { Textarea } from '../../components/ui/Field';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import { statusTone, initials } from './topicMeta';
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
        <RelationshipsSidebar />
      </div>
    </section>
  );
}

function DetailHeader({ topic }: { topic: Topic }) {
  const { t } = useTranslation();
  const fmt = useDateFmt();
  const urgent = topic.urgency !== 'Normal';
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
        <Button disabled title={t('topics.comingSoon')}>
          <Icon name="calendar" size={15} aria-hidden /> {t('detail.addAgenda')}
        </Button>
        <Button variant="secondary" disabled title={t('topics.comingSoon')}>{t('detail.edit')}</Button>
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

function RelationshipsSidebar() {
  const { t } = useTranslation();
  return (
    <aside className="dt-rel" aria-label={t('detail.rel.title')}>
      <div className="dt-rel-head">
        <div className="dt-rel-title">
          <Icon name="deps" size={16} aria-hidden />
          <h2>{t('detail.rel.title')}</h2>
        </div>
        <div className="dt-rel-sub">{t('detail.rel.sub')}</div>
      </div>
      <div className="dt-rel-body">
        <EmptyState title={t('detail.rel.emptyTitle')} body={t('detail.rel.emptyBody')} />
      </div>
    </aside>
  );
}
