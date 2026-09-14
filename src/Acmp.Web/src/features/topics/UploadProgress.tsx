import { useTranslation } from 'react-i18next';
import { cssVars } from '../../lib/cssVars';
import { Pct } from '../../lib/numberFmt';

/*
 * DEF-171: a large upload shows how far it has got. The status line is announced once; the percentage lives on
 * the progressbar's aria-valuenow, so a screen reader is not interrupted a hundred times during one upload.
 */
export function UploadProgress({ name, pct }: { name: string; pct: number }) {
  const { t } = useTranslation();
  return (
    <div className="sub-upload">
      <span className="sub-upload-label" role="status" aria-live="polite">{t('detail.attach.uploading', { name })}</span>
      <span className="sub-upload-bar" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label={name}>
        <span className="sub-upload-fill" ref={cssVars({ '--pct': `${pct}%` })} />
      </span>
      <span className="sub-upload-pct"><Pct value={pct} /></span>
    </div>
  );
}
