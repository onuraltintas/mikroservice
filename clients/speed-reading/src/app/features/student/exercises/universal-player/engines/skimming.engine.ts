import { BaseEngine, EngineCallbacks, EngineConfig, EngineResult, EngineState } from './base-engine.interface';
import { boundedInteger, boundedText, caseInsensitiveField, mergeCaseInsensitiveRecords, recordOrEmpty } from './reading-pacer-safety';

/** Timed inspection of a text's overall meaning; questions are handled by the player. */
export class SkimmingEngine implements BaseEngine {
  readonly engineType = 'skimming';
  readonly displayName = 'Göz Gezdirme';
  state: EngineState = this.initialState();
  private callbacks!: EngineCallbacks;
  private text = '';
  private title = '';
  private minimumMs = 3000;
  private maximumMs = 90_000;
  private fontSize = 20;
  private startedAt = 0;
  private pausedAt = 0;
  private timer?: ReturnType<typeof setInterval>;

  initialize(config: EngineConfig, callbacks: EngineCallbacks): void {
    clearInterval(this.timer);
    this.callbacks = callbacks;
    const nested = recordOrEmpty(caseInsensitiveField(config, 'engineConfig'));
    const timing = mergeCaseInsensitiveRecords(config, nested, 'timing');
    const visuals = mergeCaseInsensitiveRecords(config, nested, 'visuals');
    const owned = config['serverAuthoritative'] === true;
    const content = caseInsensitiveField(config, 'content');
    const previewContent = caseInsensitiveField(nested, 'content') ?? content;
    const ownedText = caseInsensitiveField(config, 'readingTextContent') ?? (typeof content === 'string' ? content
      : caseInsensitiveField(recordOrEmpty(content), 'text'));
    this.text = boundedText(owned ? ownedText : ownedText ?? (typeof previewContent === 'string' ? previewContent
      : caseInsensitiveField(recordOrEmpty(previewContent), 'text')), '').trim();
    this.title = boundedText(caseInsensitiveField(config, 'readingTextTitle'), '');
    this.minimumMs = boundedInteger(owned ? config['readingMinimumMs'] : timing['minreadingtimems'], 3000, 0, 3_600_000);
    const seconds = caseInsensitiveField(nested, 'timeLimitSeconds') ?? caseInsensitiveField(nested, 'timeLimit')
      ?? caseInsensitiveField(config, 'timeLimitSeconds') ?? caseInsensitiveField(config, 'timeLimit') ?? timing['timelimitsec'];
    this.maximumMs = boundedInteger(owned ? config['readingMaximumMs'] : timing['maxreadingtimems'],
      boundedInteger(seconds, 90, 1, 3600) * 1000, 1000, 3_600_000);
    this.fontSize = boundedInteger(Number.parseInt(String(visuals['fontsize'] ?? '20'), 10), 20, 14, 48);
    this.state = this.initialState();
  }

  start(): void {
    if (this.state.isRunning || this.state.isCompleted) return;
    if (!this.text || this.minimumMs >= this.maximumMs) {
      this.callbacks.onError('Göz Gezdirme metni veya inceleme süresi hazır değil.');
      return;
    }
    this.startedAt = Date.now();
    this.state.isRunning = true;
    this.timer = setInterval(() => {
      if (this.state.isPaused) return;
      this.updateTime();
      if (this.state.timeElapsed >= this.maximumMs) this.completeReading(true);
      else this.callbacks.onStateChange({ ...this.state });
    }, 100);
    this.callbacks.onStart();
    this.callbacks.onStateChange({ ...this.state });
  }

  pause(): void {
    if (!this.state.isRunning || this.state.isPaused) return;
    this.updateTime(); this.pausedAt = Date.now(); this.state.isPaused = true;
    this.callbacks.onPause(); this.callbacks.onStateChange({ ...this.state });
  }
  resume(): void {
    if (!this.state.isRunning || !this.state.isPaused) return;
    this.startedAt += Date.now() - this.pausedAt; this.state.isPaused = false;
    this.callbacks.onResume(); this.callbacks.onStateChange({ ...this.state });
  }
  stop(): void {
    clearInterval(this.timer); this.state.isRunning = false; this.state.isPaused = false;
    this.callbacks?.onStateChange({ ...this.state });
  }
  reset(): void { this.stop(); this.state = this.initialState(); this.callbacks.onStateChange({ ...this.state }); }
  destroy(): void { this.stop(); }
  handleInput(input: { action?: string }): void { if (input.action === 'complete_reading') this.completeReading(); }

  completeReading(timedOut = false): void {
    if (!this.state.isRunning || this.state.isPaused || this.state.isCompleted) return;
    this.updateTime();
    if (!timedOut && this.state.timeElapsed < this.minimumMs) return;
    timedOut ||= this.state.timeElapsed >= this.maximumMs;
    clearInterval(this.timer);
    this.state.isCompleted = true; this.state.isRunning = false; this.state.currentStep = timedOut ? 0 : 1;
    const result: EngineResult = { score: 0, accuracy: 0, totalTime: this.state.timeElapsed, totalSteps: 1,
      completedSteps: this.state.currentStep, errors: 0,
      details: { wpm: null, inspectionTimeMs: this.state.timeElapsed, timedOut, incomplete: timedOut } };
    this.callbacks.onStateChange({ ...this.state }); this.callbacks.onComplete(result);
  }
  getText(): string { return this.text; }
  getTitle(): string { return this.title; }
  getWordCount(): number { return this.text ? this.text.split(/\s+/).length : 0; }
  getFontSize(): string { return 'medium'; }
  getFontSizePx(): number { return this.fontSize; }
  getLineHeight(): number { return 1.8; }
  getMaximumMs(): number { return this.maximumMs; }
  canComplete(): boolean { return this.state.isRunning && !this.state.isPaused && this.state.timeElapsed >= this.minimumMs; }
  private updateTime(): void { this.state.timeElapsed = Math.min(this.maximumMs, Math.max(0, Date.now() - this.startedAt)); }
  private initialState(): EngineState {
    return { isRunning: false, isPaused: false, isCompleted: false, currentStep: 0, totalSteps: 1,
      score: 0, accuracy: 0, timeElapsed: 0, errors: 0 };
  }
}
