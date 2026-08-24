/**
 * Regression Reduction Engine
 * Focuses on preventing backward eye movements (regressions).
 * Implements "Trailing Mask" or "Fade Out" scientific paradigms.
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';

export interface RegressionConfig extends EngineConfig {
    mode: string;
    wpm: number;
    maskingType: 'none' | 'fade' | 'trailing' | 'contingent' | 'ior';
    maskingEnabled: boolean;
    wordDelayMs?: number;
    chunkSize?: number; // Kelime grubu boyutu (1, 2, veya 3)
    ReadingTextContent?: string;
    Questions?: any[];
}

export class RegressionReductionEngine implements BaseEngine {
    readonly engineType = 'regression_reduction';
    readonly displayName = 'Regresyon Azaltma';

    state: EngineState = {
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

    private config!: RegressionConfig;
    private callbacks!: EngineCallbacks;
    private startTime = 0;
    private pauseStartTime = 0;
    private timerInterval: any;
    private pacerInterval: any;

    private words: string[] = [];
    private currentWordIndex = -1;
    private phase: 'reading' | 'answering' = 'reading';

    // Questions Related
    private questions: any[] = [];
    private currentQuestionIndex = 0;
    private answers: any[] = [];

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.config = config as RegressionConfig;
        this.callbacks = callbacks;

        const backend = config as any;
        const sessionData = backend.SessionData || backend;

        // Load content
        const text = sessionData.ReadingTextContent || sessionData.readingTextContent || "";
        this.words = text.split(/\s+/).filter((w: string) => w.length > 0);
        this.questions = sessionData.Questions || sessionData.questions || [];

        this.state.totalSteps = this.words.length + this.questions.length;
        this.state.currentStep = 0;
        this.phase = 'reading';
        this.currentWordIndex = -1;


    }

    start(): void {
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.startTime = Date.now();
        this.callbacks.onStart();

        // Start Global Timer
        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;
                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        if (this.phase === 'reading') {
            this.startPacer();
        }
    }

    private startPacer(): void {
        const chunkSize = this.config.chunkSize || 1;

        // Calculate milliseconds per chunk
        // If specific wordDelayMs provided by backend (e.g. for masking), use it
        // Otherwise calculate from WPM
        let msPerChunk: number;
        if (this.config.wordDelayMs && this.config.wordDelayMs > 0) {
            msPerChunk = this.config.wordDelayMs * chunkSize;
        } else {
            const wpm = this.config.wpm || 200;
            msPerChunk = (60000 / wpm) * chunkSize;
        }



        this.pacerInterval = setInterval(() => {
            if (!this.state.isPaused && this.phase === 'reading') {
                this.advanceChunk();
            }
        }, msPerChunk);
    }

    private advanceChunk(): void {
        const chunkSize = this.config.chunkSize || 1;

        // chunkSize kadar kelime ilerle
        this.currentWordIndex += chunkSize;
        this.state.currentStep = Math.min(this.currentWordIndex + 1, this.words.length);

        if (this.currentWordIndex >= this.words.length) {
            this.finishReading();
            return;
        }

        this.callbacks.onStepComplete(this.state.currentStep, true);
        this.callbacks.onStateChange({ ...this.state });
    }

    private finishReading(): void {
        clearInterval(this.pacerInterval);
        this.phase = 'answering';
        this.currentQuestionIndex = 0;

        // Notify backend that reading is finished
        this.callbacks.onAction({
            action: 'finish_reading',
            timeMs: this.state.timeElapsed,
            timestamp: new Date()
        });

        this.callbacks.onStateChange({ ...this.state });
    }

    pause(): void {
        if (this.state.isPaused) return;
        this.state.isPaused = true;
        this.pauseStartTime = Date.now();
        this.callbacks.onPause();
        this.callbacks.onStateChange({ ...this.state });
    }

    resume(): void {
        if (!this.state.isPaused) return;

        // Adjust startTime to account for pause duration
        const pauseDuration = Date.now() - this.pauseStartTime;
        this.startTime += pauseDuration;

        this.state.isPaused = false;
        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearInterval(this.pacerInterval);
        this.callbacks.onStateChange({ ...this.state });
    }

    reset(): void {
        this.stop();
        this.currentWordIndex = -1;
        this.phase = 'reading';
        this.currentQuestionIndex = 0;
        this.answers = [];
        this.state.currentStep = 0;
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.stop();
    }

    handleInput(input: any): void {
        if (this.phase === 'answering' && input.type === 'answer') {
            const question = this.questions[this.currentQuestionIndex];
            const correctAnswer = question.CorrectAnswer || question.correctAnswer;
            const isCorrect = input.answer === correctAnswer;

            // Cevabı kaydet
            this.answers.push({
                questionId: question.QuestionId || question.questionId,
                questionText: question.QuestionText || question.questionText,
                userAnswer: input.answer,
                correctAnswer: correctAnswer,
                isCorrect: isCorrect
            });

            // Skoru güncelle
            if (isCorrect) {
                this.state.score += Math.round(100 / this.questions.length);
            }

            // Notify backend
            this.callbacks.onAction({
                action: 'answer_question',
                questionId: question.QuestionId || question.questionId,
                answer: input.answer,
                isCorrect: isCorrect,
                timestamp: new Date()
            });

            this.currentQuestionIndex++;
            this.state.currentStep = this.words.length + this.currentQuestionIndex;

            if (this.currentQuestionIndex >= this.questions.length) {
                this.complete();
            } else {
                this.callbacks.onStateChange({ ...this.state });
            }
        }

        // Detect Regression (if user clicks on previous words)
        // This would be called from the component when a word is clicked.
        if (input.type === 'regression') {
            this.callbacks.onAction({
                action: 'regression_detected',
                number: input.wordIndex,
                timestamp: new Date()
            });
            this.state.errors++;
            this.state.accuracy = Math.round(100 - (this.state.errors / this.words.length * 100));
            this.callbacks.onStateChange({ ...this.state });
        }
    }

    private complete(): void {
        this.state.isCompleted = true;
        this.state.isRunning = false;
        clearInterval(this.timerInterval);

        // Anlama skorunu hesapla
        const correctCount = this.answers.filter(a => a.isCorrect).length;
        const comprehensionScore = this.questions.length > 0
            ? Math.round((correctCount / this.questions.length) * 100)
            : 100;

        // Regresyon skorunu hesapla
        const regressionScore = Math.max(0, 100 - (this.state.errors * 5));

        // Genel skor: %40 regresyon + %60 anlama
        const finalScore = Math.round((regressionScore * 0.4) + (comprehensionScore * 0.6));

        // WPM hesapla
        const readingTimeMinutes = this.state.timeElapsed / 60000;
        const wpm = readingTimeMinutes > 0 ? Math.round(this.words.length / readingTimeMinutes) : 0;

        const result: EngineResult = {
            score: finalScore,
            accuracy: comprehensionScore,
            totalTime: this.state.timeElapsed,
            totalSteps: this.state.totalSteps,
            completedSteps: this.state.totalSteps,
            errors: this.state.errors,
            details: {
                wpm: wpm,
                regressionCount: this.state.errors,
                comprehensionScore: comprehensionScore,
                correctAnswers: correctCount,
                totalQuestions: this.questions.length,
                answers: this.answers,
                phase: 'completed'
            }
        };

        this.callbacks.onComplete(result);
        this.callbacks.onStateChange({ ...this.state });
    }

    // Public Getters for UI
    getWords(): string[] { return this.words; }
    getCurrentWordIndex(): number { return this.currentWordIndex; }
    getPhase(): 'reading' | 'answering' { return this.phase; }
    getQuestions(): any[] { return this.questions; }
    getCurrentQuestion(): any { return this.questions[this.currentQuestionIndex]; }
    getCurrentQuestionIndex(): number { return this.currentQuestionIndex; }
    getMaskingType(): string { return this.config.maskingType || 'none'; }
    getAnswers(): any[] { return this.answers; }
    getLastAnswer(): any { return this.answers.length > 0 ? this.answers[this.answers.length - 1] : null; }
}
