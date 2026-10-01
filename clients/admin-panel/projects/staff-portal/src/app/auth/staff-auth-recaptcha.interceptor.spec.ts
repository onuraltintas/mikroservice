import { HttpErrorResponse, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { lastValueFrom, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { staffAuthRecaptchaInterceptor, getStaffAuthRecaptchaAction } from './staff-auth-recaptcha.interceptor';
import { StaffAuthRecaptchaService } from './staff-auth-recaptcha.service';

describe('staff portal reCAPTCHA interceptor', () => {
  it.each([
    ['/api/auth/coaching/login', 'auth_login'],
    ['/api/auth/speed-reading/login', 'auth_login']
  ])('maps %s to %s', (url, action) => {
    expect(getStaffAuthRecaptchaAction(new HttpRequest('POST', url, {}))).toBe(action);
  });

  it('does not protect MFA or session operations', () => {
    expect(getStaffAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/mfa/verify', {}))).toBeNull();
    expect(getStaffAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/refresh-token', {}))).toBeNull();
    expect(getStaffAuthRecaptchaAction(new HttpRequest('GET', '/api/auth/coaching/login'))).toBeNull();
  });

  it('protects teacher EFT with a token from the Coaching configuration', async () => {
    const createToken = vi.fn().mockResolvedValue('eft-token');
    const next = vi.fn((request: HttpRequest<unknown>) => of(new HttpResponse({ status: 200, body: request })));
    TestBed.configureTestingModule({ providers: [{ provide: StaffAuthRecaptchaService, useValue: { createToken } }] });
    const request = new HttpRequest('POST', '/api/coaching/subscriptions/teacher-bank-transfer-requests', {});

    expect(getStaffAuthRecaptchaAction(request)).toBe('coaching_eft_submit');
    const response = await lastValueFrom(TestBed.runInInjectionContext(() => staffAuthRecaptchaInterceptor(request, next)));
    expect(createToken).toHaveBeenCalledWith('coaching_eft_submit', '/api/coaching/subscriptions/recaptcha');
    expect((response as HttpResponse<HttpRequest<unknown>>).body?.headers.get('X-Auth-Recaptcha-Token')).toBe('eft-token');
  });

  it('blocks teacher EFT when no token is returned', async () => {
    const createToken = vi.fn().mockResolvedValue(null);
    const next = vi.fn(() => of(new HttpResponse({ status: 200 })));
    TestBed.configureTestingModule({ providers: [{ provide: StaffAuthRecaptchaService, useValue: { createToken } }] });
    const request = new HttpRequest('POST', '/api/coaching/subscriptions/teacher-bank-transfer-requests', {});
    await expect(lastValueFrom(TestBed.runInInjectionContext(() => staffAuthRecaptchaInterceptor(request, next))))
      .rejects.toMatchObject({ status: 403, error: { code: 'Coaching.CaptchaFailed' } });
    expect(next).not.toHaveBeenCalled();
  });

  it('blocks teacher EFT when token generation fails', async () => {
    const createToken = vi.fn().mockRejectedValue(new Error('captcha unavailable'));
    const next = vi.fn(() => of(new HttpResponse({ status: 200 })));
    TestBed.configureTestingModule({ providers: [{ provide: StaffAuthRecaptchaService, useValue: { createToken } }] });
    const request = new HttpRequest('POST', '/api/coaching/subscriptions/teacher-bank-transfer-requests', {});
    await expect(lastValueFrom(TestBed.runInInjectionContext(() => staffAuthRecaptchaInterceptor(request, next))))
      .rejects.toMatchObject({ status: 403, error: { code: 'Coaching.CaptchaFailed' } });
    expect(next).not.toHaveBeenCalled();
  });

  it('adds a fresh server-configured token to product login requests', async () => {
    const createToken = vi.fn().mockResolvedValue('single-use-captcha');
    const next = vi.fn((request: HttpRequest<unknown>) => of(new HttpResponse({ status: 200, body: request })));
    TestBed.configureTestingModule({
      providers: [{ provide: StaffAuthRecaptchaService, useValue: { createToken } }]
    });

    const request = new HttpRequest('POST', '/api/auth/coaching/login', {});
    const response = await lastValueFrom(TestBed.runInInjectionContext(() =>
      staffAuthRecaptchaInterceptor(request, next)));

    expect(createToken).toHaveBeenCalledWith('auth_login');
    expect((response as HttpResponse<HttpRequest<unknown>>).body?.headers.get('X-Auth-Recaptcha-Token'))
      .toBe('single-use-captcha');
  });

  it('fails closed if CAPTCHA cannot be created', async () => {
    const createToken = vi.fn().mockRejectedValue(new Error('captcha unavailable'));
    const next = vi.fn(() => of(new HttpResponse({ status: 200 })));
    TestBed.configureTestingModule({
      providers: [{ provide: StaffAuthRecaptchaService, useValue: { createToken } }]
    });

    const request = new HttpRequest('POST', '/api/auth/speed-reading/login', {});
    await expect(lastValueFrom(TestBed.runInInjectionContext(() =>
      staffAuthRecaptchaInterceptor(request, next)))).rejects.toMatchObject({
      status: 403,
      error: { code: 'Auth.CaptchaFailed' }
    });
    expect(next).not.toHaveBeenCalled();
  });
});
