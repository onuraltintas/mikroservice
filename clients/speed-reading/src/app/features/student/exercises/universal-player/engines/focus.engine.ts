/**
 * Mental Registration Engine (Focus Engine)
 * 
 * Implements N-Back Working Memory training with 3 modes:
 * - "position": Grid-based spatial memory (easier)
 * - "word": Verbal memory with words (medium)  
 * - "dual": Both position and word simultaneously (hardest)
 */

import { BaseEngine, EngineConfig, EngineState, EngineCallbacks } from './base-engine.interface';
import { caseInsensitiveField, recordOrEmpty } from './reading-pacer-safety';

interface MentalRegistrationConfig extends EngineConfig {
    Mode: string;              // "position" | "word" | "dual"
    NLevel: number;            // 1-Back, 2-Back, etc.
    SpeedMs: number;           // Duration per item in ms
    GridSize: number;          // 3 for 3x3, 4 for 4x4
    AssessmentMode?: boolean;
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
    private assessmentMode = false;

    // Timing drift correction
    private expectedTime = 0;
    private nextStepTime = 0;
    private assessmentStepTimeout: any;
    private transitionTimeout: any;
    private pendingTransition: (() => void) | null = null;
    private transitionDeadline = 0;
    private transitionRemainingMs = 0;
    private assessmentRemainingMs = 0;
    private assessmentDeadline = 0;
    private awaitingAssessmentStep = false;
    private configurationError = '';

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.callbacks = callbacks;
        this.reset();
        this.configurationError = '';
        const root = recordOrEmpty(config);
        const nested = recordOrEmpty(caseInsensitiveField(root, 'engineConfig'));
        const session = recordOrEmpty(caseInsensitiveField(nested, 'sessionData') ?? caseInsensitiveField(root, 'sessionData'));
        const read = (...names: string[]) => {
            for (const source of [session, nested, root]) {
                for (const name of names) { const value = caseInsensitiveField(source, name); if (value !== undefined) return value; }
            }
            return undefined;
        };
        const backendData = {
            NLevel: read('NLevel', 'FocusNLevel'), WordSequence: read('WordSequence'), PositionSequence: read('PositionSequence'),
            TotalSteps: read('TotalSteps'), Mode: read('Mode', 'FocusMode'), SpeedMs: read('SpeedMs', 'FocusSpeedMs'), GridSize: read('GridSize'),
            IsAssessmentMode: root['previewOnly'] === true ? false : read('IsAssessmentMode', 'AssessmentMode')
        };
        const nLevel = Number(backendData.NLevel ?? 1);
        const wordSequence = Array.isArray(backendData.WordSequence)
            ? backendData.WordSequence
            : [];
        const positionSequence = Array.isArray(backendData.PositionSequence)
            ? backendData.PositionSequence
            : [];
        const configuredTotalSteps = Number(
            backendData.TotalSteps ?? 0);
        const assessmentMode = backendData.IsAssessmentMode === true
            ;

        this.config = {
            ...config,
            AssessmentMode: assessmentMode,
            Mode: String(backendData.Mode ?? 'position').toLowerCase(),
            NLevel: nLevel,
            SpeedMs: Number(backendData.SpeedMs ?? 1500),
            GridSize: Number(backendData.GridSize ?? 3),
            WordSequence: wordSequence,
            // Target arrays are optional presentation hints. The server is
            // always the scoring authority; public payloads omit configured
            // target indices while the N-back level remains visible because it
            // is the task rule the student must follow.
            WordTargetIndices: assessmentMode
                ? []
                : this.deriveTargetIndices(wordSequence, nLevel),
            PositionSequence: positionSequence,
            PositionTargetIndices: assessmentMode
                ? []
                : this.deriveTargetIndices(positionSequence, nLevel)
        } as MentalRegistrationConfig;

        this.mode = this.config.Mode;
        this.assessmentMode = assessmentMode;

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
        if (this.assessmentMode && configuredTotalSteps > 0) {
            this.sequenceLength = configuredTotalSteps;
        }

        const usesWords = this.mode === 'word' || this.mode === 'dual';
        const usesPositions = this.mode === 'position' || this.mode === 'dual';
        if (!['position', 'word', 'dual'].includes(this.mode)
            || !Number.isInteger(nLevel) || nLevel < 1 || nLevel > 5
            || !Number.isInteger(this.config.GridSize) || this.config.GridSize < 3 || this.config.GridSize > 7
            || !Number.isInteger(this.config.SpeedMs) || this.config.SpeedMs < 100 || this.config.SpeedMs > 10000
            || this.sequenceLength <= nLevel || this.sequenceLength > 500
            || (!assessmentMode && (usesWords && (wordSequence.length !== this.sequenceLength || wordSequence.some(word => typeof word !== 'string' || !word.trim()))
                || usesPositions && (positionSequence.length !== this.sequenceLength || positionSequence.some(position => !Number.isInteger(position) || position < 1 || position > this.config.GridSize ** 2))))) {
            this.configurationError = 'Odaklanma ayarları veya uyaran dizisi geçersiz. Lütfen ayarları kontrol edin.';
        }

        this.state.totalSteps = this.sequenceLength;
        this.state.currentStep = 0;

        this.hits = 0;
        this.misses = 0;
        this.falseAlarms = 0;


    }

    private deriveTargetIndices(sequence: readonly unknown[], nLevel: number): number[] {
        const targets: number[] = [];
        for (let index = Math.max(1, nLevel); index < sequence.length; index++) {
            if (sequence[index] === sequence[index - nLevel]) {
                targets.push(index);
            }
        }
        return targets;
    }

    start(): void {
        if (this.state.isRunning || this.state.isCompleted) return;
        if (this.configurationError || this.sequenceLength <= 0) {
            this.callbacks.onError(this.configurationError || 'N-back uyaran dizisi alınamadı. Egzersiz yapılandırmasını kontrol edin.');
            return;
        }

        this.state.isRunning = true;
        this.state.isPaused = false;
        this.state.isCompleted = false;
        this.startTime = Date.now();
        this.currentIndex = -1;
        this.awaitingAssessmentStep = false;

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
        if (this.assessmentMode) {
            this.advanceStep();
            return;
        }

        this.expectedTime = Date.now();
        this.advanceStep();
        if (!this.state.isRunning || this.state.isCompleted) return;

        this.pacerInterval = setInterval(() => {
            if (this.state.isRunning
                && !this.state.isPaused
                && !this.state.isCompleted
                && !this.isTransitioning
                && Date.now() >= this.nextStepTime) {
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

        if (this.assessmentMode) {
            this.awaitingAssessmentStep = true;
            this.callbacks.onAction({
                action: 'focus_step',
                index: this.currentIndex,
                timestamp: new Date()
            });
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

            this.pendingTransition = () => {
                this.isTransitioning = false;
                this.showStep(nextPosition, nextWord);
            };
            this.transitionRemainingMs = 150;
            this.scheduleTransition();
        } else {
            this.showStep(nextPosition, nextWord);
        }
    }

    private showStep(position: number | null | undefined, word: string | null | undefined): void {
        // Update current values based on mode
        if ((this.mode === 'position' || this.mode === 'dual') && position !== null && position !== undefined) {
            this.currentPosition = position;
        }

        if ((this.mode === 'word' || this.mode === 'dual') && word !== null && word !== undefined) {
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

        if (!this.assessmentMode) this.calculateNextStepTime();
    }

    private checkMisses(): void {
        if (this.assessmentMode) return;

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
        if (!this.state.isRunning || this.state.isPaused || this.state.isCompleted || this.isTransitioning) return;

        // Assessment attempts must not expose client-derived correctness or
        // feedback. The server records and scores the action; the client only
        // forwards the user's channel/index response.
        if (this.assessmentMode) {
            if (this.awaitingAssessmentStep || this.currentIndex < 0) return;

            const isPositionInput = input.type === 'position_match'
                || (input.type === 'match' && this.mode === 'position');
            const isWordInput = input.type === 'word_match'
                || (input.type === 'match' && this.mode !== 'position');

            if (isPositionInput && !this.hasRespondedPosition) {
                this.hasRespondedPosition = true;
                this.callbacks.onAction({ action: 'position_match', index: this.currentIndex });
            }
            if (isWordInput && !this.hasRespondedWord) {
                this.hasRespondedWord = true;
                this.callbacks.onAction({ action: 'word_match', index: this.currentIndex });
            }
            return;
        }

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

    /**
     * Applies the server's authoritative aggregate after a focus action.
     * Local feedback keeps the interaction responsive, while the persisted
     * counters are reconciled as soon as the validation response arrives.
     */
    reconcileServerResponse(action: any, response: any): void {
        if (!this.state.isRunning || this.state.isCompleted) return;
        if (this.assessmentMode) {
            const actionName = String(action?.action || '').toLowerCase();
            if (actionName === 'focus_step') {
                this.awaitingAssessmentStep = false;
                if (response?.isValid === false) {
                    this.callbacks.onError(response?.message || 'Focus uyaranı alınamadı.');
                    return;
                }

                const feedback = response?.feedbackData;
                if (!feedback || typeof feedback !== 'object') {
                    this.callbacks.onError('Focus uyaranı sunucudan eksik döndü.');
                    return;
                }

                this.showStep(
                    feedback.position == null ? undefined : Number(feedback.position),
                    feedback.word == null ? undefined : String(feedback.word));
                this.assessmentRemainingMs = Math.max(1, this.config.SpeedMs);
                this.scheduleNextAssessmentStep();
                return;
            }

            if (response?.isValid === false) {
                if (actionName === 'position_match') this.hasRespondedPosition = false;
                if (actionName === 'word_match') this.hasRespondedWord = false;
            }
            return;
        }

        const actionName = String(action?.action || '').toLowerCase();
        const feedback = response?.feedbackData;

        if (response?.isValid === false && (actionName === 'position_match' || actionName === 'word_match')) {
            const channel = actionName === 'position_match' ? 'position' : 'word';
            const index = Number(action?.index);
            const wasTarget = Number.isInteger(index)
                && (channel === 'position'
                    ? this.config.PositionTargetIndices?.includes(index)
                    : this.config.WordTargetIndices?.includes(index));

            if (wasTarget) {
                this.hits = Math.max(0, this.hits - 1);
                this.state.score = Math.max(0, this.state.score - 10);
            } else {
                this.falseAlarms = Math.max(0, this.falseAlarms - 1);
                this.state.errors = Math.max(0, this.state.errors - 1);
            }
            this.state.score = Math.max(0, this.hits * 10 - this.falseAlarms * 5);
            this.updateAccuracy();
            this.callbacks.onStateChange({ ...this.state });
            return;
        }

        if (!feedback || typeof feedback !== 'object') {
            return;
        }

        const hits = Number(feedback.hits);
        const misses = Number(feedback.misses);
        const falseAlarms = Number(feedback.falseAlarms);
        if (![hits, misses, falseAlarms].every(Number.isFinite)) {
            return;
        }

        this.hits = Math.max(0, Math.trunc(hits));
        this.misses = Math.max(0, Math.trunc(misses));
        this.falseAlarms = Math.max(0, Math.trunc(falseAlarms));
        this.state.errors = this.misses + this.falseAlarms;
        this.state.score = Math.max(0, this.hits * 10 - this.falseAlarms * 5);
        this.updateAccuracy();
        this.callbacks.onStateChange({ ...this.state });
    }

    private updateAccuracy(): void {
        const responses = this.hits + this.falseAlarms + this.misses;
        this.state.accuracy = responses > 0 ? Math.round(100 * this.hits / responses) : 0;
    }

    private scheduleTransition(): void {
        if (!this.pendingTransition || !this.state.isRunning || this.state.isPaused) return;
        this.transitionDeadline = Date.now() + this.transitionRemainingMs;
        this.transitionTimeout = setTimeout(() => {
            this.transitionTimeout = null;
            if (!this.state.isRunning || this.state.isPaused) return;
            const transition = this.pendingTransition;
            this.pendingTransition = null;
            transition?.();
        }, this.transitionRemainingMs);
    }

    private scheduleNextAssessmentStep(): void {
        if (this.assessmentStepTimeout) clearTimeout(this.assessmentStepTimeout);
        this.assessmentStepTimeout = null;
        if (!this.assessmentMode || !this.state.isRunning || this.state.isPaused || this.awaitingAssessmentStep)
            return;

        this.assessmentDeadline = Date.now() + this.assessmentRemainingMs;
        this.assessmentStepTimeout = setTimeout(() => {
            this.assessmentStepTimeout = null;
            if (this.state.isRunning && !this.state.isPaused) this.advanceStep();
        }, this.assessmentRemainingMs);
    }

    pause(): void {
        if (this.state.isPaused) return;
        this.state.isPaused = true;
        this.pauseStartTime = Date.now();
        if (this.transitionTimeout) {
            this.transitionRemainingMs = Math.max(0, this.transitionDeadline - Date.now());
            clearTimeout(this.transitionTimeout);
            this.transitionTimeout = null;
        }
        if (this.assessmentStepTimeout) {
            this.assessmentRemainingMs = Math.max(0, this.assessmentDeadline - Date.now());
            clearTimeout(this.assessmentStepTimeout);
            this.assessmentStepTimeout = null;
        }
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
        this.scheduleTransition();
        this.scheduleNextAssessmentStep();
        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearInterval(this.pacerInterval);
        if (this.assessmentStepTimeout) clearTimeout(this.assessmentStepTimeout);
        this.assessmentStepTimeout = null;
        if (this.transitionTimeout) clearTimeout(this.transitionTimeout);
        this.transitionTimeout = null;
        this.pendingTransition = null;
        this.isTransitioning = false;
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
        this.awaitingAssessmentStep = false;
        this.hits = 0;
        this.misses = 0;
        this.falseAlarms = 0;
        this.hasRespondedPosition = false;
        this.hasRespondedWord = false;
        this.callbacks.onStateChange?.({ ...this.state });
    }

    destroy(): void {
        this.stop();
    }

    private complete(): void {
        if (this.state.isCompleted) return;

        // Check final step misses
        if (this.currentIndex > 0) {
            this.checkMisses();
        }

        this.state.isCompleted = true;
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearInterval(this.pacerInterval);
        if (this.assessmentStepTimeout) clearTimeout(this.assessmentStepTimeout);
        this.assessmentStepTimeout = null;

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
            }
        };
    }


    // Helper getters for UI
    get gridSize(): number {
        return this.config?.GridSize || 3;
    }

    get nLevel(): number {
        return this.config?.NLevel || 1;
    }

    get isAssessmentMode(): boolean {
        return this.assessmentMode;
    }

    get isAwaitingAssessmentStep(): boolean {
        return this.awaitingAssessmentStep;
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
