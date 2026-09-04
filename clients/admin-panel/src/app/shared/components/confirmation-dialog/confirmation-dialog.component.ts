import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface ConfirmationDialogData {
    title: string;
    message: string;
    confirmText?: string;
    cancelText?: string;
}

@Component({
    selector: 'app-confirmation-dialog',
    standalone: true,
    imports: [MatDialogModule, MatButtonModule, MatIconModule],
    template: `
        <div class="confirmation-dialog">
            <div class="dialog-header">
                <mat-icon class="dialog-icon" aria-hidden="true">help_outline</mat-icon>
                <h2 class="dialog-title">{{ data.title }}</h2>
            </div>

            <p class="dialog-message">{{ data.message }}</p>

            <div class="dialog-actions">
                <button mat-stroked-button type="button" class="cancel-btn" (click)="onCancel()">
                    {{ data.cancelText || 'İptal' }}
                </button>
                <button mat-flat-button type="button" class="confirm-btn" (click)="onConfirm()">
                    {{ data.confirmText || 'Tamam' }}
                </button>
            </div>
        </div>
    `,
    styles: [`
        .confirmation-dialog {
            padding: 28px;
            max-width: 450px;
            background: var(--ui-surface);
            color: var(--ui-text);
        }

        .dialog-header {
            display: flex;
            align-items: center;
            gap: 12px;
            margin-bottom: 16px;
        }

        .dialog-icon {
            color: var(--ui-brand);
        }

        .dialog-title {
            margin: 0;
            color: var(--ui-text);
            font-size: 1.2rem;
            font-weight: 800;
        }

        .dialog-message {
            margin: 0 0 24px;
            color: var(--ui-text-muted);
            font-size: 0.95rem;
            line-height: 1.55;
        }

        .dialog-actions {
            display: flex;
            justify-content: flex-end;
            gap: 12px;
        }

        .cancel-btn,
        .confirm-btn {
            min-width: 100px;
            min-height: var(--ui-control-height);
            font-weight: 700;
        }

        .cancel-btn {
            color: var(--ui-text-muted);
        }

        .confirm-btn {
            background: var(--ui-brand);
            color: #fff;
        }

        .confirm-btn:hover {
            background: var(--ui-brand-strong);
        }

        @media (max-width: 480px) {
            .confirmation-dialog {
                padding: 22px;
            }

            .dialog-actions {
                flex-direction: column-reverse;
            }

            .cancel-btn,
            .confirm-btn {
                width: 100%;
            }
        }
    `]
})
export class ConfirmationDialogComponent {
    constructor(
        private readonly dialogRef: MatDialogRef<ConfirmationDialogComponent>,
        @Inject(MAT_DIALOG_DATA) public readonly data: ConfirmationDialogData
    ) { }

    onConfirm(): void {
        this.dialogRef.close(true);
    }

    onCancel(): void {
        this.dialogRef.close(false);
    }
}
