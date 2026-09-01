import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { ToasterService } from '../services/toaster.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);
  const toaster = inject(ToasterService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let errorMessage = 'Bir hata oluştu';

      if (error.error instanceof ErrorEvent) {
        // Client-side error
        errorMessage = `Hata: ${error.error.message}`;
      } else {
        // Server-side error
        switch (error.status) {
          case 400:
            // C# backend Message (büyük M) veya message (küçük m) kullanabilir
            errorMessage = error.error?.Message || error.error?.message || 'Geçersiz istek';
            break;

          case 401:
            // Don't auto-logout here! Auth interceptor handles 401 by trying refresh token first.
            // If refresh fails, auth interceptor will call logout.
            errorMessage = error.error?.Message || error.error?.message || 'Oturum süreniz doldu.';
            // Skip showing toast for 401 (handled by auth interceptor)
            return throwError(() => error);

          case 403:
            // Modal-level access management can handle this response locally.
            errorMessage = 'Bu işlem için yetkiniz yok';
            if (!req.headers.has('X-Skip-Forbidden-Redirect')) {
              router.navigate(['/error/403']);
            }
            break;

          case 404:
            errorMessage = error.error?.Message || error.error?.message || 'Kaynak bulunamadı';
            break;

          case 409:
            errorMessage = error.error?.Message || error.error?.message || 'Çakışma hatası';
            break;

          case 422:
            // Validation error
            errorMessage = 'Geçersiz veri';
            if (error.error?.errors) {
              const validationErrors = Object.values(error.error.errors).flat();
              if (validationErrors.length > 0) {
                errorMessage = validationErrors[0] as string;
              }
            }
            break;

          case 500:
            errorMessage = 'Sunucu hatası. Lütfen daha sonra tekrar deneyin.';
            router.navigate(['/error/500']);
            break;

          case 503:
            errorMessage = error.error?.Message || error.error?.message || 'Servis geçici olarak kullanılamıyor';
            break;

          default:
            errorMessage = error.error?.Message || error.error?.message || `Hata kodu: ${error.status}`;
        }
      }

      // Skip toast for 401 (refresh token handled by auth interceptor), 403, and 500
      const skipToast = [401, 403, 500].includes(error.status) || req.headers.has('X-Skip-Error-Toast');

      if (!skipToast) {
        toaster.error(errorMessage);
      }

      console.error('HTTP Error:', {
        status: error.status,
        message: errorMessage,
        url: error.url,
        error: error.error
      });

      return throwError(() => error);
    })
  );
};
