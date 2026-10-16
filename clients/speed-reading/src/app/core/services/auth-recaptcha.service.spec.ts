import { HttpBackend, HttpEvent, HttpRequest, HttpResponse } from '@angular/common/http';
import { fakeAsync, flushMicrotasks, TestBed, tick } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { NEVER, Observable, of, throwError } from 'rxjs';
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

  it('loads public configuration and executes the expected action', async () => {
    backend.body = { enabled: true, siteKey: 'public-key' };
    const execute = jasmine.createSpy('execute').and.resolveTo('one-time-token');
    (window as GoogleRecaptchaWindow).grecaptcha = {
      ready: callback => callback(),
      execute
    };

    await expectAsync(TestBed.inject(AuthRecaptchaService).createToken('auth_register'))
      .toBeResolvedTo('one-time-token');

    expect(backend.requests[0].url).toBe('/api/auth/captcha-config');
    expect(execute).toHaveBeenCalledOnceWith('public-key', { action: 'auth_register' });
  });

  it('does not execute CAPTCHA when the local Identity service disables it', async () => {
    backend.body = { enabled: false, siteKey: null };
    const execute = jasmine.createSpy('execute');
    (window as GoogleRecaptchaWindow).grecaptcha = {
      ready: callback => callback(),
      execute
    };

    await expectAsync(TestBed.inject(AuthRecaptchaService).createToken('auth_login'))
      .toBeResolvedTo(null);
    expect(execute).not.toHaveBeenCalled();
  });

  it('keeps EFT configuration separate from login and creates a fresh token for each submission', async () => {
    const service = TestBed.inject(AuthRecaptchaService);
    backend.body = { enabled: false, siteKey: null };
    await expectAsync(service.createToken('auth_login')).toBeResolvedTo(null);
    backend.body = { enabled: true, siteKey: 'eft-key' };
    const execute = jasmine.createSpy('execute').and.returnValues(Promise.resolve('first-token'), Promise.resolve('second-token'));
    (window as GoogleRecaptchaWindow).grecaptcha = { ready: callback => callback(), execute };
    await expectAsync(service.createToken('speed_reading_eft_submit', '/api/speed-reading/bank-transfer/recaptcha')).toBeResolvedTo('first-token');
    await expectAsync(service.createToken('speed_reading_eft_submit', '/api/speed-reading/bank-transfer/recaptcha')).toBeResolvedTo('second-token');
    expect(backend.requests.map(request => request.url)).toEqual(['/api/auth/captcha-config', '/api/speed-reading/bank-transfer/recaptcha']);
    expect(execute).toHaveBeenCalledTimes(2);
    expect(execute).toHaveBeenCalledWith('eft-key', { action: 'speed_reading_eft_submit' });
  });

  it('fails closed if the CAPTCHA configuration endpoint is unavailable', async () => {
    backend.error = new Error('identity unavailable');
    const execute = jasmine.createSpy('execute');
    (window as GoogleRecaptchaWindow).grecaptcha = {
      ready: callback => callback(),
      execute
    };

    await expectAsync(TestBed.inject(AuthRecaptchaService).createToken('auth_login'))
      .toBeRejectedWithError('identity unavailable');
    expect(execute).not.toHaveBeenCalled();
  });

  for (const stage of ['configuration', 'ready', 'execute']) {
    it(`stops waiting when CAPTCHA ${stage} never responds and allows a fresh attempt`, fakeAsync(() => {
      backend.body = { enabled: true, siteKey: 'public-key' };
      const execute = jasmine.createSpy('execute').and.returnValue(new Promise<string>(() => {}));
      (window as GoogleRecaptchaWindow).grecaptcha = {
        ready: callback => { if (stage !== 'ready') callback(); },
        execute
      };
      const handle = spyOn(backend, 'handle').and.callThrough();
      if (stage === 'configuration') handle.and.returnValue(NEVER);
      let rejected = false;
      const service = TestBed.inject(AuthRecaptchaService);
      void service.createToken('auth_login').catch(() => { rejected = true; });
      flushMicrotasks();
      tick(15000);
      flushMicrotasks();
      expect(rejected).toBeTrue();

      handle.and.callThrough();
      (window as GoogleRecaptchaWindow).grecaptcha = {
        ready: callback => callback(),
        execute: () => Promise.resolve('fresh-token')
      };
      const result: { token: string | null } = { token: null };
      void service.createToken('auth_login').then(value => result.token = value);
      flushMicrotasks();
      expect(result.token).toBe('fresh-token');
    }));
  }
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
