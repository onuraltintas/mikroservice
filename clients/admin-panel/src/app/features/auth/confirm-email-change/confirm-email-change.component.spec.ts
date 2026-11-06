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
// button (a mail scanner opening the link changes nothing). Review M1 (b): the page first says which account changes
// (masked current address); review L4: the link value leaves the address bar once read.
describe('E-posta değişikliği onayı', () => {
  async function render(query: Record<string, string>, profile: { id: string; roles: string[] } | null = null) {
    const auth = { refreshSession: vi.fn(async () => true), userProfile: signal(profile) };
    const replace = vi.spyOn(window.history, 'replaceState');
    TestBed.configureTestingModule({ imports: [ConfirmEmailChangeComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: AuthService, useValue: auth },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) } } }] });
    const fixture = TestBed.createComponent(ConfirmEmailChangeComponent);
    fixture.detectChanges();
    const settle = async () => { fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); };
    const http = TestBed.inject(HttpTestingController);
    return { el: fixture.nativeElement as HTMLElement, http, auth, settle, replace };
  }

  async function previewed(query: Record<string, string>, profile: { id: string; roles: string[] } | null = null) {
    const page = await render(query, profile);
    const preview = page.http.expectOne(r => r.method === 'POST' && r.url === '/api/auth/email-change-preview');
    expect(preview.request.body).toEqual({ userId: query['userId'], token: query['token'] });
    preview.flush({ currentEmailMasked: 'a***@example.test' }); await page.settle();
    return page;
  }

  afterEach(() => vi.restoreAllMocks());

  it('is served at /auth/confirm-email-change', async () => {
    const route = AUTH_ROUTES.find(r => r.path === 'confirm-email-change')!;
    expect(await route.loadComponent!()).toBe(ConfirmEmailChangeComponent);
  });

  it('removes the link value from the address bar and names the account before the button', async () => {
    const { el, replace } = await previewed({ userId: 'u1', token: 'abc' });
    expect(replace).toHaveBeenCalledWith(null, '', '/auth/confirm-email-change');
    expect(el.querySelector('[data-testid="change-account"]')?.textContent).toContain('a***@example.test');
  });

  it('confirms only on the button and then says the address changed', async () => {
    const { el, http, auth, settle } = await previewed({ userId: 'u1', token: 'abc' }, { id: 'u1', roles: ['Student'] });
    http.expectNone(r => r.url === '/api/auth/confirm-email-change');
    el.querySelector<HTMLButtonElement>('[data-testid="confirm-email-change"]')!.click();
    const post = http.expectOne(r => r.method === 'POST' && r.url === '/api/auth/confirm-email-change');
    expect(post.request.body).toEqual({ userId: 'u1', token: 'abc' });
    post.flush(null); await settle();
    expect(el.querySelector('[role="status"]')?.textContent).toContain('E-posta adresin değiştirildi');
    expect(auth.refreshSession).toHaveBeenCalled();
    expect(el.querySelector('a[href="/coaching-portal/account/settings/account"]')).not.toBeNull();
  });

  // Review L7: a parent goes back to the parent's Hesabım.
  it('sends a signed-in parent back to the parent Hesabım', async () => {
    const { el, http, settle } = await previewed({ userId: 'u1', token: 'abc' }, { id: 'u1', roles: ['Parent'] });
    el.querySelector<HTMLButtonElement>('[data-testid="confirm-email-change"]')!.click();
    http.expectOne(r => r.url === '/api/auth/confirm-email-change').flush(null); await settle();
    expect(el.querySelector('a[href="/coaching-portal/parent/account/settings"]')).not.toBeNull();
  });

  it('explains an expired or used link without a button', async () => {
    const { el, http, settle } = await render({ userId: 'u1', token: 'abc' });
    http.expectOne(r => r.url === '/api/auth/email-change-preview').flush(
      { code: 'EmailChange.InvalidLink', description: 'Bağlantı geçersiz ya da süresi dolmuş. Hesabım sayfasından yeni bir bağlantı iste.' },
      { status: 400, statusText: 'Bad Request' });
    await settle();
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('süresi dolmuş');
    expect(el.querySelector('[data-testid="confirm-email-change"]')).toBeNull();
  });

  it('has no button for an incomplete link', async () => {
    const { el, http } = await render({ userId: 'u1' });
    http.expectNone(r => r.url === '/api/auth/email-change-preview');
    expect(el.querySelector('[data-testid="confirm-email-change"]')).toBeNull();
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('eksik');
  });
});
