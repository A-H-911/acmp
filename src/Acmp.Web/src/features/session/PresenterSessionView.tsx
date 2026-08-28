import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatBytes } from '../../lib/numberFmt';
import { openSessionMaterial, type PresenterSession, type SessionMaterial } from '../../api/session';
import { Icon } from '../../components/icons';
import './session.css';

/*
 * FR-159 / FR-165 — the GUEST / PRESENTER SHELL itself, rendered identically by the presenter's own
 * /session page and by the Chairman/Secretary preview of somebody else's slot (DEC-086).
 *
 * ⚠ SHARED FOR THE SAME REASON THE SERVER-SIDE COMPOSER IS: a preview that can disagree with the thing it
 * previews is worse than no preview, and two copies of this markup would drift on the first change to
 * either — silently, because nothing compares them. Extracting it makes the agreement structural instead
 * of something a reviewer has to notice.
 *
 * Built to "ACMP Navigation & IA.dc.html" lines 304-347 (INV-014): the expiry banner, the topic card, the
 * agenda-slot card, and "Materials for your slot". ONE LINE OF THE REFERENCE IS DELIBERATELY ABSENT — the
 * alt-language topic title (line 320) — because Topic carries a single Title and no bilingual field
 * exists anywhere in the domain. Recorded as SC-006 against DEC-037 rather than dropped silently.
 */
export function PresenterSessionView({
  session,
  readOnlyMaterials = false,
}: {
  session: PresenterSession;
  /**
   * DEC-086 d2 — in a PREVIEW the materials are listed and not openable.
   *
   * Not a styling choice: opening one needs a second pre-signed-URL path targeted at another person's
   * content, which is the largest piece of new authorization surface this feature could have added, and
   * the operator ruled it out of scope. The committee-side attachment-retrieval gap it leaves visible is
   * carried by DW-088 rather than being papered over here.
   */
  readOnlyMaterials?: boolean;
}) {
  const { t, i18n } = useTranslation();

  const fmtDateTime = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso));
  // 24-hour in BOTH languages, matching the reference's "10:40–10:55" and "١٠:٤٠–١٠:٥٥". Left to the
  // locale, en-US renders "10:40 AM–10:55 AM", which is a different shape from the design's.
  const fmtTime = (iso: string) =>
    new Intl.DateTimeFormat(i18n.language, { hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(iso));

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
              session.materials.map((m) =>
                readOnlyMaterials
                  ? <MaterialListing key={m.id} material={m} />
                  : <MaterialRow key={m.id} material={m} />,
              )
            )}
            {readOnlyMaterials && session.materials.length > 0 && (
              <p className="gs-materials-empty">{t('sessionPreview.materialsReadOnly')}</p>
            )}
          </article>
        </div>
      </div>
    </section>
  );
}

/** PDFs get the design's danger tile, everything else its info tile — the reference draws exactly two. */
const isPdf = (contentType: string) => contentType.toLowerCase().includes('pdf');

/**
 * The tile itself, shared by both variants.
 *
 * ⚠ `error` IS RENDERED INSIDE `.gs-material-main`, WHERE IT HAS ALWAYS LIVED. Hoisting it a level out
 * would compile, pass every assertion that only looks for the text, and quietly restyle the failure
 * message — the class of defect jsdom structurally cannot see (trap 5).
 */
function MaterialMeta({ material, error }: { material: SessionMaterial; error?: React.ReactNode }) {
  const { i18n } = useTranslation();
  return (
    <>
      <span className={`gs-material-icon ${isPdf(material.contentType) ? 'is-doc' : 'is-other'}`} aria-hidden="true">
        <Icon name={isPdf(material.contentType) ? 'doc' : 'diagram'} size={17} />
      </span>
      <span className="gs-material-main">
        <span className="gs-material-name">{material.fileName}</span>
        <span className="gs-material-meta" dir="ltr">
          {material.contentType} · {formatBytes(material.sizeBytes, i18n.language)}
        </span>
        {error}
      </span>
    </>
  );
}

/**
 * The preview's material row: the same tile, WITHOUT an open control.
 *
 * ⚠ A LIST ITEM RATHER THAN A DISABLED BUTTON, deliberately. TopicDetail already ships a permanently
 * disabled download button with a "coming soon" tooltip for the committee-side gap (DW-088), and
 * repeating that pattern on a brand-new surface would spread a knowingly-inert control instead of
 * containing it. Nothing here promises an action it cannot perform.
 */
function MaterialListing({ material }: { material: SessionMaterial }) {
  return (
    // `is-listing` removes the pointer cursor and hover that .gs-material carries for its <button>
    // form. Without it this renders an affordance that lies — it looks clickable and does nothing.
    <div className="gs-material is-listing">
      <MaterialMeta material={material} />
    </div>
  );
}

function MaterialRow({ material }: { material: SessionMaterial }) {
  const { t } = useTranslation();
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

  return (
    <button type="button" className="gs-material" onClick={open} disabled={opening}>
      <MaterialMeta
        material={material}
        error={failed ? <span className="gs-material-error" role="alert">{t('session.openFailed')}</span> : undefined}
      />
      <span className="gs-material-open">
        {t('session.open')}
        <Icon name="chevron" size={14} aria-hidden />
      </span>
    </button>
  );
}
