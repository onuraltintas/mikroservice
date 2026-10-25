import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const cwd=fileURLToPath(new URL('../clients/admin-panel/',import.meta.url));
const suites={
  'admin-panel': ['src/app/features/coaching-portal/**/*.spec.ts','src/app/core/services/dark-mode.service.spec.ts','src/app/core/services/toaster.service.spec.ts'],
  'staff-portal': ['projects/staff-portal/src/app/features/coaching/**/*.spec.ts','projects/staff-portal/src/app/app.component.spec.ts','projects/staff-portal/src/app/account/**/*.spec.ts','projects/staff-portal/src/app/notifications/**/*.spec.ts']
};
for(const [project,files] of Object.entries(suites)) {
  const result=spawnSync(process.execPath,['node_modules/@angular/cli/bin/ng.js','test',project,'--watch=false',...files.map(f=>'--include='+f)],{cwd,stdio:'inherit'});
  if(result.error)throw result.error;
  if(result.status!==0)process.exit(result.status??1);
}
