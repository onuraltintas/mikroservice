/**
 * Error Analysis Engine (Hata Analizi / Proofreading)
 * 
 * Metindeki hazırlanmış hataları bulma alıştırmasıdır.
 * Bulunan/kaçırılan hata ve yanlış seçim raporlanır; klinik ölçüm veya d-prime hesaplaması değildir.
 * 
 * Egzersiz Akışı:
 * 1. Hatalı metin göster
 * 2. Kullanıcı hatalı kelimelere tıklar
 * 3. Her seçimde geri bildirim ver
 * 4. Tüm hatalar bulununca veya süre dolunca bitir
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';
import { boundedText, caseInsensitiveField, mergeCaseInsensitiveRecords, recordOrEmpty } from './reading-pacer-safety';

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
    private fontSize: string = 'medium';

    getFontSize(): string { return this.fontSize; }

    private foundErrors: number[] = [];
    private falseAlarms: number[] = [];
    private selectedWords: Set<number> = new Set();
    private hintUsedCount: number = 0; // Track hints used

    private phase: ErrorAnalysisPhase = 'idle';
    private timerInterval: any = null;
    private lastTick = 0;
    private timeLimitMs = 180_000;
    private serverAuthoritative = false;
    private serverStarted = false;
    private pendingAction: any = null;
    private failedAction: any = null;
    private hintIndex: number | null = null;

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
        this.stopTimer();
        this.callbacks = callbacks;
        const root = recordOrEmpty(config);
        const nested = recordOrEmpty(caseInsensitiveField(root, 'engineConfig'));
        const display = mergeCaseInsensitiveRecords(root, nested, 'display');
        const fontSize = String(display['fontsize'] ?? 'medium').toLowerCase();
        this.fontSize = ['small', 'medium', 'large'].includes(fontSize) ? fontSize : 'medium';
        const sessionData = recordOrEmpty(caseInsensitiveField(root, 'sessionData'));
        const read = (name: string) => caseInsensitiveField(sessionData, name)
            ?? caseInsensitiveField(nested, name)
            ?? caseInsensitiveField(root, name);
        this.serverAuthoritative = config['serverAuthoritative'] === true;
        this.serverStarted = false; this.pendingAction = null; this.failedAction = null; this.hintIndex = null;
        const timing = mergeCaseInsensitiveRecords(root, nested, 'timing');
        const seconds = Number(read('timeLimitSeconds') ?? read('timeLimit') ?? timing['timelimitsec'] ?? 180);
        this.timeLimitMs = Number.isFinite(seconds) && seconds > 0 ? Math.min(3600, seconds) * 1000 : 180_000;

        // Parse config with PascalCase fallback
        this.textWithErrors = boundedText(read('textWithErrors'), '');
        this.originalText = boundedText(read('originalText'), '');

        // Parse words
        const rawWords = read('words');
        this.words = (Array.isArray(rawWords) ? rawWords : []).slice(0, 10_000).map((value: any) => {
            const w = recordOrEmpty(value);
            return {
            index: w['Index'] ?? w['index'] ?? NaN,
            text: w['Text'] ?? w['text'] ?? '',
            isSelected: false
        } as WordInfo; });

        // Parse errors
        const rawErrors = read('errors');
        this.errors = (Array.isArray(rawErrors) ? rawErrors : []).slice(0, 1_000).map((value: any) => {
            const e = recordOrEmpty(value);
            return {
            wordIndex: e['WordIndex'] ?? e['wordIndex'] ?? NaN,
            originalWord: e['OriginalWord'] ?? e['originalWord'] ?? '',
            errorWord: e['ErrorWord'] ?? e['errorWord'] ?? '',
            errorType: e['ErrorType'] ?? e['errorType'] ?? 'spelling',
            explanation: e['Explanation'] ?? e['explanation'] ?? ''
        } as ErrorInfo; });

        this.errorCount = this.serverAuthoritative ? Number(read('totalSteps') ?? this.errors.length) : this.errors.length;

        // Reset tracking
        this.foundErrors = [];
        this.falseAlarms = [];
        this.selectedWords = new Set();
        this.hintUsedCount = 0;
        this.phase = 'idle';

        this.state = {
            ...this.getInitialState(),
            totalSteps: this.errorCount
        };

    }

    start(): void {
        if (this.state.isRunning || this.state.isCompleted) return;
        const indices = new Set(this.words.map(word => word.index));
        if (!this.words.length || !this.errorCount || indices.size !== this.words.length
            || this.words.some(word => !Number.isInteger(word.index) || word.index < 0 || typeof word.text !== 'string' || !word.text.trim())
            || new Set(this.errors.map(error => error.wordIndex)).size !== this.errors.length
            || this.errors.some(error => !indices.has(error.wordIndex)
                || typeof error.originalWord !== 'string' || !error.originalWord.trim()
                || typeof error.errorWord !== 'string' || !error.errorWord.trim()
                || error.originalWord === error.errorWord
                || this.words.find(word => word.index === error.wordIndex)?.text !== error.errorWord)) {
            this.callbacks?.onError?.('Hata analizi içeriği eksik veya tutarsız. Lütfen farklı bir egzersiz seçin.');
            return;
        }

        this.state.isRunning = true;
        this.phase = 'active';

        this.startTimer();

        this.callbacks?.onStart?.();
        this.callbacks?.onStateChange?.(this.state);
        if (this.serverAuthoritative) this.submitAction('error_analysis_start');

    }

    pause(): void {
        if (!this.state.isRunning || this.state.isPaused || this.pendingAction) return;
        this.updateElapsed();
        this.state.isPaused = true;
        this.stopTimer();

        this.callbacks?.onPause?.();
        this.callbacks?.onStateChange?.(this.state);
    }

    resume(): void {
        if (!this.state.isRunning || !this.state.isPaused) return;

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
        this.pendingAction = null;

        this.callbacks?.onStateChange?.(this.state);
    }

    reset(): void {
        this.stopTimer();
        this.foundErrors = [];
        this.falseAlarms = [];
        this.selectedWords = new Set();
        this.hintUsedCount = 0;
        this.phase = 'idle';
        this.pendingAction = null; this.failedAction = null; this.serverStarted = false; this.hintIndex = null;
        this.state = {
            ...this.getInitialState(),
            totalSteps: this.errorCount
        };

        this.callbacks?.onStateChange?.(this.state);
    }

    destroy(): void {
        this.stopTimer();
        this.state.isRunning = false;
        this.state.isPaused = false;
        this.phase = 'completed';
        this.pendingAction = null;
    }

    handleInput(input: any): void {
        if (!this.state.isRunning || this.state.isPaused || this.phase !== 'active') return;

        if (input?.type === 'select_word' && typeof input.wordIndex === 'number') {
            this.handleWordSelection(input.wordIndex);
        }
    }

    private handleWordSelection(wordIndex: number): void {
        if (!Number.isInteger(wordIndex) || !this.words.some(word => word.index === wordIndex)) return;
        if (this.serverAuthoritative) {
            if (!this.serverStarted || this.pendingAction || this.selectedWords.has(wordIndex)) return;
            this.submitAction('error_analysis_select', wordIndex); return;
        }

        // Already selected?
        if (this.selectedWords.has(wordIndex)) {
            return;
        }

        this.selectedWords.add(wordIndex);
        this.hintIndex = null;

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
                return;
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
        if (this.state.isCompleted) return;
        this.updateElapsed();
        this.stopTimer();
        this.phase = 'completed';
        this.state.isRunning = false;
        this.state.isCompleted = true;

        // Exercise-specific hit-rate score; not a standardized sensitivity measure.
        const hits = this.foundErrors.length;
        const misses = this.errorCount - hits;
        const falseAlarmCount = this.falseAlarms.length;
        const evaluatedTargets = this.errorCount + falseAlarmCount;
        this.state.accuracy = evaluatedTargets > 0
            ? Math.round((hits / evaluatedTargets) * 100)
            : 0;

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
                hintUsedCount: this.hintUsedCount,
                assisted: this.hintUsedCount > 0,
                measurementKind: 'proofreading',
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
        this.lastTick = Date.now();
        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.updateElapsed();
                this.callbacks?.onStateChange?.(this.state);
                if (this.state.timeElapsed >= this.timeLimitMs && !this.pendingAction && !this.failedAction) this.forceComplete();
            }
        }, 100);
    }

    private updateElapsed(): void {
        if (this.state.isRunning && !this.state.isPaused && this.lastTick) {
            const now = Date.now();
            this.state.timeElapsed = Math.min(this.timeLimitMs, this.state.timeElapsed + Math.max(0, now - this.lastTick));
            this.lastTick = now;
        }
    }

    private submitAction(action: string, index?: number): void {
        if (this.pendingAction || this.failedAction) return;
        this.pendingAction = { action, index, timestamp: new Date(), actionId: crypto.randomUUID() };
        this.callbacks?.onAction?.(this.pendingAction);
    }

    reconcileServerResponse(action: any, response: any): void {
        if (action !== this.pendingAction || !this.state.isRunning || this.state.isCompleted) return;
        this.pendingAction = null;
        if (!response?.isValid || !response.feedbackData) {
            this.failedAction = action;
            this.callbacks?.onError?.(response?.message || 'Seçiminiz kaydedilemedi. Lütfen tekrar deneyin.'); return;
        }
        this.failedAction = null;
        const data = response.feedbackData;
        if (Array.isArray(data.errors)) this.errors = data.errors;
        this.serverStarted = true;
        this.selectedWords = new Set(data.selected ?? []);
        this.foundErrors = data.found ?? []; this.falseAlarms = data.falseAlarms ?? [];
        this.hintUsedCount = data.hintUsedCount ?? 0;
        this.hintIndex = typeof data.hintIndex === 'number' ? data.hintIndex : null;
        this.state.currentStep = this.foundErrors.length; this.state.errors = this.falseAlarms.length;
        this.state.timeElapsed = data.timeElapsed ?? this.state.timeElapsed; this.lastTick = Date.now();
        if (response.isCompleted) this.completeExercise();
        else { this.state.accuracy = data.accuracy ?? 0; this.callbacks?.onStateChange?.(this.state); }
    }

    isAwaitingServer(): boolean { return !!this.pendingAction; }
    hasFailedAction(): boolean { return !!this.failedAction; }
    retryServerAction(): void {
        if (!this.failedAction || this.pendingAction || !this.state.isRunning || this.state.isPaused) return;
        this.pendingAction = this.failedAction; this.failedAction = null;
        this.callbacks?.onAction?.(this.pendingAction);
    }
    getHintIndex(): number | null { return this.hintIndex; }

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
        if (this.phase !== 'active' || this.state.isPaused || !this.state.isRunning) return;
        if (this.serverAuthoritative) {
            if (!this.serverStarted) this.submitAction('error_analysis_start');
            else this.submitAction('error_analysis_finish');
            return;
        }
        this.completeExercise();
    }

    // Get missed errors for review
    getMissedErrors(): ErrorInfo[] {
        return this.errors.filter(e => !this.foundErrors.includes(e.wordIndex));
    }

    useHint(): number | null {
        if (this.phase !== 'active' || this.state.isPaused || !this.state.isRunning) return null;
        if (this.serverAuthoritative) {
            if (this.serverStarted) this.submitAction('error_analysis_hint');
            return null;
        }

        const missedErrors = this.getMissedErrors();
        if (missedErrors.length === 0) return null;

        // Randomly select one missed error
        const randomIndex = Math.floor(Math.random() * missedErrors.length);
        const randomError = missedErrors[randomIndex];

        this.hintUsedCount++;
        this.hintIndex = randomError.wordIndex;

        // Return index to highlight
        return randomError.wordIndex;
    }

    getHintUsedCount(): number {
        return this.hintUsedCount;
    }
}
