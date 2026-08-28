import { Component, OnInit, OnDestroy, inject, signal, computed, effect, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AssessmentService } from '../../../services/assessment.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { switchMap } from 'rxjs';

interface AssessmentExercise {
  id: string;
  name: string;
  description: string;
  completed: boolean;
  icon: string;
  typeName: string;
  difficultyLevel: number;
}

@Component({
  selector: 'app-assessment',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule
  ],
  templateUrl: './assessment.component.html',
  styleUrl: './assessment.component.scss'
})
export class AssessmentComponent implements OnInit, OnDestroy {
  private assessmentService = inject(AssessmentService);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  // State
  loading = signal(true);
  calculating = signal(false);
  exercises = signal<AssessmentExercise[]>([]);
  exerciseIds = signal<{ readingSpeed: string; comprehension: string; focus: string } | null>(null);
  currentStep = signal(0);
  showResult = signal(false);
  result = signal<any>(null);
  assessmentAttemptId = signal<string | null>(null);

  // Comprehension Exercise State


  constructor() {
    // Listen to assessment service refresh trigger (when exercises are completed)
    // Use effect to reactively watch ONLY the trigger signal
    let lastProcessedTrigger = 0;
    effect(() => {
      const trigger = this.assessmentService.getRefreshTrigger()();

      // Use untracked to read other signals without creating dependencies
      untracked(() => {
        // Only process if trigger increased
        if (trigger > 0 && trigger > lastProcessedTrigger) {
          lastProcessedTrigger = trigger;
          this.loadAssessmentExercises();
        }
      });
    });
  }

  ngOnInit(): void {
    this.loadAssessmentExercises();
  }

  ngOnDestroy(): void {
    // Cleanup if needed
  }

  loadAssessmentExercises(): void {
    this.loading.set(true);

    this.assessmentService.startAttempt({
      phase: 1,
      formVersion: 'tr-baseline-v1',
      language: 'tr-TR',
      expectedExerciseCount: 3
    }).pipe(
      switchMap(attempt => {
        this.assessmentAttemptId.set(attempt.id);
        return this.assessmentService.getAssessmentExercises();
      })
    ).subscribe({
      next: (data: any) => {
        if (data) {
          // Store exercise IDs
          this.exerciseIds.set({
            readingSpeed: data.comprehensionExerciseId,
            comprehension: data.visualExpansionExerciseId,
            focus: data.focusExerciseId
          });

          // Dynamic exercise loading
          if (data.exercises && data.exercises.length > 0) {
            this.exercises.set(data.exercises.map((ex: any) => ({
              id: ex.id,
              name: ex.name,
              description: ex.description,
              completed: ex.isCompleted,
              typeName: ex.typeName,
              difficultyLevel: ex.difficultyLevel || 2,
              icon: this.getIconForType(ex.typeName)
            })));
          } else {
            console.warn('No exercises list returned from backend.');
            this.exercises.set([]);
          }
        } else {
          this.toaster.error('Değerlendirme egzersizleri yüklenemedi');
        }

        this.loading.set(false);
      },
      error: (error) => {
        console.error('Assessment exercises error:', error);
        this.toaster.error('Değerlendirme egzersizleri yüklenirken hata oluştu', 3000);
        this.loading.set(false);
      }
    });
  }

  startExercise(index: number): void {
    const exercise = this.exercises()[index];

    // Navigate to universal player for ALL exercises including Comprehension
    this.router.navigate(['/student/exercises/universal-player', exercise.id], {
      queryParams: {
        assessmentMode: 'true',
        assessmentStep: index + 1,
        assessmentAttemptId: this.assessmentAttemptId(),
        returnUrl: '/student/assessment'
      },
      state: {
        assessmentMode: true,
        assessmentStep: index + 1,
        assessmentAttemptId: this.assessmentAttemptId(),
        returnUrl: '/student/assessment'
      }
    });
  }



  completedCount = computed(() => {
    return this.exercises().filter(e => e.completed).length;
  });

  progress = computed(() => {
    const total = this.exercises().length;
    return total > 0 ? (this.completedCount() / total) * 100 : 0;
  });



  calculateLevel(): void {
    this.calculating.set(true);

    this.assessmentService.calculateLevel([], this.assessmentAttemptId() ?? undefined).subscribe({
      next: (data: any) => {
        if (data) {
          this.result.set(data);
          this.showResult.set(true);
        } else {
          this.toaster.error('Seviye hesaplanamadı');
        }
        this.calculating.set(false);
      },
      error: (error) => {
        console.error('Calculate level error:', error);

        // Check if it's because exercises not completed
        if (error.status === 400) {
          this.toaster.error('Lütfen önce tüm egzersizleri tamamlayın', 3000);
        } else {
          this.toaster.error('Seviye hesaplanırken hata oluştu', 3000);
        }

        this.calculating.set(false);
      }
    });
  }

  goToDashboard(): void {
    this.router.navigate(['/student/dashboard']);
  }
  getIconForType(typeName: string): string {
    switch (typeName) {
      case 'Comprehension': return 'menu_book';
      case 'Tachistoscope': return 'flash_on';
      case 'VisualExpansion': return 'visibility';
      case 'Fixation': return 'my_location';
      case 'Focus': return 'center_focus_strong'; // Legacy fallback
      default: return 'assignment';
    }
  }

  getExerciseTypeLabel(typeName: string): string {
    switch (typeName) {
      case 'Comprehension': return 'Okuduğunu Anlama';
      case 'Tachistoscope': return 'Hızlı Algı';
      case 'VisualExpansion': return 'Görsel Genişleme';
      case 'Fixation': return 'Göz Sabitleme';
      case 'Focus': return 'Odaklanma';
      default: return typeName;
    }
  }
}
