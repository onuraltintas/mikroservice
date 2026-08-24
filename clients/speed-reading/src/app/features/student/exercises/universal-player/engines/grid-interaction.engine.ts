/**
 * Grid Interaction Engine
 * Schulte Table ve benzeri grid tabanlı egzersizler için.
 * 
 * Bilimsel standartlara uygun özellikler:
 * - Fixation point (merkez odaklanma noktası)
 * - Heatmap (ısı haritası) - tıklama sürelerini takip
 * - Benchmark (performans karşılaştırması)
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';

export interface GridInteractionConfig extends EngineConfig {
    gridSize: number;           // 3, 4, 5, 6, 7
    sequenceType: string;       // 'numeric', 'alphabetic', 'mixed'
    timeLimit?: number;         // saniye
    showHints?: boolean;
    highlightOnClick?: boolean;
    showFixationPoint?: boolean; // Merkez odaklanma noktası
}

// Hücre tıklama verisi
export interface CellClickData {
    cellIndex: number;
    value: string | number;
    timestamp: number;
    responseTime: number;  // Bu hücreyi bulmak için geçen süre (ms)
    isCorrect: boolean;
    row: number;
    col: number;
}

// Performans benchmark seviyeleri (5x5 grid için, ms cinsinden)
export const SCHULTE_BENCHMARKS: { [gridSize: number]: { beginner: number; intermediate: number; advanced: number; expert: number } } = {
    3: { beginner: 15000, intermediate: 10000, advanced: 7000, expert: 5000 },
    4: { beginner: 30000, intermediate: 20000, advanced: 15000, expert: 10000 },
    5: { beginner: 60000, intermediate: 45000, advanced: 30000, expert: 20000 },
    6: { beginner: 90000, intermediate: 70000, advanced: 50000, expert: 35000 },
    7: { beginner: 120000, intermediate: 90000, advanced: 70000, expert: 50000 }
};

export class GridInteractionEngine implements BaseEngine {
    readonly engineType = 'grid_interaction';
    readonly displayName = 'Grid Etkileşim';

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

    private config!: GridInteractionConfig;
    private callbacks!: EngineCallbacks;
    private grid: (string | number)[] = [];
    private sequence: (string | number)[] = [];
    private currentTarget = 0;
    private startTime = 0;
    private lastClickTime = 0;
    private pauseStartTime = 0;
    private timerInterval: any;
    private correctClicks = 0;
    private totalClicks = 0;

    // Heatmap verileri
    private clickHistory: CellClickData[] = [];
    private cellResponseTimes: number[] = []; // Her hücre için tepki süresi

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.config = config as GridInteractionConfig;
        this.callbacks = callbacks;
        this.generateGrid();
        this.state.totalSteps = this.sequence.length;

        // Heatmap için boş dizi oluştur
        this.cellResponseTimes = new Array(this.sequence.length).fill(0);
        this.clickHistory = [];
        this.correctClicks = 0;
        this.totalClicks = 0;
        this.state.timeElapsed = 0;

    }

    private generateGrid(): void {
        const size = this.config.gridSize || 5;
        const totalCells = size * size;

        if (this.config.sequenceType === 'alphabetic') {
            this.sequence = Array.from({ length: totalCells }, (_, i) =>
                String.fromCharCode(65 + i) // A, B, C...
            );
        } else {
            this.sequence = Array.from({ length: totalCells }, (_, i) => i + 1);
        }

        // Shuffle for grid display
        this.grid = [...this.sequence].sort(() => Math.random() - 0.5);
    }

    start(): void {
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.startTime = Date.now();
        this.lastClickTime = Date.now();
        this.currentTarget = 0;
        this.clickHistory = [];
        this.correctClicks = 0;
        this.totalClicks = 0;

        if (this.timerInterval) {
            clearInterval(this.timerInterval);
        }

        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;
                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        this.callbacks.onStart();
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

        // Pause süresini hesaba kat ve startTime'ı ileri kaydır
        const pauseDuration = Date.now() - this.pauseStartTime;
        this.startTime += pauseDuration;

        this.state.isPaused = false;
        this.lastClickTime = Date.now();
        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        this.callbacks.onStateChange({ ...this.state });
    }

    reset(): void {
        this.stop();
        this.state = {
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            totalSteps: this.sequence.length,
            score: 0,
            accuracy: 100,
            timeElapsed: 0,
            errors: 0
        };
        this.currentTarget = 0;
        this.correctClicks = 0;
        this.totalClicks = 0;
        this.clickHistory = [];
        this.cellResponseTimes = new Array(this.sequence.length).fill(0);
        this.generateGrid();
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.stop();
    }

    handleInput(input: { cellIndex: number; value: string | number }): void {
        if (!this.state.isRunning || this.state.isPaused || this.state.isCompleted) {
            return;
        }

        const now = Date.now();
        const responseTime = now - this.lastClickTime;
        this.totalClicks++;

        const expectedValue = this.sequence[this.currentTarget];
        const isCorrect = input.value === expectedValue;
        const gridSize = this.config.gridSize || 5;

        // Tıklama verisini kaydet
        const clickData: CellClickData = {
            cellIndex: input.cellIndex,
            value: input.value,
            timestamp: now,
            responseTime: responseTime,
            isCorrect: isCorrect,
            row: Math.floor(input.cellIndex / gridSize),
            col: input.cellIndex % gridSize
        };
        this.clickHistory.push(clickData);

        if (isCorrect) {
            this.correctClicks++;
            this.cellResponseTimes[input.cellIndex] = responseTime;
            this.lastClickTime = now;
            this.currentTarget++;
            this.state.currentStep = this.currentTarget;
            this.state.score = Math.round((this.correctClicks / this.sequence.length) * 100);

            this.callbacks.onStepComplete(this.currentTarget, true);

            // Tamamlandı mı?
            if (this.currentTarget >= this.sequence.length) {
                this.complete();
            }
        } else {
            this.state.errors++;
            this.callbacks.onStepComplete(this.currentTarget, false);
        }

        this.state.accuracy = this.totalClicks > 0
            ? Math.round((this.correctClicks / this.totalClicks) * 100)
            : 100;

        this.callbacks.onStateChange({ ...this.state });
    }

    private complete(): void {
        this.state.isCompleted = true;
        this.state.isRunning = false;
        clearInterval(this.timerInterval);

        const gridSize = this.config.gridSize || 5;
        const benchmarks = SCHULTE_BENCHMARKS[gridSize] || SCHULTE_BENCHMARKS[5];

        // Performans seviyesini belirle
        let performanceLevel: string;
        let performanceScore: number;

        if (this.state.timeElapsed <= benchmarks.expert) {
            performanceLevel = 'Uzman';
            performanceScore = 100;
        } else if (this.state.timeElapsed <= benchmarks.advanced) {
            performanceLevel = 'İleri';
            performanceScore = 85;
        } else if (this.state.timeElapsed <= benchmarks.intermediate) {
            performanceLevel = 'Orta';
            performanceScore = 70;
        } else if (this.state.timeElapsed <= benchmarks.beginner) {
            performanceLevel = 'Başlangıç';
            performanceScore = 55;
        } else {
            performanceLevel = 'Geliştirilmeli';
            performanceScore = 40;
        }

        // Heatmap verisi oluştur (normalized 0-1)
        const maxResponseTime = Math.max(...this.cellResponseTimes.filter(t => t > 0), 1);
        const heatmapData = this.cellResponseTimes.map(time =>
            time > 0 ? time / maxResponseTime : 0
        );

        const result: EngineResult = {
            score: this.state.score,
            accuracy: this.state.accuracy,
            totalTime: this.state.timeElapsed,
            totalSteps: this.sequence.length,
            completedSteps: this.currentTarget,
            errors: this.state.errors,
            details: {
                gridSize: this.config.gridSize,
                sequenceType: this.config.sequenceType,
                avgTimePerStep: this.state.timeElapsed / this.sequence.length,
                // Yeni bilimsel veriler
                performanceLevel: performanceLevel,
                performanceScore: performanceScore,
                benchmarks: benchmarks,
                heatmapData: heatmapData,
                clickHistory: this.clickHistory,
                slowestCells: this.getSlowestCells(3),
                fastestCells: this.getFastestCells(3)
            }
        };

        this.callbacks.onComplete(result);
    }

    // En yavaş N hücreyi bul
    private getSlowestCells(n: number): { index: number; time: number }[] {
        return this.cellResponseTimes
            .map((time, index) => ({ index, time }))
            .filter(cell => cell.time > 0)
            .sort((a, b) => b.time - a.time)
            .slice(0, n);
    }

    // En hızlı N hücreyi bul
    private getFastestCells(n: number): { index: number; time: number }[] {
        return this.cellResponseTimes
            .map((time, index) => ({ index, time }))
            .filter(cell => cell.time > 0)
            .sort((a, b) => a.time - b.time)
            .slice(0, n);
    }

    // Public API for component
    getGrid(): (string | number)[] {
        return this.grid;
    }

    getCurrentTarget(): string | number {
        return this.sequence[this.currentTarget];
    }

    getGridSize(): number {
        return this.config.gridSize || 5;
    }

    getHeatmapData(): number[] {
        return this.cellResponseTimes;
    }

    getBenchmarks(): { beginner: number; intermediate: number; advanced: number; expert: number } {
        const gridSize = this.config.gridSize || 5;
        return SCHULTE_BENCHMARKS[gridSize] || SCHULTE_BENCHMARKS[5];
    }
}

