import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { LanguageService } from './language.service';

// Models
export interface LandingContentVm {
    blocks: { [key: string]: string };
}

export interface PageDto {
    id: string;
    title: string;
    slug: string;
    content: string;
    isPublished: boolean;
    seoSettings: SeoSettings;
    createdAt: string;
    updatedAt: string;
}

export interface BlogPostDto {
    id: string;
    title: string;
    slug: string;
    summary: string;
    content: string;
    author?: string;
    publishedAt?: string;
    tags: string[];
    coverImageUrl?: string;
    seoSettings: SeoSettings;
    viewCount: number;
    isPublished: boolean;
}

export interface BlogListVm {
    posts: BlogPostDto[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
}

export interface SeoSettings {
    metaTitle?: string;
    metaDescription?: string;
    metaKeywords?: string;
    noIndex: boolean;
}

export interface ContactMessageRequest {
    name: string;
    email: string;
    subject: string;
    message: string;
}

export interface NewsletterSubscribeRequest {
    email: string;
    name?: string;
}

@Injectable({
    providedIn: 'root'
})
export class PublicCmsService {
    private apiUrl = environment.speedReadingApiUrl + '/cms';
    private languageService = inject(LanguageService);

    constructor(private http: HttpClient) { }

    // Landing Page & Content Blocks
    getLandingContent(group: string = 'HomePage', language?: string): Observable<LandingContentVm> {
        const lang = language ?? this.languageService.currentLanguage();
        return this.http.get<any>(`${this.apiUrl}/landing`, { params: { group, language: lang } }).pipe(
            map(response => {
                const items = Array.isArray(response) ? response : (response?.data ?? []);
                return {
                blocks: Object.fromEntries(
                    items.map((item: any) => [item.key ?? item.blockKey, item.value ?? item.blockValue])
                ) as { [key: string]: string }
                };
            })
        );
    }

    // Pages
    getPage(slug: string): Observable<PageDto> {
        return this.http.get<any>(`${this.apiUrl}/pages/${slug}`).pipe(
            map(response => response?.data ?? response)
        );
    }

    // Blog
    getBlogPosts(page: number = 1, tag?: string): Observable<BlogListVm> {
        let params = new HttpParams().set('pageNumber', page.toString()).set('pageSize', '10');
        if (tag) {
            params = params.set('tag', tag);
        }
        return this.http.get<any>(`${this.apiUrl}/blog`, { params }).pipe(
            map(response => {
                const result = response?.data ?? response;
                return {
                    posts: result?.items ?? [],
                    totalCount: result?.totalCount ?? 0,
                    pageNumber: result?.pageNumber ?? page,
                    pageSize: result?.pageSize ?? 10,
                    totalPages: result?.totalPages ?? 0
                };
            })
        );
    }

    getBlogPost(slug: string): Observable<BlogPostDto> {
        return this.http.get<any>(`${this.apiUrl}/blog/${slug}`).pipe(
            map(response => response?.data ?? response)
        );
    }

    // Contact
    submitContact(data: ContactMessageRequest): Observable<string> {
        return this.http.post<any>(`${this.apiUrl}/contact`, data).pipe(
            map(response => response?.message ?? response?.data?.id ?? '')
        );
    }

    // Newsletter
    subscribeNewsletter(data: NewsletterSubscribeRequest): Observable<string> {
        return this.http.post<any>(`${this.apiUrl}/newsletter/subscribe`, data).pipe(
            map(response => response?.message ?? '')
        );
    }

    unsubscribeNewsletter(token: string): Observable<string> {
        return this.http.post<any>(`${this.apiUrl}/newsletter/unsubscribe`, { token }).pipe(
            map(response => response?.message ?? '')
        );
    }
}
