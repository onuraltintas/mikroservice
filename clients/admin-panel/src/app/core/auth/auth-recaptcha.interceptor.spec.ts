import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, HttpRequest, HttpResponse } from '@angular/common/http';
import { lastValueFrom, of } from 'rxjs';
import { throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthRecaptchaService } from './auth-recaptcha.service';
import { authRecaptchaInterceptor, getAuthRecaptchaAction } from './auth-recaptcha.interceptor';

describe('auth reCAPTCHA interceptor', () => {
  it.each([
    ['/api/auth/coaching/login', 'auth_login'],
    ['/api/auth/coaching/google-login', 'auth_login'],
    ['/api/auth/coaching/register/institution', 'auth_register'],
    ['/api/auth/speed-reading/register/student', 'auth_register'],
    ['/api/auth/forgot-password', 'auth_recovery'],
    ['/api/auth/resend-verification-email', 'auth_recovery']
  ])('maps %s to %s', (url, expectedAction) => {
    expect(getAuthRecaptchaAction(new HttpRequest('POST', url, {}))).toBe(expectedAction);
  });

  it('does not protect unrelated endpoints or non-POST requests', () => {
    expect(getAuthRecaptchaAction(new HttpRequest('GET', '/api/auth/captcha-config'))).toBeNull();
    expect(getAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/mfa/verify', {}))).toBeNull();
    expect(getAuthRecaptchaAction(new HttpRequest('POST', '/api/users', {}))).toBeNull();
  });

  it('protects student EFT with the Coaching CAPTCHA configuration and fresh tokens', async () => {
    const createToken = vi.fn().mockResolvedValueOnce('first-token').mockResolvedValueOnce('second-token');
    const next = vi.fn((request: HttpRequest<unknown>) => of(new HttpResponse({ status: 200, body: request })));
    TestBed.configureTestingModule({ providers: [{ provide: AuthRecaptchaService, useValue: { createToken } }] });
    const request = new HttpRequest('POST', '/api/coaching/subscriptions/bank-transfer-requests', {});
    expect(getAuthRecaptchaAction(request)).toBe('coaching_eft_submit');

    for (const token of ['first-token', 'second-token']) {
      const response = await lastValueFrom(TestBed.runInInjectionContext(() => authRecaptchaInterceptor(request, next)));
      expect((response as HttpResponse<HttpRequest<unknown>>).body?.headers.get('X-Auth-Recaptcha-Token')).toBe(token);
    }
    expect(createToken).toHaveBeenCalledWith('coaching_eft_submit', '/api/coaching/subscriptions/recaptcha');
  });

  it('blocks EFT when CAPTCHA is disabled and no token is returned', async () => {
    const createToken = vi.fn().mockResolvedValue(null);
    const next = vi.fn(() => of(new HttpResponse({ status: 200 })));
    TestBed.configureTestingModule({ providers: [{ provide: AuthRecaptchaService, useValue: { createToken } }] });
    const request = new HttpRequest('POST', '/api/coaching/subscriptions/bank-transfer-requests', {});
    await expect(lastValueFrom(TestBed.runInInjectionContext(() => authRecaptchaInterceptor(request, next))))
      .rejects.toMatchObject({ status: 403, error: { code: 'Coaching.CaptchaFailed' } });
    expect(next).not.toHaveBeenCalled();
  });

  it('blocks EFT when token generation fails', async () => {
    const createToken = vi.fn().mockRejectedValue(new Error('captcha unavailable'));
    const next = vi.fn(() => of(new HttpResponse({ status: 200 })));
    TestBed.configureTestingModule({ providers: [{ provide: AuthRecaptchaService, useValue: { createToken } }] });
    const request = new HttpRequest('POST', '/api/coaching/subscriptions/bank-transfer-requests', {});
    await expect(lastValueFrom(TestBed.runInInjectionContext(() => authRecaptchaInterceptor(request, next))))
      .rejects.toMatchObject({ status: 403, error: { code: 'Coaching.CaptchaFailed' } });
    expect(next).not.toHaveBeenCalled();
  });

  it('adds a fresh token to protected auth requests', async () => {
    const createToken = vi.fn().mockResolvedValue('one-time-captcha-token');
    const next = vi.fn((request: HttpRequest<unknown>) => of(new HttpResponse({ status: 200, body: request })));
    TestBed.configureTestingModule({
      providers: [{ provide: AuthRecaptchaService, useValue: { createToken } }]
    });

    const request = new HttpRequest('POST', '/api/auth/coaching/login', {});
    const response = TestBed.runInInjectionContext(() => authRecaptchaInterceptor(request, next));
    const result = await lastValueFrom(response);

    expect(createToken).toHaveBeenCalledWith('auth_login');
    expect((result as HttpResponse<HttpRequest<unknown>>).body?.headers.get('X-Auth-Recaptcha-Token'))
      .toBe('one-time-captcha-token');
  });

  it('does not send a protected request when token generation fails', async () => {
    const createToken = vi.fn().mockRejectedValue(new Error('captcha unavailable'));
    const next = vi.fn(() => of(new HttpResponse({ status: 200 })));
    TestBed.configureTestingModule({
      providers: [{ provide: AuthRecaptchaService, useValue: { createToken } }]
    });

    const request = new HttpRequest('POST', '/api/auth/coaching/login', {});
    const response = TestBed.runInInjectionContext(() => authRecaptchaInterceptor(request, next));

    await expect(lastValueFrom(response)).rejects.toMatchObject({
      status: 403,
      error: { code: 'Auth.CaptchaFailed' }
    });
    expect(next).not.toHaveBeenCalled();
  });

  it('leaves a local auth request unchanged when the server explicitly disables CAPTCHA', async () => {
    const createToken = vi.fn().mockResolvedValue(null);
    const next = vi.fn((request: HttpRequest<unknown>) => of(new HttpResponse({ status: 200, body: request })));
    TestBed.configureTestingModule({
      providers: [{ provide: AuthRecaptchaService, useValue: { createToken } }]
    });

    const request = new HttpRequest('POST', '/api/auth/coaching/login', {});
    const response = await lastValueFrom(TestBed.runInInjectionContext(() =>
      authRecaptchaInterceptor(request, next)));

    expect((response as HttpResponse<HttpRequest<unknown>>).body?.headers.has('X-Auth-Recaptcha-Token'))
      .toBe(false);
    expect(next).toHaveBeenCalledOnce();
  });

  it('preserves authentication errors returned by the server after CAPTCHA passes', async () => {
    const createToken = vi.fn().mockResolvedValue('valid-token');
    const next = vi.fn(() => throwError(() => new HttpErrorResponse({ status: 400 })));
    TestBed.configureTestingModule({
      providers: [{ provide: AuthRecaptchaService, useValue: { createToken } }]
    });

    const request = new HttpRequest('POST', '/api/auth/coaching/login', {});
    const response = TestBed.runInInjectionContext(() => authRecaptchaInterceptor(request, next));

    await expect(lastValueFrom(response)).rejects.toMatchObject({ status: 400 });
  });
});
