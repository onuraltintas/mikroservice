import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { studentPageGuard } from './student-page.guard';

describe('studentPageGuard', () => {
  function permits(path: string, roles: string[]): boolean {
    const navigate = jasmine.createSpy('navigate');
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { hasRole: (role: string) => roles.includes(role) } },
        { provide: Router, useValue: { navigate } }
      ]
    });
    return TestBed.runInInjectionContext(() =>
      studentPageGuard({ routeConfig: { path } } as ActivatedRouteSnapshot, {} as never)) as boolean;
  }

  it('blocks staff-only accounts from personal student reports', () => {
    expect(permits('reports', ['Teacher'])).toBeFalse();
  });

  it('allows teachers to open staff training programs and their daily training', () => {
    expect(permits('training-programs', ['Teacher'])).toBeTrue();
    TestBed.resetTestingModule();
    expect(permits('daily-exercises', ['Teacher'])).toBeTrue();
  });
  it('redirects the teacher student-shell landing to training programs', () => {
    const destination = {};
    const router = { navigate: jasmine.createSpy(), createUrlTree: jasmine.createSpy().and.returnValue(destination) };
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: { hasRole: (role: string) => role === 'Teacher' } },
      { provide: Router, useValue: router }
    ] });
    const result = TestBed.runInInjectionContext(() => studentPageGuard(
      { routeConfig: { path: 'dashboard' } } as ActivatedRouteSnapshot, {} as never));
    expect(result).toBe(destination as any);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/student/training-programs']);
  });

  it('preserves staff exercise preview', () => {
    expect(permits('exercises/universal-player/:exerciseId', ['Teacher'])).toBeTrue();
  });

  it('preserves every student page for an account with both roles', () => {
    expect(permits('reports', ['Teacher', 'Student'])).toBeTrue();
  });
});
