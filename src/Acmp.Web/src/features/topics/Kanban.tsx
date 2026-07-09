/*
 * Backlog kanban (P5b) — a VIEW grouping topics into 5 buckets over canonical TopicStatus (P5a decision),
 * matching the design's kanban. Accessible DnD per the design: native HTML5 drag for pointer + a keyboard
 * "M" move popover (AC-043); an aria-live region announces every move.
 *
 * P5 transition reality (only the endpoints that exist): the ONLY input-free moves don't change bucket, so
 * every legal cross-bucket move opens a dialog and two columns reject all drops —
 *  - → accepted (from triage): accept needs an owner → AcceptDialog
 *  - → returned (from triage/accepted): reject or defer needs a reason → ReturnDialog
 *  - → scheduled / → done / others: no P5 endpoint (scheduling needs a Meeting → P6) → rejected with an
 *    announced reason, not a silent no-op.
 */
import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { type TopicSummary, useAcceptTopic, useReturnTopic } from '../../api/topics';
import { useMembers } from '../../api/members';
import { bucketOf, moveAction, KANBAN_BUCKETS, BUCKET_TONE, initials, type KanbanBucket } from './topicMeta';
import { Dialog } from '../../components/ui/Dialog';
import { Select } from '../../components/ui/Select';
import { Field, Textarea } from '../../components/ui/Field';
import { Radio } from '../../components/ui/Choice';
import { Button } from '../../components/ui/Button';
import { Tag } from '../../components/ui/Chip';
import { Icon } from '../../components/icons';

type Pending = { kind: 'accept' | 'return'; topic: TopicSummary } | null;

export function Kanban({ rows }: { rows: TopicSummary[] }) {
  const { t } = useTranslation();
  const [dragId, setDragId] = useState<string | null>(null);
  const [moveId, setMoveId] = useState<string | null>(null);
  const [pending, setPending] = useState<Pending>(null);
  const [live, setLive] = useState('');
  const liveRef = useRef(live);
  liveRef.current = live;

  const bucketLabel = (b: KanbanBucket) => t(`kanban.bucket.${b}`);

  const requestMove = (topicId: string, to: KanbanBucket) => {
    const topic = rows.find((r) => r.id === topicId);
    if (!topic) return;
    const action = moveAction(bucketOf(topic.status), to);
    if (action === 'none') return;
    if (action === 'illegal') {
      setLive(t('kanban.illegal', { bucket: bucketLabel(to) }));
      return;
    }
    setPending({ kind: action, topic });
  };

  const columns = KANBAN_BUCKETS.map((b) => ({
    bucket: b,
    cards: rows.filter((r) => bucketOf(r.status) === b),
  }));

  return (
    <div className="kb">
      <div className="kb-hint">
        <Icon name="grip" size={15} aria-hidden />
        {t('kanban.hint')}
      </div>
      <div aria-live="assertive" className="visually-hidden">{live}</div>

      <div className="kb-board">
        {columns.map((col) => (
          <section
            key={col.bucket}
            className={`kb-col ${dragId ? 'drop' : ''}`}
            aria-label={`${bucketLabel(col.bucket)}, ${col.cards.length}`}
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault();
              if (dragId) requestMove(dragId, col.bucket);
              setDragId(null);
            }}
          >
            <div className="kb-col-head">
              <span className="kb-col-title">
                <span className={`kb-dot tone-${BUCKET_TONE[col.bucket]}`} aria-hidden="true" />
                {bucketLabel(col.bucket)}
              </span>
              <span className="kb-count">{col.cards.length}</span>
            </div>
            <div className="kb-cards">
              {col.cards.map((c) => (
                <Card
                  key={c.id}
                  topic={c}
                  dragging={dragId === c.id}
                  onDragStart={() => setDragId(c.id)}
                  onDragEnd={() => setDragId(null)}
                  onMoveKey={() => setMoveId(c.id)}
                />
              ))}
            </div>
          </section>
        ))}
      </div>

      {moveId && (
        <MovePopover
          topic={rows.find((r) => r.id === moveId)}
          onPick={(to) => {
            requestMove(moveId, to);
            setMoveId(null);
          }}
          onClose={() => setMoveId(null)}
        />
      )}

      {pending?.kind === 'accept' && (
        <AcceptDialog topic={pending.topic} onClose={() => setPending(null)} onDone={(name) => { setLive(t('kanban.moved', { key: pending.topic.key, bucket: bucketLabel('accepted') })); setPending(null); void name; }} />
      )}
      {pending?.kind === 'return' && (
        <ReturnDialog topic={pending.topic} onClose={() => setPending(null)} onDone={() => { setLive(t('kanban.moved', { key: pending.topic.key, bucket: bucketLabel('returned') })); setPending(null); }} />
      )}
    </div>
  );
}

function Card({ topic, dragging, onDragStart, onDragEnd, onMoveKey }: {
  topic: TopicSummary; dragging: boolean; onDragStart: () => void; onDragEnd: () => void; onMoveKey: () => void;
}) {
  const { t } = useTranslation();
  const urgent = topic.urgency !== 'Normal';
  return (
    <div
      className={`kb-card ${dragging ? 'dragging' : ''}`}
      draggable
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      tabIndex={0}
      role="group"
      aria-label={`${topic.key}: ${topic.title}. ${t('kanban.moveHint')}`}
      onKeyDown={(e) => {
        if (e.key === 'm' || e.key === 'M') {
          e.preventDefault();
          onMoveKey();
        }
      }}
    >
      <div className="kb-card-top">
        <Link className="bk-key" to={`/topics/${topic.key}`}>{topic.key}</Link>
        {/* Accepted & Prepared share the 'accepted' bucket (topicMeta:42) — badge the Prepared ones so
            they're distinguishable without splitting the column (design = 5 fixed buckets). */}
        {topic.status === 'Prepared' && <Tag tone="info">{t('topics.status.Prepared')}</Tag>}
        {urgent && <Icon name="warnTriangle" size={12} className="bk-urgent-ic" aria-label={t('topics.urgent')} />}
      </div>
      <Link className="kb-card-title" to={`/topics/${topic.key}`}>{topic.title}</Link>
      <div className="kb-card-foot">
        <span className="bk-streams">{topic.streams.slice(0, 2).map((s) => <Tag key={s}>{s}</Tag>)}</span>
        <span className="kb-card-side">
          <span className={`bk-age ${topic.slaBreached ? 'breached' : ''}`}>{t('topics.age', { days: topic.ageDays })}</span>
          {topic.ownerName && <span className="bk-avatar" aria-hidden="true">{initials(topic.ownerName)}</span>}
        </span>
      </div>
    </div>
  );
}

function MovePopover({ topic, onPick, onClose }: { topic?: TopicSummary; onPick: (b: KanbanBucket) => void; onClose: () => void }) {
  const { t } = useTranslation();
  if (!topic) return null;
  const current = bucketOf(topic.status);
  return (
    <Dialog
      open
      onClose={onClose}
      title={t('kanban.moveTitle')}
      description={`${topic.key} · ${topic.title}`}
      footer={<Button variant="secondary" onClick={onClose}>{t('kanban.cancel')}</Button>}
    >
      <div className="kb-move-list">
        {KANBAN_BUCKETS.map((b) => (
          <button key={b} type="button" className="kb-move-item" aria-current={b === current ? 'true' : undefined} disabled={b === current} onClick={() => onPick(b)}>
            <span className={`kb-dot tone-${BUCKET_TONE[b]}`} aria-hidden="true" />
            {t(`kanban.bucket.${b}`)}
            {b === current && <span className="kb-move-current">{t('kanban.current')}</span>}
          </button>
        ))}
      </div>
    </Dialog>
  );
}

function AcceptDialog({ topic, onClose, onDone }: { topic: TopicSummary; onClose: () => void; onDone: (ownerName: string) => void }) {
  const { t } = useTranslation();
  const { data: members } = useMembers();
  const accept = useAcceptTopic();
  const [ownerId, setOwnerId] = useState('');
  const [err, setErr] = useState<string | null>(null);
  const options = (members ?? []).map((m) => ({ value: m.publicId, label: m.fullName }));
  const submit = () => {
    const owner = members?.find((m) => m.publicId === ownerId);
    if (!owner) { setErr(t('kanban.accept.ownerRequired')); return; }
    accept.mutate(
      { topicId: topic.id, ownerId: owner.publicId, ownerName: owner.fullName },
      { onSuccess: () => onDone(owner.fullName), onError: () => setErr(t('kanban.actionError')) },
    );
  };
  return (
    <Dialog
      open
      onClose={onClose}
      title={t('kanban.accept.title', { key: topic.key })}
      description={t('kanban.accept.body')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('kanban.cancel')}</Button>
          <Button onClick={submit} loading={accept.isPending}>{t('kanban.accept.confirm')}</Button>
        </>
      }
    >
      <Field label={t('kanban.accept.owner')} required error={err ?? undefined}>
        {(p) => <Select {...p} options={options} value={ownerId} onChange={setOwnerId} placeholder={t('kanban.accept.ownerPh')} ariaLabel={t('kanban.accept.owner')} />}
      </Field>
    </Dialog>
  );
}

function ReturnDialog({ topic, onClose, onDone }: { topic: TopicSummary; onClose: () => void; onDone: () => void }) {
  const { t } = useTranslation();
  const ret = useReturnTopic();
  const [mode, setMode] = useState<'defer' | 'reject'>('defer');
  const [reason, setReason] = useState('');
  const [revisitOn, setRevisitOn] = useState('');
  const [err, setErr] = useState<string | null>(null);
  const submit = () => {
    if (!reason.trim()) { setErr(t('kanban.return.reasonRequired')); return; }
    ret.mutate(
      { topicId: topic.id, mode, reason: reason.trim(), revisitOn: mode === 'defer' && revisitOn ? revisitOn : null },
      { onSuccess: onDone, onError: () => setErr(t('kanban.actionError')) },
    );
  };
  return (
    <Dialog
      open
      onClose={onClose}
      tone="warn"
      icon={<Icon name="warnTriangle" size={20} aria-hidden />}
      title={t('kanban.return.title', { key: topic.key })}
      description={t('kanban.return.body')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('kanban.cancel')}</Button>
          <Button variant="danger" onClick={submit} loading={ret.isPending}>{t('kanban.return.confirm')}</Button>
        </>
      }
    >
      <div className="kb-return">
        <fieldset className="kb-return-mode">
          <legend>{t('kanban.return.mode')}</legend>
          <Radio name="return-mode" label={t('kanban.return.defer')} checked={mode === 'defer'} onChange={() => setMode('defer')} />
          <Radio name="return-mode" label={t('kanban.return.reject')} checked={mode === 'reject'} onChange={() => setMode('reject')} />
        </fieldset>
        <Field label={t('kanban.return.reason')} required error={err ?? undefined}>
          {(p) => <Textarea {...p} rows={3} value={reason} onChange={(e) => setReason(e.target.value)} placeholder={t('kanban.return.reasonPh')} />}
        </Field>
        {mode === 'defer' && (
          <Field label={t('kanban.return.revisit')}>
            {(p) => <input {...p} className="input" type="date" value={revisitOn} onChange={(e) => setRevisitOn(e.target.value)} />}
          </Field>
        )}
      </div>
    </Dialog>
  );
}
