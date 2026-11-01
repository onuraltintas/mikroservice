import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { environment } from '../../../environments/environment';

/** T2 (31.10.2026): the unread message count shown on the Mesajlar menu item and the phone bar. */
@Injectable({ providedIn: 'root' })
export class CoachingMessagesUnreadService {
  private readonly http = inject(HttpClient);
  private request?: Subscription;
  readonly count = signal(0);

  refresh() {
    this.request?.unsubscribe();
    this.request = this.http.get<{ count: number }>(`${environment.apiUrl}/coaching/messages/unread-count`).subscribe({
      next: response => this.count.set(Math.max(0, Number(response?.count) || 0)),
      error: () => this.count.set(0)
    });
  }
}
