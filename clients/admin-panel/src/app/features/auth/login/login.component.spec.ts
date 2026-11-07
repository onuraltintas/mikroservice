import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, noRoleForProductError } from '../../../core/auth/auth.service';
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
    expect(component.errorMessage()).toBe('Kayıt işleminin süresi doldu. Google ile yeniden devam et.'); // Paket F L8
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

  // Paket F L8: server texts are shown with sen on the Coaching site; the management panel keeps them.
  it('shows server texts with sen on the Coaching site and unchanged on the management panel', async () => {
    const locked = { error: { code: 'Auth.MfaLocked', description: 'Çok fazla hatalı deneme yapıldı. Lütfen daha sonra tekrar deneyin.' } };
    const auth = { verifyMfa: vi.fn().mockRejectedValue(locked), loginWithPassword: vi.fn().mockRejectedValue({
      error: { code: 'System.MaintenanceMode', message: 'Sistem şu anda bakım modundadır. Lütfen daha sonra tekrar deneyiniz.' } }) };
    const coaching = createComponent(auth, 'sen');
    coaching.mfaChallengeToken = 'c'; coaching.mfaStage.set('verify'); coaching.mfaCode = '123456';
    await coaching.submitMfa();
    expect(coaching.errorMessage()).toBe('Çok fazla hatalı doğrulama denemesi yapıldı. Biraz sonra yeniden dene.');
    coaching.email = 'a@example.test'; coaching.password = 'x';
    await coaching.onSubmit(new Event('submit'));
    expect(coaching.errorMessage()).toBe('Sistem şu anda bakımda. Biraz sonra yeniden dene.');

    TestBed.resetTestingModule();
    const management = createComponent(auth, 'siz');
    management.mfaChallengeToken = 'c'; management.mfaStage.set('verify'); management.mfaCode = '123456';
    await management.submitMfa();
    expect(management.errorMessage()).toBe('Çok fazla hatalı deneme yapıldı. Lütfen daha sonra tekrar deneyin.');
  });

  // Paket F F4 (live 07.11.2026): a Koçluk account without a Koçluk role must get the reason, not the management shell.
  it('shows the join hint when the account has no role in this platform', async () => {
    const roleLess = { error: { code: 'Auth.NoRoleForProduct', description: 'Bu platformda hesabınıza tanımlı bir yetki bulunmuyor.' } };
    const coaching = createComponent({ loginWithPassword: vi.fn().mockRejectedValue(roleLess) });
    coaching.email = 'a@example.test'; coaching.password = 'x';
    await coaching.onSubmit(new Event('submit'));
    expect(coaching.errorMessage()).toBe(
      'Bu platformda hesabına tanımlı bir yetki kalmadı. Yeniden erişim için kurumunla ya da destekle iletişime geç.');

    TestBed.resetTestingModule();
    const management = createComponent({ loginWithPassword: vi.fn().mockRejectedValue(roleLess) }, 'siz');
    management.email = 'a@example.test'; management.password = 'x';
    await management.onSubmit(new Event('submit'));
    expect(management.errorMessage()).toBe('Bu platformda hesabınıza tanımlı bir yetki bulunmuyor.');
  });

  it('leaves MFA and shows the reason when the verified session has no role', async () => {
    const component = createComponent({ verifyMfa: vi.fn().mockRejectedValue(noRoleForProductError()) });
    component.mfaChallengeToken = 'challenge'; component.mfaStage.set('verify'); component.mfaCode = '123456';
    await component.submitMfa();
    expect(component.mfaStage()).toBe('none');
    expect(component.mfaChallengeToken).toBe('');
    expect(component.errorMessage()).toContain('tanımlı bir yetki kalmadı');
  });

  // PR #125 review L4: the notice left by a guard or a refused refresh, also when it arrives after the page opened.
  it('shows the notice left by a guard or a refused refresh that ended a role-less session', () => {
    const notice = signal<ReturnType<typeof noRoleForProductError> | null>(null);
    const component = createComponent({
      signInNotice: notice,
      consumeSignInNotice: vi.fn(() => { const value = notice(); notice.set(null); return value; })
    });
    TestBed.tick();
    expect(component.errorMessage()).toBeNull();
    notice.set(noRoleForProductError());
    TestBed.tick();
    expect(component.errorMessage()).toBe(
      'Bu platformda hesabına tanımlı bir yetki kalmadı. Yeniden erişim için kurumunla ya da destekle iletişime geç.');
    expect(notice()).toBeNull();
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
