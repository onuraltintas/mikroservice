import { Injectable } from '@angular/core';
import { SwPush } from '@angular/service-worker';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { catchError, map, of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';

@Injectable({
    providedIn: 'root'
})
export class PushService {
    private readonly VAPID_PUBLIC_KEY = environment.vapidPublicKey;
    private readonly apiUrl = `${environment.apiUrl}/notifications/subscribe`;

    constructor(
        private swPush: SwPush,
        private http: HttpClient,
        private toastr: ToastrService
    ) { }

    requestSubscription() {
        if (!this.swPush.isEnabled) {
            console.warn('Service Worker is not enabled');
            this.toastr.warning('Service Worker aktif değil. Tarayıcınız desteklemiyor veya dev modundasınız.');
            return;
        }

        this.swPush.requestSubscription({
            serverPublicKey: this.VAPID_PUBLIC_KEY
        })
            .then(sub => {
                this.sendSubscriptionToBackend(sub);
            })
            .catch(err => {
                console.error('Could not subscribe to notifications', err);
                this.toastr.error('Bildirim izni alınamadı: ' + err.message);
            });
    }

    private sendSubscriptionToBackend(subscription: PushSubscription) {
        const keys = subscription.toJSON().keys;
        if (!keys) return;

        this.http.post(this.apiUrl, {
            endpoint: subscription.endpoint,
            p256dh: keys['p256dh'],
            auth: keys['auth']
        }).subscribe({
            next: () => this.toastr.success('Bildirimler başarıyla açıldı!'),
            error: err => {
                console.error('Could not send subscription to backend', err);
                this.toastr.error('Sunucu kaydı başarısız oldu.');
            }
        });
    }
}
