import { BaseEngine, EngineCallbacks, EngineConfig, EngineResult, EngineState } from './base-engine.interface';
import { boundedInteger, boundedText, caseInsensitiveField, recordOrEmpty } from './reading-pacer-safety';

export interface AdaptiveFluencyStageFeedback {
  stage: number;
  targetWpm?: number;
  purpose?: string;
  baselineComprehension?: number;
  transferComprehension?: number;
  transferGainPercent?: number;
  completed?: boolean;
}

export class AdaptiveFluencyEngine implements BaseEngine {
  readonly engineType = 'adaptive_fluency';
  readonly displayName = 'Anlam Korumalı Hızlanma';
  state!: EngineState;

  private callbacks!: EngineCallbacks;
  private config!: EngineConfig;
  private timer?: ReturnType<typeof setInterval>;
  private startedAt = 0;
  private pausedAt = 0;
  private stage = 0;
  private targetWpm?: number;
  private purpose = 'Başlangıç düzeyinizi ölçün.';

  initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
    const root = recordOrEmpty(config);
    const nested = recordOrEmpty(caseInsensitiveField(root, 'engineConfig'));
    const sessionData = recordOrEmpty(caseInsensitiveField(root, 'sessionData'));
    this.config = { ...root, ...nested, ...sessionData };
    this.callbacks = callbacks;
    this.stage = boundedInteger(caseInsensitiveField(this.config, 'adaptiveStage'), 0, 0, 3);
    const configuredTarget = caseInsensitiveField(this.config, 'adaptiveTargetWpm');
    this.targetWpm = typeof configuredTarget === 'number' && Number.isFinite(configuredTarget)
      ? boundedInteger(configuredTarget, 200, 20, 1500)
      : undefined;
    this.state = this.freshState();
  }

  start(): void {
    if (this.state.isRunning || this.state.isCompleted) return;
    this.startedAt = Date.now();
    this.state = { ...this.freshState(), isRunning: true, targetWPM: this.targetWpm };
    this.timer = setInterval(() => {
      if (!this.state.isPaused) {
        this.state.timeElapsed = Date.now() - this.startedAt;
        this.callbacks.onStateChange({ ...this.state });
      }
    }, 250);
    this.callbacks.onStart();
    this.callbacks.onStateChange({ ...this.state });
  }

  pause(): void {
    if (this.state.isPaused) return;
    this.pausedAt = Date.now();
    this.state.isPaused = true;
    this.callbacks.onPause();
    this.callbacks.onStateChange({ ...this.state });
  }

  resume(): void {
    if (!this.state.isPaused) return;
    this.startedAt += Date.now() - this.pausedAt;
    this.state.isPaused = false;
    this.callbacks.onResume();
    this.callbacks.onStateChange({ ...this.state });
  }

  stop(): void { this.clearTimer(); this.state.isRunning = false; }
  reset(): void { this.clearTimer(); this.state = this.freshState(); }
  destroy(): void { this.clearTimer(); }

  handleInput(input: any): void {
    if (input?.action === 'complete_reading') this.completeReading();
  }

  completeReading(): void {
    if (this.state.isCompleted) return;
    this.clearTimer();
    this.state.isRunning = false;
    this.state.isCompleted = true;
    this.state.currentStep = this.stage + 1;
    this.callbacks.onStateChange({ ...this.state });
    const minutes = this.state.timeElapsed / 60_000;
    const result: EngineResult = {
      score: 0,
      accuracy: 0,
      totalTime: this.state.timeElapsed,
      totalSteps: 4,
      completedSteps: this.stage + 1,
      errors: 0,
      details: { wpm: minutes > 0 ? Math.round(this.getWordCount() / minutes) : 0, stage: this.stage }
    };
    this.callbacks.onComplete(result);
  }

  applyStage(feedback: AdaptiveFluencyStageFeedback): void {
    this.clearTimer();
    this.stage = boundedInteger(feedback.stage, 0, 0, 3);
    this.targetWpm = feedback.targetWpm === undefined
      ? undefined
      : boundedInteger(feedback.targetWpm, 200, 20, 1500);
    this.purpose = feedback.purpose || this.purpose;
    this.state = this.freshState();
    this.callbacks.onStateChange({ ...this.state });
  }

  getStage(): number { return this.stage; }
  getPurpose(): string { return this.purpose; }
  getTargetWpm(): number | undefined { return this.targetWpm; }
  getText(): string {
    return boundedText(this.stage === 3
      ? caseInsensitiveField(this.config, 'adaptiveTransferContent')
      : caseInsensitiveField(this.config, 'content') ?? caseInsensitiveField(this.config, 'readingTextContent'), '');
  }
  getTitle(): string {
    return boundedText(this.stage === 3
      ? caseInsensitiveField(this.config, 'adaptiveTransferTitle')
      : caseInsensitiveField(this.config, 'readingTextTitle'), '', 1_000);
  }
  getWordCount(): number {
    const configured = caseInsensitiveField(this.config,
      this.stage === 3 ? 'adaptiveTransferWordCount' : 'wordCount');
    return typeof configured === 'number' && Number.isFinite(configured)
      ? boundedInteger(configured, 0, 0, 100_000)
      : this.getText().split(/\s+/).filter(Boolean).length;
  }
  getQuestions(): any[] {
    if (this.stage === 3) {
      const questions = caseInsensitiveField(this.config, 'adaptiveTransferQuestions');
      return Array.isArray(questions) ? questions.slice(0, 100) : [];
    }
    if (this.stage === 0) {
      const questions = caseInsensitiveField(this.config, 'adaptivePrimaryQuestions')
        ?? caseInsensitiveField(this.config, 'questions');
      return Array.isArray(questions) ? questions.slice(0, 100) : [];
    }
    return [];
  }
  getFontSize(): string { return 'medium'; }

  private freshState(): EngineState {
    return {
      isRunning: false, isPaused: false, isCompleted: false,
      currentStep: this.stage, totalSteps: 4, score: 0, accuracy: 0,
      timeElapsed: 0, errors: 0, targetWPM: this.targetWpm
    };
  }
  private clearTimer(): void { if (this.timer) clearInterval(this.timer); this.timer = undefined; }
}
