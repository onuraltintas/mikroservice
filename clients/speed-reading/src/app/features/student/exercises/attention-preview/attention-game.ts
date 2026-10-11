export type AttentionShape = 'star' | 'circle' | 'triangle' | 'square' | 'diamond' | 'cross';
export interface AttentionPreset { label: string; shapeCount: number; rounds: number; responseMs: number; }
type Status = 'intro' | 'ready' | 'playing' | 'feedback' | 'paused' | 'complete';

// Pilot choices for usability testing, not age norms or a validated assessment.
export const ATTENTION_PRESETS: readonly AttentionPreset[] = [
  { label: 'Sakin', shapeCount: 6, rounds: 12, responseMs: 5000 },
  { label: 'Dengeli', shapeCount: 12, rounds: 20, responseMs: 4000 },
  { label: 'Yoğun', shapeCount: 20, rounds: 24, responseMs: 3000 }
];

const DISTRACTORS: readonly AttentionShape[] = ['circle', 'triangle', 'square', 'diamond', 'cross'];
const FEEDBACK_MS = 700;
const TUTORIAL_ROUNDS = 3;

export class AttentionGame {
  status: Status = 'intro';
  tutorial = false;
  shapes: AttentionShape[] = [];
  round = 0;
  hits = 0;
  wrong = 0;
  missed = 0;
  message = '';
  outcome: 'correct' | 'wrong' | 'missed' | null = null;
  private startedAt = 0;
  private deadline = 0;
  private feedbackUntil = 0;
  private pausedAt = 0;
  private previousStatus: Status = 'playing';
  private previousTarget = -1;
  private responses: number[] = [];

  constructor(readonly preset: AttentionPreset, private readonly random = Math.random) {
    if (!Number.isInteger(preset.shapeCount) || preset.shapeCount < 6 || preset.shapeCount > 20
      || !Number.isInteger(preset.rounds) || preset.rounds < 1 || preset.rounds > 100
      || !Number.isInteger(preset.responseMs) || preset.responseMs < 1000 || preset.responseMs > 10000) {
      throw new Error('Dikkat denemesi ayarları geçersiz.');
    }
  }

  get totalRounds(): number { return this.tutorial ? TUTORIAL_ROUNDS : this.preset.rounds; }
  get accuracy(): number {
    const resolved = this.hits + this.wrong + this.missed;
    return resolved ? Math.round(this.hits / resolved * 100) : 0;
  }
  get meanResponseMs(): number | null {
    return this.responses.length ? this.responses.reduce((sum, value) => sum + value, 0) / this.responses.length : null;
  }

  startTutorial(now: number): void { this.start(true, now); }
  startSession(now: number): void { this.start(false, now); }

  private start(tutorial: boolean, now: number): void {
    this.tutorial = tutorial;
    this.round = 1;
    this.hits = this.wrong = this.missed = 0;
    this.responses = [];
    this.previousTarget = -1;
    this.openRound(now);
  }

  private openRound(now: number): void {
    const count = this.tutorial ? 6 : this.preset.shapeCount;
    const positions = Array.from({ length: count }, (_, index) => index).filter(index => index !== this.previousTarget);
    const target = positions[Math.floor(this.random() * positions.length)];
    this.shapes = Array.from({ length: count }, () => DISTRACTORS[Math.floor(this.random() * DISTRACTORS.length)]);
    this.shapes[target] = 'star';
    this.previousTarget = target;
    this.startedAt = now;
    this.deadline = now + this.preset.responseMs;
    this.message = this.tutorial ? 'Yıldızı bulup tıklayın. Bu denemede süre sınırı yok.' : 'Yıldızı bul ve tıkla.';
    this.outcome = null;
    this.status = 'playing';
  }

  choose(index: number, now: number): void {
    if (this.status !== 'playing' || !Number.isInteger(index) || index < 0 || index >= this.shapes.length) return;
    if (!this.tutorial && now >= this.deadline) { this.finishRound('missed', now); return; }
    if (this.shapes[index] !== 'star' && this.tutorial) {
      this.message = 'Bu başka bir şekil. Yıldızı bulup tekrar deneyin.';
      return;
    }
    const correct = this.shapes[index] === 'star';
    if (!this.tutorial && correct) this.responses.push(Math.max(0, now - this.startedAt));
    this.finishRound(correct ? 'correct' : 'wrong', now);
  }

  private finishRound(outcome: 'correct' | 'wrong' | 'missed', now: number): void {
    this.outcome = outcome;
    if (!this.tutorial) {
      if (outcome === 'correct') this.hits++;
      else if (outcome === 'wrong') this.wrong++;
      else this.missed++;
    }
    this.message = outcome === 'correct' ? 'Doğru, hedefi yakaladınız!'
      : outcome === 'wrong' ? 'Bu hedef değildi. Yıldızı arayın.' : 'Süre doldu. Sonraki turda tekrar deneyin.';
    this.status = 'feedback';
    this.feedbackUntil = now + FEEDBACK_MS;
  }

  tick(now: number): void {
    if (this.status === 'playing' && !this.tutorial && now >= this.deadline) this.finishRound('missed', now);
    else if (this.status === 'feedback' && now >= this.feedbackUntil) {
      if (this.round >= this.totalRounds) {
        this.status = this.tutorial ? 'ready' : 'complete';
        this.message = this.tutorial ? 'Hazırsınız! Şimdi gerçek denemeyi başlatabilirsiniz.' : 'Deneme tamamlandı.';
      } else {
        this.round++;
        this.openRound(now);
      }
    }
  }

  remainingMs(now: number): number {
    return Math.max(0, this.deadline - (this.status === 'paused' ? this.pausedAt : now));
  }

  pause(now: number): void {
    if (this.status !== 'playing' && this.status !== 'feedback') return;
    this.previousStatus = this.status;
    this.pausedAt = now;
    this.status = 'paused';
  }

  resume(now: number): void {
    if (this.status !== 'paused') return;
    const duration = Math.max(0, now - this.pausedAt);
    this.startedAt += duration;
    this.deadline += duration;
    this.feedbackUntil += duration;
    this.status = this.previousStatus;
  }
}
