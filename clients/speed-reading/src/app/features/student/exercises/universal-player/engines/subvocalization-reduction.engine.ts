/**
 * Subvocalization Reduction Engine
 * Trains to reduce internal speech while reading to increase speed.
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';

export interface SubvocalizationConfig extends EngineConfig {
    displayMode: 'highlight' | 'rsvp' | 'chunk';
    wpm: number;
    msPerWord: number;
    metronomeEnabled: boolean;
    metronomeBpm: number;
    visualMetronome: boolean;
    chunkSize: number;
    ReadingTextContent?: string;
    Questions?: any[];
    description?: string;
}

export class SubvocalizationReductionEngine implements BaseEngine {
    readonly engineType = 'subvocalization_reduction';
    readonly displayName = 'İç Ses Azaltma';

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

    private config!: SubvocalizationConfig;
    private callbacks!: EngineCallbacks;
    private startTime = 0;
    private readingStartTime = 0;
    private pauseStartTime = 0;
    private readingEndTime = 0;
    private timerInterval: any;
    private wordTimeout: any; // Changed from wordInterval
    private metronomeInterval: any;

    private lineBreakIndices: Set<number> = new Set();
    private actualChunkSize = 1;

    private words: string[] = [];
    private currentWordIndex = -1;
    private phase: 'reading' | 'answering' | 'completed' = 'reading';

    private questions: any[] = [];
    private currentQuestionIndex = 0;
    private answers: any[] = [];

    private metronomeBeat = false;
    private metronomeBeats = 0;

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.config = config as SubvocalizationConfig;
        this.callbacks = callbacks;

        const backend = config as any;
        const sessionData = backend.SessionData || backend;

        const text = sessionData.ReadingTextContent || sessionData.readingTextContent || "";
        this.words = text.split(/\s+/).filter((w: string) => w.length > 0);
        this.questions = sessionData.Questions || sessionData.questions || [];

        // Set parameters from DifficultySettings if provided (Backend mapping)
        const diff = (sessionData.DifficultySettings || sessionData.difficultySettings) as any;

        let targetWpm = 200;
        if (diff) {
            targetWpm = diff.TargetWPM || diff.targetWpm || 200;
            this.config.wpm = targetWpm;
            this.config.msPerWord = Math.round(60000 / targetWpm);
            this.config.displayMode = diff.DisplayMode || diff.displayMode || 'highlight';
            this.config.chunkSize = diff.ChunkSize || diff.chunkSize || 1;
            this.config.metronomeEnabled = diff.MetronomeEnabled ?? diff.metronomeEnabled ?? false;
            this.config.metronomeBpm = diff.MetronomeBPM || diff.metronomeBpm || 60;
            this.config.description = diff.Description || diff.description || '';
        } else {
            const cfg = (this.config as any);
            targetWpm = cfg.wpm || cfg.targetWpm || (cfg.timing?.targetWpm) || 200;
            this.config.wpm = targetWpm;
            this.config.msPerWord = Math.round(60000 / targetWpm);
            this.config.displayMode = 'highlight';
            this.config.chunkSize = this.config.chunkSize || 1;
        }

        this.state.totalSteps = this.words.length;
        this.state.currentStep = 0;
        this.phase = 'reading';
        this.currentWordIndex = -1;
        this.currentQuestionIndex = 0;
        this.answers = [];


    }

    start(): void {
        this.cleanup();
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.state.timeElapsed = 0;
        this.startTime = Date.now();
        this.readingStartTime = Date.now();

        this.timerInterval = setInterval(() => {
            if (this.state.isRunning && !this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;

                // Calculate WPM here periodically, not on every get call
                if (this.readingStartTime && this.currentWordIndex >= 0) {
                    const elapsedMs = Date.now() - this.readingStartTime;
                    if (elapsedMs > 3000) {
                        const elapsedMinutes = elapsedMs / 60000;
                        const currentWpm = Math.round((this.currentWordIndex + 1) / elapsedMinutes);
                        this.state.currentWPM = Math.min(2000, currentWpm);
                    } else {
                        this.state.currentWPM = this.config.wpm;
                    }
                }

                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        this.startHighlightMode();

        if (this.config.metronomeEnabled && (this.config.metronomeBpm || 0) > 0) {
            this.startMetronome();
        }

        this.callbacks.onAction({
            action: 'start_reading',
            timestamp: new Date()
        });

        // Initial WPM
        this.state.currentWPM = this.config.wpm;
        this.callbacks.onStateChange({ ...this.state });
    }



    private startHighlightMode(): void {
        if (this.wordTimeout) clearTimeout(this.wordTimeout);

        const runStep = () => {
            if (!this.state.isRunning) return;
            if (this.state.isPaused) {
                this.wordTimeout = setTimeout(runStep, 100);
                return;
            }

            // Calculate next step size based on line breaks
            let step = this.config.chunkSize || 1;
            const nextIdx = this.currentWordIndex + step;

            // Safety: Don't exceed word count
            if (this.currentWordIndex + step >= this.words.length) {
                step = this.words.length - this.currentWordIndex - 1;
            }

            // Line separation check: Don't cross to next line in the same chunk
            for (let i = 1; i < step; i++) {
                if (this.lineBreakIndices.has(this.currentWordIndex + 1 + i)) {
                    step = i;
                    break;
                }
            }

            this.actualChunkSize = Math.max(1, step);
            this.currentWordIndex += this.actualChunkSize;

            if (this.currentWordIndex >= this.words.length) {
                this.finishReading();
                return;
            }

            this.state.currentStep = this.currentWordIndex;
            this.callbacks.onStateChange({
                ...this.state,
                currentWordIndex: this.currentWordIndex,
                actualChunkSize: this.actualChunkSize,
                displayMode: 'highlight',
                chunkSize: this.config.chunkSize
            } as any);

            // Calculate delay: proportional to words shown to keep WPM rhythm
            const delay = this.config.msPerWord * this.actualChunkSize;
            this.wordTimeout = setTimeout(runStep, delay);
        };

        // Start the first step after a small initial delay
        this.wordTimeout = setTimeout(runStep, this.config.msPerWord);
    }

    private startMetronome(): void {
        // Fixed 1 second per beat (user request: each number visible for 1 second)
        const msPerBeat = 1000;

        // Start at 1
        this.metronomeBeats = 1;

        // Store in state explicitly if needed, but for now we'll ensure it's passed
        // We need to persist this so other updates don't wipe it out.
        // Since EngineState interface doesn't have it, we'll cast or just rely on passing it.
        // BETTER APPROACH: Add it to the class property state effectively (via casting)
        (this.state as any).metronomeStep = this.metronomeBeats;

        this.callbacks.onStateChange({
            ...this.state,
            metronomeStep: this.metronomeBeats
        } as any);

        this.metronomeInterval = setInterval(() => {
            if (this.state.isPaused) return;

            // Move to next number: 1 -> 2 -> 3 -> 4 -> 1 ...
            this.metronomeBeats = (this.metronomeBeats % 4) + 1;
            (this.state as any).metronomeStep = this.metronomeBeats;

            this.callbacks.onStateChange({
                ...this.state,
                metronomeStep: this.metronomeBeats
            } as any);
        }, msPerBeat);
    }

    private finishReading(): void {
        this.cleanup();
        this.readingEndTime = Date.now();

        this.callbacks.onAction({
            action: 'finish_reading',
            timestamp: new Date()
        });

        if (this.questions.length > 0) {
            this.phase = 'answering';
            this.currentQuestionIndex = 0;
            this.callbacks.onStateChange({
                ...this.state,
                phase: 'answering',
                currentQuestionIndex: 0
            } as any);
        } else {
            this.complete();
        }
    }

    pause(): void {
        if (this.state.isPaused) return;
        this.state.isPaused = true;
        this.pauseStartTime = Date.now();
        this.callbacks.onStateChange({ ...this.state });
    }

    resume(): void {
        if (!this.state.isPaused) return;

        // Adjust startTime and readingStartTime to account for pause duration
        const pauseDuration = Date.now() - this.pauseStartTime;
        this.startTime += pauseDuration;
        this.readingStartTime += pauseDuration;

        this.state.isPaused = false;
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        this.cleanup();
        this.state.isRunning = false;
        this.callbacks.onStateChange({ ...this.state });
    }

    // Feedback State
    public showingFeedback = false;
    public lastAnswer = '';
    public lastAnswerCorrect = false;
    public currentCorrectAnswer = '';

    handleInput(input: any): void {
        if (input.type === 'line_breaks') {
            this.lineBreakIndices = new Set(input.indices);
            return;
        }

        if (this.phase === 'answering' && input.type === 'answer') {
            if (this.showingFeedback) return; // Prevent double submit

            const question = this.questions[this.currentQuestionIndex];
            const correctAnswer = question.CorrectAnswer || question.correctAnswer;
            const isCorrect = input.answer === correctAnswer;

            // Set Feedback State
            this.showingFeedback = true;
            this.lastAnswer = input.answer;
            this.lastAnswerCorrect = isCorrect;
            this.currentCorrectAnswer = correctAnswer;

            this.answers.push({
                questionId: question.QuestionId || question.questionId,
                questionText: question.QuestionText || question.questionText,
                userAnswer: input.answer,
                correctAnswer: correctAnswer,
                isCorrect: isCorrect
            });

            if (isCorrect) {
                this.state.score += Math.round(100 / this.questions.length);
            }

            this.callbacks.onAction({
                action: 'answer_question',
                questionId: question.QuestionId || question.questionId,
                answer: input.answer,
                isCorrect: isCorrect,
                timestamp: new Date()
            });

            // Do NOT advance yet, wait for nextQuestion()
            this.callbacks.onStateChange({ ...this.state });
        }
    }

    nextQuestion(): void {
        if (!this.showingFeedback) return;

        this.showingFeedback = false;
        this.lastAnswer = '';
        this.currentCorrectAnswer = '';

        this.currentQuestionIndex++;
        this.state.currentStep = this.words.length + this.currentQuestionIndex;

        if (this.currentQuestionIndex >= this.questions.length) {
            this.complete();
        } else {
            this.callbacks.onStateChange({ ...this.state });
        }
    }

    private complete(): void {
        this.cleanup();

        this.phase = 'completed';
        this.state.isCompleted = true;
        this.state.isRunning = false;
        this.state.timeElapsed = Date.now() - this.startTime;

        const readingTimeMs = this.readingEndTime - this.readingStartTime;
        const readingTimeMinutes = readingTimeMs / 60000;
        const actualWpm = readingTimeMinutes > 0 ? Math.round(this.words.length / readingTimeMinutes) : 0;

        const correctCount = this.answers.filter(a => a.isCorrect).length;
        const comprehensionScore = this.questions.length > 0
            ? Math.round((correctCount / this.questions.length) * 100)
            : 100;

        const speedScore = Math.min(100, Math.round((actualWpm / this.config.wpm) * 100));
        const finalScore = Math.round((speedScore * 0.5) + (comprehensionScore * 0.5));

        this.state.score = finalScore;
        this.state.accuracy = comprehensionScore;

        const result: EngineResult = {
            score: finalScore,
            accuracy: comprehensionScore,
            totalTime: this.state.timeElapsed,
            totalSteps: this.state.totalSteps,
            completedSteps: this.state.totalSteps,
            errors: 0,
            details: {
                wpm: actualWpm,
                targetWpm: this.config.wpm,
                readingTimeMs: readingTimeMs,
                wordCount: this.words.length,
                speedScore: speedScore,
                comprehensionScore: comprehensionScore,
                correctAnswers: correctCount,
                totalQuestions: this.questions.length,
                answers: this.answers,
                metronomeBeats: this.metronomeBeats,
                metronomeUsed: this.config.metronomeEnabled,
                displayMode: this.config.displayMode,
                chunkSize: this.config.chunkSize,
                phase: 'completed'
            }
        };

        this.callbacks.onComplete(result);
        this.callbacks.onStateChange({ ...this.state });
    }

    private cleanup(): void {
        if (this.timerInterval) clearInterval(this.timerInterval);
        if (this.wordTimeout) clearTimeout(this.wordTimeout);
        if (this.metronomeInterval) clearInterval(this.metronomeInterval);
        this.timerInterval = null;
        this.wordTimeout = null;
        this.metronomeInterval = null;
    }

    reset(): void {
        this.cleanup();
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
        this.currentQuestionIndex = 0;
        this.answers = [];
        this.phase = 'reading';
        this.metronomeBeat = false;
        this.metronomeBeats = 0;
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.cleanup();
    }

    getWords(): string[] { return this.words; }
    getCurrentWordIndex(): number { return this.currentWordIndex; }

    getCurrentChunk(): string {
        if (this.currentWordIndex < 0 || this.currentWordIndex >= this.words.length) {
            return '';
        }
        const chunkEnd = Math.min(this.currentWordIndex + (this.config.chunkSize || 1), this.words.length);
        return this.words.slice(this.currentWordIndex, chunkEnd).join(' ');
    }

    getPhase(): string { return this.phase; }
    getQuestions(): any[] { return this.questions; }
    getCurrentQuestion(): any { return this.questions[this.currentQuestionIndex]; }
    getCurrentQuestionIndex(): number { return this.currentQuestionIndex; }
    getQuestionCount(): number { return this.questions.length; }
    getDisplayMode(): string { return this.config.displayMode; }
    getTargetWpm(): number { return this.config.wpm; }

    getCurrentWpm(): number {
        return this.state.currentWPM || this.config.wpm;
    }

    getProgress(): number {
        if (this.words.length === 0) return 0;
        return Math.round(((this.currentWordIndex + 1) / this.words.length) * 100);
    }
}
