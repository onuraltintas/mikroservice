import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { StaffAuthService } from './staff-auth.service';

export const staffAuthInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiRequest(request.url)) return next(request);

  const auth = inject(StaffAuthService);
  const authorizedRequest = withSession(request, auth.getAccessToken());
  return next(authorizedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || isSessionEndpoint(request.url)) {
        return throwError(() => error);
      }

      return from(auth.refreshSession()).pipe(
        switchMap(refreshed => refreshed
          ? next(withSession(request, auth.getAccessToken()))
          : throwError(() => error))
      );
    })
  );
};

function isApiRequest(url: string): boolean {
  const apiBase = environment.apiUrl.replace(/\/+$/, '');
  return url === apiBase || url.startsWith(`${apiBase}/`);
}

function withSession(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return request.clone({
    withCredentials: true,
    setHeaders: token ? { Authorization: `Bearer ${token}` } : {}
  });
}

function isSessionEndpoint(url: string): boolean {
  return url.endsWith('/auth/coaching/login')
    || url.endsWith('/auth/speed-reading/login')
    || url.endsWith('/auth/refresh-token')
    || url.endsWith('/auth/revoke-token')
    || url.endsWith('/auth/mfa/setup')
    || url.endsWith('/auth/mfa/enable')
    || url.endsWith('/auth/mfa/verify');
}
