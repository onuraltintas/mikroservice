import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CoachingDatePipe } from '../coaching-labels';
import { CsSegmentedTabsComponent, CsTabOption } from '../ui';
import { CoachingParentService, ParentLink } from './coaching-parent.service';
import { ParentCoachingTabComponent } from './parent-coaching-tab.component';
import { parentRelationshipLabel } from './parent-home.component';
import { PRODUCT_LABELS, ParentProduct, firstName, hiddenProductsText, productFromSlug, productSlug, visibleProducts } from './parent-model';
import { ParentSpeedReadingTabComponent } from './parent-speed-reading-tab.component';

/**
 * V4 (01.11.2026): one linked child. A tab per product the child uses and shares (`?urun=kocluk|hizli-okuma`); with one
 * product there is no tab strip, with none the page says why.
 */
@Component({
  selector: 'app-parent-child',
  standalone: true,
  imports: [RouterLink, CoachingDatePipe, CsSegmentedTabsComponent, ParentCoachingTabComponent, ParentSpeedReadingTabComponent],
  template: `
    <div class="cp-page" aria-labelledby="parent-child-title">
      <div class="cp-toolbar no-print">
        <a routerLink="/coaching-portal/parent" class="cp-back">← Çocuklarım</a>
        @if (link()) { <button type="button" data-testid="print-child" class="cp-secondary" (click)="print()">Yazdır / PDF</button> }
      </div>
      @if (error()) { <p class="cp-alert" role="alert">{{ error() }}</p> }
      @if (notLinked()) {
        <p class="cp-notice" role="alert">Bu öğrenciyle doğrulanmış bir veli bağlantın yok. Çocuğunun bağlantı isteğini onaylamasını bekle.</p>
      } @else if (link(); as child) {
        <header class="cp-header">
          <h1 id="parent-child-title">{{ child.studentName }}</h1>
          <p>{{ relationship(child.relationship) }}@if (child.verifiedAt) { · {{ child.verifiedAt | coachingDate }} tarihinden beri bağlı }</p>
        </header>
        @if (!products().length) {
          <p class="cp-notice" data-testid="no-products">{{ hiddenText() }}</p>
        } @else {
          @if (products().length > 1) {
            <cs-segmented-tabs class="cp-tabs" label="Ürün" [options]="tabs()" [value]="tabSlug()" (valueChange)="select($event)" />
          }
          <div [attr.role]="products().length > 1 ? 'tabpanel' : null" [attr.aria-label]="label(active())" class="cp-panel">
            @if (active() === 'Coaching') { <app-parent-coaching-tab [studentId]="studentId" [childName]="child.studentName" /> }
            @else { <app-parent-speed-reading-tab [studentId]="studentId" [childName]="child.studentName" /> }
          </div>
        }
      }
    </div>`
})
export class ParentChildComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly studentId = this.route.snapshot.paramMap.get('studentId') ?? '';
  readonly link = signal<ParentLink | null>(null);
  readonly notLinked = signal(false);
  readonly error = signal<string | null>(null);
  readonly products = computed<ParentProduct[]>(() => { const link = this.link(); return link ? visibleProducts(link) : []; });
  readonly tabs = computed<CsTabOption[]>(() => this.products().map(product => ({ value: productSlug(product), label: PRODUCT_LABELS[product] })));
  private readonly requested = signal<ParentProduct | null>(productFromSlug(this.route.snapshot.queryParamMap?.get('urun')));
  /** The asked-for product when the child shares it, otherwise the first one. */
  readonly active = computed<ParentProduct | null>(() => { const products = this.products(); const asked = this.requested();
    return asked && products.includes(asked) ? asked : products[0] ?? null; });
  readonly tabSlug = computed(() => { const active = this.active(); return active ? productSlug(active) : ''; });
  readonly hiddenText = computed(() => { const link = this.link(); return link ? hiddenProductsText(link) : ''; });

  ngOnInit(): void {
    this.service.getLinks().subscribe({
      next: links => {
        const link = links.find(item => item.studentUserId === this.studentId && item.status === 'Verified') ?? null;
        this.link.set(link); this.notLinked.set(!link);
      },
      error: () => this.error.set('Veli bağlantıların yüklenemedi. Sayfayı yenileyip yeniden dene.')
    });
  }

  select(slug: string) {
    const product = productFromSlug(slug);
    if (!product || product === this.active()) return;
    this.requested.set(product);
    void this.router.navigate([], { relativeTo: this.route, queryParams: { urun: slug }, replaceUrl: true });
  }

  label(product: ParentProduct | null) { return product ? `${firstName(this.link()?.studentName)} · ${PRODUCT_LABELS[product]}` : ''; }
  relationship(value: string) { return parentRelationshipLabel(value); }
  /** Roadmap O13: the browser's print dialog, where the page can also be saved as PDF. */
  print(): void { window.print(); }
}
