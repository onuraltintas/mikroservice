/**
 * Mental Registration Engine (Focus Engine)
 * 
 * Implements N-Back Working Memory training with 3 modes:
 * - "position": Grid-based spatial memory (easier)
 * - "word": Verbal memory with words (medium)  
 * - "dual": Both position and word simultaneously (hardest)
 */

import { BaseEngine, EngineConfig, EngineState, EngineCallbacks } from './base-engine.interface';

interface MentalRegistrationConfig extends EngineConfig {
    Mode: string;              // "position" | "word" | "dual"
    NLevel: number;            // 1-Back, 2-Back, etc.
    SpeedMs: number;           // Duration per item in ms
    GridSize: number;          // 3 for 3x3, 4 for 4x4
    WordSequence?: string[];
    WordTargetIndices?: number[];
    PositionSequence?: number[];
    PositionTargetIndices?: number[];
}

export class FocusEngine implements BaseEngine {
    readonly engineType = 'focus';
    readonly displayName = 'Zihinsel Kayıt';

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

    public config!: MentalRegistrationConfig;
    private callbacks!: EngineCallbacks;

    private timerInterval: any;
    private pacerInterval: any;
    private startTime = 0;
    private pauseStartTime = 0;

    // Game Logic
    private currentIndex = -1;
    public currentWord = '';
    public currentPosition = 0; // Grid cell number (1-9 for 3x3)
    public mode: string = 'position';

    // Visual transition flag - briefly hides the active cell between steps
    public isTransitioning = false;

    // Scoring - separate for position and word
    public hits = 0;
    public misses = 0;
    public falseAlarms = 0;

    // Track responses for each channel in dual mode
    private hasRespondedPosition = false;
    private hasRespondedWord = false;

    private sequenceLength = 0;

    // Timing drift correction
    private expectedTime = 0;
    private nextStepTime = 0;

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.callbacks = callbacks;

        const backendData = (config as any).SessionData || config;

        this.config = {
            ...config,
            Mode: backendData.Mode || 'position',
            NLevel: backendData.NLevel || 1,
            SpeedMs: backendData.SpeedMs || 1500,
            GridSize: backendData.GridSize || 3,
            WordSequence: backendData.WordSequence || [],
            WordTargetIndices: backendData.WordTargetIndices || [],
            PositionSequence: backendData.PositionSequence || [],
            PositionTargetIndices: backendData.PositionTargetIndices || []
        } as MentalRegistrationConfig;

        this.mode = this.config.Mode;

        // Determine sequence length based on mode
        if (this.mode === 'position') {
            this.sequenceLength = this.config.PositionSequence?.length || 0;
        } else if (this.mode === 'word') {
            this.sequenceLength = this.config.WordSequence?.length || 0;
        } else {
            // Dual mode - use the longer one (should be same)
            this.sequenceLength = Math.max(
                this.config.PositionSequence?.length || 0,
                this.config.WordSequence?.length || 0
            );
        }

        this.state.totalSteps = this.sequenceLength;
        this.state.currentStep = 0;

        this.hits = 0;
        this.misses = 0;
        this.falseAlarms = 0;


    }

    start(): void {
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.state.isCompleted = false;
        this.startTime = Date.now();
        this.currentIndex = -1;

        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;
                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        this.callbacks.onStart();
        this.startPacer();
    }

    private startPacer(): void {
        this.expectedTime = Date.now();
        this.calculateNextStepTime();
        this.advanceStep();

        this.pacerInterval = setInterval(() => {
            if (!this.state.isPaused && Date.now() >= this.nextStepTime) {
                this.advanceStep();
            }
        }, 50);
    }

    private calculateNextStepTime(): void {
        this.expectedTime += this.config.SpeedMs;
        this.nextStepTime = this.expectedTime;

        if (this.nextStepTime < Date.now() - 2000) {
            this.expectedTime = Date.now();
            this.nextStepTime = this.expectedTime + this.config.SpeedMs;
        }
    }

    private advanceStep(): void {
        // Check for misses from previous step
        if (this.currentIndex >= 0) {
            this.checkMisses();
        }

        this.currentIndex++;
        this.hasRespondedPosition = false;
        this.hasRespondedWord = false;

        if (this.currentIndex >= this.sequenceLength) {
            this.complete();
            return;
        }

        // Get next values
        const nextPosition = this.config.PositionSequence?.[this.currentIndex] || 1;
        const nextWord = this.config.WordSequence?.[this.currentIndex] || '';

        // Check if position is same as current - need visual transition
        const needsTransition = (this.mode === 'position' || this.mode === 'dual') &&
            this.currentPosition === nextPosition &&
            this.currentIndex > 0;

        if (needsTransition) {
            // Brief blink to show new step
            this.isTransitioning = true;
            this.callbacks.onStateChange({ ...this.state });

            setTimeout(() => {
                this.isTransitioning = false;
                this.showStep(nextPosition, nextWord);
            }, 150); // 150ms blink
        } else {
            this.showStep(nextPosition, nextWord);
        }
    }

    private showStep(position: number, word: string): void {
        // Update current values based on mode
        if (this.mode === 'position' || this.mode === 'dual') {
            this.currentPosition = position;
        }

        if (this.mode === 'word' || this.mode === 'dual') {
            this.currentWord = word;
        }

        // 🔍 DEBUG: Detaylı log
        const isPositionTarget = this.config.PositionTargetIndices?.includes(this.currentIndex) ?? false;
        const isWordTarget = this.config.WordTargetIndices?.includes(this.currentIndex) ?? false;
        const nLevel = this.config.NLevel || 1;

        // N-Back kontrolü: N adım önceki değerle karşılaştır
        let nBackPosition = null;
        let nBackWord = null;
        if (this.currentIndex >= nLevel) {
            nBackPosition = this.config.PositionSequence?.[this.currentIndex - nLevel];
            nBackWord = this.config.WordSequence?.[this.currentIndex - nLevel];
        }



        this.state.currentStep = this.currentIndex;

        this.callbacks.onStateChange({ ...this.state });

        this.callbacks.onAction({
            action: 'step_change',
            data: {
                word: this.currentWord,
                position: this.currentPosition,
                mode: this.mode,
                level: this.config.NLevel
            }
        });

        this.calculateNextStepTime();
    }

    private checkMisses(): void {
        const idx = this.currentIndex;

        // Check position miss (for position and dual modes)
        if ((this.mode === 'position' || this.mode === 'dual') && !this.hasRespondedPosition) {
            if (this.config.PositionTargetIndices?.includes(idx)) {
                this.misses++;
                this.state.errors++;
                this.callbacks.onAction({ action: 'feedback', data: { type: 'miss', channel: 'position' } });
            }
        }

        // Check word miss (for word and dual modes)
        if ((this.mode === 'word' || this.mode === 'dual') && !this.hasRespondedWord) {
            if (this.config.WordTargetIndices?.includes(idx)) {
                this.misses++;
                this.state.errors++;
                this.callbacks.onAction({ action: 'feedback', data: { type: 'miss', channel: 'word' } });
            }
        }

        this.updateAccuracy();
        this.callbacks.onStateChange({ ...this.state });
    }

    handleInput(input: any): void {
        if (!this.state.isRunning || this.state.isPaused || this.state.isCompleted) return;

        // Handle position match (for position and dual modes)
        if (input.type === 'position_match') {
            if (this.hasRespondedPosition) return;
            this.hasRespondedPosition = true;

            const isTarget = this.config.PositionTargetIndices?.includes(this.currentIndex) ?? false;

            // 🔍 DEBUG: Kullanıcı tıklama logu
            const nLevel = this.config.NLevel || 1;
            const nBackPosition = this.currentIndex >= nLevel ? this.config.PositionSequence?.[this.currentIndex - nLevel] : null;


            if (isTarget) {
                this.hits++;
                this.state.score += 10;
                this.callbacks.onAction({ action: 'feedback', data: { type: 'correct', channel: 'position' } });
            } else {
                this.falseAlarms++;
                this.state.errors++;
                this.state.score = Math.max(0, this.state.score - 5);
                this.callbacks.onAction({ action: 'feedback', data: { type: 'wrong', channel: 'position' } });
            }

            this.callbacks.onAction({
                action: 'position_match',
                index: this.currentIndex
            });

            this.updateAccuracy();
            this.callbacks.onStateChange({ ...this.state });
        }

        // Handle word match (for word and dual modes)
        if (input.type === 'word_match' || input.type === 'match') {
            if (this.hasRespondedWord) return;
            this.hasRespondedWord = true;

            const isTarget = this.config.WordTargetIndices?.includes(this.currentIndex) ?? false;

            if (isTarget) {
                this.hits++;
                this.state.score += 10;
                this.callbacks.onAction({ action: 'feedback', data: { type: 'correct', channel: 'word' } });
            } else {
                this.falseAlarms++;
                this.state.errors++;
                this.state.score = Math.max(0, this.state.score - 5);
                this.callbacks.onAction({ action: 'feedback', data: { type: 'wrong', channel: 'word' } });
            }

            this.callbacks.onAction({
                action: 'word_match',
                index: this.currentIndex
            });

            this.updateAccuracy();
            this.callbacks.onStateChange({ ...this.state });
        }

        // Legacy: handle 'match' for single-mode backward compatibility
        if (input.type === 'match' && this.mode === 'position') {
            // Redirect to position match
            this.handleInput({ type: 'position_match' });
        }
    }

    private updateAccuracy(): void {
        const totalTrials = this.currentIndex + 1;
        if (totalTrials <= 0) return;

        const errors = this.falseAlarms + this.misses;
        this.state.accuracy = Math.round(100 * (1 - (errors / (totalTrials * (this.mode === 'dual' ? 2 : 1) || 1))));
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

        const pauseDuration = Date.now() - this.pauseStartTime;
        this.startTime += pauseDuration;
        this.expectedTime += pauseDuration;
        this.nextStepTime += pauseDuration;

        this.state.isPaused = false;
        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearInterval(this.pacerInterval);
    }

    reset(): void {
        this.stop();
        this.state = {
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            totalSteps: this.sequenceLength,
            score: 0,
            accuracy: 100,
            timeElapsed: 0,
            errors: 0
        };
        this.currentIndex = -1;
        this.currentWord = '';
        this.currentPosition = 0;
        this.hits = 0;
        this.misses = 0;
        this.falseAlarms = 0;
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.stop();
    }

    private complete(): void {
        // Check final step misses
        if (this.currentIndex > 0) {
            this.checkMisses();
        }

        this.state.isCompleted = true;
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearInterval(this.pacerInterval);

        this.callbacks.onAction({
            action: 'complete',
            timeMs: this.state.timeElapsed,
            timestamp: new Date()
        });

        this.callbacks.onStateChange({ ...this.state });

        // Call onComplete with results
        const result = this.getResult();
        this.callbacks.onComplete(result);
    }

    getResult(): any {
        const totalTargets = (this.config.PositionTargetIndices?.length || 0) +
            (this.config.WordTargetIndices?.length || 0);
        const totalTrials = this.sequenceLength;

        // Calculate accuracy based on hits, misses, and false alarms
        const correctResponses = this.hits;
        const incorrectResponses = this.misses + this.falseAlarms;
        const totalResponses = correctResponses + incorrectResponses;
        const accuracy = totalResponses > 0 ? (correctResponses / totalResponses) * 100 : 0;

        // D-prime calculation (signal detection theory) - simplified version
        const hitRate = totalTargets > 0 ? Math.min(0.99, Math.max(0.01, this.hits / totalTargets)) : 0.5;
        const falseAlarmRate = (totalTrials - totalTargets) > 0
            ? Math.min(0.99, Math.max(0.01, this.falseAlarms / (totalTrials - totalTargets)))
            : 0.5;

        // Z-score approximation for d-prime
        const zHit = this.zScore(hitRate);
        const zFA = this.zScore(falseAlarmRate);
        const dPrime = zHit - zFA;

        return {
            score: this.state.score,
            accuracy: Math.round(accuracy),
            totalTime: this.state.timeElapsed,
            totalSteps: totalTrials,
            completedSteps: this.currentIndex + 1,
            errors: this.state.errors,
            details: {
                mode: this.mode,
                nLevel: this.config.NLevel,
                hits: this.hits,
                misses: this.misses,
                falseAlarms: this.falseAlarms,
                totalTargets: totalTargets,
                dPrime: Math.round(dPrime * 100) / 100,
                hitRate: Math.round(hitRate * 100),
                falseAlarmRate: Math.round(falseAlarmRate * 100)
            }
        };
    }

    // Z-score approximation for d-prime calculation
    private zScore(p: number): number {
        // Approximation of inverse normal CDF
        if (p <= 0) return -3;
        if (p >= 1) return 3;

        const a1 = -3.969683028665376e+01;
        const a2 = 2.209460984245205e+02;
        const a3 = -2.759285104469687e+02;
        const a4 = 1.383577518672690e+02;
        const a5 = -3.066479806614716e+01;
        const a6 = 2.506628277459239e+00;

        const b1 = -5.447609879822406e+01;
        const b2 = 1.615858368580409e+02;
        const b3 = -1.556989798598866e+02;
        const b4 = 6.680131188771972e+01;
        const b5 = -1.328068155288572e+01;

        const c1 = -7.784894002430293e-03;
        const c2 = -3.223964580411365e-01;
        const c3 = -2.400758277161838e+00;
        const c4 = -2.549732539343734e+00;
        const c5 = 4.374664141464968e+00;
        const c6 = 2.938163982698783e+00;

        const d1 = 7.784695709041462e-03;
        const d2 = 3.224671290700398e-01;
        const d3 = 2.445134137142996e+00;
        const d4 = 3.754408661907416e+00;

        const pLow = 0.02425;
        const pHigh = 1 - pLow;

        let q, r;

        if (p < pLow) {
            q = Math.sqrt(-2 * Math.log(p));
            return (((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6) / ((((d1 * q + d2) * q + d3) * q + d4) * q + 1);
        } else if (p <= pHigh) {
            q = p - 0.5;
            r = q * q;
            return (((((a1 * r + a2) * r + a3) * r + a4) * r + a5) * r + a6) * q / (((((b1 * r + b2) * r + b3) * r + b4) * r + b5) * r + 1);
        } else {
            q = Math.sqrt(-2 * Math.log(1 - p));
            return -(((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6) / ((((d1 * q + d2) * q + d3) * q + d4) * q + 1);
        }
    }

    // Helper getters for UI
    get gridSize(): number {
        return this.config?.GridSize || 3;
    }

    get nLevel(): number {
        return this.config?.NLevel || 1;
    }

    get isPositionMode(): boolean {
        return this.mode === 'position' || this.mode === 'dual';
    }

    get isWordMode(): boolean {
        return this.mode === 'word' || this.mode === 'dual';
    }

    get isDualMode(): boolean {
        return this.mode === 'dual';
    }
}
