import { HttpBackend, HttpClient, HttpEvent, HttpRequest, HttpResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { fakeAsync, flushMicrotasks, TestBed, tick } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { apiResponseInterceptor } from './api-response.interceptor';
import { authRecaptchaInterceptor } from './auth-recaptcha.interceptor';

describe('authentication CAPTCHA error flow', () => {
  it('preserves the CAPTCHA error body for the login screen after a timeout', fakeAsync(() => {
    const browserWindow = window as unknown as { grecaptcha?: unknown };
    const original = browserWindow.grecaptcha;
    const backend = new CaptchaBackend();
    try {
      browserWindow.grecaptcha = { ready: (callback: () => void) => callback(), execute: () => new Promise(() => {}) };
      TestBed.configureTestingModule({ providers: [
        provideHttpClient(withInterceptors([apiResponseInterceptor, authRecaptchaInterceptor])),
        { provide: HttpBackend, useValue: backend }
      ] });
      let error: any;
      TestBed.inject(HttpClient).post('/api/auth/speed-reading/login', {}).subscribe({ error: value => error = value });
      flushMicrotasks();
      tick(15000);
      flushMicrotasks();
      expect(error.status).toBe(403);
      expect(error.error?.code).toBe('Auth.CaptchaFailed');
      expect(error.error?.message).toBe('Güvenlik doğrulaması tamamlanamadı. Lütfen tekrar deneyin.');
      expect(backend.requests.map(request => request.method)).toEqual(['GET']);
    } finally {
      if (original === undefined) delete browserWindow.grecaptcha;
      else browserWindow.grecaptcha = original;
    }
  }));
});

class CaptchaBackend implements HttpBackend {
  readonly requests: HttpRequest<unknown>[] = [];
  handle(request: HttpRequest<unknown>): Observable<HttpEvent<unknown>> {
    this.requests.push(request);
    return of(new HttpResponse({ status: 200, body: { enabled: true, siteKey: 'public-key' } }));
  }
}
