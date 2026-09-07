import { resolveCentralAdminPath } from './central-admin-redirect.component';

describe('resolveCentralAdminPath', () => {
  it('maps legacy user administration to the central identity workspace', () => {
    expect(resolveCentralAdminPath('/admin/users')).toBe('identity/users');
    expect(resolveCentralAdminPath('/admin/teachers')).toBe('identity/users');
  });

  it('maps legacy content administration to the central speed reading workspace', () => {
    expect(resolveCentralAdminPath('/admin/program-templates/123/edit')).toBe('speed-reading/programs');
    expect(resolveCentralAdminPath('/admin/vocabulary')).toBe('speed-reading/language-content');
    expect(resolveCentralAdminPath('/admin/question-bank')).toBe('speed-reading/catalog');
    expect(resolveCentralAdminPath('/admin/legal-documents')).toBe('speed-reading/communications');
  });

  it('preserves legacy report, notification and coaching destinations', () => {
    expect(resolveCentralAdminPath('/admin/reports/institutions')).toBe('speed-reading/analytics?tab=institutions');
    expect(resolveCentralAdminPath('/admin/reports/content')).toBe('speed-reading/analytics?tab=content');
    expect(resolveCentralAdminPath('/admin/notifications/send')).toBe('speed-reading/communications?tab=notifications');
    expect(resolveCentralAdminPath('/admin/coaching/exam-results')).toBe('coaching/operations?resource=exams');
    expect(resolveCentralAdminPath('/admin/coaching/sessions/42')).toBe('coaching/operations?resource=sessions');
    expect(resolveCentralAdminPath('/admin/coaching/goals')).toBe('coaching/operations?resource=goals');
  });

  it('sends removed tools to the central dashboard', () => {
    expect(resolveCentralAdminPath('/admin/test-mode')).toBe('');
    expect(resolveCentralAdminPath('/admin/system/backups')).toBe('');
    expect(resolveCentralAdminPath('/admin')).toBe('');
  });
});
