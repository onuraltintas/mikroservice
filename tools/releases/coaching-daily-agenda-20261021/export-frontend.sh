set -euo pipefail
umask 077
cd /var/lib/eduivme/releases/coaching-calendar-menu-20261021-r4/source/admin-panel
tar -czf /var/lib/eduivme/release-previews/coaching-daily-agenda-20261021/frontend-baseline.tar.gz \
 src/app/features/coaching-portal/pages/coaching-portal-home.component.ts \
 src/app/features/coaching-portal/pages/coaching-portal-home.component.html \
 src/app/features/coaching-portal/pages/coaching-portal-home.component.scss \
 src/app/features/coaching-portal/pages/student-weekly-home.component.ts \
 projects/staff-portal/src/app/features/coaching/coaching-teacher-tracking.component.ts \
 projects/staff-portal/src/app/features/coaching/coaching-teacher-tracking.component.html \
 projects/staff-portal/src/app/features/coaching/coaching-teacher-tracking.service.ts \
 projects/staff-portal/src/app/features/coaching/coaching-teacher-workspace.component.ts
