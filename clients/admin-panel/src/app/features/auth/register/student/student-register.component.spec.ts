import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
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
  it('shows the duplicate-account message in a toaster on student registration', () => {
    const authState = new Subject<{ idToken?: string }>();
    const toaster = { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() };
    const component = createComponent(authState, {}, toaster);
    component.form.patchValue({ firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.com', password: 'StrongPass1!', confirmPassword: 'StrongPass1!' });
    void component.onSubmit();
    TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/auth/register-student'))
      .flush({ code: 'Identity.UserExists', message: 'Bu e-posta adresiyle zaten kayıtlı bir hesap var.' }, { status: 409, statusText: 'Conflict' });
    expect(toaster.error).toHaveBeenCalledWith('Bu e-posta adresiyle zaten kayıtlı bir hesap var.');
    TestBed.inject(HttpTestingController).verify();
  });

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
        provideHttpClientTesting(),
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
