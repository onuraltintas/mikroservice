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

/** The institution packages as one card on the public page (23.11.2026). */
export interface InstitutionPackages { minSeats: number; maxSeats: number; lowestPrice: number; periods: string; features: string[]; }

/** What the student may use, as the Hızlı Okuma service answers (it asks the billing service). */
export interface UserModules {
  modules: string[];
  hasSpeedReading: boolean;
  hasCoaching: boolean;
}

interface BillingCatalog {
  plans: {
    id: string; name: string; description: string; buyerType: 'Individual' | 'Teacher' | 'Institution'; price: number; currency: string;
    durationDays: number; isContactOnly: boolean; features: string[]; productCodes: string[]; seatCount?: number | null;
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

  /**
   * 23.11.2026: institutions buy packages that differ only in their students and period; students train free. The public
   * page shows them as one card: the student range, the periods and the lowest price. Null when there are fewer than two.
   */
  getInstitutionPackages(): Observable<InstitutionPackages | null> {
    return this.http.get<BillingCatalog>(`${environment.apiUrl}/billing/v1/catalog/speed-reading`).pipe(map(catalog => {
      const tiers = (catalog?.plans ?? []).filter(plan => plan.buyerType === 'Institution' && !plan.isContactOnly && !!plan.seatCount);
      if (tiers.length < 2) return null;
      const seats = tiers.map(plan => plan.seatCount!);
      const days = [...new Set(tiers.map(plan => plan.durationDays))].sort((a, b) => a - b);
      return {
        minSeats: Math.min(...seats), maxSeats: Math.max(...seats), lowestPrice: Math.min(...tiers.map(plan => plan.price)),
        periods: days.map(day => day >= 360 ? 'yıllık' : day % 30 === 0 ? `${day / 30} aylık` : `${day} günlük`).join(' ya da '),
        features: (tiers[0].features ?? []).filter(feature => tiers.every(plan => (plan.features ?? []).includes(feature)))
      };
    }));
  }

  getMyModules(): Observable<UserModules> {
    return this.http.get<{ data?: UserModules } & UserModules>(`${environment.speedReadingApiUrl}/subscriptions/my-modules`)
      .pipe(map(result => result?.data ?? result));
  }
}
