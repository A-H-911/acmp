import { useTranslation } from 'react-i18next';
import { EmptyState } from '../components/states';

/*
 * Foundation placeholder for nav areas whose feature screens land in later
 * phases. Renders the area's localized title + the empty state, so the shell,
 * routing, and role gating are fully navigable now without building features.
 *
 * `phase2` marks a genuinely deferred-to-Phase-2 surface (e.g. Diagrams) with the
 * designed Phase-2 state instead of the generic "coming soon" (Usage Map decision 7).
 */
export default function PlaceholderPage({ titleKey, phase2 = false }: { titleKey: string; phase2?: boolean }) {
  const { t } = useTranslation();
  return (
    <section className="page">
      <h1 className="page-title">{t(titleKey)}</h1>
      <p className="page-lead">{t(phase2 ? 'common.phase2Lead' : 'common.comingSoon')}</p>
      <EmptyState
        icon={phase2 ? 'clock' : undefined}
        title={phase2 ? t('common.phase2Title') : undefined}
        body={phase2 ? t('common.phase2Body') : undefined}
      />
    </section>
  );
}
