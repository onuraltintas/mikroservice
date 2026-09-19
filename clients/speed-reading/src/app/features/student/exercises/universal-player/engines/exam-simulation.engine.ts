/**
 * Exam Simulation Engine
 * Sınav simülasyonu için engine.
 * Reading Comprehension ile benzer yapıdadır ancak sınav tipi özelliklerini barındırır.
 * 
 * Backend'den gelen veriler:
 * - readingTextContent: Metin içeriği (Varsa)
 * - wordCount: Kelime sayısı
 * - questions: Soru listesi (Her sorunun kendi metni olabilir)
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';
import { boundedInteger, boundedText, caseInsensitiveField, mergeCaseInsensitiveRecords, recordOrEmpty } from './reading-pacer-safety';

interface ExamSimulationConfig extends EngineConfig {
    // Backend session data
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

    // Exam specific
    examType?: string;
    difficultyLevel?: number;

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

export class ExamSimulationEngine implements BaseEngine {
    engineType = 'exam_simulation' as const;
    displayName = 'Sınav Simülasyonu';

    private config!: ExamSimulationConfig;
    private callbacks!: EngineCallbacks;
    state!: EngineState;

    private timerInterval: any;
    private text: string = '';
    private title: string = '';
    private words: string[] = [];
    private configuredTotalSteps = 0;
    private readingState: ReadingState = {
        phase: 'reading',
        scrollProgress: 0,
        hasScrolledToEnd: false
    };

    // Default text pool for testing (fallback only)
    private defaultTexts = [
        `Sınav simülasyonu başlıyor. Lütfen metni dikkatlice okuyunuz ve ardından soruları cevaplayınız. 
        Bu modülde gerçek sınav deneyimine uygun olarak süre ve dikkat faktörleri ölçülmektedir. 
        Her soru farklı bir bilişsel beceriyi test etmek üzere tasarlanmıştır. Başarılar dileriz.`
    ];

    initialize(config: ExamSimulationConfig, callbacks: EngineCallbacks): void {
        this.callbacks = callbacks;
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

        // Metin belirleme mantığı
        // ExamSimulation'da metin genellikle 'questions' dizisinin içindeki ilk sorudan veya genel 'content'ten gelir.
        // Şimdilik genel content'e bakıyoruz.

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
        const contentWordCount = caseInsensitiveField(nestedContentRecord, 'wordCount')
            ?? caseInsensitiveField(rootContentRecord, 'wordCount');
        const dedicatedRawText = caseInsensitiveField(nested, 'readingTextContent')
            ?? caseInsensitiveField(cfg, 'readingTextContent')
            ?? contentText
            ?? caseInsensitiveField(nested, 'text')
            ?? caseInsensitiveField(cfg, 'text');
        const assessmentMode = cfg.isAssessmentMode === true || cfg.IsAssessmentMode === true;
        const invalidAssessmentText = dedicatedRawText !== undefined
            && (typeof dedicatedRawText !== 'string' || dedicatedRawText.length > 100_000);
        let extractedText = typeof dedicatedRawText === 'string'
            ? boundedText(dedicatedRawText, '', 100_000)
            : '';
        if ((!extractedText || invalidAssessmentText) && assessmentMode) {
            throw new Error('Assessment reading text was not provided by the server.');
        }

        // Check if text is in questions array (First question's text)
        // Backend sends PascalCase (Questions), also check camelCase
        const questionsArray = caseInsensitiveField(nested, 'questions') ?? caseInsensitiveField(cfg, 'questions');
        if (!assessmentMode && !extractedText && questionsArray && Array.isArray(questionsArray) && questionsArray.length > 0) {
            // Try various casing and property names
            const q = questionsArray[0];
            const rawQuestionText = caseInsensitiveField(recordOrEmpty(q), 'content')
                ?? caseInsensitiveField(recordOrEmpty(q), 'questionText')
                ?? caseInsensitiveField(recordOrEmpty(q), 'text');
            extractedText = typeof rawQuestionText === 'string'
                ? boundedText(rawQuestionText, '', 100_000) : '';
            if (extractedText) {
            }
        }

        this.text = extractedText || this.getRandomText();

        this.title = cfg.ReadingTextTitle ||
            config.readingTextTitle ||
            cfg.Content?.Title ||
            cfg.content?.title ||
            'Sınav Metni';

        this.words = this.text.split(/\s+/).filter(w => w.length > 0);

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

    }

    private getRandomText(): string {
        const index = Math.floor(Math.random() * this.defaultTexts.length);
        return this.defaultTexts[index];
    }

    getTitle(): string {
        return this.title;
    }

    private startTime = 0;
    private pauseStartTime = 0;

    start(): void {
        if (this.state.isRunning || this.state.isCompleted) return;
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.state.isCompleted = false;
        this.state.timeElapsed = 0;
        this.readingState.phase = 'reading';
        this.startTime = Date.now();

        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;

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
        this.state.isRunning = false;
        this.state.isPaused = false;
        this.state.isCompleted = false;
        this.state.timeElapsed = 0;
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
        if (input.scrollProgress !== undefined) {
            this.readingState.scrollProgress = input.scrollProgress;
            if (input.scrollProgress >= 95) {
                this.readingState.hasScrolledToEnd = true;
            }
        }

        if (input.action === 'complete_reading') {
            this.completeReading();
        }
    }

    completeReading(force = false): void {
        if (this.state.isCompleted || !this.state.isRunning) return;
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

        const result: EngineResult = {
            score: 0,
            accuracy: 0,
            totalTime: this.state.timeElapsed,
            totalSteps: this.configuredTotalSteps,
            completedSteps: this.configuredTotalSteps,
            errors: 0,
            details: {
                readingTimeMs: this.state.timeElapsed
            }
        };

        this.callbacks.onComplete(result);
    }

    // Public getters
    getText(): string { return this.text; }
    getWords(): string[] { return this.words; }
    getWordCount(): number { return this.words.length; }
    getFontSize(): string { return this.config.display?.fontSize || 'medium'; }
    getLineHeight(): number { return this.config.display?.lineHeight || 1.8; }
    getReadingPhase(): string { return this.readingState.phase; }
}
