import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AccountSecurityComponent } from './account-security.component';

// Live test 16.11.2026 (#56): the Hızlı Okuma student changes password and e-mail address like in Koçluk.
describe('AccountSecurityComponent', () => {
  function setup() {
    TestBed.configureTestingModule({ imports: [AccountSecurityComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(AccountSecurityComponent);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne(req => req.url.endsWith('/users/me') && req.method === 'GET').flush({ email: 'ogrenci@ornek.com', mfaEnabled: false });
    fixture.detectChanges();
    return { fixture, http, component: fixture.componentInstance, el: fixture.nativeElement as HTMLElement };
  }

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('sends the new address with the current password and shows that it waits for confirmation', () => {
    const { fixture, http, component, el } = setup();
    component.newEmail = 'Yeni@Ornek.com'; component.emailPassword = 'Eski-Sifre1';
    component.changeEmail();
    const request = http.expectOne(req => req.url.endsWith('/users/me/email-change'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ currentPassword: 'Eski-Sifre1', newEmail: 'yeni@ornek.com' });
    request.flush({ pendingEmail: 'yeni@ornek.com', expiresAt: '2026-11-18T10:00:00Z' });
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="pending-email"]')?.textContent).toContain('yeni@ornek.com');
  });

  it('checks the new password before sending it and then changes it', () => {
    const { fixture, http, component, el } = setup();
    component.currentPassword = 'Eski-Sifre1'; component.newPassword = 'kisa'; component.confirmPassword = 'kisa';
    component.changePassword(); fixture.detectChanges();
    expect(el.querySelector('[data-testid="password-message"]')?.textContent).toContain('kurallara uymuyor');
    component.newPassword = 'Yeni-Sifre2'; component.confirmPassword = 'Yeni-Sifre2';
    component.changePassword();
    const request = http.expectOne(req => req.url.endsWith('/users/me/change-password'));
    expect(request.request.body).toEqual({ currentPassword: 'Eski-Sifre1', newPassword: 'Yeni-Sifre2' });
    request.flush({});
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="password-message"]')?.textContent).toContain('Şifren değiştirildi.');
  });
});
