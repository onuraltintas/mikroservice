import { Component, Input, OnInit, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { InstitutionPackages, SubscriptionService, SubscriptionPlan } from '../../../../../core/services/subscription.service';
import { DEFAULT_HOME_PAGE_CONTENT, HomeSectionHeading } from '../../home-page-content';

@Component({
  selector: 'app-pricing-section',
  standalone: true,
  imports: [RouterModule],
  templateUrl: './pricing-section.html',
  styleUrl: './pricing-section.scss'
})
export class PricingSectionComponent implements OnInit {
  private subscriptionService = inject(SubscriptionService);
  @Input() content: HomeSectionHeading = DEFAULT_HOME_PAGE_CONTENT.pricing;
  plans: SubscriptionPlan[] = [];
  /** 23.11.2026: institutions buy packages; students train free. */
  institution: InstitutionPackages | null = null;
  loading = true;

  ngOnInit() {
    this.loadPlans();
  }

  private loadPlans() {
    this.subscriptionService.getInstitutionPackages().subscribe({ next: packages => this.institution = packages, error: () => this.institution = null });
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
    if (plan.isContactOnly) return 'Fiyat için iletişime geçin';
    if (plan.price === 0) return 'Ücretsiz';
    return `₺${plan.price.toLocaleString('tr-TR')}`;
  }

  /** "1 ay", "3 ay", "1 yıl": the time the plan gives access for. */
  getPeriod(plan: SubscriptionPlan): string {
    const days = plan.durationDays;
    if (days % 365 === 0) return days === 365 ? '/ 1 yıl' : `/ ${days / 365} yıl`;
    if (days % 30 === 0) return days === 30 ? '/ 1 ay' : `/ ${days / 30} ay`;
    return `/ ${days} gün`;
  }

  isPopular(plan: SubscriptionPlan): boolean {
    return plan.isBundle;
  }
}
