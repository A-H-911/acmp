/*
 * Convert-to-ADR dialog (P11e, FR-068). Launched from an issued Decision's "Convert to ADR" action (Chairman
 * only). Matches the confirm dialog in "ACMP Decision, Voting & ADR.dc.html" (convertOpen) — a confirmation,
 * not a form: the new ADR is pre-filled server-side from the decision. On confirm we POST
 * /api/adrs/from-decision and route to the new /adrs/:key. A 409 (already promoted / not issued) surfaces
 * inline without navigating.
 */
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../components/ui/Dialog';
import { Button } from '../../components/ui/Button';
import { Icon } from '../../components/icons';
import { ApiError } from '../../api/apiClient';
import { usePromoteDecisionToAdr } from '../../api/adrs';

interface Props {
  open: boolean;
  onClose: () => void;
  decisionId: string;
  decisionKey: string;
}

export function ConvertToAdrDialog({ open, onClose, decisionId, decisionKey }: Props) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const promote = usePromoteDecisionToAdr();
  const [submitError, setSubmitError] = useState<string | null>(null);

  async function onConfirm() {
    setSubmitError(null);
    try {
      const adr = await promote.mutateAsync(decisionId);
      onClose();
      navigate(`/adrs/${adr.key}`);
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.problem?.title ?? t('decisions.convert.error') : t('decisions.convert.error'));
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      tone="accent"
      icon={<Icon name="adr" size={20} aria-hidden />}
      title={t('decisions.convert.title')}
      description={t('decisions.convert.subtitle')}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" loading={promote.isPending} onClick={() => void onConfirm()}>
            {t('decisions.convert.confirm')}
          </Button>
        </>
      }
    >
      <div className="dec-convert-body">
        <p className="dec-convert-text">{t('decisions.convert.body', { key: decisionKey })}</p>
        {submitError && (
          <p className="field-error" role="alert"><Icon name="alertCircle" size={13} aria-hidden />{submitError}</p>
        )}
      </div>
    </Dialog>
  );
}
