import { resolveCoachingDestination } from './coaching-redirect.component';

describe('legacy coaching destinations', () => {
  it('moves student coaching into the current student portal', () => {
    expect(resolveCoachingDestination('/student/coaching')).toBe('https://eduivme.com/coaching-portal');
  });

  it('moves all old coach pages into the coaching staff workspace', () => {
    for (const path of ['/coaching', '/coaching/dashboard', '/coaching/students/123', '/coaching/sessions', '/coaching/goals']) {
      expect(resolveCoachingDestination(path)).toBe('https://onuraltintas.net/staff/?product=coaching');
    }
  });
});
