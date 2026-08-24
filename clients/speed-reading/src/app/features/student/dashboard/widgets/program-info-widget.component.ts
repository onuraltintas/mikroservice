import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StudentProgramService, StudentProgramInfo, ProgramType } from '../../../../core/services/student-program.service';

/**
 * Program Info Widget Component
 * Displays student's current program information on dashboard
 */
@Component({
  selector: 'app-program-info-widget',
  standalone: true,
  imports: [
    CommonModule
  ],
  templateUrl: './program-info-widget.component.html',
  styleUrls: ['./program-info-widget.component.scss']
})
export class ProgramInfoWidgetComponent implements OnInit {
  private studentProgramService = inject(StudentProgramService);

  programInfo = signal<StudentProgramInfo | null>(null);
  loading = signal<boolean>(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadProgramInfo();
  }

  loadProgramInfo(): void {
    this.loading.set(true);
    this.studentProgramService.getMyProgram().subscribe({
      next: (data) => {
        this.programInfo.set(data);
        this.error.set(null);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Program bilgisi yüklenirken hata oluştu');
        this.loading.set(false);
      }
    });
  }

  isExamPrep(): boolean {
    const program = this.programInfo();
    return program ? program.programType === ProgramType.ExamPrep : false;
  }

  getProgramTypeColor(): string {
    const program = this.programInfo();
    return program ? this.studentProgramService.getProgramTypeColor(program.programType) : 'primary';
  }

  getDifficultyDescription(): string {
    const program = this.programInfo();
    return program ? this.studentProgramService.getDifficultyDescription(program.currentDifficultyLevel) : '';
  }

  getProgressPercentage(): number {
    const program = this.programInfo();
    return program ? this.studentProgramService.getProgressPercentage(program) : 0;
  }

  isMaxDifficulty(): boolean {
    const program = this.programInfo();
    return program ? program.currentDifficultyLevel === program.maxDifficultyLevel : false;
  }
}
