import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot } from '@angular/router';
import { Observable, firstValueFrom, isObservable, of, throwError } from 'rxjs';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

// 18.11.2026: "Panelime git" on a page left open renews the session instead of sending the student to sign in.
describe('authGuard', () => {
  let auth: { isAuthenticated: boolean; currentUserValue: unknown; hasRole: jasmine.Spy; refreshToken: jasmine.Spy };
  let router: jasmine.SpyObj<Router>;
  const route = { data: { role: 'Student' } } as unknown as ActivatedRouteSnapshot;
  const state = { url: '/student/dashboard' } as RouterStateSnapshot;

  beforeEach(() => {
    auth = { isAuthenticated: false, currentUserValue: null, hasRole: jasmine.createSpy().and.returnValue(true), refreshToken: jasmine.createSpy() };
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: auth }, { provide: Router, useValue: router }] });
  });

  const run = async (): Promise<boolean> => {
    const result = TestBed.runInInjectionContext(() => authGuard(route, state));
    return isObservable(result) ? await firstValueFrom(result as Observable<boolean>) : result as boolean;
  };

  it('lets a signed-in student in', async () => {
    auth.isAuthenticated = true;
    expect(await run()).toBeTrue();
    expect(auth.refreshToken).not.toHaveBeenCalled();
  });

  it('renews an expired access token of a stored session before opening the panel', async () => {
    auth.currentUserValue = { roles: ['Student'] };
    auth.refreshToken.and.callFake(() => { auth.isAuthenticated = true; return of({}); });
    expect(await run()).toBeTrue();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('sends the visitor to sign in when the session cannot be renewed', async () => {
    auth.currentUserValue = { roles: ['Student'] };
    auth.refreshToken.and.returnValue(throwError(() => new Error('401')));
    expect(await run()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { queryParams: { returnUrl: '/student/dashboard' } });
  });

  it('sends an anonymous visitor to sign in without a renewal call', async () => {
    expect(await run()).toBeFalse();
    expect(auth.refreshToken).not.toHaveBeenCalled();
  });

  it('refuses a role the account does not have', async () => {
    auth.isAuthenticated = true;
    auth.hasRole.and.returnValue(false);
    expect(await run()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/error/403']);
  });
});
