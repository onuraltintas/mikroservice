import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, ExamResult, Goal } from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-coaching-portal-progress',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './coaching-portal-progress.component.html',
  styleUrl: './coaching-portal-progress.component.scss'
})
export class CoachingPortalProgressComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly goals = signal<Goal[]>([]);
  readonly examResults = signal<ExamResult[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit() {
    const studentId = this.authService.userProfile()?.id;
    if (!studentId) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğrenci profili bulunamadı.');
      return;
    }

    forkJoin({
      goals: this.coachingService.getStudentGoals(studentId, 1, 100),
      exams: this.coachingService.getStudentExamResults(studentId, 1, 100)
    }).subscribe({
      next: result => {
        this.goals.set(result.goals.items);
        this.examResults.set(result.exams.items);
      },
      error: () => {
        this.errorMessage.set('İlerleme verileri yüklenemedi.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  averageScore() {
    const results = this.examResults();
    if (results.length === 0) return null;
    const percentage = results.reduce((total, result) => total + (result.maxScore > 0 ? result.score / result.maxScore : 0), 0) / results.length;
    return Math.round(percentage * 100);
  }
}
