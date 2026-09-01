/*
 * Backlog calendar view — FR-035 / DW-037 (WBS-24.2). The design's month-grid chrome
 * (month nav, locale weekday header, day cells, today ring, legend) now carries REAL
 * markers: one chip per meeting on its scheduled date, matching the reference's day-cell
 * event shape (a dot, an ellipsised label, the whole chip clickable).
 *
 * ⚠ THE DATE COMES FROM THE MEETINGS API, NOT THE TOPICS API, AND DW-037 SAYS SO IN CAPITALS
 * BECAUSE THE OBVIOUS PLACE IS WRONG. Topic.Schedule(meetingId, …) does NOT persist the
 * meeting id — it transitions status and raises TopicScheduledEvent, which has ZERO consumers
 * outside Topics.Domain, and the Topic aggregate has no MeetingId column. The pairing that
 * exists is MeetingSummary.scheduledStart + AgendaItem.topicId. Do not go looking for a
 * scheduled date on the topic read model; there is none.
 *
 * WHY THE TOPIC TITLES LOAD ON DEMAND. /meetings returns summaries with scheduledStart and
 * itemCount but NO topic ids — only /meetings/{key} carries agenda items. Rendering every
 * topic in the grid would therefore mean one detail request per meeting per month, and the
 * design's cells hold two or three chips before they overflow anyway. So the grid answers
 * "how are topics spread across upcoming meetings" from ONE request via the per-meeting
 * count, and selecting a meeting fetches that ONE meeting's topics.
 * ponytail: bounded to the selected meeting; add a dedicated endpoint only if the grid ever
 * needs every topic title at once (DW-086 records that residual).
 *
 * Gregorian throughout, localized via Intl.
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Icon } from '../../components/icons';
import {
  useAgendaProjection,
  useMeetingDetail,
  useMeetings,
  type MeetingSummary,
} from '../../api/meetings';

const WEEKS = 6; // 6 rows × 7 = 42 cells covers any month

/** Local-time Y-M-D key. Deliberately NOT toISOString(), which shifts to UTC and can move a
 *  late-evening meeting onto the next day for anyone east of Greenwich. */
function dayKey(d: Date) {
  return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`;
}

export function Calendar() {
  const { t, i18n } = useTranslation();
  const [offset, setOffset] = useState(0); // months relative to the current month
  const [selected, setSelected] = useState<MeetingSummary | null>(null);

  const { data: meetings } = useMeetings();
  const { data: detail, isLoading: detailLoading } = useMeetingDetail(selected?.key);

  const now = new Date();
  const month = new Date(now.getFullYear(), now.getMonth() + offset, 1);
  const year = month.getFullYear();
  const m = month.getMonth();

  /*
   * WBS-26.5 / DW-086 — ONE request for the whole visible month, never one per meeting.
   *
   * The range is exactly the month being rendered, so paging the chevrons refetches rather than
   * accumulating. `to` is EXCLUSIVE (the first instant of the next month), matching the server.
   */
  const rangeFrom = new Date(year, m, 1).toISOString();
  const rangeTo = new Date(year, m + 1, 1).toISOString();
  const { data: projection } = useAgendaProjection(rangeFrom, rangeTo);
  const topicsByMeeting = new Map((projection ?? []).map((p) => [p.meetingId, p.items]));
  const startDow = new Date(year, m, 1).getDay(); // 0 = Sunday
  const daysInMonth = new Date(year, m + 1, 0).getDate();
  const isThisMonth = offset === 0;

  const monthLabel = new Intl.DateTimeFormat(i18n.language, { month: 'long', year: 'numeric' }).format(month);
  const weekdayFmt = new Intl.DateTimeFormat(i18n.language, { weekday: 'short' });
  // 2024-09-01 is a Sunday → seed locale-aware short weekday names, Sunday-first (Gregorian).
  const weekdays = Array.from({ length: 7 }, (_, i) => weekdayFmt.format(new Date(2024, 8, 1 + i)));

  const byDay = new Map<string, MeetingSummary[]>();
  for (const mt of meetings ?? []) {
    const d = new Date(mt.scheduledStart);
    if (Number.isNaN(d.getTime())) continue; // a malformed date must not take the grid down
    const k = dayKey(d);
    byDay.set(k, [...(byDay.get(k) ?? []), mt]);
  }

  const cells = Array.from({ length: WEEKS * 7 }, (_, i) => {
    const dayNum = i - startDow + 1;
    const inMonth = dayNum >= 1 && dayNum <= daysInMonth;
    return {
      key: i,
      day: inMonth ? dayNum : null,
      isToday: inMonth && isThisMonth && dayNum === now.getDate(),
      events: inMonth ? (byDay.get(dayKey(new Date(year, m, dayNum))) ?? []) : [],
    };
  });

  const monthMeetings = cells.reduce((n, c) => n + c.events.length, 0);

  return (
    <div className="cal">
      <div className="cal-bar">
        <div className="cal-nav">
          <button type="button" className="cal-navbtn cal-prev" aria-label={t('topics.calendar.prev')} onClick={() => setOffset((o) => o - 1)}>
            <Icon name="chevron" size={14} aria-hidden />
          </button>
          <span className="cal-month">{monthLabel}</span>
          <button type="button" className="cal-navbtn cal-next" aria-label={t('topics.calendar.next')} onClick={() => setOffset((o) => o + 1)}>
            <Icon name="chevron" size={14} aria-hidden />
          </button>
        </div>
        <div className="cal-legend">
          <span className="cal-leg"><span className="cal-leg-dot sched" aria-hidden="true" />{t('topics.calendar.scheduled')}</span>
          <span className="cal-leg"><span className="cal-leg-dot due" aria-hidden="true" />{t('topics.calendar.due')}</span>
        </div>
      </div>
      <div className="cal-weekdays" aria-hidden="true">
        {weekdays.map((w, i) => <span key={i} className="cal-weekday">{w}</span>)}
      </div>
      <div className="cal-grid">
        {cells.map((c) => (
          <div key={c.key} className={`cal-cell ${c.day === null ? 'out' : ''}`}>
            {c.day !== null && <span className={`cal-day ${c.isToday ? 'today' : ''}`}>{c.day}</span>}
            {c.events.map((e) => (
              <button
                key={e.id}
                type="button"
                className={`cal-event ${selected?.id === e.id ? 'is-selected' : ''}`}
                aria-pressed={selected?.id === e.id}
                onClick={() => setSelected((s) => (s?.id === e.id ? null : e))}
              >
                <span className="cal-event-dot" aria-hidden="true" />
                <span className="cal-event-label">{e.title}</span>
                {/* A bare numeral, deliberately: this codebase has NO plural keys anywhere, and
                    count-noun agreement is a six-form problem in Arabic (DEC-032 — morphology is a
                    rule, not a substitution). A labelled numeral needs no agreement in either
                    language, and the label carries the meaning for a screen reader. */}
                <span className="cal-event-count">
                  <span className="visually-hidden">{t('topics.calendar.agendaItems')} </span>
                  {e.itemCount}
                </span>
              </button>
            ))}
            {/*
              WBS-26.5 / DEC-108 d1 / DEC-109 d3 — THE TOPIC CHIPS, KEYED AND NESTED.

              THE LABEL IS THE TOPIC KEY, not its title: `ACMP Backlog & Topic.dc.html` draws day-cell
              chips as `TOP-009`, and INV-014 makes the reference the visual source of truth. A full
              title does not fit a 10.5px chip anyway.

              THEY NEST UNDER THE MEETING CHIP RATHER THAN REPLACING IT. AC-145 is Met and immutable,
              and its Then clause requires the meeting chip carrying title and topic count — removing
              it would falsify a criterion that can never afterwards be corrected. The reference draws
              one chip per cell because it assumes topics carry their own dates; ACMP topics do not
              (Topic.Schedule raises an event with zero consumers), so a topic lands on its meeting's
              day and one-chip-per-cell is unreachable from this data model. DEC-109 d3 is the record.

              AN EMPTY KEY IS A RESTRICTED TOPIC. The server redacts key and title to empty rather
              than sending an English word, so the localized placeholder is rendered here — and it is
              NOT a link, because there is nothing the caller may open.
            */}
            {c.events.flatMap((e) =>
              (topicsByMeeting.get(e.id) ?? []).map((it) =>
                it.topicKey === '' ? (
                  <span key={it.topicId} className="cal-topic is-restricted">
                    {t('topics.calendar.restrictedTopic')}
                  </span>
                ) : (
                  <Link
                    key={it.topicId}
                    className="cal-topic"
                    to={`/topics/${it.topicKey}`}
                    title={it.topicTitle}
                  >
                    {it.topicKey}
                  </Link>
                ),
              ),
            )}
          </div>
        ))}
      </div>

      {/* The grid answers "how are topics spread" from the counts; this panel answers "which
          topics" for one meeting, which is the only place the API can supply them. */}
      {selected && (
        <div className="cal-detail">
          <div className="cal-detail-head">
            <span className="cal-detail-title">{selected.title}</span>
            <Link className="cal-detail-link" to={`/meetings/${selected.key}`}>{t('topics.calendar.openMeeting')}</Link>
          </div>
          {detailLoading ? (
            <p className="cal-detail-empty">{t('common.loading')}</p>
          ) : (detail?.agenda?.items.length ?? 0) === 0 ? (
            <p className="cal-detail-empty">{t('topics.calendar.noTopics')}</p>
          ) : (
            <ul className="cal-detail-list">
              {detail!.agenda!.items.map((it) => (
                <li key={it.topicId}>
                  <Link className="cal-detail-topic" to={`/topics/${it.topicKey}`}>
                    <span className="bk-key">{it.topicKey}</span>
                    <span className="cal-detail-topic-title">{it.topicTitle}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {/* Honest note, kept and narrowed rather than dropped. The scheduled marker now has real
          data; the DUE marker in the design's legend still has none, because topics carry no
          due date at all (FR-036 / DW-001, deferred). Saying so beats a legend entry that
          silently never appears. */}
      <p className="bk-view-note" role="note">
        <Icon name="infoCircle" size={14} aria-hidden />
        {monthMeetings === 0 ? t('topics.calendar.noneThisMonth') : t('topics.calendar.dueNote')}
      </p>
    </div>
  );
}
