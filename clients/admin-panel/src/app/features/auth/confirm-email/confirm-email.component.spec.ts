import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { AUTH_VOICE } from '../auth-voice';
import { ConfirmEmailComponent } from './confirm-email.component';

// Paket F #10: one message per reason, and a way to get a new link through the existing resend endpoint.
const bad = (code: string | null, status = 400) => new HttpErrorResponse({
  status, error: code ? { code, description: 'Sunucu metni' } : null
});

async function render(confirm: () => Promise<unknown>, query: Record<string, string> = { userId: 'u1', token: 't1' }, voice: 'sen' | 'siz' = 'sen') {
  const auth = { confirmEmail: vi.fn(confirm), resendVerificationEmail: vi.fn(async () => undefined) };
  const toaster = { success: vi.fn(), error: vi.fn() };
  TestBed.configureTestingModule({
    imports: [ConfirmEmailComponent],
    providers: [
      provideRouter([]),
      { provide: AuthService, useValue: auth },
      { provide: ToasterService, useValue: toaster },
      { provide: AUTH_VOICE, useValue: voice },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) } } }
    ]
  });
  const fixture = TestBed.createComponent(ConfirmEmailComponent);
  fixture.detectChanges();
  await fixture.whenStable(); fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  const messages = () => el.querySelectorAll('[data-testid="confirm-email-message"]');
  return { fixture, el, auth, toaster, messages };
}

describe('ConfirmEmailComponent', () => {
  it('confirms the address and offers sign-in', async () => {
    const { el, auth, toaster, messages } = await render(async () => ({ status: 'confirmed' }));
    expect(auth.confirmEmail).toHaveBeenCalledWith('u1', 't1');
    expect(messages()).toHaveLength(1);
    expect(messages()[0].textContent).toContain('E-posta adresin doğrulandı');
    expect(el.querySelector('[data-testid="confirm-email-login"]')?.getAttribute('href')).toBe('/auth/login');
    expect(toaster.success).toHaveBeenCalledTimes(1);
  });

  it('says an already verified address is verified and offers sign-in, without a resend form', async () => {
    const { el, toaster, messages } = await render(async () => ({ status: 'alreadyConfirmed' }));
    expect(messages()).toHaveLength(1);
    expect(messages()[0].textContent).toContain('zaten doğrulanmış');
    expect(el.querySelector('[data-testid="confirm-email-login"]')?.textContent?.trim()).toBe('Giriş yap');
    expect(el.querySelector('[data-testid="confirm-email-resend"]')).toBeNull();
    expect(toaster.success).not.toHaveBeenCalled();
  });

  it('shows one expired message (no stacked generic errors, no toast) and offers a new e-mail', async () => {
    const { el, toaster, messages } = await render(() => Promise.reject(bad('User.TokenExpired')));
    expect(messages()).toHaveLength(1);
    expect(messages()[0].textContent).toContain('süresi dolmuş');
    expect(el.textContent).not.toContain('bir hata oluştu');
    expect(el.textContent).not.toContain('Sunucu metni');
    expect(toaster.error).not.toHaveBeenCalled();
    expect(el.querySelector('[data-testid="confirm-email-resend"]')).not.toBeNull();
  });

  it.each([['User.InvalidToken'], [null]])('shows the invalid-link message for %s and for a link without its values', async code => {
    const { messages } = await render(() => Promise.reject(bad(code)));
    expect(messages()).toHaveLength(1);
    expect(messages()[0].textContent).toContain('Bağlantı geçersiz');
    TestBed.resetTestingModule();
    const missing = await render(async () => ({}), { userId: 'u1' });
    expect(missing.auth.confirmEmail).not.toHaveBeenCalled();
    expect(missing.messages()[0].textContent).toContain('Bağlantı geçersiz');
  });

  it('sends a new verification e-mail through the resend endpoint and gives the same answer for any address', async () => {
    const { fixture, el, auth } = await render(() => Promise.reject(bad('User.TokenExpired')));
    const input = el.querySelector<HTMLInputElement>('#confirm-email-address')!;
    input.value = ' ada@example.test '; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    el.querySelector<HTMLFormElement>('[data-testid="confirm-email-resend"]')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable(); fixture.detectChanges();
    expect(auth.resendVerificationEmail).toHaveBeenCalledWith('ada@example.test');
    expect(el.querySelector('[data-testid="confirm-email-resend-status"]')?.textContent).toContain('doğrulama bekliyorsa');
    // G5 (#39): the e-mail may take a few minutes; it is not to be sent again needlessly.
    expect(el.querySelector('[data-testid="confirm-email-resend-status"]')?.textContent).toContain('E-postanın gelmesi birkaç dakika sürebilir; gereksiz yere yeniden gönderme.');
    expect(el.querySelector('[data-testid="confirm-email-resend"]')).toBeNull();
  });

  it('explains a rate-limited resend', async () => {
    const { fixture, el, auth } = await render(() => Promise.reject(bad('User.InvalidToken')));
    auth.resendVerificationEmail.mockRejectedValueOnce(new HttpErrorResponse({ status: 429, error: null }));
    const input = el.querySelector<HTMLInputElement>('#confirm-email-address')!;
    input.value = 'ada@example.test'; input.dispatchEvent(new Event('input')); fixture.detectChanges();
    el.querySelector<HTMLFormElement>('[data-testid="confirm-email-resend"]')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable(); fixture.detectChanges();
    expect(el.querySelector('#confirm-email-resend-error')?.textContent).toContain('Çok fazla deneme');
  });

  it('treats a server or network failure as temporary and lets the link be tried again', async () => {
    let calls = 0;
    const { fixture, el, messages } = await render(() => ++calls === 1 ? Promise.reject(bad(null, 503)) : Promise.resolve({ status: 'confirmed' }));
    expect(messages()[0].textContent).toContain('şu an tamamlanamadı');
    el.querySelector<HTMLButtonElement>('[data-testid="confirm-email-retry"]')!.click();
    await fixture.whenStable(); fixture.detectChanges();
    expect(messages()[0].textContent).toContain('doğrulandı');
  });

  it('keeps siz on the management panel host', async () => {
    const { messages } = await render(() => Promise.reject(bad('User.TokenExpired')), undefined, 'siz');
    expect(messages()[0].textContent).toContain('isteyin');
  });
});
