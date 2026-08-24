/**
 * Scan & Find Engine
 * Scanning egzersizleri için.
 * Metin içinde belirli kelimeleri/hedefleri bulma.
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';

export interface ScanFindConfig extends EngineConfig {
    // Backend session data format
    scanningRounds?: Array<{
        roundNumber: number;
        textContent: string;
        textTitle: string;
        wordCount: number;
        targets: string[];
        foundTargets: string[];
        searchTimeMs: number;
        isCompleted: boolean;
    }>;
    currentRound?: number;

    // Alternative format
    content?: {
        source: string;       // 'text_id', 'random_text'
        text?: string;
        wordCount?: number;
    };
    targets?: {
        words: string[];      // Aranacak kelimeler
        caseSensitive?: boolean;
        mode?: 'find_all' | 'find_any'; // Tüm geçişleri mi bulacak, yoksa kelime listesindekilerin herbirini bir kez mi?
    };
    timing?: {
        timeLimitSec?: number;
    };
    visuals?: {
        fontSize?: string;
        highlightColor?: string;
    };
    timeLimitSeconds?: number;
    timeLimit?: number; // Added for compatibility with recent Seeder update
}

export class ScanFindEngine implements BaseEngine {
    readonly engineType = 'scan_find';
    readonly displayName = 'Tarama ve Bulma';

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

    private config!: ScanFindConfig;
    private callbacks!: EngineCallbacks;
    private startTime = 0;
    private timerInterval: any;

    // Game State
    private words: Array<{ text: string, id: number, isTarget: boolean, found: boolean }> = [];
    private foundCount = 0;
    private targetCount = 0;
    private targetWords: string[] = [];
    private currentRoundIndex = 0;
    private totalFoundUniqueAcrossRounds = 0;
    private foundUniqueWordsInRound = new Set<string>();

    // Dummy text generator
    private static readonly TEXT_POOL = [
        "Hızlı okuma becerisi, bilgi çağında hayati bir yetenektir.",
        "Göz kaslarını geliştirmek için düzenli egzersiz yapmak gerekir.",
        "Periferik görüş alanını genişleterek daha fazla kelime görebilirsiniz.",
        "Odaklanma süresini artırmak, okuma verimliliğini doğrudan etkiler.",
        "Beyin, görsel bilgiyi işleme konusunda olağanüstü bir kapasiteye sahiptir.",
        "Tekrar okuma alışkanlığı, okuma hızını düşüren en büyük faktördür.",
        "İç seslendirmeyi azaltmak, daha hızlı okumanın anahtarıdır.",
        "Okuma sırasında aktif olmak, metni daha iyi anlamayı sağlar.",
        "Gözler metin üzerinde kayarken, beyin anlamı oluşturur.",
        "Egzersizler zorluk seviyesine göre kademeli olarak artırılmalıdır."
    ];

    initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
        this.config = config as ScanFindConfig;
        this.callbacks = callbacks;
        this.currentRoundIndex = this.config.currentRound || 0;
        this.totalFoundUniqueAcrossRounds = 0;

        this.generateContent();
    }

    private generateContent(): void {
        let rawText = "";
        let targetWordsList: string[] = [];

        // Use the internal state tracker for rounds
        const currentRound = this.config.scanningRounds?.[this.currentRoundIndex];

        if (currentRound) {
            // Backend format
            rawText = currentRound.textContent || "";
            targetWordsList = currentRound.targets || [];
        } else if (this.config.content?.text) {
            // Alternative format
            rawText = this.config.content.text;
            targetWordsList = this.config.targets?.words || [];
        } else {
            // Fallback to dummy text
            const count = this.config.content?.wordCount || 100;
            const sentenceCount = Math.ceil(count / 10);
            for (let i = 0; i < sentenceCount; i++) {
                rawText += ScanFindEngine.TEXT_POOL[Math.floor(Math.random() * ScanFindEngine.TEXT_POOL.length)] + " ";
            }
        }

        const caseSensitive = this.config.targets?.caseSensitive || false;
        this.targetWords = targetWordsList.map(w => caseSensitive ? w : w.toLowerCase());

        const splitWords = rawText.split(/\s+/).filter(w => w.length > 0);

        this.words = splitWords.map((w, index) => {
            const cleanWord = w.replace(/[.,;!?:'"()]/g, '');
            const checkWord = caseSensitive ? cleanWord : cleanWord.toLowerCase();
            const isTarget = this.targetWords.includes(checkWord);

            return {
                text: w,
                id: index,
                isTarget: isTarget,
                found: false
            };
        });

        // Use unique word count for targetCount to match UI chips
        this.targetCount = this.targetWords.length;
        this.foundCount = 0;
        this.foundUniqueWordsInRound.clear();

        this.state.currentStep = 0;
        this.state.totalSteps = this.targetCount;
    }

    getTargetWords(): string[] {
        return this.targetWords;
    }

    start(): void {
        this.state.isRunning = true;
        this.state.isPaused = false;
        this.startTime = Date.now() - this.state.timeElapsed;

        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;

                const timeLimitSec = this.config.timeLimit || this.config.timeLimitSeconds || this.config.timing?.timeLimitSec;
                if (timeLimitSec && this.state.timeElapsed > timeLimitSec * 1000) {
                    this.complete();
                }

                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        this.callbacks.onStart();
        this.callbacks.onStateChange({ ...this.state });
    }

    handleWordClick(index: number): void {
        if (!this.state.isRunning || this.state.isPaused || this.state.isCompleted) return;

        const word = this.words[index];
        if (!word) return;

        if (word.isTarget && !word.found) {
            word.found = true;

            // Track unique word discovery
            const caseSensitive = this.config.targets?.caseSensitive || false;
            const cleanText = word.text.replace(/[.,;!?:'"()]/g, '');
            const checkWord = caseSensitive ? cleanText : cleanText.toLowerCase();

            if (!this.foundUniqueWordsInRound.has(checkWord)) {
                this.foundUniqueWordsInRound.add(checkWord);
                this.foundCount = this.foundUniqueWordsInRound.size;
                this.state.currentStep = this.foundCount;
                this.callbacks.onStepComplete(this.foundCount, true);
            }

            if (this.foundUniqueWordsInRound.size >= this.targetCount) {
                this.nextRound();
            }
        } else if (!word.isTarget) {
            this.state.errors++;
        }

        this.callbacks.onStateChange({ ...this.state });
    }

    private nextRound(): void {
        this.totalFoundUniqueAcrossRounds += this.foundUniqueWordsInRound.size;
        this.currentRoundIndex++;

        if (this.config.scanningRounds && this.currentRoundIndex < this.config.scanningRounds.length) {
            this.foundCount = 0;
            this.foundUniqueWordsInRound.clear();
            this.generateContent();
            this.callbacks.onStateChange({ ...this.state });
        } else {
            this.complete();
        }
    }

    pause(): void {
        this.state.isPaused = true;
        this.callbacks.onPause();
        this.callbacks.onStateChange({ ...this.state });
    }

    resume(): void {
        this.state.isPaused = false;
        // Recalculate startTime to account for pause duration
        this.startTime = Date.now() - this.state.timeElapsed;
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
            totalSteps: this.config.scanningRounds?.length || 1,
            score: 0,
            accuracy: 0,
            timeElapsed: 0,
            errors: 0
        };
        this.currentRoundIndex = 0;
        this.foundCount = 0;
        this.totalFoundUniqueAcrossRounds = 0;
        this.foundUniqueWordsInRound.clear();
        this.generateContent();
        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.stop();
    }

    handleInput(input: any): void {
        if (input.type === 'click_word') {
            this.handleWordClick(input.wordIndex);
        }
    }

    private complete(): void {
        this.totalFoundUniqueAcrossRounds += this.foundUniqueWordsInRound.size;
        this.state.isCompleted = true;
        this.state.isRunning = false;

        const totalRounds = this.config.scanningRounds?.length || 1;
        if (this.currentRoundIndex >= totalRounds) {
            this.state.accuracy = 100;
        } else {
            this.state.accuracy = Math.round((this.currentRoundIndex / totalRounds) * 100);
        }

        this.state.score = (this.totalFoundUniqueAcrossRounds * 100) - (this.state.errors * 10);
        this.state.currentStep = this.state.totalSteps;

        clearInterval(this.timerInterval);

        const result: EngineResult = {
            score: Math.max(0, this.state.score),
            accuracy: this.state.accuracy,
            totalTime: this.state.timeElapsed,
            totalSteps: this.state.totalSteps,
            completedSteps: this.state.currentStep,
            errors: this.state.errors,
            details: {
                foundCount: this.totalFoundUniqueAcrossRounds,
                roundsCompleted: this.currentRoundIndex
            }
        };

        this.callbacks.onComplete(result);
    }

    getWords() {
        return this.words;
    }
}
