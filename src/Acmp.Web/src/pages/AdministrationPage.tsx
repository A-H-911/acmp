import { UsersMembership } from '../features/administration/UsersMembership';

/*
 * Administration area (admin-gated by route). P4 ships the Users & Membership screen; Templates,
 * System Health, and Notification Settings are later phases (shown as disabled tabs).
 */
export default function AdministrationPage() {
  return <UsersMembership />;
}
