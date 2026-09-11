import { BaseEngine, EngineCallbacks, EngineConfig, EngineResult, EngineState } from './base-engine.interface';

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
    this.config = config;
    this.callbacks = callbacks;
    this.stage = Number(config['AdaptiveStage'] ?? config['adaptiveStage'] ?? 0);
    this.targetWpm = config['AdaptiveTargetWpm'] ?? config['adaptiveTargetWpm'];
    this.state = this.freshState();
  }

  start(): void {
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
    this.stage = feedback.stage;
    this.targetWpm = feedback.targetWpm;
    this.purpose = feedback.purpose || this.purpose;
    this.state = this.freshState();
    this.callbacks.onStateChange({ ...this.state });
  }

  getStage(): number { return this.stage; }
  getPurpose(): string { return this.purpose; }
  getTargetWpm(): number | undefined { return this.targetWpm; }
  getText(): string {
    const cfg = this.config as any;
    return this.stage === 3
      ? cfg.AdaptiveTransferContent ?? cfg.adaptiveTransferContent ?? ''
      : cfg.Content ?? cfg.content ?? cfg.readingTextContent ?? '';
  }
  getTitle(): string {
    const cfg = this.config as any;
    return this.stage === 3
      ? cfg.AdaptiveTransferTitle ?? cfg.adaptiveTransferTitle ?? ''
      : cfg.ReadingTextTitle ?? cfg.readingTextTitle ?? '';
  }
  getWordCount(): number {
    const configured = this.stage === 3
      ? this.config['AdaptiveTransferWordCount'] ?? this.config['adaptiveTransferWordCount']
      : this.config['WordCount'] ?? this.config['wordCount'];
    return Number(configured) || this.getText().split(/\s+/).filter(Boolean).length;
  }
  getQuestions(): any[] {
    const cfg = this.config as any;
    if (this.stage === 3) return cfg.AdaptiveTransferQuestions ?? cfg.adaptiveTransferQuestions ?? [];
    if (this.stage === 0) return cfg.AdaptivePrimaryQuestions ?? cfg.adaptivePrimaryQuestions ?? cfg.Questions ?? cfg.questions ?? [];
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
