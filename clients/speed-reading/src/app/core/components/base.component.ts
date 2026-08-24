import { Directive, OnDestroy, inject, signal, WritableSignal } from '@angular/core';
import { Subject } from 'rxjs';
import { ToasterService } from '../services/toaster.service';

/**
 * Base component class that provides common functionality
 * All components should extend this to get automatic cleanup and error handling
 */
@Directive()
export abstract class BaseComponent implements OnDestroy {
    protected toaster = inject(ToasterService);

    /**
     * Subject for managing subscriptions
     * Use with takeUntil(this.destroy$) to prevent memory leaks
     */
    protected destroy$ = new Subject<void>();

    /**
     * Loading state for the component (as a signal)
     */
    loading: WritableSignal<boolean> = signal(false);

    /**
     * Cleanup subscriptions on component destroy
     */
    ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();
    }

    /**
     * Handle errors with user-friendly messages
     * @param error The error object
     * @param customMessage Optional custom message to show
     */
    protected handleError(error: any, customMessage?: string): void {
        console.error('Component error:', error);

        const message = customMessage ||
            error?.message ||
            error?.error?.message ||
            'Bir hata oluştu';

        this.toaster.error(message);
    }

    /**
     * Show success message
     * @param message Success message to display
     * @param duration Duration in milliseconds (default: 3000)
     */
    protected handleSuccess(message: string, duration: number = 3000): void {
        this.toaster.success(message, duration);
    }

    /**
     * Show info message
     * @param message Info message to display
     * @param duration Duration in milliseconds (default: 3000)
     */
    protected handleInfo(message: string, duration: number = 3000): void {
        this.toaster.info(message, duration);
    }

    /**
     * Show warning message
     * @param message Warning message to display
     * @param duration Duration in milliseconds (default: 3000)
     */
    protected handleWarning(message: string, duration: number = 3000): void {
        this.toaster.warning(message, duration);
    }

    /**
     * Confirm action with user
     * @param message Confirmation message
     * @returns Promise<boolean> - true if confirmed
     */
    protected async confirm(message: string): Promise<boolean> {
        return this.toaster.confirm(message);
    }
}
