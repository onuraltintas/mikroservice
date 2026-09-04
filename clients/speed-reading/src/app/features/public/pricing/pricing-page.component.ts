import { Component, OnInit, inject } from '@angular/core';
import { FooterComponent } from '../../../shared/components/footer/footer';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { SeoService } from '../../../core/services/seo.service';
import { PricingSectionComponent } from '../home/sections/pricing-section/pricing-section';

@Component({
  selector: 'app-pricing-page',
  standalone: true,
  imports: [NavbarComponent, PricingSectionComponent, FooterComponent],
  template: `
    <app-navbar [forceOpaque]="true"></app-navbar>
    <main class="pricing-page-content">
      <app-pricing-section></app-pricing-section>
    </main>
    <app-footer></app-footer>
  `
})
export class PricingPageComponent implements OnInit {
  private readonly seoService = inject(SeoService);

  ngOnInit(): void {
    this.seoService.updateTags({
      title: 'Fiyatlandırma | Hızlı Okuma',
      description: 'Hızlı okuma hedeflerinize uygun abonelik planını seçin.',
      url: `${window.location.origin}/fiyatlandirma`,
      type: 'website'
    });
  }
}
