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

describe('StudentRegisterComponent registration', () => {
  // Roadmap Y8: registration asks for the student's birth date and sends it as a calendar date.
  it('requires a real past birth date and sends it with the registration', async () => {
    const component = createComponent(new Subject(), {}, {}, { navigate: vi.fn() });
    const values = { firstName: 'Test', lastName: 'Student', email: 'new@example.test', password: 'NewPassword1!', confirmPassword: 'NewPassword1!' };
    component.form.setValue({ ...values, birthDate: '' });
    expect(component.form.invalid).toBe(true);
    component.form.setValue({ ...values, birthDate: '2999-01-01' });
    expect(component.form.controls.birthDate.hasError('birthDate')).toBe(true);
    component.form.setValue({ ...values, birthDate: '2011-05-03' });
    await component.onSubmit();
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne(candidate => candidate.url.endsWith('/auth/coaching/register/student'));
    expect(request.request.body.birthDate).toBe('2011-05-03');
    expect(request.request.body.dateOfBirth).toBeUndefined();
    request.flush({ userId: 'new-user' });
    http.verify();
  });

  it('shows the actionable API conflict description instead of a generic registration error', async () => {
    const toaster = { error: vi.fn() };
    const component = createComponent(new Subject(), {}, toaster);
    component.form.setValue({ firstName: 'Test', lastName: 'Student', email: 'existing@example.test',
      password: 'ExistingPassword1!', confirmPassword: 'ExistingPassword1!', birthDate: '2011-05-03' });
    await component.onSubmit();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(request => request.url.endsWith('/auth/coaching/register/student')).flush({
      code: 'Identity.UserExists', description: 'Bu hesabın Koçluk kaydı zaten var. Giriş yapın.'
    }, { status: 409, statusText: 'Conflict' });
    expect(component.errorMessage()).toBe('Bu hesabın Koçluk kaydı zaten var. Giriş yapın.');
    expect(toaster.error).toHaveBeenCalledWith(component.errorMessage());
    expect(component.isLoading()).toBe(false);
    http.verify();
  });

  it('sends existing credentials and current Coaching acceptances without changing the password', async () => {
    const toaster = { success: vi.fn() };
    const router = { navigate: vi.fn() };
    const component = createComponent(new Subject(), {}, toaster, router);
    component.errorMessage.set('Previous error');
    component.form.setValue({ firstName: 'Test', lastName: 'Student', email: 'existing@example.test',
      password: 'ExistingPassword1!', confirmPassword: 'ExistingPassword1!', birthDate: '2011-05-03' });
    await component.onSubmit();
    expect(component.errorMessage()).toBeNull();
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne(candidate => candidate.url.endsWith('/auth/coaching/register/student'));
    expect(request.request.body.password).toBe('ExistingPassword1!');
    expect(request.request.body.legalAcceptances).toEqual(component.legalAcceptances);
    request.flush({ userId: 'existing-user' });
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { queryParams: { registered: 'true', role: 'student' } });
    expect(toaster.success).toHaveBeenCalled();
    http.verify();
  });
  it('does not send Google registration without current legal acceptances', async () => {
    const authState = new Subject<{ idToken?: string }>();
    const authService = { loginWithGoogle: vi.fn() };
    const toaster = { error: vi.fn(), info: vi.fn() };
    const component = createComponent(authState, authService, toaster);
    component.legalReady = false;
    component.legalAcceptances = [];
    authState.next({ idToken: 'google-token' });
    await Promise.resolve();
    expect(authService.loginWithGoogle).not.toHaveBeenCalled();
    expect(toaster.info).toHaveBeenCalled();
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

    await vi.waitFor(() => expect(authService.loginWithGoogle).toHaveBeenCalledWith('google-id-token', [
      { slug: 'privacy', version: 1 }, { slug: 'kvkk', version: 1 }, { slug: 'coaching-terms', version: 1 }
    ]));
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
    const component = TestBed.runInInjectionContext(() => new StudentRegisterComponent());
    component.legalReady = true;
    component.legalAcceptances = [
      { slug: 'privacy', version: 1 }, { slug: 'kvkk', version: 1 }, { slug: 'coaching-terms', version: 1 }
    ];
    return component;
  }
});
