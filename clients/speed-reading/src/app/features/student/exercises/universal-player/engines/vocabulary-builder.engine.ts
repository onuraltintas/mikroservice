/**
 * Vocabulary Builder Engine (Kelime Hazinesi)
 * 
 * Kelime öğrenme ve quiz modları ile kelime hazinesi geliştirme engine'i.
 * 
 * Özellikler:
 * 1. Learning Mode - Flashcard tarzı kelime kartları
 * 2. Quiz Mode - Çoktan seçmeli sorular
 * 3. Review Mode - Öğrenilmiş kelimeleri tekrar
 */

import { BaseEngine, EngineConfig, EngineState, EngineResult, EngineCallbacks } from './base-engine.interface';

// ==================== INTERFACES ====================

interface VocabularyWord {
    id: string;
    word: string;
    definition: string;
    exampleSentence?: string;
    synonyms?: string;
    antonyms?: string;
    category: string;
    difficultyLevel: number;
}

interface VocabularyConfig extends EngineConfig {
    words?: VocabularyWord[];
    mode?: 'learning' | 'quiz' | 'review';
    totalWords?: number;
    currentWordIndex?: number;
    quizType?: 'word_to_definition' | 'definition_to_word' | 'mixed';
    timeLimitPerWord?: number; // Seconds
}

interface WordProgress {
    box: number; // 1-5
    lastSeen: number;
    nextDue: number;
    consecutiveCorrect: number;
}

interface QuizOption {
    letter: string;
    text: string;
    isCorrect: boolean;
}

// ==================== ENGINE ====================

export class VocabularyBuilderEngine implements BaseEngine {
    engineType = 'vocabulary_builder' as const;
    displayName = 'Kelime Hazinesi';

    private config!: VocabularyConfig;
    private callbacks!: EngineCallbacks;
    state!: EngineState;

    // Words
    private words: VocabularyWord[] = [];
    private currentWordIndex = 0;
    private mode: 'learning' | 'quiz' | 'review' = 'learning';
    private quizType: 'word_to_definition' | 'definition_to_word' | 'mixed' = 'word_to_definition';
    public timeLimitPerWord: number = 0;
    private wordTimer: any;
    public wordTimeRemaining: number = 0;

    // Responses
    private responses: { wordId: string; word: string; isCorrect: boolean; responseTimeMs: number }[] = [];

    // Quiz state
    private currentQuizOptions: QuizOption[] = [];
    private showingDefinition = false;
    private showingFeedback = false;
    private lastAnswerCorrect = false;
    private correctAnswer = '';
    private currentQuizQuestionType: 'word' | 'definition' = 'word'; // 'word' means question is word, options are definitions

    // Persistence (Local Spaced Repetition)
    private userProgress: Record<string, WordProgress> = {};
    private userId: string = 'guest';

    // Timers
    private startTime = 0;
    private pauseStartTime = 0;
    private timerInterval: any;
    private wordStartTime = 0;

    // ==================== LIFECYCLE ====================

    initialize(config: VocabularyConfig, callbacks: EngineCallbacks): void {
        this.config = config;
        this.callbacks = callbacks;

        // userId mapping (fallback to guest)
        this.userId = config['userId'] || config['UserId'] || 'guest';
        this.loadProgress();

        // Parse words (handle PascalCase from backend)
        const rawWords = config.words || config['Words'] || [];
        this.words = rawWords.map((w: any) => ({
            id: w.id || w.Id,
            word: w.word || w.Word,
            definition: w.definition || w.Definition,
            exampleSentence: w.exampleSentence || w.ExampleSentence,
            synonyms: w.synonyms || w.Synonyms,
            antonyms: w.antonyms || w.Antonyms,
            category: w.category || w.Category,
            difficultyLevel: w.difficultyLevel || w.DifficultyLevel
        }));

        this.mode = config.mode || config['Mode'] || 'learning';
        this.quizType = config.quizType || config['QuizType'] || 'mixed';
        this.currentWordIndex = config.currentWordIndex || config['CurrentWordIndex'] || 0;
        this.timeLimitPerWord = config.timeLimitPerWord || config['TimeLimitPerWord'] || 0;

        // Initialize state
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

    }

    start(): void {
        if (this.words.length === 0) {
            console.error('[VocabularyBuilderEngine] No words to display');
            return;
        }

        this.state.isRunning = true;
        this.state.isPaused = false;
        this.startTime = Date.now();
        this.wordStartTime = Date.now();

        // Start timer
        this.timerInterval = setInterval(() => {
            if (!this.state.isPaused) {
                this.state.timeElapsed = Date.now() - this.startTime;
                this.callbacks.onStateChange({ ...this.state });
            }
        }, 100);

        // Prepare first word
        this.prepareCurrentStep();

        this.callbacks.onStart();
        this.callbacks.onStateChange({ ...this.state });
    }

    private prepareCurrentStep(): void {
        if (this.mode === 'quiz') {
            this.prepareQuizOptions();
            this.startWordTimer();
        }
    }

    private startWordTimer(): void {
        this.clearWordTimer();
        if (this.timeLimitPerWord > 0 && this.mode === 'quiz') {
            this.wordTimeRemaining = this.timeLimitPerWord;
            this.wordTimer = setInterval(() => {
                this.wordTimeRemaining--;
                if (this.wordTimeRemaining <= 0) {
                    this.handleTimeout();
                }
            }, 1000);
        }
    }

    private clearWordTimer(): void {
        if (this.wordTimer) clearInterval(this.wordTimer);
        this.wordTimer = null;
    }

    private handleTimeout(): void {
        this.clearWordTimer();
        if (this.showingFeedback) return;

        const word = this.words[this.currentWordIndex];
        const responseTime = this.timeLimitPerWord * 1000;

        // Mark as wrong in SRS
        this.updateLeitnerBox(word.id, false);

        this.responses.push({
            wordId: word.id,
            word: word.word,
            isCorrect: false,
            responseTimeMs: responseTime
        });

        this.state.errors++;
        this.state.currentStep++;

        // Show feedback (Timeout)
        this.showingFeedback = true;
        this.lastAnswerCorrect = false;

        this.callbacks.onAction({
            action: 'timeout',
            wordId: word.id,
            responseTime: responseTime,
            timestamp: new Date(),
            customData: {
                correctAnswer: this.correctAnswer,
                box: this.userProgress[word.id]?.box,
                isTimeout: true
            }
        });

        this.callbacks.onStateChange({ ...this.state });
    }

    pause(): void {
        if (this.state.isPaused) return;
        this.clearWordTimer();
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
        if (this.mode === 'quiz' && this.timeLimitPerWord > 0 && !this.showingFeedback && this.wordTimeRemaining > 0) {
            this.startWordTimer(); // Simplistic resume, resets interval but uses remaining time
        }
        this.callbacks.onResume();
        this.callbacks.onStateChange({ ...this.state });
    }

    stop(): void {
        this.clearWordTimer();
        if (this.timerInterval) clearInterval(this.timerInterval);
        this.state.isRunning = false;
        this.callbacks.onStateChange({ ...this.state });
    }

    reset(): void {
        this.clearWordTimer();
        if (this.timerInterval) clearInterval(this.timerInterval);
        this.currentWordIndex = 0;
        this.responses = [];
        this.showingDefinition = false;
        this.showingFeedback = false;

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

        this.callbacks.onStateChange({ ...this.state });
    }

    destroy(): void {
        this.clearWordTimer();
        if (this.timerInterval) clearInterval(this.timerInterval);
    }

    handleInput(input: any): void {
        // Handle keyboard shortcuts if needed
    }

    getResult(): EngineResult {
        const totalWords = this.words.length;
        const correctCount = this.responses.filter(r => r.isCorrect).length;
        const accuracy = totalWords > 0 ? (correctCount / totalWords) * 100 : 0;

        return {
            score: Math.round(accuracy),
            accuracy: Math.round(accuracy),
            totalTime: this.state.timeElapsed,
            totalSteps: totalWords,
            completedSteps: this.responses.length,
            errors: this.state.errors,
            details: {
                mode: this.mode,
                quizType: this.quizType,
                totalWords: totalWords,
                correctCount: correctCount,
                incorrectCount: totalWords - correctCount,
                responses: this.responses,
                wordsReviewed: this.words.map(w => ({
                    word: w.word,
                    definition: w.definition,
                    category: w.category,
                    box: this.userProgress[w.id]?.box || 1
                }))
            }
        };
    }

    // ==================== LEARNING MODE ====================

    getCurrentWord(): VocabularyWord | null {
        return this.words[this.currentWordIndex] || null;
    }

    getProgress(): { current: number; total: number } {
        return {
            current: this.currentWordIndex + 1,
            total: this.words.length
        };
    }

    isShowingDefinition(): boolean {
        return this.showingDefinition;
    }

    showDefinition(): void {
        this.showingDefinition = true;
        this.callbacks.onStateChange({ ...this.state });
    }

    markAsKnown(): void {
        const word = this.getCurrentWord();
        if (word) this.updateLeitnerBox(word.id, true);
        this.recordResponse(true);
    }

    markAsUnknown(): void {
        const word = this.getCurrentWord();
        if (word) this.updateLeitnerBox(word.id, false);
        this.recordResponse(false);
    }

    private recordResponse(isCorrect: boolean): void {
        const word = this.words[this.currentWordIndex];
        if (!word) return;

        const responseTime = Date.now() - this.wordStartTime;

        this.responses.push({
            wordId: word.id,
            word: word.word,
            isCorrect: isCorrect,
            responseTimeMs: responseTime
        });

        if (isCorrect) {
            this.state.score++;
        } else {
            this.state.errors++;
        }
        this.state.currentStep++;

        // Send to backend
        this.callbacks.onAction({
            action: isCorrect ? 'mark_known' : 'mark_unknown',
            wordId: word.id,
            responseTime: responseTime,
            timestamp: new Date(),
            customData: { box: this.userProgress[word.id]?.box }
        });

        this.nextWord();
    }

    private nextWord(): void {
        this.currentWordIndex++;
        this.showingDefinition = false;
        this.wordStartTime = Date.now();

        if (this.currentWordIndex >= this.words.length) {
            this.completeExercise();
        } else {
            this.prepareCurrentStep();
            this.callbacks.onStateChange({ ...this.state });
        }
    }

    // ==================== QUIZ MODE ====================

    private prepareQuizOptions(): void {
        const currentWord = this.words[this.currentWordIndex];
        if (!currentWord) return;

        // Determine quiz direction
        if (this.quizType === 'word_to_definition') {
            this.currentQuizQuestionType = 'word';
        } else if (this.quizType === 'definition_to_word') {
            this.currentQuizQuestionType = 'definition';
        } else {
            // Mixed
            this.currentQuizQuestionType = Math.random() > 0.5 ? 'word' : 'definition';
        }

        // Get 3 wrong options from other words
        const otherWords = this.words
            .filter((w, i) => i !== this.currentWordIndex)
            .sort(() => Math.random() - 0.5)
            .slice(0, 3);

        // Create options array
        const options: QuizOption[] = [
            {
                letter: '',
                text: this.currentQuizQuestionType === 'word' ? currentWord.definition : currentWord.word,
                isCorrect: true
            }
        ];

        otherWords.forEach(w => {
            options.push({
                letter: '',
                text: this.currentQuizQuestionType === 'word' ? w.definition : w.word,
                isCorrect: false
            });
        });

        // Ensure we have 4 options (fallback if too few words)
        if (options.length < 4) {
            const placeholders = ['Option A', 'Option B', 'Option C', 'Option D'];
            while (options.length < 4) {
                options.push({ letter: '', text: placeholders[options.length], isCorrect: false });
            }
        }

        // Shuffle and assign letters
        options.sort(() => Math.random() - 0.5);
        const letters = ['A', 'B', 'C', 'D'];
        options.forEach((opt, i) => {
            opt.letter = letters[i];
        });

        this.currentQuizOptions = options;
        this.correctAnswer = options.find(o => o.isCorrect)?.letter || 'A';
    }

    getQuizQuestion(): string {
        const word = this.getCurrentWord();
        if (!word) return '';
        return this.currentQuizQuestionType === 'word' ? word.word : word.definition;
    }

    getQuizOptions(): QuizOption[] {
        return this.currentQuizOptions;
    }

    submitQuizAnswer(letter: string): void {
        if (this.showingFeedback) return;
        this.clearWordTimer();

        const isCorrect = letter === this.correctAnswer;
        const word = this.words[this.currentWordIndex];
        const responseTime = Date.now() - this.wordStartTime;

        // Update Leitner
        this.updateLeitnerBox(word.id, isCorrect);

        this.responses.push({
            wordId: word.id,
            word: word.word,
            isCorrect: isCorrect,
            responseTimeMs: responseTime
        });

        if (isCorrect) {
            this.state.score++;
        } else {
            this.state.errors++;
        }
        this.state.currentStep++;

        // Show feedback
        this.showingFeedback = true;
        this.lastAnswerCorrect = isCorrect;

        // Send to backend
        this.callbacks.onAction({
            action: 'answer_question',
            answer: letter,
            customData: {
                correctAnswer: this.correctAnswer,
                quizType: this.quizType,
                questionType: this.currentQuizQuestionType,
                box: this.userProgress[word.id]?.box
            },
            wordId: word.id,
            responseTime: responseTime,
            timestamp: new Date()
        });

        this.callbacks.onStateChange({ ...this.state });
    }

    isShowingFeedback(): boolean {
        return this.showingFeedback;
    }

    getLastAnswerCorrect(): boolean {
        return this.lastAnswerCorrect;
    }

    getCorrectAnswer(): string {
        return this.correctAnswer;
    }

    nextQuizQuestion(): void {
        this.showingFeedback = false;
        this.nextWord();
    }

    // ==================== SPACED REPETITION (LEITNER) ====================

    private loadProgress(): void {
        try {
            const data = localStorage.getItem(`vocab_progress_${this.userId}`);
            if (data) {
                this.userProgress = JSON.parse(data);
            }
        } catch (e) {
            console.error('[VocabularyBuilderEngine] Error loading progress', e);
        }
    }

    private saveProgress(): void {
        try {
            localStorage.setItem(`vocab_progress_${this.userId}`, JSON.stringify(this.userProgress));
        } catch (e) {
            console.error('[VocabularyBuilderEngine] Error saving progress', e);
        }
    }

    private updateLeitnerBox(wordId: string, isCorrect: boolean): void {
        if (!this.userProgress[wordId]) {
            this.userProgress[wordId] = {
                box: 1,
                lastSeen: Date.now(),
                nextDue: Date.now(),
                consecutiveCorrect: 0
            };
        }

        const progress = this.userProgress[wordId];
        progress.lastSeen = Date.now();

        if (isCorrect) {
            progress.consecutiveCorrect++;
            if (progress.box < 5) {
                progress.box++;
            }
        } else {
            progress.consecutiveCorrect = 0;
            progress.box = 1; // Back to box 1 on error (strict Leitner)
        }

        // Set next due date based on box
        const intervals = [0, 1, 3, 7, 14, 30]; // Days
        const intervalMs = (intervals[progress.box - 1] || 0) * 24 * 60 * 60 * 1000;
        progress.nextDue = Date.now() + intervalMs;

        this.saveProgress();
    }

    // ==================== COMPLETION ====================

    private completeExercise(): void {
        if (this.timerInterval) clearInterval(this.timerInterval);
        this.state.isCompleted = true;

        const result = this.getResult();
        this.state.accuracy = result.accuracy;

        this.callbacks.onComplete(result);
        this.callbacks.onStateChange({ ...this.state });
    }

    // ==================== GETTERS ====================

    getMode(): string {
        return this.mode;
    }

    getCorrectCount(): number {
        return this.responses.filter(r => r.isCorrect).length;
    }

    getIncorrectCount(): number {
        return this.responses.filter(r => !r.isCorrect).length;
    }

    getWordBox(wordId: string): number {
        return this.userProgress[wordId]?.box || 1;
    }
}
