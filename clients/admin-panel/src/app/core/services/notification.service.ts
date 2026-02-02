import { Injectable, inject, signal, PLATFORM_ID, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import * as signalR from '@microsoft/signalr';
import { Observable, BehaviorSubject, from } from 'rxjs';

export interface Notification {
    id: string;
    title: string;
    message: string;
    type: string;
    createdAt: Date;
    isRead: boolean;
    relatedEntityId?: string;
}

@Injectable({
    providedIn: 'root'
})
export class NotificationService implements OnDestroy {
    private http = inject(HttpClient);
    private authService = inject(AuthService);
    private platformId = inject(PLATFORM_ID);

    private apiUrl = `${environment.apiUrl}/notifications`;
    private hubUrl = `${environment.apiUrl.replace('/api', '')}/hubs/notifications`;

    private hubConnection?: signalR.HubConnection;

    private _notifications = signal<Notification[]>([]);
    notifications = this._notifications.asReadonly();

    private _unreadCount = signal<number>(0);
    unreadCount = this._unreadCount.asReadonly();

    constructor() {
        if (isPlatformBrowser(this.platformId)) {
            this.initSignalR();
            this.fetchNotifications();
        }
    }

    private initSignalR() {
        const token = this.authService.getToken();
        if (!token) return;

        this.hubConnection = new signalR.HubConnectionBuilder()
            .withUrl(this.hubUrl, {
                accessTokenFactory: () => token
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Error)
            .build();

        this.hubConnection.start()
            .then(() => console.log('SignalR Notification Hub connected'))
            .catch(err => console.error('SignalR Error: ', err));

        this.hubConnection.on('ReceiveNotification', (notification: Notification) => {
            this._notifications.update(prev => [notification, ...prev]);
            this._unreadCount.update(count => count + 1);
        });
    }

    fetchNotifications() {
        if (!this.authService.isAuthenticated()) return;

        this.http.get<Notification[]>(this.apiUrl).subscribe(notifications => {
            this._notifications.set(notifications);
            this._unreadCount.set(notifications.filter(n => !n.isRead).length);
        });
    }

    markAsRead(id: string): Observable<any> {
        return this.http.post(`${this.apiUrl}/${id}/mark-as-read`, {});
    }

    markAllAsRead(): Observable<any> {
        return this.http.post(`${this.apiUrl}/mark-all-as-read`, {});
    }

    deleteNotification(id: string): Observable<any> {
        return this.http.delete(`${this.apiUrl}/${id}`);
    }

    replyToSupportRequest(supportRequestId: string, message: string): Observable<any> {
        return this.http.post(`${environment.apiUrl}/support/reply`, {
            supportRequestId,
            replyMessage: message
        });
    }

    ngOnDestroy() {
        this.hubConnection?.stop();
    }
}
