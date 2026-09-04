import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarConfig } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import { TextInputDialogComponent, TextInputDialogData } from '../../shared/components/text-input-dialog/text-input-dialog.component';
import { ConfirmOptions, PromptOptions, ToastOptions } from '../../../../../shared/types/feedback.types';
import {
    ConfirmationDialogComponent,
    ConfirmationDialogData
} from '../../shared/components/confirmation-dialog/confirmation-dialog.component';

@Injectable({
    providedIn: 'root'
})
export class ToasterService {
    private readonly snackBar = inject(MatSnackBar);
    private readonly dialog = inject(MatDialog);

    private readonly defaultConfig: MatSnackBarConfig = {
        horizontalPosition: 'end',
        verticalPosition: 'top',
        duration: 4000,
        panelClass: ['ui-toast'],
        politeness: 'polite'
    };

    success(message: string, options?: ToastOptions): void;
    success(message: string, title?: string): void;
    success(message: string, value: ToastOptions | string = {}): void {
        this.open('success', message, this.normalizeToastOptions(value, 'Başarılı', 4000));
    }

    error(message: string, options?: ToastOptions): void;
    error(message: string, title?: string): void;
    error(message: string, value: ToastOptions | string = {}): void {
        this.open('error', message, this.normalizeToastOptions(value, 'Hata', 5000));
    }

    warning(message: string, options?: ToastOptions): void;
    warning(message: string, title?: string): void;
    warning(message: string, value: ToastOptions | string = {}): void {
        this.open('warning', message, this.normalizeToastOptions(value, 'Uyarı', 4500));
    }

    info(message: string, options?: ToastOptions): void;
    info(message: string, title?: string): void;
    info(message: string, value: ToastOptions | string = {}): void {
        this.open('info', message, this.normalizeToastOptions(value, 'Bilgi', 3500));
    }

    async confirm(message: string, options: ConfirmOptions = {}): Promise<boolean> {
        const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
            width: 'min(460px, calc(100vw - 32px))',
            maxWidth: 'calc(100vw - 32px)',
            panelClass: 'ui-confirm-dialog',
            autoFocus: false,
            data: {
                title: options.title || 'Onay',
                message,
                confirmText: options.confirmText,
                cancelText: options.cancelText
            } as ConfirmationDialogData
        });

        const result = await firstValueFrom(dialogRef.afterClosed());
        return result === true;
    }

    dismiss(): void {
        this.snackBar.dismiss();
    }

    alert(message: string, options?: ToastOptions): void {
        this.info(message, { title: options?.title || 'Bilgi', duration: options?.duration || 5000 });
    }

    async prompt(message: string, value = '', options: PromptOptions = {}): Promise<string | null> {
        const dialogRef = this.dialog.open(TextInputDialogComponent, {
            width: 'min(520px, calc(100vw - 32px))',
            maxWidth: 'calc(100vw - 32px)',
            panelClass: 'ui-confirm-dialog',
            autoFocus: false,
            data: {
                ...options,
                title: options.title || 'Bilgi',
                message,
                value
            } as TextInputDialogData
        });

        const result = await firstValueFrom(dialogRef.afterClosed());
        return result ?? null;
    }

    private normalizeToastOptions(
        value: ToastOptions | string,
        defaultTitle: string,
        defaultDuration: number
    ): Required<Pick<ToastOptions, 'title' | 'duration' | 'actionLabel'>> {
        if (typeof value === 'string') {
            return { title: value, duration: defaultDuration, actionLabel: 'Kapat' };
        }

        return {
            title: value.title || defaultTitle,
            duration: value.duration || defaultDuration,
            actionLabel: value.actionLabel || 'Kapat'
        };
    }

    private open(
        type: 'success' | 'error' | 'warning' | 'info',
        message: string,
        options: Required<Pick<ToastOptions, 'title' | 'duration' | 'actionLabel'>>
    ): void {
        const text = options.title && options.title !== message ? `${options.title}: ${message}` : message;

        this.snackBar.open(text, options.actionLabel, {
            ...this.defaultConfig,
            duration: options.duration,
            panelClass: ['ui-toast', `ui-toast--${type}`],
            politeness: type === 'error' ? 'assertive' : 'polite'
        });
    }
}
