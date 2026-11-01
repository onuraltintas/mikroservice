import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CoachingScoreEstimateService, ScoreEstimate, ScoreTypeEstimate } from '../../../core/services/coaching-score-estimate.service';

const DIPLOMA_KEY = 'coaching.diplomaGrade';

/**
 * Roadmap O5: estimated LGS / YKS score and rank range from the last three practice exams, and the distance to the target
 * school or programme. Shown to the student and (read-only, same data) to a linked parent.
 */
@Component({
  selector: 'app-student-score-estimate', standalone: true, imports: [FormsModule],
  template: `
    <section class="panel" aria-labelledby="score-estimate-heading" data-testid="score-estimate">
      <h2 id="score-estimate-heading">Puan ve sıralama tahmini</h2>
      @if (loading()) { <p role="status">Tahmin hazırlanıyor…</p> }
      @if (error()) { <p role="alert">{{ error() }}</p> }
      @if (data(); as result) {
        @if (result.notice) { <p>{{ result.notice }}</p> }
        @for (item of result.estimates; track item.scoreType) {
          <div class="plan-row">
            <strong>{{ item.name }}: yaklaşık {{ num(item.score) }} puan</strong> ({{ num(item.scoreLow) }}–{{ num(item.scoreHigh) }})<br>
            Tahmini sıralama: {{ int(item.rankBest) }}–{{ int(item.rankWorst) }}
            @if (item.percentile !== null) { · Yüzdelik dilim: %{{ num(item.percentile) }} }
            <br><small>{{ item.modelYear }} verisiyle, son {{ item.examsUsed }} denemeye göre{{ item.includesDiplomaGrade ? ' · diploma notu dahil' : '' }}.</small>
          </div>
        }
        @for (item of result.languageScores ?? []; track item.examCode) {
          <div class="plan-row" data-testid="language-score">
            <strong>{{ item.name }}: yaklaşık {{ num(item.score) }} puan</strong> ({{ num(item.scoreLow) }}–{{ num(item.scoreHigh) }})<br>
            <small>Son {{ item.examsUsed }} denemene göre. Her doğru 1,25 puan; yanlışlar puanı düşürmez.</small>
          </div>
        }
        @if (result.targetExam; as exam) {
          <p class="plan-row" data-testid="score-target-exam">{{ exam.source === 'Student' ? 'Sınavına' : exam.name + ' sınavına' }} {{ int(exam.daysLeft) }} gün kaldı.</p>
        }
        @if (result.target; as target) {
          <p class="plan-row" data-testid="score-target">
            <strong>Hedef: {{ target.targetName }}</strong>
            @if (target.targetScore !== null) { · taban puan {{ num(target.targetScore) }} }
            @if (target.targetRank !== null) { · başarı sırası {{ int(target.targetRank) }} }
            @if (target.targetPercentile !== null) { · yüzdelik %{{ num(target.targetPercentile) }} }
            <br>{{ targetText(target) }}
          </p>
        }
        @if (hasYks(result)) {
          <form class="diploma" (ngSubmit)="saveDiploma()">
            <label for="diploma-grade">Diploma notu (YKS puanına OBP olarak eklenir)</label>
            <input id="diploma-grade" name="diploma" type="number" min="50" max="100" step="0.01" [(ngModel)]="diploma">
            <button type="submit">Hesapla</button>
          </form>
        }
        @if (result.estimates.length) { <p><small>Bu bir tahmindir. Gerçek puan ve sıralama her yıl adayların sonuçlarına göre değişir.</small></p> }
      }
    </section>`
})
export class StudentScoreEstimateComponent implements OnInit {
  /** null: the signed-in student. */
  @Input() studentId: string | null = null;
  private readonly api = inject(CoachingScoreEstimateService);
  readonly data = signal<ScoreEstimate | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  diploma: number | null = null;

  ngOnInit(): void {
    try { const saved = Number(localStorage.getItem(this.key)); if (saved >= 50 && saved <= 100) this.diploma = saved; } catch { /* storage unavailable */ }
    this.load();
  }

  saveDiploma(): void {
    if (this.diploma != null && (this.diploma < 50 || this.diploma > 100)) { this.error.set('Diploma notu 50 ile 100 arasında olmalı.'); return; }
    try { this.diploma == null ? localStorage.removeItem(this.key) : localStorage.setItem(this.key, String(this.diploma)); } catch { /* storage unavailable */ }
    this.load();
  }

  hasYks(result: ScoreEstimate): boolean { return result.estimates.some((x: ScoreTypeEstimate) => x.scoreType !== 'LGS'); }

  targetText(target: NonNullable<ScoreEstimate['target']>): string {
    // K4 (31.10.2026): without a taban or a rank on the target no gap can be shown, whatever the estimate.
    if (target.targetScore === null && target.targetRank === null)
      return 'Bu hedefin taban puanı ya da sıralama verisi yok; bu yüzden hedefe olan fark hesaplanamıyor.';
    if (target.scoreGap === null && target.rankGap === null) return 'Bu hedefin puan türü için henüz tahmin yok; o puan türünde bir deneme gir.';
    if (target.reached) return 'Tahmini puanın hedefin tabanına ulaşıyor. Bu çizgiyi korumaya çalış.';
    if (target.scoreGap !== null) return `Hedefe yaklaşık ${this.num(target.netGap ?? 0)} net (${this.num(target.scoreGap)} puan) kaldı.`;
    return `Hedef sıralamaya yaklaşık ${this.int(target.rankGap ?? 0)} kişi kaldı.`;
  }

  num(value: number): string { return value.toLocaleString('tr-TR', { maximumFractionDigits: 2 }); }
  int(value: number): string { return Math.round(value).toLocaleString('tr-TR'); }

  private get key() { return `${DIPLOMA_KEY}.${this.studentId ?? 'me'}`; }

  private load(): void {
    this.loading.set(true); this.error.set(null);
    this.api.estimate(this.studentId, this.diploma).subscribe({
      next: value => { this.data.set(value); this.loading.set(false); },
      error: () => { this.error.set('Puan tahmini şu an hazırlanamadı.'); this.loading.set(false); }
    });
  }
}
