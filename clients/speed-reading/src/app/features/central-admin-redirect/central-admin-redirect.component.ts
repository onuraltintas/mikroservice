import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

const LEGACY_ADMIN_PATHS: Record<string, string> = {
  'dashboard': '',
  'users': 'identity/users',
  'teachers': 'identity/users',
  'students': 'identity/users',
  'coaches': 'coaching',
  'roles': 'identity/roles',
  'editors': 'identity/users',
  'bulk-operations': 'identity/bulk-users',
  'question-bank': 'speed-reading/catalog',
  'institutions': 'identity/institutions',
  'program-templates': 'speed-reading/programs',
  'analytics/programs': 'speed-reading/analytics?tab=programs',
  'exercises': 'speed-reading/catalog',
  'exercise-types': 'speed-reading/catalog',
  'reading-texts': 'speed-reading/catalog',
  'vocabulary': 'speed-reading/language-content',
  'assessment-config': 'speed-reading/content-configuration',
  'age-groups': 'speed-reading/content-configuration',
  'achievements': 'speed-reading/achievements',
  'cms/visualization-scenes': 'speed-reading/visualization-scenes',
  'metrics': 'speed-reading/analytics',
  'email-templates': 'speed-reading/communications',
  'email-campaigns': 'speed-reading/communications',
  'notifications': 'speed-reading/communications?tab=notifications',
  'notifications/all': 'speed-reading/communications?tab=notifications',
  'notifications/send': 'speed-reading/communications?tab=notifications',
  'notifications/preferences': 'notifications',
  'reports': 'speed-reading/analytics?tab=platform',
  'reports/pages': 'speed-reading/analytics?tab=institutions',
  'reports/students': 'speed-reading/progress',
  'reports/institutions': 'speed-reading/analytics?tab=institutions',
  'reports/platform-usage': 'speed-reading/analytics?tab=platform',
  'reports/content': 'speed-reading/analytics?tab=content',
  'reports/health': 'speed-reading/analytics?tab=health',
  'report-templates': 'speed-reading/reports?tab=templates',
  'coaching': 'coaching',
  'coaching/sessions': 'coaching/operations?resource=sessions',
  'coaching/study-sessions': 'coaching/operations?resource=sessions',
  'coaching/exams': 'coaching/operations?resource=exams',
  'coaching/exam-results': 'coaching/operations?resource=exams',
  'coaching/goals': 'coaching/operations?resource=goals',
  'coaching/assignments': 'coaching/assignments',
  'coaching/snapshots': 'speed-reading/analytics?tab=platform',
  'coaching/weak-subjects': 'speed-reading/progress',
  'subscriptions': 'speed-reading/subscriptions',
  'settings': 'settings/configurations',
  'audit-logs': 'settings/admin-audit',
  'cms': 'speed-reading/communications',
  'legal-documents': 'speed-reading/communications'
};

export function resolveCentralAdminPath(pathname: string): string {
  const legacyPath = pathname.replace(/^\/admin(?:\/|$)/, '').replace(/\/$/, '');
  if (!legacyPath) return '';

  const exactMatch = LEGACY_ADMIN_PATHS[legacyPath];
  if (exactMatch !== undefined) return exactMatch;

  const prefixMatch = Object.keys(LEGACY_ADMIN_PATHS)
    .filter(path => legacyPath.startsWith(`${path}/`))
    .sort((left, right) => right.length - left.length)[0];
  if (prefixMatch) return LEGACY_ADMIN_PATHS[prefixMatch];

  const firstSegment = legacyPath.split('/')[0];
  return LEGACY_ADMIN_PATHS[firstSegment] ?? '';
}

@Component({
  selector: 'app-central-admin-redirect',
  standalone: true,
  template: `
    <main aria-labelledby="admin-moved-title" class="admin-moved">
      <h1 id="admin-moved-title">Yönetim paneli taşındı</h1>
      <p>Yönetim işlemleri artık eduivme.com üzerindeki merkezi panelden yürütülüyor.</p>
      <a [href]="destination">Merkezi yönetim panelini aç</a>
    </main>
  `
})
export class CentralAdminRedirectComponent implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  readonly destination = environment.centralAdminUrl;

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    const path = resolveCentralAdminPath(window.location.pathname);
    const destination = path ? `${this.destination}/${path}` : this.destination;
    window.location.replace(destination);
  }
}
