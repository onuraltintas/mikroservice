import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Component, output } from '@angular/core';
import { provideRouter } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { Subject } from 'rxjs';
import { StaffAuthRecaptchaService } from '../../../../staff-portal/src/app/auth/staff-auth-recaptcha.service';
import { GoogleButtonComponent } from './google-button.component';
import { LegalConsentComponent } from './legal-consent.component';
import { RegisterComponent } from './register.component';

@Component({ selector: 'mk-google-button', standalone: true, template: '' })
class GoogleButtonStub { readonly token = output<string>(); }
@Component({ selector: 'mk-legal-consent', standalone: true, template: '' })
class LegalConsentStub { readonly accepted = output<unknown>(); }

describe('RegisterComponent', () => {
  const tick = () => new Promise(resolve => setTimeout(resolve));

  function setup() {
    TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        { provide: SocialAuthService, useValue: { authState: new Subject() } },
        { provide: StaffAuthRecaptchaService, useValue: { createToken: () => Promise.resolve(null) } }]
    });
    TestBed.overrideComponent(RegisterComponent, {
      remove: { imports: [GoogleButtonComponent, LegalConsentComponent] }, add: { imports: [GoogleButtonStub, LegalConsentStub] }
    });
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http: TestBed.inject(HttpTestingController) };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('asks the institution\'s name, a phone and the consents before sending anything', async () => {
    const { component, http } = setup();
    component.choose('institution');
    Object.assign(component, { firstName: 'TEST', lastName: 'Kişi', email: 'test@example.com', password: 'Guclu-Sifre1', passwordAgain: 'Guclu-Sifre1', phone: '123' });
    await component.submit();
    expect(Object.keys(component.errors()).sort()).toEqual(['legal', 'phone', 'workspaceName']);
    http.expectNone('/api/auth/course-management/register');
  });

  it('asks for the password twice and stops when the two differ (20.11.2026 live test)', async () => {
    const { component, http } = setup();
    component.choose('institution');
    Object.assign(component, { firstName: 'TEST', lastName: 'Kişi', email: 'test@example.com', password: 'Guclu-Sifre1', passwordAgain: 'Guclu-Sifre2',
      phone: '0532 123 45 67', workspaceName: 'TEST Dershanesi' });
    await component.submit();
    expect(component.errors()['passwordAgain']).toContain('aynı değil');
    http.expectNone('/api/auth/course-management/register');
  });

  it('opens an institution and shows the verification step', async () => {
    const { fixture, component, http } = setup();
    component.choose('institution');
    Object.assign(component, { firstName: 'TEST', lastName: 'Kişi', email: 'test@example.com', password: 'Guclu-Sifre1', passwordAgain: 'Guclu-Sifre1', phone: '0532 123 45 67', workspaceName: 'TEST Dershanesi' });
    component.legal.set([{ slug: 'kvkk', version: 1 }, { slug: 'privacy', version: 1 }, { slug: 'course-management-terms', version: 1 }]);
    const sending = component.submit();
    const request = http.expectOne('/api/auth/course-management/register');
    expect(request.request.body).toMatchObject({ kind: 'institution', workspaceName: 'TEST Dershanesi', phone: '0532 123 45 67', mfaCode: null });
    request.flush({ userId: 'u1', verificationEmail: 'sent' });
    await sending;
    await tick();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('E-postanızı doğrulayın');
  });

  it('asks the existing account\'s code when it has two-step verification', async () => {
    const { component, http } = setup();
    component.choose('teacher');
    Object.assign(component, { firstName: 'TEST', lastName: 'Kişi', email: 'test@example.com', password: 'Eski-Sifre1', passwordAgain: 'Eski-Sifre1', phone: '05321234567' });
    component.legal.set([{ slug: 'kvkk', version: 1 }]);
    const sending = component.submit();
    http.expectOne('/api/auth/course-management/register').flush(
      { code: 'Auth.ExistingAccountMfaRequired', description: 'Mevcut hesabınızın doğrulama uygulamasındaki altı haneli kodu girin.' },
      { status: 400, statusText: 'Bad Request' });
    await sending;
    expect(component.needsCode()).toBe(true);
    expect(component.error()).toContain('altı haneli kodu');
  });
});
