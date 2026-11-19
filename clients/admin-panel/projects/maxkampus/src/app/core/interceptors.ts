import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { StaffAuthRecaptchaService } from '../../../../staff-portal/src/app/auth/staff-auth-recaptcha.service';
import { environment } from '../../environments/environment';
import { MaxkampusSession } from './session.service';

const AUTH = `${environment.apiUrl}/auth/`;

/** Requests that open or renew a session carry no bearer token. */
function isSessionRequest(request: HttpRequest<unknown>): boolean {
  return request.url.startsWith(AUTH) && /\/(login|register|google|google-register-complete|refresh-token|revoke-token|mfa\/(verify|setup|enable)|forgot-password|reset-password|confirm-email|resend-verification-email)$/
    .test(new URL(request.url, 'http://localhost').pathname);
}

/** The bearer token of the maxkampüs session on every API call; one refresh and retry after a 401. */
export const maxkampusAuthInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith(environment.apiUrl) || isSessionRequest(request)) return next(request);
  const session = inject(MaxkampusSession);
  const withToken = (token: string) => token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
  return from(session.accessToken() ? Promise.resolve(true) : session.signedIn() ? session.refresh() : Promise.resolve(false)).pipe(
    switchMap(() => next(withToken(session.accessToken()))),
    catchError(error => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !session.signedIn()) return throwError(() => error);
      return from(session.refresh()).pipe(switchMap(renewed => renewed ? next(withToken(session.accessToken())) : throwError(() => error)));
    })
  );
};

export const RECAPTCHA_HEADER = 'X-Auth-Recaptcha-Token';

/** The invisible robot check Identity asks for on these maxkampus.com account requests. */
export function maxkampusRecaptchaAction(request: HttpRequest<unknown>): string | null {
  if (request.method !== 'POST' || !request.url.startsWith(AUTH)) return null;
  const path = new URL(request.url, 'http://localhost').pathname;
  if (path.endsWith('/auth/forgot-password') || path.endsWith('/auth/resend-verification-email')) return 'auth_recovery';
  if (path.endsWith('/course-management/login') || path.endsWith('/course-management/google')) return 'auth_login';
  if (path.endsWith('/course-management/register') || path.endsWith('/course-management/google-register-complete')) return 'auth_register';
  return null;
}

export const maxkampusRecaptchaInterceptor: HttpInterceptorFn = (request, next) => {
  const action = maxkampusRecaptchaAction(request);
  if (!action) return next(request);
  return from(inject(StaffAuthRecaptchaService).createToken(action)).pipe(
    catchError(() => throwError(() => new HttpErrorResponse({
      status: 403, url: request.url,
      error: { code: 'Auth.CaptchaFailed', message: 'Robot doğrulaması tamamlanamadı. Sayfayı yenileyip yeniden deneyin.' }
    }))),
    switchMap(token => next(token ? request.clone({ setHeaders: { [RECAPTCHA_HEADER]: token } }) : request))
  );
};
