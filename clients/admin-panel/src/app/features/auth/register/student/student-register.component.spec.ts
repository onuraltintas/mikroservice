import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { AuthService } from '../../../../core/auth/auth.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';
import { StudentRegisterComponent } from './student-register.component';

describe('StudentRegisterComponent Google registration', () => {
  it('starts Google registration from the social auth state', async () => {
    const authState = new Subject<{ idToken?: string }>();
    const authService = {
      loginWithGoogle: vi.fn().mockResolvedValue({ requiresMfa: false })
    };
    const toaster = {
      success: vi.fn(),
      error: vi.fn(),
      warning: vi.fn(),
      info: vi.fn()
    };
    createComponent(authState, authService, toaster);

    authState.next({ idToken: 'google-id-token' });

    await vi.waitFor(() => expect(authService.loginWithGoogle).toHaveBeenCalledWith('google-id-token'));
    expect(toaster.success).toHaveBeenCalledWith('Google hesabınızla devam edildi.');
  });

  it('sends MFA-required Google accounts back to the login flow', async () => {
    const authState = new Subject<{ idToken?: string }>();
    const authService = {
      loginWithGoogle: vi.fn().mockResolvedValue({ requiresMfa: true })
    };
    const router = { navigate: vi.fn().mockResolvedValue(true) };
    const toaster = {
      success: vi.fn(),
      error: vi.fn(),
      warning: vi.fn(),
      info: vi.fn()
    };
    createComponent(authState, authService, toaster, router);

    authState.next({ idToken: 'google-mfa-token' });

    await vi.waitFor(() => expect(router.navigate).toHaveBeenCalledWith(['/auth/login']));
    expect(toaster.info).toHaveBeenCalled();
  });

  it('shows a registration-disabled message without redirecting unexpectedly', async () => {
    const authState = new Subject<{ idToken?: string }>();
    const authService = {
      loginWithGoogle: vi.fn().mockRejectedValue({
        error: { code: 'Identity.RegistrationDisabled', message: 'Kayıt kapalı.' }
      })
    };
    const toaster = {
      success: vi.fn(),
      error: vi.fn(),
      warning: vi.fn(),
      info: vi.fn()
    };
    const component = createComponent(authState, authService, toaster);

    authState.next({ idToken: 'google-disabled-token' });

    await vi.waitFor(() => expect(toaster.info).toHaveBeenCalledWith('Yeni kullanıcı kayıtları şu an kapalıdır.'));
    expect(component.errorMessage()).toBe('Kayıt kapalı.');
  });

  function createComponent(
    authState: Subject<{ idToken?: string }>,
    authService: Record<string, unknown>,
    toaster: Record<string, unknown>,
    router: Record<string, unknown> = { navigate: vi.fn().mockResolvedValue(true) }
  ): StudentRegisterComponent {
    TestBed.configureTestingModule({
      imports: [ReactiveFormsModule],
      providers: [
        provideHttpClient(),
        { provide: AuthService, useValue: authService },
        { provide: SocialAuthService, useValue: { authState } },
        { provide: Router, useValue: router },
        { provide: ToasterService, useValue: toaster },
        { provide: ConfigurationService, useValue: {} }
      ]
    });
    return TestBed.runInInjectionContext(() => new StudentRegisterComponent());
  }
});
