import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { CoachingScoreEstimateService, ESTIMATE_LABEL, EXACT_LABEL, ScoreEstimate, ScoreTypeNotComputed, diplomaRankNote, estimateMethodNote, notComputedText, scoreGroup } from '../../../core/services/coaching-score-estimate.service';
import { CsCardComponent, CsEmptyStateComponent, CsStatusChipComponent } from '../ui';
import { goalGauge, goalScore } from './growth-model';
import { possessive } from '../parent/parent-model';

const DIPLOMA_KEY = 'coaching.diplomaGrade';
const decimal = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 });
const whole = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 0 });

/**
 * T6 (31.10.2026): the target school or programme with a now → target gauge, and the score estimate as cards. Read-only
 * for a parent (`studentId` set); the student can add a diploma grade to the YKS estimate. Paket B (05.11.2026): without a
 * target school or programme the card shows the score of the exam the student works towards (MEB-AGS, YDS, YÖKDİL too), and
 * the YDS / YÖKDİL scores appear as cards.
 */
@Component({
  selector: 'app-student-goal-summary',
  standalone: true,
  imports: [FormsModule, CsCardComponent, CsEmptyStateComponent, CsStatusChipComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'cs-growth-goal' },
  template: `
    @if (error()) { <p class="cs-growth-alert" role="alert">{{ error() }} <button type="button" (click)="load()">Yeniden dene</button></p> }
    @if (loading() && !data()) { <div class="cs-growth-skeleton" role="status" [attr.aria-label]="parent() ? 'Hedef yükleniyor' : 'Hedefin yükleniyor'"></div> }
    @if (data(); as result) {
      @if (gauge(); as goal) {
        <cs-card tone="raised" class="cs-goal-hero" [attr.data-reached]="goal.reached">
          <p class="cs-goal-kicker">{{ parent() ? possessive(name()) + ' hedefi' : 'Hedefin' }} · {{ goal.scoreType }}</p>
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
      } @else if (goalCard(); as card) {
        <cs-card tone="raised" class="cs-goal-hero" data-testid="goal-score">
          <p class="cs-goal-kicker">{{ parent() ? possessive(name()) + ' hedefi' : 'Hedefin' }} · {{ card.group }}</p>
          <h2>{{ card.title }}</h2>
          <p class="cs-goal-headline">{{ card.headline }}</p>
          <p class="cs-growth-muted"><small>{{ card.label }}</small></p>
        </cs-card>
      } @else if (parent()) {
        <cs-empty-state [title]="name() + ' henüz bir hedef seçmedi'" text="Hedef okul ya da bölüm seçildiğinde ona ne kadar kaldığı burada görünür." />
      } @else if (!readonly()) {
        <cs-empty-state title="Bir hedef seç" text="Okulunu ya da bölümünü seçtiğinde ona ne kadar kaldığını burada gösteririz." />
      }
      @if (result.notice) { <p class="cs-growth-muted">{{ result.notice }}</p> }
      @if (result.estimates.length || result.notComputed?.length || result.languageScores?.length) {
        <h2 class="cs-growth-section-title">{{ sectionTitle() }}</h2>
        <div class="cs-estimate-grid">
          @for (item of result.estimates; track item.scoreType) {
            <cs-card data-testid="estimate-card" class="cs-estimate">
              <div class="cs-growth-card-head"><strong>{{ item.name }}</strong><cs-status-chip [label]="'son ' + item.examsUsed + ' deneme'" /></div>
              <p class="cs-estimate-score">{{ num(item.score) }}<small> puan</small></p>
              <p class="cs-growth-muted">{{ num(item.scoreLow) }}–{{ num(item.scoreHigh) }} arası@if (item.margin) { · ±{{ num(item.margin) }} }</p>
              @if (item.rankBest !== null && item.rankWorst !== null) {
                <p>Sıralama <b>{{ int(item.rankBest) }}–{{ int(item.rankWorst) }}</b>@if (item.percentile !== null) { · ilk %{{ num(item.percentile) }} }</p>
              }
              <p class="cs-growth-muted"><small><span data-testid="estimate-approximate">{{ item.label ?? label }}</span> · {{ item.modelYear }} verisiyle{{ item.includesDiplomaGrade ? ', diploma notu dahil' : '' }}</small></p>
            </cs-card>
          }
          @for (item of result.languageScores ?? []; track item.examCode) {
            <cs-card data-testid="language-card" class="cs-estimate">
              <div class="cs-growth-card-head"><strong>{{ item.name }}</strong><cs-status-chip [label]="'son ' + item.examsUsed + ' deneme'" /></div>
              <p class="cs-estimate-score">{{ num(item.score) }}<small> puan</small></p>
              <p class="cs-growth-muted">{{ num(item.scoreLow) }}–{{ num(item.scoreHigh) }} arası@if (item.level) { · Seviye <b>{{ item.level }}</b> }</p>
              <p class="cs-growth-muted"><small>{{ item.label ?? exactLabel }} · her doğru 1,25 puan</small></p>
            </cs-card>
          }
          @for (item of result.notComputed ?? []; track item.scoreType) {
            <cs-card data-testid="estimate-not-computed" class="cs-estimate">
              <div class="cs-growth-card-head"><strong>{{ item.name }}</strong><cs-status-chip [label]="'son ' + item.examsUsed + ' deneme'" /></div>
              <p><b>Puan hesaplanmaz</b></p>
              <p class="cs-growth-muted"><small>{{ notComputed(item) }}</small></p>
            </cs-card>
          }
        </div>
        @if (result.estimates.length) {
          <p class="cs-growth-muted" data-testid="estimate-method"><small>{{ methodNote() }}</small></p>
          @if (diplomaNote(); as note) { <p class="cs-growth-muted"><small>{{ note }}</small></p> }
          <p class="cs-growth-muted"><small>Bu bir tahmin. Gerçek puan ve sıralama her yıl adayların sonuçlarına göre değişir.</small></p>
        }
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
  /** V4: 'parent' talks about the child by name ("Elif'in hedefi") instead of to the student. */
  readonly audience = input<'student' | 'parent'>('student');
  readonly childName = input('');
  readonly parent = computed(() => this.audience() === 'parent');
  readonly name = computed(() => this.childName() || 'Çocuğun');
  readonly possessive = possessive;
  readonly data = signal<ScoreEstimate | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly gauge = computed(() => { const data = this.data(); return data ? goalGauge(data, this.parent() ? this.name() : null) : null; });
  readonly goalCard = computed(() => { const data = this.data(); return data ? goalScore(data, this.parent() ? this.name() : null) : null; });
  readonly sectionTitle = computed(() => {
    const data = this.data();
    const onlyLanguage = !!data && !data.estimates.length && !data.notComputed?.length;
    return onlyLanguage ? (this.parent() ? 'Deneme puanları' : 'Deneme puanların') : (this.parent() ? 'Puan tahmini' : 'Puan tahminin');
  });
  readonly label = ESTIMATE_LABEL;
  readonly exactLabel = EXACT_LABEL;
  readonly methodNote = computed(() => estimateMethodNote(this.data()?.estimates ?? []));
  readonly diplomaNote = computed(() => diplomaRankNote(this.data()?.estimates ?? []));
  readonly hasYks = computed(() => [...this.data()?.estimates ?? [], ...this.data()?.notComputed ?? []].some(x => scoreGroup(x) === 'YKS'));
  readonly notComputed = (item: ScoreTypeNotComputed) => notComputedText(item);
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
