import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';

// ---------------------------------------------------------------------------
// Product
// ---------------------------------------------------------------------------

export interface Product {
  id: string;
  slug: string;
  name: string;
  description: string;
  /** Bu ürünle birlikte erişim sağlanan ürün slug'ları (combo için) */
  includedProductSlugs: string[];
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
}

export interface CreateProductRequest {
  slug: string;
  name: string;
  description: string;
  includedProductSlugs: string[];
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
}

export interface UpdateProductRequest {
  name?: string;
  description?: string;
  includedProductSlugs?: string[];
  isActive?: boolean;
  isPublic?: boolean;
  sortOrder?: number;
}

// ---------------------------------------------------------------------------
// Subscription Plan
// ---------------------------------------------------------------------------

export interface SubscriptionPlan {
  id: string;
  name: string;
  description: string;
  slug: string;
  // Ürün bilgileri
  productId: string;
  productSlug: string;
  productName: string;
  /** Bu planın erişim sağladığı tüm ürün slug'ları */
  includedProductSlugs: string[];
  /** Geriye dönük uyumluluk — eski kod için */
  modules: string[];
  price: number;
  billingPeriod: 'Monthly' | 'Quarterly' | 'Annual' | 'Lifetime';
  durationDays: number | null;
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
  features: string[] | null;
}

export interface CreatePlanRequest {
  name: string;
  description: string;
  slug: string;
  productId: string;
  price: number;
  billingPeriod: string;
  durationDays: number | null;
  isActive: boolean;
  isPublic: boolean;
  sortOrder: number;
  features: string[] | null;
}

export interface UpdatePlanRequest {
  name?: string;
  description?: string;
  price?: number;
  isActive?: boolean;
  isPublic?: boolean;
  sortOrder?: number;
  features?: string[] | null;
}

// ---------------------------------------------------------------------------
// User Subscription
// ---------------------------------------------------------------------------

export interface UserSubscription {
  id: string;
  userId: string;
  userName: string | null;
  userEmail: string | null;
  plan: SubscriptionPlan;
  productSlug: string;
  productName: string;
  status: 'Active' | 'Expired' | 'Cancelled' | 'Trial';
  startDate: string;
  endDate: string | null;
  notes: string | null;
  createdAt: string;
  isActive: boolean;
}

export interface CreateSubscriptionRequest {
  userId: string;
  userName: string | null;
  userEmail: string | null;
  planId: string;
  startDate: string;
  endDate: string | null;
  notes: string | null;
}

export interface UpdateSubscriptionRequest {
  status: string;
  endDate: string | null;
  notes: string | null;
}

export interface SubscriptionPagedResult {
  items: UserSubscription[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ---------------------------------------------------------------------------
// Access / Modules
// ---------------------------------------------------------------------------

/** Yeni erişim modeli — ürün slug'larına dayalı */
export interface UserAccess {
  products: string[];
  hasSpeedReading: boolean;
  hasCoaching: boolean;
}

/** Geriye dönük uyumluluk */
export interface UserModules {
  modules: string[];
  hasSpeedReading: boolean;
  hasCoaching: boolean;
}

// ---------------------------------------------------------------------------
// Misc
// ---------------------------------------------------------------------------

export interface UserSearchResult {
  id: string;
  fullName: string;
  email: string;
  roles: string[];
}

// ---------------------------------------------------------------------------
// Service
// ---------------------------------------------------------------------------

@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private readonly http = inject(HttpClient);
  private readonly productsUrl = `${environment.speedReadingApiUrl}/products`;
  private readonly plansUrl    = `${environment.speedReadingApiUrl}/subscription-plans`;
  private readonly subsUrl     = `${environment.speedReadingApiUrl}/subscriptions`;

  // ── Products ──────────────────────────────────────────────────────────────
  getPublicProducts(): Observable<Product[]> {
    return this.http.get<any>(this.productsUrl).pipe(map(result => result?.data ?? result ?? []));
  }

  getAllProducts(): Observable<Product[]> {
    return this.http.get<any>(`${this.productsUrl}/all`).pipe(map(result => result?.data ?? result ?? []));
  }

  createProduct(request: CreateProductRequest): Observable<Product> {
    return this.http.post<any>(this.productsUrl, request).pipe(map(result => result?.data ?? result));
  }

  updateProduct(id: string, request: UpdateProductRequest): Observable<Product> {
    return this.http.put<any>(`${this.productsUrl}/${id}`, request).pipe(map(result => result?.data ?? result));
  }

  deactivateProduct(id: string): Observable<void> {
    return this.http.delete<void>(`${this.productsUrl}/${id}`);
  }

  // ── Plans ─────────────────────────────────────────────────────────────────
  getPublicPlans(): Observable<SubscriptionPlan[]> {
    return this.http.get<any>(this.plansUrl).pipe(map(result => result?.data ?? result ?? []));
  }

  getAllPlans(): Observable<SubscriptionPlan[]> {
    return this.http.get<any>(`${this.plansUrl}/all`).pipe(map(result => result?.data ?? result ?? []));
  }

  createPlan(request: CreatePlanRequest): Observable<SubscriptionPlan> {
    return this.http.post<any>(this.plansUrl, request).pipe(map(result => result?.data ?? result));
  }

  updatePlan(id: string, request: UpdatePlanRequest): Observable<SubscriptionPlan> {
    return this.http.put<any>(`${this.plansUrl}/${id}`, request).pipe(map(result => result?.data ?? result));
  }

  deactivatePlan(id: string): Observable<void> {
    return this.http.delete<void>(`${this.plansUrl}/${id}`);
  }

  // ── User search (autocomplete) ────────────────────────────────────────────
  searchUsers(search: string): Observable<UserSearchResult[]> {
    const params = new HttpParams().set('searchTerm', search).set('pageSize', '10');
    return this.http.get<any>(`${environment.apiUrl}/v1/users`, { params })
      .pipe(map((r: any) => {
        const items: any[] = r?.items ?? [];
        return items.map(u => ({
          id: u.id,
          fullName: (u.fullName || '').trim() || u.email?.split('@')[0] || u.id,
          email: u.email ?? '',
          roles: u.roles ?? []
        } as UserSearchResult));
      }));
  }

  // ── Subscriptions ─────────────────────────────────────────────────────────
  getSubscriptions(params?: {
    search?: string;
    status?: string;
    page?: number;
    pageSize?: number;
  }): Observable<SubscriptionPagedResult> {
    let httpParams = new HttpParams();
    if (params?.search)    httpParams = httpParams.set('search', params.search);
    if (params?.status)    httpParams = httpParams.set('status', params.status);
    if (params?.page)      httpParams = httpParams.set('page', params.page.toString());
    if (params?.pageSize)  httpParams = httpParams.set('pageSize', params.pageSize.toString());
    return this.http.get<any>(this.subsUrl, { params: httpParams }).pipe(
      map(result => result?.data ?? result)
    );
  }

  getUserSubscriptions(userId: string): Observable<UserSubscription[]> {
    return this.http.get<any>(`${this.subsUrl}/user/${userId}`).pipe(map(result => result?.data ?? result ?? []));
  }

  createSubscription(request: CreateSubscriptionRequest): Observable<UserSubscription> {
    return this.http.post<any>(this.subsUrl, request).pipe(map(result => result?.data ?? result));
  }

  updateSubscription(id: string, request: UpdateSubscriptionRequest): Observable<UserSubscription> {
    return this.http.put<any>(`${this.subsUrl}/${id}`, request).pipe(map(result => result?.data ?? result));
  }

  deleteSubscription(id: string): Observable<void> {
    return this.http.delete<void>(`${this.subsUrl}/${id}`);
  }

  // ── Access ────────────────────────────────────────────────────────────────
  /** Yeni erişim endpoint'i — ürün slug'larına dayalı */
  getMyAccess(): Observable<UserAccess> {
    return this.http.get<any>(`${this.subsUrl}/my-access`).pipe(map(result => result?.data ?? result));
  }

  /** Geriye dönük uyumluluk */
  getMyModules(): Observable<UserModules> {
    return this.http.get<any>(`${this.subsUrl}/my-modules`).pipe(map(result => result?.data ?? result));
  }
}
