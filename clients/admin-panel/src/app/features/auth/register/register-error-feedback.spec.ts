import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { LocationService } from '../../../core/services/location.service';
import { ConfigurationService } from '../../../core/services/settings/configuration.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { InstitutionRegisterComponent } from './institution/institution-register.component';
import { ParentRegisterComponent } from './parent/parent-register.component';
import { StudentRegisterComponent } from './student/student-register.component';
import { TeacherRegisterComponent } from './teacher/teacher-register.component';

type Voice = 'sen' | 'siz';
interface Form {
  isLoading(): boolean;
  errorMessage(): string | null;
  form: { setValue(value: unknown): void; valid: boolean; controls: { password: { setValue(v: string): void; valid: boolean } } };
  legalReady: boolean;
  legalAcceptances: unknown[];
  onSubmit(): Promise<void>;
}

const password = 'Sifre-2026';
// Paket F #12 (07.11.2026): every Coaching sign-up form says sen, teachers and institutions too.
const forms: Array<{ name: string; voice: Voice; path: string; create: () => Form; value: Record<string, unknown> }> = [
  {
    name: 'student', voice: 'sen', path: '/auth/coaching/register/student',
    create: () => new StudentRegisterComponent() as unknown as Form,
    value: { firstName: 'Elif', lastName: 'Kaya', email: 'elif@example.test', password, confirmPassword: password },
  },
  {
    name: 'parent', voice: 'sen', path: '/auth/coaching/register/parent',
    create: () => new ParentRegisterComponent() as unknown as Form,
    value: { firstName: 'Ayşe', lastName: 'Kaya', email: 'ayse@example.test', password, confirmPassword: password, phoneNumber: '5551234567' },
  },
  {
    name: 'teacher', voice: 'sen', path: '/auth/coaching/register/teacher',
    create: () => new TeacherRegisterComponent() as unknown as Form,
    value: { firstName: 'Ali', lastName: 'Demir', email: 'ali@example.test', password, confirmPassword: password, branch: 'Matematik' },
  },
  {
    name: 'institution', voice: 'sen', path: '/auth/coaching/register/institution',
    create: () => new InstitutionRegisterComponent() as unknown as Form,
    value: { institutionName: 'Okul', institutionType: 1, provinceId: '34', districtId: '3401', firstName: 'Ali', lastName: 'Demir', email: 'ali@example.test', password, confirmPassword: password },
  },
];

const policy = 'Şifre 8-128 karakter arasında olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.';
const expected = (voice: Voice) => ({
  validation: `${policy} Geçerli bir e-posta adresi giriniz.`,
  conflict: voice === 'sen'
    ? 'Bu e-posta adresiyle zaten bir hesap var. Giriş yapabilir ya da şifreni sıfırlayabilirsin.'
    : 'Bu e-posta adresiyle zaten bir hesap var. Giriş yapabilir ya da şifrenizi sıfırlayabilirsiniz.',
  rateLimit: voice === 'sen'
    ? 'Çok fazla deneme yapıldı. Birkaç dakika bekleyip yeniden dene.'
    : 'Çok fazla deneme yapıldı. Lütfen birkaç dakika bekleyip yeniden deneyin.',
  network: voice === 'sen'
    ? 'Sunucuya ulaşılamadı. İnternet bağlantını kontrol edip yeniden dene.'
    : 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip yeniden deneyin.',
  server: voice === 'sen'
    ? 'Kayıt tamamlanamadı. Biraz sonra yeniden dene.'
    : 'Kayıt tamamlanamadı. Lütfen biraz sonra yeniden deneyin.',
});

function create(entry: (typeof forms)[number]) {
  const toaster = { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(), provideHttpClientTesting(),
      { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      { provide: ToasterService, useValue: toaster },
      { provide: ConfigurationService, useValue: {} },
      { provide: AuthService, useValue: {} },
      { provide: SocialAuthService, useValue: { authState: new Subject() } },
      { provide: LocationService, useValue: { getProvinces: () => new Subject(), getDistricts: () => new Subject() } },
    ],
  });
  const component = TestBed.runInInjectionContext(entry.create);
  component.legalReady = true;
  component.legalAcceptances = [{ slug: 'privacy', version: 1 }, { slug: 'kvkk', version: 1 }, { slug: 'coaching-terms', version: 1 }];
  component.form.setValue(entry.value);
  return { component, toaster };
}

describe.each(forms)('$name registration feedback', entry => {
  const messages = expected(entry.voice);

  it('accepts the same passwords as the server', () => {
    const { component } = create(entry);
    for (const valid of ['Sifre-2026', 'Sifre_2026', 'Sifre 2026']) {
      component.form.controls.password.setValue(valid);
      expect(component.form.controls.password.valid).toBe(true);
    }
    for (const invalid of ['Sifre2026', 'sifre-2026', 'Sifre-2026' + 'x'.repeat(119)]) {
      component.form.controls.password.setValue(invalid);
      expect(component.form.controls.password.valid).toBe(false);
    }
  });

  it.each([
    ['validation', 400, { title: 'Validation Error', detail: 'One or more validation errors occurred.', errors: { Password: [policy], Email: ['Geçerli bir e-posta adresi giriniz.'] } }],
    ['conflict', 409, null],
    ['rateLimit', 429, { title: 'Too Many Requests' }],
    ['network', 0, null],
    ['server', 500, { title: 'Internal Server Error', detail: 'An unexpected error occurred.' }],
  ] as const)('shows a clear Turkish message for %s and recovers', async (kind, status, body) => {
    const { component, toaster } = create(entry);

    await component.onSubmit();
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne(candidate => candidate.url.endsWith(entry.path));
    if (status === 0) request.error(new ProgressEvent('error'), { status: 0, statusText: 'Unknown Error' });
    else request.flush(body, { status, statusText: 'x' });

    expect(component.errorMessage()).toBe(messages[kind]);
    expect(toaster.error).toHaveBeenCalledWith(messages[kind]);
    expect(component.isLoading()).toBe(false);
    http.verify();
  });
});
