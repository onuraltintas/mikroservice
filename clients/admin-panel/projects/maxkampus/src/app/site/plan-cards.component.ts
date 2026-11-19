import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BillingCatalogPlan, MaxkampusApi, SiteEntry } from '../core/api';
import { trMoney } from '../core/text';

interface PlanCard { plan: BillingCatalogPlan; note: SiteEntry | null; featured: boolean; }

/**
 * The plans of Kurs Yönetimi as billing sells them (name, price, period, student limit) with the editors' words from
 * the site (maxkampüs › Site içeriği › Fiyat sayfası). Plans not on public sale are left out.
 */
@Component({
  selector: 'mk-plan-cards',
  standalone: true,
  imports: [RouterLink],
  template: `
    @if (loading()) {
      <p class="mk-muted" role="status">Planlar yükleniyor</p>
    } @else if (!cards().length) {
      <p class="mk-notice">Planlar yakında burada. Kurumunuza uygun planı birlikte belirlemek için <a routerLink="/iletisim">bize yazın</a>.</p>
    } @else {
      <ul class="plans">
        @for (card of cards(); track card.plan.id) {
          <li class="mk-card plan" [class.plan--featured]="card.featured">
            @if (card.featured) { <span class="mk-pill">En çok tercih edilen</span> }
            <h3>{{ card.note?.title || card.plan.name }}</h3>
            <p class="plan__price">
              @if (card.plan.isContactOnly) { <span>Teklif alın</span> }
              @else { <strong class="num">{{ money(card.plan.price, card.plan.currency) }}</strong> <span class="mk-muted">/ {{ period(card.plan.durationDays) }}</span> }
            </p>
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
  `,
  styles: `
    .plans { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 20px; margin: 0; padding: 0; list-style: none; }
    .plan { display: flex; flex-direction: column; gap: 12px; align-items: flex-start; }
    .plan--featured { border: 2px solid var(--ds-accent); }
    .plan h3 { margin: 0; font-size: 20px; }
    .plan__price { margin: 0; font-size: 15px; } .plan__price strong { font-size: 30px; }
    .plan p { margin: 0; }
    .plan__list { margin: 0; padding-left: 20px; flex: 1; }
    .plan .ds-btn { align-self: stretch; margin-top: auto; }
  `
})
export class PlanCardsComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  readonly notes = input<SiteEntry[]>([]);
  readonly plans = signal<BillingCatalogPlan[]>([]);
  readonly loading = signal(true);
  readonly money = trMoney;
  readonly cards = computed<PlanCard[]>(() => this.plans()
    .filter(plan => plan.isActive !== false && plan.isPublic !== false)
    .sort((a, b) => a.sortOrder - b.sortOrder || a.price - b.price)
    .map(plan => {
      const note = this.notes().find(item => item.slug === plan.id) ?? null;
      return { plan, note, featured: note?.category === 'featured' };
    }));

  async ngOnInit(): Promise<void> {
    try { this.plans.set((await this.api.catalog()).plans ?? []); }
    catch { this.plans.set([]); }
    finally { this.loading.set(false); }
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
