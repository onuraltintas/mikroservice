import { effect, Injectable, inject, signal, PLATFORM_ID, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import * as signalR from '@microsoft/signalr';
import { Observable } from 'rxjs';

export interface Notification {
    id: string;
    title: string;
    message: string;
    type: string;
    createdAt: Date;
    isRead: boolean;
    relatedEntityId?: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationHubConnectionFactory {
    create(hubUrl: string, accessTokenFactory: () => string): signalR.HubConnection {
        return new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, { accessTokenFactory })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Error)
            .build();
    }
}

@Injectable({
    providedIn: 'root'
})
export class NotificationService implements OnDestroy {
    private http = inject(HttpClient);
    private authService = inject(AuthService);
    private platformId = inject(PLATFORM_ID);
    private hubConnectionFactory = inject(NotificationHubConnectionFactory);

    private apiUrl = `${environment.apiUrl}/notifications`;
    private hubUrl = `${environment.apiUrl.replace('/api', '')}/hubs/notifications`;

    private hubConnection?: signalR.HubConnection;
    private sessionGeneration = 0;
    private sessionTransition = Promise.resolve();

    private _notifications = signal<Notification[]>([]);
    notifications = this._notifications.asReadonly();

    private _unreadCount = signal<number>(0);
    unreadCount = this._unreadCount.asReadonly();

    private readonly authSessionEffect = effect(() => {
        const userId = this.authService.userProfile()?.id ?? null;
        this.queueSessionTransition(userId);
    });

    constructor() {
        // The authSessionEffect owns browser session startup and teardown.
    }

    private queueSessionTransition(userId: string | null) {
        if (!isPlatformBrowser(this.platformId)) return;

        const generation = ++this.sessionGeneration;
        if (!userId) this.clearNotifications();
        this.sessionTransition = this.sessionTransition
            .then(() => this.transitionSession(userId, generation))
            .catch(error => console.error('SignalR session transition failed:', error));
    }

    private async transitionSession(userId: string | null, generation: number) {
        const previousConnection = this.hubConnection;
        this.hubConnection = undefined;
        this.clearNotifications();

        if (previousConnection) {
            await previousConnection.stop();
        }

        if (generation !== this.sessionGeneration || !userId || !this.authService.getToken()) {
            return;
        }

        const connection = this.hubConnectionFactory.create(
            this.hubUrl,
            () => this.authService.getToken());

        connection.on('ReceiveNotification', (notification: Notification) => {
            const alreadyReceived = this._notifications().some(existing => existing.id === notification.id);
            if (!alreadyReceived) {
                this._notifications.update(prev => [notification, ...prev]);
                this._unreadCount.update(count => count + 1);
            }
        });

        this.hubConnection = connection;
        await connection.start();

        if (generation !== this.sessionGeneration) {
            if (this.hubConnection === connection) this.hubConnection = undefined;
            await connection.stop();
            return;
        }

        this.fetchNotifications();
    }

    private clearNotifications() {
        this._notifications.set([]);
        this._unreadCount.set(0);
    }

    fetchNotifications() {
        if (!this.authService.isAuthenticated()) return;

        this.http.get<Notification[]>(`${this.apiUrl}?pageNumber=1&pageSize=100`, {
            observe: 'response'
        }).subscribe(response => {
            const notifications = response.body ?? [];
            this._notifications.set(notifications);
            const unreadHeader = response.headers.get('X-Unread-Count');
            const unreadCount = unreadHeader === null
                ? notifications.filter(n => !n.isRead).length
                : Number.parseInt(unreadHeader, 10);
            this._unreadCount.set(Number.isFinite(unreadCount) ? unreadCount : 0);
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
        this.authSessionEffect.destroy();
        this.sessionGeneration++;
        this.clearNotifications();
        void this.hubConnection?.stop();
        this.hubConnection = undefined;
    }
}
