import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterModule } from '@angular/router';
import { PublicCmsService } from '../../../../../core/services/public-cms.service';
import { SubscriptionService, SubscriptionPlan } from '../../../../../core/services/subscription.service';

@Component({
  selector: 'app-pricing-section',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, RouterModule],
  templateUrl: './pricing-section.html',
  styleUrl: './pricing-section.scss'
})
export class PricingSectionComponent implements OnInit {
  private cmsService = inject(PublicCmsService);
  private subscriptionService = inject(SubscriptionService);

  title = 'Size Uygun Paketi Seçin';
  subtitle = 'İster bireysel ister kurumsal, her ihtiyaca uygun esnek planlar';
  plans: SubscriptionPlan[] = [];
  loading = true;

  private readonly periodLabel: Record<string, string> = {
    Monthly: '/ay',
    Quarterly: '/3 ay',
    Annual: '/yıl',
    Lifetime: 'tek seferlik'
  };

  ngOnInit() {
    this.loadTitle();
    this.loadPlans();
  }

  private loadTitle() {
    this.cmsService.getLandingContent().subscribe({
      next: (content) => {
        if (content.blocks['pricing_title']) {
          this.title = content.blocks['pricing_title'];
        }
        if (content.blocks['pricing_subtitle']) {
          this.subtitle = content.blocks['pricing_subtitle'];
        }
      }
    });
  }

  private loadPlans() {
    this.subscriptionService.getPublicPlans().subscribe({
      next: (plans) => {
        this.plans = plans;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  formatPrice(plan: SubscriptionPlan): string {
    if (plan.price === 0) return 'Ücretsiz';
    return `₺${plan.price.toLocaleString('tr-TR')}`;
  }

  getPeriod(plan: SubscriptionPlan): string {
    return this.periodLabel[plan.billingPeriod] ?? '';
  }

  isPopular(plan: SubscriptionPlan): boolean {
    return (plan.includedProductSlugs?.length ?? 0) > 0;
  }
}
