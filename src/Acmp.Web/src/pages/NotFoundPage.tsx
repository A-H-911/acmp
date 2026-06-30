import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '../components/ui/Button';

/** 404 catch-all — reconciled to ACMP System States `404` (docs/14 page 90). */
export function NotFoundPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <div className="page">
      <div className="state">
        <div className="state-code">404</div>
        <p className="state-title state-title-lg">{t('notFound.title')}</p>
        <p className="state-body">{t('notFound.body')}</p>
        <div className="state-actions">
          <Button onClick={() => navigate('/')}>{t('common.goToDashboard')}</Button>
          <Button variant="secondary" onClick={() => navigate('/search')}>{t('common.search')}</Button>
        </div>
      </div>
    </div>
  );
}
