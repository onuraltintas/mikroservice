import { CommonModule } from '@angular/common';
import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { PromptOptions } from '../../../../../../shared/types/feedback.types';

export interface TextInputDialogData extends PromptOptions {
  title: string;
  message: string;
  value?: string;
}

@Component({
  selector: 'app-text-input-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  template: `
    <div class="ui-input-dialog">
      <h2 mat-dialog-title>{{ data.title }}</h2>
      <mat-dialog-content>
        <p class="ui-input-dialog__message">{{ data.message }}</p>
        <mat-form-field appearance="outline" class="ui-input-dialog__field">
          <mat-label>{{ data.placeholder || 'Yanıtınızı yazın' }}</mat-label>
          <textarea *ngIf="data.multiline" matInput [(ngModel)]="value" rows="5"></textarea>
          <input *ngIf="!data.multiline" matInput [(ngModel)]="value" />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-stroked-button type="button" (click)="close(null)">{{ data.cancelText || 'İptal' }}</button>
        <button mat-flat-button type="button" (click)="close(value)">{{ data.confirmText || 'Tamam' }}</button>
      </mat-dialog-actions>
    </div>
  `,
  styles: [`
    .ui-input-dialog { min-width: min(420px, calc(100vw - 48px)); padding: 8px 0 0; color: var(--ui-text); }
    .ui-input-dialog__message { margin: 0 0 16px; color: var(--ui-text-muted); line-height: 1.5; }
    .ui-input-dialog__field { width: 100%; }
    mat-dialog-actions { gap: 12px; padding: 8px 24px 24px; }
    @media (max-width: 480px) {
      .ui-input-dialog { min-width: 0; }
      mat-dialog-actions { flex-direction: column-reverse; align-items: stretch; }
      mat-dialog-actions button { width: 100%; }
    }
  `]
})
export class TextInputDialogComponent {
  value: string;

  constructor(
    private readonly dialogRef: MatDialogRef<TextInputDialogComponent, string | null>,
    @Inject(MAT_DIALOG_DATA) public readonly data: TextInputDialogData
  ) {
    this.value = data.value || '';
  }

  close(value: string | null): void {
    this.dialogRef.close(value);
  }
}
