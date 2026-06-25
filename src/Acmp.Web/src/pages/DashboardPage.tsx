import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AcmpAuthContext';
import { Card } from '../components/ui/Card';

/*
 * Home / role dashboard (docs/14 page 3). The role-tailored widget grid is the
 * Reporting phase; P3 ships the framed landing page so the shell has a real
 * default route.
 */
export default function DashboardPage() {
  const { t } = useTranslation();
  const { displayName } = useAuth();
  return (
    <section className="page">
      <h1 className="page-title">{t('dashboard.title')}</h1>
      <p className="page-lead">{t('dashboard.welcome', { name: displayName })}</p>
      <Card>{t('dashboard.placeholder')}</Card>
    </section>
  );
}
