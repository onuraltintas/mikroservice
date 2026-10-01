import { PLATFORM_ID } from '@angular/core';
import { HttpBackend, HttpEvent, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthRecaptchaService } from './auth-recaptcha.service';

describe('AuthRecaptchaService', () => {
  let backend: StubBackend;
  let originalRecaptcha: GoogleRecaptchaWindow['grecaptcha'];

  beforeEach(() => {
    backend = new StubBackend();
    originalRecaptcha = (window as GoogleRecaptchaWindow).grecaptcha;
    TestBed.configureTestingModule({
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: HttpBackend, useValue: backend }
      ]
    });
  });

  afterEach(() => {
    const browserWindow = window as GoogleRecaptchaWindow;
    if (originalRecaptcha) browserWindow.grecaptcha = originalRecaptcha;
    else delete browserWindow.grecaptcha;
  });

  it('loads the public config and requests an action-bound token', async () => {
    backend.body = { enabled: true, siteKey: 'public-key' };
    const execute = vi.fn().mockResolvedValue('one-time-token');
    (window as GoogleRecaptchaWindow).grecaptcha = {
      ready: callback => callback(),
      execute
    };
    const service = TestBed.inject(AuthRecaptchaService);

    await expect(service.createToken('auth_login')).resolves.toBe('one-time-token');

    expect(backend.requests[0].url).toBe('/api/auth/captcha-config');
    expect(execute).toHaveBeenCalledWith('public-key', { action: 'auth_login' });
  });

  it('does not request a token when CAPTCHA is disabled outside production', async () => {
    backend.body = { enabled: false, siteKey: null };
    const execute = vi.fn();
    (window as GoogleRecaptchaWindow).grecaptcha = {
      ready: callback => callback(),
      execute
    };

    await expect(TestBed.inject(AuthRecaptchaService).createToken('auth_login')).resolves.toBeNull();
    expect(execute).not.toHaveBeenCalled();
  });

  it('fails closed when public CAPTCHA configuration cannot be loaded', async () => {
    backend.error = new Error('identity unavailable');
    const execute = vi.fn();
    (window as GoogleRecaptchaWindow).grecaptcha = {
      ready: callback => callback(),
      execute
    };

    await expect(TestBed.inject(AuthRecaptchaService).createToken('auth_login'))
      .rejects.toThrow('identity unavailable');
    expect(execute).not.toHaveBeenCalled();
  });

  it('uses a separate Coaching key even when the login configuration is already cached', async () => {
    backend.body = { enabled: false, siteKey: null };
    const execute = vi.fn().mockResolvedValue('eft-token');
    (window as GoogleRecaptchaWindow).grecaptcha = { ready: callback => callback(), execute };
    const service = TestBed.inject(AuthRecaptchaService);
    await expect(service.createToken('auth_login')).resolves.toBeNull();
    backend.body = { enabled: true, siteKey: 'coaching-key' };

    await expect(service.createToken('coaching_eft_submit', '/api/coaching/subscriptions/recaptcha'))
      .resolves.toBe('eft-token');

    expect(backend.requests.map(request => request.url)).toEqual([
      '/api/auth/captcha-config', '/api/coaching/subscriptions/recaptcha'
    ]);
    expect(execute).toHaveBeenCalledWith('coaching-key', { action: 'coaching_eft_submit' });
  });
});

interface GoogleRecaptchaWindow {
  grecaptcha?: {
    ready(callback: () => void): void;
    execute(siteKey: string, options: { action: string }): Promise<string>;
  };
}

class StubBackend implements HttpBackend {
  readonly requests: HttpRequest<unknown>[] = [];
  body: unknown = null;
  error?: Error;

  handle(request: HttpRequest<unknown>): Observable<HttpEvent<unknown>> {
    this.requests.push(request);
    return this.error
      ? throwError(() => this.error)
      : of(new HttpResponse({ status: 200, body: this.body }));
  }
}
