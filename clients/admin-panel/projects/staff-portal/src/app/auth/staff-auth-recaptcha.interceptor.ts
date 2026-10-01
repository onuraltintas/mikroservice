import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { StaffAuthRecaptchaService } from './staff-auth-recaptcha.service';

export const STAFF_AUTH_RECAPTCHA_TOKEN_HEADER = 'X-Auth-Recaptcha-Token';

export function getStaffAuthRecaptchaAction(request: HttpRequest<unknown>): string | null {
  if (request.method === 'POST'
      && request.url === `${environment.apiUrl}/coaching/subscriptions/teacher-bank-transfer-requests`) {
    return 'coaching_eft_submit';
  }
  if (request.method !== 'POST' || !request.url.startsWith(`${environment.apiUrl}/auth/`)) {
    return null;
  }

  const segments = new URL(request.url, 'http://localhost').pathname
    .split('/')
    .filter(Boolean)
    .slice(2)
    .map(segment => segment.toLowerCase());
  const [product, action] = segments;

  return segments.length === 2
      && ['coaching', 'speed-reading'].includes(product)
      && action === 'login'
    ? 'auth_login'
    : null;
}

export const staffAuthRecaptchaInterceptor: HttpInterceptorFn = (request, next) => {
  const action = getStaffAuthRecaptchaAction(request);
  if (!action) return next(request);

  const service = inject(StaffAuthRecaptchaService);
  const isPayment = action === 'coaching_eft_submit';
  const tokenPromise = isPayment
    ? service.createToken(action, `${environment.apiUrl}/coaching/subscriptions/recaptcha`)
    : service.createToken(action);
  return from(tokenPromise.then(token => {
    if (isPayment && !token) throw new Error('EFT CAPTCHA token is required.');
    return token;
  })).pipe(
    catchError(() => throwError(() => new HttpErrorResponse({
      status: 403,
      url: request.url,
      error: {
        success: false,
        code: isPayment ? 'Coaching.CaptchaFailed' : 'Auth.CaptchaFailed',
        message: 'Güvenlik doğrulaması tamamlanamadı. Lütfen tekrar deneyin.'
      }
    }))),
    switchMap(token => token
      ? next(request.clone({ setHeaders: { [STAFF_AUTH_RECAPTCHA_TOKEN_HEADER]: token } }))
      : next(request))
  );
};
