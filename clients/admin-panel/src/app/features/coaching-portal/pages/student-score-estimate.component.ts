import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GoalCardComponent } from '../growth/goal-card.component';
import { CoachingScoreEstimateService, ESTIMATE_LABEL, EXACT_LABEL, ScoreEstimate, ScoreTypeNotComputed, diplomaRankNote, estimateMethodNote, notComputedText, rawScoresText, scoreGroup, scoreTypeNote, P3_NOTE } from '../../../core/services/coaching-score-estimate.service';

const DIPLOMA_KEY = 'coaching.diplomaGrade';

/**
 * Roadmap O5: estimated LGS / YKS score and rank range from the last three practice exams, and the distance to the target
 * school or programme. Shown to the student and (read-only, same data) to a linked parent and to staff. Paket B (05.11.2026):
 * MEB-AGS and LGS with the model's ± range (no rank: none is published), YDS / YÖKDİL as ÖSYM's own rule with the YDS level,
 * and the score type the student works towards marked. The copy addresses nobody, so staff pages can reuse it.
 */
@Component({
  selector: 'app-student-score-estimate', standalone: true, imports: [FormsModule, GoalCardComponent],
  template: `
    <section class="panel" aria-labelledby="score-estimate-heading" data-testid="score-estimate">
      <h2 id="score-estimate-heading">Puan ve sıralama tahmini</h2>
      @if (loading()) { <p role="status">Tahmin hazırlanıyor…</p> }
      @if (error()) { <p role="alert">{{ error() }}</p> }
      @if (data(); as result) {
        <!-- Paket C (#4): the same goal states and wording as the student's Bugün card (third person for staff). -->
        <app-goal-card [estimate]="result" [childName]="studentId ? 'Öğrenci' : null" [title]="studentId ? 'Öğrencinin hedefi' : 'Hedefin'" [link]="!studentId" />
        @if (result.notice) { <p>{{ result.notice }}</p> }
        @for (item of result.estimates; track item.scoreType) {
          <div class="plan-row">
            <strong>{{ item.name }}: yaklaşık {{ num(item.score) }} puan</strong> ({{ num(item.scoreLow) }}–{{ num(item.scoreHigh) }})@if (item.margin) { · model payı ±{{ num(item.margin) }} }
            <span class="badge" data-testid="estimate-approximate">{{ item.label ?? label }}</span>
            @if (result.goal?.scoreType === item.scoreType) { <span class="badge" data-testid="estimate-goal">Hedef puan türü</span> }
            @if (item.rankBest !== null && item.rankWorst !== null) {
              <br>Tahmini sıralama: {{ int(item.rankBest) }}–{{ int(item.rankWorst) }}
              @if (item.percentile !== null) { · Yüzdelik dilim: %{{ num(item.percentile) }} }
            }
            <br><small>{{ item.modelYear }} verisiyle, son {{ item.examsUsed }} denemeye göre{{ item.includesDiplomaGrade ? ' · diploma notu dahil' : '' }}.</small>
            @if (typeNote(item.scoreType); as note) { <br><small>{{ note }}</small> }
          </div>
        }
        @for (item of result.notComputed ?? []; track item.scoreType) {
          <div class="plan-row" data-testid="estimate-not-computed">
            <strong>{{ item.name }}: puan hesaplanmaz</strong><br>
            {{ notComputed(item) }}<br>
            <small>Son {{ item.examsUsed }} denemenin ortalama ham puanı: {{ rawScores(item) }}.</small>
          </div>
        }
        @for (item of result.languageScores ?? []; track item.examCode) {
          <div class="plan-row" data-testid="language-score">
            <strong>{{ item.name }}: {{ num(item.score) }} puan</strong> ({{ num(item.scoreLow) }}–{{ num(item.scoreHigh) }})@if (item.level) { · Seviye {{ item.level }} }
            <span class="badge">{{ item.label ?? exactLabel }}</span>
            @if (result.goal?.scoreType === item.examCode) { <span class="badge" data-testid="estimate-goal">Hedef sınav</span> }<br>
            <small>Son {{ item.examsUsed }} denemeye göre. Her doğru 1,25 puan; yanlışlar puanı düşürmez (ÖSYM'nin hesabı).</small>
          </div>
        }
        @if (result.goal?.scoreType === 'P1') { <p class="plan-row"><small>{{ p3Note }}</small></p> }
        @if (result.targetExam; as exam) {
          <p class="plan-row" data-testid="score-target-exam">{{ exam.source === 'Student' ? 'Sınavına' : exam.name + ' sınavına' }} {{ exam.source === 'Estimated' ? 'tahminen ' : '' }}{{ int(exam.daysLeft) }} gün kaldı.@if (exam.source === 'Estimated') { <small> Sınav tarihi henüz açıklanmadı; tahmini bir tarihe göre hesaplandı.</small> }</p>
        }
        @if (result.target; as target) {
          <p class="plan-row" data-testid="score-target">
            <strong>Hedef: {{ target.targetName }}</strong>
            @if (target.targetScore !== null) { · {{ target.kind === 'score' || target.ownTargetScore ? 'hedef puanı' : 'taban puan' }} {{ num(target.targetScore) }} }
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
        @if (result.estimates.length) {
          <p data-testid="estimate-method"><small>{{ method(result) }}</small></p>
          @if (diplomaNote(result); as note) { <p><small>{{ note }}</small></p> }
          <p><small>Bu bir tahmindir. Gerçek puan ve sıralama her yıl adayların sonuçlarına göre değişir.</small></p>
        }
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

  readonly label = ESTIMATE_LABEL;
  readonly exactLabel = EXACT_LABEL;
  readonly typeNote = scoreTypeNote;
  readonly p3Note = P3_NOTE;
  method(result: ScoreEstimate): string | null { return estimateMethodNote(result.estimates); }
  diplomaNote(result: ScoreEstimate): string | null { return diplomaRankNote(result.estimates); }

  hasYks(result: ScoreEstimate): boolean { return [...result.estimates, ...result.notComputed ?? []].some(x => scoreGroup(x) === 'YKS'); }
  notComputed(item: ScoreTypeNotComputed): string { return notComputedText(item); }
  rawScores(item: ScoreTypeNotComputed): string { return rawScoresText(item); }

  targetText(target: NonNullable<ScoreEstimate['target']>): string {
    // K4 (31.10.2026): without a taban or a rank on the target no gap can be shown, whatever the estimate.
    if (target.targetScore === null && target.targetRank === null)
      return 'Bu hedefin taban puanı ya da sıralama verisi yok; bu yüzden hedefe olan fark hesaplanamıyor.';
    // 01.11.2026: ÖSYM does not compute the target's score type for these ham puanlar (2026 YKS Kılavuzu, Tablo 1D).
    if (target.notComputedReason) return `Bu hedefin puan türünde (${target.scoreType}) puan hesaplanmadığı için hedefe olan fark gösterilemiyor.`;
    if (target.scoreGap === null && target.rankGap === null)
      // Re-check 06.11.2026: an estimate without a rank (LGS, MEB-AGS) cannot be compared with a rank-only target.
      return this.data()?.estimates.some(x => x.scoreType === target.scoreType)
        ? 'Hedefe olan fark hesaplanamıyor: bu hedefin taban puanı yok, tahminde de sıralama yok.'
        : 'Bu hedefin puan türü için henüz tahmin yok; o puan türünde bir deneme gir.';
    // Review 06.11.2026: an approximate (LGS / MEB-AGS) taban inside the estimate's range is borderline.
    if (target.status === 'borderline') {
      const now = this.data()?.estimates.find(x => x.scoreType === target.scoreType);
      return now ? `Hedef taban puanı tahmini aralığın içinde (${this.num(now.scoreLow)}–${this.num(now.scoreHigh)}): sınırda.` : 'Hedef taban puanı tahmini aralığın içinde: sınırda.';
    }
    if (target.reached) return target.kind === 'score' ? 'Tahmini puan hedef puana ulaşıyor.' : 'Tahmini puanın hedefin tabanına ulaşıyor. Bu çizgiyi korumaya çalış.';
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
