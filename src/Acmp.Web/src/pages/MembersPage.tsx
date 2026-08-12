import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Member } from '../api/members';
import { UsersDirectory, UserDetail } from '../features/administration/UsersMembership';
import '../styles/administration.css';

/*
 * Members area (FR-156 / FR-157, OQ-069 resolved by DEC-041) — the committee roster, the invite, and
 * role assignment, for an Administrator OR a SECRETARY.
 *
 * WHY THIS IS NOT AN ADMINISTRATION TAB ANY MORE. Both requirements read "As an Administrator or
 * Secretary" and the SERVER HAS ALWAYS HONOURED THAT — both commands admit either role, proven at
 * the HTTP boundary. But the affordances lived on Administration > Users, and /admin is
 * Administrator-only, so half of each requirement was unreachable from the product. Widening /admin
 * instead would have handed the Secretary system health, the job monitor, streams and notification
 * settings, contradicting permission-role-matrix row 27 and SoD-5 — an authorization decision, not a
 * layout one. Keeping a copy in both places was refused: one capability in two places is two places
 * for authorization to drift.
 *
 * ⚠ INV-014 DIVERGENCE, RECORDED AS SC-008 BEFORE THE WORK: "ACMP Administration.dc.html" draws a
 * Users tab, and the nav matrix in "ACMP Navigation & IA.dc.html" has no members area at all. The
 * reference and FR-156/FR-157 contradict each other and one of them had to move. The components
 * themselves are MOVED, NOT REDESIGNED, so the USER DETAIL + INVITE layout the reference fixes is
 * preserved exactly in its new home.
 *
 * The route guard is UI courtesy only — the API is what refuses (AC-088/AC-089 assert the 403s).
 */
export default function MembersPage() {
  const { t, i18n } = useTranslation();
  const [detail, setDetail] = useState<Member | null>(null);

  if (detail) {
    return <UserDetail member={detail} isArabic={i18n.language === 'ar'} onBack={() => setDetail(null)} />;
  }

  return (
    <section className="page">
      <div className="adm-head">
        <div>
          <h1 className="page-title">{t('members.title')}</h1>
          <div className="adm-head-sub">{t('members.sub')}</div>
        </div>
      </div>

      <UsersDirectory onView={setDetail} />
    </section>
  );
}
