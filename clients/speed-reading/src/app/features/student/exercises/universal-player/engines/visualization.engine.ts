/**
 * Visualization Engine
 * Görselleştirme egzersizi için frontend engine.
 * 
 * Akış:
 * 1. Sahne açıklaması gösterilir (süre sınırlı)
 * 2. Kullanıcı sahneyi zihninde görselleştirir
 * 3. Sorular gösterilir
 * 4. Sonraki sahneye geçilir
 * 
 * RecallTime: Sahne gösteriminden ilk soruya cevaba kadar geçen süre
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';
import { boundedInteger, boundedStringArray, boundedText, caseInsensitiveField, recordOrEmpty } from './reading-pacer-safety';

interface VisualizationScene {
    sceneId: string;
    description: string;
    imageUrl?: string;
    duration: number; // seconds
    displayOrder: number;
    questions: VisualizationQuestion[];
    steps?: string[];     // For guided mode
    stepDurationMs?: number; // For manual or auto advance
}

interface VisualizationQuestion {
    questionId: string;
    questionText: string;
    options: string[];
    correctAnswer?: string;
    questionType: string;
    hintText?: string;
}

interface VisualizationConfig extends EngineConfig {
    scenes?: VisualizationScene[];
    Scenes?: VisualizationScene[];
    mode?: 'static' | 'guided' | 'flash';
}

type Phase = 'scene' | 'questions' | 'completed';

export class VisualizationEngine implements BaseEngine {
    engineType = 'visualization' as const;
    displayName = 'Görselleştirme';

    private config!: VisualizationConfig;
    private callbacks!: EngineCallbacks;
    state!: EngineState;

    private scenes: VisualizationScene[] = [];
    private currentSceneIndex = 0;
    private currentQuestionIndex = 0;
    private phase: Phase = 'scene';
    public mode: 'static' | 'guided' | 'flash' = 'static'; // Public for template access

    private stepDeadline = 0;
    private stepRemainingMs = 0;
    private sceneDeadline = 0;
    private questionStartedAtMs = 0;
    private startedAtMs = 0;
    private pausedAtMs: number | null = null;
    private pausedMilliseconds = 0;
    private questionAnswers: { questionId: string; answer: string; isCorrect: boolean | null; }[] = [];

    private timerInterval: any;
    private sceneTimeout: any;
    private sceneDisplayRemaining = 0;
    private sceneDisplayTotal = 0;

    getSceneDisplayPercent(): number {
        return this.sceneDisplayTotal > 0
            ? Math.min(100, Math.max(0, this.sceneDisplayRemaining / this.sceneDisplayTotal * 100)) : 0;
    }
    private serverAuthoritative = false;
    private previewOnly = false;
    private pendingServerAnswer: { questionId: string; answer: string; sceneId: string } | null = null;
    private answerEvaluated = false;

    // Guided Mode
    private currentGuidedStepIndex = 0;
    private guidedStepTimer: any;

    // Question Feedback State
    public showingFeedback = false;
    public lastAnswer = '';
    public lastAnswerCorrect = false;
    public correctAnswer = '';

    initialize(config: VisualizationConfig, callbacks: EngineCallbacks): void {
        this.cleanup();
        this.callbacks = callbacks;
        const root = recordOrEmpty(config);
        const nested = recordOrEmpty(caseInsensitiveField(root, 'engineConfig'));
        const sessionData = recordOrEmpty(caseInsensitiveField(root, 'sessionData'));
        const read = (name: string) => caseInsensitiveField(sessionData, name)
            ?? caseInsensitiveField(nested, name)
            ?? caseInsensitiveField(root, name);
        this.config = { ...root, ...nested, ...sessionData } as VisualizationConfig;
        const mode = read('mode');
        this.mode = ['static', 'guided', 'flash'].includes(mode) ? mode : 'static';
        this.previewOnly = root['previewOnly'] === true;
        this.serverAuthoritative = !this.previewOnly;

        // Get scenes from config (try both cases)
        const configuredScenes = read('scenes');
        const rawScenes = Array.isArray(configuredScenes) ? configuredScenes.slice(0, 100) : [];

        // Map PascalCase to camelCase
        this.scenes = rawScenes.map((s: any) => ({
            sceneId: boundedText(caseInsensitiveField(recordOrEmpty(s), 'sceneId'), '', 100),
            description: boundedText(caseInsensitiveField(recordOrEmpty(s), 'description'), '', 10_000),
            imageUrl: boundedText(caseInsensitiveField(recordOrEmpty(s), 'imageUrl'), '', 2_000),
            duration: boundedInteger(caseInsensitiveField(recordOrEmpty(s), 'duration'), 5, 1, 3_600),
            displayOrder: boundedInteger(caseInsensitiveField(recordOrEmpty(s), 'displayOrder'), 0, 0, 10_000),
            steps: boundedStringArray(caseInsensitiveField(recordOrEmpty(s), 'steps'), 100, 2_000),
            stepDurationMs: boundedInteger(caseInsensitiveField(recordOrEmpty(s), 'stepDurationMs'), 3000, 100, 60_000),
            questions: (Array.isArray(caseInsensitiveField(recordOrEmpty(s), 'questions'))
                ? caseInsensitiveField(recordOrEmpty(s), 'questions').slice(0, 100) : []).map((q: any) => ({
                questionId: q.questionId || q.QuestionId,
                questionText: q.questionText || q.QuestionText,
                options: q.options || q.Options || [],
                correctAnswer: q.correctAnswer || q.CorrectAnswer || '',
                questionType: q.questionType || q.QuestionType,
                hintText: q.hintText || q.HintText
            }))
        }));

        const totalQuestions = this.scenes.reduce((sum, s) => sum + (s.questions?.length || 0), 0);

        this.state = {
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            totalSteps: totalQuestions,
            score: 0,
            accuracy: 100,
            timeElapsed: 0,
            errors: 0
        };

        this.currentSceneIndex = 0;
        this.currentQuestionIndex = 0;
        this.phase = 'scene';
        this.questionAnswers = [];
        this.pendingServerAnswer = null;
        this.answerEvaluated = false;
        this.showingFeedback = false;
        this.lastAnswer = '';
        this.lastAnswerCorrect = false;
        this.correctAnswer = '';

    }

    start(): void {
        if (this.state.isRunning || this.state.isCompleted) return;
        if (this.scenes.length === 0) {
            this.callbacks.onError('Görselleştirme için uygun sahne bulunamadı. Lütfen başka bir egzersiz seçin.');
            return;
        }

        this.state.isRunning = true;
        this.state.isPaused = false;
        this.state.isCompleted = false;
        this.state.timeElapsed = 0;
        this.startedAtMs = Date.now();
        this.pausedAtMs = null;
        this.pausedMilliseconds = 0;

        // Start global timer
        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused && this.state.isRunning) {
                this.state.timeElapsed = this.activeElapsedMs();

                // Update scene countdown
                if (this.phase === 'scene' && this.sceneDisplayRemaining > 0) {
                    this.sceneDisplayRemaining = Math.max(0, this.sceneDeadline - Date.now());
                }

                this.callbacks.onStateChange({
                    ...this.state,
                    sceneDisplayRemaining: this.sceneDisplayRemaining,
                    phase: this.phase,
                    currentSceneIndex: this.currentSceneIndex,
                    currentQuestionIndex: this.currentQuestionIndex,
                    // Pass guided step info explicitly if needed, or rely on template getter
                } as any);
            }
        }, 100);

        this.callbacks.onStart();
        this.startScene();
    }

    private startScene(): void {
        if (this.currentSceneIndex >= this.scenes.length) {
            this.complete();
            return;
        }

        this.phase = 'scene';
        this.currentQuestionIndex = 0;

        const scene = this.scenes[this.currentSceneIndex];
        this.sceneDisplayRemaining = scene.duration * 1000;

        if (this.mode === 'guided' && scene.steps && scene.steps.length > 0) {
            this.sceneDisplayRemaining = scene.steps.length * (scene.stepDurationMs || 3000);
            // Guided Mode Logic
            this.currentGuidedStepIndex = 0;
            this.startGuidedSteps(scene);
        } else {
            this.sceneDeadline = Date.now() + this.sceneDisplayRemaining;
            // Static/Flash Mode Logic
            // Auto-transition to questions after scene duration
            this.sceneTimeout = setTimeout(() => {
                if (this.state.isRunning && !this.state.isPaused) {
                    this.endSceneDisplay();
                }
            }, scene.duration * 1000);
        }

        this.sceneDisplayTotal = this.sceneDisplayRemaining;
        this.sceneDeadline = Date.now() + this.sceneDisplayRemaining;
        this.callbacks.onStateChange({
            ...this.state,
            phase: 'scene',
            currentSceneIndex: this.currentSceneIndex,
            sceneDisplayRemaining: this.sceneDisplayRemaining
        } as any);

    }

    private startGuidedSteps(scene: VisualizationScene): void {
        // Initial Step
        this.updateGuidedStepState();

        // Start Timer for Steps
        const stepDuration = scene.stepDurationMs || 3000;

        // Clear any existing step timer
        if (this.guidedStepTimer) clearTimeout(this.guidedStepTimer);

        // Function to advance steps
        const advanceStep = () => {
            if (!this.state.isRunning || this.state.isPaused) return;

            this.currentGuidedStepIndex++;
            if (this.currentGuidedStepIndex >= (scene.steps?.length || 0)) {
                // All steps done, finish scene display
                this.endSceneDisplay();
            } else {
                // Next step
                this.updateGuidedStepState();
                this.stepDeadline = Date.now() + stepDuration;
                this.guidedStepTimer = setTimeout(advanceStep, stepDuration);
            }
        };

        // Start first timeout
        this.stepDeadline = Date.now() + stepDuration;
        this.guidedStepTimer = setTimeout(advanceStep, stepDuration);
    }

    private updateGuidedStepState(): void {
        // Just trigger a state change so template updates
        this.callbacks.onStateChange({
            ...this.state,
            phase: 'scene',
            currentSceneIndex: this.currentSceneIndex,
            // Add custom data for template if needed, or rely on engine getter
        } as any);
    }

    // Public getter for template
    getGuidedStepText(): string {
        const scene = this.getCurrentScene();
        if (this.mode === 'guided' && scene && scene.steps && this.currentGuidedStepIndex < scene.steps.length) {
            return scene.steps[this.currentGuidedStepIndex];
        }
        return scene?.description || '';
    }

    private endSceneDisplay(): void {
        this.phase = 'questions';
        this.questionStartedAtMs = this.activeElapsedMs();

        if (this.sceneTimeout) {
            clearTimeout(this.sceneTimeout);
            this.sceneTimeout = null;
        }

        const scene = this.scenes[this.currentSceneIndex];
        if (this.guidedStepTimer) clearTimeout(this.guidedStepTimer);

        if (scene.questions.length === 0) {
            this.currentSceneIndex++;
            if (this.currentSceneIndex >= this.scenes.length) this.complete();
            else this.startScene();
            return;
        }

        this.callbacks.onStateChange({
            ...this.state,
            phase: 'questions',
            currentSceneIndex: this.currentSceneIndex,
            currentQuestionIndex: this.currentQuestionIndex
        } as any);

    }

    pause(): void {
        if (!this.state.isRunning || this.state.isPaused) return;
        this.state.timeElapsed = this.activeElapsedMs();
        this.pausedAtMs = Date.now();
        this.stepRemainingMs = Math.max(0, this.stepDeadline - Date.now());
        if (this.phase === 'scene') {
            this.sceneDisplayRemaining = Math.max(0, this.sceneDeadline - Date.now());
        }
        this.state.isPaused = true;
        if (this.sceneTimeout) clearTimeout(this.sceneTimeout);
        if (this.guidedStepTimer) clearTimeout(this.guidedStepTimer);
        this.callbacks.onPause();
        this.callbacks.onStateChange({ ...this.state });
    }

    resume(): void {
        if (!this.state.isRunning || !this.state.isPaused) return;
        this.pausedMilliseconds += Date.now() - (this.pausedAtMs ?? Date.now());
        this.pausedAtMs = null;
        this.state.isPaused = false;

        // Resume scene timer if in scene phase
        if (this.phase === 'scene') {
            this.sceneDeadline = Date.now() + this.sceneDisplayRemaining;
            if (this.mode === 'guided' && this.getCurrentScene()?.steps?.length) {
                // Resume guided steps (simple restart of current step duration for now)
                const scene = this.getCurrentScene();
                if (scene) {
                    const stepDuration = scene.stepDurationMs || 3000;
                    this.stepDeadline = Date.now() + this.stepRemainingMs;
                    this.guidedStepTimer = setTimeout(() => this.continueGuidedSteps(scene, stepDuration), this.stepRemainingMs);
                }
            } else if (this.sceneDisplayRemaining > 0) {
                this.sceneDeadline = Date.now() + this.sceneDisplayRemaining;
                this.sceneTimeout = setTimeout(() => {
                    if (this.state.isRunning && !this.state.isPaused) {
                        this.endSceneDisplay();
                    }
                }, this.sceneDisplayRemaining);
            }
        }

        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    // Helper to continue steps after resume
    private continueGuidedSteps(scene: VisualizationScene, stepDuration: number): void {
        if (!this.state.isRunning || this.state.isPaused) return;

        this.currentGuidedStepIndex++;
        if (this.currentGuidedStepIndex >= (scene.steps?.length || 0)) {
            this.endSceneDisplay();
        } else {
            this.updateGuidedStepState();
            this.stepDeadline = Date.now() + stepDuration;
            this.guidedStepTimer = setTimeout(() => this.continueGuidedSteps(scene, stepDuration), stepDuration);
        }
    }

    stop(): void {
        this.cleanup();
        this.state.isRunning = false;
        this.callbacks.onStateChange({ ...this.state });
    }

    reset(): void {
        this.cleanup();
        this.currentSceneIndex = 0;
        this.currentQuestionIndex = 0;
        this.phase = 'scene';
        this.questionAnswers = [];
        this.pendingServerAnswer = null;
        this.answerEvaluated = false;
        this.showingFeedback = false;
        this.lastAnswer = '';
        this.lastAnswerCorrect = false;
        this.correctAnswer = '';
        this.state = {
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            totalSteps: this.scenes.reduce((sum, s) => sum + s.questions.length, 0),
            score: 0,
            accuracy: 100,
            timeElapsed: 0,
            errors: 0
        };
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.cleanup();
    }

    private cleanup(): void {
        if (this.timerInterval) clearInterval(this.timerInterval);
        if (this.sceneTimeout) clearTimeout(this.sceneTimeout);
        if (this.guidedStepTimer) clearTimeout(this.guidedStepTimer);
        this.timerInterval = null;
        this.sceneTimeout = null;
        this.guidedStepTimer = null;
    }

    private activeElapsedMs(): number {
        return Math.max(0, (this.pausedAtMs ?? Date.now()) - this.startedAtMs - this.pausedMilliseconds);
    }

    handleInput(input: any): void {
        if (!input || typeof input !== 'object' || !this.state.isRunning || this.state.isPaused || this.state.isCompleted) return;

        // Skip scene display early
        if (input.action === 'skip_scene' && this.phase === 'scene') {
            this.endSceneDisplay();
            return;
        }

        // Answer question
        if (input.type === 'answer' && this.phase === 'questions') {
            this.answerQuestion(input.answer);
        }
    }

    private answerQuestion(answer: string): void {
        if (this.showingFeedback) return; // Prevent double answers

        const scene = this.scenes[this.currentSceneIndex];
        const question = scene.questions[this.currentQuestionIndex];

        if (this.previewOnly) {
            this.showingFeedback = true;
            this.answerEvaluated = false;
            this.lastAnswer = answer;
            this.lastAnswerCorrect = false;
            this.correctAnswer = '';
            this.state.currentStep++;
            this.callbacks.onStateChange({ ...this.state });
            return;
        }

        if (this.serverAuthoritative) {
            this.pendingServerAnswer = {
                questionId: question.questionId,
                answer,
                sceneId: scene.sceneId
            };
            this.showingFeedback = true;
            this.answerEvaluated = false;
            this.lastAnswer = answer;
            this.lastAnswerCorrect = false;
            this.correctAnswer = '';
            this.callbacks.onAction({
                action: 'answer_question',
                questionId: question.questionId,
                answer: this.toOptionLetter(question, answer),
                responseTime: Math.max(0, this.activeElapsedMs() - this.questionStartedAtMs),
                customData: { sceneId: scene.sceneId },
                timestamp: new Date()
            });
            this.callbacks.onStateChange({
                ...this.state,
                phase: 'questions',
                currentSceneIndex: this.currentSceneIndex,
                currentQuestionIndex: this.currentQuestionIndex
            } as any);
            return;
        }

    }

    private toOptionLetter(question: VisualizationQuestion, answer: string): string {
        const optionIndex = question.options.indexOf(answer);
        return optionIndex >= 0 && optionIndex < 4
            ? ['A', 'B', 'C', 'D'][optionIndex]
            : answer;
    }

    // Called when user clicks "Next Question" button
    nextQuestion(): void {
        if (!this.state.isRunning || this.state.isPaused || !this.showingFeedback || this.pendingServerAnswer) return;

        this.showingFeedback = false;
        this.lastAnswer = '';

        const scene = this.scenes[this.currentSceneIndex];

        // Move to next question or next scene
        this.currentQuestionIndex++;
        if (this.currentQuestionIndex >= scene.questions.length) {
            // All questions for this scene done, move to next scene
            this.currentSceneIndex++;
            if (this.currentSceneIndex >= this.scenes.length) {
                this.complete();
            } else {
                this.startScene();
            }
        } else {
            this.questionStartedAtMs = this.activeElapsedMs();
            this.callbacks.onStateChange({
                ...this.state,
                phase: 'questions',
                currentSceneIndex: this.currentSceneIndex,
                currentQuestionIndex: this.currentQuestionIndex
            } as any);
        }
    }

    /** Applies an accepted answer from the authoritative session endpoint. */
    applyServerResponse(response: any): void {
        if (!this.state.isRunning || this.state.isCompleted) return;
        const pending = this.pendingServerAnswer;
        if (!pending) return;

        if (response?.isValid !== true || (typeof response.isCorrect !== 'boolean' && response.isCorrect !== null)) {
            this.pendingServerAnswer = null;
            this.showingFeedback = false;
            this.lastAnswer = '';
            this.correctAnswer = '';
            this.answerEvaluated = false;
            this.callbacks.onStateChange({
                ...this.state,
                phase: 'questions',
                currentSceneIndex: this.currentSceneIndex,
                currentQuestionIndex: this.currentQuestionIndex
            } as any);
            return;
        }

        const isAssessment = response?.isCorrect === null || response?.isCorrect === undefined;
        const isCorrect = isAssessment ? null : response.isCorrect === true;
        this.lastAnswerCorrect = isCorrect === true;
        this.answerEvaluated = !isAssessment;
        const answerKey = String(response?.correctAnswer || '').trim().toUpperCase();
        const optionIndex = ['A', 'B', 'C', 'D'].indexOf(answerKey);
        this.correctAnswer = isAssessment ? '' : this.getCurrentQuestion()?.options[optionIndex] || String(response?.correctAnswer || '');
        this.questionAnswers.push({
            questionId: pending.questionId,
            answer: pending.answer,
            isCorrect
        });
        if (isCorrect) {
            this.state.score++;
        } else if (!isAssessment) {
            this.state.errors++;
        }
        this.state.currentStep++;
        this.pendingServerAnswer = null;
        this.callbacks.onStepComplete(this.state.currentStep, isCorrect === true);
        this.callbacks.onStateChange({
            ...this.state,
            phase: 'questions',
            currentSceneIndex: this.currentSceneIndex,
            currentQuestionIndex: this.currentQuestionIndex
        } as any);
    }

    isAnswerPending(): boolean {
        return this.pendingServerAnswer !== null;
    }

    isAnswerEvaluated(): boolean {
        return this.answerEvaluated;
    }

    private complete(): void {
        if (this.state.isCompleted) return;
        this.state.timeElapsed = this.activeElapsedMs();
        this.cleanup();
        this.state.isRunning = false;
        this.state.isCompleted = true;
        this.phase = 'completed';

        const totalQuestions = this.scenes.reduce((sum, s) => sum + s.questions.length, 0);
        const accuracy = totalQuestions > 0 ? (this.state.score / totalQuestions) * 100 : 0;
        this.state.accuracy = Math.round(accuracy);

        const result: EngineResult = {
            score: this.state.score,
            accuracy: this.state.accuracy,
            totalTime: this.state.timeElapsed,
            totalSteps: totalQuestions,
            completedSteps: totalQuestions,
            errors: this.state.errors,
            details: {
                measurementStatus: !this.previewOnly && this.questionAnswers.length > 0
                    && this.questionAnswers.length === totalQuestions
                    && this.questionAnswers.every(answer => typeof answer.isCorrect === 'boolean') ? 'Measured' : 'NotMeasured',
                scenesCompleted: this.scenes.length,
                answers: this.questionAnswers
            }
        };

        this.callbacks.onComplete(result);
        this.callbacks.onStateChange({
            ...this.state,
            phase: 'completed'
        } as any);

    }

    // Public getters for template
    getCurrentScene(): VisualizationScene | null {
        if (this.currentSceneIndex < this.scenes.length) {
            return this.scenes[this.currentSceneIndex];
        }
        return null;
    }

    getCurrentQuestion(): VisualizationQuestion | null {
        const scene = this.getCurrentScene();
        if (scene && this.currentQuestionIndex < scene.questions.length) {
            return scene.questions[this.currentQuestionIndex];
        }
        return null;
    }

    getPhase(): Phase {
        return this.phase;
    }

    getSceneProgress(): { current: number; total: number } {
        return {
            current: this.currentSceneIndex + 1,
            total: this.scenes.length
        };
    }

    getQuestionProgress(): { current: number; total: number } {
        const scene = this.getCurrentScene();
        return {
            current: this.currentQuestionIndex + 1,
            total: scene?.questions.length || 0
        };
    }

    getSceneDisplayRemaining(): number {
        return Math.max(0, Math.ceil(this.sceneDisplayRemaining / 1000));
    }
}
