/**
 * Visual Expansion Engine
 * Görsel Genişletme / Periferik Görüş egzersizleri için.
 * Merkezde fiksasyon noktası, kenarlarda anlık beliren uyaranlar.
 * 
 * Bilimsel Temel: Kullanıcının periferik görüşünü genişletmek için
 * görüş açısı (derece) tabanlı hesaplama ve adaptif zorluk kullanır.
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';
import { visualAngleToAxisOffsetPercent } from './visual-expansion-position';

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
    private startDegrees = 5;
    private targetDegrees = 30;

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
    private serverAuthoritative = false;
    private awaitingServer = false;
    private pendingAnswers: string[] = [];

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
        this.serverAuthoritative = backendConfig.serverAuthoritative === true;

        // Catalog configurations exist in both the newer nested shape and
        // the legacy mode/content shape. Normalize them before rendering so
        // the server stimulus and the client position use the same settings.
        const expansionConfig = backendConfig.expansion || {};
        const contentConfig = backendConfig.content || {};
        const timingConfig = backendConfig.timing || {};
        const configuredPattern = backendConfig.VisualExpansionPattern
            || backendConfig.visualExpansionPattern
            || expansionConfig.pattern
            || backendConfig.pattern
            || backendConfig.mode
            || this.config.expansion?.pattern
            || 'horizontal';
        const configuredStimulusType = backendConfig.VisualExpansionStimulusType
            || backendConfig.visualExpansionStimulusType
            || expansionConfig.stimulusType
            || contentConfig.stimulusType
            || this.config.expansion?.stimulusType
            || 'letter';
        this.config.expansion = {
            ...(this.config.expansion || {}),
            level: this.config.expansion?.level || backendConfig.difficultyLevel || 1,
            pattern: configuredPattern,
            stimulusType: configuredStimulusType,
            symmetry: this.config.expansion?.symmetry ?? true
        } as VisualExpansionConfig['expansion'];
        this.config.timing = {
            ...(this.config.timing || {}),
            durationMs: this.config.timing?.durationMs
                || Number(backendConfig.VisualExpansionDisplayDurationMs || backendConfig.visualExpansionDisplayDurationMs || timingConfig.durationMs)
                || 250,
            intervalMs: this.config.timing?.intervalMs
                || Number(timingConfig.intervalMs)
                || 1500
        };

        // Başlangıç açısı (backend'den veya varsayılan)
        this.startDegrees = Number(backendConfig.VisualExpansionStartDegrees
            || backendConfig.visualExpansionStartDegrees
            || backendConfig.StartDegrees
            || backendConfig.startDegrees
            || this.config.expansion?.startDegrees
            || (2 + (this.config.expansion?.level || 1) * 2));

        // Hedef açı (kullanım için sakla)
        this.targetDegrees = Number(backendConfig.VisualExpansionTargetDegrees
            || backendConfig.visualExpansionTargetDegrees
            || backendConfig.TargetDegrees
            || backendConfig.targetDegrees
            || 30);
        this.startDegrees = Math.max(2, Math.min(60, this.startDegrees));
        this.targetDegrees = Math.max(this.startDegrees, Math.min(60, this.targetDegrees));
        this.currentDegrees = Number(backendConfig.VisualExpansionCurrentDegrees
            || backendConfig.visualExpansionCurrentDegrees
            || this.startDegrees);

        // Tur sayısı
        this.state.totalSteps = backendConfig.Rounds || backendConfig.rounds || 20;

        // Gösterim süresi (backend'den zorluk bazlı)
        const displayDurationMs = backendConfig.DisplayDurationMs
            || backendConfig.displayDurationMs
            || backendConfig.VisualExpansionDisplayDurationMs
            || backendConfig.visualExpansionDisplayDurationMs;
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
                if (this.serverAuthoritative) {
                    this.awaitingServer = true;
                    this.callbacks.onAction({ action: 'visual_expansion_present', timestamp: new Date() });
                } else {
                    this.showStimulus();
                }
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

        if (this.serverAuthoritative) {
            this.isWaitingForInput = false;
            this.awaitingServer = true;
            this.pendingAnswers = input.answers.map(answer => answer.trim());
            this.callbacks.onAction({
                action: 'visual_expansion_answer',
                answers: this.pendingAnswers,
                timestamp: new Date()
            });
            return;
        }

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

    reconcileServerResponse(action: any, response: any): void {
        const actionName = String(action?.action || '').toLowerCase();
        this.awaitingServer = false;
        if (response?.isValid === false) {
            const message = String(response?.message || '').toLowerCase();
            if (actionName === 'visual_expansion_answer'
                && message.includes('outside its response window')) {
                // A delayed gateway response must not strand the assessment
                // on an error page. The server has discarded this round, so
                // request a fresh presentation for the same round.
                this.pendingAnswers = [];
                this.isWaitingForInput = false;
                this.callbacks.onStateChange({ ...this.state });
                this.scheduleNextStimulus();
                return;
            }
            if (actionName === 'visual_expansion_answer') {
                this.isWaitingForInput = false;
                this.scheduleNextStimulus();
            }
            this.callbacks.onError(response?.message || 'Görsel genişleme işlemi doğrulanamadı.');
            return;
        }

        if (actionName === 'visual_expansion_present') {
            const stimuli = response?.feedbackData?.stimuli;
            if (!Array.isArray(stimuli) || stimuli.length < 2) {
                this.callbacks.onError('Görsel genişleme uyaranı sunucudan eksik döndü.');
                return;
            }
            const degrees = Number(response?.feedbackData?.degrees);
            if (Number.isFinite(degrees) && degrees > 0) {
                this.currentDegrees = Math.max(2, Math.min(60, degrees));
            } else {
                const round = Number(response?.feedbackData?.round);
                if (Number.isFinite(round)) {
                    this.currentDegrees = this.getDegreesForRound(round);
                }
            }
            this.currentStimuli = this.positionStimuli(stimuli.map(String));
            this.isStimulusVisible = true;
            this.callbacks.onStateChange({ ...this.state });
            this.hideTimeout = setTimeout(
                () => this.hideStimulus(),
                Number(response?.feedbackData?.displayDurationMs) || this.config.timing?.durationMs || 250);
            return;
        }

        if (actionName === 'visual_expansion_answer') {
            // Assessment responses intentionally omit correctness so the
            // answer key is not exposed to the browser. We can still show
            // immediate feedback by comparing the submitted values with the
            // stimuli that were already displayed; the server remains the
            // source of truth for persistence and assessment scoring.
            const serverCorrect = response?.isCorrect;
            const submittedAnswers = this.pendingAnswers.map(value => value.trim().toUpperCase());
            const expectedAnswers = this.lastShownStimuli.map(value => value.trim().toUpperCase());
            const isCorrect = serverCorrect == null
                ? submittedAnswers.length === expectedAnswers.length
                    && submittedAnswers.every((value, index) => value === expectedAnswers[index])
                : serverCorrect === true;
            const correctAnswers = this.lastShownStimuli.map(value => value.toUpperCase());
            const responseTimeMs = Date.now() - this.stimulusShownTime;
            this.totalAnswers++;
            if (isCorrect) this.correctAnswers++; else this.state.errors++;
            this.roundResults.push({
                round: this.state.currentStep + 1,
                degrees: this.currentDegrees,
                correct: isCorrect,
                leftChar: correctAnswers[0] || '',
                rightChar: correctAnswers[1] || '',
                userLeftAnswer: this.pendingAnswers[0] || '',
                userRightAnswer: this.pendingAnswers[1] || '',
                responseTimeMs,
                timestamp: new Date().toISOString()
            });
            this.pendingAnswers = [];
            this.state.currentStep++;
            this.state.accuracy = Math.round((this.correctAnswers / this.totalAnswers) * 100);
            this.callbacks.onStepComplete(this.state.currentStep, isCorrect);
            this.callbacks.onStateChange({ ...this.state });
            this.scheduleNextStimulus();
        }
    }

    private positionStimuli(contents: string[]): Array<{ content: string; x: number; y: number }> {
        const bounds = this.getRenderBounds();
        const pattern = this.config.expansion?.pattern || 'horizontal';
        const radial = pattern === 'radial';
        const x = visualAngleToAxisOffsetPercent(this.currentDegrees, bounds.width, radial);
        const y = visualAngleToAxisOffsetPercent(this.currentDegrees, bounds.height, radial);
        if (pattern === 'vertical') {
            return contents.map((content, index) => ({ content, x: 50, y: index === 0 ? 50 - y : 50 + y }));
        }
        if (pattern === 'radial') {
            const positions = [[50 - x, 50 - y], [50 + x, 50 - y], [50 - x, 50 + y], [50 + x, 50 + y]];
            return contents.map((content, index) => ({ content, x: positions[index][0], y: positions[index][1] }));
        }
        return contents.map((content, index) => ({ content, x: index === 0 ? 50 - x : 50 + x, y: 50 }));
    }

    private getDegreesForRound(round: number): number {
        if (this.state.totalSteps <= 1) return this.startDegrees;
        const progress = Math.max(0, Math.min(1, round / (this.state.totalSteps - 1)));
        return Math.round(this.startDegrees + ((this.targetDegrees - this.startDegrees) * progress));
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

        const bounds = this.getRenderBounds();
        const radial = pattern === 'radial';
        const xOffset = visualAngleToAxisOffsetPercent(this.currentDegrees, bounds.width, radial);
        const yOffset = visualAngleToAxisOffsetPercent(this.currentDegrees, bounds.height, radial);

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
            const leftX = clamp(50 - xOffset);
            const rightX = clamp(50 + xOffset);
            this.currentStimuli.push(
                { content: getUniqueContent(), x: leftX, y: 50 },
                { content: getUniqueContent(), x: rightX, y: 50 }
            );
        } else if (pattern === 'vertical') {
            const topY = clamp(50 - yOffset);
            const bottomY = clamp(50 + yOffset);
            this.currentStimuli.push(
                { content: getUniqueContent(), x: 50, y: topY },
                { content: getUniqueContent(), x: 50, y: bottomY }
            );
        } else if (pattern === 'radial') {
            const diagX = xOffset;
            const diagY = yOffset;
            this.currentStimuli.push(
                { content: getUniqueContent(), x: clamp(50 - diagX), y: clamp(50 - diagY) },
                { content: getUniqueContent(), x: clamp(50 + diagX), y: clamp(50 - diagY) },
                { content: getUniqueContent(), x: clamp(50 - diagX), y: clamp(50 + diagY) },
                { content: getUniqueContent(), x: clamp(50 + diagX), y: clamp(50 + diagY) }
            );
        }
    }

    private getRenderBounds(): { width: number; height: number } {
        const configured = (this.config as any)?.getRenderBounds?.();
        const width = Number(configured?.width);
        const height = Number(configured?.height);
        return {
            width: Number.isFinite(width) && width > 0 ? width : window.innerWidth,
            height: Number.isFinite(height) && height > 0 ? height : window.innerHeight
        };
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
        this.currentDegrees = this.startDegrees;
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
