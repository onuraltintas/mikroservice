import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { StudentProgramService } from '../../../core/services/student-program.service';

export interface ProgramCompletionData {
  completedProgramName: string;
  stats: {
    totalDays: number;
    averageSuccessRate: number;
    longestStreak: number;
    totalExercises: number;
  };
  recommendedProgram?: {
    templateId: string;
    name: string;
    description: string;
    totalDays: number;
    totalWeeks: number;
    difficultyRange: string;
    recommendationReason: string;
    programType: string;
  };
}

@Component({
  selector: 'app-program-completion-modal',
  standalone: true,
  imports: [
    CommonModule
  ],
  templateUrl: './program-completion-modal.html',
  styleUrls: ['./program-completion-modal.scss']
})
export class ProgramCompletionModalComponent {
  isLoading = false;

    constructor(
    @Inject(MAT_DIALOG_DATA) public data: ProgramCompletionData,
    private dialogRef: MatDialogRef<ProgramCompletionModalComponent>,
    private studentProgramService: StudentProgramService
  ) { }

  async startProgram() {
    if (!this.data.recommendedProgram) return;

    this.isLoading = true;

    try {
      const response = await firstValueFrom(
        this.studentProgramService.startProgram(this.data.recommendedProgram.templateId)
      );

      // Show success feedback inline (snackbar removed)
      console.log(response.message || 'Program başlatıldı!');

      this.dialogRef.close({ started: true });

      // Reload page to refresh dashboard
      setTimeout(() => {
        window.location.reload();
      }, 500);
    } catch (error: any) {
      console.error('Error starting program:', error);

      // Show error feedback inline (snackbar removed)
      const errorMessage = error.error?.message || 'Program başlatılırken bir hata oluştu.';
      console.error(errorMessage);

      this.isLoading = false;
    }
  }

  later() {
    this.dialogRef.close({ started: false });
  }
}
