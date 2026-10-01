import { HttpErrorResponse, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { lastValueFrom, of, throwError } from 'rxjs';
import { AuthRecaptchaService } from '../services/auth-recaptcha.service';
import { authRecaptchaInterceptor, getAuthRecaptchaAction } from './auth-recaptcha.interceptor';

describe('auth reCAPTCHA interceptor', () => {
  it('protects EFT using the product CAPTCHA configuration', async () => {
    const createToken = jasmine.createSpy('createToken').and.resolveTo('eft-token');
    TestBed.configureTestingModule({ providers: [provideAuthCaptcha(createToken)] });
    const next = jasmine.createSpy('next').and.callFake((request: HttpRequest<unknown>) =>
      of(new HttpResponse({ status: 200, body: request })));
    const request = new HttpRequest('POST', '/api/speed-reading/bank-transfer/requests', {});
    expect(getAuthRecaptchaAction(request)).toBe('speed_reading_eft_submit');
    const response = await lastValueFrom(TestBed.runInInjectionContext(() => authRecaptchaInterceptor(request, next)));
    expect(createToken).toHaveBeenCalledOnceWith('speed_reading_eft_submit', '/api/speed-reading/bank-transfer/recaptcha');
    expect((response as HttpResponse<HttpRequest<unknown>>).body?.headers.get('X-Auth-Recaptcha-Token')).toBe('eft-token');
  });

  for (const outcome of ['missing', 'failed']) {
    it('blocks EFT when CAPTCHA is ' + outcome, async () => {
      const createToken = jasmine.createSpy('createToken');
      if (outcome === 'missing') createToken.and.resolveTo(null);
      else createToken.and.rejectWith(new Error('captcha unavailable'));
      TestBed.configureTestingModule({ providers: [provideAuthCaptcha(createToken)] });
      const next = jasmine.createSpy('next').and.returnValue(of(new HttpResponse({ status: 200 })));
      const request = new HttpRequest('POST', '/api/speed-reading/bank-transfer/requests', {});
      const error = await lastValueFrom(TestBed.runInInjectionContext(() => authRecaptchaInterceptor(request, next))).catch(reason => reason);
      expect(error.status).toBe(403);
      expect(error.error.code).toBe('SpeedReading.CaptchaFailed');
      expect(next).not.toHaveBeenCalled();
    });
  }

  it('maps login, Google login, registration, and recovery to server actions', () => {
    expect(getAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/speed-reading/google-register-complete', {})))
      .toBe('auth_register');
    expect(getAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/speed-reading/login', {})))
      .toBe('auth_login');
    expect(getAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/speed-reading/google', {})))
      .toBe('auth_login');
    expect(getAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/speed-reading/register/teacher', {})))
      .toBe('auth_register');
    expect(getAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/forgot-password', {})))
      .toBe('auth_recovery');
  });

  it('does not protect MFA, password reset, config reads, or other products', () => {
    expect(getAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/mfa/verify', {}))).toBeNull();
    expect(getAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/reset-password', {}))).toBeNull();
    expect(getAuthRecaptchaAction(new HttpRequest('GET', '/api/auth/captcha-config'))).toBeNull();
    expect(getAuthRecaptchaAction(new HttpRequest('POST', '/api/auth/other/login', {}))).toBeNull();
  });

  it('sends the auth token header', async () => {
    const createToken = jasmine.createSpy('createToken').and.resolveTo('one-time-captcha-token');
    TestBed.configureTestingModule({
      providers: [provideAuthCaptcha(createToken)]
    });
    const next = jasmine.createSpy('next').and.callFake((request: HttpRequest<unknown>) =>
      of(new HttpResponse({ status: 200, body: request })));
    const request = new HttpRequest('POST', '/api/auth/speed-reading/login', {});

    const response = await lastValueFrom(TestBed.runInInjectionContext(() =>
      authRecaptchaInterceptor(request, next)));

    expect(createToken).toHaveBeenCalledOnceWith('auth_login');
    expect((response as HttpResponse<HttpRequest<unknown>>).body?.headers.get('X-Auth-Recaptcha-Token'))
      .toBe('one-time-captcha-token');
  });

  it('blocks the protected request if token creation fails', async () => {
    const createToken = jasmine.createSpy('createToken').and.rejectWith(new Error('captcha unavailable'));
    TestBed.configureTestingModule({
      providers: [provideAuthCaptcha(createToken)]
    });
    const next = jasmine.createSpy('next').and.returnValue(of(new HttpResponse({ status: 200 })));
    const request = new HttpRequest('POST', '/api/auth/speed-reading/login', {});
    const response = TestBed.runInInjectionContext(() => authRecaptchaInterceptor(request, next));

    const error = await lastValueFrom(response).catch(reason => reason);
    expect(error.status).toBe(403);
    expect(error.error.code).toBe('Auth.CaptchaFailed');
    expect(next).not.toHaveBeenCalled();
  });

  it('preserves an authentication error returned by Identity', async () => {
    const createToken = jasmine.createSpy('createToken').and.resolveTo('valid-token');
    TestBed.configureTestingModule({
      providers: [provideAuthCaptcha(createToken)]
    });
    const next = jasmine.createSpy('next').and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400 }))
    );
    const request = new HttpRequest('POST', '/api/auth/speed-reading/login', {});
    const response = TestBed.runInInjectionContext(() => authRecaptchaInterceptor(request, next));

    const error = await lastValueFrom(response).catch(reason => reason);
    expect(error.status).toBe(400);
  });
});

function provideAuthCaptcha(createToken: jasmine.Spy) {
  return { provide: AuthRecaptchaService, useValue: { createToken } };
}
