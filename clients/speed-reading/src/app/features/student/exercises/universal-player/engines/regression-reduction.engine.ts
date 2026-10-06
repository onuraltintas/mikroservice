/**
 * Regression Reduction Engine
 * Focuses on preventing backward eye movements (regressions).
 * Implements "Trailing Mask" or "Fade Out" scientific paradigms.
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';
import { boundedInteger, caseInsensitiveField, recordOrEmpty, resolveReadingText } from './reading-pacer-safety';

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
    private readingTimeMs = 0;
    private pauseStartTime = 0;
    private timerInterval: any;
    private pacerInterval: any;
    private chunkDeadline = 0;
    private remainingChunkMs = 0;

    private words: string[] = [];
    private currentWordIndex = -1;
    private currentChunkStart = 0;
    private backwardClickCount = 0;
    private phase: 'reading' | 'answering' | 'completed' = 'reading';

    // Questions Related
    private questions: any[] = [];
    private currentQuestionIndex = 0;
    private answers: any[] = [];

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.callbacks = callbacks;
        const root = config as any;
        const nested = recordOrEmpty(caseInsensitiveField(root, 'engineConfig'));
        const sessionData = recordOrEmpty(caseInsensitiveField(root, 'sessionData'));
        const read = (name: string) => caseInsensitiveField(sessionData, name)
            ?? caseInsensitiveField(nested, name)
            ?? caseInsensitiveField(root, name);
        this.config = {
            ...root,
            ...nested,
            wpm: boundedInteger(read('wpm') ?? read('targetWpm'), 200, 20, 1500),
            wordDelayMs: boundedInteger(read('wordDelayMs'), 0, 0, 10000),
            chunkSize: boundedInteger(read('chunkSize'), 1, 1, 10),
            maskingType: ['none', 'fade', 'trailing', 'contingent', 'ior'].includes(read('maskingType'))
                ? read('maskingType') : 'none'
        } as RegressionConfig;

        // Load content
        const text = resolveReadingText({ ...sessionData, ...root }, '');
        this.words = text.split(/\s+/).filter((w: string) => w.length > 0);
        const questions = read('questions');
        this.questions = Array.isArray(questions)
            ? questions.filter(question => question && typeof question === 'object').slice(0, 100)
            : [];

        this.state.totalSteps = this.words.length + this.questions.length;
        this.state.currentStep = 0;
        this.phase = 'reading';
        this.currentWordIndex = -1;


    }

    start(): void {
        if (this.state.isRunning || this.state.isCompleted) return;
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.startTime = Date.now();
        this.readingTimeMs = 0;
        this.callbacks.onStart();

        // Start Global Timer
        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;
                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        if (this.phase === 'reading') {
            this.advanceChunk();
        }
    }

    private startPacer(): void {
        this.chunkDeadline = Date.now() + this.remainingChunkMs;
        this.pacerInterval = setTimeout(() => {
            if (!this.state.isPaused && this.phase === 'reading') {
                this.advanceChunk();
            }
        }, this.remainingChunkMs);
    }

    private advanceChunk(): void {
        const chunkSize = this.config.chunkSize || 1;

        const nextStart = this.currentWordIndex + 1;
        if (nextStart >= this.words.length) {
            this.finishReading();
            return;
        }

        const visibleWords = Math.min(chunkSize, this.words.length - nextStart);
        this.currentChunkStart = nextStart;
        this.currentWordIndex = nextStart + visibleWords - 1;
        this.state.currentStep = this.currentWordIndex + 1;
        this.remainingChunkMs = visibleWords * (this.config.wordDelayMs || 60000 / this.config.wpm);
        this.startPacer();

        this.callbacks.onStepComplete(this.state.currentStep, true);
        this.callbacks.onStateChange({ ...this.state });
    }

    private finishReading(): void {
        clearTimeout(this.pacerInterval);
        this.readingTimeMs = Date.now() - this.startTime;
        this.state.timeElapsed = this.readingTimeMs;
        this.phase = 'answering';
        this.currentQuestionIndex = 0;

        // Notify backend that reading is finished
        this.callbacks.onAction({
            action: 'finish_reading',
            timeMs: this.state.timeElapsed,
            timestamp: new Date()
        });

        this.callbacks.onStateChange({ ...this.state });
        if (this.questions.length === 0) this.complete();
    }

    pause(): void {
        if (!this.state.isRunning || this.state.isPaused) return;
        this.state.isPaused = true;
        this.pauseStartTime = Date.now();
        if (this.phase === 'reading') {
            this.remainingChunkMs = Math.max(0, this.chunkDeadline - this.pauseStartTime);
            clearTimeout(this.pacerInterval);
        }
        this.callbacks.onPause();
        this.callbacks.onStateChange({ ...this.state });
    }

    resume(): void {
        if (!this.state.isPaused) return;

        // Adjust startTime to account for pause duration
        const pauseDuration = Date.now() - this.pauseStartTime;
        this.startTime += pauseDuration;

        this.state.isPaused = false;
        if (this.phase === 'reading') this.startPacer();
        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearTimeout(this.pacerInterval);
        this.callbacks.onStateChange({ ...this.state });
    }

    reset(): void {
        this.stop();
        this.state = {
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            totalSteps: this.words.length + this.questions.length,
            score: 0,
            accuracy: 100,
            timeElapsed: 0,
            errors: 0
        };
        this.currentWordIndex = -1;
        this.phase = 'reading';
        this.currentQuestionIndex = 0;
        this.answers = [];
        this.backwardClickCount = 0;
        this.currentChunkStart = 0;
        this.readingTimeMs = 0;
        this.remainingChunkMs = 0;
        this.chunkDeadline = 0;
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.stop();
    }

    handleInput(input: any): void {
        if (!input || typeof input !== 'object' || this.state.isCompleted) return;

        if (this.phase === 'answering' && input.type === 'answer') {
            const question = this.questions[this.currentQuestionIndex];
            if (!question) return;
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
        if (input.type === 'regression'
            && this.state.isRunning && !this.state.isPaused && this.phase === 'reading'
            && Number.isInteger(input.wordIndex)
            && input.wordIndex >= 0
            && input.wordIndex < this.currentChunkStart) {
            this.callbacks.onAction({
                action: 'backward_word_clicked',
                number: input.wordIndex,
                timestamp: new Date()
            });
            this.backwardClickCount++;
            this.callbacks.onStateChange({ ...this.state });
        }
    }

    private complete(): void {
        if (this.state.isCompleted) return;
        this.state.isCompleted = true;
        this.state.isRunning = false;
        this.phase = 'completed';
        clearInterval(this.timerInterval);
        clearTimeout(this.pacerInterval);
        this.state.timeElapsed = Date.now() - this.startTime;

        // Anlama skorunu hesapla
        const correctCount = this.answers.filter(a => a.isCorrect).length;
        const comprehensionScore = this.questions.length > 0
            ? Math.round((correctCount / this.questions.length) * 100)
            : null;

        const finalScore = comprehensionScore ?? 0;
        this.state.score = finalScore;
        this.state.accuracy = comprehensionScore ?? 0;
        this.state.currentStep = this.state.totalSteps;

        const result: EngineResult = {
            score: finalScore,
            accuracy: comprehensionScore ?? 0,
            totalTime: this.state.timeElapsed,
            totalSteps: this.state.totalSteps,
            completedSteps: this.state.totalSteps,
            errors: this.state.errors,
            details: {
                displayPaceWpm: this.config.wordDelayMs ? Math.round(60000 / this.config.wordDelayMs) : this.config.wpm,
                readingTimeMs: this.readingTimeMs,
                measurementStatus: this.questions.length > 0 ? 'Measured' : 'NotMeasured',
                backwardClickCount: this.backwardClickCount,
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
    getCurrentChunkStart(): number { return this.currentChunkStart; }
    getChunkSize(): number { return this.config.chunkSize || 1; }
    getDisplayPaceWpm(): number { return this.config.wordDelayMs ? Math.round(60000 / this.config.wordDelayMs) : this.config.wpm; }
    getBackwardClickCount(): number { return this.backwardClickCount; }
    getPhase(): 'reading' | 'answering' | 'completed' { return this.phase; }
    getQuestions(): any[] { return this.questions; }
    getCurrentQuestion(): any { return this.questions[this.currentQuestionIndex]; }
    getCurrentQuestionIndex(): number { return this.currentQuestionIndex; }
    getMaskingType(): string { return this.config.maskingType || 'none'; }
    getAnswers(): any[] { return this.answers; }
    getLastAnswer(): any { return this.answers.length > 0 ? this.answers[this.answers.length - 1] : null; }
}
