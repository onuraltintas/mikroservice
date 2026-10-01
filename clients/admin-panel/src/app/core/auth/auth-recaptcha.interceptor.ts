import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, of, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthRecaptchaService } from './auth-recaptcha.service';

export const AUTH_RECAPTCHA_TOKEN_HEADER = 'X-Auth-Recaptcha-Token';

export function getAuthRecaptchaAction(request: HttpRequest<unknown>): string | null {
  if (request.method === 'POST'
      && request.url === `${environment.apiUrl}/coaching/subscriptions/bank-transfer-requests`) {
    return 'coaching_eft_submit';
  }
  if (request.method !== 'POST' || !request.url.startsWith(`${environment.apiUrl}/auth/`)) {
    return null;
  }

  const segments = new URL(request.url, 'http://localhost').pathname
    .split('/')
    .filter(Boolean)
    .slice(2);
  const [first, second, third] = segments.map(segment => segment.toLowerCase());

  if (segments.length === 1) {
    if (['login', 'google', 'google-login'].includes(first)) return 'auth_login';
    if (['forgot-password', 'resend-verification-email'].includes(first)) return 'auth_recovery';
    if (first === 'register') return 'auth_register';
  }

  if (segments.length === 2 && ['coaching', 'speed-reading'].includes(first)) {
    if (['login', 'google', 'google-login'].includes(second)) return 'auth_login';
  }

  if (segments.length === 3
      && ['coaching', 'speed-reading'].includes(first)
      && second === 'register'
      && ['student', 'teacher', 'institution', 'parent'].includes(third)) {
    return 'auth_register';
  }

  return null;
}

export const authRecaptchaInterceptor: HttpInterceptorFn = (request, next) => {
  const action = getAuthRecaptchaAction(request);
  if (!action) return next(request);

  const service = inject(AuthRecaptchaService);
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
      ? next(request.clone({ setHeaders: { [AUTH_RECAPTCHA_TOKEN_HEADER]: token } }))
      : next(request))
  );
};
