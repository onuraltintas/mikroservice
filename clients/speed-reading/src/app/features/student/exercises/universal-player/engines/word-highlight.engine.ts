/**
 * Word Highlight Engine
 * Speed Reading, Chunking ve Skimming egzersizleri için.
 * Metin üzerinde kelime veya öbek bazlı vurgulama (pacer) yapar.
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';

export interface WordHighlightConfig extends EngineConfig {
    content: {
        source: string;
        text?: string;
        wordCount?: number;
    };
    pacer: {
        speedWpm: number;
        chunkSize: number;
        autoScroll: boolean;
        fixationType: string;
    };
    visuals: {
        fontSize: string;
        lineHeight: number;
        fontFamily: string;
        width: string;
    };
    timing?: {
        timeLimitSec?: number;
    };
    mode?: 'speed_reading' | 'chunking' | 'skimming';
}

export interface ChunkInfo {
    words: string[];
    hasNewline: boolean;
    startIndex: number;
}

export class WordHighlightEngine implements BaseEngine {
    readonly engineType = 'word_highlight';
    readonly displayName = 'Hızlı Okuma';

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

    private config!: WordHighlightConfig;
    private callbacks!: EngineCallbacks;
    private startTime = 0;
    private pauseStartTime = 0;
    private timerInterval: any;
    private pacerInterval: any;

    private chunks: ChunkInfo[] = [];
    private currentChunkIdx = 0;
    private totalChunks = 0;
    private nextChunkTime = 0;
    private allWords: string[] = [];

    private static readonly TEXT_POOL = [
        "Hızlı okuma becerisi, bilgi çağında hayati bir yetenektir.",
        "Göz kaslarını geliştirmek için düzenli egzersiz yapmak gerekir.",
        "Periferik görüş alanını genişleterek daha fazla kelime görebilirsiniz.",
        "Odaklanma süresini artırmak, okuma verimliliğini doğrudan etkiler.",
        "Beyin, görsel bilgiyi işleme konusunda olağanüstü bir kapasiteye sahiptir."
    ];

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.config = config as WordHighlightConfig;
        this.callbacks = callbacks;

        const backend = config as any;
        this.chunks = [];
        this.allWords = [];
        let wordPointer = 0;

        // Populate chunks from backend primary source
        if (backend.Chunks && Array.isArray(backend.Chunks) && backend.Chunks.length > 0) {
            backend.Chunks.forEach((chunkStr: string) => {
                const words = chunkStr.split(' ').filter(w => w.length > 0);
                if (words.length > 0) {
                    const cleanWords = words.map(w => w.trim());
                    this.chunks.push({
                        words: cleanWords,
                        hasNewline: chunkStr.includes('\n'),
                        startIndex: wordPointer
                    });
                    this.allWords.push(...cleanWords);
                    wordPointer += cleanWords.length;
                }
            });
        } else {
            // Fallback to text splitting logic
            const text = this.config.content?.text || WordHighlightEngine.TEXT_POOL.join(' ');
            const rawWords = text.split(/\s+/).filter(w => w.length > 0);
            const cs = backend.ChunkSize || this.config.pacer?.chunkSize || 1;

            for (let i = 0; i < rawWords.length; i += cs) {
                const words = rawWords.slice(i, i + cs);
                this.chunks.push({
                    words: words,
                    hasNewline: false,
                    startIndex: i
                });
                this.allWords.push(...words);
            }
        }

        this.totalChunks = this.chunks.length;
        this.state.totalSteps = this.totalChunks;
        this.state.currentStep = 0;
        this.currentChunkIdx = 0;

        // Setup pacer defaults
        if (!this.config.pacer) {
            this.config.pacer = {
                speedWpm: backend.TargetWpm || 200,
                chunkSize: backend.ChunkSize || 1,
                autoScroll: true,
                fixationType: 'highlight'
            };
        }


    }

    start(): void {
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.state.isCompleted = false;
        this.startTime = Date.now();
        this.currentChunkIdx = 0;

        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;
                if (this.config.timing?.timeLimitSec && this.state.timeElapsed >= this.config.timing.timeLimitSec * 1000) {
                    this.complete();
                }
                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        this.callbacks.onStart();
        this.startPacer();
    }

    private expectedTime = 0;

    private startPacer(): void {
        this.expectedTime = Date.now();
        this.calculateNextChunkTime();
        this.pacerInterval = setInterval(() => {
            if (!this.state.isPaused && Date.now() >= this.nextChunkTime) {
                this.advancePacer();
            }
        }, 30); // Higher frequency for better accuracy
    }

    private calculateNextChunkTime(): void {
        const wpm = this.config.pacer?.speedWpm || 200;
        const currentSize = this.chunks[this.currentChunkIdx]?.words.length || 1;
        const ms = (60000 / wpm) * currentSize;

        // Accumulator approach to prevent drift
        this.expectedTime += ms;
        this.nextChunkTime = this.expectedTime;

        // Safety check: if we are way behind (e.g. tab was inactive), jump ahead
        if (this.nextChunkTime < Date.now() - 2000) {
            this.expectedTime = Date.now();
            this.nextChunkTime = this.expectedTime + ms;
        }
    }

    private advancePacer(): void {
        if (!this.state.isRunning || this.state.isPaused) return;

        this.currentChunkIdx++;
        if (this.currentChunkIdx >= this.totalChunks) {
            this.complete();
            return;
        }

        this.state.currentStep = this.currentChunkIdx;
        this.callbacks.onStepComplete(this.state.currentStep, true);
        this.callbacks.onStateChange({ ...this.state });
        this.calculateNextChunkTime();
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

        // Shift expected time forward by pause duration code
        this.expectedTime += pauseDuration;
        this.nextChunkTime += pauseDuration;

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
            ...this.state,
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            timeElapsed: 0
        };
        this.currentChunkIdx = 0;
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void { this.stop(); }
    handleInput(input: any): void { }

    private complete(): void {
        this.state.isCompleted = true;
        this.state.isRunning = false;
        this.state.currentStep = this.state.totalSteps;
        this.stop();
        this.callbacks.onStateChange({ ...this.state });

        const result: EngineResult = {
            score: 100,
            accuracy: 100,
            totalTime: this.state.timeElapsed,
            totalSteps: this.state.totalSteps,
            completedSteps: this.state.totalSteps,
            errors: 0,
            details: {
                wpm: this.config.pacer?.speedWpm,
                chunkCount: this.totalChunks,
                mode: this.config.mode || 'chunking'
            }
        };
        this.callbacks.onComplete(result);
    }

    // Public API
    getChunks(): ChunkInfo[] { return this.chunks; }
    getCurrentChunkIndex(): number { return this.currentChunkIdx; }
    getWords(): string[] { return this.allWords; }
    getCurrentWordIndex(): number { return this.chunks[this.currentChunkIdx]?.startIndex || 0; }
    getChunkSize(): number { return this.chunks[this.currentChunkIdx]?.words.length || 1; }
    getWpm(): number { return this.config.pacer?.speedWpm || 200; }
    getHighlightFontSize(): string { return this.config.visuals?.fontSize || 'medium'; }
}
