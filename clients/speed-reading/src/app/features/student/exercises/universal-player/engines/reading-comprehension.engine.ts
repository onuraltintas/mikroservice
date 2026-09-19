/**
 * Reading Comprehension Engine
 * Serbest okuma + anlama soruları için engine.
 * 
 * Kullanıcı metni kendi hızında okur, "Okudum" butonuna basar,
 * ardından anlama sorularını cevaplar.
 * 
 * Backend'den gelen veriler:
 * - readingTextContent: Metin içeriği
 * - wordCount: Kelime sayısı
 * - readingTextTitle: Metin başlığı
 * - questions: Anlama soruları
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';
import { boundedInteger, boundedText, caseInsensitiveField, mergeCaseInsensitiveRecords, recordOrEmpty } from './reading-pacer-safety';

interface ReadingComprehensionConfig extends EngineConfig {
    // Backend session data (from ComprehensionEngine)
    readingTextContent?: string;
    readingTextTitle?: string;
    wordCount?: number;

    // Alternative content source
    content?: string | {
        text?: string;
        wordCount?: number;
        title?: string;
        source?: string;
        complexity?: number;
        questionCount?: number;
    };

    // Direct text field
    text?: string;

    display?: {
        fontSize?: 'small' | 'medium' | 'large';
        lineHeight?: number;
        showProgress?: boolean;
    };
    timing?: {
        minReadingTimeMs?: number;  // Minimum time to read before allowing completion
        maxReadingTimeMs?: number;  // Maximum time allowed
    };
}

interface ReadingState {
    phase: 'reading' | 'completed';
    scrollProgress: number;  // 0-100
    hasScrolledToEnd: boolean;
}

export class ReadingComprehensionEngine implements BaseEngine {
    engineType: 'reading_comprehension' | 'free_reading';
    displayName = 'Anlama Testi';

    private config!: ReadingComprehensionConfig;
    private callbacks!: EngineCallbacks;
    state!: EngineState;

    private timerInterval: any;
    private startTime = 0;
    private pauseStartTime = 0;
    private text: string = '';
    private title: string = '';
    private words: string[] = [];
    private configuredTotalSteps = 0;
    private readingState: ReadingState = {
        phase: 'reading',
        scrollProgress: 0,
        hasScrolledToEnd: false
    };

    constructor(engineType: 'reading_comprehension' | 'free_reading' = 'reading_comprehension') {
        this.engineType = engineType;
    }

    // Default text pool for testing (fallback only)
    private defaultTexts = [
        `Okuma, bilgiye ulaşmanın en temel yollarından biridir. İnsanlık tarihi boyunca yazılı metinler, 
        bilginin nesilden nesile aktarılmasını sağlamıştır. Günümüzde dijital çağda bile okuma becerisi, 
        öğrenmenin ve gelişimin temel taşı olmaya devam etmektedir. Hızlı ve etkili okuma, modern 
        dünyada başarının anahtarlarından biridir. Araştırmalar gösteriyor ki düzenli okuma alışkanlığı 
        olan bireyler, hem akademik hem de profesyonel yaşamda daha başarılı olmaktadır.`,

        `Beyin, sürekli egzersiz gerektiren bir organdır. Tıpkı kaslarımızı güçlendirmek için 
        fiziksel egzersiz yapmamız gerektiği gibi, zihinsel kapasitemizi artırmak için de beynimizi 
        çalıştırmalıyız. Okuma, yazma, bulmaca çözme ve yeni beceriler öğrenme gibi aktiviteler 
        beyin sağlığını korumaya yardımcı olur. Düzenli zihinsel egzersiz, yaşlanmayla birlikte 
        ortaya çıkabilecek bilişsel gerilemeyi yavaşlatabilir.`,

        `Dikkat ve odaklanma, başarılı öğrenmenin temel unsurlarıdır. Günümüzün dikkat dağıtıcı 
        dünyasında, tek bir göreve odaklanma becerisi giderek daha değerli hale gelmektedir. 
        Meditasyon, düzenli uyku ve fiziksel egzersiz, odaklanma kapasitesini artırabilir. 
        Ayrıca, çoklu görev yapmaktan kaçınmak ve belirli zaman dilimlerinde tek bir işe 
        konsantre olmak da verimliliği artırır.`
    ];

    initialize(config: ReadingComprehensionConfig, callbacks: EngineCallbacks): void {
        this.callbacks = callbacks;

        // Debug full config to see available keys

        // Get text from backend session data (priority order)
        // 1. readingTextContent (direct from ComprehensionEngine)
        // 2. Content.Text or content.text (C# sends PascalCase!)
        // 3. text (direct field)
        // 4. fallback to default
        // Backend sends SpeedReadingSessionData with:
        // - Content.Text (PascalCase from C#)
        // - ReadingTextTitle (PascalCase)
        // - Content.WordCount
        // - Questions[]
        const cfg = config as any;
        const nested = recordOrEmpty(caseInsensitiveField(cfg, 'engineConfig'));
        const timing = mergeCaseInsensitiveRecords(cfg, nested, 'timing');
        const display = mergeCaseInsensitiveRecords(cfg, nested, 'display');
        const configuredFontSize = typeof display['fontsize'] === 'string'
            ? display['fontsize'].toLowerCase() : '';
        const fontSize = ['small', 'medium', 'large'].includes(configuredFontSize)
            ? configuredFontSize as 'small' | 'medium' | 'large' : 'medium';
        const lineHeight = typeof display['lineheight'] === 'number' && Number.isFinite(display['lineheight'])
            ? Math.min(3, Math.max(1, display['lineheight'])) : 1.8;
        const minReadingTimeMs = boundedInteger(timing['minreadingtimems'], 0, 0, 3_600_000);
        const configuredMaximum = boundedInteger(timing['maxreadingtimems'], 0, 0, 3_600_000);
        const maxReadingTimeMs = configuredMaximum > 0
            ? Math.max(minReadingTimeMs, configuredMaximum) : 0;
        this.config = {
            ...config,
            timing: { minReadingTimeMs, maxReadingTimeMs },
            display: { ...display, fontSize, lineHeight }
        };
        // The session endpoint serializes the reading body as `content` when
        // it returns a public session snapshot. Older preview/configuration
        // payloads use an object (`content.text`) or `Content.Text`; accept
        // both shapes so a real snapshot is always shown to the student.
        const nestedContent = caseInsensitiveField(nested, 'content');
        const rootContent = caseInsensitiveField(cfg, 'content');
        const nestedContentRecord = recordOrEmpty(nestedContent);
        const rootContentRecord = recordOrEmpty(rootContent);
        const contentText = (typeof nestedContent === 'string'
            ? nestedContent
            : caseInsensitiveField(nestedContentRecord, 'text'))
            ?? (typeof rootContent === 'string'
                ? rootContent
                : caseInsensitiveField(rootContentRecord, 'text'));
        const contentTitle = caseInsensitiveField(nestedContentRecord, 'title')
            ?? caseInsensitiveField(rootContentRecord, 'title');
        const contentWordCount = caseInsensitiveField(nestedContentRecord, 'wordCount')
            ?? caseInsensitiveField(rootContentRecord, 'wordCount');
        const assessmentMode = cfg.isAssessmentMode === true || cfg.IsAssessmentMode === true;
        const rawText = caseInsensitiveField(nested, 'readingTextContent')
            ?? caseInsensitiveField(cfg, 'readingTextContent')
            ?? contentText
            ?? caseInsensitiveField(nested, 'text')
            ?? caseInsensitiveField(cfg, 'text');
        const resolvedText = typeof rawText === 'string'
            ? boundedText(rawText, '', 100_000)
            : '';
        const invalidAssessmentText = rawText !== undefined
            && (typeof rawText !== 'string' || rawText.length > 100_000);
        if ((!resolvedText || invalidAssessmentText) && assessmentMode) {
            throw new Error('Assessment reading text was not provided by the server.');
        }
        this.text = resolvedText || this.getRandomText();

        this.title = cfg.ReadingTextTitle ||   // PascalCase from C#
            config.readingTextTitle ||
            cfg.Content?.Title ||
            contentTitle ||
            '';
        this.words = this.text.split(/\s+/).filter(w => w.length > 0);

        // Use wordCount from backend if available
        const wordCount = boundedInteger(
            caseInsensitiveField(nested, 'wordCount')
                ?? contentWordCount
                ?? caseInsensitiveField(cfg, 'wordCount'),
            this.words.length, 1, 100_000);

        this.state = {
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            totalSteps: wordCount,
            score: 0,
            accuracy: 100,
            timeElapsed: 0,
            errors: 0
        };
        this.configuredTotalSteps = wordCount;

        this.readingState = {
            phase: 'reading',
            scrollProgress: 0,
            hasScrolledToEnd: false
        };

        if (this.title) {
        }
        // Log text source for debugging
        if (config.readingTextContent) {
        } else if (cfg.Content?.Text) {
        } else if (contentText) {
        } else if (config.text) {
        } else {
        }
    }

    private getRandomText(): string {
        const index = Math.floor(Math.random() * this.defaultTexts.length);
        return this.defaultTexts[index];
    }

    getTitle(): string {
        return this.title;
    }

    start(): void {
        if (this.state.isRunning || this.state.isCompleted) return;
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.state.isCompleted = false;
        this.state.timeElapsed = 0;
        this.readingState.phase = 'reading';

        this.startTime = Date.now();

        // Start timer
        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;

                // Check max time limit (crucial for Skimming/Speed Reading tests)
                const maxTime = this.config.timing?.maxReadingTimeMs;
                if (maxTime && this.state.timeElapsed >= maxTime) {
                    this.completeReading(true);
                }

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

        // Adjust startTime by pause duration
        const pauseDuration = Date.now() - this.pauseStartTime;
        this.startTime += pauseDuration;

        this.state.isPaused = false;
        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        clearInterval(this.timerInterval);
        this.state.isRunning = false;
        this.callbacks.onStateChange({ ...this.state });
    }

    reset(): void {
        clearInterval(this.timerInterval);
        this.state = {
            isRunning: false,
            isPaused: false,
            isCompleted: false,
            currentStep: 0,
            totalSteps: this.configuredTotalSteps,
            score: 0,
            accuracy: 100,
            timeElapsed: 0,
            errors: 0
        };
        this.readingState = {
            phase: 'reading',
            scrollProgress: 0,
            hasScrolledToEnd: false
        };
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        clearInterval(this.timerInterval);
    }

    handleInput(input: any): void {
        // Handle scroll progress updates
        if (input.scrollProgress !== undefined) {
            this.readingState.scrollProgress = input.scrollProgress;
            if (input.scrollProgress >= 95) {
                this.readingState.hasScrolledToEnd = true;
            }
        }

        // Handle "I'm done reading" action
        if (input.action === 'complete_reading') {
            this.completeReading();
        }
    }

    /**
     * Called when user clicks "Okudum" button
     */
    completeReading(force = false): void {
        if (this.state.isCompleted || !this.state.isRunning) return;
        // Check minimum reading time (optional)
        const minTime = this.config.timing?.minReadingTimeMs || 0;
        if (!force && this.state.timeElapsed < minTime) return;

        this.readingState.phase = 'completed';
        this.complete();
    }

    private complete(): void {
        if (this.state.isCompleted) return;
        clearInterval(this.timerInterval);

        this.state.isCompleted = true;
        this.state.isRunning = false;
        this.state.currentStep = this.configuredTotalSteps;
        this.state.score = 0;
        this.state.accuracy = 0;

        // Notify component that reading is done
        this.callbacks.onStateChange({ ...this.state });

        // Calculate WPM
        const readingTimeMinutes = this.state.timeElapsed / 1000 / 60;
        const wpm = readingTimeMinutes > 0 ? Math.round(this.words.length / readingTimeMinutes) : 0;

        const result: EngineResult = {
            score: 0,
            accuracy: 0,
            totalTime: this.state.timeElapsed,
            totalSteps: this.configuredTotalSteps,
            completedSteps: this.configuredTotalSteps,
            errors: 0,
            details: {
                wpm: wpm,
                wordCount: this.words.length,
                readingTimeMs: this.state.timeElapsed,
                scrolledToEnd: this.readingState.hasScrolledToEnd
            }
        };

        this.callbacks.onComplete(result);
    }

    // Public getters for template
    getText(): string {
        return this.text;
    }

    getWords(): string[] {
        return this.words;
    }

    getWordCount(): number {
        return this.words.length;
    }

    getFontSize(): string {
        return this.config.display?.fontSize || 'medium';
    }

    getLineHeight(): number {
        return this.config.display?.lineHeight || 1.8;
    }

    getReadingPhase(): string {
        return this.readingState.phase;
    }

    hasReachedEnd(): boolean {
        return this.readingState.hasScrolledToEnd;
    }

    getScrollProgress(): number {
        return this.readingState.scrollProgress;
    }

    getMinReadingTime(): number {
        return this.config.timing?.minReadingTimeMs || 0;
    }

    canComplete(): boolean {
        // Can complete if either scrolled to end or spent enough time
        const minTime = this.config.timing?.minReadingTimeMs ?? 5000;
        return this.state.timeElapsed >= minTime || this.readingState.hasScrolledToEnd;
    }
}
