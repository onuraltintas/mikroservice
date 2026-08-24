/**
 * Text Fade Engine (Scientific Version)
 * World Standard: Fading tracks the reader based on Target WPM.
 * Forces the eye to move forward and prevents regression.
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';

export interface TextFadeConfig extends EngineConfig {
    content: {
        text?: string;
        wordCount?: number;
    };
    fading: {
        speedWpm: number;
        lagMs: number;
    };
    visuals: {
        fontSize: string;
    };
}

export class TextFadeEngine implements BaseEngine {
    readonly engineType = 'text_fade';
    readonly displayName = 'Kaybolan Metin';

    state: EngineState = {
        isRunning: false,
        isPaused: false,
        isCompleted: false,
        currentStep: 0,
        totalSteps: 0,
        score: 0,
        accuracy: 0,
        timeElapsed: 0,
        errors: 0
    };

    private config!: TextFadeConfig;
    private callbacks!: EngineCallbacks;
    private startTime = 0;
    private pauseStartTime = 0;
    private timerInterval: any;
    private fadeInterval: any;

    private words: string[] = [];
    private fadedWordIndex = -1;
    private nextFadeTime = 0;

    private static readonly TEXT_POOL = [
        "Hızlı okuma becerisi, bilgi çağında hayati bir yetenektir.",
        "Göz kaslarını geliştirmek için düzenli egzersiz yapmak gerekir.",
        "Periferik görüş alanını genişleterek daha fazla kelime görebilirsiniz.",
        "Odaklanma süresini artırmak, okuma verimliliğini doğrudan etkiler.",
        "Beyin, görsel bilgiyi işleme konusunda olağanüstü bir kapasiteye sahiptir."
    ];

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.config = config as TextFadeConfig;
        this.callbacks = callbacks;

        const backend = config as any;

        // Prepare words
        const text = backend.ReadingTextContent || this.config.content?.text || TextFadeEngine.TEXT_POOL.join(' ');
        this.words = text.split(/\s+/).filter((w: string) => w.length > 0);

        // Determine Speed (WPM)
        if (!this.config.fading) {
            this.config.fading = {
                speedWpm: backend.TargetWpm || 200,
                lagMs: backend.LagMs || 3000
            };
        } else {
            this.config.fading.speedWpm = backend.TargetWpm || this.config.fading.speedWpm || 200;
            this.config.fading.lagMs = backend.LagMs || this.config.fading.lagMs || 3000;
        }

        this.state.totalSteps = this.words.length;
        this.state.currentStep = 0;
        this.state.targetWPM = this.config.fading.speedWpm;
        this.state.currentWPM = 0;
        this.fadedWordIndex = -1;


    }

    start(): void {
        if (this.state.isRunning) return;

        this.state.isRunning = true;
        this.state.isPaused = false;
        this.startTime = Date.now();
        this.fadedWordIndex = -1;
        this.state.currentStep = 0;

        // Global Timer with WPM calculation
        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;
                // Calculate current WPM based on words read
                const elapsedMinutes = this.state.timeElapsed / 60000;
                if (elapsedMinutes > 0 && this.state.currentStep > 0) {
                    this.state.currentWPM = Math.round(this.state.currentStep / elapsedMinutes);
                }
                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        this.callbacks.onStart();

        // Initialize countdown
        this.config.fading.lagMs = 3000; // Force 3 seconds
        this.state.countdown = 3;
        this.callbacks.onStateChange({ ...this.state });

        // Countdown interval
        const countdownInterval = setInterval(() => {
            if (this.state.isPaused) return;

            if (this.state.countdown !== undefined && this.state.countdown > 1) {
                this.state.countdown--;
                this.callbacks.onStateChange({ ...this.state });
            } else {
                this.state.countdown = 0;
                this.callbacks.onStateChange({ ...this.state });
                clearInterval(countdownInterval);
                this.startFading();
            }
        }, 1000);
    }

    private expectedTime = 0;

    private startFading(): void {
        this.expectedTime = Date.now();
        this.calculateNextFadeTime();

        // High-precision checker
        this.fadeInterval = setInterval(() => {
            if (!this.state.isPaused && Date.now() >= this.nextFadeTime) {
                this.advanceFade();
            }
        }, 50);
    }

    private calculateNextFadeTime(): void {
        const wpm = this.config.fading.speedWpm || 200;
        const msPerWord = 60000 / wpm;

        // Accumulator
        this.expectedTime += msPerWord;
        this.nextFadeTime = this.expectedTime;

        // Safety check
        if (this.nextFadeTime < Date.now() - 2000) {
            this.expectedTime = Date.now();
            this.nextFadeTime = this.expectedTime + msPerWord;
        }
    }

    private advanceFade(): void {
        if (!this.state.isRunning || this.state.isPaused) return;

        this.fadedWordIndex++;
        this.state.currentStep = this.fadedWordIndex + 1;

        if (this.fadedWordIndex >= this.words.length - 1) {
            this.complete();
            return;
        }

        this.callbacks.onStepComplete(this.state.currentStep, true);
        this.callbacks.onStateChange({ ...this.state });
        this.calculateNextFadeTime();
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

        // Adjust expected time
        this.expectedTime += pauseDuration;
        this.nextFadeTime += pauseDuration;

        this.state.isPaused = false;
        // calculateNextFadeTime call removed as nextFadeTime is shifted

        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearInterval(this.fadeInterval);
    }

    reset(): void {
        this.stop();
        this.state = {
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            totalSteps: this.words.length,
            score: 0,
            accuracy: 0,
            timeElapsed: 0,
            errors: 0
        };
        this.fadedWordIndex = -1;
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void { this.stop(); }
    handleInput(input: any): void { }

    private complete(): void {
        this.state.isCompleted = true;
        this.state.isRunning = false;
        this.stop();
        this.callbacks.onStateChange({ ...this.state });

        const result: EngineResult = {
            score: 100,
            accuracy: 100,
            totalTime: this.state.timeElapsed,
            totalSteps: this.state.totalSteps,
            completedSteps: this.state.currentStep,
            errors: 0,
            details: {
                wpm: this.config.fading.speedWpm,
                mode: 'vanishing_text'
            }
        };
        this.callbacks.onComplete(result);
    }

    // Public API
    getWords(): string[] { return this.words; }
    getFadedIndex(): number { return this.fadedWordIndex; }
    getActiveIndex(): number { return this.fadedWordIndex + 1; }
    getWpm(): number { return this.config.fading.speedWpm || 200; }
    getFontSize(): string { return (this.config.visuals as any)?.fontSize || 'medium'; }
}
