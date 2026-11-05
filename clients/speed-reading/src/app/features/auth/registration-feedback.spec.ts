import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { GoogleIdentityService } from '../../core/services/google-identity.service';
import { LocationsService } from '../../core/services/locations.service';
import { ToasterService } from '../../core/services/toaster.service';
import { RegisterSchoolComponent } from './register-school/register-school.component';
import { RegisterTeacherComponent } from './register-teacher/register-teacher.component';
import { RegisterComponent } from './register/register.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';

type Voice = 'sen' | 'siz';
const policy = 'Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.';
const acceptances = ['privacy', 'kvkk', 'speed-reading-terms'].map(slug => ({ slug, version: 1 }));
const password = 'Sifre-2026';

interface Harness { submit(): void; error(): string; busy(): boolean; setPassword(value: string): void; passwordValid(): boolean; }

const forms: Array<{ name: string; voice: Voice; method: string; fallback: string; create(): Harness }> = [
  {
    name: 'student registration', voice: 'sen', method: 'register', fallback: 'Kayıt tamamlanamadı. Biraz sonra yeniden dene.',
    create: () => {
      const c = TestBed.runInInjectionContext(() => new RegisterComponent());
      c.registerForm.setValue({ firstName: 'Elif', lastName: 'Kaya', email: 'elif@example.test', password, confirmPassword: password });
      c.legalReady = true; c.legalAcceptances = acceptances;
      return { submit: () => c.onSubmit(), error: () => c.error, busy: () => c.loading,
        setPassword: v => c.registerForm.controls['password'].setValue(v), passwordValid: () => c.registerForm.controls['password'].valid };
    },
  },
  {
    name: 'teacher registration', voice: 'siz', method: 'registerTeacher', fallback: 'Kayıt tamamlanamadı. Lütfen biraz sonra yeniden deneyin.',
    create: () => {
      const c = TestBed.runInInjectionContext(() => new RegisterTeacherComponent());
      c.registerForm.setValue({ firstName: 'Ali', lastName: 'Demir', email: 'ali@example.test', password, confirmPassword: password });
      c.legalReady = true; c.legalAcceptances = acceptances;
      return { submit: () => c.onSubmit(), error: () => c.error, busy: () => c.isLoading,
        setPassword: v => c.registerForm.controls.password.setValue(v), passwordValid: () => c.registerForm.controls.password.valid };
    },
  },
  {
    name: 'school registration', voice: 'siz', method: 'registerInstitution', fallback: 'Kayıt tamamlanamadı. Lütfen biraz sonra yeniden deneyin.',
    create: () => {
      const c = TestBed.runInInjectionContext(() => new RegisterSchoolComponent());
      c.registerForm.setValue({ schoolName: 'Okul', contactEmail: 'okul@example.test', phoneNumber: '5551234567', address: '',
        provinceId: '34', districtId: '3401', firstName: 'Ali', lastName: 'Demir', adminEmail: 'ali@example.test', password, confirmPassword: password });
      c.legalReady = true; c.legalAcceptances = acceptances;
      return { submit: () => c.onSubmit(), error: () => c.error, busy: () => c.isLoading,
        setPassword: v => c.registerForm.controls.password.setValue(v), passwordValid: () => c.registerForm.controls.password.valid };
    },
  },
  {
    name: 'password reset', voice: 'siz', method: 'resetPassword', fallback: 'Şifre sıfırlama başarısız. Lütfen tekrar deneyin veya yeni bir bağlantı isteyin.',
    create: () => {
      const c = TestBed.runInInjectionContext(() => new ResetPasswordComponent());
      c.ngOnInit();
      c.resetPasswordForm.setValue({ newPassword: password, confirmPassword: password });
      return { submit: () => c.onSubmit(), error: () => c.error, busy: () => c.loading,
        setPassword: v => c.resetPasswordForm.controls['newPassword'].setValue(v), passwordValid: () => c.resetPasswordForm.controls['newPassword'].valid };
    },
  },
];

function configure(method: string, response: () => unknown) {
  const auth = jasmine.createSpyObj('AuthService', [method, 'googleAuth']);
  auth[method].and.callFake(response);
  TestBed.configureTestingModule({ providers: [
    { provide: AuthService, useValue: auth },
    { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) },
    { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } }, queryParams: of({ email: 'a@example.test', token: 't' }) } },
    { provide: GoogleIdentityService, useValue: {} },
    { provide: ToasterService, useValue: jasmine.createSpyObj('ToasterService', ['error', 'success', 'info']) },
    { provide: LocationsService, useValue: { getProvinces: () => new Subject(), getDistricts: () => new Subject() } },
  ] });
}

forms.forEach(entry => {
  describe(`${entry.name} feedback`, () => {
    const sen = entry.voice === 'sen';
    const cases: Array<[string, number, unknown, string]> = [
      ['validation', 400, { title: 'Validation Error', detail: 'One or more validation errors occurred.', errors: { Password: [policy] } }, policy],
      ['described error', 400, { code: 'ResetPassword.InvalidRequest', description: 'Sıfırlama bağlantısı geçersiz.' }, 'Sıfırlama bağlantısı geçersiz.'],
      ['rate limit', 429, null, sen ? 'Çok fazla deneme yapıldı. Birkaç dakika bekleyip yeniden dene.' : 'Çok fazla deneme yapıldı. Lütfen birkaç dakika bekleyip yeniden deneyin.'],
      ['network', 0, null, sen ? 'Sunucuya ulaşılamadı. İnternet bağlantını kontrol edip yeniden dene.' : 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip yeniden deneyin.'],
      ['server error', 500, { detail: 'An unexpected error occurred.' }, entry.fallback],
    ];

    it('accepts the same passwords as the server', () => {
      configure(entry.method, () => of({}));
      const form = entry.create();
      for (const valid of ['Sifre-2026', 'Sifre 2026', 'Sifre€2026']) { form.setPassword(valid); expect(form.passwordValid()).withContext(valid).toBeTrue(); }
      for (const invalid of ['Sifre2026', 'Sifre-2026' + 'x'.repeat(119)]) { form.setPassword(invalid); expect(form.passwordValid()).withContext(invalid).toBeFalse(); }
    });

    cases.forEach(([kind, status, body, message]) => {
      it(`shows a clear Turkish message for ${kind} and recovers`, () => {
        configure(entry.method, () => throwError(() => new HttpErrorResponse({ status, error: body, url: '/api/auth/x' })));
        const form = entry.create();

        form.submit();

        expect(form.error()).toBe(message);
        expect(form.busy()).toBeFalse();
      });
    });
  });
});
