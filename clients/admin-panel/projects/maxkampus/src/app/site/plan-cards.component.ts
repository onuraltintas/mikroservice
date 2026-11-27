import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BillingCatalogPlan, MaxkampusApi, SiteEntry } from '../core/api';
import { SiteStore } from '../core/site-store';
import { trMoney } from '../core/text';
import { SiteIconComponent } from './site-icon.component';

interface PlanCard { plan: BillingCatalogPlan; note: SiteEntry | null; featured: boolean; }
/** One student count on sale for a month and/or a year (27.11.2026). */
interface Tier { students: number; byPeriod: Map<'month' | 'year', PlanCard>; }

/**
 * The plans of Kurs Yönetimi as billing sells them (name, price, period, student limit) with the editors' words from
 * the site (maxkampüs › Site içeriği › Fiyat sayfası). Plans not on public sale are left out.
 * 27.11.2026: when the plans are student counts sold monthly and yearly, the visitor picks the period once and sees one
 * card per student count, with the monthly price per student and what a yearly payment saves. Anything else (contact
 * plans, plans without a student count) is shown as before, one card each.
 */
@Component({
  selector: 'mk-plan-cards',
  standalone: true,
  imports: [RouterLink, SiteIconComponent],
  template: `
    @if (loading()) {
      <p class="mk-muted" role="status">Planlar yükleniyor</p>
    } @else if (!cards().length) {
      <p class="mk-notice">Planlar yakında burada. Kurumunuza uygun planı birlikte belirlemek için <a routerLink="/iletisim">bize yazın</a>.</p>
    } @else {
      @if (tiers().length) {
        <div class="picker">
          @if (hasBothPeriods()) {
            <div class="period" role="group" aria-label="Ödeme dönemi">
              <button type="button" [attr.aria-pressed]="billing() === 'month'" (click)="billing.set('month')">Aylık</button>
              <button type="button" [attr.aria-pressed]="billing() === 'year'" (click)="billing.set('year')">Yıllık
                @if (freeMonths() > 0) { <span class="period__save">{{ freeMonths() }} ay ücretsiz</span> }
              </button>
            </div>
          }
          <ul class="tiers mk-tones">
            @for (tier of tiers(); track tier.students; let i = $index) {
              @if (cardOf(tier); as card) {
                <li class="tier mk-lift" [class.tier--featured]="card.featured" [style.--i]="i" data-testid="plan-tier">
                  @if (card.featured) { <span class="tier__flag">En çok tercih edilen</span> }
                  <p class="tier__count"><strong class="num">{{ tier.students }}</strong> öğrenciye kadar</p>
                  <div class="tier__price" aria-live="polite">
                    <strong class="num">{{ money(card.plan.price, card.plan.currency) }}</strong>
                    <span class="mk-muted">{{ (card.plan.vatRate != null ? '+ KDV, ' : '') + (card.plan.durationDays >= 360 ? 'yıllık' : card.plan.durationDays <= 31 ? 'aylık' : period(card.plan.durationDays)) }}</span>
                    @if (card.plan.vatRate != null) { <span class="mk-muted">KDV dahil {{ money(card.plan.priceWithVat ?? card.plan.price, card.plan.currency) }}</span> }
                  </div>
                  <p class="tier__each">Öğrenci başı aylık <strong class="num">{{ money(perStudentMonthly(card.plan, tier.students), card.plan.currency) }}</strong></p>
                  @if (savingOf(tier) > 0 && billing() === 'year') { <p class="tier__save">Aylık ödemeye göre <strong class="num">{{ money(savingOf(tier), card.plan.currency) }}</strong> az</p> }
                  @if (card.note?.body) { <p class="mk-muted">{{ card.note!.body }}</p> }
                  <a routerLink="/kayit" class="ds-btn ds-btn--block" [class.mk-btn-amber]="card.featured" [class.ds-btn--primary]="!card.featured">Kurumunuzu açın</a>
                </li>
              }
            }
          </ul>
          @if (included().length) {
            <div class="included">
              <p class="included__title">Her planda bütün özellikler var</p>
              <ul class="included__list mk-tones">
                @for (item of included(); track item.id) {
                  <li><mk-site-icon [name]="item.icon" />{{ item.title }}</li>
                }
              </ul>
            </div>
          }
        </div>
      }
      @if (others().length) {
        <ul class="plans">
          @for (card of others(); track card.plan.id) {
            <li class="mk-card plan mk-lift" [class.plan--featured]="card.featured">
              @if (card.featured) { <span class="mk-pill">En çok tercih edilen</span> }
              <h3>{{ card.note?.title || card.plan.name }}</h3>
              <p class="plan__price">
                @if (card.plan.isContactOnly) { <span>Teklif alın</span> }
                @else {
                  <strong class="num">{{ money(card.plan.price, card.plan.currency) }}</strong>
                  <span class="mk-muted">{{ ' ' + (card.plan.vatRate != null ? '+ KDV ' : '') + '/ ' + period(card.plan.durationDays) }}</span>
                }
              </p>
              @if (!card.plan.isContactOnly && card.plan.vatRate != null) {
                <p class="mk-muted plan__vat">KDV dahil {{ money(card.plan.priceWithVat ?? card.plan.price, card.plan.currency) }}</p>
              }
              @if (card.note?.body || card.plan.description) { <p class="mk-muted">{{ card.note?.body || card.plan.description }}</p> }
              @if (highlights(card).length) {
                <ul class="plan__list">@for (line of highlights(card); track line) { <li>{{ line }}</li> }</ul>
              }
              @if (card.plan.isContactOnly) {
                <a routerLink="/iletisim" class="ds-btn ds-btn--secondary ds-btn--block">Bize yazın</a>
              } @else {
                <a routerLink="/kayit" class="ds-btn ds-btn--block" [class.ds-btn--primary]="card.featured" [class.ds-btn--secondary]="!card.featured">Kurumunuzu açın</a>
              }
            </li>
          }
        </ul>
      }
    }
  `,
  styles: `
    :host { display: block; }
    .picker { display: flex; flex-direction: column; gap: 28px; }
    .period { display: inline-flex; align-self: center; gap: 4px; padding: 5px; border: 1px solid var(--ds-line); border-radius: 14px; background: var(--ds-surface); box-shadow: var(--ds-shadow); }
    .period button { display: inline-flex; align-items: center; gap: 8px; min-height: 44px; padding: 0 20px; border: 0; border-radius: 10px; background: transparent;
      color: var(--ds-text); font: inherit; font-weight: 600; cursor: pointer; transition: background-color .2s ease, color .2s ease; }
    .period button[aria-pressed='true'] { background: var(--ds-primary); color: var(--ds-on-primary); }
    .period button:focus-visible { outline: 3px solid var(--ds-focus); outline-offset: 2px; }
    .period__save { padding: 2px 8px; border-radius: 999px; background: var(--ds-accent); color: var(--ds-on-accent); font-size: 12px; font-weight: 700; }
    .period button[aria-pressed='true'] .period__save { background: var(--ds-surface); color: var(--ds-text); }
    .tiers { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin: 0; padding: 0; list-style: none; }
    .tier { position: relative; display: flex; flex-direction: column; gap: 10px; padding: 26px 20px 22px; overflow: hidden;
      border: 1px solid color-mix(in srgb, var(--ds-line) 70%, transparent); border-radius: 18px; background: var(--ds-surface); box-shadow: var(--ds-shadow-raised, var(--ds-shadow)); }
    .tier::before { content: ''; position: absolute; inset: 0 0 auto; height: 6px; background: var(--mk-icon-fg, var(--ds-accent)); }
    .tier--featured { border: 2px solid var(--ds-accent); }
    .tier__flag { align-self: flex-start; padding: 3px 10px; border-radius: 999px; background: var(--ds-accent); color: var(--ds-on-accent); font-size: 12px; font-weight: 700; }
    .tier p { margin: 0; }
    .tier__count { color: var(--ds-muted); font-size: 15px; }
    .tier__count strong { display: block; color: var(--mk-icon-fg, var(--ds-text)); font-size: 40px; line-height: 1.05; letter-spacing: -0.02em; }
    .tier__price { display: flex; flex-direction: column; gap: 2px; }
    .tier__price strong { font-size: 26px; letter-spacing: -0.01em; white-space: nowrap; }
    .tier__price span { font-size: 14px; }
    .tier__each strong { white-space: nowrap; }
    .tier__each { font-size: 15px; }
    .tier__save { padding: 6px 10px; border-radius: 8px; background: var(--ds-success-soft); color: var(--ds-success); font-size: 14px; }
    .tier .ds-btn { margin-top: auto; }
    .included { display: flex; flex-direction: column; gap: 14px; padding: 24px; border-radius: 18px; background: var(--ds-surface); border: 1px solid var(--ds-line); }
    .included__title { margin: 0; font-weight: 700; }
    .included__list { display: flex; flex-wrap: wrap; gap: 10px; margin: 0; padding: 0; list-style: none; }
    .included__list li { display: inline-flex; align-items: center; gap: 8px; padding: 6px 14px 6px 6px; border-radius: 999px; background: var(--mk-icon-bg, var(--ds-soft)); color: var(--ds-text); font-weight: 600; font-size: 15px; }
    .included__list ::ng-deep .icon { width: 32px; height: 32px; border-radius: 999px; background: var(--ds-surface); }
    .plans { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 20px; margin: 28px 0 0; padding: 0; list-style: none; }
    .plan { display: flex; flex-direction: column; gap: 12px; align-items: flex-start; }
    .plan--featured { border: 2px solid var(--ds-accent); box-shadow: var(--ds-shadow-hover, var(--ds-shadow)); }
    .plan h3 { margin: 0; font-size: 20px; }
    .plan__price { margin: 0; font-size: 15px; } .plan__price strong { font-size: 30px; }
    .plan__price strong + span, .plan__price span + span { margin-left: 4px; }
    .plan__vat { margin: 2px 0 0; font-size: 14px; }
    .plan p { margin: 0; }
    .plan__list { margin: 0; padding-left: 20px; flex: 1; }
    .plan .ds-btn { align-self: stretch; margin-top: auto; }
    @media (prefers-reduced-motion: reduce) { .period button { transition: none; } }
  `
})
export class PlanCardsComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  private readonly store = inject(SiteStore);
  readonly notes = input<SiteEntry[]>([]);
  readonly plans = signal<BillingCatalogPlan[]>([]);
  readonly loading = signal(true);
  readonly billing = signal<'month' | 'year'>('year');
  readonly money = trMoney;
  readonly cards = computed<PlanCard[]>(() => this.plans()
    .filter(plan => plan.isActive !== false && plan.isPublic !== false)
    .sort((a, b) => a.sortOrder - b.sortOrder || a.price - b.price)
    .map(plan => {
      const note = this.notes().find(item => item.slug === plan.id) ?? null;
      return { plan, note, featured: note?.category === 'featured' };
    }));

  /** Priced plans with a student count, a month or a year long, grouped by the count (needs at least two counts). */
  readonly tiers = computed<Tier[]>(() => {
    const byStudents = new Map<number, Tier>();
    for (const card of this.cards()) {
      const students = studentsOf(card.plan);
      const kind = periodKind(card.plan.durationDays);
      if (card.plan.isContactOnly || !students || !kind) continue;
      const tier = byStudents.get(students) ?? { students, byPeriod: new Map() };
      if (!tier.byPeriod.has(kind)) tier.byPeriod.set(kind, card);
      byStudents.set(students, tier);
    }
    const tiers = [...byStudents.values()].sort((a, b) => a.students - b.students);
    return tiers.length >= 2 ? tiers : [];
  });
  readonly hasBothPeriods = computed(() => this.tiers().some(tier => tier.byPeriod.size === 2));
  readonly others = computed(() => {
    const shown = new Set(this.tiers().flatMap(tier => [...tier.byPeriod.values()]));
    return this.cards().filter(card => !shown.has(card));
  });
  /** How many months a yearly payment leaves free compared with twelve monthly ones (the best tier, rounded down). */
  readonly freeMonths = computed(() => Math.max(0, ...this.tiers().map(tier => {
    const month = tier.byPeriod.get('month')?.plan, year = tier.byPeriod.get('year')?.plan;
    return month && year && month.price > 0 ? Math.floor((month.price * 12 - year.price) / month.price + 1e-9) : 0;
  })));
  /** The product's features from the home page, as "every plan has" chips. */
  readonly included = computed(() => this.store.home()?.features ?? []);

  async ngOnInit(): Promise<void> {
    void this.store.load();
    try { this.plans.set((await this.api.catalog()).plans ?? []); }
    catch { this.plans.set([]); }
    finally { this.loading.set(false); }
  }

  cardOf(tier: Tier): PlanCard | undefined { return tier.byPeriod.get(this.billing()) ?? tier.byPeriod.values().next().value; }
  perStudentMonthly(plan: BillingCatalogPlan, students: number): number {
    const months = plan.durationDays >= 360 ? 12 : Math.max(1, Math.round(plan.durationDays / 30));
    return Math.round(plan.price / students / months * 100) / 100;
  }
  savingOf(tier: Tier): number {
    const month = tier.byPeriod.get('month')?.plan, year = tier.byPeriod.get('year')?.plan;
    return month && year ? Math.max(0, month.price * 12 - year.price) : 0;
  }

  highlights(card: PlanCard): string[] {
    return card.note?.highlights?.length ? card.note.highlights : card.plan.features ?? [];
  }

  period(days: number): string {
    if (days % 365 === 0) return days === 365 ? 'yıl' : `${days / 365} yıl`;
    if (days % 30 === 0) return days === 30 ? 'ay' : `${days / 30} ay`;
    return `${days} gün`;
  }
}

/** "10 öğrenci (aylık)" or the feature "En çok 10 etkin öğrenci": the student count of a plan. */
function studentsOf(plan: BillingCatalogPlan): number | null {
  if (plan.seatCount) return plan.seatCount;
  for (const text of [plan.name, ...(plan.features ?? [])]) {
    const match = /(\d+)\s*(?:etkin\s+)?öğrenci/i.exec(text ?? '');
    if (match) return Number(match[1]);
  }
  return null;
}

function periodKind(days: number): 'month' | 'year' | null {
  return days >= 28 && days <= 31 ? 'month' : days >= 360 && days <= 366 ? 'year' : null;
}
