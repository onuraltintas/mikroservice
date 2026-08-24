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
    private apiUrl = environment.apiUrl + '/v1/cms';
    private languageService = inject(LanguageService);

    constructor(private http: HttpClient) { }

    // Landing Page & Content Blocks
    getLandingContent(group: string = 'HomePage', language?: string): Observable<LandingContentVm> {
        const lang = language ?? this.languageService.currentLanguage();
        return this.http.get<any[]>(`${this.apiUrl}/landing`, { params: { group, language: lang } }).pipe(
            map(items => ({
                blocks: Object.fromEntries(
                    (items || []).map((item: any) => [item.blockKey, item.blockValue])
                ) as { [key: string]: string }
            }))
        );
    }

    // Pages
    getPage(slug: string): Observable<PageDto> {
        return this.http.get<PageDto>(`${this.apiUrl}/pages/${slug}`);
    }

    // Blog
    getBlogPosts(page: number = 1, tag?: string): Observable<BlogListVm> {
        let params = new HttpParams().set('page', page.toString());
        if (tag) {
            params = params.set('tag', tag);
        }
        return this.http.get<BlogListVm>(`${this.apiUrl}/blog`, { params });
    }

    getBlogPost(slug: string): Observable<BlogPostDto> {
        return this.http.get<BlogPostDto>(`${this.apiUrl}/blog/${slug}`);
    }

    // Contact
    submitContact(data: ContactMessageRequest): Observable<string> {
        return this.http.post<string>(`${this.apiUrl}/contact`, data);
    }

    // Newsletter
    subscribeNewsletter(data: NewsletterSubscribeRequest): Observable<string> {
        return this.http.post<string>(`${this.apiUrl}/newsletter/subscribe`, data);
    }
}
