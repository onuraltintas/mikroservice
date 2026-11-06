import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { COACHING_PORTAL_ROUTES } from '../coaching-portal.routes';
import { CoachingAccountComponent } from './coaching-account.component';

// E5 (#22, live test 06.11.2026): Profil ve ayarlar › Hesabım — name, e-mail (verified through the new address) and
// password for students and parents; Google-only accounts are told so and may ask for a password link.
const me = (over: Record<string, unknown> = {}) => ({
  userId: 'u1', email: 'ada@example.test', firstName: 'Ada', lastName: 'Kaya', fullName: 'Ada Kaya', role: 'Student',
  isActive: true, emailConfirmed: true, mfaEnabled: false, hasPassword: true, pendingEmail: null, pendingEmailExpiresAt: null,
  roles: ['Student'], permissions: [], ...over
});

describe('Hesabım', () => {
  let auth: { refreshSession: ReturnType<typeof vi.fn>; logout: ReturnType<typeof vi.fn> };

  async function render(profile = me()) {
    auth = { refreshSession: vi.fn(async () => true), logout: vi.fn(async () => undefined) };
    TestBed.configureTestingModule({ imports: [CoachingAccountComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: AuthService, useValue: auth }] });
    const fixture = TestBed.createComponent(CoachingAccountComponent);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.method === 'GET' && r.url === '/api/users/me').flush(profile);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const type = (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!; input.value = value; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    };
    const submit = async (form: string) => {
      el.querySelector<HTMLFormElement>(`form[data-testid="${form}"]`)!.dispatchEvent(new Event('submit')); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    };
    const settle = async () => { fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); };
    return { fixture, el, http, type, submit, settle };
  }

  it('is a tab of Profil ve ayarlar for students and a page for parents', async () => {
    const settings = COACHING_PORTAL_ROUTES.find(r => r.path === 'account/settings')!;
    expect(await settings.children!.find(c => c.path === 'account')!.loadComponent!()).toBe(CoachingAccountComponent);
    const parent = COACHING_PORTAL_ROUTES.find(r => r.path === 'parent/account/settings')!;
    expect(parent.data?.['coachingRoles']).toEqual(['Parent']);
    expect(await parent.loadComponent!()).toBe(CoachingAccountComponent);
  });

  it('shows the name, the e-mail address and the password forms without browser validation', async () => {
    const { el } = await render();
    expect(el.querySelector('h1')?.textContent?.trim()).toBe('Hesabım');
    expect(el.querySelector<HTMLInputElement>('input[name="firstName"]')!.value).toBe('Ada');
    expect(el.querySelector<HTMLInputElement>('input[name="lastName"]')!.value).toBe('Kaya');
    expect(el.textContent).toContain('ada@example.test');
    for (const form of ['name-form', 'email-form', 'password-form'])
      expect(el.querySelector(`form[data-testid="${form}"]`)!.hasAttribute('novalidate')).toBe(true);
    expect(el.querySelector('app-password-requirements')).not.toBeNull();
    expect(el.textContent).not.toContain('İki adımlı doğrulama');
  });

  it('marks a missing name at the field and in a summary, and sends nothing', async () => {
    const { el, http, type, submit } = await render();
    type('input[name="firstName"]', ' ');
    await submit('name-form');
    const input = el.querySelector<HTMLInputElement>('input[name="firstName"]')!;
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(input.getAttribute('aria-describedby')).toBe('account-firstName-error');
    expect(el.querySelector('#account-firstName-error')?.textContent).toContain('Adını yaz');
    expect(el.querySelector('[data-testid="name-form-summary"][role="alert"]')?.textContent).toContain('Adın kaydedilmedi');
    http.expectNone(r => r.method === 'PUT');
  });

  it('saves the name, renews the session so the header shows it, and clears the old message on the next attempt', async () => {
    const { el, http, type, submit, settle } = await render();
    type('input[name="firstName"]', ' ');
    await submit('name-form');
    type('input[name="firstName"]', 'Ayşe');
    await submit('name-form');
    expect(el.querySelector('[data-testid="name-form-summary"]')).toBeNull();
    const put = http.expectOne(r => r.method === 'PUT' && r.url === '/api/users/me');
    expect(put.request.body).toEqual({ firstName: 'Ayşe', lastName: 'Kaya' });
    put.flush(null); await settle();
    expect(auth.refreshSession).toHaveBeenCalled();
    expect(el.querySelector('[data-testid="name-form-status"][role="status"]')?.textContent).toContain('Adın ve soyadın kaydedildi');
  });

  it('asks for a valid new address and the current password before sending a verification link', async () => {
    const { el, http, type, submit, settle } = await render();
    type('input[name="newEmail"]', 'adres-degil');
    await submit('email-form');
    expect(el.querySelector('#account-newEmail-error')?.textContent).toContain('geçerli bir e-posta');
    expect(el.querySelector('#account-emailPassword-error')?.textContent).toContain('Mevcut şifreni yaz');
    http.expectNone(r => r.url === '/api/users/me/email-change');

    type('input[name="newEmail"]', 'yeni@example.test');
    type('input[name="emailPassword"]', 'Current-Password-1!');
    await submit('email-form');
    const post = http.expectOne(r => r.method === 'POST' && r.url === '/api/users/me/email-change');
    expect(post.request.body).toEqual({ currentPassword: 'Current-Password-1!', newEmail: 'yeni@example.test' });
    post.flush({ pendingEmail: 'yeni@example.test', expiresAt: '2026-11-07T10:00:00Z' }); await settle();
    expect(el.querySelector('[data-testid="email-form-status"]')?.textContent).toContain('yeni@example.test');
    expect(el.querySelector('[data-testid="pending-email"]')?.textContent).toContain('yeni@example.test');
    expect(el.textContent).toContain('ada@example.test');
    expect(el.querySelector<HTMLInputElement>('input[name="emailPassword"]')?.value ?? '').toBe('');
  });

  it('shows a wrong current password at its field', async () => {
    const { el, http, type, submit, settle } = await render();
    type('input[name="newEmail"]', 'yeni@example.test');
    type('input[name="emailPassword"]', 'Wrong-Password-1!');
    await submit('email-form');
    http.expectOne(r => r.url === '/api/users/me/email-change')
      .flush({ error: { code: 'Auth.InvalidCredentials', description: 'Mevcut şifre hatalı.' } }, { status: 400, statusText: 'Bad Request' });
    await settle();
    expect(el.querySelector<HTMLInputElement>('input[name="emailPassword"]')!.getAttribute('aria-invalid')).toBe('true');
    expect(el.querySelector('#account-emailPassword-error')?.textContent).toContain('Mevcut şifre hatalı');
  });

  it('shows a pending change and lets it be cancelled', async () => {
    const { el, http, settle } = await render(me({ pendingEmail: 'yeni@example.test', pendingEmailExpiresAt: '2026-11-07T10:00:00Z' }));
    expect(el.querySelector('[data-testid="pending-email"]')?.textContent).toContain('yeni@example.test');
    el.querySelector<HTMLButtonElement>('[data-testid="cancel-email-change"]')!.click();
    http.expectOne(r => r.method === 'DELETE' && r.url === '/api/users/me/email-change').flush(null); await settle();
    expect(el.querySelector('[data-testid="pending-email"]')).toBeNull();
  });

  it('checks the password policy and the repeat, then changes the password and offers a new sign-in', async () => {
    const { el, http, type, submit, settle } = await render();
    type('input[name="currentPassword"]', 'Current-Password-1!');
    type('input[name="newPassword"]', 'kisa');
    type('input[name="confirmPassword"]', 'baska');
    await submit('password-form');
    expect(el.querySelector('#account-newPassword-error')?.textContent).toContain('En az 8 karakter');
    expect(el.querySelector('#account-confirmPassword-error')?.textContent).toContain('aynı');
    http.expectNone(r => r.url === '/api/users/me/change-password');

    type('input[name="newPassword"]', 'Yeni-Sifre-2026!');
    type('input[name="confirmPassword"]', 'Yeni-Sifre-2026!');
    await submit('password-form');
    const post = http.expectOne(r => r.method === 'POST' && r.url === '/api/users/me/change-password');
    expect(post.request.body).toEqual({ currentPassword: 'Current-Password-1!', newPassword: 'Yeni-Sifre-2026!' });
    post.flush(null); await settle();
    expect(el.querySelector('[data-testid="password-form-status"]')?.textContent).toContain('yeniden giriş');
    el.querySelector<HTMLButtonElement>('[data-testid="sign-in-again"]')!.click();
    expect(auth.logout).toHaveBeenCalled();
  });

  it('tells a Google account that it signs in with Google and sends a password link on request', async () => {
    const { el, http, settle } = await render(me({ hasPassword: false }));
    expect(el.textContent).toContain('Google ile giriş yapıyorsun');
    expect(el.querySelector('form[data-testid="password-form"]')).toBeNull();
    expect(el.querySelector('form[data-testid="email-form"]')).toBeNull();
    el.querySelector<HTMLButtonElement>('[data-testid="request-password-setup"]')!.click();
    const post = http.expectOne(r => r.method === 'POST' && r.url === '/api/auth/forgot-password');
    expect(post.request.body).toEqual({ email: 'ada@example.test', product: 'coaching' });
    post.flush(null); await settle();
    expect(el.querySelector('[data-testid="password-setup-status"]')?.textContent).toContain('ada@example.test');
  });

  it('shows two-step verification only when it is on', async () => {
    const { el } = await render(me({ mfaEnabled: true }));
    expect(el.querySelector('[data-testid="mfa-status"]')?.textContent).toContain('İki adımlı doğrulama açık');
  });
});
