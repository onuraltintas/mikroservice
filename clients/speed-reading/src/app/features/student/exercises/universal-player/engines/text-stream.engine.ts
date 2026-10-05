/**
 * Text Stream Engine
 * Tachistoscope, RSVP ve benzeri metin akışı egzersizleri için.
 * Kelimeler/cümleler belirli hızda gösterilir, kullanıcı ne gördüğünü yazar.
 * 
 * Eğitim akışı:
 * - Stimulus gösterim süresi: 50-500ms (adaptif)
 * - Fixation point öncesi
 * - Türkçe büyük/küçük harf normalizasyonuyla yanıt karşılaştırma
 * - Yapılandırılabilir eğitim kuralları; bilimsel okuma hızı normu değildir.
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';
import { boundedInteger, caseInsensitiveField, mergeCaseInsensitiveRecords, recordOrEmpty } from './reading-pacer-safety';

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
    private transitionTimeout: any;
    private transitionCallback: (() => void) | null = null;
    private transitionEndsAt = 0;
    private transitionRemainingMs = 0;

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
    private serverAuthoritative = false;
    private awaitingServer = false;
    private pendingAnswer = '';
    private pendingStimulus = '';
    private restoredCorrectCount = 0;
    private restoredIncorrectCount = 0;
    private contentPool: string[] = [];
    private targetLength = 3;
    private initialTargetLength = 3;
    private consecutiveCorrect = 0;

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
        this.callbacks = callbacks;
        const root = config as any;
        const nested = recordOrEmpty(caseInsensitiveField(root, 'engineConfig'));
        const timing = mergeCaseInsensitiveRecords(root, nested, 'timing');
        const content = mergeCaseInsensitiveRecords(root, nested, 'content');
        const visuals = mergeCaseInsensitiveRecords(root, nested, 'visuals');
        const adaptive = mergeCaseInsensitiveRecords(root, nested, 'adaptive');
        this.config = {
            ...root,
            ...nested,
            mode: caseInsensitiveField(nested, 'mode') ?? caseInsensitiveField(root, 'mode'),
            timing,
            content,
            visuals: {
                ...visuals,
                fontSize: typeof visuals['fontsize'] === 'string' ? visuals['fontsize'] : 'large',
                showFixation: visuals['showfixation'] !== false
            }
        } as TextStreamConfig;
        this.config.adaptive = {
            enabled: adaptive['enabled'] !== false,
            minDurationMs: boundedInteger(adaptive['mindurationms'], 50, 50, 5000),
            maxDurationMs: boundedInteger(adaptive['maxdurationms'], 1000, 50, 5000)
        };
        this.config.adaptive.maxDurationMs = Math.max(
            this.config.adaptive.minDurationMs,
            this.config.adaptive.maxDurationMs);
        this.config.timing.intervalMs = boundedInteger(timing['intervalms'], 0, 0, 10000);
        const contentItems = caseInsensitiveField(content, 'items');
        this.config.content.items = Array.isArray(contentItems)
            ? contentItems.filter(item => typeof item === 'string').slice(0, 500)
            : undefined;

        // Backend property normalization (handle PascalCase vs camelCase)
        const rawStimuli = caseInsensitiveField(nested, 'stimuli')
            ?? caseInsensitiveField(nested, 'words')
            ?? caseInsensitiveField(nested, 'chunks')
            ?? caseInsensitiveField(root, 'stimuli')
            ?? caseInsensitiveField(root, 'words')
            ?? caseInsensitiveField(root, 'chunks');
        const stimuli = Array.isArray(rawStimuli)
            ? rawStimuli.filter(item => typeof item === 'string' || (item && typeof item === 'object')).slice(0, 500)
            : undefined;
        const displayDuration = boundedInteger(
            caseInsensitiveField(nested, 'displayDurationMs')
            ?? caseInsensitiveField(nested, 'intervalMs')
            ?? caseInsensitiveField(root, 'displayDurationMs')
            ?? caseInsensitiveField(root, 'intervalMs')
            ?? timing['durationms'],
            500, 50, 5000);
        const totalStimuli = boundedInteger(
            caseInsensitiveField(nested, 'totalStimuli')
            ?? caseInsensitiveField(nested, 'totalWords')
            ?? caseInsensitiveField(root, 'totalStimuli')
            ?? caseInsensitiveField(root, 'totalWords')
            ?? content['count'],
            stimuli?.length || 20, 1, 500);

        // Store normalized values in config for easier access
        this.config.Stimuli = stimuli;
        this.config.DisplayDurationMs = displayDuration;
        this.config.TotalStimuli = totalStimuli;
        if (this.config.content) this.config.content.count = totalStimuli;
        if (adaptive['maxdurationms'] === undefined) this.config.adaptive.maxDurationMs = Math.max(this.config.adaptive.maxDurationMs, displayDuration);

        this.serverAuthoritative = root.serverAuthoritative === true && !this.isRsvpMode();
        const serverState = recordOrEmpty(root.tachistoscope);
        this.targetLength = boundedInteger(root.difficultyLevel, 1, 1, 5) * 2 + 1;
        if (this.config.content?.type === 'letter') this.targetLength = boundedInteger(root.difficultyLevel, 1, 1, 5);
        this.initialTargetLength = this.targetLength;
        this.generateStimuli();
        if (this.serverAuthoritative) {
            this.stimuli = new Array(boundedInteger(serverState['count'], totalStimuli, 1, 500)).fill('');
            this.currentStimulusIndex = boundedInteger(serverState['round'], 0, 0, this.stimuli.length);
            this.restoredCorrectCount = boundedInteger(serverState['correctCount'], 0, 0, this.currentStimulusIndex);
            this.restoredIncorrectCount = boundedInteger(serverState['incorrectCount'], 0, 0, this.currentStimulusIndex);
        }
        this.state.totalSteps = this.stimuli.length;

        // Initialize adaptive speed
        this.currentDurationMs = displayDuration;
        if (!this.isRsvpMode() && this.config.adaptive?.enabled) {
            this.currentDurationMs = Math.max(this.config.adaptive.minDurationMs,
                Math.min(this.config.adaptive.maxDurationMs, this.currentDurationMs));
        }
        if (this.serverAuthoritative) this.currentDurationMs = boundedInteger(serverState['displayDurationMs'], this.currentDurationMs, 50, 5000);
        this.initialDurationMs = this.currentDurationMs;


    }

    private generateStimuli(): void {
        // Priority 1: Backend'den gelen stimuli (TachistoscopeSessionData veya RSVPSessionData)
        if (this.config.Stimuli && this.config.Stimuli.length > 0) {
            // Handle both PascalCase (Text) and camelCase (text) properties, OR just strings (for RSVP Words)
            this.stimuli = this.config.Stimuli.map((s: any) => {
                const text = typeof s === 'string' ? s : (s.Text || s.text || '');
                return typeof text === 'string' ? text.slice(0, 1000) : '';
            }).filter(Boolean);
            if (!this.isRsvpMode()) {
                this.contentPool = [...this.stimuli];
                this.stimuli = new Array(this.config.TotalStimuli).fill('');
            }
            return;
        }

        // Priority 2: Config'den gelen custom items
        const count = this.config.TotalStimuli || this.config.content?.count || 20;
        const type = this.config.content?.type || 'word';
        const source = this.config.content?.source || 'random_pool';

        if (source === 'custom' && this.config.content?.items) {
            this.stimuli = this.config.content.items
                .filter(item => typeof item === 'string')
                .slice(0, count)
                .map(item => item.slice(0, 1000));
            this.contentPool = [...this.config.content.items];
            if (!this.isRsvpMode()) this.stimuli = new Array(count).fill('');
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
        this.contentPool = type === 'phrase'
            ? TextStreamEngine.WORD_POOL.map((word, index) => word + ' ' + TextStreamEngine.WORD_POOL[(index + 1) % TextStreamEngine.WORD_POOL.length])
            : [...TextStreamEngine.WORD_POOL];
    }

    private shuffleArray<T>(array: T[]): T[] {
        for (let i = array.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [array[i], array[j]] = [array[j], array[i]];
        }
        return array;
    }

    private selectPreviewStimulus(): string {
        const type = this.config.content?.type || 'word';
        if (this.config.content?.source !== 'custom' && type === 'letter') {
            const alphabet = 'ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ';
            return Array.from({ length: Math.min(12, this.targetLength) }, () => alphabet[Math.floor(Math.random() * alphabet.length)]).join('');
        }
        if (this.config.content?.source !== 'custom' && type === 'number') {
            return Array.from({ length: Math.min(12, this.targetLength) }, () => Math.floor(Math.random() * 10)).join('');
        }
        let candidates = this.contentPool.filter(word => word !== this.currentStimulus);
        if (!candidates.length) candidates = this.contentPool;
        if (!candidates.length) {
            this.stop();
            this.callbacks.onError('Takistoskop için uygun içerik bulunamadı.');
            return '';
        }
        const distance = Math.min(...candidates.map(word => Math.abs(word.length - this.targetLength)));
        candidates = candidates.filter(word => Math.abs(word.length - this.targetLength) === distance);
        return candidates[Math.floor(Math.random() * candidates.length)];
    }

    reconcileServerResponse(action: { action: string; index?: number }, response: {
        isValid: boolean; message?: string; isCorrect?: boolean | null; isCompleted?: boolean;
        feedbackData?: { round?: number; stimulus?: string; displayDurationMs?: number; targetLength?: number; trial?: TrialRecord };
    }): void {
        if (!this.serverAuthoritative || !this.state.isRunning) return;
        this.awaitingServer = false;
        if (!response.isValid) {
            this.stop();
            this.callbacks.onError(response.message || 'Takistoskop yanıtı doğrulanamadı.');
            return;
        }
        const data = response.feedbackData;
        if (action.action === 'tachistoscope_present') {
            if (this.state.isPaused) return;
            if (data?.round !== this.currentStimulusIndex || typeof data.stimulus !== 'string' || !data.stimulus.length) {
                this.stop(); this.callbacks.onError('Takistoskop uyaranı sunucudan eksik döndü.'); return;
            }
            this.currentDurationMs = boundedInteger(data.displayDurationMs, this.currentDurationMs, 50, 5000);
            this.targetLength = boundedInteger(data.targetLength, this.targetLength, 1, 100);
            this.currentStimulus = data.stimulus;
            this.displayStimulus();
            return;
        }
        if (action.action !== 'tachistoscope_answer' || data?.round !== this.currentStimulusIndex + 1) {
            this.stop(); this.callbacks.onError('Takistoskop tur sırası doğrulanamadı.'); return;
        }
        const correct = response.isCorrect === true;
        if (response.isCorrect != null && !(this.config as any).isAssessmentMode) {
            this.trials.push(data.trial || { stimulus: this.pendingStimulus, userAnswer: this.pendingAnswer,
                isCorrect: correct, responseTimeMs: Math.max(0, Date.now() - this.stimulusShowTime - this.currentDurationMs),
                displayDurationMs: this.currentDurationMs });
            if (correct) this.correctCount++; else this.state.errors++;
        }
        this.currentStimulusIndex = data.round;
        this.state.currentStep = data.round;
        this.state.accuracy = this.correctCount + this.state.errors
            ? Math.round(this.correctCount / (this.correctCount + this.state.errors) * 100) : 0;
        this.state.score = this.state.accuracy;
        this.currentDurationMs = boundedInteger(data.displayDurationMs, this.currentDurationMs, 50, 5000);
        this.targetLength = boundedInteger(data.targetLength, this.targetLength, 1, 100);
        this.callbacks.onStepComplete(this.currentStimulusIndex, correct);
        this.callbacks.onStateChange({ ...this.state });
        if (this.state.isPaused) return;
        this.scheduleTransition(() => this.showNextStimulus(),
            this.currentStimulusIndex >= this.stimuli.length ? 500 : this.config.timing.intervalMs);
    }

    start(): void {
        if (this.state.isRunning || this.state.isCompleted) return;
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.startTime = Date.now();
        if (!this.serverAuthoritative) this.currentStimulusIndex = 0;
        this.trials = [];
        this.correctCount = this.serverAuthoritative ? this.restoredCorrectCount : 0;
        this.state.errors = this.serverAuthoritative ? this.restoredIncorrectCount : 0;
        this.state.currentStep = this.currentStimulusIndex;
        this.state.accuracy = this.isRsvpMode() ? 100 : this.correctCount + this.state.errors
            ? Math.round(this.correctCount / (this.correctCount + this.state.errors) * 100) : 0;
        this.consecutiveCorrect = 0;

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

        if (this.serverAuthoritative) {
            this.awaitingServer = true;
            this.callbacks.onAction({ action: 'tachistoscope_present', index: this.currentStimulusIndex });
            return;
        }
        this.currentStimulus = this.isRsvpMode() ? this.stimuli[this.currentStimulusIndex] : this.selectPreviewStimulus();
        this.displayStimulus();
    }

    private displayStimulus(): void {
        if (!this.state.isRunning || this.state.isPaused) return;
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
        if ((this.config as any).exerciseTypeName === 'Tachistoscope') return false;
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
                this.scheduleTransition(() => this.showNextStimulus(), gapMs);
            } else {
                this.showNextStimulus();
            }
        }
    }

    private scheduleTransition(callback: () => void, delayMs: number): void {
        clearTimeout(this.transitionTimeout);
        this.transitionCallback = callback;
        this.transitionRemainingMs = delayMs;
        this.transitionEndsAt = Date.now() + delayMs;
        this.transitionTimeout = setTimeout(() => {
            const pendingCallback = this.transitionCallback;
            this.transitionCallback = null;
            this.transitionRemainingMs = 0;
            pendingCallback?.();
        }, delayMs);
    }

    pause(): void {
        if (this.state.isPaused) return;
        this.state.isPaused = true;
        this.pauseStartTime = Date.now();
        clearTimeout(this.stimulusTimeout);
        if (!this.isRsvpMode()) {
            this.isShowingStimulus = false;
            this.isShowingFixation = false;
            if (this.serverAuthoritative || !this.isWaitingForAnswer) {
                this.isWaitingForAnswer = false;
                this.currentStimulus = '';
            }
        }
        if (this.transitionCallback) {
            this.transitionRemainingMs = Math.max(0, this.transitionEndsAt - this.pauseStartTime);
            clearTimeout(this.transitionTimeout);
        }
        this.callbacks.onPause();
        this.callbacks.onStateChange({ ...this.state });
    }

    resume(): void {
        if (!this.state.isPaused) return;

        // Adjust startTime to account for pause duration
        const pauseDuration = Date.now() - this.pauseStartTime;
        this.startTime += pauseDuration;
        if (this.isWaitingForAnswer) this.stimulusShowTime += pauseDuration;

        this.state.isPaused = false;
        if (this.serverAuthoritative) {
            this.awaitingServer = false;
            this.showNextStimulus();
        } else if (this.isWaitingForAnswer) {
            // Continue waiting for answer
            this.callbacks.onStateChange({ ...this.state });
        } else if (this.transitionCallback) {
            this.scheduleTransition(this.transitionCallback, this.transitionRemainingMs);
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
        clearTimeout(this.transitionTimeout);
        this.transitionCallback = null;
        this.transitionRemainingMs = 0;
        this.isShowingStimulus = false;
        this.isShowingFixation = false;
        this.isWaitingForAnswer = false;
        this.awaitingServer = false;
        this.callbacks.onStateChange({ ...this.state });
    }

    /**
     * Force finish the exercise (e.g. timeout)
     */
    finish(): void {
        this.complete(false);
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
        this.consecutiveCorrect = 0;
        this.targetLength = this.initialTargetLength;
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
        if (!this.isWaitingForAnswer || !this.state.isRunning || this.state.isPaused || this.awaitingServer) {
            return;
        }

        const responseTime = Date.now() - this.stimulusShowTime - this.currentDurationMs;
        const userAnswer = (input.answer || '').trim().toLocaleLowerCase('tr-TR');
        const correctAnswer = this.currentStimulus.toLocaleLowerCase('tr-TR');
        if (this.serverAuthoritative) {
            this.pendingAnswer = input.answer || '';
            this.pendingStimulus = this.currentStimulus;
            this.awaitingServer = true;
            this.isWaitingForAnswer = false;
            this.callbacks.onAction({ action: 'tachistoscope_answer', index: this.currentStimulusIndex, answer: this.pendingAnswer });
            this.callbacks.onStateChange({ ...this.state });
            return;
        }

        // Compare normalized Turkish text exactly; near-matches are not correct answers.
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
            // Increase difficulty after two consecutive correct answers.
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
            this.scheduleTransition(() => this.complete(), 500);
        } else {
            // Respect the configured interval before the next stimulus.
            this.scheduleTransition(() => this.showNextStimulus(), this.config.timing.intervalMs);
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
            this.targetLength = Math.max(this.initialTargetLength, this.targetLength - 1);
        }
    }

    private checkFastAdaptation(isCorrect: boolean): void {
        this.consecutiveCorrect = isCorrect ? this.consecutiveCorrect + 1 : 0;

        // Every 2 correct answers, speed up (Başarı Durumu)
        if (this.consecutiveCorrect >= 2) {
            const minDuration = this.config.adaptive?.minDurationMs || 50;
            const newDuration = Math.max(minDuration, Math.round(this.currentDurationMs * 0.9));
            this.currentDurationMs = newDuration;
            const maxLength = this.config.content?.type === 'number' || this.config.content?.type === 'letter'
                ? 12 : Math.max(this.targetLength, ...this.contentPool.map(word => word.length));
            this.targetLength = Math.min(maxLength, this.targetLength + 1);
            this.consecutiveCorrect = 0;
        }
    }

    /**
     * Compare normalized Turkish answers without treating typos as correct.
     */
    private calculateSimilarity(answer1: string, answer2: string): boolean {
        const normalize = (value: string) => value.trim().replace(/\s+/g, ' ').normalize('NFC').toLocaleLowerCase('tr-TR');
        return normalize(answer1) === normalize(answer2);
    }



    private complete(completedNaturally = true): void {
        if (this.state.isCompleted) return;
        const completedSteps = Math.min(this.stimuli.length, this.currentStimulusIndex);
        const coverage = this.stimuli.length > 0 ? completedSteps / this.stimuli.length : 0;
        const score = completedNaturally ? this.state.accuracy : Math.round(this.state.accuracy * coverage);
        this.state.isCompleted = true;
        this.state.isRunning = false;
        this.state.score = score;
        if (this.startTime) this.state.timeElapsed = Date.now() - this.startTime - (this.state.isPaused ? Date.now() - this.pauseStartTime : 0);
        clearInterval(this.timerInterval);
        clearTimeout(this.stimulusTimeout);
        clearTimeout(this.transitionTimeout);
        this.transitionCallback = null;

        const avgResponseTime = this.trials.length > 0
            ? Math.round(this.trials.reduce((sum, t) => sum + t.responseTimeMs, 0) / this.trials.length)
            : 0;

        const speedImprovement = this.initialDurationMs > 0
            ? Math.round((this.initialDurationMs - this.currentDurationMs) / this.initialDurationMs * 100)
            : 0;

        const result: EngineResult = {
            score,
            accuracy: this.state.accuracy,
            totalTime: this.state.timeElapsed,
            totalSteps: this.stimuli.length,
            completedSteps,
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
                wpm: this.isRsvpMode() ? Math.round(60000 / this.currentDurationMs) : null,
                readWordCount: this.currentStimulusIndex,
                totalWordCount: this.stimuli.length,
                durationSeconds: Math.round(this.state.timeElapsed / 1000),
                timedOut: !completedNaturally
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
        return this.isWaitingForAnswer && !this.state.isPaused;
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
