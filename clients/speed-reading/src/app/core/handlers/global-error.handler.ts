import { ErrorHandler, Injectable, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { ToasterService } from '../services/toaster.service';

/**
 * Global error handler for the application
 * Catches all unhandled errors and provides user-friendly messages
 */
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
    private toaster = inject(ToasterService);
    private router = inject(Router);

    // Throttle client errors to prevent infinite loops
    private lastErrorTime = 0;
    private errorThrottleMs = 1000; // 1 second between error toasts

    // Known Angular Material lifecycle errors that should be suppressed
    private suppressedErrorPatterns = [
        'mat-form-field must contain a MatFormFieldControl',
        'NG0912', // Component ID collision
        'ExpressionChangedAfterItHasBeenCheckedError',
        'ResizeObserver loop completed with undelivered notifications',
        // CDK Table dataSource type mismatch (dataStream is undefined) - various browser formats:
        'can\'t access property "pipe"',   // Firefox (double quotes)
        "can't access property 'pipe'",    // Firefox (single quotes)
        "(reading 'pipe')",                // Chrome
        'Cannot read properties of undefined',
    ];

    // Stack trace patterns that indicate suppressible errors
    private suppressedStackPatterns = [
        '_observeRenderChanges',
        'observeRenderChanges',
    ];

    handleError(error: any): void {
        // Check if this is a known Angular lifecycle error that should be suppressed
        const errorMessage = error?.message || error?.toString() || '';
        const errorStack = error?.stack || '';
        const isSuppressedMessage = this.suppressedErrorPatterns.some(p => errorMessage.includes(p));
        const isSuppressedStack = this.suppressedStackPatterns.some(p => errorStack.includes(p));
        if (isSuppressedMessage || isSuppressedStack) {
            // Log but don't show toast to prevent loops
            console.warn('[Suppressed Error]:', errorMessage);
            return;
        }

        // HTTP errors
        if (error instanceof HttpErrorResponse) {
            this.handleHttpError(error);
        }
        // Client-side errors
        else if (error instanceof Error) {
            this.handleClientError(error);
        }
        // Unknown errors
        else {
            console.error('Unknown error:', error);
            this.showThrottledError('Beklenmeyen bir hata oluştu');
        }
    }

    private handleHttpError(error: HttpErrorResponse): void {
        console.error('HTTP Error:', error);

        switch (error.status) {
            case 0:
                // Network error
                this.toaster.error('Sunucuya bağlanılamıyor. İnternet bağlantınızı kontrol edin.');
                break;

            case 400:
                // Bad request - validation errors
                if (error.error?.errors && Array.isArray(error.error.errors)) {
                    this.toaster.error(error.error.errors.join(', '));
                } else {
                    this.toaster.error(error.error?.message || 'Geçersiz istek');
                }
                break;

            case 401:
                // Unauthorized - redirect to login
                this.toaster.error('Oturum süreniz doldu. Lütfen tekrar giriş yapın.');
                this.router.navigate(['/auth/login']);
                break;

            case 403:
                // Forbidden
                this.toaster.error('Bu işlem için yetkiniz bulunmamaktadır.');
                break;

            case 404:
                // Not found
                this.toaster.error('İstenen kayıt bulunamadı.');
                break;

            case 409:
                // Conflict
                this.toaster.error(error.error?.message || 'Bu işlem çakışma yarattı.');
                break;

            case 422:
                // Unprocessable entity - validation
                this.toaster.error(error.error?.message || 'Girilen veriler geçersiz.');
                break;

            case 500:
            case 502:
            case 503:
            case 504:
                // Server errors
                this.toaster.error('Sunucu hatası oluştu. Lütfen daha sonra tekrar deneyin.');
                break;

            default:
                this.toaster.error(error.error?.message || 'Bir hata oluştu.');
        }
    }

    private handleClientError(error: Error): void {
        console.error('Client Error:', error);

        // Don't show technical errors to users in production
        if (this.isProduction()) {
            this.showThrottledError('Bir hata oluştu. Lütfen sayfayı yenileyin.');
        } else {
            this.showThrottledError(`Hata: ${error.message}`);
        }
    }

    /**
     * Show error toast with throttling to prevent infinite loops
     */
    private showThrottledError(message: string): void {
        const now = Date.now();
        if (now - this.lastErrorTime > this.errorThrottleMs) {
            this.lastErrorTime = now;
            this.toaster.error(message);
        }
    }

    private isProduction(): boolean {
        // Check if running in production mode
        return !window.location.hostname.includes('localhost');
    }
}
