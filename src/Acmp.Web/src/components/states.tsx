/*
 * The four canonical screen states (docs/14 §4): empty, loading, error,
 * permission-denied. Every list/detail screen composes these rather than
 * hand-rolling its own. All copy comes through i18n (guardrail 9).
 */
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from './icons';
import { Button } from './ui/Button';

interface StateProps {
  title?: string;
  body?: string;
  icon?: ReactNode;
  action?: ReactNode;
  tone?: 'default' | 'error';
}

function StateShell({ title, body, icon, action, tone = 'default' }: StateProps) {
  return (
    <div className="state" role="status">
      {icon && <div className={`state-icon ${tone === 'default' ? '' : tone}`}>{icon}</div>}
      {title && <p className="state-title">{title}</p>}
      {body && <p className="state-body">{body}</p>}
      {action && <div className="state-actions">{action}</div>}
    </div>
  );
}

export function EmptyState({ title, body }: { title?: string; body?: string }) {
  const { t } = useTranslation();
  return <StateShell icon={<Icon name="doc" size={20} />} title={title ?? t('state.emptyTitle')} body={body ?? t('state.emptyBody')} />;
}

export function LoadingState({ label }: { label?: string }) {
  const { t } = useTranslation();
  return (
    <div className="state" role="status" aria-live="polite" aria-busy="true">
      <span className="visually-hidden">{label ?? t('common.loading')}</span>
      <div style={{ inlineSize: '100%', maxInlineSize: '32rem' }}>
        <div className="skeleton skeleton-row" style={{ inlineSize: '60%' }} />
        <div className="skeleton skeleton-row" />
        <div className="skeleton skeleton-row" style={{ inlineSize: '80%' }} />
      </div>
    </div>
  );
}

export function ErrorState({ title, body, onRetry }: { title?: string; body?: string; onRetry?: () => void }) {
  const { t } = useTranslation();
  return (
    <StateShell
      tone="error"
      icon={<Icon name="alertCircle" size={20} aria-hidden />}
      title={title ?? t('state.errorTitle')}
      body={body ?? t('common.error')}
      action={onRetry && <Button variant="secondary" onClick={onRetry}>{t('common.retry')}</Button>}
    />
  );
}

export function PermissionDenied({ body }: { body?: string }) {
  const { t } = useTranslation();
  return (
    <StateShell
      icon={<Icon name="lock" size={20} aria-hidden />}
      title={t('state.deniedTitle')}
      body={body ?? t('common.permissionDenied')}
    />
  );
}
