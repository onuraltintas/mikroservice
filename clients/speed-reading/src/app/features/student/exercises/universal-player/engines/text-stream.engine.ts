/**
 * Text Stream Engine
 * Tachistoscope, RSVP ve benzeri metin akışı egzersizleri için.
 * Kelimeler/cümleler belirli hızda gösterilir, kullanıcı ne gördüğünü yazar.
 * 
 * Bilimsel Standartlar:
 * - Stimulus gösterim süresi: 50-500ms (adaptif)
 * - Fixation point öncesi
 * - Kullanıcı yanıtı + Levenshtein fuzzy matching
 * - Adaptif hız: %80+ doğruluk → %10 hızlan, %60 altı → %20 yavaşla
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';

export interface TextStreamConfig extends EngineConfig {
    mode: string;           // 'tachistoscope', 'rsvp', 'sequence'
    timing: {
        durationMs: number;   // Her stimulus gösterim süresi
        intervalMs: number;   // Stimuluslar arası bekleme
    };
    content: {
        type: string;         // 'word', 'phrase', 'number', 'letter'
        count: number;        // Toplam stimulus sayısı
        source: string;       // 'random_pool', 'custom', 'backend'
        items?: string[];     // Özel içerik listesi
    };
    visuals: {
        fontSize: string;     // 'small', 'medium', 'large', 'xlarge'
        showFixation: boolean;
    };
    adaptive?: {
        enabled: boolean;
        minDurationMs: number;
        maxDurationMs: number;
    };
    // Backend'den gelen TachistoscopeSessionData
    Stimuli?: Array<{ Text: string; Type: string; DifficultyLevel: number }>;
    DisplayDurationMs?: number;
    TotalStimuli?: number;
    [key: string]: any; // Index signature for flexible config access
}

interface TrialRecord {
    stimulus: string;
    userAnswer: string;
    isCorrect: boolean;
    responseTimeMs: number;
    displayDurationMs: number;
}

export class TextStreamEngine implements BaseEngine {
    readonly engineType = 'text_stream';
    readonly displayName = 'Metin Akışı';

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

    private config!: TextStreamConfig;
    private callbacks!: EngineCallbacks;
    private startTime = 0;
    private pauseStartTime = 0;
    private timerInterval: any;
    private stimulusTimeout: any;

    // Content
    private stimuli: string[] = [];
    private currentStimulusIndex = 0;
    private currentStimulus = '';
    private isShowingStimulus = false;
    private isShowingFixation = false;
    private isWaitingForAnswer = false;
    private stimulusShowTime = 0;

    // Answer tracking
    private trials: TrialRecord[] = [];
    private correctCount = 0;

    // Adaptive speed
    private currentDurationMs = 500;
    private initialDurationMs = 500;

    // Fallback word pools (used only if no backend data)
    private static readonly WORD_POOL = [
        'kitap', 'okuma', 'hızlı', 'anlama', 'öğrenme', 'bilgi', 'düşünce', 'kavram',
        'metin', 'sayfa', 'kelime', 'cümle', 'paragraf', 'başlık', 'içerik', 'anlam',
        'zihin', 'beyin', 'hafıza', 'dikkat', 'odaklanma', 'konsantrasyon', 'pratik',
        'gelişim', 'ilerleme', 'başarı', 'hedef', 'motivasyon', 'azim', 'çalışma',
        'zaman', 'hız', 'verimlilik', 'teknik', 'yöntem', 'strateji', 'plan', 'sistem',
        'görsel', 'algı', 'tepki', 'refleks', 'sinir', 'bağlantı', 'işlem', 'süreç',
        'kalem', 'defter', 'masa', 'sandalye', 'pencere', 'kapı', 'duvar', 'tavan',
        'elma', 'armut', 'portakal', 'muz', 'çilek', 'kiraz', 'üzüm', 'erik'
    ];

    private static readonly NUMBER_POOL = [
        '123', '456', '789', '234', '567', '890', '135', '246', '357', '468',
        '1234', '5678', '9012', '3456', '7890', '2468', '1357', '8024', '6913', '4802',
        '12345', '67890', '24680', '13579', '98765', '43210', '86420', '97531'
    ];

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.config = config as TextStreamConfig;
        this.callbacks = callbacks;

        // Backend property normalization (handle PascalCase vs camelCase)
        const stimuli = this.config.Stimuli || this.config['stimuli'] || this.config['Words'] || this.config['words'] || this.config['Chunks'] || this.config['chunks'];
        const displayDuration = this.config.DisplayDurationMs || this.config['displayDurationMs'] ||
            this.config['IntervalMs'] || this.config['intervalMs'] ||
            this.config.timing?.durationMs || 500;
        const totalStimuli = this.config.TotalStimuli || this.config['totalStimuli'] || this.config['TotalWords'] || this.config['totalWords'];

        // Store normalized values in config for easier access
        this.config.Stimuli = stimuli;
        this.config.DisplayDurationMs = displayDuration;
        if (totalStimuli) this.config.TotalStimuli = totalStimuli;

        this.generateStimuli();
        this.state.totalSteps = this.stimuli.length;

        // Initialize adaptive speed
        this.currentDurationMs = displayDuration;
        this.initialDurationMs = this.currentDurationMs;


    }

    private generateStimuli(): void {
        // Priority 1: Backend'den gelen stimuli (TachistoscopeSessionData veya RSVPSessionData)
        if (this.config.Stimuli && this.config.Stimuli.length > 0) {
            // Handle both PascalCase (Text) and camelCase (text) properties, OR just strings (for RSVP Words)
            this.stimuli = this.config.Stimuli.map((s: any) => {
                if (typeof s === 'string') return s;
                return s.Text || s.text || '';
            });
            return;
        }

        // Priority 2: Config'den gelen custom items
        const count = this.config.TotalStimuli || this.config.content?.count || 20;
        const type = this.config.content?.type || 'word';
        const source = this.config.content?.source || 'random_pool';

        if (source === 'custom' && this.config.content?.items) {
            this.stimuli = this.config.content.items.slice(0, count);
            return;
        }

        // Priority 3: Fallback to local pool
        if (type === 'number') {
            this.stimuli = this.shuffleArray([...TextStreamEngine.NUMBER_POOL]).slice(0, count);
        } else {
            this.stimuli = this.shuffleArray([...TextStreamEngine.WORD_POOL]).slice(0, count);
        }

        // Ensure we have enough stimuli
        while (this.stimuli.length < count) {
            const pool = type === 'number' ? TextStreamEngine.NUMBER_POOL : TextStreamEngine.WORD_POOL;
            this.stimuli.push(...this.shuffleArray([...pool]));
        }
        this.stimuli = this.stimuli.slice(0, count);
    }

    private shuffleArray<T>(array: T[]): T[] {
        for (let i = array.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [array[i], array[j]] = [array[j], array[i]];
        }
        return array;
    }

    start(): void {
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.startTime = Date.now();
        this.currentStimulusIndex = 0;
        this.trials = [];
        this.correctCount = 0;

        // Timer
        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;
                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        this.callbacks.onStart();
        this.callbacks.onStateChange({ ...this.state });

        // Start stimulus cycle
        this.showNextStimulus();

    }

    private showNextStimulus(): void {
        if (!this.state.isRunning || this.state.isPaused) {
            return;
        }

        if (this.currentStimulusIndex >= this.stimuli.length) {
            this.complete();
            return;
        }

        const showFixation = this.config.visuals?.showFixation !== false;

        // Show fixation point first (if enabled)
        if (showFixation && !this.isShowingFixation) {
            this.isShowingFixation = true;
            this.isShowingStimulus = false;
            this.isWaitingForAnswer = false;
            this.currentStimulus = '';
            this.callbacks.onStateChange({ ...this.state });

            this.stimulusTimeout = setTimeout(() => {
                this.isShowingFixation = false;
                this.showStimulus();
            }, 300); // Fixation duration
            return;
        }

        this.showStimulus();
    }

    private showStimulus(): void {
        if (!this.state.isRunning || this.state.isPaused) {
            return;
        }

        // Show stimulus
        this.currentStimulus = this.stimuli[this.currentStimulusIndex];
        this.isShowingStimulus = true;
        this.isShowingFixation = false;
        this.isWaitingForAnswer = false;
        this.stimulusShowTime = Date.now();
        this.callbacks.onStateChange({ ...this.state });

        // Hide after duration
        this.stimulusTimeout = setTimeout(() => {
            this.isShowingStimulus = false;

            if (this.isRsvpMode()) {
                // RSVP Mode: Continuous flow, no user input required for valid reading
                this.handleAutoAdvance();
            } else {
                // Tachistoscope Mode: Wait for answer
                this.isWaitingForAnswer = true;
                this.callbacks.onStateChange({ ...this.state });
            }
        }, this.currentDurationMs);
    }

    private isRsvpMode(): boolean {
        return this.config.mode === 'rsvp' || (this.config as any).exerciseTypeName === 'RSVP';
    }

    private handleAutoAdvance(): void {
        // Pseudo-input for auto-advancing
        // In RSVP, we assume they read it. Correction is not applicable per word.
        this.currentStimulusIndex++;
        this.state.currentStep = this.currentStimulusIndex;

        // Notify progress
        this.callbacks.onStepComplete(this.currentStimulusIndex, true);
        this.callbacks.onStateChange({ ...this.state });

        if (this.currentStimulusIndex >= this.stimuli.length) {
            this.complete();
        } else {
            // Very brief gap between words (optional, helps separate words visually)
            const gapMs = this.config.timing?.intervalMs || 0;
            if (gapMs > 0) {
                setTimeout(() => this.showNextStimulus(), gapMs);
            } else {
                this.showNextStimulus();
            }
        }
    }

    pause(): void {
        if (this.state.isPaused) return;
        this.state.isPaused = true;
        this.pauseStartTime = Date.now();
        clearTimeout(this.stimulusTimeout);
        this.callbacks.onPause();
        this.callbacks.onStateChange({ ...this.state });
    }

    resume(): void {
        if (!this.state.isPaused) return;

        // Adjust startTime to account for pause duration
        const pauseDuration = Date.now() - this.pauseStartTime;
        this.startTime += pauseDuration;

        this.state.isPaused = false;
        if (this.isWaitingForAnswer) {
            // Continue waiting for answer
            this.callbacks.onStateChange({ ...this.state });
        } else {
            this.showNextStimulus();
        }
        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearTimeout(this.stimulusTimeout);
        this.callbacks.onStateChange({ ...this.state });
    }

    /**
     * Force finish the exercise (e.g. timeout)
     */
    finish(): void {
        this.complete();
    }

    reset(): void {
        this.stop();
        this.state = {
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            totalSteps: this.stimuli.length,
            score: 0,
            accuracy: 100,
            timeElapsed: 0,
            errors: 0
        };
        this.currentStimulusIndex = 0;
        this.currentStimulus = '';
        this.isShowingStimulus = false;
        this.isShowingFixation = false;
        this.isWaitingForAnswer = false;
        this.trials = [];
        this.correctCount = 0;
        this.currentDurationMs = this.initialDurationMs;
        this.generateStimuli(); // Regenerate for variety
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.stop();
    }

    /**
     * Handle user answer submission
     */
    handleInput(input: { answer: string }): void {
        if (!this.isWaitingForAnswer || !this.state.isRunning) {
            return;
        }

        const responseTime = Date.now() - this.stimulusShowTime - this.currentDurationMs;
        const userAnswer = (input.answer || '').trim().toLocaleLowerCase('tr-TR');
        const correctAnswer = this.currentStimulus.toLocaleLowerCase('tr-TR');

        // Check correctness using Levenshtein distance (fuzzy matching)
        const isCorrect = this.calculateSimilarity(userAnswer, correctAnswer);

        // Record trial
        const trial: TrialRecord = {
            stimulus: this.currentStimulus,
            userAnswer: input.answer || '',
            isCorrect,
            responseTimeMs: Math.max(0, responseTime),
            displayDurationMs: this.currentDurationMs
        };
        this.trials.push(trial);

        if (isCorrect) {
            this.correctCount++;
        } else {
            this.state.errors++;
        }

        // Update state
        this.currentStimulusIndex++;
        this.state.currentStep = this.currentStimulusIndex;
        this.state.accuracy = this.trials.length > 0
            ? Math.round((this.correctCount / this.trials.length) * 100)
            : 100;
        this.state.score = this.state.accuracy;

        // Adaptive speed adjustment
        if (this.config.adaptive?.enabled !== false) {
            // Success logic: Every 2 correct answers (instant)
            this.checkFastAdaptation(isCorrect);

            // Deceleration logic: Every 5 trials (periodic)
            if (this.trials.length % 5 === 0) {
                this.adjustSpeed();
            }
        }

        this.isWaitingForAnswer = false;

        // Notify step complete with feedback
        this.callbacks.onStepComplete(this.currentStimulusIndex, isCorrect);
        this.callbacks.onStateChange({ ...this.state });

        // Check completion or continue
        if (this.currentStimulusIndex >= this.stimuli.length) {
            setTimeout(() => this.complete(), 500);
        } else {
            // Brief pause then show next (Wait for feedback to finish which is 1.2s)
            setTimeout(() => this.showNextStimulus(), 1300);
        }
    }

    /**
     * Adaptive speed adjustment based on recent performance
     */
    private adjustSpeed(): void {
        const recentTrials = this.trials.slice(-5);
        const recentCorrect = recentTrials.filter(t => t.isCorrect).length;
        const recentAccuracy = (recentCorrect / 5) * 100;

        const minDuration = this.config.adaptive?.minDurationMs || 50;
        const maxDuration = this.config.adaptive?.maxDurationMs || 1000;

        // Deceleration logic (Zorlanma Durumu)
        if (recentAccuracy < 60) {
            // Low accuracy → slow down (increase duration by 10% as requested)
            const newDuration = Math.min(maxDuration, Math.round(this.currentDurationMs * 1.1));
            this.currentDurationMs = newDuration;
        }
    }

    private checkFastAdaptation(isCorrect: boolean): void {
        if (!isCorrect) return;

        // Every 2 correct answers, speed up (Başarı Durumu)
        const totalCorrect = this.trials.filter(t => t.isCorrect).length;
        if (totalCorrect > 0 && totalCorrect % 2 === 0) {
            const minDuration = this.config.adaptive?.minDurationMs || 50;
            const newDuration = Math.max(minDuration, Math.round(this.currentDurationMs * 0.9));
            this.currentDurationMs = newDuration;
        }
    }

    /**
     * Calculate similarity using Levenshtein distance
     * Allows small typos
     */
    private calculateSimilarity(answer1: string, answer2: string): boolean {
        // Strict match: Remove Levenshtein fuzzy matching as requested.
        // Only case-insensitive and trimmed comparison.
        return answer1.trim().toLocaleLowerCase('tr-TR') === answer2.trim().toLocaleLowerCase('tr-TR');
    }



    private complete(): void {
        this.state.isCompleted = true;
        this.state.isRunning = false;
        clearInterval(this.timerInterval);
        clearTimeout(this.stimulusTimeout);

        const avgResponseTime = this.trials.length > 0
            ? Math.round(this.trials.reduce((sum, t) => sum + t.responseTimeMs, 0) / this.trials.length)
            : 0;

        const speedImprovement = this.initialDurationMs > 0
            ? Math.round((this.initialDurationMs - this.currentDurationMs) / this.initialDurationMs * 100)
            : 0;

        const result: EngineResult = {
            score: this.state.accuracy,
            accuracy: this.state.accuracy,
            totalTime: this.state.timeElapsed,
            totalSteps: this.stimuli.length,
            completedSteps: this.currentStimulusIndex,
            errors: this.state.errors,
            details: {
                mode: this.config.mode,
                stimulusType: this.config.content?.type,
                stimulusCount: this.stimuli.length,
                correctCount: this.correctCount,
                incorrectCount: this.state.errors,
                avgResponseTime,
                initialDurationMs: this.initialDurationMs,
                finalDurationMs: this.currentDurationMs,
                speedImprovementPercent: speedImprovement,
                trials: this.trials,

                // RSVP Specific Details
                wpm: Math.round(60000 / this.currentDurationMs),
                readWordCount: this.currentStimulusIndex,
                totalWordCount: this.stimuli.length,
                durationSeconds: Math.round(this.state.timeElapsed / 1000)
            }
        };

        // First update state so component shows completed screen
        this.callbacks.onStateChange({ ...this.state });
        // Then notify completion with results
        this.callbacks.onComplete(result);
    }

    // Public API
    getCurrentStimulus(): string {
        return this.currentStimulus;
    }

    isShowingContent(): boolean {
        return this.isShowingStimulus;
    }

    isShowingFixationPoint(): boolean {
        return this.isShowingFixation;
    }

    isWaitingForUserAnswer(): boolean {
        return this.isWaitingForAnswer;
    }

    getFontSize(): string {
        return this.config.visuals?.fontSize || 'large';
    }

    getMode(): string {
        return this.config.mode || 'tachistoscope';
    }

    getCurrentDuration(): number {
        return this.currentDurationMs;
    }

    getLastTrialResult(): TrialRecord | null {
        return this.trials.length > 0 ? this.trials[this.trials.length - 1] : null;
    }
}
