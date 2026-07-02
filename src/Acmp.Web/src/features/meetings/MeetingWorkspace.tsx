/*
 * Live meeting workspace (P6d) — THE design screen ("ACMP Agenda & Meeting.dc.html", the
 * isMeeting block). Rendered inside MeetingPage's "Meeting" tab while the meeting is
 * InProgress. Composes the shared library (Button, Select, StatusChip, Icon, states).
 * Reads the meeting by key (cached by MeetingPage); the live mutations are by meeting id.
 *
 * Design↔behavior reconciliations (visual SoT = design; behavior SoT = the package):
 *  - The page breadcrumb + the Agenda/Meeting tab switcher live in MeetingPage (the design
 *    puts the tabs in the top bar; we use a shared in-page Tabs control). This component only
 *    renders the live workspace header + 3-column grid.
 *  - The design's Pause button is mock chrome → rendered disabled (coming soon).
 *  - Discussion notes: the design editor (toolbar + content box) is rendered; the toolbar
 *    buttons insert markdown into the plain-text body (no backend change). Autosave-on-blur
 *    POSTs /discussion and shows the "Autosaved" indicator (no explicit Save button).
 *  - "End → Minutes" ends the meeting (POST /end); the Minutes screen itself is P7, so on
 *    success we navigate back to the meetings list (no minutes UI here).
 *  - Record decision / Create action / Call vote are disabled stubs → P7 / P8 / P9.
 *  - "Captured on this item" renders with an honest empty state; it populates once
 *    Decisions/Actions ship (P7/P8). The inline quick-create is omitted.
 *  - Actual-time / outcome recording (DV-16): a minutes input + outcome select + "Record time"
 *    button on the active item, wired to useRecordActualTime (POST …/actual-time).
 *  - Attendance roster = active members (GET /api/members) merged with meeting.attendance by
 *    member.publicId (the attendance userId). Quorum is a client-side DISPLAY heuristic only;
 *    the authoritative quorum gate is the Voting phase (P9).
 *  - Elapsed timer is derived from startedAt via a 1s interval. Meeting/topic titles are
 *    single-language user content; only chrome is i18n'd (guardrail 9).
 */
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  useMeetingDetail,
  useEndMeeting,
  useMarkAttendance,
  useCaptureDiscussion,
  useRecordActualTime,
  type AgendaItem,
  type Discussion,
} from '../../api/meetings';
import { useMembers, type Member } from '../../api/members';
import { useAuth, hasRole } from '../../auth/AcmpAuthContext';
import { CallVoteDialog } from '../voting/CallVoteDialog';
import { AREAS } from '../../nav/navModel';
import { Button } from '../../components/ui/Button';
import { Select } from '../../components/ui/Select';
import { MarkdownEditor } from '../../components/ui/MarkdownEditor';
import { StatusChip } from '../../components/ui/StatusChip';
import { EmptyState } from '../../components/states';
import { Icon } from '../../components/icons';
import './meetings.css';

// Per-item outcomes a recorder can set (backend AgendaItemOutcome; Pending is the unset default).
const OUTCOMES = ['Discussed', 'Deferred', 'CarriedOver'] as const;

/** Map a committee role onto the meeting's AttendanceRole vocabulary. */
function toAttendanceRole(role: string): 'Chair' | 'Secretary' | 'Member' | 'Reviewer' | 'Guest' {
  switch (role) {
    case 'Chairman':
      return 'Chair';
    case 'Secretary':
      return 'Secretary';
    case 'Member':
      return 'Member';
    case 'Reviewer':
      return 'Reviewer';
    default:
      return 'Guest';
  }
}

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/);
  return (((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase()) || '?';
}

function formatElapsed(ms: number): string {
  const total = Math.max(0, Math.floor(ms / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
}

export function MeetingWorkspace() {
  const { key } = useParams();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const auth = useAuth();
  const meetingQuery = useMeetingDetail(key);
  const membersQuery = useMembers();
  const endMeeting = useEndMeeting(key);
  const markAttendance = useMarkAttendance(key);
  const captureDiscussion = useCaptureDiscussion(key);
  const recordActualTime = useRecordActualTime(key);

  const [activeTopicId, setActiveTopicId] = useState<string | null>(null);
  // ponytail: a real wall-clock tick is genuine external sync, not derived state — keep the interval.
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);

  const meeting = meetingQuery.data;
  if (!meeting) return null; // MeetingPage owns loading/error; this guards the type.

  const items = meeting.agenda?.items ?? [];
  // Default to the first item still Pending (the one "running"), else the first item.
  const activeItem =
    items.find((i) => i.topicId === activeTopicId) ?? items.find((i) => i.outcome === 'Pending') ?? items[0];
  const elapsedMs = meeting.startedAt ? now - new Date(meeting.startedAt).getTime() : 0;

  const activeMembers = (membersQuery.data ?? []).filter((m) => m.isActive);
  const roster = activeMembers.map((m) => ({
    member: m,
    present: meeting.attendance.find((a) => a.userId === m.publicId)?.status === 'Present',
  }));
  const presentCount = roster.filter((r) => r.present).length;
  // ponytail: client-side display heuristic (majority of voting-eligible present). The
  // authoritative quorum gate is the Voting phase (P9) — don't gate any action on this.
  const votingEligible = activeMembers.filter((m) => m.isVotingEligible);
  const presentVoting = roster.filter((r) => r.present && r.member.isVotingEligible).length;
  const quorumNeeded = votingEligible.length > 0 ? Math.floor(votingEligible.length / 2) + 1 : 0;
  const quorumMet = votingEligible.length > 0 && presentVoting >= quorumNeeded;

  const onEnd = () => endMeeting.mutate({ meetingId: meeting.id }, { onSuccess: () => navigate(AREAS.agenda.path) });

  const onToggleAttendance = (member: Member, present: boolean) =>
    markAttendance.mutate({
      meetingId: meeting.id,
      userId: member.publicId,
      name: member.fullName,
      role: toAttendanceRole(member.role),
      status: present ? 'Absent' : 'Present',
      isVotingEligible: member.isVotingEligible,
    });

  return (
    <div className="mt-ws">
      <header className="mt-ws-head">
        <div className="mt-ws-titlerow">
          <h1 className="page-title">{meeting.title}</h1>
          <span className="mt-live">
            <StatusChip tone="danger" label={t('meetings.live')} />
          </span>
        </div>
        <div className="mt-ws-controls">
          <div className="mt-ws-elapsed">
            <span className="mt-ws-elapsed-label">{t('meetings.elapsed')}</span>
            <span className="mt-ws-elapsed-val" role="timer" aria-label={t('meetings.elapsed')}>
              {formatElapsed(elapsedMs)}
            </span>
          </div>
          <span className="mt-ws-divider" aria-hidden="true" />
          <Button variant="secondary" disabled title={t('meetings.comingSoon')}>
            {t('meetings.pause')}
          </Button>
          <Button onClick={onEnd} loading={endMeeting.isPending}>
            {t('meetings.endToMinutes')}
          </Button>
        </div>
      </header>

      <div className="mt-ws-grid">
        <AgendaSpine items={items} activeTopicId={activeItem?.topicId} onSelect={setActiveTopicId} />

        {activeItem ? (
          <ActiveItem
            key={activeItem.topicId}
            item={activeItem}
            meetingId={meeting.id}
            canManageVote={hasRole(auth, 'chairman', 'secretary')}
            index={items.findIndex((i) => i.topicId === activeItem.topicId) + 1}
            discussion={meeting.discussions.find((d) => d.topicId === activeItem.topicId)}
            onSaveNote={(body) => captureDiscussion.mutate({ meetingId: meeting.id, topicId: activeItem.topicId, body })}
            onRecordTime={(actualMinutes, outcome) =>
              // Omit outcome when unset: '' is not a valid AgendaItemOutcome, so the time-only path
              // must send no outcome field (server keeps the item's current/Pending outcome).
              recordActualTime.mutate({ meetingId: meeting.id, topicId: activeItem.topicId, actualMinutes, outcome: outcome || undefined })
            }
            recording={recordActualTime.isPending}
          />
        ) : (
          <section className="mt-active" aria-label={t('meetings.agendaSpine')}>
            <EmptyState icon="viewKanban" title={t('meetings.agendaEmpty.title')} body={t('meetings.agendaEmpty.body')} />
          </section>
        )}

        <AttendancePanel
          roster={roster}
          presentCount={presentCount}
          total={roster.length}
          needed={quorumNeeded}
          quorumMet={quorumMet}
          marking={markAttendance.isPending}
          onToggle={onToggleAttendance}
        />
      </div>
    </div>
  );
}

function AgendaSpine({
  items,
  activeTopicId,
  onSelect,
}: {
  items: AgendaItem[];
  activeTopicId: string | undefined;
  onSelect: (topicId: string) => void;
}) {
  const { t } = useTranslation();
  return (
    <nav className="mt-spine" aria-label={t('meetings.agendaSpine')}>
      <div className="mt-spine-head">{t('meetings.agendaSpine')}</div>
      <ol className="mt-spine-list">
        {items.map((item, i) => {
          const done = item.outcome !== 'Pending';
          const active = item.topicId === activeTopicId;
          return (
            <li key={item.topicId}>
              <button
                type="button"
                className={`mt-spine-item ${active ? 'active' : ''} ${done ? 'done' : ''}`}
                aria-current={active ? 'true' : undefined}
                onClick={() => onSelect(item.topicId)}
              >
                <span className={`mt-spine-num ${done ? 'done' : ''}`}>
                  {done ? <Icon name="check" size={12} aria-hidden /> : i + 1}
                </span>
                <span className="mt-spine-body">
                  <span className="mt-spine-title">{item.topicTitle}</span>
                  <span className="mt-spine-meta">
                    <Icon name="clock" size={11} aria-hidden /> {t('meetings.minShort', { count: item.timeboxMinutes })}
                    {active && <span className="mt-spine-running">· {t('meetings.running')}</span>}
                  </span>
                </span>
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

function ActiveItem({
  item,
  meetingId,
  canManageVote,
  index,
  discussion,
  onSaveNote,
  onRecordTime,
  recording,
}: {
  item: AgendaItem;
  meetingId: string;
  canManageVote: boolean;
  index: number;
  discussion: Discussion | undefined;
  onSaveNote: (body: string) => void;
  onRecordTime: (actualMinutes: number, outcome: string) => void;
  recording: boolean;
}) {
  const { t } = useTranslation();
  const [voteOpen, setVoteOpen] = useState(false);
  return (
    <section className="mt-active" aria-label={item.topicTitle}>
      <div className="mt-active-card">
        <div className="mt-active-head">
          <div className="mt-active-headmain">
            <div className="mt-active-keyrow">
              <span className="mt-active-index" aria-hidden="true">{index}</span>
              <span className="mt-key">{item.topicKey}</span>
              {item.urgent && <span className="mt-urgent-pill">{t('meetings.urgent')}</span>}
            </div>
            <h2 className="mt-active-title">{item.topicTitle}</h2>
          </div>
          <div className="mt-active-time">
            <span className="mt-active-time-label">{t('meetings.itemTime')}</span>
            <span className="mt-active-time-val">
              {t('meetings.itemTimeValue', { actual: item.actualMinutes, total: item.timeboxMinutes })}
            </span>
          </div>
        </div>

        <div className="mt-active-body">
          <DiscussionNote initialBody={discussion?.body ?? ''} onSave={onSaveNote} />
          {/* DV-16: actual-time + outcome recording, wired to useRecordActualTime. Re-added after
              round-1 deferred it; the backend command (POST …/actual-time) was already in place. */}
          <ActualTimeControl item={item} onRecord={onRecordTime} busy={recording} />
          {/* Design action row = the 3 capture buttons (Decisions/Actions/Voting → P7/P8/P9). */}
          <div className="mt-actions">
            <Button variant="secondary" disabled title={t('meetings.comingSoon')}>
              <Icon name="decision" size={14} aria-hidden /> {t('meetings.recordDecision')}
            </Button>
            <Button variant="secondary" disabled title={t('meetings.comingSoon')}>
              <Icon name="action" size={14} aria-hidden /> {t('meetings.createAction')}
            </Button>
            <Button
              variant="secondary"
              disabled={!canManageVote}
              title={canManageVote ? undefined : t('voting.openVotingNote')}
              onClick={() => setVoteOpen(true)}
            >
              <Icon name="audit" size={14} aria-hidden /> {t('meetings.callVote')}
            </Button>
          </div>
        </div>
      </div>

      <CapturedOnItem />
      <CallVoteDialog
        open={voteOpen}
        onClose={() => setVoteOpen(false)}
        source={{ topicId: item.topicId, topicKey: item.topicKey, meetingId }}
      />
    </section>
  );
}

/* DV-16 — actual-time + outcome recorder (POST …/agenda/items/{topicId}/actual-time). Local state
 * only; remounts per item (ActiveItem is keyed by topicId), so it seeds from the item each time.
 * Minutes is a bounded number input; outcome maps to AgendaItemOutcome (Pending = unset). */
function ActualTimeControl({
  item,
  onRecord,
  busy,
}: {
  item: AgendaItem;
  onRecord: (actualMinutes: number, outcome: string) => void;
  busy: boolean;
}) {
  const { t } = useTranslation();
  const [minutes, setMinutes] = useState(item.actualMinutes > 0 ? String(item.actualMinutes) : '');
  const [outcome, setOutcome] = useState((OUTCOMES as readonly string[]).includes(item.outcome) ? item.outcome : '');
  const parsed = Number(minutes);
  const valid = minutes.trim() !== '' && Number.isFinite(parsed) && parsed >= 0;

  return (
    <div className="mt-record">
      <label className="mt-record-field">
        <span className="mt-record-label">{t('meetings.actualTime')}</span>
        <span className="mt-record-minutes">
          <input
            type="number"
            min={0}
            inputMode="numeric"
            className="mt-record-input"
            aria-label={t('meetings.actualTime')}
            value={minutes}
            onChange={(e) => setMinutes(e.target.value)}
          />
          <span className="mt-record-unit">{t('meetings.minutesUnit')}</span>
        </span>
      </label>
      <label className="mt-record-field">
        <span className="mt-record-label">{t('meetings.outcomeLabel')}</span>
        <Select
          ariaLabel={t('meetings.outcomeLabel')}
          placeholder={t('meetings.outcomePick')}
          value={outcome}
          onChange={setOutcome}
          options={OUTCOMES.map((o) => ({ value: o, label: t(`meetings.outcome.${o}`) }))}
        />
      </label>
      <Button variant="secondary" disabled={!valid || busy} loading={busy} onClick={() => onRecord(parsed, outcome)}>
        <Icon name="clock" size={14} aria-hidden /> {t('meetings.recordTime')}
      </Button>
    </div>
  );
}

/** "Captured on this item" card (design block). Decisions/Actions land here once those
 *  modules ship (P7/P8); until then it renders an honest empty state to match the layout. */
function CapturedOnItem() {
  const { t } = useTranslation();
  return (
    <div className="mt-captured">
      <div className="mt-captured-title">{t('meetings.captured.title')}</div>
      <p className="mt-captured-empty">{t('meetings.captured.empty')}</p>
    </div>
  );
}

function DiscussionNote({ initialBody, onSave }: { initialBody: string; onSave: (body: string) => void }) {
  const { t } = useTranslation();
  const [body, setBody] = useState(initialBody);
  const [saved, setSaved] = useState(initialBody.trim().length > 0);
  const trimmed = body.trim();
  // Autosave on blur. Skip if empty (server rejects) or unchanged from the loaded value.
  const dirty = trimmed.length > 0 && trimmed !== initialBody.trim();

  const save = () => {
    if (!dirty) return;
    onSave(trimmed);
    setSaved(true);
  };

  return (
    <div className="mt-note">
      <div className="mt-note-head">
        <Icon name="doc" size={15} aria-hidden />
        <span className="mt-note-label">{t('meetings.discussionNotes')}</span>
        {saved && (
          <span className="mt-note-saved">
            <span className="mt-note-dot" aria-hidden="true" />
            {t('meetings.autosaved')}
          </span>
        )}
      </div>
      <MarkdownEditor
        value={body}
        onChange={setBody}
        onBlur={save}
        ariaLabel={t('meetings.discussionNotes')}
        placeholder={t('meetings.notePlaceholder')}
      />
    </div>
  );
}

function AttendancePanel({
  roster,
  presentCount,
  total,
  needed,
  quorumMet,
  marking,
  onToggle,
}: {
  roster: { member: Member; present: boolean }[];
  presentCount: number;
  total: number;
  needed: number;
  quorumMet: boolean;
  marking: boolean;
  onToggle: (member: Member, present: boolean) => void;
}) {
  const { t } = useTranslation();
  return (
    <aside className="mt-att" aria-label={t('meetings.attendance')}>
      <div className="mt-att-head">
        <div className="mt-att-headrow">
          <h2 className="mt-col-title">{t('meetings.attendance')}</h2>
          <StatusChip tone={quorumMet ? 'success' : 'neutral'} label={t(quorumMet ? 'meetings.quorum.met' : 'meetings.quorum.notMet')} />
        </div>
        <div className="mt-att-summary">{t('meetings.attendanceSummary', { present: presentCount, total, needed })}</div>
      </div>
      <ul className="mt-att-list">
        {roster.map(({ member, present }) => (
          <li key={member.publicId} className="mt-att-row">
            <span className={`mt-avatar ${present ? 'present' : ''}`} aria-hidden="true">
              {initialsOf(member.fullName)}
              {present && <span className="mt-avatar-dot" />}
            </span>
            <span className="mt-att-id">
              <span className="mt-att-name">{member.fullName}</span>
              <span className="mt-att-role">{t(`meetings.attendanceRole.${toAttendanceRole(member.role)}`)}</span>
            </span>
            <button
              type="button"
              className={`mt-att-toggle ${present ? 'present' : 'absent'}`}
              aria-pressed={present}
              aria-label={t('meetings.attendanceToggle', { name: member.fullName })}
              disabled={marking}
              onClick={() => onToggle(member, present)}
            >
              {t(present ? 'meetings.present' : 'meetings.absent')}
            </button>
          </li>
        ))}
      </ul>
    </aside>
  );
}
