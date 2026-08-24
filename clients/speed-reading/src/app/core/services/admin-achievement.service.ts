import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface Achievement {
    id: string;
    name: string;
    description: string;
    category: string;
    tier: string;
    iconUrl: string;
    iconEmoji: string;
    criteriaType: string;
    criteriaValue: string;
    triggerType?: string;
    triggerValue?: number;
    isRepeatable: boolean;
    xpReward: number;
    isActive: boolean;
    sortOrder: number;
    createdAt: string;
    updatedAt?: string;
    unlockedByUsersCount: number;
}

export interface AchievementListResponse {
    items: Achievement[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
}

export interface CreateAchievementRequest {
    name: string;
    description: string;
    category: string;
    tier: string;
    iconUrl?: string;
    iconEmoji: string;
    criteriaType: string;
    criteriaValue: string;
    triggerType?: string;
    triggerValue?: number;
    isRepeatable: boolean;
    xpReward: number;
    isActive: boolean;
    sortOrder: number;
}

export interface UpdateAchievementRequest extends CreateAchievementRequest { }

@Injectable({
    providedIn: 'root'
})
export class AdminAchievementService {
    private apiUrl = `${environment.apiUrl}/speed-reading/achievements`;

    constructor(private http: HttpClient) { }

    getAchievements(
        pageNumber: number = 1,
        pageSize: number = 20,
        searchTerm?: string,
        category?: string,
        tier?: string,
        isActive?: boolean,
        sortBy: string = 'SortOrder',
        sortDescending: boolean = false
    ): Observable<AchievementListResponse> {
        let params = new HttpParams()
            .set('pageNumber', pageNumber.toString())
            .set('pageSize', pageSize.toString())
            .set('sortBy', sortBy)
            .set('sortDescending', sortDescending.toString());

        if (searchTerm) params = params.set('searchTerm', searchTerm);
        if (category) params = params.set('category', category);
        if (tier) params = params.set('tier', tier);
        if (isActive !== undefined) params = params.set('isActive', isActive.toString());

        return this.http.get<any>(`${this.apiUrl}/admin`, { params })
            .pipe(map(response => {
                // Handle both wrapped and unwrapped responses
                const data = response?.data || response;
                return {
                    items: data?.items || [],
                    totalCount: data?.totalCount || 0,
                    pageNumber: data?.pageNumber || pageNumber,
                    pageSize: data?.pageSize || pageSize,
                    totalPages: data?.totalPages || 0
                };
            }));
    }

    getAchievementById(id: string): Observable<Achievement> {
        return this.http.get<Achievement>(`${this.apiUrl}/admin/${id}`);
    }

    createAchievement(request: CreateAchievementRequest): Observable<Achievement> {
        return this.http.post<Achievement>(this.apiUrl, request, {
            headers: this.idempotencyHeaders()
        });
    }

    updateAchievement(id: string, request: UpdateAchievementRequest): Observable<Achievement> {
        return this.http.put<Achievement>(`${this.apiUrl}/${id}`, request, {
            headers: this.idempotencyHeaders()
        });
    }

    deleteAchievement(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${id}`, {
            headers: this.idempotencyHeaders()
        });
    }

    getCategories(): Observable<string[]> {
        return this.http.get<any>(`${this.apiUrl}/categories`)
            .pipe(map(response => response?.data || response || []));
    }

    getTiers(): Observable<string[]> {
        return this.http.get<any>(`${this.apiUrl}/tiers`)
            .pipe(map(response => response?.data || response || []));
    }

    getStats(): Observable<AchievementStats> {
        return this.http.get<any>(`${this.apiUrl}/admin/stats`)
            .pipe(map(response => {
                const data = response?.data || response;
                return {
                    totalCount: data?.totalCount || 0,
                    bronzeCount: data?.bronzeCount || 0,
                    silverCount: data?.silverCount || 0,
                    goldCount: data?.goldCount || 0,
                    diamondCount: data?.diamondCount || 0,
                    activeCount: data?.activeCount || 0,
                    inactiveCount: data?.inactiveCount || 0,
                    categoryCounts: data?.categoryCounts || {}
                };
            }));
    }

    private idempotencyHeaders(): HttpHeaders {
        const key = globalThis.crypto?.randomUUID?.()
            ?? `achievement-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        return new HttpHeaders({ 'Idempotency-Key': key });
    }
}

export interface AchievementStats {
    totalCount: number;
    bronzeCount: number;
    silverCount: number;
    goldCount: number;
    diamondCount: number;
    activeCount: number;
    inactiveCount: number;
    categoryCounts: { [key: string]: number };
}
