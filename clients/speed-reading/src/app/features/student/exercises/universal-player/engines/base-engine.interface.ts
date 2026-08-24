/**
 * Universal Exercise Engine - Base Interface
 * Tüm mini engine'ler bu interface'i implement eder.
 */

export interface EngineConfig {
    [key: string]: any;
}

export interface EngineState {
    isRunning: boolean;
    isPaused: boolean;
    isCompleted: boolean;
    currentStep: number;
    totalSteps: number;
    score: number;
    accuracy: number;
    timeElapsed: number; // ms
    errors: number;
    currentValue?: string; // Saccade/Fixation hedeflerinde gösterilecek değer
    metronomeStep?: number; // 1-2-3-4 ritmi için
    metronomeBeat?: boolean; // Görsel blink için
    actualChunkSize?: number; // Satır sonuna göre dinamikleşen grup boyutu
    countdown?: number; // Başlangıç geri sayımı için
    currentWPM?: number; // Anlık okuma hızı (kelime/dakika)
    targetWPM?: number; // Hedef okuma hızı
    remainingSeconds?: number; // Geri sayım (kalan süre)
    targetCount?: number; // Tamamlanan hedef sayısı
    isLastTenSeconds?: boolean; // Son 10 saniyede mi
}

export interface EngineResult {
    score: number;
    accuracy: number;
    totalTime: number;
    totalSteps: number;
    completedSteps: number;
    errors: number;
    details: any;
}

export interface EngineCallbacks {
    onStart: () => void;
    onPause: () => void;
    onResume: () => void;
    onComplete: (result: EngineResult) => void;
    onError: (error: string) => void;
    onStateChange: (state: EngineState) => void;
    onStepComplete: (step: number, correct: boolean) => void;
    onAction: (action: any) => void; // Backend'e aksiyon bildirmek için
}

export interface BaseEngine {
    // Engine bilgileri
    readonly engineType: string;
    readonly displayName: string;

    // Durum
    state: EngineState;

    // Lifecycle
    initialize(config: EngineConfig, callbacks: EngineCallbacks): void;
    start(): void;
    pause(): void;
    resume(): void;
    stop(): void;
    reset(): void;
    destroy(): void;

    // Kullanıcı etkileşimi
    handleInput(input: any): void;
}
