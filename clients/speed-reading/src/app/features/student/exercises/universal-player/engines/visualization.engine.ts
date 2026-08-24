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
    correctAnswer: string;
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

    private sceneStartTime = 0;
    private sceneEndTime = 0;
    private questionAnswers: { questionId: string; answer: string; isCorrect: boolean; }[] = [];

    private timerInterval: any;
    private sceneTimeout: any;
    private sceneDisplayRemaining = 0;

    // Guided Mode
    private currentGuidedStepIndex = 0;
    private guidedStepTimer: any;

    // Question Feedback State
    public showingFeedback = false;
    public lastAnswer = '';
    public lastAnswerCorrect = false;
    public correctAnswer = '';

    initialize(config: VisualizationConfig, callbacks: EngineCallbacks): void {
        this.config = config;
        this.callbacks = callbacks;
        this.mode = config.mode || 'static';

        // Get scenes from config (try both cases)
        const rawScenes = config.scenes || config.Scenes || [];

        // Map PascalCase to camelCase
        this.scenes = rawScenes.map((s: any) => ({
            sceneId: s.sceneId || s.SceneId,
            description: s.description || s.Description,
            imageUrl: s.imageUrl || s.ImageUrl,
            duration: s.duration || s.Duration,
            displayOrder: s.displayOrder || s.DisplayOrder,
            steps: s.steps || s.Steps || [],
            stepDurationMs: s.stepDurationMs || s.StepDurationMs || 3000,
            questions: (s.questions || s.Questions || []).map((q: any) => ({
                questionId: q.questionId || q.QuestionId,
                questionText: q.questionText || q.QuestionText,
                options: q.options || q.Options || [],
                correctAnswer: q.correctAnswer || q.CorrectAnswer,
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

    }

    start(): void {
        if (this.scenes.length === 0) {
            console.error('[VisualizationEngine] No scenes to display');
            return;
        }

        this.state.isRunning = true;
        this.state.isPaused = false;
        this.state.isCompleted = false;
        this.state.timeElapsed = 0;

        // Start global timer
        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused && this.state.isRunning) {
                this.state.timeElapsed += 100;

                // Update scene countdown
                if (this.phase === 'scene' && this.sceneDisplayRemaining > 0) {
                    this.sceneDisplayRemaining -= 100;
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
        this.sceneStartTime = Date.now();
        this.currentQuestionIndex = 0;

        const scene = this.scenes[this.currentSceneIndex];
        this.sceneDisplayRemaining = scene.duration * 1000;

        if (this.mode === 'guided' && scene.steps && scene.steps.length > 0) {
            // Guided Mode Logic
            this.currentGuidedStepIndex = 0;
            this.startGuidedSteps(scene);
        } else {
            // Static/Flash Mode Logic
            // Auto-transition to questions after scene duration
            this.sceneTimeout = setTimeout(() => {
                if (this.state.isRunning && !this.state.isPaused) {
                    this.endSceneDisplay();
                }
            }, scene.duration * 1000);
        }

        this.callbacks.onStateChange({
            ...this.state,
            phase: 'scene',
            currentSceneIndex: this.currentSceneIndex,
            sceneDisplayRemaining: this.sceneDisplayRemaining
        } as any);

        // Notify backend
        this.callbacks.onAction({
            action: 'scene_viewed',
            timestamp: new Date()
        });

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
                this.guidedStepTimer = setTimeout(advanceStep, stepDuration);
            }
        };

        // Start first timeout
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
        return '';
    }

    private endSceneDisplay(): void {
        this.sceneEndTime = Date.now();
        this.phase = 'questions';

        if (this.sceneTimeout) {
            clearTimeout(this.sceneTimeout);
            this.sceneTimeout = null;
        }

        const scene = this.scenes[this.currentSceneIndex];
        const recallTime = (this.sceneEndTime - this.sceneStartTime) / 1000;

        // Send scene_completed with RecallTime to backend
        this.callbacks.onAction({
            action: 'scene_completed',
            customData: {
                sceneId: scene.sceneId,
                recallTimeSeconds: recallTime
            },
            timestamp: new Date()
        });

        this.callbacks.onStateChange({
            ...this.state,
            phase: 'questions',
            currentSceneIndex: this.currentSceneIndex,
            currentQuestionIndex: this.currentQuestionIndex
        } as any);

    }

    pause(): void {
        this.state.isPaused = true;
        if (this.sceneTimeout) clearTimeout(this.sceneTimeout);
        if (this.guidedStepTimer) clearTimeout(this.guidedStepTimer);
        this.callbacks.onPause();
        this.callbacks.onStateChange({ ...this.state });
    }

    resume(): void {
        this.state.isPaused = false;

        // Resume scene timer if in scene phase
        if (this.phase === 'scene') {
            if (this.mode === 'guided') {
                // Resume guided steps (simple restart of current step duration for now)
                const scene = this.getCurrentScene();
                if (scene) {
                    const stepDuration = scene.stepDurationMs || 3000;
                    // Note: Ideally we should track remaining time, but restarting step is acceptable
                    this.guidedStepTimer = setTimeout(() => this.continueGuidedSteps(scene, stepDuration), stepDuration);
                }
            } else if (this.sceneDisplayRemaining > 0) {
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

    handleInput(input: any): void {
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

        const isCorrect = question.correctAnswer.toLowerCase() === answer.toLowerCase();

        // Store feedback state
        this.showingFeedback = true;
        this.lastAnswer = answer;
        this.lastAnswerCorrect = isCorrect;
        this.correctAnswer = question.correctAnswer;

        this.questionAnswers.push({
            questionId: question.questionId,
            answer: answer,
            isCorrect: isCorrect
        });

        if (isCorrect) {
            this.state.score++;
        } else {
            this.state.errors++;
        }
        this.state.currentStep++;

        // Notify backend
        this.callbacks.onAction({
            action: 'answer_question',
            questionId: question.questionId,
            answer: answer,
            customData: {
                sceneId: scene.sceneId,
                isCorrect: isCorrect,
                correctAnswer: question.correctAnswer
            },
            timestamp: new Date()
        });

        this.callbacks.onStepComplete(this.state.currentStep, isCorrect);
        this.callbacks.onStateChange({
            ...this.state,
            phase: 'questions',
            currentSceneIndex: this.currentSceneIndex,
            currentQuestionIndex: this.currentQuestionIndex
        } as any);
    }

    // Called when user clicks "Next Question" button
    nextQuestion(): void {
        if (!this.showingFeedback) return;

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
            this.callbacks.onStateChange({
                ...this.state,
                phase: 'questions',
                currentSceneIndex: this.currentSceneIndex,
                currentQuestionIndex: this.currentQuestionIndex
            } as any);
        }
    }

    private complete(): void {
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
