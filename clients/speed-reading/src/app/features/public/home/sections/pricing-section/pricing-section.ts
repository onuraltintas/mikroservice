import { Component, Input, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterModule } from '@angular/router';
import { SubscriptionService, SubscriptionPlan } from '../../../../../core/services/subscription.service';
import { DEFAULT_HOME_PAGE_CONTENT, HomeSectionHeading } from '../../home-page-content';

@Component({
  selector: 'app-pricing-section',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, RouterModule],
  templateUrl: './pricing-section.html',
  styleUrl: './pricing-section.scss'
})
export class PricingSectionComponent implements OnInit {
  private subscriptionService = inject(SubscriptionService);
  @Input() content: HomeSectionHeading = DEFAULT_HOME_PAGE_CONTENT.pricing;
  plans: SubscriptionPlan[] = [];
  loading = true;

  private readonly periodLabel: Record<string, string> = {
    OneTime: 'tek seferlik',
    Monthly: '/ay',
    Quarterly: '/3 ay',
    Annual: '/yıl',
    Lifetime: 'tek seferlik'
  };

  ngOnInit() {
    this.loadPlans();
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
