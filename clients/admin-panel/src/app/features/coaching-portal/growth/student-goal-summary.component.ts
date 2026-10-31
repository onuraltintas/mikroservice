import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { CoachingScoreEstimateService, ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';
import { CsCardComponent, CsEmptyStateComponent, CsStatusChipComponent } from '../ui';
import { goalGauge } from './growth-model';

const DIPLOMA_KEY = 'coaching.diplomaGrade';
const decimal = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 });
const whole = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 0 });

/**
 * T6 (31.10.2026): the target school or programme with a now → target gauge, and the score estimate as cards. Read-only
 * for a parent (`studentId` set); the student can add a diploma grade to the YKS estimate.
 */
@Component({
  selector: 'app-student-goal-summary',
  standalone: true,
  imports: [FormsModule, CsCardComponent, CsEmptyStateComponent, CsStatusChipComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'cs-growth-goal' },
  template: `
    @if (error()) { <p class="cs-growth-alert" role="alert">{{ error() }} <button type="button" (click)="load()">Yeniden dene</button></p> }
    @if (loading() && !data()) { <div class="cs-growth-skeleton" role="status" aria-label="Hedefin yükleniyor"></div> }
    @if (data(); as result) {
      @if (gauge(); as goal) {
        <cs-card tone="raised" class="cs-goal-hero" [attr.data-reached]="goal.reached">
          <p class="cs-goal-kicker">Hedefin · {{ goal.scoreType }}</p>
          <h2>{{ goal.targetName }}</h2>
          <p class="cs-goal-headline" [class.is-muted]="goal.missingData">{{ goal.headline }}</p>
          @for (row of goal.rows; track row.label) {
            <div class="cs-gauge">
              <div class="cs-gauge-labels"><span>{{ row.label }}</span><span><b>{{ row.current }}</b> şu an</span><span>hedef <b>{{ row.target }}</b></span></div>
              <div class="cs-gauge-track" role="meter" [attr.aria-label]="row.label" aria-valuemin="0" aria-valuemax="100" [attr.aria-valuenow]="row.percent" [attr.aria-valuetext]="row.text">
                <span class="cs-gauge-fill" [style.width.%]="row.percent"></span><span class="cs-gauge-goal" aria-hidden="true"></span>
              </div>
            </div>
          }
        </cs-card>
      } @else if (!readonly()) {
        <cs-empty-state title="Bir hedef seç" text="Okulunu ya da bölümünü seçtiğinde ona ne kadar kaldığını burada gösteririz." />
      }
      @if (result.notice) { <p class="cs-growth-muted">{{ result.notice }}</p> }
      @if (result.estimates.length) {
        <h2 class="cs-growth-section-title">Puan tahminin</h2>
        <div class="cs-estimate-grid">
          @for (item of result.estimates; track item.scoreType) {
            <cs-card data-testid="estimate-card" class="cs-estimate">
              <div class="cs-growth-card-head"><strong>{{ item.name }}</strong><cs-status-chip [label]="'son ' + item.examsUsed + ' deneme'" /></div>
              <p class="cs-estimate-score">{{ num(item.score) }}<small> puan</small></p>
              <p class="cs-growth-muted">{{ num(item.scoreLow) }}–{{ num(item.scoreHigh) }} arası</p>
              <p>Sıralama <b>{{ int(item.rankBest) }}–{{ int(item.rankWorst) }}</b>@if (item.percentile !== null) { · ilk %{{ num(item.percentile) }} }</p>
              <p class="cs-growth-muted"><small>{{ item.modelYear }} verisiyle{{ item.includesDiplomaGrade ? ', diploma notu dahil' : '' }}</small></p>
            </cs-card>
          }
        </div>
        <p class="cs-growth-muted"><small>Bu bir tahmin. Gerçek puan ve sıralama her yıl adayların sonuçlarına göre değişir.</small></p>
      }
      @if (!readonly() && hasYks()) {
        <form class="cs-diploma" (ngSubmit)="saveDiploma()">
          <label for="diploma-grade">Diploma notun <small>(YKS puanına eklenir)</small></label>
          <input id="diploma-grade" name="diploma" type="number" inputmode="decimal" min="50" max="100" step="0.01" [(ngModel)]="diploma">
          <button type="submit" class="cs-growth-secondary">Hesapla</button>
        </form>
      }
    }`
})
export class StudentGoalSummaryComponent implements OnInit {
  private readonly api = inject(CoachingScoreEstimateService);
  private readonly destroyRef = inject(DestroyRef);
  /** null: the signed-in student. */
  readonly studentId = input<string | null>(null);
  readonly readonly = input(false);
  readonly data = signal<ScoreEstimate | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly gauge = computed(() => { const data = this.data(); return data ? goalGauge(data) : null; });
  readonly hasYks = computed(() => this.data()?.estimates.some(x => x.scoreType !== 'LGS') ?? false);
  diploma: number | null = null;

  ngOnInit() {
    try { const saved = Number(localStorage.getItem(this.key)); if (saved >= 50 && saved <= 100) this.diploma = saved; } catch { /* storage unavailable */ }
    this.load();
  }

  load() {
    this.loading.set(true); this.error.set(null);
    this.api.estimate(this.studentId(), this.diploma).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => { this.data.set(value); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Hedef ve puan tahmini şu an yüklenemedi.'); }
    });
  }

  saveDiploma() {
    if (this.diploma != null && (this.diploma < 50 || this.diploma > 100)) { this.error.set('Diploma notu 50 ile 100 arasında olmalı.'); return; }
    try { this.diploma == null ? localStorage.removeItem(this.key) : localStorage.setItem(this.key, String(this.diploma)); } catch { /* storage unavailable */ }
    this.load();
  }

  num(value: number) { return decimal.format(value); }
  int(value: number) { return whole.format(Math.round(value)); }
  private get key() { return `${DIPLOMA_KEY}.${this.studentId() ?? 'me'}`; }
}
