import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface ExitConfirmationData {
  title?: string;
  message?: string;
  confirmText?: string;
  cancelText?: string;
  progress?: number;
  timeSpent?: number;
  exerciseName?: string;
}

@Component({
  selector: 'app-exit-confirmation-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './exit-confirmation-dialog.html',
  styleUrl: './exit-confirmation-dialog.scss'
})
export class ExitConfirmationDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<ExitConfirmationDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ExitConfirmationData
  ) {
    // Set default values
    if (!this.data.progress) {
      this.data.progress = 0;
    }
  }

  getFormattedProgress(): string {
    return (this.data.progress || 0).toFixed(2);
  }

  formatTime(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  }

  onNoClick(): void {
    this.dialogRef.close(false);
  }
}
