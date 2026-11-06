import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { AUTH_VOICE } from '../auth-voice';
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

  // Paket F #12: "sen" on the Coaching site, "siz" on the management panel (eduivme.com).
  it('addresses the Coaching site with sen and the management panel with siz', async () => {
    const rejectUnconfirmed = { loginWithPassword: vi.fn().mockRejectedValue({ error: { code: 'Auth.EmailNotConfirmed' } }) };
    const coaching = createComponent(rejectUnconfirmed, 'sen');
    expect(coaching.copy.welcome).toBe('Hoş geldin 👋');
    expect(coaching.copy.loginIntro).toBe('Hesabına giriş yap.');
    expect(coaching.copy.noAccount).toBe('Hesabın yok mu?');
    expect(coaching.copy.signUp).toBe('Kayıt ol');
    expect(coaching.googleButtonText).toBe('continue_with');
    coaching.email = 'ada@example.test'; coaching.password = 'x';
    await coaching.onSubmit(new Event('submit'));
    expect(coaching.errorMessage()).toBe('Giriş yapmadan önce e-posta adresini doğrula.');

    TestBed.resetTestingModule();
    const management = createComponent(rejectUnconfirmed, 'siz');
    expect(management.copy.welcome).toBe('Hoş Geldiniz 👋');
    expect(management.copy.signUp).toBe('Kayıt Olun');
    expect(management.googleButtonText).toBe('signin_with');
    management.email = 'admin@example.test'; management.password = 'x';
    await management.onSubmit(new Event('submit'));
    expect(management.errorMessage()).toBe('Lütfen e-posta adresinizi doğrulayın.');
  });

  function createComponent(authService: Record<string, unknown>, voice: 'sen' | 'siz' = 'sen'): LoginComponent {
    TestBed.configureTestingModule({
      providers: [
        { provide: AUTH_VOICE, useValue: voice },
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() } },
        { provide: SocialAuthService, useValue: { authState: new Subject() } }
      ]
    });
    return TestBed.runInInjectionContext(() => new LoginComponent());
  }
});
