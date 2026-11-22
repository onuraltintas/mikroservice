import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { exerciseDisplayName, exerciseTypeLabel } from '../../../../shared/utils/exercise-type-label';
import { difficultyLabel } from '../../../../shared/utils/difficulty-label';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { map } from 'rxjs';
import { MeasuredProgressService } from '../../../../core/services/measured-progress.service';

interface DailyPlanItem {
  id: string;
  name: string;
  exerciseType: string;
  completed: boolean;
  difficultyLevel?: number;
  ageGroup?: string;
  /** Live test #55: the exercise's own estimate ("~5 dk" was written for every one). */
  estimatedDurationMinutes?: number;
  /** Programdaki sıra; oynatıcı bu sırayla açılır. */
  order?: number;
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
  programProgressId?: string;
}

@Component({
  selector: 'app-daily-plan-widget',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './daily-plan-widget.component.html',
  styleUrl: './daily-plan-widget.component.scss'
})
export class DailyPlanWidgetComponent implements OnInit {
  readonly typeLabel = exerciseTypeLabel;
  readonly displayName = exerciseDisplayName;
  readonly difficulty = difficultyLabel;
  private http = inject(HttpClient);
  private measuredProgress = inject(MeasuredProgressService);
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
    // One shared my-progress request for the whole home page (live test 19.11.2026).
    this.measuredProgress.load().pipe(map(value => value.raw as any)).subscribe({
      next: (progressData: any) => {
        // New students do not have a program yet; the API intentionally
        // returns 204 in that case, which Angular exposes as a null body.
        // Keep the dashboard in its empty-plan state instead of requesting a
        // made-up day for a program that does not exist.
        if (!progressData) {
          this.todaysPlan.set(null);
          this.loading.set(false);
          return;
        }

        const currentDay = ((progressData.currentWeek || 1) - 1) * 7 + (progressData.currentDay || 1);

        // Now fetch exercises for the current program day
        this.http.get<any>(`${this.apiUrl}/day/${currentDay}`, {
          headers: { 'X-Skip-Error-Toast': 'true' }
        }).subscribe({
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
                ageGroup: ex.ageGroupName || '',
                estimatedDurationMinutes: typeof ex.estimatedDurationMinutes === 'number' ? ex.estimatedDurationMinutes : undefined,
                order: typeof ex.order === 'number' ? ex.order : index + 1
              }));

              this.todaysPlan.set({
                id: 'today',
                title: `Gün ${currentDay} Egzersizleri`,
                dayNumber: currentDay,
                goalDescription: 'Günlük egzersizlerini tamamla',
                totalItems: totalCount,
                completedItems: completedCount,
                progressPercentage: progressPercentage,
                status: completedCount === totalCount ? 'Completed' : 'InProgress',
                isUnlocked: true,
                exercises: mappedExercises,
                isCompleted: completedCount === totalCount,
                programProgressId: progressData.programProgressId ?? progressData.id
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

    // Live test 22.11.2026: "Başla" opened the list instead of the exercise. It opens the next exercise itself,
    // with the same program parameters as the daily exercises page.
    if (nextExercise?.id && plan.programProgressId) {
      this.router.navigate(['/student/exercises/universal-player', nextExercise.id], {
        queryParams: { slotOrder: nextExercise.order, programDay: plan.dayNumber, programProgressId: plan.programProgressId },
        state: { fromDailyExercises: true, practiceMode: false, difficultyLevel: nextExercise.difficultyLevel }
      });
      return;
    }
    this.router.navigate(['/student/daily-exercises']);
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
