import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot } from '@angular/router';
import { Observable, firstValueFrom, isObservable, of } from 'rxjs';
import { AssessmentService } from '../../services/assessment.service';
import { AuthService } from '../services/auth.service';
import { assessmentGuard } from './assessment.guard';
import { profileSetupGuard } from './profile-setup.guard';

describe('student prerequisites', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let assessmentService: jasmine.SpyObj<AssessmentService>;
  let router: jasmine.SpyObj<Router>;
  let http: jasmine.SpyObj<HttpClient>;
  const route = {} as ActivatedRouteSnapshot;
  const state = { url: '/student/daily-exercises' } as RouterStateSnapshot;

  beforeEach(() => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['hasAdminAccess', 'hasRole', 'hasCompletedProfile']);
    assessmentService = jasmine.createSpyObj<AssessmentService>('AssessmentService', ['getAssessmentStatus']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    http = jasmine.createSpyObj<HttpClient>('HttpClient', ['get']);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: AssessmentService, useValue: assessmentService },
        { provide: Router, useValue: router },
        { provide: HttpClient, useValue: http }
      ]
    });
  });

  for (const role of ['Admin', 'Editor']) {
    it(`does not require profile or assessment from ${role}`, () => {
      authService.hasAdminAccess.and.returnValue(role === 'Admin');
      authService.hasRole.and.callFake(candidate => candidate === role);
      authService.hasCompletedProfile.and.returnValue(false);

      expect(TestBed.runInInjectionContext(() => profileSetupGuard(route, state))).toBe(true);
      expect(TestBed.runInInjectionContext(() => assessmentGuard(route, state))).toBe(true);
      expect(assessmentService.getAssessmentStatus).not.toHaveBeenCalled();
      expect(router.navigate).not.toHaveBeenCalled();
    });
  }

  it('still requires an incomplete student profile', async () => {
    authService.hasAdminAccess.and.returnValue(false);
    authService.hasRole.and.returnValue(false);
    authService.hasCompletedProfile.and.returnValue(false);
    http.get.and.returnValue(of({ hasAgeGroupConfiguration: false }));

    const result = TestBed.runInInjectionContext(() => profileSetupGuard(route, state));
    expect(isObservable(result)).toBe(true);
    expect(await firstValueFrom(result as Observable<boolean>)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/student/profile-setup']);
  });

  it('returns a student with an identity-only profile to profile setup', async () => {
    authService.hasAdminAccess.and.returnValue(false);
    authService.hasRole.and.returnValue(false);
    authService.hasCompletedProfile.and.returnValue(true);
    http.get.and.returnValue(of({ hasAgeGroupConfiguration: false }));

    const result = TestBed.runInInjectionContext(() => profileSetupGuard(route, state));
    expect(isObservable(result)).toBe(true);
    expect(await firstValueFrom(result as Observable<boolean>)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/student/profile-setup']);
  });

  it('allows a student whose identity and speed-reading profiles are complete', async () => {
    authService.hasAdminAccess.and.returnValue(false);
    authService.hasRole.and.returnValue(false);
    authService.hasCompletedProfile.and.returnValue(true);
    http.get.and.returnValue(of({ hasAgeGroupConfiguration: true }));

    const result = TestBed.runInInjectionContext(() => profileSetupGuard(route, state));
    expect(await firstValueFrom(result as Observable<boolean>)).toBe(true);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('still requires an incomplete student assessment', async () => {
    authService.hasAdminAccess.and.returnValue(false);
    authService.hasRole.and.returnValue(false);
    assessmentService.getAssessmentStatus.and.returnValue(of({ hasCompleted: false } as any));

    const result = TestBed.runInInjectionContext(() => assessmentGuard(route, state));
    expect(isObservable(result)).toBe(true);
    expect(await firstValueFrom(result as Observable<boolean>)).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/student/assessment-intro'], {
      queryParams: { returnUrl: state.url }
    });
  });
});
