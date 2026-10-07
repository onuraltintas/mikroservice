/**
 * Universal Exercise Player Component
 * Backend'den gelen engineType'a göre dinamik olarak mini-engine yükler.
 * 
 * Kullanım: /student/exercises/universal-player/:exerciseId
 */

import { Component, OnInit, OnDestroy, ChangeDetectorRef, ViewChild, ElementRef, AfterViewChecked, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { firstValueFrom, Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { canUseStaffTraining } from '../../../../core/guards/staff-training.guard';
import { applyCustomPreviewSettings } from './custom-preview-settings';
import { resolveSchulteSettings } from './schulte-settings';

import { EngineFactory, EngineType } from './engines/engine-factory';
import { shouldForwardExerciseAction } from './exercise-action-policy';
import { shouldShowReadingQuestions } from './reading-question-flow';
import { SkimmingEngine } from './engines/skimming.engine';
import {
  createActionFailureState,
  finishAfterPendingActions,
  recordActionFailure,
  resetActionFailureState,
  runForActionGeneration
} from './exercise-action-queue';
import { FocusEngine } from './engines/focus.engine';
import { AdaptiveFluencyEngine, AdaptiveFluencyStageFeedback } from './engines/adaptive-fluency.engine';
import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './engines/base-engine.interface';
import { GridInteractionEngine } from './engines/grid-interaction.engine';
import { TextStreamEngine } from './engines/text-stream.engine';
import { TextFadeEngine } from './engines/text-fade.engine';
import { WordHighlightEngine } from './engines/word-highlight.engine';
import { ReadingComprehensionEngine } from './engines/reading-comprehension.engine';
import { VisualExpansionEngine } from './engines/visual-expansion.engine';
import { MotionPathEngine } from './engines/motion-path.engine';
import { ScanFindEngine } from './engines/scan-find.engine';
import { ErrorAnalysisEngine } from './engines/error-analysis.engine';
import { RegressionReductionEngine } from './engines/regression-reduction.engine';
import { SubvocalizationReductionEngine } from './engines/subvocalization-reduction.engine';
import { VisualizationEngine } from './engines/visualization.engine';
import { VocabularyBuilderEngine } from './engines/vocabulary-builder.engine';
import { ExerciseService } from '../../../../core/services/exercise.service';
import { ExerciseSessionService } from '../../../../core/services/exercise-session.service';
import { ExerciseProgramService, CompleteExerciseRequest } from '../../../../core/services/exercise-program.service';
import { StudentProgramService } from '../../../../core/services/student-program.service'; // INJECTED
import { LearningPathService } from '../../../../core/services/learning-path.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ReviewService } from '../../../../services/review.service';
import {
  ActionData,
  StartSessionRequest,
  ExerciseResult as SessionResult,
  ValidationResponse
} from '../../../../core/models/exercise-session.model';
import { toCompleteSessionRequest } from '../../../../core/services/exercise-session-completion';

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
  private readonly learningPathService = inject(LearningPathService);
  private readonly sessionService = inject(ExerciseSessionService);
  private readonly reviewService = inject(ReviewService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);

  private readonly toaster = inject(ToasterService);

  showToast(msg: string, type: 'info' | 'warn' | 'error' = 'info', duration = 3000): void {
    if (type === 'error') {
      this.toaster.error(msg, duration);
    } else if (type === 'warn') {
      this.toaster.warning(msg, duration);
    } else {
      this.toaster.info(msg, duration);
    }
  }

  // Program Completion State
  showProgramCompletionModal = false;
  dailyProgressSaveStatus: 'idle' | 'saving' | 'saved' | 'failed' = 'idle';
  staffTrainingMode = false;
  private pendingDailyProgressRequest: CompleteExerciseRequest | null = null;

  startPostTrainingAssessment(): void {
    if (this.staffTrainingMode) {
      this.router.navigate(['/student/training-programs']);
      return;
    }
    this.router.navigate(['/student/assessment'], { queryParams: { phase: 2 } });
  }

  retryDailyProgress(): void {
    if (this.dailyProgressSaveStatus === 'failed' && this.pendingDailyProgressRequest) {
      this.submitDailyProgress(this.pendingDailyProgressRequest);
    }
  }
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
  resultSaveStatus: 'idle' | 'saving' | 'saved' | 'failed' | 'preview' = 'idle';
  reviewSaveStatus: 'idle' | 'saving' | 'saved' | 'failed' = 'idle';
  reviewItemId: string | null = null;

  // Grid interaction specific
  clickedCells = new Set<number>();
  correctCells = new Set<number>();
  schulteSettings = resolveSchulteSettings({}, {});
  wrongCells = new Set<number>();

  sessionId: string | null = null;
  sessionResult: SessionResult | null = null;
  showExitConfirm = false;
  isAssessmentMode = false;
  assessmentAttemptId: string | null = null;
  assessmentPhase: number | null = null;

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
  private adaptiveQuestionHistory: any[] = [];
  readingWpm = 0;
  private readingIncomplete = false;
  exercisePhase: 'reading' | 'questions' | 'completed' = 'reading';
  selectedAnswer: string | null = null;
  questionFeedback: { isCorrect: boolean | null; correctAnswer?: string; explanation?: string; question?: any } | null = null;

  // Question Timer (for Exam Simulation)
  questionTimeRemaining = 0;
  questionTimerInterval: any = null;
  questionStartTime = 0;
  private questionTimerGeneration = 0;

  // Visual Expansion specific
  expansionAnswers: string[] = [];
  @ViewChild('visualExpansionArea') visualExpansionArea?: ElementRef<HTMLDivElement>;

  // Peripheral Vision Input
  @ViewChild('peripheralInput') peripheralInput?: ElementRef<HTMLInputElement>;
  private lastAwaitingState = false;
  private readingTrackingStarted = false;
  private readingTrackingFinished = false;
  private readingTrackingStartCompleted = false;
  private pendingReadingCompletion?: () => void;
  private actionQueue: Promise<void> = Promise.resolve();
  isPauseTransitionPending = false;
  private restoredTachistoscopePaused = false;
  private readonly actionFailureState = createActionFailureState();
  questionSubmissionPending = false;

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
        this.showToast('Yeni programınız başarıyla başlatıldı!', 'info', 3000);
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
  pathItemId: string | null = null;
  customPreviewActive = false;

  ngOnInit(): void {
    // Scroll to top immediately and after a short delay to ensure it works
    window.scrollTo(0, 0);
    setTimeout(() => {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }, 100);

    const state = history.state;

    // Check assignment and assessment context from query params so the flow survives refreshes.
    this.route.queryParams.subscribe(params => {
      this.staffTrainingMode = params['mode'] === 'staff-training' && canUseStaffTraining(this.authService);
      this.assignmentId = params['assignmentId'];
      this.reviewItemId = params['mode'] === 'review' ? params['reviewItemId'] || null : null;
      this.pathItemId = params['pathItemId'] || null;
      this.isAssessmentMode = state?.assessmentMode === true || params['assessmentMode'] === 'true';
      this.assessmentAttemptId = state?.assessmentAttemptId || params['assessmentAttemptId'] || null;
      const phase = Number(state?.assessmentPhase || params['assessmentPhase']);
      this.assessmentPhase = Number.isInteger(phase) && phase >= 1 && phase <= 4 ? phase : null;
    });

    const exerciseId = this.route.snapshot.paramMap.get('exerciseId');
    if (exerciseId) {
      this.loadExercise(exerciseId);
    } else {
      this.error = 'Egzersiz ID bulunamadı';
      this.finishLoading();
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
    if (this.engine?.engineType === 'word_highlight' && !(this.engine as WordHighlightEngine).shouldAutoScroll()) return;
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
          this.applyCustomPreview(history.state);
          if (this.error) { this.finishLoading(); return; }
          this.startSession();
        },
        error: (err) => {
          this.error = 'Egzersiz yüklenirken hata oluştu';
          this.finishLoading();
          console.error('[ExercisePlayer] Load error:', err);
        }
      });
  }

  private startSession(): void {
    if (!this.exercise) {
      this.error = 'Egzersiz bilgisi bulunamadı';
      this.finishLoading();
      return;
    }

    this.resetActionTracking();

    // Preview users must never create a server-owned session. Besides keeping
    // results out of progress tables, this also prevents gamification and
    // adaptive-learning side effects. Reading text content is still loaded
    // from the catalogue so a preview uses the same material as a student.
    if (this.isPreviewSession()) {
      this.startPreviewSession();
      return;
    }

    const request: StartSessionRequest = {
      exerciseId: this.exercise.id,
      readingTextId: this.parsedConfig?.['metadata']?.targetReadingTextId || this.parsedConfig?.['readingTextId'],
      studentAssignmentId: this.assignmentId || undefined,
      assessmentAttemptId: this.isAssessmentMode ? this.assessmentAttemptId || undefined : undefined
    };

    this.sessionService.startSession(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.sessionId = response.sessionId;

          const initialData = response.initialData || (response as any).InitialData || {};
          this.restoredTachistoscopePaused = response.status === 2 && !!initialData.tachistoscope;
          const configuration = response.configuration || (response as any).Configuration || {};
          // Assessment content is pinned by the server when the attempt is
          // created. Normalize the public session snapshot into the fields
          // every reading engine understands, so the catalogue's generic
          // configuration can never replace the attempt's text.
          this.backendSessionConfig = this.normalizeSessionConfiguration(configuration, initialData);

          try {
            if (this.isAssessmentMode
              && this.isReadingAssessmentEngine(this.backendSessionConfig)
              && !this.backendSessionConfig.readingTextContent) {
              throw new Error('Seviye tespit metni sunucudan alınamadı. Lütfen değerlendirmeyi yeniden başlatın.');
            }

            // Assessment questions are part of the sanitized initial session
            // state; configuration is reserved for engine settings.
            const questionSources = [this.backendSessionConfig, initialData];
            const questions = questionSources.map(source =>
              source?.Questions ||
              source?.questions ||
              source?.Content?.Questions ||
              source?.content?.questions
            ).find(candidate => Array.isArray(candidate));
            if (questions && Array.isArray(questions)) {
              this.comprehensionQuestions = questions;
            }

            this.restoreAssessmentQuestionProgress(this.backendSessionConfig);

            this.initializeEngine();
            this.resumeAssessmentSession(this.backendSessionConfig);
          } catch (error) {
            this.handleInitializationError(error);
          } finally {
            this.finishLoading();
          }
        },
        error: (err) => {
          console.error('Session start failed:', err);
          this.error = 'Oturum başlatılamadı. Lütfen internet bağlantınızı kontrol edin.';
          this.finishLoading();
        }
      });
  }

  private startPreviewSession(): void {
    this.sessionId = 'preview-mode';
    this.backendSessionConfig = {};

    const readingTextId = this.getConfiguredReadingTextId();
    if (!readingTextId) {
      this.initializePreviewEngine();
      return;
    }

    this.exerciseService.getReadingText(readingTextId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: readingText => {
          this.backendSessionConfig = this.createPreviewReadingConfig(readingText);
          this.comprehensionQuestions = this.backendSessionConfig['questions'] as any[];
          this.initializePreviewEngine();
        },
        error: error => {
          // A preview remains usable with the exercise's own configuration if
          // an optional reading-text lookup is unavailable.
          console.warn('[ExercisePlayer] Preview reading text could not be loaded:', error);
          this.initializePreviewEngine();
        }
      });
  }

  private initializePreviewEngine(): void {
    try {
      this.initializeEngine();
    } catch (error) {
      this.handleInitializationError(error);
    } finally {
      this.finishLoading();
    }
  }

  private getConfiguredReadingTextId(): string | undefined {
    const metadata = this.parsedConfig?.['metadata'] ?? this.parsedConfig?.['Metadata'] ?? {};
    const value = metadata.targetReadingTextId
      ?? metadata.TargetReadingTextId
      ?? this.parsedConfig?.['readingTextId']
      ?? this.parsedConfig?.['ReadingTextId'];
    return typeof value === 'string' && value.trim() ? value : undefined;
  }

  private createPreviewReadingConfig(readingText: any): Record<string, unknown> {
    const content = typeof readingText?.content === 'string' ? readingText.content : '';
    const title = typeof readingText?.title === 'string' ? readingText.title : '';
    const wordCount = Number(readingText?.wordCount) || content.split(/\s+/).filter(Boolean).length;
    const availableQuestions = Array.isArray(readingText?.questions) ? readingText.questions : [];
    const questions = this.parsedConfig?.['engineType'] === 'skimming'
      ? availableQuestions.filter((question: any) => Number(question.type ?? question.questionType ?? question.QuestionType) === 1)
      : availableQuestions;
    const existingContent = this.parsedConfig?.['content'];
    const contentConfig = existingContent && typeof existingContent === 'object'
      ? existingContent
      : {};
    const words = content.split(/\s+/).filter(Boolean);

    return {
      readingTextContent: content,
      readingTextTitle: title,
      wordCount,
      ReadingTextContent: content,
      ReadingTextTitle: title,
      Content: {
        ...contentConfig,
        Text: content,
        text: content,
        Title: title,
        title,
        WordCount: wordCount,
        wordCount
      },
      content: {
        ...contentConfig,
        text: content,
        title,
        wordCount,
        source: 'custom',
        items: words
      },
      Questions: questions,
      questions,
      adaptivePrimaryQuestions: questions,
      adaptiveTransferContent: content,
      adaptiveTransferTitle: title,
      adaptiveTransferWordCount: wordCount,
      adaptiveTransferQuestions: questions
    };
  }

  private normalizeSessionConfiguration(configuration: any, initialData: any): any {
    const config = this.isRecord(configuration) ? configuration : {};
    const state: any = this.isRecord(initialData) ? initialData : {};
    const merged = { ...config, ...state };

    const content = this.readFirstString(
      state.content,
      state.Content?.Text,
      state.Content?.text,
      state.readingTextContent,
      state.ReadingTextContent,
      state.text,
      state.Text);
    const title = this.readFirstString(
      state.readingTextTitle,
      state.ReadingTextTitle,
      state.Content?.Title,
      state.Content?.title) ?? '';
    const questions = Array.isArray(state.questions)
      ? state.questions
      : Array.isArray(state.Questions)
        ? state.Questions
        : Array.isArray(state.Content?.questions)
          ? state.Content.questions
          : Array.isArray(state.Content?.Questions)
            ? state.Content.Questions
            : undefined;

    if (!content) {
      return { ...merged, isAssessmentMode: this.isAssessmentMode };
    }

    const wordCount = this.readPositiveNumber(
      state.wordCount,
      state.WordCount,
      state.Content?.wordCount,
      state.Content?.WordCount) || content.split(/\s+/).filter(Boolean).length;
    const contentObject = {
      ...(this.isRecord(state.Content) ? state.Content : {}),
      Text: content,
      text: content,
      Title: title,
      title,
      WordCount: wordCount,
      wordCount
    };

    return {
      ...merged,
      isAssessmentMode: this.isAssessmentMode,
      engineConfig: {
        ...(this.isRecord(config['engineConfig']) ? config['engineConfig'] : {}),
        ...(this.isRecord(state.engineConfig) ? state.engineConfig : {}),
        readingTextContent: content,
        content: contentObject,
        wordCount,
        ...(questions ? { Questions: questions, questions } : {})
      },
      readingTextContent: content,
      ReadingTextContent: content,
      readingTextTitle: title,
      ReadingTextTitle: title,
      wordCount,
      WordCount: wordCount,
      // The generic exercise configuration may contain an object named
      // `content`; replace it with the server-owned assessment text.
      content,
      Content: contentObject,
      ...(questions ? { Questions: questions, questions } : {})
    };
  }

  private isReadingAssessmentEngine(config: any): boolean {
    const engineType = String(
      config?.engineType
      ?? config?.EngineType
      ?? this.parsedConfig?.engineType
      ?? '').toLowerCase();
    return [
      'reading_comprehension',
      'word_highlight',
      'exam_simulation',
      'text_fade',
      'text_stream',
      'free_reading',
      'rsvp',
      'regression_reduction',
      'subvocalization_reduction',
      'chunking',
      'skimming',
      'scanning'
    ].includes(engineType);
  }

  /**
   * Assessment sessions can be resumed after a refresh or an interrupted
   * request. The server snapshot contains answers already accepted for the
   * session; keep those questions out of the client question loop so the UI
   * does not submit an already-recorded answer again.
   */
  private restoreAssessmentQuestionProgress(initialData: any): void {
    if ((!this.isAssessmentMode && this.backendSessionConfig?.engineType !== 'exam_simulation') || !this.comprehensionQuestions.length) return;

    const state = this.isRecord(initialData) ? initialData : {};
    const answers = state['answers'] ?? state['Answers'];
    if (!Array.isArray(answers) || answers.length === 0) return;

    const questionsById = new Map<string, any>();
    this.comprehensionQuestions.forEach(question => {
      const id = question.QuestionId || question.questionId;
      if (typeof id === 'string' && id) questionsById.set(id, question);
    });

    this.questionAnswers = answers
      .map(answer => {
        const questionId = answer?.questionId || answer?.QuestionId;
        const question = questionsById.get(questionId);
        if (!questionId || !question) return null;

        const rawAnswer = answer?.answer ?? answer?.Answer;
        const selectedAnswer = typeof rawAnswer === 'string' && rawAnswer !== '__timeout__'
          ? rawAnswer.toUpperCase()
          : '';
        const targetTime = question.TargetTimeSeconds || question.targetTimeSeconds || 60;
        return {
          questionId,
          selectedAnswer,
          isCorrect: answer?.isCorrect === true || answer?.IsCorrect === true,
          timeSpent: Number(answer?.timeSpentSeconds ?? answer?.TimeSpentSeconds) || 0,
          targetTime,
          questionText: question.QuestionText || question.questionText || question.Text || question.text
        };
      })
      .filter((answer): answer is NonNullable<typeof answer> => answer !== null);

    const answeredIds = new Set(this.questionAnswers.map(answer => answer.questionId));
    const firstUnanswered = this.comprehensionQuestions.findIndex(question => {
      const id = question.QuestionId || question.questionId;
      return typeof id !== 'string' || !answeredIds.has(id);
    });
    this.currentQuestionIndex = firstUnanswered >= 0
      ? firstUnanswered
      : this.comprehensionQuestions.length - 1;
  }

  /** Resume a reading assessment that had already moved past its text phase. */
  private resumeAssessmentSession(initialData: any): void {
    if (!this.isAssessmentMode || !this.engine || !this.comprehensionQuestions.length) return;
    const state = this.isRecord(initialData) ? initialData : {};
    const answers = state['answers'] ?? state['Answers'];
    const hasFinishedReading = Boolean(
      state['readingEndTime']
      || state['ReadingEndTime']
      || (Array.isArray(answers) && answers.length > 0));
    if (!hasFinishedReading) return;

    this.exercisePhase = 'questions';
    this.engineState.isRunning = true;
    this.selectedAnswer = null;
    this.questionFeedback = null;
    if (this.questionAnswers.length >= this.comprehensionQuestions.length) {
      this.finishQuestionPhase();
      return;
    }
    this.startQuestionTimer();
    this.cdr.detectChanges();
  }

  private isRecord(value: unknown): value is Record<string, any> {
    return !!value && typeof value === 'object' && !Array.isArray(value);
  }

  private readFirstString(...values: unknown[]): string | undefined {
    return values.find(value => typeof value === 'string' && value.trim().length > 0) as string | undefined;
  }

  private readPositiveNumber(...values: unknown[]): number | undefined {
    const value = values.find(item => typeof item === 'number' && Number.isFinite(item) && item > 0);
    return typeof value === 'number' ? value : undefined;
  }

  /**
   * HTTP callbacks can complete outside the component's normal change
   * detection turn when the Fetch backend is used. Always publish the final
   * loading state explicitly so a successful session cannot leave the player
   * on the indefinite spinner.
   */
  private finishLoading(): void {
    this.isLoading = false;
    this.cdr.detectChanges();
  }

  private handleInitializationError(error: unknown): void {
    console.error('[ExercisePlayer] Engine initialization failed:', error);
    this.engine = null;
    this.error = 'Egzersiz hazırlanırken bir hata oluştu. Lütfen tekrar deneyin.';
  }

  private applyCustomPreview(state: unknown): void {
    this.customPreviewActive = false;
    if (!this.parsedConfig || !this.isPreviewSession() || this.isAssessmentMode || this.assignmentId || this.reviewItemId || this.pathItemId) return;
    if (!state || typeof state !== 'object') return;
    const custom = (state as Record<string, unknown>)['customPreview'];
    if (!custom || typeof custom !== 'object') return;
    const request = custom as Record<string, unknown>;
    if (request['exerciseId'] !== this.exercise?.id || !request['values'] || typeof request['values'] !== 'object' || Array.isArray(request['values'])) return;
    const roles = this.authService.currentUserValue?.roles ?? [];
    if (!roles.some(role => ['Admin', 'SystemAdmin', 'Teacher'].includes(role))) return;
    try {
      const changed = applyCustomPreviewSettings(this.parsedConfig, request['values'] as Record<string, unknown>, { roles, preview: true });
      this.customPreviewActive = changed !== this.parsedConfig;
      this.parsedConfig = changed;
    } catch {
      this.error = 'Özel ayarlar geçersiz. Kataloğa dönüp ayarları kontrol edin.';
    }
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
          if (this.engine?.engineType === 'focus' || this.engine?.engineType === 'attention_training') {
            void this.enqueueAction({
              action: 'focus_start',
              timestamp: new Date()
            } as ActionData).catch(() => undefined);
          }
        },
        onPause: () => {
          this.stopTimer();
        },
        onResume: () => {
          this.startTimer();
        },
        onComplete: (result) => {
          this.stopTimer();
          this.readingIncomplete = result.details?.timedOut === true;

          if (this.engine?.engineType === 'adaptive_fluency') {
            this.handleAdaptiveReadingCompleted(result);
            return;
          }

          const finalizeCompletion = () => {

          // For word_highlight or reading_comprehension with questions, go to question phase
          const hasQuestions = shouldShowReadingQuestions(
            this.engine?.engineType,
            this.engine?.engineType === 'text_stream' ? (this.engine as TextStreamEngine).getMode() : undefined,
            this.comprehensionQuestions.length,
            this.exercise?.exerciseTypeName);
          const isReadingEngine =
            this.engine?.engineType === 'skimming' ||
            this.engine?.engineType === 'word_highlight' ||
            this.engine?.engineType === 'reading_comprehension' ||
            this.engine?.engineType === 'exam_simulation' ||
            this.engine?.engineType === 'text_fade' ||
            (this.engine?.engineType === 'text_stream' && !this.isTachistoscopeMode()) ||
            this.engine?.engineType === 'free_reading'; // text_stream (RSVP) added

          // Regression Reduction engine handles its own question flow internally
          // When it calls onComplete, it means everything (reading + questions) is done
          if (this.engine?.engineType === 'regression_reduction') {
            const normalizedResult = this.normalizeEngineResultForDisplay(result);
            this.result = normalizedResult;
            this.exercisePhase = 'completed';
            this.saveResult(normalizedResult);
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
            this.readingWpm = this.readingIncomplete || this.engine?.engineType === 'skimming' ? 0 : (this.engine as any).getCurrentWPM?.() ||
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

          const normalizedResult = this.normalizeEngineResultForDisplay(result);
          this.result = normalizedResult;
          this.exercisePhase = 'completed';
          this.saveResult(normalizedResult);
          this.cdr.detectChanges();
          };

          this.waitForPendingActions(() => this.finishReadingTracking(finalizeCompletion, result.details?.timedOut === true));
        },
        onError: (error) => {
          if (this.engine?.engineType === 'error_analysis' && this.engine.state.isRunning) {
            this.toaster.error(error); this.cdr.detectChanges(); return;
          }
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
          if (this.isTachistoscopeMode() && !this.isAssessmentMode) this.refreshTachistoscopeFeedback();
        },
        onAction: (action) => {
          if (this.engine?.engineType === 'error_analysis') {
            const errorEngine = this.engine as ErrorAnalysisEngine;
            void this.enqueueAction(action as ActionData, response => errorEngine.reconcileServerResponse(action, response), false)
              .catch(() => errorEngine.reconcileServerResponse(action, { isValid: false }));
            return;
          }
          // Backend motoruna aksiyonu bildir

          // Pasif gözlem/okuma bazlı egzersizlerde her adımda validation yapma
          // Bu egzersizler completion'da topluca değerlendirilir (rate limit + performans)
          const engineType = this.parsedConfig?.engineType || this.engine?.engineType;
          const engineMode = this.engine?.engineType === 'motion_path'
            ? (this.engine as MotionPathEngine).getMode()
            : this.parsedConfig?.engineConfig?.['mode']
            || this.parsedConfig?.['mode']
            || this.backendSessionConfig?.['mode'];

          if (engineType === 'vocabulary_builder') {
            const vocabularyItemId = (action as any).wordId;
            if (typeof vocabularyItemId === 'string'
              && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(vocabularyItemId)) {
              const reviewKind = action.action === 'mark_known'
                ? 'known'
                : action.action === 'mark_unknown'
                  ? 'unknown'
                  : action.action === 'timeout'
                    ? 'timeout'
                    : action.action === 'answer_question'
                      ? 'quiz'
                      : null;
              if (!reviewKind) return;
              const vocabularyEngine = this.engine as VocabularyBuilderEngine;
              void this.enqueueAction({
                ...action,
                action: 'vocabulary_review',
                customData: {
                  ...action.customData,
                  vocabularyItemId,
                  reviewKind
                }
              }, response => vocabularyEngine.applyServerResponse(response))
                .catch(() => vocabularyEngine.applyServerResponse({ isValid: false }));
            }
            return;
          }

          // Validation GEREKEN egzersizler (kullanıcı aktif input yapıyor)
          const requiresValidation = [
            'grid_interaction',    // Schulte/grid tıklamaları server layout ile doğrulanır
            'schulte_grid',      // Tıklama sırası doğrulanmalı
            'memory_grid',       // Hafıza testi, seçimler doğrulanmalı
          ];

          // Focus engine için özel mantık: sadece match aksiyonlarını backend'e gönder
          if (engineType === 'focus' || engineType === 'attention_training') {
            const validFocusActions = ['focus_start', 'focus_step', 'position_match', 'word_match', 'match_attempt', 'complete'];
            if (!validFocusActions.includes(action.action)) {
              // step_change, feedback gibi internal aksiyonları atla
              return;
            }
          }

          if (!shouldForwardExerciseAction(engineType, engineMode, action.action)) {
            return;
          }

          // Sadece kullanıcı input gerektiren egzersizlerde validation yap
          const onResponse = ['regression_reduction', 'subvocalization_reduction'].includes(engineType || '') && action.action === 'finish_reading'
            ? (response: ValidationResponse) => {
              if (!response.isValid) throw new Error(response.message || 'Okuma aşaması kaydedilemedi.');
              this.readingTrackingFinished = true;
            }
            : (engineType === 'focus' || engineType === 'attention_training')
            ? (response: ValidationResponse) =>
              (this.engine as FocusEngine).reconcileServerResponse(action, response)
            : engineType === 'visualization'
              ? (response: ValidationResponse) =>
                (this.engine as VisualizationEngine).applyServerResponse(response)
              : engineType === 'visual_expansion'
                ? (response: ValidationResponse) =>
                  (this.engine as VisualExpansionEngine).reconcileServerResponse(action, response)
              : engineType === 'motion_path'
                ? (response: ValidationResponse) =>
                  (this.engine as MotionPathEngine).reconcileServerResponse(action, response)
              : engineType === 'text_stream'
                ? (response: ValidationResponse) =>
                  (this.engine as TextStreamEngine).reconcileServerResponse(action, response)
              : this.engine?.engineType === 'scan_find'
                ? (response: ValidationResponse) =>
                  (this.engine as ScanFindEngine).reconcileServerResponse(action, response)
              : undefined;
          void this.enqueueAction(action as ActionData, onResponse).catch(() => undefined);
        }
      };

      // Engine config'i hazırla - backend session data ile birleştir
      this.schulteSettings = resolveSchulteSettings(this.parsedConfig, this.backendSessionConfig);
      const engineConfig = {
        ...(this.parsedConfig || {}),
        ...(this.parsedConfig?.engineConfig || {}),
        ...(this.backendSessionConfig || {}),
        ...(this.backendSessionConfig?.EngineConfig || {}),
        ...(this.backendSessionConfig?.engineConfig || {}),
        // Extract gridSize from legacy root property or new unified engineConfig.grid.rows
        gridSize: this.backendSessionConfig?.['gridSize'] ||
          this.backendSessionConfig?.['engineConfig']?.['grid']?.['rows'] ||
          this.parsedConfig?.['gridSize'] ||
          this.parsedConfig?.['engineConfig']?.['grid']?.['rows'] || 5,
        serverGrid: engineType === 'grid_interaction'
          ? this.backendSessionConfig?.['grid']
          : undefined,
        sequenceType: 'numeric',
        ...(engineType === 'grid_interaction' ? this.schulteSettings : {}),
        exerciseTypeName: this.exercise?.exerciseTypeName,
        mode: engineType === 'motion_path'
          ? (this.parsedConfig?.engineConfig?.['mode'] || this.parsedConfig?.['mode']
            || this.backendSessionConfig?.['textStreamMode']
            || (this.exercise?.exerciseTypeName?.replace(/[^a-z]/gi, '').toLowerCase() === 'eyetracking' ? 'tracking' : 'fixation'))
          : (this.backendSessionConfig?.FocusMode
            || this.backendSessionConfig?.focusMode
            || this.backendSessionConfig?.mode
            || this.parsedConfig?.engineConfig?.['mode']),
        NLevel: this.backendSessionConfig?.FocusNLevel
          || this.backendSessionConfig?.focusNLevel
          || this.backendSessionConfig?.NLevel
          || this.parsedConfig?.engineConfig?.['NLevel'],
        PositionSequence: this.isAssessmentMode
          ? undefined
          : (this.backendSessionConfig?.PositionSequence
            || this.backendSessionConfig?.positionSequence
            || this.parsedConfig?.engineConfig?.['PositionSequence']),
        positionSequence: this.isAssessmentMode
          ? undefined
          : (this.backendSessionConfig?.positionSequence
            || this.parsedConfig?.engineConfig?.['positionSequence']),
        WordSequence: this.isAssessmentMode
          ? undefined
          : (this.backendSessionConfig?.WordSequence
            || this.backendSessionConfig?.wordSequence
            || this.parsedConfig?.engineConfig?.['WordSequence']),
        wordSequence: this.isAssessmentMode
          ? undefined
          : (this.backendSessionConfig?.wordSequence
            || this.parsedConfig?.engineConfig?.['wordSequence']),
        // Visual expansion keeps its progression values at the catalog root
        // for backwards compatibility. Pass them through for preview mode as
        // well as server-owned student sessions.
        rounds: this.backendSessionConfig?.rounds
          || this.backendSessionConfig?.Rounds
          || this.parsedConfig?.['rounds'],
        startDegrees: this.backendSessionConfig?.startDegrees
          || this.backendSessionConfig?.StartDegrees
          || this.backendSessionConfig?.visualExpansionStartDegrees
          || this.parsedConfig?.['startDegrees'],
        targetDegrees: this.backendSessionConfig?.targetDegrees
          || this.backendSessionConfig?.TargetDegrees
          || this.backendSessionConfig?.visualExpansionTargetDegrees
          || this.parsedConfig?.['targetDegrees'],
        displayDurationMs: this.backendSessionConfig?.displayDurationMs
          || this.backendSessionConfig?.DisplayDurationMs
          || this.backendSessionConfig?.visualExpansionDisplayDurationMs
          || this.parsedConfig?.['displayDurationMs'],
        isAssessmentMode: this.isAssessmentMode,
        scenes: this.backendSessionConfig?.VisualizationScenes
          || this.backendSessionConfig?.visualizationScenes
          || this.parsedConfig?.['scenes']
          || this.parsedConfig?.['Scenes'],
        words: this.backendSessionConfig?.errorAnalysisWords
          || this.backendSessionConfig?.vocabularyWords
          || this.backendSessionConfig?.VocabularyWords
          || this.backendSessionConfig?.words
          || this.backendSessionConfig?.Words
          || ((!this.sessionId || this.sessionId === 'preview-mode' || this.isPreviewSession())
            ? this.parsedConfig?.engineConfig?.['words'] : undefined),
        serverAuthoritative: !!this.sessionId && this.sessionId !== 'preview-mode',
        previewOnly: this.sessionId === 'preview-mode',
        getRenderBounds: () => {
          const element = this.visualExpansionArea?.nativeElement;
          return element
            ? { width: element.clientWidth, height: element.clientHeight }
            : { width: window.innerWidth, height: window.innerHeight };
        },
        // Metadata ve yaş grubu/zorluk bilgisini ekle
        metadata: this.parsedConfig?.['metadata'],
        difficultyLevel: this.parsedConfig?.['difficultyLevel'] || this.backendSessionConfig?.difficultyLevel
          || this.backendSessionConfig?.DifficultyLevel || this.exercise?.difficultyLevel
      };

      this.engine.initialize(engineConfig, callbacks);

      // --- Timer Initialization for RSVP / Duration Based Mode ---
      // If the exercise has a defined duration (implied by word count * interval for RSVP), setup the timer
      if (this.engine.engineType === 'text_stream' && !this.isTachistoscopeMode()) {
        this.totalDurationSeconds = Math.ceil((this.engine as TextStreamEngine).getPresentationDurationMs() / 1000);
        this.engineState.remainingSeconds = this.totalDurationSeconds;
      } else if (!this.isTachistoscopeMode() && engineConfig.words && Array.isArray(engineConfig.words) && engineConfig.words.length > 0 && engineConfig.intervalMs) {
        const wordCount = engineConfig.words.length;
        const interval = engineConfig.intervalMs;
        // Total duration in seconds (Rounded up)
        this.totalDurationSeconds = Math.ceil((wordCount * interval) / 1000);
        this.engineState.remainingSeconds = this.totalDurationSeconds;
      }
    }

  }

  private shouldTrackReading(): boolean {
    return !!this.sessionId
      && this.sessionId !== 'preview-mode'
      && !this.isPreviewSession()
      && !this.isTachistoscopeMode()
      && [
        'word_highlight',
        'reading_comprehension',
        'skimming',
        'text_fade',
        'text_stream',
        'free_reading',
        'regression_reduction',
        'subvocalization_reduction',
        'adaptive_fluency'
      ].includes(this.engine?.engineType || '');
  }

  private startReadingTracking(onStarted: () => void = () => undefined): void {
    if (!this.shouldTrackReading()) {
      onStarted();
      return;
    }
    if (this.readingTrackingStarted) return;

    this.readingTrackingStarted = true;
    this.sessionService.validateAction(this.sessionId!, {
      action: 'start_reading',
      timestamp: new Date()
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: response => {
        if (!response.isValid) {
          this.failReadingTracking();
          return;
        }
        this.readingTrackingStartCompleted = true;
        onStarted();
        if (this.pendingReadingCompletion) {
          const onFinished = this.pendingReadingCompletion;
          this.pendingReadingCompletion = undefined;
          this.finishReadingTracking(onFinished);
        }
      },
      error: (error) => {
        this.readingTrackingStarted = false;
        this.readingTrackingStartCompleted = false;
        this.failReadingTracking();
      }
    });
  }

  private finishReadingTracking(onFinished: (response?: ValidationResponse) => void, incomplete = false): void {
    if (!this.shouldTrackReading() || !this.readingTrackingStarted || this.readingTrackingFinished) {
      onFinished();
      return;
    }

    if (!this.readingTrackingStartCompleted) {
      this.pendingReadingCompletion = onFinished;
      return;
    }

    this.readingTrackingFinished = true;
    this.sessionService.validateAction(this.sessionId!, {
      action: 'finish_reading',
      isTimeout: incomplete,
      timestamp: new Date()
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: response => response.isValid ? onFinished(response) : this.failReadingTracking(),
      error: (error) => {
        this.failReadingTracking();
      }
    });
  }

  private failReadingTracking(): void {
    this.readingTrackingFinished = false;
    this.pendingReadingCompletion = undefined;
    this.engine?.stop();
    this.stopTimer();
    this.error = 'Okuma süresi doğrulanamadı. Lütfen bağlantınızı kontrol edip egzersizi yeniden açın.';
    this.cdr.detectChanges();
  }

  private enqueueAction(
    action: ActionData,
    onResponse?: (response: ValidationResponse) => void,
    persistFailure = !(this.engine?.engineType === 'visualization' && action.action === 'answer_question')): Promise<void> {
    if (!this.sessionId || this.sessionId === 'preview-mode') {
      return Promise.resolve();
    }

    // Keep one id across retries. If the gateway times out after the server
    // has committed the action, the retry receives the cached idempotent
    // response instead of submitting a second round.
    const actionWithId: ActionData = {
      ...action,
      actionId: action.actionId ?? crypto.randomUUID()
    };
    const actionGeneration = this.actionFailureState.generation;
    const actionSessionId = this.sessionId;
    const validation = this.actionQueue
      .catch(() => undefined)
      .then(() => actionGeneration === this.actionFailureState.generation
        ? this.validateActionWithTransientRetry(actionSessionId, actionWithId)
        : undefined);
    const queued = validation.then(
      response => {
        if (response) {
          runForActionGeneration(
            this.actionFailureState,
            actionGeneration,
            () => onResponse?.(response));
        }
      },
      error => {
        // A failed focus/visualization action must release the engine's
        // pending state; otherwise a transient network error can leave an
        // assessment waiting forever for a response that will never arrive.
        if (onResponse && actionGeneration === this.actionFailureState.generation) {
          try {
            onResponse({
              isValid: false,
              message: this.getActionValidationErrorMessage(error),
              isCompleted: false
            });
          } catch (callbackError) {
            console.error('[ExercisePlayer] Action error callback failed:', callbackError);
          }
        }
        throw error;
      });

    this.actionQueue = queued;
    void queued.catch(error => {
      if (persistFailure) recordActionFailure(this.actionFailureState, actionGeneration, error);
      console.error('[ExercisePlayer] Action validation error:', error);
    });
    return queued;
  }

  private async validateActionWithTransientRetry(
    sessionId: string,
    action: ActionData
  ): Promise<ValidationResponse> {
    let lastError: unknown;
    for (let attempt = 0; attempt < 2; attempt++) {
      try {
        return await firstValueFrom(this.sessionService.validateAction(sessionId, action));
      } catch (error) {
        lastError = error;
        const status = Number((error as any)?.status || (error as any)?.error?.status);
        const isTransient = status === 0 || status === 502 || status === 503 || status === 504;
        if (!isTransient || attempt === 1) break;
        await new Promise(resolve => setTimeout(resolve, 500));
      }
    }
    throw lastError;
  }

  private getActionValidationErrorMessage(error: unknown): string {
    const response = (error as any)?.error;
    const message = response?.message || response?.title || (error as any)?.message;
    return typeof message === 'string' && message.trim()
      ? message
      : 'Egzersiz doğrulaması alınamadı. Lütfen bağlantınızı kontrol edip tekrar deneyin.';
  }

  private waitForPendingActions(onFinished: () => void): void {
    const actionGeneration = this.actionFailureState.generation;
    void finishAfterPendingActions(
      this.actionQueue,
      () => runForActionGeneration(this.actionFailureState, actionGeneration, onFinished),
      error => {
        if (actionGeneration !== this.actionFailureState.generation) return;
        this.engineState = {
          ...this.engineState,
          isRunning: false,
          isCompleted: false
        };
        this.error = this.getActionValidationErrorMessage(error);
        this.showToast('Cevap kaydedilemedi; egzersiz tamamlanmadı.', 'error', 5000);
        this.cdr.detectChanges();
      },
      () => this.actionFailureState);
  }

  private resetActionTracking(): void {
    this.actionQueue = Promise.resolve();
    resetActionFailureState(this.actionFailureState);
  }

  async startExercise(): Promise<void> {
    if (this.isPauseTransitionPending) return;
    if (this.restoredTachistoscopePaused && this.sessionId) {
      this.isPauseTransitionPending = true;
      try {
        await firstValueFrom(this.sessionService.resumeSession(this.sessionId));
        this.restoredTachistoscopePaused = false;
      } catch (error) {
        this.error = this.getActionValidationErrorMessage(error);
        return;
      } finally {
        this.isPauseTransitionPending = false;
        this.cdr.detectChanges();
      }
    }
    window.scrollTo({ top: 0, behavior: 'smooth' });
    this.clickedCells.clear();
    this.correctCells.clear();
    this.wrongCells.clear();

    // Exam Simulation: Skip reading phase, go directly to questions phase
    // Questions include their own paragraph content
    if (this.engine?.engineType === 'exam_simulation' && this.comprehensionQuestions.length > 0) {
      this.exercisePhase = 'questions';
      if (!this.questionAnswers.length) this.currentQuestionIndex = 0;
      this.selectedAnswer = null;
      this.questionFeedback = null;
      this.engineState.isRunning = true;
      if (this.questionAnswers.length >= this.comprehensionQuestions.length) {
        this.finishQuestionPhase();
        return;
      }
      this.startQuestionTimer();
      this.cdr.detectChanges();
      return;
    }

    if (this.engine instanceof MotionPathEngine && this.engine.getMode() === 'tracking' && !this.isPreviewSession()) {
      try {
        let valid = false;
        await this.enqueueAction({ action: 'tracking_start', timestamp: new Date() } as ActionData, response => valid = response.isValid);
        if (!valid) return;
      } catch (error) {
        this.error = this.getActionValidationErrorMessage(error);
        return;
      }
    }
    this.startReadingTracking(() => this.engine?.start());

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
    if (this.engine?.engineType === 'skimming') return;
    if (this.engine?.engineType === 'text_stream' && !this.isTachistoscopeMode()) return;

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

  async togglePause(): Promise<void> {
    if (this.engine?.engineType === 'exam_simulation') {
      this.showToast('Sınav sırasında süre durdurulamaz.', 'info');
      return;
    }
    if (this.isPauseTransitionPending) return;
    if (this.engine?.engineType === 'error_analysis' && (this.engine as ErrorAnalysisEngine).isAwaitingServer()) return;
    if ((this.isTachistoscopeMode() || this.shouldTrackReading() || this.isFixationMode() || this.engine?.engineType === 'error_analysis' || this.engine?.engineType === 'scan_find' || this.engine?.engineType === 'visual_expansion') && this.sessionId && this.sessionId !== 'preview-mode') {
      this.isPauseTransitionPending = true;
      const resuming = this.engineState.isPaused;
      if (!resuming) this.engine?.pause();
      this.tachistoscopeAnswer = '';
      this.tachistoscopeFeedback = null;
      try {
        await this.actionQueue;
        await firstValueFrom(resuming
          ? this.sessionService.resumeSession(this.sessionId)
          : this.sessionService.pauseSession(this.sessionId));
        if (resuming) this.engine?.resume();
      } catch (error) {
        this.engine?.stop();
        this.error = this.getActionValidationErrorMessage(error);
      } finally {
        this.isPauseTransitionPending = false;
        this.cdr.detectChanges();
      }
      return;
    }
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
    this.readingIncomplete = false;
    this.readingTrackingStarted = false;
    this.readingTrackingFinished = false;
    this.readingTrackingStartCompleted = false;
    this.pendingReadingCompletion = undefined;
    this.questionAnswers = [];
    this.adaptiveQuestionHistory = [];
    this.selectedAnswer = null;
    this.questionFeedback = null;
    this.tachistoscopeAnswer = '';
    this.tachistoscopeFeedback = null;
    this.expansionAnswers = [];
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

  async goBack(): Promise<void> {
    // Eğer egzersiz çalışıyorsa, onay iste
    if (this.engineState.isRunning && !this.engineState.isCompleted) {
      if (this.isTachistoscopeMode() || this.shouldTrackReading() || this.isFixationMode() || this.engine?.engineType === 'scan_find' || this.engine?.engineType === 'visual_expansion') {
        if (this.isPauseTransitionPending) return;
        if (!this.engineState.isPaused) await this.togglePause();
      } else this.engine?.pause();
      this.showExitConfirm = true;
    } else {
      this.navigateBack();
    }
  }

  async cancelExit(): Promise<void> {
    if (this.isPauseTransitionPending) return;
    this.showExitConfirm = false;
    if (this.isTachistoscopeMode() || this.shouldTrackReading() || this.isFixationMode() || this.engine?.engineType === 'scan_find' || this.engine?.engineType === 'visual_expansion') {
      if (this.engineState.isPaused) await this.togglePause();
    } else this.engine?.resume();
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
      this.router.navigate(['/student/assessment'], {
        queryParams: this.assessmentPhase ? { phase: this.assessmentPhase } : {}
      });
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
    return q.Content || q.content || q.Paragraph || q.paragraph || q.Text || q.text
      || (this.engine?.engineType === 'exam_simulation' ? this.getComprehensionText() : '');
  }

  // Helper to get target WPM from config
  getTargetWpm(): number {
    if (this.engine?.engineType === 'skimming') return 0;
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
    if (this.questionFeedback || this.questionSubmissionPending) return; // Already answered or being saved

    this.stopQuestionTimer();
    this.selectedAnswer = option;
    this.submitQuestionAnswer(option, false);
  }

  private submitQuestionAnswer(answer: string, isTimeout: boolean): void {
    const question = this.getCurrentQuestion();
    if (!question || this.questionFeedback || this.questionSubmissionPending) return;

    const questionId = question.QuestionId || question.questionId || question.Id || question.id;
    if (!questionId) {
      this.showToast('Soru kimliği bulunamadı; cevap kaydedilemedi.', 'error');
      return;
    }

    const targetTime = this.getQuestionTimeLimit(question);
    const timeSpent = isTimeout
      ? targetTime
      : Math.max(0, targetTime - this.questionTimeRemaining);
    this.questionSubmissionPending = true;

    const action: ActionData = {
      action: 'answer_question',
      questionId,
      answer,
      isTimeout,
      responseTime: Math.max(0, Math.round(timeSpent * 1000)),
      timestamp: new Date()
    };

    const applyResponse = (response: ValidationResponse): void => {
      if (!response.isValid) {
        throw new Error(response.message || 'Cevap kaydedilemedi.');
      }

      const isCorrect = response.isCorrect === true;
      const serverExamFeedback = this.engine?.engineType === 'exam_simulation' ? response.feedbackData : null;
      const answerTimedOut = isTimeout || serverExamFeedback?.timedOut === true;
      const answerTimeSpent = typeof serverExamFeedback?.responseTimeSeconds === 'number' ? serverExamFeedback.responseTimeSeconds : timeSpent;
      this.questionFeedback = {
        isCorrect: this.isAssessmentMode ? null : response.isCorrect ?? false,
        correctAnswer: this.isAssessmentMode ? undefined : response.correctAnswer,
        explanation: this.isAssessmentMode
          ? (isTimeout ? 'Süre doldu; cevap kaydedildi.' : 'Cevabınız kaydedildi.')
          : (response.explanation || response.message)
      };
      if (!this.questionAnswers.some(item => item.questionId === questionId)) {
        this.questionAnswers.push({
          questionId,
          selectedAnswer: answerTimedOut ? '' : answer,
          isCorrect,
          timeSpent: answerTimeSpent,
          targetTime,
          questionText: question.QuestionText || question.questionText || question.Text || question.text,
          correctAnswer: this.isAssessmentMode ? undefined : response.correctAnswer
        });
      }
      this.questionSubmissionPending = false;
      this.cdr.detectChanges();
    };

    if (this.sessionId === 'preview-mode') {
      const correctAnswer = this.getQuestionCorrectAnswer(question);
      applyResponse({
        isValid: true,
        message: isTimeout ? 'Süre doldu!' : 'Cevap değerlendirildi.',
        isCompleted: false,
        isCorrect: !isTimeout && answer === correctAnswer,
        correctAnswer,
        explanation: question.Explanation || question.explanation
      });
      return;
    }

    if (!this.sessionId) {
      this.questionSubmissionPending = false;
      this.showToast('Oturum bulunamadı; cevap kaydedilemedi.', 'error');
      return;
    }

    this.enqueueAction(action, applyResponse).catch(error => {
      this.questionSubmissionPending = false;
      this.selectedAnswer = null;
      this.showToast(
        error?.message || 'Cevap sunucuya kaydedilemedi; lütfen tekrar deneyin.',
        'error');
      this.cdr.detectChanges();
    });
  }

  private getQuestionCorrectAnswer(question: any): string | undefined {
    let correctAnswer = question.CorrectAnswer || question.correctAnswer;
    if (correctAnswer) {
      return String(correctAnswer).toUpperCase();
    }

    const correctOption = question.CorrectOption ?? question.correctOption;
    if (typeof correctOption === 'string') {
      return correctOption.toUpperCase();
    }
    if (typeof correctOption === 'number' && correctOption >= 1) {
      return String.fromCharCode(64 + correctOption);
    }
    return undefined;
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
  private getQuestionTimeLimit(question: any): number {
    if (this.engine?.engineType === 'exam_simulation') {
      const seconds = this.backendSessionConfig?.examQuestionTimeSeconds
        ?? this.parsedConfig?.['engineConfig']?.['timing']?.questionTimeSeconds
        ?? this.parsedConfig?.['timing']?.questionTimeSeconds ?? 60;
      return typeof seconds === 'number' && Number.isFinite(seconds) ? Math.min(3600, Math.max(1, Math.round(seconds))) : 60;
    }
    return question.TargetTimeSeconds || question.targetTimeSeconds || 60;
  }

  async startQuestionTimer(): Promise<void> {
    this.stopQuestionTimer();
    const generation = this.questionTimerGeneration;
    const question = this.getCurrentQuestion();
    if (!question) return;
    if (this.engine?.engineType === 'exam_simulation' && !this.isPreviewSession()) {
      this.questionSubmissionPending = true;
      try {
        let valid = false;
        await this.enqueueAction({ action: 'exam_question_start', questionId: question.QuestionId || question.questionId || question.Id || question.id,
          timestamp: new Date() } as ActionData, response => {
            valid = response.isValid;
            if (valid && typeof response.feedbackData?.remainingSeconds === 'number')
              question.examRemainingSeconds = response.feedbackData.remainingSeconds;
          });
        if (!valid || generation !== this.questionTimerGeneration || question !== this.getCurrentQuestion()) return;
      } catch (error) {
        this.error = this.getActionValidationErrorMessage(error);
        return;
      } finally {
        this.questionSubmissionPending = false;
        this.cdr.detectChanges();
      }
    }
    const targetTime = this.getQuestionTimeLimit(question);
    const remaining = this.engine?.engineType === 'exam_simulation' && typeof question.examRemainingSeconds === 'number'
      ? Math.max(0, Math.min(targetTime, question.examRemainingSeconds)) : targetTime;
    this.questionTimeRemaining = remaining;
    this.questionStartTime = Date.now() - (targetTime - remaining) * 1000;

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
    this.questionTimerGeneration = (this.questionTimerGeneration || 0) + 1;
    if (this.questionTimerInterval) {
      clearInterval(this.questionTimerInterval);
      this.questionTimerInterval = null;
    }
  }

  handleQuestionTimeout(): void {
    this.stopQuestionTimer();
    const question = this.getCurrentQuestion();
    if (!question || this.questionFeedback || this.questionSubmissionPending) return;

    // Mark as wrong (no answer given)
    this.selectedAnswer = null;
    this.submitQuestionAnswer('', true);
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
    if (this.engine?.engineType === 'adaptive_fluency') {
      this.adaptiveQuestionHistory.push(...this.questionAnswers);
      this.advanceAdaptiveStage();
      return;
    }
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
      totalTime: this.engine?.engineType === 'exam_simulation' ? totalTimeSpent * 1000 : this.engineState.timeElapsed,
      totalSteps: totalQuestions,
      completedSteps: totalQuestions,
      errors: totalQuestions - correctCount,
      details: {
        wpm: this.readingIncomplete || ['skimming', 'exam_simulation'].includes(this.engine?.engineType || '') ? null : this.readingWpm,
        ...(this.engine?.engineType === 'skimming' ? { inspectionTimeMs: this.engineState.timeElapsed } : {}),
        timedOut: this.readingIncomplete,
        targetWpm: this.getTargetWpm(),
        comprehensionScore: comprehensionAccuracy,
        correctAnswers: correctCount,
        totalQuestions: totalQuestions,
        performanceLevel: this.engine?.engineType === 'exam_simulation' ? 'Süreli paragraf değerlendirmesi'
          : this.engine?.engineType === 'skimming' ? 'Ana fikir değerlendirmesi'
          : this.readingIncomplete ? 'Okuma tamamlanmadı' : this.getPerformanceLevel(this.readingWpm, comprehensionAccuracy),
        // Time statistics
        totalTimeSpent,
        averageTimePerQuestion,
        totalTargetTime,
        timeoutCount,
        // Detailed answers with timing
        answers: this.questionAnswers
      }
    };

    this.result = this.normalizeEngineResultForDisplay(this.result);
    this.exercisePhase = 'completed';
    this.engineState.isCompleted = true;
    this.saveResult(this.result);
    this.cdr.detectChanges();

  }

  private getPerformanceLevel(wpm: number, comprehension: number): string {
    if (comprehension >= 80 && wpm >= 300) return 'Mükemmel!';
    if (comprehension >= 70 && wpm >= 250) return 'Çok İyi!';
    if (comprehension >= 60 && wpm >= 200) return 'İyi!';
    if (comprehension >= 50) return 'Gelişiyor';
    return 'Pratik Gerekli';
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
    if (this.engine?.engineType === 'skimming')
      return Math.max(0, (this.engine as SkimmingEngine).getMaximumMs() - this.engineState.timeElapsed);
    if (this.engine?.engineType === 'grid_interaction') {
      return Math.max(0, (this.schulteSettings.timeLimit ?? 0) * 1000 - this.engineState.timeElapsed);
    }
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
    return (this.engine as TextStreamEngine).getDisplayPaceWpm();
  }

  // --- Regression Reduction Helpers ---
  getRegressionWords(): string[] {
    return (this.engine as RegressionReductionEngine)?.getWords?.() || [];
  }

  getRegressionActiveIndex(): number {
    return (this.engine as RegressionReductionEngine)?.getCurrentWordIndex?.() ?? -1;
  }

  getRegressionChunkSize(): number {
    return (this.engine as RegressionReductionEngine)?.getChunkSize?.() || 1;
  }

  getRegressionWpm(): number {
    return (this.engine as RegressionReductionEngine)?.getDisplayPaceWpm?.() || 0;
  }

  getRegressionBackwardClickCount(): number {
    return (this.engine as RegressionReductionEngine)?.getBackwardClickCount?.() || 0;
  }

  isWordInActiveChunk(wordIndex: number): boolean {
    const activeIndex = this.getRegressionActiveIndex();
    if (activeIndex < 0) return false;
    const startChunkIndex = (this.engine as RegressionReductionEngine).getCurrentChunkStart();
    return wordIndex >= startChunkIndex && wordIndex <= activeIndex;
  }

  isWordInTrailingMask(wordIndex: number): boolean {
    const activeIndex = this.getRegressionActiveIndex();
    if (activeIndex < 0) return false;
    const startChunkIndex = (this.engine as RegressionReductionEngine).getCurrentChunkStart();
    return wordIndex < startChunkIndex;
  }

  getRegressionPhase(): string {
    return (this.engine as RegressionReductionEngine)?.getPhase?.() || 'reading';
  }

  getRegressionMaskingType(): string {
    return (this.engine as RegressionReductionEngine)?.getMaskingType?.() || 'none';
  }

  onRegressionWordClick(index: number): void {
    this.engine?.handleInput({ type: 'regression', wordIndex: index });
  }

  submitRegressionAnswer(option: string): void {
    this.submitPacedReadingAnswer(option, this.getRegressionCurrentQuestion(), this.getRegressionPhase());
  }

  private submitPacedReadingAnswer(option: string, question: any, phase: string): void {
    if (!question || this.questionSubmissionPending || this.questionFeedback
      || !this.engineState.isRunning || this.engineState.isPaused
      || phase !== 'answering' || !['A', 'B', 'C', 'D'].includes(option)) return;
    if (this.sessionId === 'preview-mode') {
      this.selectedAnswer = option;
      this.questionFeedback = { question, isCorrect: null };
      this.engine?.handleInput({ type: 'answer', answer: option, previewOnly: true });
      this.cdr.detectChanges();
      return;
    }
    if (!this.sessionId) {
      this.showToast('Cevabı doğrulamak için geçerli bir oturum gerekir.', 'error');
      return;
    }
    const questionId = question.QuestionId || question.questionId || question.Id || question.id;
    if (!questionId) {
      this.showToast('Soru kimliği bulunamadı; cevap kaydedilemedi.', 'error');
      return;
    }
    this.questionSubmissionPending = true;
    this.selectedAnswer = option;
    void this.enqueueAction({ action: 'answer_question', questionId, answer: option, timestamp: new Date() }, response => {
      if (!response.isValid || typeof response.isCorrect !== 'boolean') throw new Error(response.message || 'Cevap kaydedilemedi.');
      const correctAnswer = this.isAssessmentMode ? undefined : response.correctAnswer;
      this.questionFeedback = { question, isCorrect: this.isAssessmentMode ? null : response.isCorrect ?? null,
        correctAnswer, explanation: this.isAssessmentMode ? undefined : response.explanation };
      if (!this.questionAnswers.some(answer => answer.questionId === questionId)) {
        this.questionAnswers.push({ questionId, selectedAnswer: option, isCorrect: response.isCorrect === true,
          timeSpent: 0, targetTime: 0, questionText: question.QuestionText || question.questionText, correctAnswer });
      }
      this.questionSubmissionPending = false;
      this.engine?.handleInput({ type: 'answer', answer: option, serverValidated: true,
        isCorrect: response.isCorrect, correctAnswer });
      this.cdr.detectChanges();
    }, this.engine?.engineType !== 'subvocalization_reduction').catch(error => {
      this.questionSubmissionPending = false;
      this.showToast(error?.message || 'Cevap kaydedilemedi; lütfen yeniden deneyin.', 'error');
      this.cdr.detectChanges();
    });
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

  isSubvocMetronomeEnabled(): boolean {
    return (this.engine as SubvocalizationReductionEngine)?.isMetronomeEnabled?.() || false;
  }

  submitSubvocAnswer(answer: string): void {
    this.submitPacedReadingAnswer(answer, this.getSubvocCurrentQuestion(), this.getSubvocPhase());
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
    if (this.questionSubmissionPending) return;
    this.selectedAnswer = null;
    this.questionFeedback = null;
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

  getVisualizationScenePercent(): number {
    return (this.engine as VisualizationEngine)?.getSceneDisplayPercent?.() || 0;
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

  isVisualizationAnswerPending(): boolean {
    return (this.engine as VisualizationEngine)?.isAnswerPending?.() || false;
  }

  isVisualizationAnswerEvaluated(): boolean {
    return (this.engine as VisualizationEngine)?.isAnswerEvaluated?.() || false;
  }

  getVisualizationCorrectAnswer(): string {
    return (this.engine as VisualizationEngine)?.correctAnswer || '';
  }

  nextVisualizationQuestion(): void {
    (this.engine as VisualizationEngine)?.nextQuestion?.();
    this.cdr.detectChanges();
  }

  hasTimeLimit(): boolean {
    if (this.engine?.engineType === 'skimming') return true;
    if (this.engine?.engineType === 'grid_interaction') return this.schulteSettings.timeLimit !== undefined;
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
  isFixationTargetVisible(): boolean { return (this.engine as MotionPathEngine)?.isFixationTargetVisible() ?? false; }
  getMotionPathFixationDuration(): number { return (this.engine as any)?.getFixationDuration() || 0; }

  trackPeripheralChar(index: number, char: any): string {
    return char.position; // Use position (top/bottom/left/right) as unique identifier
  }

  isFixationMode(): boolean {
    return this.engine?.engineType === 'motion_path'
      ? (this.engine as MotionPathEngine).getMode() === 'fixation'
      : false;
  }

  isSaccadeMode(): boolean {
    return this.engine?.engineType === 'motion_path'
      && (this.engine as MotionPathEngine).getMode() === 'saccade';
  }

  getTrackingTarget(): { type: string; color?: string } | null {
    return this.engine instanceof MotionPathEngine && this.engine.getMode() === 'tracking'
      ? this.engine.getTargetConfig() : null;
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
    const value = input.value.toUpperCase().replace(/[^A-Z]/g, '');

    // Update the model value (force uppercase)
    this.peripheralInputValue = value;
    input.value = value;

    this.engine.handleInput({ type: 'text', value });

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

    // Submit to engine
    (this.engine as TextStreamEngine).handleInput({ answer });

    // Server-backed feedback arrives through onStepComplete, not the submitted input.
    this.tachistoscopeAnswer = '';
    this.cdr.detectChanges();
  }

  isTachistoscopeMode(): boolean {
    return this.engine?.engineType === 'text_stream'
      && (this.exercise?.exerciseTypeName?.toLowerCase() === 'tachistoscope'
        || (this.engine as TextStreamEngine).getMode() !== 'rsvp');
  }

  private refreshTachistoscopeFeedback(): void {

    // Get last trial result for feedback
    const lastTrial = (this.engine as TextStreamEngine).getLastTrialResult?.();
    if (lastTrial) {
      this.tachistoscopeFeedback = {
        isCorrect: lastTrial.isCorrect,
        correctAnswer: lastTrial.stimulus
      };

    }

    this.cdr.detectChanges();
  }

  isShowingTachistoscopeFeedback(): boolean {
    return !!this.tachistoscopeFeedback && !this.isAssessmentMode
      && !this.isShowingFixation() && !this.isShowingStimulus() && !this.isWaitingForAnswer();
  }

  getTachistoscopeCorrectCount(): number {
    return (this.engine as TextStreamEngine)?.getCorrectCount?.() || 0;
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

  getHighlightColor(): string {
    return this.engine?.engineType === 'word_highlight'
      ? (this.engine as WordHighlightEngine).getHighlightColor() : '#fef08a';
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

    // Uyaran (karakterler) ekrandayken cevap alanı gösterilmemeli
    if (isVisible) return false;

    // İlk kez true olduğunda focus'u tetikle
    if (isWaiting && !this.wasWaitingForInput) {
      this.focusExpansionInput();
    }
    this.wasWaitingForInput = isWaiting;

    return isWaiting;
  }

  getExpansionRelativeDistance(): number {
    return (this.engine as VisualExpansionEngine)?.getRelativeDistancePercent?.() || 0;
  }

  getExpansionStimulusSize(): string {
    return (this.engine as VisualExpansionEngine)?.getStimulusSizeCss?.() || '2rem';
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

  getExpansionAnswerSlots(): number[] {
    const count = (this.engine as VisualExpansionEngine)?.getExpectedAnswerCount?.() || 0;
    return Array.from({ length: count }, (_, index) => index);
  }

  getExpansionAnswerLabel(index: number): string {
    const pattern = (this.engine as VisualExpansionEngine)?.getPattern?.();
    const labels = pattern === 'radial' ? ['Üst sol', 'Üst sağ', 'Alt sol', 'Alt sağ']
      : pattern === 'vertical' ? ['Üst', 'Alt'] : ['Sol', 'Sağ'];
    return labels[index] || `Uyaran ${index + 1}`;
  }

  getExpansionAnswerMaxLength(index: number): number {
    return Math.max(1, (this.engine as VisualExpansionEngine)?.getLastShownStimuli?.()[index]?.length || 1);
  }

  canSubmitExpansionAnswer(): boolean {
    const engine = this.engine as VisualExpansionEngine;
    const count = engine?.getExpectedAnswerCount?.() || 0;
    return !!engine?.isWaitingForInput && !engine.state.isPaused && count > 0
      && this.expansionAnswers.length === count
      && this.getExpansionAnswerSlots().every(index => !!this.expansionAnswers[index]?.trim());
  }

  submitExpansionAnswer(): void {
    if (this.engine?.engineType !== 'visual_expansion' || !this.canSubmitExpansionAnswer()) return;
    this.engine.handleInput({ answers: this.expansionAnswers.map(answer => answer.trim()) });
    this.expansionAnswers = [];
    this.cdr.detectChanges();
  }

  focusExpansionInput(): void {
    setTimeout(() => this.visualExpansionArea?.nativeElement.querySelector<HTMLInputElement>('input')?.focus(), 100);
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
      return (this.engine as TextStreamEngine).getDisplayPaceWpm();
    }
    return 0;
  }

  getTextFadeFontSize(): string {
    return (this.engine as TextFadeEngine)?.getFontSizeCss?.() || '20px';
  }

  private isMeasuredClientResult(result: EngineResult): boolean {
    if (this.engine?.engineType === 'visualization') {
      return result.details?.measurementStatus === 'Measured';
    }
    if (['regression_reduction', 'subvocalization_reduction'].includes(this.engine?.engineType || ''))
      return result.details?.measurementStatus === 'Measured' && result.details?.comprehensionScore != null;
    if (this.engine?.engineType === 'motion_path' && result.details?.serverValidatedFixation)
      return true;
    if (this.questionAnswers.length > 0
      || result.details?.totalQuestions > 0
      || result.details?.totalAnswers > 0
      || result.details?.correctAnswers > 0
      || result.details?.trials?.length > 0
      || result.details?.answers?.length > 0) {
      return true;
    }

    const observationOnlyEngines = [
      'motion_path',
      'text_fade',
      'word_highlight',
      'text_stream',
      'regression_reduction',
      'subvocalization_reduction',
      'chunking',
      'rsvp',
      'speed_reading',
      'free_reading',
      'skimming'
    ];
    return !observationOnlyEngines.includes(this.engine?.engineType || '');
  }

  isPacedReadingEngine(): boolean {
    return ['word_highlight', 'text_fade', 'text_stream'].includes(this.engine?.engineType || '');
  }

  getReadingMeasurementMessage(): string {
    return this.result?.details?.comprehensionScore == null
      ? 'Anlama ölçülmedi. Gösterilen tempo, metnin gösterim temposudur; ölçülmüş okuma hızı değildir.'
      : 'Anlama sorularla ölçüldü. Gösterilen tempo, metnin gösterim temposudur; ölçülmüş okuma hızı değildir.';
  }

  private normalizeEngineResultForDisplay(result: EngineResult): EngineResult {
    const paced = this.isPacedReadingEngine();
    const details = { ...(result.details || {}), ...(this.engine?.engineType === 'skimming' ? { wpm: null } : {}), ...(paced ? {
      wpm: null, displayPaceWpm: this.getWpm(),
      completionPercent: result.totalSteps > 0 ? Math.round(result.completedSteps / result.totalSteps * 100) : 0,
      incomplete: result.details?.timedOut === true
    } : {}) };
    if (this.isMeasuredClientResult(result)) {
      return {
        ...result,
        details: { ...details, measurementStatus: 'Measured' }
      };
    }

    return {
      ...result,
      score: 0,
      accuracy: 0,
      details: {
        ...details,
        wpm: null,
        speedScore: null,
        comprehensionScore: null,
        measurementStatus: 'NotMeasured'
      }
    };
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
    // Preview mode is intentionally local-only for every preview role.
    if (this.isPreviewSession() || this.sessionId === 'preview-mode') {
      this.resultSaveStatus = 'preview';
      this.showToast('Önizleme modu - Sonuçlar kaydedilmedi.', 'info', 3000);
      return;
    }

    if (!this.sessionId) {
      console.warn('No active session, cannot save result securely.');
      this.showToast('Oturum bulunamadı, sonuç kaydedilemedi.', 'error', 3000);
      return;
    }

    this.resultSaveStatus = 'saving';

    const isMeasured = this.isMeasuredClientResult(result);
    const customData = {
      wordsRead: this.engineState.totalSteps,
      errors: result.errors,
      engineType: this.engine?.engineType,
      ...this.parsedConfig,
      details: result.details,
      wpm: isMeasured ? this.getWpm() : null,
      accuracy: isMeasured ? result.accuracy : null,
      score: isMeasured ? result.score : null,
      measurementStatus: isMeasured ? 'Measured' : 'NotMeasured'
    };


    // 2. Günlük egzersiz ilerlemesini kaydet (pratik modu değilse)
    const state = history.state;
    const isPracticeMode = state?.practiceMode === true;
    const isAssessmentMode = this.isAssessmentMode;
    const completionQuestionAnswers = this.engine?.engineType === 'visualization'
      ? undefined
      : this.questionAnswers;

    // 1. Session kaydet (gamification için) - Assessment modunda XP kazanımı backend tarafında engellenir
    this.sessionService.completeSession(this.sessionId, toCompleteSessionRequest(
      completionQuestionAnswers,
      customData,
      isAssessmentMode
    )).subscribe({
      next: (sessionResult: SessionResult) => {
        this.resultSaveStatus = 'saved';
        this.sessionResult = sessionResult;
        const tachistoscope = this.isTachistoscopeMode() ? sessionResult.detailedResults?.tachistoscope : undefined;
        const trials: Array<{ responseTimeMs: number }> = tachistoscope?.trials || [];
        this.result = {
          ...(this.result || result),
          score: sessionResult.score ?? 0,
          accuracy: sessionResult.accuracy ?? 0,
          details: {
            ...((this.result || result).details || {}),
            ...(this.engine?.engineType === 'error_analysis' && sessionResult.detailedResults?.errorAnalysisFound ? {
              totalErrors: sessionResult.detailedResults.totalSteps,
              foundErrors: sessionResult.detailedResults.errorAnalysisFound.length,
              missedErrors: sessionResult.detailedResults.totalSteps - sessionResult.detailedResults.errorAnalysisFound.length,
              falseAlarms: sessionResult.detailedResults.errorAnalysisFalseAlarms?.length ?? 0,
              hintUsedCount: sessionResult.detailedResults.errorAnalysisHints ?? 0,
              assisted: (sessionResult.detailedResults.errorAnalysisHints ?? 0) > 0
            } : {}),
            ...(tachistoscope ? {
              trials,
              correctCount: sessionResult.correctCount,
              incorrectCount: sessionResult.incorrectCount,
              initialDurationMs: tachistoscope.initialDurationMs,
              finalDurationMs: tachistoscope.displayDurationMs,
              avgResponseTime: trials.length ? Math.round(trials.reduce((sum, trial) => sum + trial.responseTimeMs, 0) / trials.length) : null
            } : {}),
            wpm: sessionResult.rawWPM ?? null,
            comprehensionScore: sessionResult.comprehensionScore ?? null,
            weightedKDP: sessionResult.weightedKDP ?? null,
            measurementStatus: sessionResult.measurementStatus,
            ...(this.engine?.engineType === 'skimming' ? {
              inspectionTimeMs: sessionResult.detailedResults?.skimmingInspectionMs,
              incomplete: sessionResult.detailedResults?.readingIncomplete === true,
              timedOut: sessionResult.detailedResults?.readingIncomplete === true
            } : {}),
            ...(sessionResult.detailedResults?.groupingDisplayPaceWpm > 0 ? {
              displayPaceWpm: sessionResult.detailedResults.groupingDisplayPaceWpm,
              completionPercent: sessionResult.detailedResults.groupingCompletionPercent,
              incomplete: sessionResult.detailedResults.readingIncomplete === true
            } : {}),
            ...(sessionResult.detailedResults?.fadeDisplayPaceWpm > 0 ? {
              displayPaceWpm: sessionResult.detailedResults.fadeDisplayPaceWpm,
              completionPercent: sessionResult.detailedResults.fadeCompletionPercent,
              incomplete: sessionResult.detailedResults.readingIncomplete === true
            } : {}),
            ...(sessionResult.detailedResults?.rsvpDisplayPaceWpm > 0 ? {
              displayPaceWpm: sessionResult.detailedResults.rsvpDisplayPaceWpm,
              completionPercent: sessionResult.detailedResults.rsvpCompletionPercent,
              rsvpPresentedWords: sessionResult.detailedResults.rsvpPresentedWords,
              incomplete: sessionResult.detailedResults.readingIncomplete === true
            } : {}),
            ...(this.isFixationMode() && sessionResult.detailedResults?.fixationRoundResults?.length ? {
              fixationResults: sessionResult.detailedResults.fixationRoundResults,
              averageResponseTimeMs: Math.round(sessionResult.detailedResults.fixationRoundResults.reduce(
                (sum: number, round: { responseTimeMs: number }) => sum + round.responseTimeMs, 0) / sessionResult.detailedResults.fixationRoundResults.length)
            } : {}),
            ...(this.engine?.engineType === 'visual_expansion' && sessionResult.detailedResults?.visualExpansionRoundResults ? {
              maxDegreesReached: sessionResult.detailedResults.visualExpansionMaxPresentedDistance,
              averageResponseTimeMs: sessionResult.detailedResults.visualExpansionAverageResponseTimeMs,
              roundResults: sessionResult.detailedResults.visualExpansionRoundResults
            } : {})
          }
        };
        this.cdr.detectChanges();

        let msg = 'Sonuç kaydedildi.';
        if (sessionResult.xpGained > 0) {
          msg += ` +${sessionResult.xpGained} XP kazandınız!`;
        }
        if (sessionResult.leveledUp) {
          msg += ` 🆙 Seviye Atladınız!`;
        }

        if (sessionResult.unlockedBadges && sessionResult.unlockedBadges.length > 0) {
          msg += ` Yeni rozet kazandınız!`;
        }

        this.showToast(msg, 'info', 5000);

        if (this.reviewItemId) {
          this.submitReviewResult(sessionResult.sessionId);
        }

        const incomplete = this.result?.details?.incomplete === true || this.readingIncomplete === true;
        if (!incomplete && !isAssessmentMode && this.pathItemId && this.sessionId) {
          this.learningPathService.completePersonalizedPathItem(this.pathItemId, this.sessionId)
            .subscribe({
              error: (error) => {
                console.error('Learning path completion failed:', error);
                this.showToast('Öğrenme yolu ilerlemesi kaydedilemedi. Lütfen tekrar deneyin.', 'error', 5000);
              }
            });
        } else if (!incomplete && !isAssessmentMode && !isPracticeMode && !this.reviewItemId && this.exercise?.id) {
          this.completeDailyProgress(result, customData, isMeasured);
        }
      },
      error: (err) => {
        this.resultSaveStatus = 'failed';
        console.error('Session completion failed:', err);
        this.showToast('Sonuç kaydedilemedi. Sonuç ekranından tekrar deneyebilirsiniz.', 'error', 5000);
        this.cdr.detectChanges();
      }
    });



  }

  retrySaveResult(): void {
    if (this.result && this.resultSaveStatus === 'failed') {
      this.saveResult(this.result);
    }
  }

  private isPreviewSession(): boolean {
    return this.authService.canPreviewExercises()
      && !this.staffTrainingMode
      && !this.authService.hasRole('Student');
  }

  private submitReviewResult(sessionId: string): void {
    if (!this.reviewItemId) return;
    this.reviewSaveStatus = 'saving';
    this.reviewService.submitReview(this.reviewItemId, sessionId).subscribe({
      next: () => {
        this.reviewSaveStatus = 'saved';
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Review submission failed:', error);
        this.reviewSaveStatus = 'failed';
        this.showToast('Egzersiz kaydedildi fakat tekrar planı güncellenemedi. Tekrar deneyin.', 'error', 5000);
        this.cdr.detectChanges();
      }
    });
  }

  retryReviewSubmission(): void {
    if (this.reviewSaveStatus === 'failed' && this.sessionResult?.sessionId) {
      this.submitReviewResult(this.sessionResult.sessionId);
    }
  }

  private completeDailyProgress(
    result: EngineResult,
    customData: { [key: string]: any },
    isMeasured: boolean): void {
    if (!this.exercise?.id || !this.sessionId) return;

    const completeRequest: CompleteExerciseRequest = {
      exerciseId: this.exercise.id,
      sessionId: this.sessionId,
      successRate: isMeasured ? result.accuracy ?? undefined : undefined,
      timeSpentSeconds: Math.max(1, Math.round(result.totalTime / 1000)),
      measurementStatus: isMeasured ? 'Measured' : 'NotMeasured',
      correctCount: isMeasured ? result.completedSteps || 0 : 0,
      incorrectCount: isMeasured ? result.errors || 0 : 0,
      totalAttempts: isMeasured ? result.totalSteps || 0 : 0,
      averageResponseTimeMs: result.details?.averageResponseTimeMs || 0,
      medianResponseTimeMs: result.details?.medianResponseTimeMs || 0,
      stdDevResponseTimeMs: result.details?.stdDevResponseTimeMs || 0,
      pauseCount: result.details?.pauseCount || 0,
      totalPausedSeconds: result.details?.totalPausedSeconds || 0,
      resultDataJson: JSON.stringify(customData)
    };

    this.pendingDailyProgressRequest = completeRequest;
    this.submitDailyProgress(completeRequest);
  }

  private submitDailyProgress(completeRequest: CompleteExerciseRequest): void {
    this.dailyProgressSaveStatus = 'saving';
    this.exerciseProgramService.completeExercise(completeRequest, completeRequest.sessionId).subscribe({
      next: (response: any) => {
        this.dailyProgressSaveStatus = 'saved';
        this.pendingDailyProgressRequest = null;
        this.cdr.detectChanges();
        if (response.programCompleted) {
          console.log('🎉 Program Completed!', response);
          this.programCompletionData = response;
          this.showProgramCompletionModal = true;
          this.cdr.detectChanges();
          return;
        }

        if (response.dayCompleted) {
          setTimeout(() => {
            this.showToast('Bugünün tüm egzersizlerini tamamladınız!', 'info', 5000);
          }, 2000);
        }

        if (response.adaptationUpdated) {
          setTimeout(() => {
            this.showToast('Son ölçümlerinize göre egzersiz seviyeniz ve öğrenme yolunuz güncellendi.', 'info', 5000);
          }, 2000);
        }
      },
      error: (err) => {
        console.error('[ExercisePlayer] Daily progress update failed:', err);
        this.dailyProgressSaveStatus = 'failed';
        this.showToast('Sonuç kaydedildi ancak günlük ilerleme güncellenemedi. Tekrar deneyin.', 'error', 5000);
        this.cdr.detectChanges();
      }
    });
  }

  // Reading Comprehension helpers
  private readingScrollProgress = 0;

  isManualReadingEngine(): boolean {
    return ['reading_comprehension', 'free_reading', 'exam_simulation', 'adaptive_fluency', 'skimming'].includes(this.engine?.engineType || '');
  }

  getComprehensionText(): string {
    if (this.isManualReadingEngine()) {
      return (this.engine as any).getText?.() || '';
    }
    return '';
  }

  getComprehensionWordCount(): number {
    if (this.isManualReadingEngine()) {
      return (this.engine as any).getWordCount?.() || 0;
    }
    return 0;
  }

  getComprehensionFontSize(): string {
    if (this.isManualReadingEngine()) {
      return (this.engine as any).getFontSize?.() || 'medium';
    }
    return 'medium';
  }

  getCurrentReadingWpm(): number {
    if (this.engine?.engineType === 'reading_comprehension' || this.engine?.engineType === 'free_reading' || this.engine?.engineType === 'exam_simulation') return 0;
    if (this.engine?.engineType === 'reading_comprehension' || this.engine?.engineType === 'exam_simulation' || this.engine?.engineType === 'adaptive_fluency') {
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

  getComprehensionLineHeight(): number {
    return (this.engine as ReadingComprehensionEngine)?.getLineHeight?.() || 1.8;
  }

  canCompleteReading(): boolean {
    if (!this.engineState.isRunning || this.engineState.isPaused) return false;
    return (this.engine as ReadingComprehensionEngine)?.canComplete?.() ?? true;
  }

  onReadingScroll(event: Event): void {
    const element = event.target as HTMLElement;
    if (element) {
      const scrollPercent = (element.scrollTop / (element.scrollHeight - element.clientHeight)) * 100;
      this.readingScrollProgress = Math.min(100, Math.max(0, scrollPercent));

      // Notify engine about scroll progress
      if (this.isManualReadingEngine()) {
        this.engine?.handleInput({ scrollProgress: this.readingScrollProgress });
      }
    }
  }

  completeReading(): void {
    if (this.isManualReadingEngine()) {
      (this.engine as any).completeReading();
    }
  }

  getAdaptivePurpose(): string {
    return this.engine?.engineType === 'adaptive_fluency'
      ? (this.engine as AdaptiveFluencyEngine).getPurpose()
      : '';
  }

  getAdaptiveStageLabel(): string {
    if (this.engine?.engineType !== 'adaptive_fluency') return '';
    return ['Başlangıç ölçümü', 'Amaçlı tekrar 1', 'Amaçlı tekrar 2', 'Yeni metin aktarım testi']
      [(this.engine as AdaptiveFluencyEngine).getStage()] || '';
  }

  private handleAdaptiveReadingCompleted(result: EngineResult): void {
    if (this.sessionId === 'preview-mode') {
      const engine = this.engine as AdaptiveFluencyEngine;
      this.readingWpm = Number(result.details?.wpm ?? 0);
      const questions = engine.getQuestions();
      if (questions.length > 0) {
        this.comprehensionQuestions = questions;
        this.currentQuestionIndex = 0;
        this.questionAnswers = [];
        this.selectedAnswer = null;
        this.questionFeedback = null;
        this.exercisePhase = 'questions';
        this.startQuestionTimer();
        this.cdr.detectChanges();
      } else {
        this.advanceAdaptiveStage();
      }
      return;
    }
    this.waitForPendingActions(() => this.finishReadingTracking(response => {
      if (!response?.isValid) {
        this.showToast(response?.message || 'Okuma aşaması doğrulanamadı.', 'error');
        this.readingTrackingFinished = false;
        const engine = this.engine as AdaptiveFluencyEngine;
        engine.reset();
        engine.start();
        return;
      }
      const engine = this.engine as AdaptiveFluencyEngine;
      const questions = engine.getQuestions();
      this.readingWpm = Number(response.currentWPM ?? result.details?.wpm ?? 0);
      if (questions.length > 0) {
        this.comprehensionQuestions = questions;
        this.currentQuestionIndex = 0;
        this.questionAnswers = [];
        this.selectedAnswer = null;
        this.questionFeedback = null;
        this.exercisePhase = 'questions';
        this.startQuestionTimer();
        this.cdr.detectChanges();
        return;
      }
      this.advanceAdaptiveStage();
    }));
  }

  private advanceAdaptiveStage(): void {
    if (this.sessionId === 'preview-mode') {
      const engine = this.engine as AdaptiveFluencyEngine;
      if (engine.getStage() === 3) {
        const correct = this.questionAnswers.filter(answer => answer.isCorrect).length;
        const total = this.questionAnswers.length;
        const comprehension = total ? Math.round(correct / total * 100) : 0;
        this.result = {
          score: comprehension,
          accuracy: comprehension,
          totalTime: this.engineState.timeElapsed,
          totalSteps: 4,
          completedSteps: 4,
          errors: total - correct,
          details: { wpm: this.readingWpm, comprehensionScore: comprehension,
            performanceLevel: 'Önizleme tamamlandı', answers: [...this.adaptiveQuestionHistory] }
        };
        this.exercisePhase = 'completed';
        this.engineState.isCompleted = true;
        this.saveResult(this.result);
      } else {
        engine.applyStage({ stage: engine.getStage() + 1 });
        this.questionAnswers = [];
        this.selectedAnswer = null;
        this.questionFeedback = null;
        this.readingTrackingStarted = false;
        this.readingTrackingStartCompleted = false;
        this.readingTrackingFinished = false;
        this.exercisePhase = 'reading';
        engine.start();
      }
      this.cdr.detectChanges();
      return;
    }
    if (!this.sessionId) return;
    this.enqueueAction({ action: 'adaptive_next_stage', timestamp: new Date() } as ActionData, response => {
      if (!response.isValid) {
        this.showToast(response.message || 'Sonraki aşamaya geçilemedi.', 'error');
        return;
      }
      const feedback = (response.feedbackData || {}) as AdaptiveFluencyStageFeedback;
      if (feedback.completed) {
        const stages = (response.feedbackData as any)?.stageResults || [];
        const baseline = stages.find((item: any) => item.stage === 0);
        const transfer = stages.find((item: any) => item.stage === 3);
        const comprehension = Number(feedback.transferComprehension ?? 0);
        this.result = {
          score: comprehension,
          accuracy: comprehension,
          totalTime: stages.reduce((sum: number, item: any) => sum + Number(item.readingSeconds || 0) * 1000, 0),
          totalSteps: 4,
          completedSteps: 4,
          errors: this.questionAnswers.filter(answer => !answer.isCorrect).length,
          details: {
            wpm: transfer?.wpm,
            baselineWpm: baseline?.wpm,
            comprehensionScore: comprehension,
            baselineComprehension: feedback.baselineComprehension,
            transferGainPercent: feedback.transferGainPercent,
            performanceLevel: feedback.transferGainPercent == null ? 'Anlama eşiği korunamadı' : 'Aktarım ölçüldü',
            answers: [...this.adaptiveQuestionHistory]
          }
        };
        this.exercisePhase = 'completed';
        this.engineState.isCompleted = true;
        this.saveResult(this.result);
        this.cdr.detectChanges();
        return;
      }

      const engine = this.engine as AdaptiveFluencyEngine;
      engine.applyStage(feedback);
      this.comprehensionQuestions = engine.getQuestions();
      this.questionAnswers = [];
      this.selectedAnswer = null;
      this.questionFeedback = null;
      this.readingTrackingStarted = false;
      this.readingTrackingStartCompleted = false;
      this.readingTrackingFinished = false;
      this.exercisePhase = 'reading';
      this.startReadingTracking(() => engine.start());
      this.cdr.detectChanges();
    }).catch(error => this.showToast(error?.message || 'Sonraki aşamaya geçilemedi.', 'error'));
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
      return (this.engine as ScanFindEngine).isTargetFound(target);
    }
    return false;
  }

  getScanFontSize(): number {
    return this.engine?.engineType === 'scan_find' ? (this.engine as ScanFindEngine).getFontSizePx() : 20;
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
  isVocabAwaitingPersistence(): boolean {
    return this.engine?.engineType === 'vocabulary_builder'
      && (this.engine as VocabularyBuilderEngine).isAwaitingPersistence();
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
  getErrorAnalysisFontSize(): string {
    return this.engine?.engineType === 'error_analysis'
      ? (this.engine as any).getFontSize?.() || 'medium' : 'medium';
  }

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
  getErrorAnalysisReview() {
    return this.engine?.engineType === 'error_analysis' ? (this.engine as ErrorAnalysisEngine).getErrors() : [];
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
      (this.engine as ErrorAnalysisEngine).useHint();
      this.cdr.detectChanges();
    }
  }

  isErrorWordHint(index: number): boolean {
    return this.engine?.engineType === 'error_analysis' && (this.engine as ErrorAnalysisEngine).getHintIndex() === index;
  }
  isErrorAnalysisPending(): boolean {
    return this.engine?.engineType === 'error_analysis' && (this.engine as ErrorAnalysisEngine).isAwaitingServer();
  }
  hasErrorAnalysisFailure(): boolean {
    return this.engine?.engineType === 'error_analysis' && (this.engine as ErrorAnalysisEngine).hasFailedAction();
  }
  retryErrorAnalysisAction(): void {
    if (this.engine?.engineType === 'error_analysis') (this.engine as ErrorAnalysisEngine).retryServerAction();
  }
}
