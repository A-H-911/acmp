import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMySession, openSessionMaterial, type SessionMaterial } from '../../api/session';
import { ApiError } from '../../api/apiClient';
import { Icon } from '../../components/icons';
import { LoadingState, EmptyState } from '../../components/states';
import './session.css';

/*
 * FR-159 / AC-092 / DEC-037 — the GUEST / PRESENTER SHELL, built to
 * "ACMP Navigation & IA.dc.html" lines 304-347 (INV-014): the expiry banner, the topic card, the
 * agenda-slot card, and "Materials for your slot".
 *
 * ONE LINE OF THE REFERENCE IS DELIBERATELY ABSENT — the alt-language topic title (line 320). Topic
 * carries a single Title and there is no bilingual field anywhere in the domain, so the reference asks
 * for data the system has never captured. Recorded as SC-006 against DEC-037 rather than dropped
 * silently; rendering an empty element or repeating the same title under a flipped direction would
 * both tell the reader a translation exists.
 *
 * THE BANNER'S EXPIRY IS THE SERVER'S OWN VALUE. It comes from the same stored column the per-request
 * refusal (ADR-0039) and the hourly sweep read, so the page cannot promise access the API will not
 * honour — DEC-037 requires exactly that, and one column is the only structural guarantee of it.
 */
export default function SessionPage() {
  const { t, i18n } = useTranslation();
  const { data: session, isLoading, error } = useMySession();

  const fmtDateTime = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));
  // 24-hour in BOTH languages, matching the reference's "10:40–10:55" and "١٠:٤٠–١٠:٥٥". Left to the
  // locale, en-US renders "10:40 AM–10:55 AM", which is a different shape from the design's.
  const fmtTime = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(iso));

  // AC-092's second half: once the window closes the API refuses every request, and the page has to
  // SAY so. Renewing the token cannot help, which is why this is a terminal screen and not an error
  // with a retry button.
  if (error instanceof ApiError && error.isAccessEnded) {
    return (
      <section className="page gs-ended">
        <EmptyState
          icon="lock"
          title={t('session.ended.title')}
          body={t('session.ended.body')}
        />
      </section>
    );
  }

  if (isLoading) return <LoadingState label={t('session.loading')} />;

  if (!session) {
    return (
      <section className="page gs-ended">
        <EmptyState icon="calendar" title={t('session.none.title')} body={t('session.none.body')} />
      </section>
    );
  }

  return (
    <section className="gs">
      <div className="gs-banner" role="status">
        <Icon name="lock" size={17} aria-hidden />
        <span className="gs-banner-text">{t('session.banner')}</span>
        {session.accessExpiresAt && (
          <span className="gs-banner-expiry">
            <Icon name="clock" size={13} aria-hidden />
            {t('session.expires', { at: fmtDateTime(session.accessExpiresAt) })}
          </span>
        )}
      </div>

      <div className="gs-body">
        <div className="gs-col">
          <article className="gs-card gs-topic">
            <div className="gs-topic-head">
              <span className="gs-chip">
                <Icon name="lock" size={12} aria-hidden />
                {t('session.presenterChip')}
              </span>
              <span className="gs-key" dir="ltr">{session.topicKey}</span>
            </div>
            <h3 className="gs-title">{session.topicTitle}</h3>
            <div className="gs-summary">
              <div className="gs-label">{t('session.summaryLabel')}</div>
              <p className="gs-summary-text">{session.topicSummary}</p>
            </div>
          </article>

          <article className="gs-card gs-slot">
            <div className="gs-slot-icon" aria-hidden="true">
              <Icon name="calendar" size={20} />
            </div>
            <div className="gs-slot-main">
              <div className="gs-label">{t('session.slotLabel')}</div>
              <div className="gs-slot-meeting">{session.meetingTitle}</div>
              <div className="gs-slot-detail" dir="ltr">
                {session.meetingKey} · {t('session.itemOf', { n: session.itemNumber, of: session.itemCount })}
              </div>
            </div>
            <div className="gs-timebox">
              <Icon name="clock" size={15} aria-hidden />
              {fmtTime(session.slotStart)}–{fmtTime(session.slotEnd)} · {t('session.minutes', { count: session.timeboxMinutes })}
            </div>
          </article>

          <article className="gs-card gs-materials">
            <div className="gs-materials-head">{t('session.materialsLabel')}</div>
            {session.materials.length === 0 ? (
              <p className="gs-materials-empty">{t('session.materialsEmpty')}</p>
            ) : (
              session.materials.map((m) => <MaterialRow key={m.id} material={m} />)
            )}
          </article>
        </div>
      </div>
    </section>
  );
}

/** Rounded to the unit a person reads, not the byte count. */
function formatSize(bytes: number, locale: string): string {
  const units = ['B', 'KB', 'MB', 'GB'];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: unit === 0 ? 0 : 1 }).format(value)} ${units[unit]}`;
}

/** PDFs get the design's danger tile, everything else its info tile — the reference draws exactly two. */
const isPdf = (contentType: string) => contentType.toLowerCase().includes('pdf');

function MaterialRow({ material }: { material: SessionMaterial }) {
  const { t, i18n } = useTranslation();
  const [failed, setFailed] = useState(false);
  const [opening, setOpening] = useState(false);

  async function open() {
    setFailed(false);
    setOpening(true);
    try {
      await openSessionMaterial(material.id);
    } catch {
      // The pre-signed URL is fetched on click, so a refusal surfaces HERE and must be visible: a
      // silent no-op would read as a broken button rather than as access that has ended.
      setFailed(true);
    } finally {
      setOpening(false);
    }
  }

  const meta = `${material.contentType} · ${formatSize(material.sizeBytes, i18n.language)}`;

  return (
    <button type="button" className="gs-material" onClick={open} disabled={opening}>
      <span className={`gs-material-icon ${isPdf(material.contentType) ? 'is-doc' : 'is-other'}`} aria-hidden="true">
        <Icon name={isPdf(material.contentType) ? 'doc' : 'diagram'} size={17} />
      </span>
      <span className="gs-material-main">
        <span className="gs-material-name">{material.fileName}</span>
        <span className="gs-material-meta" dir="ltr">{meta}</span>
        {failed && <span className="gs-material-error" role="alert">{t('session.openFailed')}</span>}
      </span>
      <span className="gs-material-open">
        {t('session.open')}
        <Icon name="chevron" size={14} aria-hidden />
      </span>
    </button>
  );
}
