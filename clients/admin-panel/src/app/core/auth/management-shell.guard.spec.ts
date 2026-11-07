import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { authGuard, managementShellGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { routes } from '../../app.routes';

/**
 * Paket F F4 (live 07.11.2026): a Koçluk token with roles: [] opened the /dashboard management shell
 * ("EduIvme yönetim paneli"). The shell now needs a management role.
 */
describe('managementShellGuard', () => {
  function run(profile: { roles: string[]; permissions?: string[]; product?: string } | null, options: { authenticated?: boolean; landing?: string | null } = {}) {
    const auth = {
      userProfile: signal(profile),
      waitForAuth: vi.fn().mockResolvedValue(options.authenticated ?? true),
      getLandingRoute: vi.fn(() => options.landing ?? null),
      endRoleLessSession: vi.fn().mockResolvedValue(undefined)
    };
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: auth }] });
    const router = TestBed.inject(Router);
    const result = TestBed.runInInjectionContext(() => managementShellGuard({} as never, { url: '/dashboard' } as never));
    return { auth, router, result: result as Promise<boolean | UrlTree> };
  }

  it('lets management roles into the shell', async () => {
    for (const role of ['SystemAdmin', 'Editor', 'InstitutionAdmin', 'InstitutionOwner']) {
      TestBed.resetTestingModule();
      const { result, auth } = run({ roles: [role], product: 'coaching' });
      expect(await result).toBe(true);
      expect(auth.endRoleLessSession).not.toHaveBeenCalled();
    }
  });

  it('sends a Koçluk student to the portal instead of the shell', async () => {
    const { result, router, auth } = run({ roles: ['Student'], product: 'coaching' }, { landing: '/coaching-portal' });
    expect(router.serializeUrl(await result as UrlTree)).toBe('/coaching-portal');
    expect(auth.endRoleLessSession).not.toHaveBeenCalled();
  });

  it('ends a role-less Koçluk session and opens the login page', async () => {
    const { result, router, auth } = run({ roles: [], product: 'coaching' });
    expect(router.serializeUrl(await result as UrlTree)).toBe('/auth/login');
    expect(auth.endRoleLessSession).toHaveBeenCalledTimes(1);
  });

  it('does not bounce a portal role of another product between the portal and the shell', async () => {
    const { result, router, auth } = run({ roles: ['Student'], product: 'speed-reading' }, { landing: '/coaching-portal' });
    expect(router.serializeUrl(await result as UrlTree)).toBe('/auth/login');
    expect(auth.endRoleLessSession).toHaveBeenCalledTimes(1);
  });

  // PR #125 review M2: no management shell by exclusion.
  it('ends course-management and permission-less custom sessions instead of opening the shell', async () => {
    for (const roles of [['CourseStudent'], ['CourseTeacher'], ['ContentManager']]) {
      TestBed.resetTestingModule();
      const { result, router, auth } = run({ roles, permissions: [], product: 'coaching' });
      expect(router.serializeUrl(await result as UrlTree)).toBe('/auth/login');
      expect(auth.endRoleLessSession).toHaveBeenCalledTimes(1);
    }
  });

  it('lets a custom role with an admin permission claim into the shell', async () => {
    const { result } = run({ roles: ['ContentManager'], permissions: ['Permissions.Coaching.ContentManage'], product: 'coaching' });
    expect(await result).toBe(true);
  });

  it('leaves a missing session to authGuard', async () => {
    const { result, auth } = run(null, { authenticated: false });
    expect(await result).toBe(false);
    expect(auth.endRoleLessSession).not.toHaveBeenCalled();
  });

  it('guards the /dashboard route after authGuard', () => {
    const dashboard = routes.find(route => route.path === 'dashboard');
    expect(dashboard?.canActivate).toEqual([authGuard, managementShellGuard]);
  });
});
