import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { CoachingMotivationApi, CoachingMotivationComponent, StudentMotivation } from '../../../../../shared/coaching-motivation.component';

// Roadmap D5: streak, badges and goal celebration on the student, parent and teacher screens.
describe('CoachingMotivationComponent', () => {
  const data: StudentMotivation = {
    studentId: 's1', currentStreak: 3, longestStreak: 5, activeToday: false, lastActiveDay: '2026-11-09', timeZoneId: 'Europe/Istanbul',
    badges: [
      { code: 'first-day', label: 'İlk adım', description: '', earnedOn: '2026-11-01' },
      { code: 'streak-7', label: '7 günlük seri', description: '', earnedOn: null }],
    celebration: { goalId: 'g1', goalTitle: 'LGS 450', completedAt: '2026-11-08T09:00:00Z' }
  };
  beforeEach(() => {
    try { localStorage.removeItem('coaching.celebration.dismissed'); } catch { /* ignore */ }
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  function create(student: string) {
    const fixture = TestBed.createComponent(CoachingMotivationComponent);
    fixture.componentRef.setInput('api', new CoachingMotivationApi(TestBed.inject(HttpClient), '/api'));
    fixture.componentRef.setInput('student', student);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the student their streak, earned and locked badges and lets them close the celebration', () => {
    const fixture = create('me');
    TestBed.inject(HttpTestingController).expectOne('/api/coaching/students/me/motivation').flush(data);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="motivation-streak"]')?.textContent).toContain('3');
    expect(element.querySelector('[data-testid="badge-streak-7"]')?.classList.contains('locked')).toBe(true);
    expect(element.querySelector('[data-testid="badge-first-day"]')?.classList.contains('locked')).toBe(false);
    expect(element.textContent).toContain('bugün bir görev tamamla');
    const celebration = element.querySelector('[data-testid="motivation-celebration"]')!;
    expect(celebration.textContent).toContain('Tebrikler');
    (celebration.querySelector('button') as HTMLButtonElement).click(); fixture.detectChanges();
    expect(element.querySelector('[data-testid="motivation-celebration"]')).toBeNull();
  });

  it('shows a parent or teacher the same streak in third person without a close button', () => {
    const fixture = create('child/1');
    TestBed.inject(HttpTestingController).expectOne('/api/coaching/students/child%2F1/motivation').flush({ ...data, activeToday: true });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Çalışma serisi');
    expect(element.textContent).toContain('Bugün çalıştı.');
    expect(element.querySelector('[data-testid="motivation-celebration"] button')).toBeNull();
  });
});
