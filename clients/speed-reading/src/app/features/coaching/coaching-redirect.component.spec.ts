import { resolveCoachingDestination } from './coaching-redirect.component';
import { routes } from '../../app.routes';

describe('legacy coaching destinations', () => {
  it('moves student coaching into the current student portal', () => {
    expect(resolveCoachingDestination('/student/coaching')).toBe('https://eduivme.com/coaching-portal');
    expect(resolveCoachingDestination('/student/coaching/')).toBe('https://eduivme.com/coaching-portal');
  });

  it('redirects before requiring a local speed-reading student session', () => {
    const redirect = routes.find(route => route.path === 'student/coaching');
    expect(redirect).toBeDefined();
    expect(redirect?.canActivate).toBeUndefined();
    expect(routes.indexOf(redirect!)).toBeLessThan(routes.findIndex(route => route.path === 'student'));
  });

  it('moves all old coach pages into the coaching staff workspace', () => {
    for (const path of ['/coaching', '/coaching/dashboard', '/coaching/students/123', '/coaching/sessions', '/coaching/goals']) {
      expect(resolveCoachingDestination(path)).toBe('https://onuraltintas.net/staff/?product=coaching');
    }
  });
});
