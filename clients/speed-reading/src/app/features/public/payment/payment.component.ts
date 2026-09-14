import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SubscriptionPlan, SubscriptionService } from '../../../core/services/subscription.service';
import { FooterComponent } from '../../../shared/components/footer/footer';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';

@Component({
  selector: 'app-payment',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    NavbarComponent,
    FooterComponent
  ],
  templateUrl: './payment.component.html',
  styleUrl: './payment.component.scss'
})
export class PaymentComponent implements OnInit {
  private readonly subscriptions = inject(SubscriptionService);

  readonly loading = signal(true);
  plans: SubscriptionPlan[] = [];

  ngOnInit(): void {
    this.subscriptions.getPublicPlans().subscribe({
      next: plans => {
        this.plans = plans;
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  formatPrice(plan: SubscriptionPlan): string {
    return `${plan.price.toLocaleString('tr-TR')} TL`;
  }
}
