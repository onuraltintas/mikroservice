import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ExerciseProgramService, DailyExercise, StudentProgressSummary } from '../../../core/services/exercise-program.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil } from 'rxjs/operators';
import { ExerciseTypeService } from '../../../core/services/exercise-type.service';

/**
 * Daily Exercises Component
 * 
 * Duolingo-style günlük egzersiz listesi.
 * Öğrenciye bugün yapması gereken egzersizleri gösterir.
 */
@Component({
  selector: 'app-daily-exercises',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './daily-exercises.component.html',
  styleUrls: ['./daily-exercises.component.scss']
})
export class DailyExercisesComponent extends BaseComponent implements OnInit {
  private readonly exerciseService = inject(ExerciseProgramService);
  private readonly exerciseTypeService = inject(ExerciseTypeService);
  // toaster inherited from BaseComponent
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Signals for reactive state
  exercises = signal<DailyExercise[]>([]);
  progress = signal<StudentProgressSummary | null>(null);
  // loading inherited from BaseComponent
  error = signal<string | null>(null);

  // Historical exercises state
  activeTab = signal<'today' | 'history'>('today');
  selectedTabIndex = 0; // For mat-tab-group binding
  selectedHistoricalDay = signal<number>(1);
  historicalExercises = signal<DailyExercise[]>([]);
  historicalLoading = signal<boolean>(false);
  availableHistoricalDays = signal<number[]>([]);

  // Dynamic colors map
  private dynamicTypeColors = new Map<string, string>();
  private dynamicBaseColors = new Map<string, string>();

  ngOnInit(): void {
    // Check query params to open historical tab
    this.route.queryParams.subscribe(params => {
      if (params['tab'] === 'history') {
        this.selectedTabIndex = 1;
        this.activeTab.set('history');
      }
    });

    this.loadData();
    this.loadExerciseTypes();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  /**
   * Egzersizleri ve ilerleme bilgisini yükle
   */
  loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    // Progress bilgisini yükle, ardından egzersizleri getir
    this.exerciseService.getMyProgress().subscribe({
      next: (prog) => {
        this.progress.set(prog);

        // Geçmiş günleri hesapla (bugünden önceki tüm günler)
        if (prog && prog.currentDay > 1) {
          const days = Array.from({ length: prog.currentDay - 1 }, (_, i) => i + 1);
          this.availableHistoricalDays.set(days);

          // Eğer history tab açıksa, ilk günü yükle
          if (this.activeTab() === 'history' && days.length > 0) {
            this.loadHistoricalDay(days[days.length - 1]);
          }
        }

        // Egzersizleri progress yüklendikten sonra yükle
        this.loadExercises();
      },
      error: (err) => {
        console.error('Progress yüklenemedi:', err);
        this.loading.set(false);
      }
    });
  }

  /**
   * Egzersiz tiplerini ve renklerini yükle
   */
  loadExerciseTypes(): void {
    this.exerciseTypeService.getActiveExerciseTypes().subscribe({
      next: (data) => {
        if (data && data.items) {
          data.items.forEach(type => {
            if (type.colorCode) {
              // Create gradient from color code matching ExercisesListComponent style
              this.dynamicTypeColors.set(type.name, `linear-gradient(135deg, ${type.colorCode} 0%, #444 100%)`);
              this.dynamicBaseColors.set(type.name, type.colorCode);
            }
          });
        }
      },
      error: (err) => console.error('Egzersiz tipleri renkleri yüklenemedi:', err)
    });
  }

  /**
   * Egzersizleri yükle
   */
  loadExercises(): void {
    this.loading.set(true);
    this.error.set(null);

    // First get current day from progress, then load exercises for that day
    const currentProgress = this.progress();
    if (currentProgress) {
      const currentDay = currentProgress.currentDay;
      this.exerciseService.getExercisesByDay(currentDay).subscribe({
        next: (data) => {
          if (data && data.length > 0) {
            this.exercises.set(data.sort((a, b) => a.order - b.order));
          } else {
            console.warn('No exercises returned for day', currentDay);
            this.exercises.set([]);
          }
          this.loading.set(false);
        },
        error: (err) => {
          console.error('Egzersizler yüklenirken hata:', err);
          this.error.set('Egzersizler yüklenirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.');
          this.loading.set(false);
        }
      });
    } else {
      this.loading.set(false);
    }
  }

  /**
   * Egzersizi başlat - Universal Player'a yönlendirir
   */
  startExercise(exercise: DailyExercise): void {
    // Tüm egzersizler artık universal-player üzerinden çalışıyor
    this.router.navigate(
      ['/student/exercises/universal-player', exercise.exerciseId],
      {
        state: {
          fromDailyExercises: true,
          exercise: exercise,
          difficultyLevel: exercise.difficultyLevel
        }
      }
    );
  }

  /**
   * Egzersizin disabled olup olmadığını kontrol et
   * (Sıralı ilerleme: önceki tamamlanmamışsa sonraki kilitli)
   */
  isExerciseDisabled(index: number): boolean {
    if (index === 0) return false;

    const exercises = this.exercises();
    const previousExercise = exercises[index - 1];
    return !previousExercise.isCompleted;
  }

  /**
   * Tamamlanan egzersiz sayısı
   */
  completedCount(): number {
    return this.exercises().filter(e => e.isCompleted).length;
  }

  /**
   * İlerleme yüzdesi
   */
  getProgressPercentage(): number {
    const total = this.exercises().length;
    if (total === 0) return 0;
    return Math.round((this.completedCount() / total) * 100);
  }

  // Map exercise types to gradients (FALLBACK)
  private typeColorMap: { [key: string]: string } = {
    // Göz Hareketleri (Purple/Indigo)
    'SchulteTable': 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
    'VisualExpansion': 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
    'Fixation': 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
    'Saccade': 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
    'EyeTracking': 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
    'VerticalReading': 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',

    // Hız Geliştirme (Blue/Cyan)
    'Tachistoscope': 'linear-gradient(135deg, #1976d2 0%, #2196F3 100%)',
    'SpeedReading': 'linear-gradient(135deg, #1976d2 0%, #2196F3 100%)',
    'RSVP': 'linear-gradient(135deg, #1976d2 0%, #2196F3 100%)',
    'Scanning': 'linear-gradient(135deg, #0288d1 0%, #26c6da 100%)',
    'Skimming': 'linear-gradient(135deg, #0288d1 0%, #26c6da 100%)',
    'RapidSerial': 'linear-gradient(135deg, #0288d1 0%, #26c6da 100%)',
    'WordGroups': 'linear-gradient(135deg, #0288d1 0%, #26c6da 100%)',
    'ParagrafSimulasyonu': 'linear-gradient(135deg, #0277bd 0%, #039be5 100%)',

    // Odaklanma (Orange/Amber)
    'TextFading': 'linear-gradient(135deg, #f57c00 0%, #ffb74d 100%)',
    'Focus': 'linear-gradient(135deg, #f57c00 0%, #ffb74d 100%)',
    'SubvocalizationReduction': 'linear-gradient(135deg, #ef6c00 0%, #ff9800 100%)',
    'RegressionReduction': 'linear-gradient(135deg, #ef6c00 0%, #ff9800 100%)',

    // Kavrama (Green/Teal)
    'Comprehension': 'linear-gradient(135deg, #43a047 0%, #66bb6a 100%)',
    'Chunking': 'linear-gradient(135deg, #43a047 0%, #66bb6a 100%)',
    'Vocabulary': 'linear-gradient(135deg, #00897b 0%, #4db6ac 100%)',
    'VocabularyBuilder': 'linear-gradient(135deg, #00897b 0%, #4db6ac 100%)',
    'Visualization': 'linear-gradient(135deg, #00897b 0%, #4db6ac 100%)',
    'VisualizationExercise': 'linear-gradient(135deg, #00897b 0%, #4db6ac 100%)',
    'ExamSimulation': 'linear-gradient(135deg, #2e7d32 0%, #43a047 100%)',

    // Okuma (Pink/Rose)
    'FreeReading': 'linear-gradient(135deg, #d81b60 0%, #f06292 100%)',
    'ErrorAnalysis': 'linear-gradient(135deg, #c2185b 0%, #e91e63 100%)',

    // Varsayılan (Gray)
    'default': 'linear-gradient(135deg, #546e7a 0%, #78909c 100%)'
  };

  /**
   * Egzersiz tipine göre renk kodu döndür
   */
  getExerciseColor(typeName: string): string {
    // 1. Try dynamic map from backend
    if (this.dynamicTypeColors.has(typeName)) {
      return this.dynamicTypeColors.get(typeName)!;
    }
    // 2. Fallback to static map
    return this.typeColorMap[typeName] || this.typeColorMap['default'];
  }

  getExerciseBaseColor(typeName: string): string {
    return this.dynamicBaseColors.get(typeName) || '#667eea';
  }

  /**
   * Egzersiz tipine göre ikon döndür
   */
  getExerciseIcon(typeName: string): string {
    const iconMap: { [key: string]: string } = {
      'Saccade': 'visibility',
      'EyeTracking': 'remove_red_eye',
      'ScrollingText': 'text_rotation_none',
      'SpeedReading': 'speed',
      'Tachistoscope': 'flash_on',
      'SchulteTable': 'grid_on',
      'PeripheralVision': 'panorama_horizontal',
      'VisualExpansion': 'zoom_out_map',
      'RapidSerial': 'fast_forward',
      'WordGroups': 'text_fields',
      'Scanning': 'search',
      'Skimming': 'skim_protection',
      'VerticalReading': 'height',
      'TextFading': 'opacity',
      'RegressionReduction': 'arrow_forward',
      'SubvocalizationReduction': 'volume_off',
      'Fixation': 'center_focus_strong',
      'Chunking': 'view_module',
      'VocabularyBuilder': 'school',
      'Vocabulary': 'school',
      'VisualizationExercise': 'image',
      'Visualization': 'image',
      'ExamSimulation': 'assignment',
      'ParagrafSimulasyonu': 'assignment'
    };

    return iconMap[typeName] || 'play_circle_outline';
  }

  /**
   * Tamamlanma zamanını formatla
   */
  formatCompletedTime(timestamp: string): string {
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);

    if (diffMins < 1) return 'Az önce';
    if (diffMins < 60) return `${diffMins} dakika önce`;
    if (diffMins < 1440) return `${Math.floor(diffMins / 60)} saat önce`;

    return date.toLocaleDateString('tr-TR', {
      day: 'numeric',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  /**
   * Tab değiştir (Bugün / Geçmiş Günler)
   */
  switchTab(index: number): void {
    this.selectedTabIndex = index;
    const tab = index === 0 ? 'today' : 'history';
    this.activeTab.set(tab);

    // Geçmiş sekmesine geçildiğinde günü yükle
    if (tab === 'history') {
      const days = this.availableHistoricalDays();
      if (days.length > 0) {
        // Eğer daha önce seçilmiş bir gün varsa onu, yoksa en son günü yükle
        const dayToLoad = this.selectedHistoricalDay() > 0 && this.selectedHistoricalDay() < days[0] + days.length
          ? this.selectedHistoricalDay()
          : days[days.length - 1];
        this.loadHistoricalDay(dayToLoad);
      }
    }
  }

  /**
   * Belirli bir günün egzersizlerini yükle
   */
  loadHistoricalDay(dayNumber: number): void {
    this.selectedHistoricalDay.set(dayNumber);
    this.historicalLoading.set(true);

    this.exerciseService.getExercisesByDay(dayNumber).subscribe({
      next: (exercises) => {
        this.historicalExercises.set(exercises.sort((a, b) => a.order - b.order));
        this.historicalLoading.set(false);
      },
      error: (err) => {
        console.error(`Gün ${dayNumber} yüklenirken hata:`, err);
        this.toaster.error(`Gün ${dayNumber} egzersizleri yüklenirken bir hata oluştu.`);
        this.historicalLoading.set(false);
      }
    });
  }

  /**
   * Geçmiş gündeki egzersizi başlat (pratik modu)
   * Pratik modunda completion kaydedilmez
   */
  startHistoricalExercise(exercise: DailyExercise): void {
    // Tüm egzersizler artık universal-player üzerinden çalışıyor
    this.router.navigate(
      ['/student/exercises/universal-player', exercise.exerciseId],
      {
        state: {
          fromDailyExercises: true,
          fromHistoricalDay: true,
          practiceMode: true, // Pratik modu - tamamlanma kaydedilmez
          exercise: exercise,
          difficultyLevel: exercise.difficultyLevel
        }
      }
    );
  }

  /**
   * Geçmiş gün egzersizlerinin tamamlanma sayısı
   */
  historicalCompletedCount(): number {
    return this.historicalExercises().filter(e => e.isCompleted).length;
  }
}
