/**
 * Hedef Avı (target hunt) — karar 29.10.2026, programlarda N-back'in yerini alır.
 *
 * Satırlar halinde harf veya kısa kelimeler gösterilir. Öğrenci süre bitmeden hedefin geçtiği her yere dokunur;
 * benzer görünen çeldiricilere dokunmamaya çalışır. Kayıtlı oturumda yerleşimi sunucu üretir, her dokunuşu sunucu
 * doğrular ve süreyi sunucu tutar. Önizlemede yerleşim tarayıcıda üretilir ve sonuç kaydedilmez.
 */

import { BaseEngine, EngineCallbacks, EngineConfig, EngineResult, EngineState } from './base-engine.interface';
import { caseInsensitiveField, recordOrEmpty } from './reading-pacer-safety';

export interface TargetHuntFeedback {
    index: number;
    correct: boolean;
}

interface TargetHuntSettings {
    target: string;
    distractors: string[];
    rows: number;
    columns: number;
    targetCount: number;
    timeLimit: number;
}

export class TargetHuntEngine implements BaseEngine {
    readonly engineType = 'target_hunt';
    readonly displayName = 'Hedef Avı';

    state: EngineState = TargetHuntEngine.initialState(0);

    private callbacks: EngineCallbacks | null = null;
    private items: string[] = [];
    private target = '';
    private columns = 0;
    private targetCount = 0;
    private timeLimitMs = 60_000;
    private hits = new Set<number>();
    private falseTaps = new Set<number>();
    private lastFeedback: TargetHuntFeedback | null = null;
    private serverAuthoritative = false;
    private serverStarted = false;
    private pendingAction: any = null;
    private failedAction: any = null;
    private timer: ReturnType<typeof setInterval> | null = null;
    private lastTick = 0;
    private serverScore: number | null = null;
    private serverAccuracy: number | null = null;

    private static initialState(totalSteps: number): EngineState {
        return { isRunning: false, isPaused: false, isCompleted: false, currentStep: 0, totalSteps,
            score: 0, accuracy: 0, timeElapsed: 0, errors: 0 };
    }

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.stopTimer();
        this.callbacks = callbacks;
        const root = recordOrEmpty(config);
        const nested = recordOrEmpty(caseInsensitiveField(root, 'engineConfig'));
        const sessionData = recordOrEmpty(caseInsensitiveField(root, 'sessionData'));
        const read = (name: string) => caseInsensitiveField(sessionData, name) ?? caseInsensitiveField(root, name);
        this.serverAuthoritative = root['serverAuthoritative'] === true;
        const settings = TargetHuntEngine.readSettings(nested);

        const serverItems = read('targetHuntItems');
        if (Array.isArray(serverItems)) {
            this.items = serverItems.slice(0, 144).map(item => typeof item === 'string' ? item : '');
            this.target = String(read('targetHuntTarget') ?? settings?.target ?? '');
            this.columns = Number(read('targetHuntColumns') ?? settings?.columns ?? 0);
            this.targetCount = Number(read('totalSteps') ?? this.items.filter(item => item === this.target).length);
            const seconds = Number(read('timeLimitSeconds') ?? settings?.timeLimit ?? 60);
            this.timeLimitMs = Number.isFinite(seconds) && seconds > 0 ? Math.min(600, seconds) * 1000 : 60_000;
        } else if (settings && !this.serverAuthoritative) {
            this.items = TargetHuntEngine.createLayout(settings);
            this.target = settings.target;
            this.columns = settings.columns;
            this.targetCount = settings.targetCount;
            this.timeLimitMs = settings.timeLimit * 1000;
        } else {
            this.items = []; this.target = ''; this.columns = 0; this.targetCount = 0;
        }
        this.resetProgress();
    }

    start(): void {
        if (this.state.isRunning || this.state.isCompleted) return;
        const targets = this.items.filter(item => item === this.target).length;
        if (!this.target || !this.items.length || this.items.some(item => !item) || targets === 0
            || !Number.isInteger(this.columns) || this.columns < 1 || this.targetCount < 1
            || (!this.serverAuthoritative && targets !== this.targetCount)) {
            this.callbacks?.onError?.('Hedef avı içeriği eksik veya tutarsız. Lütfen egzersizi yeniden açın.');
            return;
        }
        this.state.isRunning = true;
        this.startTimer();
        this.callbacks?.onStart?.();
        this.callbacks?.onStateChange?.(this.state);
        if (this.serverAuthoritative) this.submitAction('target_hunt_start');
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
        this.pendingAction = null;
        this.callbacks?.onStateChange?.(this.state);
    }

    reset(): void {
        this.stopTimer();
        this.resetProgress();
        this.callbacks?.onStateChange?.(this.state);
    }

    destroy(): void {
        this.stopTimer();
        this.state.isRunning = false;
        this.state.isPaused = false;
        this.pendingAction = null;
    }

    handleInput(input: any): void {
        if (!this.state.isRunning || this.state.isPaused || this.state.isCompleted) return;
        if (input?.type !== 'tap' || !Number.isInteger(input.index)) return;
        const index = input.index as number;
        if (index < 0 || index >= this.items.length || this.hits.has(index) || this.falseTaps.has(index)) return;
        if (this.serverAuthoritative) {
            if (!this.serverStarted || this.pendingAction || this.failedAction) return;
            this.submitAction('target_hunt_tap', index);
            return;
        }
        const correct = this.items[index] === this.target;
        (correct ? this.hits : this.falseTaps).add(index);
        this.lastFeedback = { index, correct };
        this.syncState();
        this.callbacks?.onStepComplete?.(this.hits.size, correct);
        if (this.hits.size >= this.targetCount) this.complete();
        else this.callbacks?.onStateChange?.(this.state);
    }

    /** Ends the round early (the student is done) or when the clock runs out. */
    forceComplete(): void {
        if (!this.state.isRunning || this.state.isPaused || this.state.isCompleted) return;
        if (this.serverAuthoritative) {
            this.submitAction(this.serverStarted ? 'target_hunt_finish' : 'target_hunt_start');
            return;
        }
        this.complete();
    }

    reconcileServerResponse(action: any, response: any): void {
        if (action !== this.pendingAction || !this.state.isRunning || this.state.isCompleted) return;
        this.pendingAction = null;
        if (!response?.isValid || !response.feedbackData) {
            this.failedAction = action;
            this.callbacks?.onError?.(response?.message || 'Dokunuşunuz kaydedilemedi. Lütfen tekrar deneyin.');
            return;
        }
        this.failedAction = null;
        this.serverStarted = true;
        const data = response.feedbackData;
        this.hits = new Set((Array.isArray(data.hits) ? data.hits : []).filter(Number.isInteger));
        this.falseTaps = new Set((Array.isArray(data.falseTaps) ? data.falseTaps : []).filter(Number.isInteger));
        if (typeof data.timeElapsed === 'number') { this.state.timeElapsed = data.timeElapsed; this.lastTick = Date.now(); }
        if (typeof data.score === 'number') this.serverScore = data.score;
        if (typeof data.accuracy === 'number') this.serverAccuracy = data.accuracy;
        if (action.action === 'target_hunt_tap' && typeof response.isCorrect === 'boolean') {
            this.lastFeedback = { index: action.index, correct: response.isCorrect };
            this.callbacks?.onStepComplete?.(this.hits.size, response.isCorrect);
        }
        this.syncState();
        if (response.isCompleted) this.complete();
        else this.callbacks?.onStateChange?.(this.state);
    }

    retryServerAction(): void {
        if (!this.failedAction || this.pendingAction || !this.state.isRunning || this.state.isPaused) return;
        this.pendingAction = this.failedAction;
        this.failedAction = null;
        this.callbacks?.onAction?.(this.pendingAction);
    }

    // Template helpers
    getItems(): string[] { return this.items; }
    getTarget(): string { return this.target; }
    getColumns(): number { return this.columns; }
    getTargetCount(): number { return this.targetCount; }
    getFoundCount(): number { return this.hits.size; }
    getFalseTapCount(): number { return this.falseTaps.size; }
    isHit(index: number): boolean { return this.hits.has(index); }
    isFalseTap(index: number): boolean { return this.falseTaps.has(index); }
    getLastFeedback(): TargetHuntFeedback | null { return this.lastFeedback; }
    isAwaitingServer(): boolean { return !!this.pendingAction; }
    hasFailedAction(): boolean { return !!this.failedAction; }
    getRemainingSeconds(): number { return Math.max(0, Math.ceil((this.timeLimitMs - this.state.timeElapsed) / 1000)); }
    getTimeLimitSeconds(): number { return Math.round(this.timeLimitMs / 1000); }

    private complete(): void {
        if (this.state.isCompleted) return;
        this.updateElapsed();
        this.stopTimer();
        this.syncState();
        const found = this.hits.size, falseTaps = this.falseTaps.size;
        const localScore = this.targetCount > 0
            ? Math.max(0, Math.round((100 * found / this.targetCount - Math.min(falseTaps * 5, 30)) * 100) / 100) : 0;
        const localAccuracy = this.targetCount > 0 ? Math.round(10000 * found / (this.targetCount + falseTaps)) / 100 : 0;
        this.state.score = this.serverScore ?? localScore;
        this.state.accuracy = this.serverAccuracy ?? localAccuracy;
        this.state.isRunning = false;
        this.state.isCompleted = true;
        const result: EngineResult = {
            score: this.state.score,
            accuracy: this.state.accuracy,
            totalTime: this.state.timeElapsed,
            totalSteps: this.targetCount,
            completedSteps: found,
            errors: falseTaps,
            details: { measurementKind: 'target_hunt', target: this.target, found, missed: Math.max(0, this.targetCount - found),
                falseTaps, totalTargets: this.targetCount }
        };
        this.callbacks?.onComplete?.(result);
        this.callbacks?.onStateChange?.(this.state);
    }

    private resetProgress(): void {
        this.hits = new Set(); this.falseTaps = new Set(); this.lastFeedback = null;
        this.serverStarted = false; this.pendingAction = null; this.failedAction = null;
        this.serverScore = null; this.serverAccuracy = null;
        this.state = TargetHuntEngine.initialState(this.targetCount);
    }

    private syncState(): void {
        this.state.currentStep = this.hits.size;
        this.state.errors = this.falseTaps.size;
        this.state.targetCount = this.hits.size;
        this.state.remainingSeconds = this.getRemainingSeconds();
        this.state.isLastTenSeconds = this.state.remainingSeconds <= 10;
    }

    private submitAction(action: string, index?: number): void {
        if (this.pendingAction || this.failedAction) return;
        this.pendingAction = { action, index, timestamp: new Date(), actionId: crypto.randomUUID() };
        this.callbacks?.onAction?.(this.pendingAction);
    }

    private startTimer(): void {
        if (this.timer) return;
        this.lastTick = Date.now();
        this.timer = setInterval(() => {
            if (this.state.isPaused) return;
            this.updateElapsed();
            this.syncState();
            this.callbacks?.onStateChange?.(this.state);
            if (this.state.timeElapsed >= this.timeLimitMs && !this.pendingAction && !this.failedAction) this.forceComplete();
        }, 250);
    }

    private stopTimer(): void {
        if (this.timer) { clearInterval(this.timer); this.timer = null; }
    }

    private updateElapsed(): void {
        if (!this.state.isRunning || this.state.isPaused || !this.lastTick) return;
        const now = Date.now();
        this.state.timeElapsed = Math.min(this.timeLimitMs, this.state.timeElapsed + Math.max(0, now - this.lastTick));
        this.lastTick = now;
    }

    private static readSettings(config: Record<string, any>): TargetHuntSettings | null {
        const target = config['target'];
        const distractors = Array.isArray(config['distractors'])
            ? config['distractors'].filter((item: unknown) => typeof item === 'string' && item.length > 0) : [];
        const number = (name: string) => Number.isInteger(config[name]) ? config[name] as number : 0;
        const settings = { target: typeof target === 'string' ? target : '', distractors, rows: number('rows'),
            columns: number('columns'), targetCount: number('targetCount'), timeLimit: number('timeLimit') };
        return settings.target && distractors.length && !distractors.includes(settings.target)
            && settings.rows >= 3 && settings.rows <= 12 && settings.columns >= 3 && settings.columns <= 12
            && settings.targetCount >= 1 && settings.targetCount * 2 <= settings.rows * settings.columns
            && settings.timeLimit >= 20 && settings.timeLimit <= 600 ? settings : null;
    }

    private static createLayout(settings: TargetHuntSettings): string[] {
        const count = settings.rows * settings.columns;
        const positions = Array.from({ length: count }, (_, index) => index);
        for (let index = positions.length - 1; index > 0; index--) {
            const swap = Math.floor(Math.random() * (index + 1));
            [positions[index], positions[swap]] = [positions[swap], positions[index]];
        }
        const targets = new Set(positions.slice(0, settings.targetCount));
        return positions.map((_, index) => targets.has(index)
            ? settings.target
            : settings.distractors[Math.floor(Math.random() * settings.distractors.length)]);
    }
}
