/**
 * Universal Exercise Player Component
 * Backend'den gelen engineType'a göre dinamik olarak mini-engine yükler.
 * 
 * Kullanım: /student/exercises/universal-player/:exerciseId
 */

import { ExerciseResult as CoreExerciseResult } from '../../../../core/models/exercise.model';

import { Component, OnInit, OnDestroy, ChangeDetectorRef, ViewChild, ElementRef, AfterViewChecked, HostListener, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';

import { EngineFactory, EngineType } from './engines/engine-factory';
import { FocusEngine } from './engines/focus.engine';
import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './engines/base-engine.interface';
import { GridInteractionEngine } from './engines/grid-interaction.engine';
import { MotionPathEngine } from './engines/motion-path.engine';
import { TextStreamEngine } from './engines/text-stream.engine';
import { TextFadeEngine } from './engines/text-fade.engine';
import { WordHighlightEngine } from './engines/word-highlight.engine';
import { VisualExpansionEngine } from './engines/visual-expansion.engine';
import { ScanFindEngine } from './engines/scan-find.engine';
import { ReadingComprehensionEngine } from './engines/reading-comprehension.engine';
import { RegressionReductionEngine } from './engines/regression-reduction.engine';
import { SubvocalizationReductionEngine } from './engines/subvocalization-reduction.engine';
import { VisualizationEngine } from './engines/visualization.engine';
import { ExerciseService } from '../../../../core/services/exercise.service';
import { ExerciseSessionService } from '../../../../core/services/exercise-session.service';
import { ExerciseProgramService, CompleteExerciseRequest } from '../../../../core/services/exercise-program.service';
import { StudentProgramService } from '../../../../core/services/student-program.service'; // INJECTED
import { StartSessionRequest, ExerciseResult as SessionResult } from '../../../../core/models/exercise-session.model';

interface ExerciseData {
  id: string;
  title: string;
  description: string;
  difficultyLevel: number;
  configurationJson: string;
  exerciseTypeName?: string;
}

interface ParsedConfig {
  engineType?: EngineType;
  engineConfig?: EngineConfig;
  [key: string]: any;
}

@Component({
  selector: 'app-exercise-player',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
  ],
  templateUrl: './exercise-player.component.html',
  styleUrls: ['./exercise-player.component.scss']
})
export class ExercisePlayerComponent implements OnInit, OnDestroy, AfterViewChecked {
  private destroy$ = new Subject<void>();

  // INJECTED SERVICES
  private readonly exerciseService = inject(ExerciseService); // Explicit injection if not already present
  private readonly exerciseProgramService = inject(ExerciseProgramService);
  private readonly studentProgramService = inject(StudentProgramService); // INJECTED
  private readonly sessionService = inject(ExerciseSessionService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  // Toast notification state
  toast = signal<{msg: string, type: 'info' | 'warn' | 'error'} | null>(null);

  showToast(msg: string, type: 'info' | 'warn' | 'error' = 'info', duration = 3000): void {
    this.toast.set({msg, type});
    setTimeout(() => this.toast.set(null), duration);
  }

  // Program Completion State
  showProgramCompletionModal = false;
  programCompletionData: any = null;
  startingNextProgram = false;

  // ViewChild for auto-focus on answer input
  @ViewChild('answerInput') answerInput!: ElementRef<HTMLInputElement>;
  private shouldFocusInput = false;

  @ViewChild('wordHighlightContainer') wordHighlightContainer?: ElementRef<HTMLDivElement>;
  @ViewChild('textFadeContainer') textFadeContainer?: ElementRef<HTMLDivElement>;
  @ViewChild('regressionContainer') regressionContainer?: ElementRef<HTMLDivElement>;
  @ViewChild('subvocDisplayArea') subvocDisplayArea?: ElementRef<HTMLDivElement>;
  private shouldScrollWord = false;
  private shouldScrollFade = false;
  private shouldScrollRegression = false;
  private shouldScrollSubvoc = false;

  @HostListener('document:mouseleave', ['$event'])
  onMouseLeave(event: MouseEvent): void {
    // Existing mouse leave logic if any
  }

  // --- Mental Registration (Focus) Helpers ---

  // Type guard for template
  get asFocusEngine(): FocusEngine | null {
    if (!this.engine) return null;
    // Check for both 'focus' and legacy 'attention_training' types
    if (this.engine.engineType === 'focus' || this.engine.engineType === 'attention_training') {
      return this.engine as FocusEngine;
    }
    return null;
  }

  getFocusModeLabel(): string {
    const mode = this.asFocusEngine?.mode;
    if (mode === 'position') return 'Konum';
    if (mode === 'word') return 'Kelime';
    if (mode === 'dual') return 'Çift Mod';
    return '';
  }

  getFocusModeClass(): string {
    const mode = this.asFocusEngine?.mode;
    if (mode === 'position') return 'mode-position';
    if (mode === 'word') return 'mode-word';
    if (mode === 'dual') return 'mode-dual';
    return '';
  }

  handleMatchClick(): void {
    if (this.engine && (this.engine.engineType === 'focus' || this.engine.engineType === 'attention_training')) {
      const focusEngine = this.engine as FocusEngine;
      // Legacy single-button behavior - route based on mode
      if (focusEngine.mode === 'position') {
        this.handlePositionMatchClick();
      } else {
        this.handleWordMatchClick();
      }
    }
  }

  handlePositionMatchClick(): void {
    if (this.engine && (this.engine.engineType === 'focus' || this.engine.engineType === 'attention_training')) {
      this.engine.handleInput({ type: 'position_match' });
    }
  }

  handleWordMatchClick(): void {
    if (this.engine && (this.engine.engineType === 'focus' || this.engine.engineType === 'attention_training')) {
      this.engine.handleInput({ type: 'word_match' });
    }
  }

  // Generate grid cells array for Focus engine (1-9 for 3x3, 1-16 for 4x4)
  getFocusGridCells(): number[] {
    const gridSize = this.asFocusEngine?.gridSize || 3;
    const cellCount = gridSize * gridSize;
    return Array.from({ length: cellCount }, (_, i) => i + 1);
  }

  @HostListener('document:keydown.space', ['$event'])
  onSpaceKey(event: Event): void {
    const kEvent = event as KeyboardEvent;
    if ((this.engine?.engineType === 'focus' || this.engine?.engineType === 'attention_training') && this.engineState.isRunning && !this.engineState.isPaused) {
      kEvent.preventDefault();
      // In single mode, Space triggers the active channel
      const focusEngine = this.engine as FocusEngine;
      if (!focusEngine.isDualMode) {
        this.handleMatchClick();
      }
    }
  }

  // Q key for Position match in dual mode
  @HostListener('document:keydown.q', ['$event'])
  onQKey(event: Event): void {
    const kEvent = event as KeyboardEvent;
    if ((this.engine?.engineType === 'focus' || this.engine?.engineType === 'attention_training') && this.engineState.isRunning && !this.engineState.isPaused) {
      const focusEngine = this.engine as FocusEngine;
      if (focusEngine.isPositionMode) {
        kEvent.preventDefault();
        this.handlePositionMatchClick();
      }
    }
  }

  // P key for Word match in dual mode
  @HostListener('document:keydown.p', ['$event'])
  onPKey(event: Event): void {
    const kEvent = event as KeyboardEvent;
    if ((this.engine?.engineType === 'focus' || this.engine?.engineType === 'attention_training') && this.engineState.isRunning && !this.engineState.isPaused) {
      const focusEngine = this.engine as FocusEngine;
      if (focusEngine.isWordMode) {
        kEvent.preventDefault();
        this.handleWordMatchClick();
      }
    }
  }
  @HostListener('window:keydown', ['$event'])
  handleKeyDown(event: KeyboardEvent): void {
    if (!this.engineState.isRunning || this.engineState.isPaused) return;

    // Visual Expansion için Enter tuşu cevap onaylar
    if (this.engine?.engineType === 'visual_expansion' && this.isExpansionWaitingInput()) {
      if (event.code === 'Enter' || event.code === 'NumpadEnter') {
        this.submitExpansionAnswer();
        event.preventDefault();
      }
    }
  }




  exercise: ExerciseData | null = null;
  parsedConfig: ParsedConfig | null = null;
  engine: BaseEngine | null = null;

  isLoading = true;
  error: string | null = null;

  engineState: EngineState = {
    isRunning: false,
    isPaused: false,
    isCompleted: false,
    currentStep: 0,
    totalSteps: 0,
    score: 0,
    accuracy: 100,
    timeElapsed: 0,
    errors: 0
  };

  result: EngineResult | null = null;

  // Grid interaction specific
  clickedCells = new Set<number>();
  correctCells = new Set<number>();
  wrongCells = new Set<number>();

  sessionId: string | null = null;
  sessionResult: SessionResult | null = null;
  showExitConfirm = false;
  isAssessmentMode = false;

  // Tachistoscope specific
  tachistoscopeAnswer = '';
  tachistoscopeFeedback: { isCorrect: boolean; correctAnswer: string } | null = null;

  // Backend session configuration (contains stimuli for Tachistoscope)
  backendSessionConfig: any = null;

  // Comprehension Questions (for Speed Reading)
  comprehensionQuestions: any[] = [];
  currentQuestionIndex = 0;
  questionAnswers: {
    questionId: string;
    selectedAnswer: string;
    isCorrect: boolean;
    timeSpent: number;
    targetTime: number;
    questionText?: string;
    correctAnswer?: string;
  }[] = [];
  readingWpm = 0;
  exercisePhase: 'reading' | 'questions' | 'completed' = 'reading';
  selectedAnswer: string | null = null;
  questionFeedback: { isCorrect: boolean; correctAnswer: string; explanation?: string; question?: any } | null = null;

  // Question Timer (for Exam Simulation)
  questionTimeRemaining = 0;
  questionTimerInterval: any = null;
  questionStartTime = 0;

  // Visual Expansion specific
  expansionAnswerLeft = '';
  expansionAnswerRight = '';
  expansionFeedback: { isCorrect: boolean; correctAnswer: string } | null = null;
  currentHintIndex: number | null = null; // For Error Analysis Hint Highlight
  @ViewChild('expansionLeftInput') expansionLeftInput?: ElementRef<HTMLInputElement>;
  @ViewChild('expansionRightInput') expansionRightInput?: ElementRef<HTMLInputElement>;

  // Peripheral Vision Input
  @ViewChild('peripheralInput') peripheralInput?: ElementRef<HTMLInputElement>;
  private lastAwaitingState = false;

  // Timer for Duration-based exercises
  private activeTimer: any = null;

  private totalDurationSeconds = 0;

  /**
   * Start the recommended next program
   */
  startNextProgram(): void {
    if (!this.programCompletionData?.recommendedNextProgram) return;

    this.startingNextProgram = true;
    const templateId = this.programCompletionData.recommendedNextProgram.templateId;

    this.studentProgramService.startProgram(templateId).subscribe({
      next: () => {
        this.showToast('Yeni programınız başarıyla başlatıldı! 🚀', 'info', 3000);
        this.router.navigate(['/student/dashboard']);
      },
      error: (err) => {
        console.error('Failed to start next program:', err);
        this.showToast('Program başlatılamadı. Lütfen daha sonra tekrar deneyin.', 'error', 3000);
        this.startingNextProgram = false;
      }
    });
  }

  /**
   * Close modal and return to dashboard
   */
  cancelProgramTransition(): void {
    this.showProgramCompletionModal = false;
    this.router.navigate(['/student/dashboard']);
  }

  constructor() { }

  assignmentId: string | null = null;

  ngOnInit(): void {
    // Scroll to top immediately and after a short delay to ensure it works
    window.scrollTo(0, 0);
    setTimeout(() => {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }, 100);

    // Check assignment ID from query params
    this.route.queryParams.subscribe(params => {
      this.assignmentId = params['assignmentId'];
    });

    // Check assessment mode from state
    const state = history.state;
    this.isAssessmentMode = state?.assessmentMode === true;

    const exerciseId = this.route.snapshot.paramMap.get('exerciseId');
    if (exerciseId) {
      this.loadExercise(exerciseId);
    } else {
      this.error = 'Egzersiz ID bulunamadı';
      this.isLoading = false;
    }
  }

  ngAfterViewChecked(): void {
    // Auto-focus answer input when it becomes visible
    if (this.shouldFocusInput && this.answerInput?.nativeElement) {
      this.answerInput.nativeElement.focus();
      this.shouldFocusInput = false;
    }

    if (this.shouldScrollWord) {
      this.handleWordHighlightScroll();
      this.shouldScrollWord = false;
    }

    if (this.shouldScrollFade) {
      this.handleTextFadeScroll();
      this.shouldScrollFade = false;
    }

    if (this.shouldScrollRegression) {
      this.handleRegressionScroll();
      this.shouldScrollRegression = false;
    }

    if (this.shouldScrollSubvoc) {
      this.handleSubvocScroll();
      this.shouldScrollSubvoc = false;
    }
  }

  private handleWordHighlightScroll(): void {
    if (!this.wordHighlightContainer?.nativeElement) return;

    const container = this.wordHighlightContainer.nativeElement;
    const highlightedElement = container.querySelector('.chunk-group.highlight') as HTMLElement;

    if (highlightedElement) {
      // Calculate scroll position to center the highlighted element within the container only
      const containerRect = container.getBoundingClientRect();
      const elementRect = highlightedElement.getBoundingClientRect();

      // Calculate the element's position relative to container's scroll position
      const elementTopRelativeToContainer = elementRect.top - containerRect.top + container.scrollTop;

      // Calculate the scroll position that would center the element
      const scrollTarget = elementTopRelativeToContainer - (container.clientHeight / 2) + (elementRect.height / 2);

      // Only scroll if element is outside the middle third of the visible area
      const visibleTop = container.scrollTop + (container.clientHeight * 0.33);
      const visibleBottom = container.scrollTop + (container.clientHeight * 0.66);

      if (elementTopRelativeToContainer < visibleTop || elementTopRelativeToContainer > visibleBottom) {
        container.scrollTo({
          top: Math.max(0, scrollTarget),
          behavior: 'smooth'
        });
      }
    }
  }


  private handleTextFadeScroll(): void {
    if (!this.textFadeContainer?.nativeElement) return;

    const container = this.textFadeContainer.nativeElement;
    const activeElement = container.querySelector('.fading-word-item.active') as HTMLElement;

    if (activeElement) {
      const containerRect = container.getBoundingClientRect();
      const elementRect = activeElement.getBoundingClientRect();

      const elementTopRelativeToContainer = elementRect.top - containerRect.top + container.scrollTop;
      const scrollTarget = elementTopRelativeToContainer - (container.clientHeight / 2) + (elementRect.height / 2);

      const visibleTop = container.scrollTop + (container.clientHeight * 0.3);
      const visibleBottom = container.scrollTop + (container.clientHeight * 0.6);

      if (elementTopRelativeToContainer < visibleTop || elementTopRelativeToContainer > visibleBottom) {
        container.scrollTo({
          top: Math.max(0, scrollTarget),
          behavior: 'smooth'
        });
      }
    }
  }

  private handleRegressionScroll(): void {
    if (!this.regressionContainer?.nativeElement) return;

    const container = this.regressionContainer.nativeElement;
    const activeElement = container.querySelector('.regression-word.active') as HTMLElement;

    if (activeElement) {
      const containerRect = container.getBoundingClientRect();
      const elementRect = activeElement.getBoundingClientRect();

      const elementTopRelativeToContainer = elementRect.top - containerRect.top + container.scrollTop;
      const scrollTarget = elementTopRelativeToContainer - (container.clientHeight / 2) + (elementRect.height / 2);

      // Sadece kelime orta alanın (üst/alt %30) dışındaysa kaydır
      const visibleTop = container.scrollTop + (container.clientHeight * 0.3);
      const visibleBottom = container.scrollTop + (container.clientHeight * 0.7);

      if (elementTopRelativeToContainer < visibleTop || elementTopRelativeToContainer > visibleBottom) {
        container.scrollTo({
          top: Math.max(0, scrollTarget),
          behavior: 'smooth'
        });
      }
    }
  }

  private handleSubvocScroll(): void {
    if (!this.subvocDisplayArea?.nativeElement) return;

    // Only scroll in highlight mode
    if (this.getSubvocDisplayMode() !== 'highlight') return;

    const container = this.subvocDisplayArea.nativeElement;
    const activeElement = container.querySelector('.subvoc-word.active') as HTMLElement;

    if (activeElement) {
      const containerRect = container.getBoundingClientRect();
      const elementRect = activeElement.getBoundingClientRect();

      const elementTopRelativeToContainer = elementRect.top - containerRect.top + container.scrollTop;
      const scrollTarget = elementTopRelativeToContainer - (container.clientHeight / 2) + (elementRect.height / 2);

      const visibleTop = container.scrollTop + (container.clientHeight * 0.25);
      const visibleBottom = container.scrollTop + (container.clientHeight * 0.75);

      if (elementTopRelativeToContainer < visibleTop || elementTopRelativeToContainer > visibleBottom) {
        container.scrollTo({
          top: Math.max(0, scrollTarget),
          behavior: 'smooth'
        });
      }
    }
  }

  ngOnDestroy(): void {
    this.stopTimer();
    this.destroy$.next();
    this.destroy$.complete();
    this.engine?.destroy();
  }

  private loadExercise(id: string): void {
    this.exerciseService.getExerciseById(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (exercise: any) => {
          this.exercise = exercise;
          this.parseConfiguration();
          this.startSession();
        },
        error: (err) => {
          this.error = 'Egzersiz yüklenirken hata oluştu';
          this.isLoading = false;
          console.error('[ExercisePlayer] Load error:', err);
        }
      });
  }

  private startSession(): void {
    if (!this.exercise) return;

    // For Teachers in preview mode, skip session creation entirely
    if (this.authService.hasRole('Teacher')) {
      console.log('Teacher preview mode - skipping session creation');
      this.sessionId = 'preview-mode'; // Placeholder ID
      this.backendSessionConfig = {};
      this.initializeEngine();
      this.isLoading = false;
      return;
    }

    const request: StartSessionRequest = {
      exerciseId: this.exercise.id,
      readingTextId: this.parsedConfig?.['metadata']?.targetReadingTextId || this.parsedConfig?.['readingTextId'],
      studentAssignmentId: this.assignmentId || undefined
    };

    this.sessionService.startSession(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.sessionId = response.sessionId;

          // Store backend session config (contains stimuli, questions, etc. from Backend Engine)
          // For ExamSimulation, Backend already puts sessionData (with Questions) into Configuration
          if (response.configuration) {
            this.backendSessionConfig = response.configuration;
          } else {
            this.backendSessionConfig = {};
          }

          // Extract comprehension/exam questions for question phase
          // Backend sends PascalCase (Questions), also check camelCase for safety
          const questions = this.backendSessionConfig.Questions ||
            this.backendSessionConfig.questions ||
            (this.backendSessionConfig.Content && this.backendSessionConfig.Content.Questions) ||
            (this.backendSessionConfig.content && this.backendSessionConfig.content.questions);
          if (questions && Array.isArray(questions)) {
            this.comprehensionQuestions = questions;
          }

          this.initializeEngine();
          this.isLoading = false;
        },
        error: (err) => {
          console.error('Session start failed:', err);
          this.error = 'Oturum başlatılamadı. Lütfen internet bağlantınızı kontrol edin.';
          this.isLoading = false;
        }
      });
  }

  private parseConfiguration(): void {
    if (!this.exercise?.configurationJson) {
      this.parsedConfig = {};
      return;
    }

    try {
      this.parsedConfig = JSON.parse(this.exercise.configurationJson);
    } catch (e) {
      console.error('[ExercisePlayer] Config parse error:', e);
      this.parsedConfig = {};
    }
  }

  private initializeEngine(): void {
    // 1. Determine Engine Type (Backend Session Config wins over static Exercise Config)
    let engineType = this.parsedConfig?.engineType as EngineType;

    // Check various paths for dynamic engine type from backend
    const sessionEngineType =
      this.backendSessionConfig?.engineType ||
      this.backendSessionConfig?.EngineConfig?.engineType ||
      this.backendSessionConfig?.engineConfig?.engineType;

    if (sessionEngineType) {
      engineType = sessionEngineType as EngineType;
    }

    if (!engineType) {
      console.warn('[ExercisePlayer] No engineType in config');
      return;
    }

    if (!EngineFactory.isSupported(engineType)) {
      console.warn('[ExercisePlayer] Unsupported engine:', engineType);
      return;
    }

    this.engine = EngineFactory.create(engineType);

    if (this.engine) {
      const callbacks: EngineCallbacks = {
        onStart: () => {
          this.startTimer();
        },
        onPause: () => {
          this.stopTimer();
        },
        onResume: () => {
          this.startTimer();
        },
        onComplete: (result) => {
          this.stopTimer();

          // For word_highlight or reading_comprehension with questions, go to question phase
          const hasQuestions = this.comprehensionQuestions.length > 0;
          const isReadingEngine =
            this.engine?.engineType === 'word_highlight' ||
            this.engine?.engineType === 'reading_comprehension' ||
            this.engine?.engineType === 'exam_simulation' ||
            this.engine?.engineType === 'text_fade' ||
            this.engine?.engineType === 'text_stream'; // text_stream (RSVP) added

          // Regression Reduction engine handles its own question flow internally
          // When it calls onComplete, it means everything (reading + questions) is done
          if (this.engine?.engineType === 'regression_reduction') {
            this.result = result;
            this.exercisePhase = 'completed';
            this.saveResult(result);
            this.cdr.detectChanges();
            return;
          }

          if (isReadingEngine && hasQuestions) {
            // Calculate WPM from reading time if not already provided
            const wordCount = (this.engine as any).getWords?.().length ||
              this.backendSessionConfig?.content?.wordCount ||
              this.backendSessionConfig?.wordCount || 100;

            const readingTimeMinutes = (result.totalTime / 1000) / 60;
            // If engine calculated WPM (like RSVP), use it, otherwise calc
            this.readingWpm = (this.engine as any).getCurrentWPM?.() ||
              (readingTimeMinutes > 0 ? Math.round(wordCount / readingTimeMinutes) : 0);

            this.exercisePhase = 'questions';
            this.currentQuestionIndex = 0;
            this.questionAnswers = [];
            this.selectedAnswer = null;
            this.questionFeedback = null;

            // If it's the specific Regression engine, it might already be in answering phase
            if (this.engine?.engineType === 'regression_reduction') {
              this.comprehensionQuestions = (this.engine as RegressionReductionEngine).getQuestions();
            }

            // Start question timer for first question
            this.startQuestionTimer();

            this.cdr.detectChanges();
            return;
          }

          this.result = result;
          this.exercisePhase = 'completed';
          this.saveResult(result);
          this.cdr.detectChanges();
        },
        onError: (error) => {
          this.error = error;
          this.stopTimer(); // Stop timer on error
          this.cdr.detectChanges();
        },
        onStateChange: (state) => {
          // Preserve remainingSeconds if set by our timer logic (engine might overwrite/reset state)
          if (this.engineState.remainingSeconds !== undefined && state.remainingSeconds === undefined) {
            state.remainingSeconds = this.engineState.remainingSeconds;
          }
          this.engineState = state;

          // Focus Engine Animation Triggers
          if (this.engine?.engineType === 'focus') {
            const focusEng = this.engine as any; // Cast as any or FocusEngine
            // Hits
            if (focusEng.hits > (this.prevHits || 0)) {
              this.hitsAnim = true;
              setTimeout(() => this.hitsAnim = false, 500);
              this.prevHits = focusEng.hits;
            }
            // Misses
            if (focusEng.misses > (this.prevMisses || 0)) {
              this.missesAnim = true;
              setTimeout(() => this.missesAnim = false, 500);
              this.prevMisses = focusEng.misses;
            }
            // False Alarms (treated as separate bad stat)
            if (focusEng.falseAlarms > (this.prevFalseAlarms || 0)) {
              this.falseAlarmsAnim = true;
              setTimeout(() => this.falseAlarmsAnim = false, 500);
              this.prevFalseAlarms = focusEng.falseAlarms;
            }
          }

          if (this.engine?.engineType === 'word_highlight') {
            this.shouldScrollWord = true;
          }
          if (this.engine?.engineType === 'text_fade') {
            this.shouldScrollFade = true;
          }
          if (this.engine?.engineType === 'regression_reduction') {
            this.shouldScrollRegression = true;
          }
          if (this.engine?.engineType === 'subvocalization_reduction') {
            this.shouldScrollSubvoc = true;
          }
          this.cdr.detectChanges();
        },
        onStepComplete: (step, correct) => {
        },
        onAction: (action) => {
          // Backend motoruna aksiyonu bildir

          // Pasif gözlem/okuma bazlı egzersizlerde her adımda validation yapma
          // Bu egzersizler completion'da topluca değerlendirilir (rate limit + performans)
          const engineType = this.parsedConfig?.engineType || this.engine?.engineType;
          const engineMode = this.parsedConfig?.engineConfig?.['mode'];

          // Validation GEREKEN egzersizler (kullanıcı aktif input yapıyor)
          const requiresValidation = [
            'schulte_grid',      // Tıklama sırası doğrulanmalı
            'memory_grid',       // Hafıza testi, seçimler doğrulanmalı
          ];

          // Validation GEREKMİYEN egzersizler (pasif gözlem/okuma)
          const skipValidation = [
            'motion_path',             // Saccade/Fixation - gözlem bazlı
            'visual_expansion',        // Görsel genişleme - gözlem bazlı
            'text_fade',               // Metin solma - pasif okuma
            'regression_reduction',    // Regresyon - pasif okuma
            'subvocalization_reduction', // Alt ses - pasif okuma
            'chunking',                // Gruplama - pasif okuma
            'rsvp',                    // RSVP - pasif okuma
            'speed_reading',           // Hızlı okuma - pasif okuma
            'free_reading',            // Serbest okuma - pasif okuma
          ];

          // Focus engine için özel mantık: sadece match aksiyonlarını backend'e gönder
          if (engineType === 'focus' || engineType === 'attention_training') {
            const validFocusActions = ['position_match', 'word_match', 'match_attempt', 'complete'];
            if (!validFocusActions.includes(action.action)) {
              // step_change, feedback gibi internal aksiyonları atla
              return;
            }
          }

          if (skipValidation.includes(engineType || '') || skipValidation.includes(engineMode || '')) {
            // Skip individual validation for passive exercises
            return;
          }

          // Sadece kullanıcı input gerektiren egzersizlerde validation yap
          this.sessionService.validateAction(this.sessionId!, action).subscribe({
            next: (res: any) => {
            },
            error: (err: any) => console.error('[ExercisePlayer] Action validation error:', err)
          });
        }
      };

      // Engine config'i hazırla - backend session data ile birleştir
      const engineConfig = {
        ...(this.parsedConfig?.engineConfig || {}),
        ...(this.backendSessionConfig || {}),
        ...(this.backendSessionConfig?.EngineConfig || {}),
        ...(this.backendSessionConfig?.engineConfig || {}),
        // Extract gridSize from legacy root property or new unified engineConfig.grid.rows
        gridSize: this.parsedConfig?.['gridSize'] ||
          this.parsedConfig?.['engineConfig']?.['grid']?.['rows'] ||
          this.backendSessionConfig?.['gridSize'] ||
          this.backendSessionConfig?.['engineConfig']?.['grid']?.['rows'] || 5,
        sequenceType: 'numeric',
        exerciseTypeName: this.exercise?.exerciseTypeName,
        // Metadata ve yaş grubu/zorluk bilgisini ekle
        metadata: this.parsedConfig?.['metadata'],
        difficultyLevel: this.parsedConfig?.['difficultyLevel'] || this.backendSessionConfig?.DifficultyLevel
      };

      this.engine.initialize(engineConfig, callbacks);

      // --- Timer Initialization for RSVP / Duration Based Mode ---
      // If the exercise has a defined duration (implied by word count * interval for RSVP), setup the timer
      if (engineConfig.words && Array.isArray(engineConfig.words) && engineConfig.words.length > 0 && engineConfig.intervalMs) {
        const wordCount = engineConfig.words.length;
        const interval = engineConfig.intervalMs;
        // Total duration in seconds (Rounded up)
        this.totalDurationSeconds = Math.ceil((wordCount * interval) / 1000);
        this.engineState.remainingSeconds = this.totalDurationSeconds;
      }
    }

  }

  startExercise(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
    this.clickedCells.clear();
    this.correctCells.clear();
    this.wrongCells.clear();

    // Exam Simulation: Skip reading phase, go directly to questions phase
    // Questions include their own paragraph content
    if (this.engine?.engineType === 'exam_simulation' && this.comprehensionQuestions.length > 0) {
      this.exercisePhase = 'questions';
      this.currentQuestionIndex = 0;
      this.questionAnswers = [];
      this.selectedAnswer = null;
      this.questionFeedback = null;
      this.engineState.isRunning = true;
      this.startQuestionTimer();
      this.cdr.detectChanges();
      return;
    }

    this.engine?.start();

    // Calculate line breaks for Subvocalization Reduction to prevent cross-line chunks
    if (this.engine?.engineType === 'subvocalization_reduction') {
      setTimeout(() => this.calculateSubvocLineBreaks(), 200);
    }
  }

  @HostListener('window:resize')
  onResize(): void {
    if (this.engine?.engineType === 'subvocalization_reduction') {
      this.calculateSubvocLineBreaks();
    }
  }

  private calculateSubvocLineBreaks(): void {
    if (!this.subvocDisplayArea?.nativeElement || !this.engine || this.engine.engineType !== 'subvocalization_reduction') return;

    const runCalculation = () => {
      if (!this.subvocDisplayArea?.nativeElement || !this.engine) return;

      const wordElements = this.subvocDisplayArea.nativeElement.querySelectorAll('.subvoc-word');
      if (wordElements.length === 0) return;

      const lineBreaks: number[] = [];

      // Compare each word with the previous one
      for (let i = 1; i < wordElements.length; i++) {
        const prevTop = (wordElements[i - 1] as HTMLElement).offsetTop;
        const currentTop = (wordElements[i] as HTMLElement).offsetTop;

        // If current word is significantly lower than previous word, it's a new line
        // Tolerance 5px (line height is usually > 20px)
        if (currentTop > prevTop + 5) {
          lineBreaks.push(i);
        }
      }

      if (lineBreaks.length > 0) {
        this.engine.handleInput({ type: 'line_breaks', indices: lineBreaks });
      }
    };

    // Run immediately and check a few times to ensure layout stability
    runCalculation();
    setTimeout(runCalculation, 200);
    setTimeout(runCalculation, 1000);
  }

  private startTimer(): void {
    if (this.activeTimer) return;

    // Sadece remainingSeconds initialize edilmişse timer başlat
    if (this.engineState.remainingSeconds === undefined || this.engineState.remainingSeconds <= 0) return;

    this.activeTimer = setInterval(() => {
      if (this.engineState.remainingSeconds !== undefined && this.engineState.remainingSeconds > 0) {
        this.engineState.remainingSeconds--;

        // Son 10 saniye flag'i için bir değişken kullanılabilir veya template'de check edilebilir
        // this.engineState.isLastTenSeconds = this.engineState.remainingSeconds <= 10;
      } else {
        this.stopTimer();
        // Süre bitti, engine'i usulüne uygun bitir
        if (this.engine && typeof (this.engine as any).finish === 'function') {
          (this.engine as any).finish();
        } else if (this.engine) {
          // Eğer finish metodu yoksa sadece durdur (fallback)
          console.warn('[ExercisePlayer] Time limit reached but engine has no finish() method. Stopping.');
          this.engine.stop();
        }
      }
      this.cdr.detectChanges();
    }, 1000);
  }

  private stopTimer(): void {
    if (this.activeTimer) {
      clearInterval(this.activeTimer);
      this.activeTimer = null;
    }
  }

  togglePause(): void {
    if (this.engineState.isPaused) {
      this.engine?.resume();
    } else {
      this.engine?.pause();
    }
  }

  resetExercise(): void {
    // 1. First destroy engine to prevent any side effects
    this.engine?.destroy();
    this.engine = null;

    // 2. Show loading immediately
    this.isLoading = true;
    window.scrollTo({ top: 0, behavior: 'smooth' });

    // 3. Clear game state
    this.clickedCells.clear();
    this.correctCells.clear();
    this.wrongCells.clear();
    this.result = null;
    this.sessionResult = null;
    this.exercisePhase = 'reading';
    this.currentQuestionIndex = 0;
    this.readingWpm = 0;
    this.questionAnswers = [];
    this.selectedAnswer = null;
    this.questionFeedback = null;
    this.tachistoscopeAnswer = '';
    this.tachistoscopeFeedback = null;
    this.expansionAnswerLeft = '';
    this.expansionAnswerRight = '';
    this.expansionFeedback = null;
    this.sessionId = null;
    this.backendSessionConfig = null;

    // Reset engine state
    this.engineState = {
      isRunning: false,
      isPaused: false,
      isCompleted: false,
      currentStep: 0,
      totalSteps: 0,
      score: 0,
      accuracy: 0,
      timeElapsed: 0,
      errors: 0,
      targetCount: 0,
      remainingSeconds: undefined,
      isLastTenSeconds: false
    };

    this.cdr.detectChanges();

    // 4. Start a new session
    this.startSession();
  }

  goBack(): void {
    // Eğer egzersiz çalışıyorsa, onay iste
    if (this.engineState.isRunning && !this.engineState.isCompleted) {
      this.engine?.pause();
      this.showExitConfirm = true;
    } else {
      this.navigateBack();
    }
  }

  cancelExit(): void {
    this.showExitConfirm = false;
    this.engine?.resume();
  }

  confirmExit(): void {
    this.showExitConfirm = false;
    this.engine?.stop();
    this.navigateBack();
  }

  /**
   * Doğru sayfaya geri dön - günlük egzersizlerden geldiyse oraya dön
   */
  private navigateBack(): void {
    const state = history.state;

    // 1. Assessment Mode
    if (this.isAssessmentMode) {
      this.router.navigate(['/student/assessment']);
      return;
    }

    // 1.5 Assignment Mode
    if (this.assignmentId) {
      this.router.navigate(['/student/assignments']);
      return;
    }

    // 2. Daily Exercises
    if (state?.fromDailyExercises) {
      this.router.navigate(['/student/daily-exercises']);
      return;
    }

    // 3. Default (Practice/Admin/Direct)
    this.router.navigate(['/student/exercises']);
  }

  // Comprehension Question Methods
  getCurrentQuestion(): any {
    if (this.currentQuestionIndex < this.comprehensionQuestions.length) {
      return this.comprehensionQuestions[this.currentQuestionIndex];
    }
    return null;
  }

  // Helper to get question text - handles PascalCase from C#
  getQuestionText(): string {
    const q = this.getCurrentQuestion();
    if (!q) return '';
    return q.QuestionStem || q.questionStem || q.QuestionText || q.questionText || q.Question || q.question || '';
  }

  // Helper to get paragraph content for exam simulation questions
  getQuestionContent(): string {
    const q = this.getCurrentQuestion();
    if (!q) return '';
    return q.Content || q.content || q.Paragraph || q.paragraph || q.Text || q.text || '';
  }

  // Helper to get target WPM from config
  getTargetWpm(): number {
    // Check config for target WPM
    const config = this.backendSessionConfig;
    if (config) {
      // engineConfig.timing.wpm format
      if (config.timing?.wpm) return config.timing.wpm;
      if (config.wpm) return config.wpm;
      if (config.targetWpm) return config.targetWpm;
    }
    // Check exercise config
    const exerciseConfig = this.exercise?.configurationJson;
    if (exerciseConfig) {
      try {
        const parsed = typeof exerciseConfig === 'string' ? JSON.parse(exerciseConfig) : exerciseConfig;
        if (parsed.engineConfig?.timing?.wpm) return parsed.engineConfig.timing.wpm;
        if (parsed.timing?.wpm) return parsed.timing.wpm;
        if (parsed.wpm) return parsed.wpm;
      } catch { }
    }
    return 200; // Default
  }

  getOptionText(option: string): string {
    const question = this.getCurrentQuestion();
    if (!question) return '';

    // Check for array based options (ExamSimulation DTO style)
    const optionsArray = question.Options || question.options;
    if (Array.isArray(optionsArray)) {
      const index = option.charCodeAt(0) - 65; // 'A' is 65
      return optionsArray[index] || '';
    }

    // C# sends PascalCase (OptionA), check both cases
    switch (option) {
      case 'A': return question.OptionA || question.optionA || '';
      case 'B': return question.OptionB || question.optionB || '';
      case 'C': return question.OptionC || question.optionC || '';
      case 'D': return question.OptionD || question.optionD || '';
      case 'E': return question.OptionE || question.optionE || '';
      default: return '';
    }
  }

  selectAnswer(option: string): void {
    if (this.questionFeedback) return; // Already answered

    this.stopQuestionTimer();
    this.selectedAnswer = option;
    const question = this.getCurrentQuestion();

    if (question) {
      // C# sends PascalCase (CorrectAnswer or CorrectOption), check both
      let correctAnswer = question.CorrectAnswer || question.correctAnswer;

      // If correctAnswer is missing, check CorrectOption
      if (!correctAnswer && (question.CorrectOption !== undefined || question.correctOption !== undefined)) {
        const correctOpt = question.CorrectOption ?? question.correctOption;
        // Backend may send as string ('A', 'B', 'C', 'D') or as number (1-based index)
        if (typeof correctOpt === 'string') {
          correctAnswer = correctOpt.toUpperCase();
        } else if (typeof correctOpt === 'number') {
          correctAnswer = String.fromCharCode(64 + correctOpt); // 1 -> 'A', 2 -> 'B'
        }
      }

      const isCorrect = option === correctAnswer;

      this.questionFeedback = {
        isCorrect,
        correctAnswer: correctAnswer,
        explanation: question.Explanation || question.explanation
      };

      // Calculate time spent on this question
      const targetTime = question.TargetTimeSeconds || question.targetTimeSeconds || 60;
      const timeSpent = targetTime - this.questionTimeRemaining;

      this.questionAnswers.push({
        questionId: question.QuestionId || question.questionId,
        selectedAnswer: option,
        isCorrect,
        timeSpent,
        targetTime,
        questionText: question.QuestionText || question.questionText || question.Text || question.text,
        correctAnswer: correctAnswer
      });

      this.cdr.detectChanges();
    }
  }

  nextQuestion(): void {
    this.stopQuestionTimer();
    if (this.isLastQuestion()) {
      this.finishQuestionPhase();
    } else {
      this.currentQuestionIndex++;
      this.selectedAnswer = null;
      this.questionFeedback = null;
      this.startQuestionTimer();
      this.cdr.detectChanges();
    }
  }

  // Question Timer Methods
  startQuestionTimer(): void {
    this.stopQuestionTimer();
    const question = this.getCurrentQuestion();
    if (!question) return;

    // Get target time from question (Backend calculates this based on word count)
    const targetTime = question.TargetTimeSeconds || question.targetTimeSeconds || 60;
    this.questionTimeRemaining = targetTime;
    this.questionStartTime = Date.now();

    this.questionTimerInterval = setInterval(() => {
      if (this.questionFeedback) {
        // Already answered, stop timer
        this.stopQuestionTimer();
        return;
      }

      const elapsed = Math.floor((Date.now() - this.questionStartTime) / 1000);
      this.questionTimeRemaining = Math.max(0, targetTime - elapsed);

      if (this.questionTimeRemaining <= 0) {
        // Time's up! Auto-submit as wrong
        this.handleQuestionTimeout();
      }

      this.cdr.detectChanges();
    }, 1000);
  }

  stopQuestionTimer(): void {
    if (this.questionTimerInterval) {
      clearInterval(this.questionTimerInterval);
      this.questionTimerInterval = null;
    }
  }

  handleQuestionTimeout(): void {
    this.stopQuestionTimer();
    const question = this.getCurrentQuestion();
    if (!question || this.questionFeedback) return;

    // Get correct answer
    let correctAnswer = question.CorrectAnswer || question.correctAnswer;
    if (!correctAnswer && (question.CorrectOption !== undefined || question.correctOption !== undefined)) {
      const correctOpt = question.CorrectOption ?? question.correctOption;
      if (typeof correctOpt === 'string') {
        correctAnswer = correctOpt.toUpperCase();
      } else if (typeof correctOpt === 'number') {
        correctAnswer = String.fromCharCode(64 + correctOpt);
      }
    }

    // Mark as wrong (no answer given)
    this.selectedAnswer = null;
    this.questionFeedback = {
      isCorrect: false,
      correctAnswer: correctAnswer || '',
      explanation: 'Süre doldu!'
    };

    // Time spent equals target time (ran out of time)
    const targetTime = question.TargetTimeSeconds || question.targetTimeSeconds || 60;

    this.questionAnswers.push({
      questionId: question.QuestionId || question.questionId,
      selectedAnswer: '',
      isCorrect: false,
      timeSpent: targetTime,
      targetTime,
      questionText: question.QuestionText || question.questionText || question.Text || question.text,
      correctAnswer: correctAnswer || ''
    });

    this.cdr.detectChanges();
  }

  isQuestionTimeUrgent(): boolean {
    return this.questionTimeRemaining > 0 && this.questionTimeRemaining <= 10;
  }

  isQuestionTimeCritical(): boolean {
    return this.questionTimeRemaining > 0 && this.questionTimeRemaining <= 5;
  }


  isLastQuestion(): boolean {
    return this.currentQuestionIndex >= this.comprehensionQuestions.length - 1;
  }

  private finishQuestionPhase(): void {
    this.stopQuestionTimer();
    const correctCount = this.questionAnswers.filter(a => a.isCorrect).length;
    const totalQuestions = this.comprehensionQuestions.length;
    const comprehensionAccuracy = totalQuestions > 0 ? Math.round((correctCount / totalQuestions) * 100) : 0;

    // Calculate detailed statistics
    const totalTimeSpent = this.questionAnswers.reduce((sum, a) => sum + a.timeSpent, 0);
    const averageTimePerQuestion = totalQuestions > 0 ? Math.round(totalTimeSpent / totalQuestions) : 0;
    const timeoutCount = this.questionAnswers.filter(a => a.selectedAnswer === '').length;
    const totalTargetTime = this.questionAnswers.reduce((sum, a) => sum + a.targetTime, 0);

    // Create final result combining reading speed and comprehension
    this.result = {
      score: comprehensionAccuracy,
      accuracy: comprehensionAccuracy,
      totalTime: this.engineState.timeElapsed,
      totalSteps: totalQuestions,
      completedSteps: totalQuestions,
      errors: totalQuestions - correctCount,
      details: {
        wpm: this.readingWpm,
        targetWpm: this.getTargetWpm(),
        comprehensionScore: comprehensionAccuracy,
        correctAnswers: correctCount,
        totalQuestions: totalQuestions,
        performanceLevel: this.getPerformanceLevel(this.readingWpm, comprehensionAccuracy),
        // Time statistics
        totalTimeSpent,
        averageTimePerQuestion,
        totalTargetTime,
        timeoutCount,
        // Detailed answers with timing
        answers: this.questionAnswers
      }
    };

    this.exercisePhase = 'completed';
    this.engineState.isCompleted = true;
    this.saveResult(this.result);
    this.cdr.detectChanges();

  }

  private getPerformanceLevel(wpm: number, comprehension: number): string {
    if (comprehension >= 80 && wpm >= 300) return 'Mükemmel! 🏆';
    if (comprehension >= 70 && wpm >= 250) return 'Çok İyi! ⭐';
    if (comprehension >= 60 && wpm >= 200) return 'İyi! 👍';
    if (comprehension >= 50) return 'Gelişiyor 📈';
    return 'Pratik Gerekli 💪';
  }

  // Get correct answer for a specific question by index
  getCorrectAnswerForIndex(index: number): string {
    if (index < 0 || index >= this.comprehensionQuestions.length) return '';
    const question = this.comprehensionQuestions[index];
    if (!question) return '';

    // Check CorrectAnswer first
    let correctAnswer = question.CorrectAnswer || question.correctAnswer;

    // If not found, check CorrectOption
    if (!correctAnswer && (question.CorrectOption !== undefined || question.correctOption !== undefined)) {
      const correctOpt = question.CorrectOption ?? question.correctOption;
      if (typeof correctOpt === 'string') {
        correctAnswer = correctOpt.toUpperCase();
      } else if (typeof correctOpt === 'number') {
        correctAnswer = String.fromCharCode(64 + correctOpt);
      }
    }

    return correctAnswer || '';
  }


  // Grid interaction handlers
  onCellClick(index: number, value: string | number): void {
    if (this.correctCells.has(index)) return;

    this.clickedCells.add(index);

    const target = this.getCurrentTarget();
    if (value === target) {
      this.correctCells.add(index);
    } else {
      this.wrongCells.add(index);
      setTimeout(() => this.wrongCells.delete(index), 300);
    }

    this.engine?.handleInput({ cellIndex: index, value });
  }

  // Helper methods
  getGrid(): (string | number)[] {
    return (this.engine as GridInteractionEngine)?.getGrid?.() || [];
  }

  getCurrentTarget(): string | number {
    return (this.engine as GridInteractionEngine)?.getCurrentTarget?.() || 1;
  }

  getGridSize(): number {
    return (this.engine as GridInteractionEngine)?.getGridSize?.() || 5;
  }

  getProgressPercent(): number {
    if (this.engineState.totalSteps === 0) return 0;
    return (this.engineState.currentStep / this.engineState.totalSteps) * 100;
  }

  trackByIdx(index: number, item: any): any {
    return index;
  }

  formatTime(ms: number | undefined | null): string {
    if (ms === undefined || ms === null || isNaN(ms)) return '00:00';
    const seconds = Math.max(0, Math.floor(ms / 1000));
    const minutes = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${minutes.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  }

  formatTimeFromSeconds(seconds: number | undefined | null): string {
    if (seconds === undefined || seconds === null || isNaN(seconds)) return '0:00';
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  }

  getRemainingTimeMs(): number {
    if (this.engineState.remainingSeconds !== undefined) {
      return this.engineState.remainingSeconds * 1000;
    }
    const config = (this.parsedConfig?.engineConfig as any) || (this.parsedConfig as any) || {};
    const backendConfig = this.backendSessionConfig || {};

    // 1. Check direct MS limit (PascalCase and camelCase)
    const maxReadingTimeMs = backendConfig.timing?.maxReadingTimeMs ||
      config.timing?.maxReadingTimeMs ||
      backendConfig.MaxReadingTimeMs ||
      config.MaxReadingTimeMs;

    if (maxReadingTimeMs) {
      return Math.max(0, maxReadingTimeMs - this.engineState.timeElapsed);
    }

    // 2. Check legacy Seconds limit
    const timeLimitSec =
      backendConfig.timeLimitSeconds ||
      backendConfig.TimeLimitSeconds ||
      config.timeLimitSeconds ||
      config.timeLimit ||
      config.TimeLimit ||
      config.timing?.timeLimitSec ||
      config.rules?.timeLimit ||
      backendConfig.timing?.maxReadingTimeMs || // Double check backend MS here too if needed but handled above
      config.timing?.maxReadingTimeMs ||
      backendConfig.MaxReadingTimeMs ||
      config.MaxReadingTimeMs;

    if (timeLimitSec && timeLimitSec < 10000) { // Assume if < 10000 it is seconds
      return Math.max(0, (timeLimitSec * 1000) - this.engineState.timeElapsed);
    }

    return 0;
  }

  getCurrentTextStreamWpm(): number {
    if (this.engine?.engineType !== 'text_stream') return 0;
    // Cast to any to access specific method
    const duration = (this.engine as any).getCurrentDuration?.();
    if (duration > 0) {
      return Math.round(60000 / duration);
    }
    return 0;
  }

  // --- Regression Reduction Helpers ---
  getRegressionWords(): string[] {
    return (this.engine as RegressionReductionEngine)?.getWords?.() || [];
  }

  getRegressionActiveIndex(): number {
    return (this.engine as RegressionReductionEngine)?.getCurrentWordIndex?.() ?? -1;
  }

  getRegressionChunkSize(): number {
    // Backend config'den chunkSize al
    const engineConfig = this.backendSessionConfig?.EngineConfig || this.backendSessionConfig?.engineConfig || {};
    return engineConfig.chunkSize || 1;
  }

  getRegressionWpm(): number {
    const engineConfig = this.backendSessionConfig?.EngineConfig || this.backendSessionConfig?.engineConfig || {};

    // If wordDelayMs is present, it's the source of truth (likely adjusted by masking)
    if (engineConfig.wordDelayMs && engineConfig.wordDelayMs > 0) {
      return Math.round(60000 / engineConfig.wordDelayMs);
    }

    return engineConfig.wpm || 0;
  }

  isWordInActiveChunk(wordIndex: number): boolean {
    const activeIndex = this.getRegressionActiveIndex();
    const chunkSize = this.getRegressionChunkSize();

    // Aktif index boşsa işlem yapma
    if (activeIndex < 0) return false;

    // Chunk size 1 ise sadece o kelime
    if (chunkSize <= 1) return wordIndex === activeIndex;

    // Chunk size > 1 ise aralık kontrolü
    // Engine currentWordIndex'i chunk'ın son kelimesi olarak güncelliyor
    const startChunkIndex = Math.max(0, activeIndex - chunkSize + 1);
    return wordIndex >= startChunkIndex && wordIndex <= activeIndex;
  }

  isWordInTrailingMask(wordIndex: number): boolean {
    const activeIndex = this.getRegressionActiveIndex();
    const chunkSize = this.getRegressionChunkSize();

    if (activeIndex < 0) return false;

    // Chunk size 1 ise aktif indexten öncekiler
    if (chunkSize <= 1) return wordIndex < activeIndex;

    // Chunk size > 1 ise chunk başlangıcından öncekiler maskelenir
    const startChunkIndex = Math.max(0, activeIndex - chunkSize + 1);
    return wordIndex < startChunkIndex;
  }

  getRegressionPhase(): string {
    return (this.engine as RegressionReductionEngine)?.getPhase?.() || 'reading';
  }

  getRegressionMaskingType(): string {
    return (this.engine as RegressionReductionEngine)?.getMaskingType?.() || 'none';
  }

  onRegressionWordClick(index: number): void {
    const activeIndex = this.getRegressionActiveIndex();
    if (index < activeIndex) {
      // User clicked a previous word -> Regression detected!
      this.engine?.handleInput({ type: 'regression', wordIndex: index });
      this.showToast('Geri dönüş (Regresyon) tespit edildi!', 'warn', 1000);
    }
  }

  submitRegressionAnswer(option: string): void {
    const question = this.getRegressionCurrentQuestion();
    const correctAnswer = question?.CorrectAnswer || question?.correctAnswer;

    // Feedback göster - soruyu da sakla çünkü engine index'i artıracak
    this.questionFeedback = {
      isCorrect: option === correctAnswer,
      correctAnswer: correctAnswer,
      explanation: question?.Explanation || question?.explanation,
      // Mevcut soruyu sakla
      question: question
    };

    this.selectedAnswer = option;

    // Engine'e cevabı gönder (bu index'i artıracak)
    this.engine?.handleInput({ type: 'answer', answer: option });

    this.cdr.detectChanges();
  }

  // Sonraki soruya geç butonu için
  goToNextRegressionQuestion(): void {
    this.selectedAnswer = null;
    this.questionFeedback = null;
    this.cdr.detectChanges();
  }

  getRegressionCurrentQuestion(): any {
    return (this.engine as RegressionReductionEngine)?.getCurrentQuestion?.();
  }

  getRegressionCurrentQuestionIndex(): number {
    return (this.engine as RegressionReductionEngine)?.getCurrentQuestionIndex?.() || 0;
  }

  getRegressionQuestionCount(): number {
    return (this.engine as RegressionReductionEngine)?.getQuestions?.()?.length || 0;
  }

  getRegressionLastAnswer(): any {
    return (this.engine as RegressionReductionEngine)?.getLastAnswer?.();
  }

  getRegressionAnswers(): any[] {
    return (this.engine as RegressionReductionEngine)?.getAnswers?.() || [];
  }

  // ==========================================
  // Subvocalization Reduction Helpers
  // ==========================================

  getSubvocWords(): string[] {
    return (this.engine as SubvocalizationReductionEngine)?.getWords?.() || [];
  }

  getSubvocCurrentWordIndex(): number {
    return (this.engine as SubvocalizationReductionEngine)?.getCurrentWordIndex?.() ?? -1;
  }

  getSubvocCurrentChunk(): string {
    return (this.engine as SubvocalizationReductionEngine)?.getCurrentChunk?.() || '';
  }

  getSubvocPhase(): string {
    return (this.engine as SubvocalizationReductionEngine)?.getPhase?.() || 'reading';
  }

  getSubvocCurrentQuestion(): any {
    return (this.engine as SubvocalizationReductionEngine)?.getCurrentQuestion?.();
  }

  getSubvocCurrentQuestionIndex(): number {
    return (this.engine as SubvocalizationReductionEngine)?.getCurrentQuestionIndex?.() || 0;
  }

  getSubvocQuestionCount(): number {
    return (this.engine as SubvocalizationReductionEngine)?.getQuestionCount?.() || 0;
  }

  getSubvocDisplayMode(): string {
    return (this.engine as SubvocalizationReductionEngine)?.getDisplayMode?.() || 'highlight';
  }

  isSubvocMetronomeBeat(): boolean {
    return (this.engine?.state as any)?.metronomeBeat || false;
  }

  submitSubvocAnswer(answer: string): void {
    this.engine?.handleInput({ type: 'answer', answer });
    this.selectedAnswer = null; // Reset for next question
    this.cdr.detectChanges();
  }

  isSubvocShowingFeedback(): boolean {
    return (this.engine as any)?.showingFeedback || false;
  }

  getSubvocLastAnswer(): string {
    return (this.engine as any)?.lastAnswer || '';
  }

  isSubvocLastAnswerCorrect(): boolean {
    return (this.engine as any)?.lastAnswerCorrect || false;
  }

  getSubvocCorrectAnswer(): string {
    return (this.engine as any)?.currentCorrectAnswer || '';
  }

  nextSubvocQuestion(): void {
    (this.engine as any)?.nextQuestion?.();
    this.cdr.detectChanges();
  }

  getSubvocProgress(): number {
    return (this.engine as SubvocalizationReductionEngine)?.getProgress?.() || 0;
  }

  getSubvocTargetWpm(): number {
    return (this.engine as SubvocalizationReductionEngine)?.getTargetWpm?.() || 0;
  }

  getSubvocCurrentWpm(): number {
    return (this.engine as SubvocalizationReductionEngine)?.getCurrentWpm?.() || 0;
  }

  getSubvocChunkSize(): number {
    return this.engineState.actualChunkSize || (this.engine as any)?.config?.chunkSize || 1;
  }

  getSubvocInstruction(): string {
    return (this.engine as any)?.config?.description || '';
  }

  // ============================================
  // Focus Engine Animation Properties
  public hitsAnim = false;
  public missesAnim = false;
  public falseAlarmsAnim = false;
  private prevHits = 0;
  private prevMisses = 0;
  private prevFalseAlarms = 0;

  // Visualization Engine Helpers
  // ============================================
  getVisualizationPhase(): string {
    return (this.engine as VisualizationEngine)?.getPhase?.() || 'scene';
  }

  getVisualizationCurrentScene(): any {
    return (this.engine as VisualizationEngine)?.getCurrentScene?.();
  }

  getVisualizationCurrentQuestion(): any {
    return (this.engine as VisualizationEngine)?.getCurrentQuestion?.();
  }

  getVisualizationSceneProgress(): { current: number; total: number } {
    return (this.engine as VisualizationEngine)?.getSceneProgress?.() || { current: 0, total: 0 };
  }

  getVisualizationQuestionProgress(): { current: number; total: number } {
    return (this.engine as VisualizationEngine)?.getQuestionProgress?.() || { current: 0, total: 0 };
  }

  getVisualizationSceneRemaining(): number {
    return (this.engine as VisualizationEngine)?.getSceneDisplayRemaining?.() || 0;
  }

  getVisualizationMode(): string {
    return (this.engine as VisualizationEngine)?.mode || 'static';
  }

  getVisualizationGuidedStepText(): string {
    return (this.engine as VisualizationEngine)?.getGuidedStepText?.() || '';
  }

  skipVisualizationScene(): void {
    this.engine?.handleInput({ action: 'skip_scene' });
  }

  submitVisualizationAnswer(answer: string): void {
    this.engine?.handleInput({ type: 'answer', answer });
    this.cdr.detectChanges();
  }

  isVisualizationShowingFeedback(): boolean {
    return (this.engine as VisualizationEngine)?.showingFeedback || false;
  }

  getVisualizationLastAnswer(): string {
    return (this.engine as VisualizationEngine)?.lastAnswer || '';
  }

  isVisualizationLastAnswerCorrect(): boolean {
    return (this.engine as VisualizationEngine)?.lastAnswerCorrect || false;
  }

  getVisualizationCorrectAnswer(): string {
    return (this.engine as VisualizationEngine)?.correctAnswer || '';
  }

  nextVisualizationQuestion(): void {
    (this.engine as VisualizationEngine)?.nextQuestion?.();
    this.cdr.detectChanges();
  }

  hasTimeLimit(): boolean {
    if (this.engineState.remainingSeconds !== undefined) {
      return true;
    }
    const config = (this.parsedConfig?.engineConfig as any) || (this.parsedConfig as any) || {};
    const backendConfig = this.backendSessionConfig || {};

    return !!(
      backendConfig.timeLimitSeconds ||
      backendConfig.TimeLimitSeconds ||
      config.timeLimitSeconds ||
      config.timeLimit ||
      config.TimeLimit ||
      config.timing?.timeLimitSec ||
      config.rules?.timeLimit ||
      backendConfig.timing?.maxReadingTimeMs ||
      config.timing?.maxReadingTimeMs ||
      backendConfig.MaxReadingTimeMs ||
      config.MaxReadingTimeMs
    );
  }

  isTimeUrgent(): boolean {
    // Check engine state for time-based exercises (saccade, fixation, etc.)
    if (this.engineState?.remainingSeconds !== undefined && this.engineState.remainingSeconds <= 10) {
      return true;
    }
    // Check time limit for other exercises
    if (!this.hasTimeLimit()) return false;
    return this.getRemainingTimeMs() < 10000; // Less than 10 seconds
  }

  getExerciseIcon(): string {
    switch (this.engine?.engineType) {
      case 'grid_interaction': return 'grid_on';
      case 'text_stream': return 'flash_on';
      case 'motion_path': return 'visibility';
      case 'reading_comprehension': return 'psychology';
      case 'word_highlight': return 'speed';
      case 'regression_reduction': return 'sync_problem';
      case 'visualization': return 'palette';
      default: return 'fitness_center';
    }
  }

  getEngineDisplayName(): string {
    return this.engine?.displayName || 'Universal Engine';
  }

  getConfigSummary(): string {
    const engineType = this.parsedConfig?.engineType;
    const engineConfig = this.parsedConfig?.engineConfig;

    if (engineType === 'word_highlight') {
      const wpm = engineConfig?.['pacer']?.['speedWpm'];
      const chunkSize = engineConfig?.['pacer']?.['chunkSize'] || engineConfig?.['content']?.['chunkSize'];
      if (chunkSize > 1) return `${chunkSize}'li Kelime Grupları`;
      if (wpm) return `${wpm} WPM Hedef Hız`;
      return 'Hızlı Okuma';
    }

    if (engineType === 'reading_comprehension') {
      // Get actual question count from backend session data
      const backendQuestions = (this.backendSessionConfig as any)?.Questions ||
        (this.backendSessionConfig as any)?.questions || [];
      const actualQuestionCount = backendQuestions.length;

      // Fallback to config or default 10
      const questionCount = actualQuestionCount > 0 ? actualQuestionCount :
        (engineConfig?.['content']?.['questionCount'] || 10);
      return `Serbest Okuma + ${questionCount} Soru`;
    }

    if (engineType === 'regression_reduction') {
      const wpm = engineConfig?.['wpm'] || 200;
      const masking = engineConfig?.['maskingType'] || 'trailing';
      return `${wpm} WPM - ${masking === 'trailing' ? 'Trailing Mask' : 'Fade Out'}`;
    }

    if (engineType === 'text_stream') {
      const diff = engineConfig?.['DifficultyLevel'] || this.exercise?.difficultyLevel || 1;
      return `Seviye: ${this.getDifficultyLabel(diff)}`;
    }

    if (engineType === 'grid_interaction') {
      const gridSize = this.parsedConfig?.['gridSize'] ||
        this.parsedConfig?.['engineConfig']?.['grid']?.['rows'] || 5;
      return `${gridSize}x${gridSize} Izgara`;
    }

    if (engineType === 'motion_path') {
      return 'Göz Takibi ve Odaklanma';
    }

    if (engineType === 'visual_expansion') {
      return 'Görüş Alanı Genişletme';
    }

    if (engineType === 'scan_find') {
      return 'Hızlı Tarama ve Bulma';
    }

    if (engineType === 'text_fade') {
      return 'Metin Takibi';
    }

    return 'Egzersiz Hazır';
  }

  // Tachistoscope Helpers
  getDifficultyLabel(level: number): string {
    switch (level) {
      case 1: return 'Kolay';
      case 2: return 'Normal';
      case 3: return 'Zor';
      case 4: return 'İleri';
      case 5: return 'Usta';
      default: return 'Bilinmiyor';
    }
  }

  getAgeGroupLabel(): string {
    const ageGroupId = this.parsedConfig?.['metadata']?.['targetAgeGroupId'];
    switch (ageGroupId) {
      case '10000000-0000-0000-0000-000000000001': return 'Çocuk';
      case '10000000-0000-0000-0000-000000000002': return 'Genç';
      case '10000000-0000-0000-0000-000000000003': return 'Yetişkin';
      default: return '';
    }
  }

  getStimulusTypeLabel(type: string): string {
    switch (type) {
      case 'word': return 'Kelime';
      case 'phrase': return 'Kelime Grubu';
      case 'sentence': return 'Cümle';
      case 'number': return 'Sayı';
      default: return type;
    }
  }

  getEstimatedStimulusCount(level: number): number {
    switch (level) {
      case 1: return 15;
      case 2: return 20;
      case 3: return 25;
      case 4: return 30;
      case 5: return 35;
      default: return 20;
    }
  }

  getEstimatedDuration(level: number): number {
    switch (level) {
      case 1: return 500;
      case 2: return 400;
      case 3: return 300;
      case 4: return 200;
      case 5: return 150;
      default: return 350;
    }
  }

  // Motion Path helpers
  getTargetX(): number { return (this.engine as any)?.getTargetPosition()?.x || 50; }
  getTargetY(): number { return (this.engine as any)?.getTargetPosition()?.y || 50; }
  getFixationPointSize(): number { return (this.engine as any)?.getPointSize() || 36; }
  getFixationProgress(): number { return (this.engine as any)?.getFixationProgress() || 0; }
  getPeripheralChars(): any[] { return (this.engine as any)?.getPeripheralChars() || []; }
  getMotionPathFixationDuration(): number { return (this.engine as any)?.getFixationDuration() || 0; }

  trackPeripheralChar(index: number, char: any): string {
    return char.position; // Use position (top/bottom/left/right) as unique identifier
  }

  isFixationMode(): boolean {
    const mode = this.parsedConfig?.engineConfig?.['mode'];
    return mode === 'fixation' || !mode;
  }

  isSaccadeMode(): boolean {
    return this.parsedConfig?.engineConfig?.['mode'] === 'saccade';
  }

  getFixationLastChars(): string {
    const results = (this.engine as any)?.getFixationResults() || [];
    if (results.length === 0) return '';
    const last = results[results.length - 1];
    return last.peripheralChars.map((c: any) => c.char).join(', ');
  }

  getSaccadeValue(): string {
    return this.engineState.currentValue || '';
  }

  getSaccadeFontSize(): number {
    const value = this.getSaccadeValue();
    const pointSize = this.getFixationPointSize();

    if (!value) return pointSize * 0.6;

    // Single character (letter/number): 60% of point size
    if (value.length === 1) {
      return pointSize * 0.55;
    }

    // Word: Calculate to fit within circle
    // Approximate: circle can fit ~2-3 characters at 50% size
    const maxFontSize = pointSize * 0.4;
    const charBasedSize = pointSize / (value.length * 0.5);

    return Math.min(maxFontSize, Math.max(14, charBasedSize)); // Min 14px, max 40% of point
  }

  // Peripheral Vision Testing Helpers
  peripheralInputValue = '';

  isAwaitingPeripheralInput(): boolean {
    const isAwaiting = (this.engine as any)?.isAwaitingInput?.() || false;

    // When dialog first appears, clear input and focus
    if (isAwaiting && !this.lastAwaitingState) {
      this.peripheralInputValue = '';
      // Focus after a short delay to ensure DOM is ready
      setTimeout(() => {
        if (this.peripheralInput?.nativeElement) {
          this.peripheralInput.nativeElement.value = '';
          this.peripheralInput.nativeElement.focus();
        }
      }, 50);
    }
    this.lastAwaitingState = isAwaiting;

    return isAwaiting;
  }

  getPeripheralAccuracy(): number {
    return (this.engine as any)?.getPeripheralAccuracy?.() ?? 100;
  }

  /**
   * Mobile-compatible input handler for peripheral vision.
   * Uses 'input' event which works reliably on all platforms including mobile keyboards.
   */
  onPeripheralInputChange(event: Event): void {
    if (!this.engine || this.engine.engineType !== 'motion_path') return;

    const input = event.target as HTMLInputElement;
    const value = input.value.toUpperCase();

    // Update the model value (force uppercase)
    this.peripheralInputValue = value;
    input.value = value;

    // Get expected character count from engine
    const expectedCount = (this.engine as any)?.getExpectedCharCount?.() ||
      (this.engine as any)?.config?.fixation?.peripheralCount || 2;

    // Send each new character to the engine
    if (value.length > 0) {
      const lastChar = value[value.length - 1];
      if (/[A-Z]/.test(lastChar)) {
        this.engine.handleInput({ type: 'keypress', key: lastChar });
      }
    }

    // Auto-submit when all characters are entered
    if (value.length >= expectedCount) {
      setTimeout(() => this.submitPeripheralInput(), 100);
    }

    this.cdr.detectChanges();
  }

  submitPeripheralInput(): void {
    if (!this.engine || this.engine.engineType !== 'motion_path') return;

    // Send Enter to engine to submit
    this.engine.handleInput({ type: 'enter', key: 'Enter' });
    this.peripheralInputValue = '';
    this.cdr.detectChanges();
  }


  isShowingPeripheralFeedback(): boolean {
    return (this.engine as any)?.isShowingFeedback?.() || false;
  }

  getPeripheralFeedback(): { isCorrect: boolean; userInput: string; correctChars: string } | null {
    return (this.engine as any)?.getFeedback?.() || null;
  }

  getCorrectCount(): number {
    return (this.engine as any)?.getCorrectCount?.() || 0;
  }

  getIncorrectCount(): number {
    return (this.engine as any)?.getIncorrectCount?.() || 0;
  }

  // Text Stream helpers
  getCurrentStimulus(): string {
    return (this.engine as TextStreamEngine)?.getCurrentStimulus?.() || '';
  }

  isShowingStimulus(): boolean {
    return (this.engine as TextStreamEngine)?.isShowingContent?.() || false;
  }

  isShowingFixation(): boolean {
    return (this.engine as TextStreamEngine)?.isShowingFixationPoint?.() || false;
  }

  getStimulusFontSize(): string {
    return (this.engine as TextStreamEngine)?.getFontSize?.() || 'large';
  }

  isWaitingForAnswer(): boolean {
    const waiting = (this.engine as TextStreamEngine)?.isWaitingForUserAnswer?.() || false;
    if (waiting && !this.shouldFocusInput) {
      this.shouldFocusInput = true;
    }
    return waiting;
  }

  getCurrentDuration(): number {
    return (this.engine as TextStreamEngine)?.getCurrentDuration?.() || 500;
  }

  submitTachistoscopeAnswer(): void {
    if (!this.engine || this.engine.engineType !== 'text_stream') return;

    const answer = this.tachistoscopeAnswer.trim();
    const currentStimulus = this.getCurrentStimulus();

    // Submit to engine
    (this.engine as TextStreamEngine).handleInput({ answer });

    // Get last trial result for feedback
    const lastTrial = (this.engine as TextStreamEngine).getLastTrialResult?.();
    if (lastTrial) {
      this.tachistoscopeFeedback = {
        isCorrect: lastTrial.isCorrect,
        correctAnswer: lastTrial.stimulus
      };

      // Clear feedback after delay - increased to 1.2s
      setTimeout(() => {
        this.tachistoscopeFeedback = null;
        this.cdr.detectChanges();
      }, 1200);
    }

    // Clear input
    this.tachistoscopeAnswer = '';
    this.cdr.detectChanges();
  }

  // Text Fade helpers
  getActiveWordIndex(): number {
    return (this.engine as TextFadeEngine)?.getActiveIndex?.() || 0;
  }


  isWordFaded(index: number): boolean {
    if (this.engine?.engineType === 'text_fade') {
      return (this.engine as TextFadeEngine).getFadedIndex() >= index;
    }
    return false;
  }

  getFontSize(): string {
    if (this.engine?.engineType === 'text_fade') {
      return (this.engine as TextFadeEngine).getFontSize();
    }
    return 'medium';
  }

  // Word Highlight helpers
  getWords(): string[] {
    if (this.engine?.engineType === 'word_highlight') {
      return (this.engine as WordHighlightEngine).getWords?.() || [];
    }
    if (this.engine?.engineType === 'text_fade') {
      return (this.engine as TextFadeEngine).getWords?.() || [];
    }
    return [];
  }

  getCurrentWordIndex(): number {
    return (this.engine as WordHighlightEngine)?.getCurrentWordIndex?.() || 0;
  }

  getChunkSize(): number {
    if (this.engine?.engineType === 'word_highlight') {
      return (this.engine as WordHighlightEngine).getChunkSize?.() || 1;
    }
    return 1;
  }

  getHighlightFontSize(): string {
    if (this.engine?.engineType === 'word_highlight') {
      return (this.engine as WordHighlightEngine).getHighlightFontSize();
    }
    return 'medium';
  }

  getChunksForDisplay(): any[] {
    return (this.engine as WordHighlightEngine)?.getChunks?.() || [];
  }

  getCurrentChunkIndex(): number {
    return (this.engine as WordHighlightEngine)?.getCurrentChunkIndex?.() || 0;
  }

  // Visual Expansion helpers
  getCenterPointType(): string {
    return (this.engine as VisualExpansionEngine)?.getCenterPointType?.() || 'cross';
  }

  getExpansionStimuli(): any[] {
    return (this.engine as VisualExpansionEngine)?.getCurrentStimuli?.() || [];
  }

  private wasWaitingForInput = false;

  isExpansionWaitingInput(): boolean {
    const engine = this.engine;
    const isVisible = (engine as any)?.isStimulusVisible || false;
    const isWaiting = (engine as any)?.isWaitingForInput || false;
    const hasFeedback = !!this.expansionFeedback;

    // Uyaran (karakterler) ekrandayken cevap alanı gösterilmemeli
    if (isVisible) return false;

    // İlk kez true olduğunda focus'u tetikle
    if (isWaiting && !this.wasWaitingForInput) {
      this.focusExpansionInput();
    }
    this.wasWaitingForInput = isWaiting;

    // Ya giriş bekliyorsak ya da yanlış cevap sonrası feedback gösteriyorsak görünür kalmalı
    return isWaiting || hasFeedback;
  }

  getCurrentDegrees(): number {
    return (this.engine as any)?.currentDegrees || 5;
  }

  getExpansionCorrectCount(): number {
    if (this.engine?.engineType !== 'visual_expansion') return 0;
    return (this.engine as any)?.correctAnswers || 0;
  }

  getExpansionWrongCount(): number {
    if (this.engine?.engineType !== 'visual_expansion') return 0;
    const engine = this.engine as any;
    const total = engine?.totalAnswers || 0;
    const correct = engine?.correctAnswers || 0;
    return total - correct;
  }

  submitExpansionAnswer(): void {
    const engine = this.engine;
    if (!engine || engine.engineType !== 'visual_expansion') return;

    const answers = [this.expansionAnswerLeft, this.expansionAnswerRight];
    const lastShown = (engine as any).getLastShownStimuli?.() || [];

    // Motora cevabı gönder
    engine.handleInput({ answers });

    // Doğruluğu kontrol et ve feedback göster (sadece hata durumunda)
    const isCorrect = answers.every((a, i) =>
      a.toUpperCase().trim() === (lastShown[i] || '').toUpperCase()
    );

    if (!isCorrect) {
      this.expansionFeedback = {
        isCorrect: false,
        correctAnswer: lastShown.join(' - ')
      };

      // Hata durumunda motoru duraklat ki kullanıcı feedback'i okuyabilsin
      engine.pause();

      // Feedback'i 2 saniye sonra temizle
      setTimeout(() => {
        this.expansionFeedback = null;
        // Yanlış cevapta 2sn bekledikten sonra inputları temizle
        this.expansionAnswerLeft = '';
        this.expansionAnswerRight = '';

        // Motoru devam ettir
        engine.resume();

        this.cdr.detectChanges();
      }, 2000);
    } else {
      // Doğruysa feedback gösterme ve HEMEN temizle
      this.expansionFeedback = null;
      this.expansionAnswerLeft = '';
      this.expansionAnswerRight = '';
    }

    this.cdr.detectChanges();
  }

  onExpansionInputChange(position: 'left' | 'right', event: Event): void {
    const input = event.target as HTMLInputElement;
    const value = input.value;

    if (position === 'left' && value.length === 1) {
      // Sol input dolduysa sağ input'a geç
      setTimeout(() => {
        this.expansionRightInput?.nativeElement.focus();
      }, 0);
    } else if (position === 'right' && value.length === 1 && this.expansionAnswerLeft) {
      // Her iki input da doluysa otomatik gönder
      setTimeout(() => {
        this.submitExpansionAnswer();
      }, 100);
    }
  }

  focusExpansionInput(): void {
    setTimeout(() => {
      this.expansionLeftInput?.nativeElement.focus();
    }, 100);
  }

  getWpm(): number {
    if (this.engine?.engineType === 'word_highlight') {
      return (this.engine as WordHighlightEngine).getWpm?.() || 200;
    }
    if (this.engine?.engineType === 'text_fade') {
      return (this.engine as TextFadeEngine).getWpm?.() || 200;
    }
    // For RSVP / Text Stream
    if (this.engine?.engineType === 'text_stream') {
      const config = (this.engine as any).config;
      if (config?.timing?.intervalMs) {
        // 60000 / interval = WPM (approx)
        return Math.round(60000 / config.timing.intervalMs);
      }
    }
    return 0;
  }

  /**
   * Heatmap rengi hesapla (0-1 arası normalize değer)
   * Yeşil (hızlı) -> Sarı -> Turuncu -> Kırmızı (yavaş)
   */
  getHeatmapColor(value: number): string {
    if (value === 0) return '#e0e0e0'; // Hiç tıklanmamış

    // Renk geçişi: Yeşil -> Sarı -> Turuncu -> Kırmızı
    const colors = [
      { pos: 0, r: 76, g: 175, b: 80 },    // Yeşil
      { pos: 0.33, r: 255, g: 235, b: 59 }, // Sarı
      { pos: 0.66, r: 255, g: 152, b: 0 },  // Turuncu
      { pos: 1, r: 244, g: 67, b: 54 }      // Kırmızı
    ];

    // İki renk arasında interpolasyon
    for (let i = 0; i < colors.length - 1; i++) {
      if (value >= colors[i].pos && value <= colors[i + 1].pos) {
        const ratio = (value - colors[i].pos) / (colors[i + 1].pos - colors[i].pos);
        const r = Math.round(colors[i].r + (colors[i + 1].r - colors[i].r) * ratio);
        const g = Math.round(colors[i].g + (colors[i + 1].g - colors[i].g) * ratio);
        const b = Math.round(colors[i].b + (colors[i + 1].b - colors[i].b) * ratio);
        return `rgb(${r}, ${g}, ${b})`;
      }
    }

    return '#f44336'; // Varsayılan kırmızı
  }

  private saveResult(result: EngineResult): void {
    // Skip saving for Teachers in preview mode
    if (this.authService.hasRole('Teacher')) {
      this.showToast('📋 Önizleme modu - Sonuçlar kaydedilmedi.', 'info', 3000);
      return;
    }

    if (!this.sessionId) {
      console.warn('No active session, cannot save result securely.');
      this.showToast('Oturum bulunamadı, sonuç kaydedilemedi.', 'error', 3000);
      return;
    }

    const customData = {
      wordsRead: this.engineState.totalSteps,
      wpm: this.getWpm(),
      accuracy: result.accuracy,
      score: result.score,
      errors: result.errors,
      engineType: this.engine?.engineType,
      ...this.parsedConfig,
      details: result.details
    };


    // 2. Günlük egzersiz ilerlemesini kaydet (pratik modu değilse)
    const state = history.state;
    const isPracticeMode = state?.practiceMode === true;
    const isAssessmentMode = state?.assessmentMode === true;

    // 1. Session kaydet (gamification için) - Assessment modunda XP kazanımı backend tarafında engellenir
    this.sessionService.completeSession(this.sessionId, {
      customData: customData,
      isAssessmentMode: isAssessmentMode
    }).subscribe({
      next: (sessionResult: SessionResult) => {
        this.sessionResult = sessionResult;

        let msg = 'Sonuç kaydedildi.';
        if (sessionResult.xpGained > 0) {
          msg += ` 🎉 +${sessionResult.xpGained} XP kazandınız!`;
        }
        if (sessionResult.leveledUp) {
          msg += ` 🆙 Seviye Atladınız!`;
        }

        if (sessionResult.unlockedBadges && sessionResult.unlockedBadges.length > 0) {
          msg += ` 🏆 Yeni Rozet!`;
        }

        this.showToast(msg, 'info', 5000);
      },
      error: (err) => {
        console.error('Session completion failed:', err);
        this.showToast('Sonuç kaydedilirken hata oluştu', 'error', 3000);
      }
    });



    if (isAssessmentMode && this.exercise?.id) {
      // ASSESSMENT MODE: Create result for StudentExerciseResults table (Backend: submitExerciseResult)
      // This is required because DailyProgress service requires an active Program, which doesn't exist yet for Assessment.
      const resultData: CoreExerciseResult = {
        exerciseId: this.exercise.id,
        timeSpentSeconds: Math.round(result.totalTime / 1000),
        wordsRead: this.engineState.totalSteps || 0,
        rawWPM: this.getWpm(),
        comprehensionScore: (() => {
          // Robust score calculation for Assessment
          let score = result.accuracy;

          // 1. Visual Expansion Fallback
          if ((!score || score === 0) && this.engine?.engineType === 'visual_expansion') {
            const correct = this.getExpansionCorrectCount();
            const wrong = this.getExpansionWrongCount();
            const total = correct + wrong;
            if (total > 0) score = (correct / total) * 100;
          }

          // 2. Fixation / Motion Path Fallback
          else if (this.engine?.engineType === 'motion_path') {
            // For passive fixation exercises, completion = 100%
            // If active (has interactions), calculate based on hits
            const correct = (this.engine as any)?.correctAnswers || 0;
            const wrong = (this.engine as any)?.incorrectAnswers || 0;
            const total = correct + wrong;

            if (total > 0) {
              score = (correct / total) * 100;
            } else {
              // Passive mode: If completed, give full score
              score = 100;
            }
          }

          // 3. General Fallback to Engine Score if Accuracy is missing
          else if ((!score || score === 0) && result.score > 0) {
            score = result.score;
          }

          return Math.round(score || 0);
        })(), // Execute robust score calculation
        questionAnswersJson: JSON.stringify(this.questionAnswers.map(q => ({
          questionId: q.questionId,
          selectedAnswer: q.selectedAnswer,
          isCorrect: q.isCorrect
        }))),
        readingMovementsJson: JSON.stringify(customData), // Save full details here as metadata
        readingTextId: this.parsedConfig?.['metadata']?.['readingTextId']
      };

      this.exerciseService.submitExerciseResult(resultData).subscribe({
        next: () => {
          this.showToast('Seviye tespit sonucu kaydedildi.', 'info', 3000);
        },
        error: (err) => {
          console.error('[ExercisePlayer] Assessment submit failed:', err);
          this.showToast('Sonuç kaydedilirken hata oluştu', 'error', 3000);
        }
      });
    } else if (!isPracticeMode && this.exercise?.id) {
      const completeRequest: CompleteExerciseRequest = {
        exerciseId: this.exercise.id,
        successRate: result.accuracy,
        timeSpentSeconds: Math.max(1, Math.round(result.totalTime / 1000)),
        correctCount: result.completedSteps || 0,
        incorrectCount: result.errors || 0,
        totalAttempts: result.totalSteps || 0,
        averageResponseTimeMs: result.details?.averageResponseTimeMs || 0,
        medianResponseTimeMs: result.details?.medianResponseTimeMs || 0,
        stdDevResponseTimeMs: result.details?.stdDevResponseTimeMs || 0,
        pauseCount: result.details?.pauseCount || 0,
        totalPausedSeconds: result.details?.totalPausedSeconds || 0,
        resultDataJson: JSON.stringify(customData)
      };


      this.exerciseProgramService.completeExercise(completeRequest).subscribe({
        next: (response: any) => {

          // Check for PROGRAM COMPLETION (Priority 1)
          if (response.programCompleted) {
            console.log('🎉 Program Completed!', response);
            this.programCompletionData = response;
            this.showProgramCompletionModal = true;
            this.cdr.detectChanges();
            return; // Stop here, modal will take over
          }

          // Gün tamamlandıysa ekstra bildirim
          if (response.dayCompleted) {
            setTimeout(() => {
              this.showToast('🎉 Bugünün tüm egzersizlerini tamamladınız!', 'info', 5000);
            }, 2000);
          }

          // Streak bilgisini güncelle
          if (response.currentStreak > 1) {
          }
        },
        error: (err) => {
          console.error('[ExercisePlayer] Daily progress update failed:', err);
          // Session zaten kaydedildi, bu hata kritik değil
        }
      });
    } else if (isPracticeMode) {
    }
  }

  // Reading Comprehension helpers
  private readingScrollProgress = 0;

  getComprehensionText(): string {
    if (this.engine?.engineType === 'reading_comprehension' || this.engine?.engineType === 'exam_simulation') {
      return (this.engine as any).getText?.() || '';
    }
    return '';
  }

  getComprehensionWordCount(): number {
    if (this.engine?.engineType === 'reading_comprehension' || this.engine?.engineType === 'exam_simulation') {
      return (this.engine as any).getWordCount?.() || 0;
    }
    return 0;
  }

  getComprehensionFontSize(): string {
    if (this.engine?.engineType === 'reading_comprehension' || this.engine?.engineType === 'exam_simulation') {
      return (this.engine as any).getFontSize?.() || 'medium';
    }
    return 'medium';
  }

  getCurrentReadingWpm(): number {
    if (this.engine?.engineType === 'reading_comprehension' || this.engine?.engineType === 'exam_simulation') {
      const wordCount = (this.engine as any).getWordCount?.() || 0;
      const timeMinutes = this.engineState.timeElapsed / 1000 / 60;
      if (timeMinutes > 0) {
        return Math.round(wordCount / timeMinutes);
      }
    }
    return 0;
  }

  getReadingScrollProgress(): number {
    return this.readingScrollProgress;
  }

  onReadingScroll(event: Event): void {
    const element = event.target as HTMLElement;
    if (element) {
      const scrollPercent = (element.scrollTop / (element.scrollHeight - element.clientHeight)) * 100;
      this.readingScrollProgress = Math.min(100, Math.max(0, scrollPercent));

      // Notify engine about scroll progress
      if (this.engine?.engineType === 'reading_comprehension' || this.engine?.engineType === 'exam_simulation') {
        this.engine.handleInput({ scrollProgress: this.readingScrollProgress });
      }
    }
  }

  completeReading(): void {
    if (this.engine?.engineType === 'reading_comprehension' || this.engine?.engineType === 'exam_simulation') {
      (this.engine as any).completeReading();
    }
  }

  // ============ Scan Find Helper Methods ============

  getScanWords(): Array<{ text: string, id: number, isTarget: boolean, found: boolean }> {
    if (this.engine?.engineType === 'scan_find') {
      return (this.engine as ScanFindEngine).getWords?.() || [];
    }
    return [];
  }

  getScanTargets(): string[] {
    if (this.engine?.engineType === 'scan_find') {
      // Use the engine's getTargetWords method for original backend target list
      const targetWords = (this.engine as ScanFindEngine).getTargetWords?.();
      if (targetWords && targetWords.length > 0) {
        return targetWords;
      }
      // Fallback: extract from words if getTargetWords not available
      const words = (this.engine as ScanFindEngine).getWords?.() || [];
      const targets = new Set<string>();
      words.filter(w => w.isTarget).forEach(w => {
        targets.add(w.text.replace(/[.,;!?:'"()]/g, ''));
      });
      return Array.from(targets);
    }
    return [];
  }

  isScanTargetFound(target: string): boolean {
    if (this.engine?.engineType === 'scan_find') {
      const words = (this.engine as ScanFindEngine).getWords?.() || [];
      // Check if at least one instance of this target word has been found
      return words.some(w => {
        const cleanWord = w.text.replace(/[.,;!?:'"()]/g, '').toLowerCase();
        return cleanWord === target.toLowerCase() && w.found;
      });
    }
    return false;
  }

  onScanWordClick(index: number): void {
    if (this.engine?.engineType === 'scan_find') {
      (this.engine as ScanFindEngine).handleWordClick(index);
      this.cdr.detectChanges();
    }
  }

  // ============ Regression Reduction Helper Methods ============

  // ============ Regression Reduction Helper Methods ============

  // Focus Helpers removed (replaced by Mental Registration)


  // ==================== VOCABULARY BUILDER HELPERS ====================

  getVocabProgress(): { current: number; total: number } {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).getProgress();
    }
    return { current: 0, total: 0 };
  }

  getVocabCurrentWord(): any {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).getCurrentWord();
    }
    return null;
  }

  isVocabDefinitionVisible(): boolean {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).isShowingDefinition();
    }
    return false;
  }

  showVocabDefinition(): void {
    if (this.engine?.engineType === 'vocabulary_builder') {
      (this.engine as any).showDefinition();
      this.cdr.detectChanges();
    }
  }

  markVocabKnown(): void {
    if (this.engine?.engineType === 'vocabulary_builder') {
      (this.engine as any).markAsKnown();
      this.cdr.detectChanges();
    }
  }

  markVocabUnknown(): void {
    if (this.engine?.engineType === 'vocabulary_builder') {
      (this.engine as any).markAsUnknown();
      this.cdr.detectChanges();
    }
  }

  getVocabMode(): string {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).getMode();
    }
    return 'learning';
  }

  getVocabQuizOptions(): any[] {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).getQuizOptions();
    }
    return [];
  }

  submitVocabQuizAnswer(letter: string): void {
    if (this.engine?.engineType === 'vocabulary_builder') {
      (this.engine as any).submitQuizAnswer(letter);
      this.cdr.detectChanges();
    }
  }

  isVocabShowingFeedback(): boolean {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).isShowingFeedback();
    }
    return false;
  }

  isVocabAnswerCorrect(): boolean {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).getLastAnswerCorrect();
    }
    return false;
  }

  getVocabWordBox(wordId: string): number {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).getWordBox(wordId);
    }
    return 1;
  }

  getVocabCorrectAnswer(): string {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).getCorrectAnswer();
    }
    return '';
  }

  nextVocabQuizQuestion(): void {
    if (this.engine?.engineType === 'vocabulary_builder') {
      (this.engine as any).nextQuizQuestion();
      this.cdr.detectChanges();
    }
  }

  getVocabQuizQuestion(): string {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).getQuizQuestion();
    }
    return '';
  }

  getVocabTimeRemaining(): number {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).wordTimeRemaining || 0;
    }
    return 0;
  }

  getVocabTimeLimit(): number {
    if (this.engine?.engineType === 'vocabulary_builder') {
      return (this.engine as any).timeLimitPerWord || 0;
    }
    return 0;
  }

  hasVocabTimeLimit(): boolean {
    return this.getVocabTimeLimit() > 0 && this.getVocabMode() === 'quiz';
  }

  // --- Error Analysis Helpers ---
  getErrorAnalysisPhase(): string {
    if (this.engine?.engineType === 'error_analysis') {
      return (this.engine as any).getPhase?.() || 'idle';
    }
    return 'idle';
  }

  getErrorAnalysisWords(): any[] {
    if (this.engine?.engineType === 'error_analysis') {
      return (this.engine as any).getWords?.() || [];
    }
    return [];
  }

  getErrorAnalysisErrorCount(): number {
    if (this.engine?.engineType === 'error_analysis') {
      return (this.engine as any).getErrorCount?.() || 0;
    }
    return 0;
  }

  getErrorAnalysisFoundCount(): number {
    if (this.engine?.engineType === 'error_analysis') {
      return (this.engine as any).getFoundCount?.() || 0;
    }
    return 0;
  }

  getErrorAnalysisFalseAlarmCount(): number {
    if (this.engine?.engineType === 'error_analysis') {
      return (this.engine as any).getFalseAlarmCount?.() || 0;
    }
    return 0;
  }

  isErrorWordSelected(index: number): boolean {
    if (this.engine?.engineType === 'error_analysis') {
      return (this.engine as any).isWordSelected?.(index) || false;
    }
    return false;
  }

  isErrorWordFoundError(index: number): boolean {
    if (this.engine?.engineType === 'error_analysis') {
      return (this.engine as any).isWordFoundError?.(index) || false;
    }
    return false;
  }

  isErrorWordFalseAlarm(index: number): boolean {
    if (this.engine?.engineType === 'error_analysis') {
      return (this.engine as any).isWordFalseAlarm?.(index) || false;
    }
    return false;
  }

  onErrorWordClick(index: number): void {
    if (this.engine?.engineType === 'error_analysis') {
      this.engine.handleInput({ type: 'select_word', wordIndex: index });
      this.cdr.detectChanges();

      // Auto-complete if all errors are found
      const foundCount = this.getErrorAnalysisFoundCount();
      const totalCount = this.getErrorAnalysisErrorCount();

      if (totalCount > 0 && foundCount >= totalCount) {
        // Add a small delay for the user to see the last success animation
        setTimeout(() => {
          this.forceCompleteErrorAnalysis();
        }, 500);
      }
    }
  }

  forceCompleteErrorAnalysis(): void {
    if (this.engine?.engineType === 'error_analysis') {
      (this.engine as any).forceComplete?.();
      this.cdr.detectChanges();
    }
  }

  useErrorAnalysisHint(): void {
    if (this.engine?.engineType === 'error_analysis') {
      const hintIndex = (this.engine as any).useHint?.();
      if (typeof hintIndex === 'number') {
        this.currentHintIndex = hintIndex;
        this.cdr.detectChanges();

        // Highlight for 2 seconds then clear
        setTimeout(() => {
          this.currentHintIndex = null;
          this.cdr.detectChanges();
        }, 2000);
      }
    }
  }

  isErrorWordHint(index: number): boolean {
    return this.currentHintIndex === index;
  }
}

