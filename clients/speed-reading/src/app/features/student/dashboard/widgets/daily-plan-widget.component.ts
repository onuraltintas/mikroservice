import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';

interface DailyPlanItem {
  id: string;
  name: string;
  exerciseType: string;
  completed: boolean;
  difficultyLevel?: number;
  ageGroup?: string;
}

interface TodaysPlan {
  id: string;
  title: string;
  dayNumber: number;
  goalDescription: string;
  totalItems: number;
  completedItems: number;
  progressPercentage: number;
  status: string;
  isUnlocked: boolean;
  exercises?: DailyPlanItem[];
  isCompleted?: boolean;
}

@Component({
  selector: 'app-daily-plan-widget',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './daily-plan-widget.component.html',
  styleUrl: './daily-plan-widget.component.scss'
})
export class DailyPlanWidgetComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private apiUrl = `${environment.speedReadingApiUrl}/daily-progress`;

  todaysPlan = signal<TodaysPlan | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  formattedDate = signal('');

  ngOnInit(): void {
    const date = new Date();
    const formatter = new Intl.DateTimeFormat('tr-TR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      weekday: 'long'
    });
    this.formattedDate.set(formatter.format(date));

    this.loadTodaysPlan();
  }

  loadTodaysPlan(): void {
    this.loading.set(true);
    this.error.set(null);

    // First, get current program info to know which day we're on
    this.http.get<any>(`${this.apiUrl}/my-progress`, {
      headers: { 'X-Skip-Error-Toast': 'true' }
    }).subscribe({
      next: (progressData: any) => {
        const currentDay = progressData.currentDay || 1;

        // Now fetch exercises for the current program day
        this.http.get<any>(`${this.apiUrl}/day/${currentDay}`).subscribe({
          next: (res: any) => {
            const exercises: any[] = Array.isArray(res) ? res : (res?.exercises ?? []);

            if (exercises && exercises.length > 0) {
              const completedCount = exercises.filter((ex: any) => ex.isCompleted).length;
              const totalCount = exercises.length;
              const progressPercentage = totalCount > 0 ? (completedCount / totalCount) * 100 : 0;

              // Map exercises to DailyPlanItem format
              const mappedExercises = exercises.map((ex: any, index: number) => ({
                id: ex.exerciseId,
                name: ex.title,
                exerciseType: ex.exerciseTypeName,
                completed: ex.isCompleted,
                difficultyLevel: ex.difficultyLevel || 1,
                ageGroup: ex.ageGroupName || ''
              }));

              this.todaysPlan.set({
                id: 'today',
                title: `Gün ${currentDay} Egzersizleri`,
                dayNumber: currentDay,
                goalDescription: 'Günlük egzersizlerinizi tamamlayın',
                totalItems: totalCount,
                completedItems: completedCount,
                progressPercentage: progressPercentage,
                status: completedCount === totalCount ? 'Completed' : 'InProgress',
                isUnlocked: true,
                exercises: mappedExercises,
                isCompleted: completedCount === totalCount
              });
            } else {
              this.todaysPlan.set(null);
            }

            this.loading.set(false);
          },
          error: (err) => {
            console.error('❌ Error loading day exercises:', err);
            this.error.set('Günlük egzersizler yüklenirken hata oluştu');
            this.loading.set(false);
          }
        });
      },
      error: (err) => {
        console.error('❌ Error loading progress:', err);
        this.error.set('İlerleme bilgisi yüklenirken hata oluştu');
        this.loading.set(false);
      }
    });
  }

  continuePlan(): void {
    const plan = this.todaysPlan();
    if (!plan) return;

    const nextExercise = plan.exercises?.find(ex => !ex.completed);

    if (nextExercise) {
      // Navigate to daily exercises page
      this.router.navigate(['/student/daily-exercises']);
    } else {
      this.router.navigate(['/student/daily-exercises']);
    }
  }

  goToSeries(): void {
    // Navigate to new daily exercises page
    this.router.navigate(['/student/daily-exercises']);
  }

  refresh(): void {
    this.loadTodaysPlan();
  }

  repeatExercises(): void {
    // Navigate to daily exercises page to repeat today's exercises
    this.router.navigate(['/student/daily-exercises']);
  }
}
