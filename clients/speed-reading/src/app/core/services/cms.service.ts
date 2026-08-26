import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
    CreatePageRequest,
    UpdatePageRequest,
    CreateBlogPostRequest,
    NewsletterSubscriber,
    Page,
    BlogPost,
    ContentBlock
} from '../models/cms.models';

@Injectable({
    providedIn: 'root'
})
export class CmsService {
    private apiUrl = environment.speedReadingApiUrl + '/admin/cms';

    constructor(private http: HttpClient) { }

    // --- Landing Page & Content Blocks ---
    getLandingContentForAdmin(group: string = 'HomePage'): Observable<ContentBlock[]> {
        return this.http.get<any>(`${this.apiUrl}/landing`, { params: { group } }).pipe(
            map(result => result?.data ?? result ?? [])
        );
    }

    updateLandingContent(blocks: { [key: string]: string }, group: string = 'HomePage'): Observable<void> {
        return this.http.put<any>(`${this.apiUrl}/landing`, { blocks, group }).pipe(map(() => undefined));
    }

    // --- Newsletter ---
    getNewsletterSubscribers(): Observable<NewsletterSubscriber[]> {
        return this.http.get<any>(`${this.apiUrl}/newsletter/subscribers`).pipe(
            map(result => {
                const data = result?.data ?? result;
                return Array.isArray(data) ? data : (data?.items ?? []);
            })
        );
    }

    deleteNewsletterSubscriber(id: string, hardDelete: boolean = false): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/newsletter/subscribers/${id}`, {
            params: { hardDelete: hardDelete.toString() }
        });
    }

    // --- Pages ---
    getPages(): Observable<Page[]> {
        return this.http.get<any>(`${this.apiUrl}/pages`).pipe(
            map(result => {
                const data = result?.data ?? result;
                return Array.isArray(data) ? data : (data?.items ?? []);
            })
        );
    }

    getPageById(id: string): Observable<Page> {
        return this.http.get<any>(`${this.apiUrl}/pages/${id}`).pipe(
            map(result => result?.data ?? result)
        );
    }

    createPage(data: CreatePageRequest): Observable<string> {
        return this.http.post<any>(`${this.apiUrl}/pages`, data).pipe(
            map(result => result?.data?.id ?? result?.id ?? '')
        );
    }

    updatePage(id: string, data: UpdatePageRequest): Observable<void> {
        return this.http.put<any>(`${this.apiUrl}/pages/${id}`, data).pipe(map(() => undefined));
    }

    deletePage(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/pages/${id}`);
    }

    // --- Blog ---
    getBlogPosts(): Observable<BlogPost[]> {
        return this.http.get<any>(`${this.apiUrl}/blog`).pipe(
            map(result => {
                const data = result?.data ?? result;
                return Array.isArray(data) ? data : (data?.items ?? []);
            })
        );
    }

    getBlogPostById(id: string): Observable<BlogPost> {
        return this.http.get<any>(`${this.apiUrl}/blog/${id}`).pipe(
            map(result => result?.data ?? result)
        );
    }

    createBlogPost(data: CreateBlogPostRequest): Observable<string> {
        return this.http.post<any>(`${this.apiUrl}/blog`, data).pipe(
            map(result => result?.data?.id ?? result?.id ?? '')
        );
    }

    updateBlogPost(id: string, data: CreateBlogPostRequest): Observable<void> {
        // Note: Backend might expect UpdateBlogPostRequest but structures are almost identical
        // We can reuse CreateBlogPostRequest interface or create a new one if strict ID is needed
        // Usually Update has ID in body too for safety
        return this.http.put<any>(`${this.apiUrl}/blog/${id}`, { id, ...data }).pipe(map(() => undefined));
    }

    deleteBlogPost(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/blog/${id}`);
    }

    // --- Contact Messages ---
    getContactMessages(
        pageNumber: number = 1,
        pageSize: number = 10,
        searchTerm?: string,
        isRead?: boolean,
        isReplied?: boolean
    ): Observable<import('../models/cms.models').ContactMessageListResponse> {
        let params: any = { pageNumber, pageSize };
        if (searchTerm) params.searchTerm = searchTerm;
        if (isRead !== undefined) params.isRead = isRead;
        if (isReplied !== undefined) params.isReplied = isReplied;

        return this.http.get<any>(`${this.apiUrl}/contact-messages`, { params }).pipe(
            map(result => result?.data ?? result)
        );
    }

    getUnreadContactMessageCount(): Observable<number> {
        return this.http.get<any>(`${this.apiUrl}/contact-messages/unread-count`).pipe(
            map(result => result?.data ?? result ?? 0)
        );
    }

    replyToContactMessage(messageId: string, replyContent: string): Observable<void> {
        return this.http.post<any>(`${this.apiUrl}/contact-messages/reply`, { messageId, replyContent }).pipe(map(() => undefined));
    }

    markContactMessageAsRead(id: string, isRead: boolean): Observable<void> {
        return this.http.put<any>(`${this.apiUrl}/contact-messages/${id}/read`, { id, isRead }).pipe(map(() => undefined));
    }

    deleteContactMessage(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/contact-messages/${id}`);
    }
}
