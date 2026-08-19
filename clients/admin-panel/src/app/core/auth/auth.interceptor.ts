import { HttpInterceptorFn, HttpErrorResponse, HttpRequest } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';
import { catchError, switchMap } from 'rxjs/operators';
import { from, throwError } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
    const authService = inject(AuthService);
    const platformId = inject(PLATFORM_ID);

    // Check if request is for our API
    if (req.url.startsWith(environment.apiUrl)) {
        const token = authService.getToken();
        req = withSession(req, token);
    }

    return next(req).pipe(
        catchError((error: HttpErrorResponse) => {
            if (error.status === 401
                && isPlatformBrowser(platformId)
                && req.url.startsWith(environment.apiUrl)
                && !isSessionEndpoint(req.url)) {
                return from(authService.refreshSession()).pipe(
                    switchMap(refreshed => {
                        if (!refreshed) {
                            void authService.logout();
                            return throwError(() => error);
                        }

                        return next(withSession(req, authService.getToken())).pipe(
                            catchError(retryError => {
                                if (retryError instanceof HttpErrorResponse && retryError.status === 401) {
                                    void authService.logout();
                                }
                                return throwError(() => retryError);
                            }));
                    }));
            }
            return throwError(() => error);
        })
    );
};

function withSession(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
    return req.clone({
        withCredentials: true,
        setHeaders: token ? { Authorization: `Bearer ${token}` } : {}
    });
}

function isSessionEndpoint(url: string): boolean {
    return url.endsWith('/auth/login')
        || url.endsWith('/auth/google-login')
        || url.endsWith('/auth/refresh-token')
        || url.endsWith('/auth/revoke-token');
}
