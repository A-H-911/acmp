import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router-dom';
import { useSessionPreview } from '../../api/sessionPreview';
import { Icon } from '../../components/icons';
import { LoadingState, EmptyState } from '../../components/states';
import { PresenterSessionView } from './PresenterSessionView';
import './session.css';

/*
 * FR-165 / DEC-086 — a Chairman or Secretary previewing a CHOSEN presenter's session view.
 *
 * ⚠⚠ THIS IS A SEPARATE ROUTE, NOT A MODE OF /session, AND THAT IS LAYER 1 OF THREE (DEC-086 d1).
 * /session must stay open to Guests, so its route guard can never refuse one — which means a preview
 * rendered there would have no route-level protection at all. Here the guard admits only the two roles
 * that run the meeting, above a path gate that refuses guests and a query that admits neither guests nor
 * anybody else. The API remains the authority; this is the layer that makes the refusal say what it means
 * (DEF-053's lesson, where a Member typing /session met "you are not presenting" — a true-sounding answer
 * to a question they were not allowed to ask).
 *
 * ⚠ NOT A NAV ITEM, and that is a ruling rather than an omission. OQ-074's resolution (DEC-048 d4) says
 * the preview starts from a chosen presenter, so navModel.ts's ACCESS map stays guest-only and DEF-053's
 * deliberate decision not to touch it still stands. The entry point is the agenda row where the presenter
 * is already named.
 *
 * INV-014: no .dc.html covers a presenter-preview affordance — verified, not assumed; the only "preview"
 * control in "ACMP Agenda & Meeting.dc.html" is a download-glyph button beside Publish, which is the
 * agenda PDF. So the chrome below is a NO-REFERENCE COMPOSITION built from the shared design system, and
 * the presenter's own shell underneath it is the reference-accurate part.
 */
export default function SessionPreviewPage() {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const meetingId = params.get('meetingId') ?? undefined;
  const topicId = params.get('topicId') ?? undefined;

  const { data: session, isLoading } = useSessionPreview(meetingId, topicId);

  const banner = (
    <div className="gs-preview-banner" role="status">
      <Icon name="lock" size={16} aria-hidden />
      <span>{t('sessionPreview.banner')}</span>
    </div>
  );

  // A hand-typed URL with no target is not an error state worth its own screen: it is the same "there is
  // nothing to preview" the server answers with, so it renders the same way rather than inventing a
  // second vocabulary for absence.
  if (!meetingId || !topicId) {
    return (
      <section className="page gs-ended">
        <EmptyState icon="calendar" title={t('sessionPreview.none.title')} body={t('sessionPreview.none.body')} />
      </section>
    );
  }

  if (isLoading) return <LoadingState label={t('sessionPreview.loading')} />;

  // 204 from the server: no presenter on that slot, a cancelled meeting, or an agenda item that no longer
  // exists. All three are exactly what the PRESENTER would see, which is the parity FR-165 requires — a
  // preview that showed more than its subject would get is not a preview.
  if (!session) {
    return (
      <section className="page gs-ended">
        {banner}
        <EmptyState icon="calendar" title={t('sessionPreview.none.title')} body={t('sessionPreview.none.body')} />
      </section>
    );
  }

  return (
    <section className="gs-preview">
      {banner}
      <Link className="gs-preview-back" to="/meetings">
        <Icon name="chevron" size={14} aria-hidden />
        {t('sessionPreview.back')}
      </Link>
      {/* readOnlyMaterials — DEC-086 d2: listed, never openable. */}
      <PresenterSessionView session={session} readOnlyMaterials />
    </section>
  );
}
