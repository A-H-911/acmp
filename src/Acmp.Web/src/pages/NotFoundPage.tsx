import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '../components/ui/Button';

/** 404 catch-all (docs/14 page 90). */
export function NotFoundPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <div className="page">
      <div className="state">
        <p className="state-title" style={{ fontSize: 28 }}>404</p>
        <p className="state-body">{t('notFound.body')}</p>
        <div className="state-actions">
          <Button onClick={() => navigate('/dashboard')}>{t('common.goToDashboard')}</Button>
        </div>
      </div>
    </div>
  );
}
