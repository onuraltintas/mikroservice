import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTabsModule } from '@angular/material/tabs';
import { BaseComponent } from '../../../core/components/base.component';
import { ProductsListComponent } from './products-list.component';
import { SubscriptionPlansListComponent } from './subscription-plans-list.component';
import { UserSubscriptionsListComponent } from './user-subscriptions-list.component';
import { PaymentHistoryComponent } from './payment-history.component';

@Component({
  selector: 'app-subscriptions-page',
  standalone: true,
  imports: [
    CommonModule,
    MatTabsModule,
    ProductsListComponent,
    SubscriptionPlansListComponent,
    UserSubscriptionsListComponent,
    PaymentHistoryComponent
  ],
  templateUrl: './subscriptions-page.component.html',
  styleUrls: ['./subscriptions-page.component.scss']
})
export class SubscriptionsPageComponent extends BaseComponent {
}
