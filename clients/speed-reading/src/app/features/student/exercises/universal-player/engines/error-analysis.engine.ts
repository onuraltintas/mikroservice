/**
 * Error Analysis Engine (Hata Analizi / Proofreading)
 * 
 * Akademik Temeller:
 * - Miscue Analysis (Goodman, 1965)
 * - Cambridge Assessment proofreading tasks
 * - Signal Detection Theory (hits, misses, false alarms)
 * 
 * Egzersiz Akışı:
 * 1. Hatalı metin göster
 * 2. Kullanıcı hatalı kelimelere tıklar
 * 3. Her seçimde geri bildirim ver
 * 4. Tüm hatalar bulununca veya süre dolunca bitir
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';

export interface ErrorInfo {
    wordIndex: number;
    originalWord: string;
    errorWord: string;
    errorType: string;
    explanation: string;
}

export interface WordInfo {
    index: number;
    text: string;
    isSelected: boolean;
}

export interface ErrorAnalysisConfig extends EngineConfig {
    TextWithErrors?: string;
    textWithErrors?: string;
    OriginalText?: string;
    originalText?: string;
    Words?: any[];
    words?: any[];
    Errors?: any[];
    errors?: any[];
    ErrorCount?: number;
    errorCount?: number;
    DifficultyLevel?: number;
    difficultyLevel?: number;
}

type ErrorAnalysisPhase = 'idle' | 'active' | 'completed';

export class ErrorAnalysisEngine implements BaseEngine {
    readonly engineType = 'error_analysis';
    readonly displayName = 'Hata Analizi';

    state: EngineState;
    private callbacks: EngineCallbacks | null = null;

    private textWithErrors: string = '';
    private originalText: string = '';
    private words: WordInfo[] = [];
    private errors: ErrorInfo[] = [];
    private errorCount: number = 0;

    private foundErrors: number[] = [];
    private falseAlarms: number[] = [];
    private selectedWords: Set<number> = new Set();
    private hintUsedCount: number = 0; // Track hints used

    private phase: ErrorAnalysisPhase = 'idle';
    private timerInterval: any = null;
    private startTime: Date | null = null;

    constructor() {
        this.state = this.getInitialState();
    }

    private getInitialState(): EngineState {
        return {
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
    }

    initialize(config: ErrorAnalysisConfig, callbacks: EngineCallbacks): void {
        this.callbacks = callbacks;

        // Parse config with PascalCase fallback
        this.textWithErrors = config.TextWithErrors || config.textWithErrors || '';
        this.originalText = config.OriginalText || config.originalText || '';
        this.errorCount = config.ErrorCount || config.errorCount || 5;

        // Parse words
        const rawWords = config.Words || config.words || [];
        this.words = rawWords.map((w: any) => ({
            index: w.Index ?? w.index ?? 0,
            text: w.Text || w.text || '',
            isSelected: false
        }));

        // Parse errors
        const rawErrors = config.Errors || config.errors || [];
        this.errors = rawErrors.map((e: any) => ({
            wordIndex: e.WordIndex ?? e.wordIndex ?? 0,
            originalWord: e.OriginalWord || e.originalWord || '',
            errorWord: e.ErrorWord || e.errorWord || '',
            errorType: e.ErrorType || e.errorType || 'spelling',
            explanation: e.Explanation || e.explanation || ''
        }));

        this.errorCount = this.errors.length;

        // Reset tracking
        this.foundErrors = [];
        this.falseAlarms = [];
        this.selectedWords = new Set();
        this.phase = 'idle';

        this.state = {
            ...this.getInitialState(),
            totalSteps: this.errorCount
        };

    }

    start(): void {
        if (this.state.isRunning) return;

        this.state.isRunning = true;
        this.phase = 'active';
        this.startTime = new Date();

        this.startTimer();

        this.callbacks?.onStart?.();
        this.callbacks?.onStateChange?.(this.state);

    }

    pause(): void {
        if (!this.state.isRunning || this.state.isPaused) return;

        this.state.isPaused = true;
        this.stopTimer();

        this.callbacks?.onPause?.();
        this.callbacks?.onStateChange?.(this.state);
    }

    resume(): void {
        if (!this.state.isPaused) return;

        this.state.isPaused = false;
        this.startTimer();

        this.callbacks?.onResume?.();
        this.callbacks?.onStateChange?.(this.state);
    }

    stop(): void {
        this.stopTimer();
        this.state.isRunning = false;
        this.state.isPaused = false;
        this.phase = 'completed';

        this.callbacks?.onStateChange?.(this.state);
    }

    reset(): void {
        this.stopTimer();
        this.foundErrors = [];
        this.falseAlarms = [];
        this.selectedWords = new Set();
        this.phase = 'idle';
        this.state = {
            ...this.getInitialState(),
            totalSteps: this.errorCount
        };

        this.callbacks?.onStateChange?.(this.state);
    }

    destroy(): void {
        this.stopTimer();
    }

    handleInput(input: any): void {
        if (!this.state.isRunning || this.state.isPaused || this.phase !== 'active') return;

        if (input?.type === 'select_word' && typeof input.wordIndex === 'number') {
            this.handleWordSelection(input.wordIndex);
        }
    }

    private handleWordSelection(wordIndex: number): void {
        // Already selected?
        if (this.selectedWords.has(wordIndex)) {
            return;
        }

        this.selectedWords.add(wordIndex);

        // Check if this is a real error
        const error = this.errors.find(e => e.wordIndex === wordIndex);

        if (error) {
            // Hit! Found a real error
            this.foundErrors.push(wordIndex);
            this.state.currentStep++;

            this.callbacks?.onStepComplete?.(this.state.currentStep, true);

            // Check if all errors found
            if (this.foundErrors.length >= this.errorCount) {
                this.completeExercise();
            }
        } else {
            // False alarm
            this.falseAlarms.push(wordIndex);
            this.state.errors++;

            this.callbacks?.onStepComplete?.(this.state.currentStep, false);
        }

        // Update accuracy
        const totalSelections = this.foundErrors.length + this.falseAlarms.length;
        this.state.accuracy = totalSelections > 0
            ? Math.round((this.foundErrors.length / totalSelections) * 100)
            : 100;

        this.callbacks?.onStateChange?.(this.state);
    }

    private completeExercise(): void {
        this.stopTimer();
        this.phase = 'completed';
        this.state.isRunning = false;
        this.state.isCompleted = true;

        // Calculate final score using Signal Detection Theory
        const hits = this.foundErrors.length;
        const misses = this.errorCount - hits;
        const falseAlarmCount = this.falseAlarms.length;

        // Hit rate (sensitivity)
        const hitRate = this.errorCount > 0 ? hits / this.errorCount : 0;

        // False alarm penalty (max 30% reduction)
        const faPenalty = Math.min(falseAlarmCount * 5, 30);

        // Final score
        this.state.score = Math.max(0, Math.round(hitRate * 100 - faPenalty));

        const result: EngineResult = {
            score: this.state.score,
            accuracy: this.state.accuracy,
            totalTime: this.state.timeElapsed,
            totalSteps: this.errorCount,
            completedSteps: this.foundErrors.length,
            errors: this.falseAlarms.length,
            details: {
                totalErrors: this.errorCount,
                foundErrors: this.foundErrors.length,
                missedErrors: misses,
                falseAlarms: falseAlarmCount,
                hitRate: Math.round(hitRate * 100),
                precision: (hits + falseAlarmCount) > 0
                    ? Math.round((hits / (hits + falseAlarmCount)) * 100)
                    : 0
            }
        };

        this.callbacks?.onComplete?.(result);
        this.callbacks?.onStateChange?.(this.state);


    }

    private startTimer(): void {
        if (this.timerInterval) return;

        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed += 100;
                this.callbacks?.onStateChange?.(this.state);
            }
        }, 100);
    }

    private stopTimer(): void {
        if (this.timerInterval) {
            clearInterval(this.timerInterval);
            this.timerInterval = null;
        }
    }

    // Getters for template
    getWords(): WordInfo[] {
        return this.words;
    }

    getTextWithErrors(): string {
        return this.textWithErrors;
    }

    getOriginalText(): string {
        return this.originalText;
    }

    getErrors(): ErrorInfo[] {
        return this.errors;
    }

    getErrorCount(): number {
        return this.errorCount;
    }

    getFoundCount(): number {
        return this.foundErrors.length;
    }

    getFalseAlarmCount(): number {
        return this.falseAlarms.length;
    }

    isWordSelected(index: number): boolean {
        return this.selectedWords.has(index);
    }

    isWordError(index: number): boolean {
        return this.errors.some(e => e.wordIndex === index);
    }

    isWordFoundError(index: number): boolean {
        return this.foundErrors.includes(index);
    }

    isWordFalseAlarm(index: number): boolean {
        return this.falseAlarms.includes(index);
    }

    getWordFeedback(index: number): { isError: boolean; explanation: string } | null {
        if (!this.selectedWords.has(index)) return null;

        const error = this.errors.find(e => e.wordIndex === index);

        if (error) {
            return {
                isError: true,
                explanation: error.explanation || `Doğru yazılış: "${error.originalWord}"`
            };
        }

        return {
            isError: false,
            explanation: 'Bu kelimede hata yok.'
        };
    }

    getPhase(): ErrorAnalysisPhase {
        return this.phase;
    }

    getRemainingErrors(): number {
        return Math.max(0, this.errorCount - this.foundErrors.length);
    }

    // For manual completion (timeout or give up)
    forceComplete(): void {
        this.completeExercise();
    }

    // Get missed errors for review
    getMissedErrors(): ErrorInfo[] {
        return this.errors.filter(e => !this.foundErrors.includes(e.wordIndex));
    }

    useHint(): number | null {
        if (this.phase !== 'active') return null;

        const missedErrors = this.getMissedErrors();
        if (missedErrors.length === 0) return null;

        // Randomly select one missed error
        const randomIndex = Math.floor(Math.random() * missedErrors.length);
        const randomError = missedErrors[randomIndex];

        this.hintUsedCount++;

        // Return index to highlight
        return randomError.wordIndex;
    }

    getHintUsedCount(): number {
        return this.hintUsedCount;
    }
}
