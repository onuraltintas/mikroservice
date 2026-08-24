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
    private apiUrl = environment.apiUrl + '/v1/admin/cms';

    constructor(private http: HttpClient) { }

    // --- Landing Page & Content Blocks ---
    getLandingContentForAdmin(group: string = 'HomePage'): Observable<ContentBlock[]> {
        return this.http.get<ContentBlock[]>(`${this.apiUrl}/landing`, { params: { group } });
    }

    updateLandingContent(blocks: { [key: string]: string }, group: string = 'HomePage'): Observable<void> {
        return this.http.put<void>(`${this.apiUrl}/landing`, { blocks, group });
    }

    // --- Newsletter ---
    getNewsletterSubscribers(): Observable<NewsletterSubscriber[]> {
        return this.http.get<any>(`${this.apiUrl}/newsletter/subscribers`).pipe(
            map(result => Array.isArray(result) ? result : (result?.items ?? []))
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
            map(result => Array.isArray(result) ? result : (result?.items ?? []))
        );
    }

    getPageById(id: string): Observable<Page> {
        return this.http.get<Page>(`${this.apiUrl}/pages/${id}`);
    }

    createPage(data: CreatePageRequest): Observable<string> {
        return this.http.post<string>(`${this.apiUrl}/pages`, data);
    }

    updatePage(id: string, data: UpdatePageRequest): Observable<void> {
        return this.http.put<void>(`${this.apiUrl}/pages/${id}`, data);
    }

    deletePage(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/pages/${id}`);
    }

    // --- Blog ---
    getBlogPosts(): Observable<BlogPost[]> {
        return this.http.get<any>(`${this.apiUrl}/blog`).pipe(
            map(result => Array.isArray(result) ? result : (result?.items ?? []))
        );
    }

    getBlogPostById(id: string): Observable<BlogPost> {
        return this.http.get<BlogPost>(`${this.apiUrl}/blog/${id}`);
    }

    createBlogPost(data: CreateBlogPostRequest): Observable<string> {
        return this.http.post<string>(`${this.apiUrl}/blog`, data);
    }

    updateBlogPost(id: string, data: CreateBlogPostRequest): Observable<void> {
        // Note: Backend might expect UpdateBlogPostRequest but structures are almost identical
        // We can reuse CreateBlogPostRequest interface or create a new one if strict ID is needed
        // Usually Update has ID in body too for safety
        return this.http.put<void>(`${this.apiUrl}/blog/${id}`, { id, ...data });
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

        return this.http.get<import('../models/cms.models').ContactMessageListResponse>(`${this.apiUrl}/contact-messages`, { params });
    }

    getUnreadContactMessageCount(): Observable<number> {
        return this.http.get<number>(`${this.apiUrl}/contact-messages/unread-count`);
    }

    replyToContactMessage(messageId: string, replyContent: string): Observable<void> {
        return this.http.post<void>(`${this.apiUrl}/contact-messages/reply`, { messageId, replyContent });
    }

    markContactMessageAsRead(id: string, isRead: boolean): Observable<void> {
        return this.http.put<void>(`${this.apiUrl}/contact-messages/${id}/read`, { id, isRead });
    }

    deleteContactMessage(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/contact-messages/${id}`);
    }
}
