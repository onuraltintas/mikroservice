import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpEvent } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { catchError, switchMap, throwError, Observable, BehaviorSubject, filter, take } from 'rxjs';

let isRefreshing = false;
const refreshTokenSubject: BehaviorSubject<string | null> = new BehaviorSubject<string | null>(null);
const anonymousAuthEndpoints = [
  '/auth/login',
  '/auth/register',
  '/auth/register/student',
  '/auth/register/teacher',
  '/auth/register/institution',
  '/auth/register/parent',
  '/auth/register-student',
  '/auth/register-teacher',
  '/auth/register-institution',
  '/auth/register-parent',
  '/auth/refresh-token',
  '/auth/confirm-email',
  '/auth/resend-verification-email',
  '/auth/google-login',
  '/auth/google',
  '/auth/mfa/setup',
  '/auth/mfa/enable',
  '/auth/mfa/verify',
  '/auth/forgot-password',
  '/auth/reset-password',
  '/auth/revoke-token'
];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router); // Inject Router at top level
  const token = authService.token;
  const isAuthRequest = req.url.includes('/auth/');
  const isAnonymousAuthRequest = anonymousAuthEndpoints.some(endpoint => req.url.endsWith(endpoint));

  // Anonymous auth endpoints use only the HttpOnly refresh cookie. Protected auth endpoints
  // (for example MFA setup and Google account linking) still require the access token.
  if (token && (!isAuthRequest || !isAnonymousAuthRequest)) {
    req = addToken(req, token);
  }

  return next(req).pipe(
    catchError(error => {
      if (error.status === 401 && authService.currentUserValue && !isAuthRequest) {
        return handle401Error(req, next, authService, router);
      }
      return throwError(() => error);
    })
  );
};

function addToken(request: HttpRequest<any>, token: string) {
  return request.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  });
}

function handle401Error(
  request: HttpRequest<any>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router
): Observable<HttpEvent<any>> {
  if (!isRefreshing) {
    isRefreshing = true;
    refreshTokenSubject.next(null);

    return authService.refreshToken().pipe(
      switchMap((response) => {
        isRefreshing = false;
        refreshTokenSubject.next(response.token);
        return next(addToken(request, response.token));
      }),
      catchError((err) => {
        isRefreshing = false;
        authService.logout();
        router.navigate(['/auth/login']);
        return throwError(() => err);
      })
    );
  } else {
    // Other requests wait for the refresh call to complete
    return refreshTokenSubject.pipe(
      filter(token => token !== null),
      take(1),
      switchMap((token) => next(addToken(request, token!)))
    );
  }
}
