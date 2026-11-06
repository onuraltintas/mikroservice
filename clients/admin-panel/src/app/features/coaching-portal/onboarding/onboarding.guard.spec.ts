import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { CoachingOnboardingService, OnboardingStatus } from '../../../core/services/coaching-onboarding.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { studentOnboardingGuard } from './onboarding.guard';

describe('studentOnboardingGuard (Paket C #3)', () => {
  async function run(roles: string[], status: OnboardingStatus | 'error', url = '/coaching-portal/plan') {
    const profile = signal<UserProfile | null>({ id: 'u1', email: 'a@test', firstName: 'A', lastName: 'B', username: 'a@test', roles, role: roles[0], permissions: [], product: 'coaching' });
    const currentStatus = vi.fn(() => status === 'error' ? throwError(() => new Error('down')) : of(status));
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: { userProfile: profile } },
      { provide: CoachingOnboardingService, useValue: { currentStatus } }] });
    TestBed.inject(CoachingPortalViewService);
    const result = await TestBed.runInInjectionContext(() => studentOnboardingGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot));
    return { result, currentStatus, router: TestBed.inject(Router) };
  }

  it('keeps the panel closed until the wizard is done', async () => {
    const { result, router } = await run(['Student'], 'InProgress');
    expect(router.serializeUrl(result as UrlTree)).toBe('/coaching-portal/welcome');
    TestBed.resetTestingModule();
    expect((await run(['Student'], 'NotStarted')).result).toBeInstanceOf(UrlTree);
  });

  it('lets a student who finished, parents, teachers and the KVKK page through', async () => {
    expect((await run(['Student'], 'Completed')).result).toBe(true);
    TestBed.resetTestingModule();
    const parent = await run(['Parent'], 'NotStarted');
    expect(parent.result).toBe(true); expect(parent.currentStatus).not.toHaveBeenCalled();
    TestBed.resetTestingModule();
    expect((await run(['Teacher'], 'NotStarted')).result).toBe(true);
    TestBed.resetTestingModule();
    expect((await run(['Student'], 'NotStarted', '/coaching-portal/account/data')).result).toBe(true);
  });

  it('does not lock the panel when the status cannot be read', async () => {
    expect((await run(['Student'], 'error')).result).toBe(true);
  });
});
