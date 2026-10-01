import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { LoginComponent } from './login.component';

describe('LoginComponent MFA flow', () => {
  it('opens legal acceptance on the same screen without repeating Google authentication', async () => {
    const router = { navigate: vi.fn().mockResolvedValue(true) };
    const authService = { loginWithGoogle: vi.fn().mockResolvedValue({
      requiresLegalAcceptance: true, registrationToken: 'pending-ticket'
    }) };
    TestBed.configureTestingModule({ providers: [
      { provide: AuthService, useValue: authService },
      { provide: Router, useValue: router },
      { provide: ToasterService, useValue: { info: vi.fn(), error: vi.fn() } },
      { provide: SocialAuthService, useValue: { authState: new Subject() } }
    ] });
    const component = TestBed.runInInjectionContext(() => new LoginComponent());
    await component.handleGoogleLogin('google-token');
    expect(router.navigate).not.toHaveBeenCalled();
    expect((component as any).googleRegistrationToken()).toBe('pending-ticket');
    expect(component.isLoading()).toBe(false);
  });
  it('completes registration with the pending ticket and accepted versions, not the Google token', async () => {
    const authService = { completeGoogleRegistration: vi.fn().mockResolvedValue({ authenticated: true }) };
    const component = createComponent(authService);
    component.googleRegistrationToken.set('ticket');
    const accepted = ['privacy', 'kvkk', 'coaching-terms'].map(slug => ({ slug, version: 1 }));
    await component.completeGoogleRegistration([]);
    expect(authService.completeGoogleRegistration).not.toHaveBeenCalled();
    await component.completeGoogleRegistration(accepted);
    expect(authService.completeGoogleRegistration).toHaveBeenCalledWith('ticket', accepted);
    expect(component.googleRegistrationToken()).toBe('');
  });

  it('clears an expired ticket and allows Google authentication again', async () => {
    const component = createComponent({ completeGoogleRegistration: vi.fn().mockRejectedValue({
      error: { code: 'Auth.GoogleRegistrationExpired', description: 'Süre doldu' }
    }) });
    component.googleRegistrationToken.set('ticket');
    await component.completeGoogleRegistration(['privacy', 'kvkk', 'coaching-terms'].map(slug => ({ slug, version: 1 })));
    expect(component.googleRegistrationToken()).toBe('');
    expect(component.errorMessage()).toBe('Süre doldu');
    expect(component.isLoading()).toBe(false);
  });

  it('starts MFA enrollment only when the backend explicitly requires it', async () => {
    const authService = {
      loginWithPassword: vi.fn().mockResolvedValue({
        authenticated: false,
        requiresMfa: true,
        mfaEnrollmentRequired: true,
        mfaChallengeToken: 'challenge'
      }),
      startMfaSetup: vi.fn().mockResolvedValue({
        secret: 'ABCDEFGHIJKLMNOP234567',
        otpAuthUri: 'otpauth://totp/test',
        setupToken: 'setup'
      })
    };
    const component = createComponent(authService);
    component.email = 'admin@example.test';
    component.password = 'password';

    await component.onSubmit(new Event('submit'));

    expect(component.mfaStage()).toBe('setup');
    expect(component.mfaSecret()).toBe('ABCDEFGHIJKLMNOP234567');
    expect(authService.startMfaSetup).toHaveBeenCalledWith('challenge');
  });

  it('shows one-time recovery codes after successful enrollment', async () => {
    const authService = {
      enableMfa: vi.fn().mockResolvedValue(['CODE01-CODE001', 'CODE02-CODE002'])
    };
    const component = createComponent(authService);
    component.mfaChallengeToken = 'challenge';
    component.mfaSetupToken = 'setup';
    component.mfaStage.set('setup');
    component.mfaCode = '123456';

    await component.submitMfa();

    expect(component.mfaStage()).toBe('recovery');
    expect(component.recoveryCodes()).toEqual(['CODE01-CODE001', 'CODE02-CODE002']);
  });

  function createComponent(authService: Record<string, unknown>): LoginComponent {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() } },
        { provide: SocialAuthService, useValue: { authState: new Subject() } }
      ]
    });
    return TestBed.runInInjectionContext(() => new LoginComponent());
  }
});
