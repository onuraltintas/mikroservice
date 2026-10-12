import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../../../core/services/auth.service';
import { publicRoutes } from '../public.routes';

@Component({ standalone: true, template: 'Payment test page' })
class PaymentTestPage {}

describe('EFT payment route access', () => {
  for (const authenticated of [false, true]) {
    it(authenticated ? 'allows a signed-in user without requiring a paid subscription' : 'redirects an anonymous visitor to login with the selected plan', async () => {
      const paymentRoute = publicRoutes.find(route => route.path === 'odeme')!;
      TestBed.configureTestingModule({ providers: [
        { provide: AuthService, useValue: { isAuthenticated: authenticated } },
        provideRouter([
          { ...paymentRoute, loadComponent: undefined, component: PaymentTestPage },
          { path: 'auth/login', component: PaymentTestPage }
        ])
      ] });
      const harness = await RouterTestingHarness.create();
      await harness.navigateByUrl('/odeme?plan=plan-1');
      const router = TestBed.inject(Router);
      if (authenticated) {
        expect(router.url).toBe('/odeme?plan=plan-1');
      } else {
        const url = router.parseUrl(router.url);
        expect(url.root.children['primary'].segments.map(segment => segment.path)).toEqual(['auth', 'login']);
        expect(url.queryParams['returnUrl']).toBe('/odeme?plan=plan-1');
      }
    });
  }

  it('keeps plan discovery publicly accessible', () => {
    expect(publicRoutes.find(route => route.path === 'fiyatlandirma')?.canActivate?.length ?? 0).toBe(0);
  });
});
