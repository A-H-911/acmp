import { useTranslation } from 'react-i18next';
import { EmptyState } from '../components/states';

/*
 * Foundation placeholder for nav areas whose feature screens land in later
 * phases. Renders the area's localized title + the empty state, so the shell,
 * routing, and role gating are fully navigable now without building features.
 *
 * `deferred` marks a surface the product has decided NOT to build, and says so plainly.
 *
 * This flag used to be `phase2` and promised "Coming in Phase 2" — copy written under Usage Map
 * decision 7, BEFORE DEC-028 (2026-07-17) deferred P14 (the Tarseem sidecar and the Diagrams
 * surface) INDEFINITELY and removed it from the ladder. The page therefore kept advertising a
 * commitment the governance record had already retracted. Renamed rather than reworded so the
 * next reader cannot restore the Phase-2 promise by "fixing" a string back.
 *
 * Only /diagrams uses it. If a surface is genuinely coming later, use the generic `comingSoon`
 * lead instead — do not name a phase the roadmap has not committed to.
 */
export default function PlaceholderPage({ titleKey, deferred = false }: { titleKey: string; deferred?: boolean }) {
  const { t } = useTranslation();
  return (
    <section className="page">
      <h1 className="page-title">{t(titleKey)}</h1>
      <p className="page-lead">{t(deferred ? 'common.deferredLead' : 'common.comingSoon')}</p>
      <EmptyState
        icon={deferred ? 'clock' : undefined}
        title={deferred ? t('common.deferredTitle') : undefined}
        body={deferred ? t('common.deferredBody') : undefined}
      />
    </section>
  );
}
