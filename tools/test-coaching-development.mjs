import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const cwd = fileURLToPath(new URL('../clients/admin-panel/', import.meta.url));
const suites = {
  'admin-panel': [
    'src/app/features/coaching-portal/pages/student-daily-agenda.component.spec.ts',
    'src/app/core/services/coaching-daily-agenda.service.spec.ts',
    'src/app/features/coaching-portal/pages/coaching-portal-home.component.spec.ts',
    'src/app/features/coaching-portal/pages/student-study-plans-routing.spec.ts',
    'src/app/features/coaching-portal/pages/weekly-decision-work.component.spec.ts',
    'src/app/features/coaching-portal/pages/student-weekly-home.component.spec.ts',
    'src/app/core/services/coaching-weekly-review.service.spec.ts',
    'src/app/features/coaching-portal/pages/weekly-review.component.spec.ts',
    'src/app/core/models/coaching-notification-target.spec.ts',
    'src/app/core/services/notification.service.spec.ts',
    'src/app/features/coaching-portal/pages/coaching-notifications.component.spec.ts',
    'src/app/features/coaching-portal/pages/coaching-portal-progress.component.spec.ts',
    'src/app/features/coaching-portal/pages/student-study-plans.component.spec.ts',
    'src/app/features/coaching-portal/pages/student-automatic-plan.component.spec.ts',
    'src/app/features/coaching-portal/pages/practice-progress.component.spec.ts',
    'src/app/features/coaching-portal/pages/practice-adaptation.component.spec.ts'
  ],
  'staff-portal': [
    'projects/staff-portal/src/app/features/coaching/coaching-teacher-tracking-context.spec.ts',
    'projects/staff-portal/src/app/features/coaching/coaching-weekly-review.spec.ts',
    'projects/staff-portal/src/app/app.component.spec.ts',
    'projects/staff-portal/src/app/notifications/staff-notification-center.component.spec.ts',
    'projects/staff-portal/src/app/notifications/staff-notifications.service.spec.ts',
    'projects/staff-portal/src/app/features/coaching/coaching-teacher-workspace.component.spec.ts',
    'projects/staff-portal/src/app/features/coaching/coaching-teacher-tracking.component.spec.ts',
    'projects/staff-portal/src/app/features/coaching/coaching-teacher-tracking.service.spec.ts',
    'projects/staff-portal/src/app/features/coaching/coaching-teacher-student-detail.component.spec.ts',
    'projects/staff-portal/src/app/features/coaching/coaching-teacher-assignments.component.spec.ts',
    'projects/staff-portal/src/app/features/coaching/coaching-teacher-assignment-detail.component.spec.ts'
  ]
};

for (const [project, files] of Object.entries(suites)) {
  console.log(`\nCoaching development regression: ${project}`);
  const result = spawnSync(process.execPath, ['node_modules/@angular/cli/bin/ng.js', 'test', project, '--watch=false',
    ...files.map(file => `--include=${file}`)], { cwd, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}
