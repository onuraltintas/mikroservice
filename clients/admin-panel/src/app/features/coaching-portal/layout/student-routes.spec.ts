import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Route, Router } from '@angular/router';
import { COACHING_PORTAL_ROUTES, LEGACY_STUDENT_REDIRECTS } from '../coaching-portal.routes';
import { STUDENT_HUBS } from './student-hubs';

@Component({ standalone: true, template: '' })
class Blank {}

// T2 (31.10.2026): every old student address leads to its new home, keeping ids and query parameters, so links in
// e-mails, notifications and bookmarks keep working.
const expected: Record<string, string> = {
  'study-plans': 'plan',
  'automatic-plan': 'plan/wizard',
  'study-plan-assistant': 'plan/wizard',
  'weekly-plan': 'plan/weekly',
  'study-availability': 'plan/hours',
  'weekly-plan-preview': 'plan/preview',
  'goals': 'growth/goals',
  'progress': 'growth/week',
  'exam-results': 'growth/exams',
  'setup': 'account/settings',
  'profile': 'account/settings/profile',
  'settings': 'account/settings/general',
  'subscription': 'account/payments',
  'finance': 'account/payments/fees',
  'finance/:feeId': 'account/payments/fees/:feeId'
};

function routerWith(routes: Route[]) {
  TestBed.configureTestingModule({ providers: [provideRouter([{ path: 'coaching-portal', children: routes }])] });
  return TestBed.inject(Router);
}

describe('student route redirects', () => {
  it('lists exactly the old student addresses and sends each to its new home', async () => {
    expect(LEGACY_STUDENT_REDIRECTS.map(r => r.path)).toEqual(Object.keys(expected));
    const router = routerWith([...LEGACY_STUDENT_REDIRECTS, { path: '**', component: Blank }]);
    for (const [from, to] of Object.entries(expected)) {
      await router.navigateByUrl(`/coaching-portal/${from.replace(':feeId', 'f1')}`);
      expect(router.url).toBe(`/coaching-portal/${to.replace(':feeId', 'f1')}`);
    }
  });

  it('keeps every redirect in the portal routes, ahead of the pages', () => {
    for (const redirect of LEGACY_STUDENT_REDIRECTS) expect(COACHING_PORTAL_ROUTES).toContain(redirect);
  });

  it('keeps ids and query parameters across the redirect', async () => {
    const router = routerWith([...LEGACY_STUDENT_REDIRECTS, { path: '**', component: Blank }]);
    const cases: [string, string][] = [
      ['/coaching-portal/study-plans?plan=p1&task=t1', '/coaching-portal/plan?plan=p1&task=t1'],
      ['/coaching-portal/progress?week=2026-10-26&decision=d1', '/coaching-portal/growth/week?week=2026-10-26&decision=d1'],
      ['/coaching-portal/subscription?plan=ogrenci-yillik', '/coaching-portal/account/payments?plan=ogrenci-yillik'],
      ['/coaching-portal/subscription?required=1', '/coaching-portal/account/payments?required=1'],
      ['/coaching-portal/finance/fee-9', '/coaching-portal/account/payments/fees/fee-9'],
      ['/coaching-portal/profile', '/coaching-portal/account/settings/profile'],
      ['/coaching-portal/study-availability', '/coaching-portal/plan/hours']
    ];
    for (const [from, to] of cases) {
      await router.navigateByUrl(from);
      expect(router.url).toBe(to);
    }
  });

  it('serves the new sections from the portal routes', () => {
    const paths = COACHING_PORTAL_ROUTES.map(r => r.path);
    for (const path of ['', 'plan', 'growth', 'assignments', 'assignments/:id', 'messages', 'messages/:conversationId', 'sessions',
      'notifications', 'account/settings', 'account/payments', 'account/data', 'parent', 'parent/children/:studentId']) {
      expect(paths).toContain(path);
    }
    const plan = COACHING_PORTAL_ROUTES.find(r => r.path === 'plan')!;
    expect(plan.children!.map(c => c.path)).toEqual(['', 'weekly', 'hours', 'wizard', 'preview']);
    const growth = COACHING_PORTAL_ROUTES.find(r => r.path === 'growth')!;
    expect(growth.children!.map(c => c.path)).toEqual(['', 'exams', 'goals', 'week']);
    const settings = COACHING_PORTAL_ROUTES.find(r => r.path === 'account/settings')!;
    expect(settings.children!.map(c => c.path)).toEqual(['', 'profile', 'general', 'theme']);
    const payments = COACHING_PORTAL_ROUTES.find(r => r.path === 'account/payments')!;
    expect(payments.children!.map(c => c.path)).toEqual(['', 'fees', 'fees/:feeId']);
  });

  it('keeps the unsaved-change guards on the moved pages', () => {
    const child = (parent: string, path: string) => COACHING_PORTAL_ROUTES.find(r => r.path === parent)!.children!.find(c => c.path === path)!;
    for (const [parent, path] of [['plan', ''], ['plan', 'weekly'], ['plan', 'hours'], ['plan', 'wizard'], ['plan', 'preview'],
      ['growth', 'exams'], ['growth', 'goals'], ['growth', 'week'], ['account/settings', ''], ['account/settings', 'profile'], ['account/settings', 'general']]) {
      expect(child(parent, path).canDeactivate?.length, `${parent}/${path}`).toBe(1);
    }
    expect(child('plan', 'hours').data?.['initialTab']).toBe('hours');
  });

  it('describes each hub with tabs that point inside it', () => {
    expect(STUDENT_HUBS.plan.tabs.map(t => t.label)).toEqual(['Planım', 'Haftalık düzen', 'Plan sihirbazı']);
    expect(STUDENT_HUBS.growth.tabs.map(t => t.label)).toEqual(['Denemeler', 'Hedefim', 'Haftam']);
    expect(STUDENT_HUBS.settings.tabs.map(t => t.label)).toEqual(['Tercihlerim', 'Profilim', 'Ayarlar', 'Görünüm']);
    expect(STUDENT_HUBS.payments.tabs.map(t => t.label)).toEqual(['Abonelik', 'Ücretlerim']);
    for (const hub of Object.values(STUDENT_HUBS)) for (const tab of hub.tabs) expect(tab.path.startsWith('/coaching-portal/')).toBe(true);
  });
});
