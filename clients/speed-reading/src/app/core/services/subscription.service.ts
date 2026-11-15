import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

/** A plan as the public pricing section shows it. Plans are kept by the billing service ("Abonelik ve Ödeme"). */
export interface SubscriptionPlan {
  id: string;
  name: string;
  description: string;
  price: number;
  currency: string;
  durationDays: number;
  isContactOnly: boolean;
  features: string[];
  /** The plan also opens another product (a bundle). */
  isBundle: boolean;
}

/** What the student may use, as the Hızlı Okuma service answers (it asks the billing service). */
export interface UserModules {
  modules: string[];
  hasSpeedReading: boolean;
  hasCoaching: boolean;
}

interface BillingCatalog {
  plans: {
    id: string; name: string; description: string; buyerType: 'Individual' | 'Teacher' | 'Institution'; price: number; currency: string;
    durationDays: number; isContactOnly: boolean; features: string[]; productCodes: string[];
  }[];
}

@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private readonly http = inject(HttpClient);

  /** The plans a visitor can buy for themself, from the billing service's open catalog. */
  getPublicPlans(): Observable<SubscriptionPlan[]> {
    return this.http.get<BillingCatalog>(`${environment.apiUrl}/billing/v1/catalog/speed-reading`).pipe(
      map(catalog => (catalog?.plans ?? []).filter(plan => plan.buyerType === 'Individual').map(plan => ({
        id: plan.id, name: plan.name, description: plan.description, price: plan.price, currency: plan.currency,
        durationDays: plan.durationDays, isContactOnly: plan.isContactOnly, features: plan.features ?? [],
        isBundle: (plan.productCodes?.length ?? 0) > 1
      }))));
  }

  getMyModules(): Observable<UserModules> {
    return this.http.get<{ data?: UserModules } & UserModules>(`${environment.speedReadingApiUrl}/subscriptions/my-modules`)
      .pipe(map(result => result?.data ?? result));
  }
}
