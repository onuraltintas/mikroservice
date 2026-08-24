/**
 * Visual Expansion Engine
 * Görsel Genişletme / Periferik Görüş egzersizleri için.
 * Merkezde fiksasyon noktası, kenarlarda anlık beliren uyaranlar.
 * 
 * Bilimsel Temel: Kullanıcının periferik görüşünü genişletmek için
 * görüş açısı (derece) tabanlı hesaplama ve adaptif zorluk kullanır.
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';
import { ScreenHelper } from '../../../../../core/utils/screen-helper';

export interface VisualExpansionConfig extends EngineConfig {
    expansion: {
        level: number;
        pattern: 'horizontal' | 'vertical' | 'radial' | 'random';
        stimulusType: 'letter' | 'number' | 'symbol' | 'word';
        symmetry: boolean;
        startDegrees?: number;
    };
    timing: {
        durationMs: number;
        intervalMs: number;
    };
    visuals: {
        centerPoint: 'cross' | 'dot' | 'circle';
        stimulusSize: string;
    };
}

export class VisualExpansionEngine implements BaseEngine {
    readonly engineType = 'visual_expansion';
    readonly displayName = 'Görsel Genişletme';

    state: EngineState = {
        isRunning: false,
        isPaused: false,
        isCompleted: false,
        currentStep: 0,
        totalSteps: 20,
        score: 0,
        accuracy: 0,
        timeElapsed: 0,
        errors: 0
    };

    private config!: VisualExpansionConfig;
    private callbacks!: EngineCallbacks;
    private startTime = 0;
    private pauseStartTime = 0;
    private timerInterval: any;
    private stimulusInterval: any;
    private hideTimeout: any;

    // Bilimsel Durum
    currentDegrees = 5;
    private successStreak = 0;
    private failStreak = 0;
    private maxDegreesReached = 5;
    private correctAnswers = 0;
    private totalAnswers = 0;

    // Detaylı tur sonuçları
    private roundResults: Array<{
        round: number;
        degrees: number;
        correct: boolean;
        leftChar: string;
        rightChar: string;
        userLeftAnswer: string;
        userRightAnswer: string;
        responseTimeMs: number;
        timestamp: string;
    }> = [];
    private stimulusShownTime = 0;

    // Stimulus State
    currentStimuli: Array<{ content: string; x: number; y: number }> = [];
    lastShownStimuli: string[] = []; // Doğrulama için saklanan karakterler
    isStimulusVisible = false;
    isWaitingForInput = false;

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.config = config as VisualExpansionConfig;
        this.callbacks = callbacks;

        // Backend'den gelen yaş ve zorluk bazlı parametreleri al
        // Backend hem PascalCase hem camelCase gönderebilir
        const backendConfig = config as any;

        // Başlangıç açısı (backend'den veya varsayılan)
        this.currentDegrees = backendConfig.StartDegrees
            || backendConfig.startDegrees
            || this.config.expansion?.startDegrees
            || (2 + (this.config.expansion?.level || 1) * 2);

        // Hedef açı (kullanım için sakla)
        const targetDegrees = backendConfig.TargetDegrees || backendConfig.targetDegrees || 30;

        // Tur sayısı
        this.state.totalSteps = backendConfig.Rounds || backendConfig.rounds || 20;

        // Gösterim süresi (backend'den zorluk bazlı)
        const displayDurationMs = backendConfig.DisplayDurationMs || backendConfig.displayDurationMs;
        if (displayDurationMs) {
            this.config.timing = this.config.timing || {} as any;
            this.config.timing.durationMs = displayDurationMs;
        }

        // Yaş grubu ve zorluk seviyesi
        // Backend artık AgeGroupConfiguration tablosundan dinamik olarak yaş grubu adını gönderiyor
        let ageGroup: string | undefined = backendConfig.AgeGroup || backendConfig.ageGroup;

        // "Unknown" değerini de falsy olarak ele al
        const isAgeGroupValid = ageGroup && ageGroup !== 'Unknown';

        // Metadata içinden kontrol et (eski format için geriye dönük uyumluluk)
        if (!isAgeGroupValid && backendConfig.metadata?.ageGroup) {
            ageGroup = backendConfig.metadata.ageGroup;
        }

        // Fallback: Eğer hala bulunamadıysa, startDegrees'e göre tahmin et
        if (!ageGroup || ageGroup === 'Unknown') {
            const start = this.currentDegrees;
            if (start <= 8) ageGroup = "Child";
            else if (start <= 12) ageGroup = "Teen";
            else ageGroup = "Adult";
        }

        const difficultyLevel = backendConfig.DifficultyLevel || backendConfig.difficultyLevel;

        this.maxDegreesReached = this.currentDegrees;
        this.correctAnswers = 0;
        this.totalAnswers = 0;
    }

    start(): void {
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.startTime = Date.now();
        this.state.currentStep = 0;

        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;
                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        this.callbacks.onStart();
        this.callbacks.onStateChange({ ...this.state });

        this.scheduleNextStimulus();
    }

    private scheduleNextStimulus(): void {
        if (this.state.currentStep >= this.state.totalSteps) {
            this.complete();
            return;
        }

        if (this.state.isPaused) return;

        const interval = this.config.timing?.intervalMs || 1500;
        this.stimulusInterval = setTimeout(() => {
            if (this.state.isRunning && !this.state.isPaused) {
                this.showStimulus();
            }
        }, interval);
    }

    private showStimulus(): void {
        this.generateStimuli();
        this.isStimulusVisible = true;
        this.isWaitingForInput = false;
        this.callbacks.onStateChange({ ...this.state });

        const duration = this.config.timing?.durationMs || 250;

        this.hideTimeout = setTimeout(() => {
            this.hideStimulus();
        }, duration);
    }

    private hideStimulus(): void {
        // Karakterleri sakla (doğrulama için)
        this.lastShownStimuli = this.currentStimuli.map(s => s.content);

        // Tepki süresi ölçümü için başlangıç zamanı
        this.stimulusShownTime = Date.now();

        this.isStimulusVisible = false;
        this.currentStimuli = [];
        this.isWaitingForInput = true;
        this.callbacks.onStateChange({ ...this.state });
    }

    /**
     * Kullanıcı cevabını doğrula
     * @param answers Kullanıcının girdiği karakterler (sol, sağ sırasıyla)
     */
    handleInput(input: { answers?: string[] }): void {
        if (!this.isWaitingForInput || !input.answers) return;

        const userAnswers = input.answers.map(a => a.toUpperCase().trim());
        const correctAnswers = this.lastShownStimuli.map(s => s.toUpperCase());

        // Tepki süresini hesapla
        const responseTimeMs = Date.now() - this.stimulusShownTime;

        // Her bir karakteri karşılaştır
        let correctCount = 0;
        for (let i = 0; i < Math.min(userAnswers.length, correctAnswers.length); i++) {
            if (userAnswers[i] === correctAnswers[i]) {
                correctCount++;
            }
        }

        const isFullyCorrect = correctCount === correctAnswers.length;
        this.totalAnswers++;

        // Detaylı tur sonucunu kaydet
        this.roundResults.push({
            round: this.state.currentStep + 1,
            degrees: this.currentDegrees,
            correct: isFullyCorrect,
            leftChar: correctAnswers[0] || '',
            rightChar: correctAnswers[1] || '',
            userLeftAnswer: userAnswers[0] || '',
            userRightAnswer: userAnswers[1] || '',
            responseTimeMs,
            timestamp: new Date().toISOString()
        });

        if (isFullyCorrect) {
            this.correctAnswers++;

            // Her iki doğru cevapta bir açıyı (zorluğu) genişlet
            if (this.correctAnswers > 0 && this.correctAnswers % 2 === 0) {
                // Maksimum 60 dereceye kadar genişlet
                this.currentDegrees = Math.min(60, this.currentDegrees + 1.5);
            }

            this.successStreak++;
            this.failStreak = 0;
        } else {
            // Yanlış cevapta hız/açı düşmez, olduğu yerde kalır
            this.failStreak++;
            this.successStreak = 0;
            this.state.errors++;
        }

        this.maxDegreesReached = Math.max(this.maxDegreesReached, this.currentDegrees);
        this.state.currentStep++;
        this.state.accuracy = this.totalAnswers > 0
            ? Math.round((this.correctAnswers / this.totalAnswers) * 100)
            : 0;
        this.isWaitingForInput = false;

        this.callbacks.onStepComplete(this.state.currentStep, isFullyCorrect);
        this.callbacks.onStateChange({ ...this.state });

        this.scheduleNextStimulus();
    }

    /**
     * Son gösterilen karakterleri döndür (UI'da feedback için)
     */
    getLastShownStimuli(): string[] {
        return this.lastShownStimuli;
    }

    private generateStimuli(): void {
        this.currentStimuli = [];
        const pattern = this.config.expansion?.pattern || 'horizontal';
        const type = this.config.expansion?.stimulusType || 'letter';

        const spacingPx = ScreenHelper.degreesToPixels(this.currentDegrees);
        const containerWidth = window.innerWidth;
        const containerHeight = window.innerHeight;
        const xPercent = (spacingPx / containerWidth) * 100;
        const yPercent = (spacingPx / containerHeight) * 100;

        const chars = "ABCDEFGHKLMNPRSTUVYZ"; // Karışıklık yaratabilecek I,O,Q çıkarıldı
        const numbers = [1, 2, 3, 4, 5, 6, 7, 8, 9];

        // Kullanılan karakterleri takip et (aynı turda ve önceki turdan gelen tekrarları önle)
        const usedInThisTurn: string[] = [];
        const previousChars = this.lastShownStimuli.map(s => s.toUpperCase());

        /**
         * Benzersiz karakter üretir
         * - Aynı turda kullanılan karakterleri tekrar kullanmaz
         * - Önceki turda gösterilen karakterleri tekrar kullanmaz
         */
        const getUniqueContent = (): string => {
            let content: string;
            let attempts = 0;
            const maxAttempts = 50; // Sonsuz döngü önleme

            do {
                if (type === 'number') {
                    content = numbers[Math.floor(Math.random() * numbers.length)].toString();
                } else {
                    content = chars[Math.floor(Math.random() * chars.length)];
                }
                attempts++;
            } while (
                (usedInThisTurn.includes(content) || previousChars.includes(content)) &&
                attempts < maxAttempts
            );

            usedInThisTurn.push(content);
            return content;
        };

        // Sınır kontrolü - karakterler egzersiz alanı dışına çıkmasın
        // Minimum %5, maksimum %95 (kenarlardan 5% boşluk)
        const MIN_PERCENT = 5;
        const MAX_PERCENT = 95;
        const clamp = (value: number) => Math.max(MIN_PERCENT, Math.min(MAX_PERCENT, value));

        if (pattern === 'horizontal') {
            const leftX = clamp(50 - (xPercent / 2));
            const rightX = clamp(50 + (xPercent / 2));
            this.currentStimuli.push(
                { content: getUniqueContent(), x: leftX, y: 50 },
                { content: getUniqueContent(), x: rightX, y: 50 }
            );
        } else if (pattern === 'vertical') {
            const topY = clamp(50 - (yPercent / 2));
            const bottomY = clamp(50 + (yPercent / 2));
            this.currentStimuli.push(
                { content: getUniqueContent(), x: 50, y: topY },
                { content: getUniqueContent(), x: 50, y: bottomY }
            );
        } else if (pattern === 'radial') {
            const diagX = xPercent / 2.8;
            const diagY = yPercent / 2.8;
            this.currentStimuli.push(
                { content: getUniqueContent(), x: clamp(50 - diagX), y: clamp(50 - diagY) },
                { content: getUniqueContent(), x: clamp(50 + diagX), y: clamp(50 - diagY) },
                { content: getUniqueContent(), x: clamp(50 - diagX), y: clamp(50 + diagY) },
                { content: getUniqueContent(), x: clamp(50 + diagX), y: clamp(50 + diagY) }
            );
        }
    }

    pause(): void {
        if (this.state.isPaused) return;
        this.state.isPaused = true;
        this.pauseStartTime = Date.now();
        clearTimeout(this.stimulusInterval);
        clearTimeout(this.hideTimeout);
        this.callbacks.onPause();
        this.callbacks.onStateChange({ ...this.state });
    }

    resume(): void {
        if (!this.state.isPaused) return;

        // Adjust startTime to account for pause duration
        const pauseDuration = Date.now() - this.pauseStartTime;
        this.startTime += pauseDuration;

        this.state.isPaused = false;
        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
        if (!this.isWaitingForInput) {
            this.scheduleNextStimulus();
        }
    }

    stop(): void {
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearTimeout(this.stimulusInterval);
        clearTimeout(this.hideTimeout);
        this.callbacks.onStateChange({ ...this.state });
    }

    reset(): void {
        this.stop();
        this.state = {
            ...this.state,
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            timeElapsed: 0,
            errors: 0,
            accuracy: 0
        };
        this.currentDegrees = this.config.expansion?.startDegrees || 5;
        this.correctAnswers = 0;
        this.totalAnswers = 0;
        this.roundResults = [];
        this.isStimulusVisible = false;
        this.isWaitingForInput = false;
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.stop();
    }

    private complete(): void {
        this.state.isCompleted = true;
        this.state.isRunning = false;
        this.state.score = this.state.accuracy;

        clearInterval(this.timerInterval);

        // Ortalama tepki süresini hesapla
        const avgResponseTime = this.roundResults.length > 0
            ? Math.round(this.roundResults.reduce((sum, r) => sum + r.responseTimeMs, 0) / this.roundResults.length)
            : 0;

        // Başlangıç ve bitiş açıları
        const startDegrees = this.roundResults.length > 0 ? this.roundResults[0].degrees : this.currentDegrees;
        const endDegrees = this.roundResults.length > 0 ? this.roundResults[this.roundResults.length - 1].degrees : this.currentDegrees;

        const result: EngineResult = {
            score: this.state.accuracy,
            accuracy: this.state.accuracy,
            totalTime: this.state.timeElapsed,
            totalSteps: this.state.totalSteps,
            completedSteps: this.state.currentStep,
            errors: this.state.errors,
            details: {
                level: this.config.expansion?.level,
                maxDegreesReached: this.maxDegreesReached,
                startDegrees,
                endDegrees,
                correctAnswers: this.correctAnswers,
                totalAnswers: this.totalAnswers,
                averageResponseTimeMs: avgResponseTime,
                roundResults: this.roundResults
            }
        };

        // State değişikliğini UI'a bildir (isCompleted = true)
        this.callbacks.onStateChange({ ...this.state });

        // Sonucu bildir
        this.callbacks.onComplete(result);
    }

    // Public Helpers
    getCurrentStimuli() {
        return this.isStimulusVisible ? this.currentStimuli : [];
    }

    getCenterPointType() {
        return this.config.visuals?.centerPoint || 'cross';
    }

    getExpectedAnswerCount(): number {
        return this.lastShownStimuli.length;
    }
}
