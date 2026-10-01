import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { staffOnboardingGuard } from './staff-onboarding.guard';

describe('staffOnboardingGuard', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;
  const previewUrl = {} as UrlTree;

  beforeEach(() => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['hasAdminAccess', 'hasRole']);
    router = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);
    router.createUrlTree.and.returnValue(previewUrl);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router }
      ]
    });
  });

  const activate = () => TestBed.runInInjectionContext(() => staffOnboardingGuard(
    {} as ActivatedRouteSnapshot,
    { url: '/student/dashboard' } as RouterStateSnapshot
  ));

  for (const role of ['Admin', 'SystemAdmin', 'Editor']) {
    it(`keeps a dual Student/${role} on student onboarding`, () => {
      authService.hasAdminAccess.and.returnValue(role !== 'Editor');
      authService.hasRole.and.callFake(candidate => candidate === 'Student' || candidate === role);
      expect(activate()).toBe(true);
      expect(router.createUrlTree).not.toHaveBeenCalled();
    });
  }

  it('sends admins and system admins to the exercise preview', () => {
    authService.hasAdminAccess.and.returnValue(true);

    expect(activate()).toBe(previewUrl);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/student/exercises']);
  });

  it('sends editors to the exercise preview', () => {
    authService.hasAdminAccess.and.returnValue(false);
    authService.hasRole.and.callFake(role => role === 'Editor');

    expect(activate()).toBe(previewUrl);
  });

  it('keeps students on their onboarding route', () => {
    authService.hasAdminAccess.and.returnValue(false);
    authService.hasRole.and.returnValue(false);

    expect(activate()).toBe(true);
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });
});
