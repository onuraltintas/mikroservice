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

interface ReadingComprehensionConfig extends EngineConfig {
    // Backend session data (from ComprehensionEngine)
    readingTextContent?: string;
    readingTextTitle?: string;
    wordCount?: number;

    // Alternative content source
    content?: {
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
    engineType = 'reading_comprehension' as const;
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
    private readingState: ReadingState = {
        phase: 'reading',
        scrollProgress: 0,
        hasScrolledToEnd: false
    };

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
        this.config = config;
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
        this.text = config.readingTextContent ||
            cfg.Content?.Text ||      // PascalCase from C#
            cfg.content?.text ||      // camelCase (if converted)
            config.text ||
            this.getRandomText();

        this.title = cfg.ReadingTextTitle ||   // PascalCase from C#
            config.readingTextTitle ||
            cfg.Content?.Title ||
            cfg.content?.title ||
            '';
        this.words = this.text.split(/\s+/).filter(w => w.length > 0);

        // Use wordCount from backend if available
        const wordCount = cfg.Content?.WordCount ||  // PascalCase
            cfg.content?.wordCount ||                // camelCase
            config.wordCount ||
            this.words.length;

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
        } else if (cfg.content?.text) {
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
                    this.completeReading();
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
            totalSteps: this.words.length,
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
    completeReading(): void {
        // Check minimum reading time (optional)
        const minTime = this.config.timing?.minReadingTimeMs || 0;
        if (this.state.timeElapsed < minTime) {
            // Could show a warning to user here
        }

        this.readingState.phase = 'completed';
        this.complete();
    }

    private complete(): void {
        clearInterval(this.timerInterval);

        this.state.isCompleted = true;
        this.state.isRunning = false;
        this.state.currentStep = this.words.length;
        this.state.score = 100; // Initial score, will be updated after questions

        // Notify component that reading is done
        this.callbacks.onStateChange({ ...this.state });

        // Calculate WPM
        const readingTimeMinutes = this.state.timeElapsed / 1000 / 60;
        const wpm = readingTimeMinutes > 0 ? Math.round(this.words.length / readingTimeMinutes) : 0;

        const result: EngineResult = {
            score: 100, // Will be updated after questions
            accuracy: 100,
            totalTime: this.state.timeElapsed,
            totalSteps: this.words.length,
            completedSteps: this.words.length,
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
        const minTime = this.config.timing?.minReadingTimeMs || 5000; // Default 5 seconds minimum
        return this.state.timeElapsed >= minTime || this.readingState.hasScrolledToEnd;
    }
}
