import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { NEVER, Subject, of } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { LocationService } from '../../../core/services/location.service';
import { ConfigurationService } from '../../../core/services/settings/configuration.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { IdentityService } from '../../../core/services/identity.service';
import { RegistrationLegalConsentComponent } from '../registration-legal-consent.component';
import { InstitutionRegisterComponent } from './institution/institution-register.component';
import { ParentRegisterComponent } from './parent/parent-register.component';
import { RegisterComponent } from './register.component';
import { StudentRegisterComponent } from './student/student-register.component';
import { TeacherRegisterComponent } from './teacher/teacher-register.component';

// Paket F #11–#13 (07.11.2026): the sign-up pages on onuraltintas.net say "sen", show no internal wording, and every
// form ends with a way back to sign-in.
const formal = /Hoş Geldiniz|hesabınız|giriniz|Seçiniz|Adınız|Soyadınız|Kayıt Olun|yönetin\.|yaratın\.|deneyiniz/;

function render<T>(component: Type<T>) {
  TestBed.configureTestingModule({
    imports: [component],
    providers: [
      provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
      { provide: ConfigurationService, useValue: { getPublicConfigurationValue: () => of('"true"') } },
      { provide: ToasterService, useValue: { success() {}, error() {}, info() {}, warning() {} } },
      { provide: AuthService, useValue: {} },
      { provide: SocialAuthService, useValue: { authState: new Subject(), initState: NEVER } },
      { provide: LocationService, useValue: { getProvinces: () => of([]), getDistricts: () => of([]) } },
    ],
  });
  const fixture = TestBed.createComponent(component);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('sign-up pages', () => {
  it('role selection says sen and leads to sign-in', () => {
    const el = render(RegisterComponent);
    expect(el.textContent).toContain('Hoş geldin');
    expect(el.textContent).not.toMatch(formal);
    const login = el.querySelector('[data-testid="role-login-link"] a') as HTMLAnchorElement;
    expect(login.getAttribute('href')).toBe('/auth/login');
    expect(login.textContent).toContain('Giriş yap');
  });

  for (const [name, component] of [
    ['student', StudentRegisterComponent], ['parent', ParentRegisterComponent],
    ['teacher', TeacherRegisterComponent], ['institution', InstitutionRegisterComponent],
  ] as const) {
    it(`the ${name} form says sen and ends with "Zaten hesabın var mı? Giriş yap" under the submit button`, () => {
      const el = render(component as Type<unknown>);
      expect(el.textContent).not.toMatch(formal);
      const link = el.querySelector('[data-testid="register-login-link"]') as HTMLElement;
      expect(link.textContent?.replace(/\s+/g, ' ').trim()).toBe('Zaten hesabın var mı? Giriş yap');
      expect(link.querySelector('a')?.getAttribute('href')).toBe('/auth/login');
      const submit = el.querySelector('form button[type="submit"]') as HTMLElement;
      expect(submit.compareDocumentPosition(link) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      // "Geri Dön" and ✕ still lead back to role selection.
      expect(el.querySelector('a[href="/auth/register"]')).not.toBeNull();
    });
  }

  it('shows no internal wording under the legal texts', () => {
    TestBed.configureTestingModule({
      imports: [RegistrationLegalConsentComponent],
      providers: [provideRouter([]), { provide: IdentityService, useValue: {
        getPublicLegalPage: (slug: string) => of({ slug, title: slug.toUpperCase(), version: 3 }) } }],
    });
    const fixture = TestBed.createComponent(RegistrationLegalConsentComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('input[type="checkbox"]')).toHaveLength(3);
    expect(el.textContent).not.toContain('Identity');
    expect(el.textContent).not.toContain('taslak');
    expect(el.querySelector('[data-testid="legal-consent-note"]')?.textContent).toContain('Onayladığın');
  });
});
