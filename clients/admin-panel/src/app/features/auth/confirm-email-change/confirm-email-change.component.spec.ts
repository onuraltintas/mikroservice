import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { AUTH_ROUTES } from '../auth.routes';
import { ConfirmEmailChangeComponent } from './confirm-email-change.component';

// E5 (#22): the link in the e-mail sent to the new address. The change is applied only when the person presses the
// button (a mail scanner opening the link changes nothing).
describe('E-posta değişikliği onayı', () => {
  async function render(query: Record<string, string>, signedIn = false) {
    const auth = { refreshSession: vi.fn(async () => true), userProfile: signal(signedIn ? { id: 'u1' } : null) };
    TestBed.configureTestingModule({ imports: [ConfirmEmailChangeComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: AuthService, useValue: auth },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) } } }] });
    const fixture = TestBed.createComponent(ConfirmEmailChangeComponent);
    fixture.detectChanges();
    const settle = async () => { fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); };
    return { el: fixture.nativeElement as HTMLElement, http: TestBed.inject(HttpTestingController), auth, settle };
  }

  it('is served at /auth/confirm-email-change', async () => {
    const route = AUTH_ROUTES.find(r => r.path === 'confirm-email-change')!;
    expect(await route.loadComponent!()).toBe(ConfirmEmailChangeComponent);
  });

  it('confirms only on the button and then says the address changed', async () => {
    const { el, http, auth, settle } = await render({ userId: 'u1', token: 'abc' }, true);
    http.expectNone(r => r.url === '/api/auth/confirm-email-change');
    el.querySelector<HTMLButtonElement>('[data-testid="confirm-email-change"]')!.click();
    const post = http.expectOne(r => r.method === 'POST' && r.url === '/api/auth/confirm-email-change');
    expect(post.request.body).toEqual({ userId: 'u1', token: 'abc' });
    post.flush(null); await settle();
    expect(el.querySelector('[role="status"]')?.textContent).toContain('E-posta adresin değiştirildi');
    expect(auth.refreshSession).toHaveBeenCalled();
  });

  it('explains an expired or used link', async () => {
    const { el, http, settle } = await render({ userId: 'u1', token: 'abc' });
    el.querySelector<HTMLButtonElement>('[data-testid="confirm-email-change"]')!.click();
    http.expectOne(r => r.url === '/api/auth/confirm-email-change').flush(
      { code: 'EmailChange.InvalidLink', description: 'Bağlantı geçersiz ya da süresi dolmuş. Hesabım sayfasından yeni bir bağlantı iste.' },
      { status: 400, statusText: 'Bad Request' });
    await settle();
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('süresi dolmuş');
  });

  it('has no button for an incomplete link', async () => {
    const { el } = await render({ userId: 'u1' });
    expect(el.querySelector('[data-testid="confirm-email-change"]')).toBeNull();
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('eksik');
  });
});
