/*
 * App-level error boundary. React has no functional equivalent, so this stays a
 * class component. It shows a user-friendly state with no technical detail
 * (reconciled to ACMP System States `error`, docs/14 page 92) and recovery
 * actions; the raw error is logged to the console for diagnostics only.
 */
import { Component, type ErrorInfo, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icon } from './icons';
import { Button } from './ui/Button';

interface Props { children: ReactNode; }
interface State { hasError: boolean; }

/** Design-faithful fallback (System States `error`). Separate so it can use i18n + routing. */
function ErrorFallback() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <main className="app-main">
      <div className="page">
        <div className="state" role="alert">
          <div className="state-icon error state-icon-lg">
            <Icon name="warnTriangle" size={28} aria-hidden />
          </div>
          <p className="state-title state-title-lg">{t('errorBoundary.title')}</p>
          <p className="state-body">{t('errorBoundary.body')}</p>
          <div className="state-actions">
            <Button onClick={() => window.location.reload()}>{t('common.reload')}</Button>
            <Button variant="secondary" onClick={() => navigate('/')}>{t('common.goToDashboard')}</Button>
          </div>
        </div>
      </div>
    </main>
  );
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    // Diagnostics only — never surfaced to the user (no internal details leak).
    console.error('Unhandled UI error', error, info.componentStack);
  }

  render(): ReactNode {
    if (this.state.hasError) return <ErrorFallback />;
    return this.props.children;
  }
}
