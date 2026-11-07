import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { environment } from '../../../environments/environment';

/** How a refresh treats a request in flight (Paket F #36, review of PR 122). */
export interface CoachingRefreshOptions {
  /** Something changed (a conversation was read): abort the request in flight and ask again. */
  force?: boolean;
  /** A new account (the shell opens): forget the last answer and ask again. */
  reset?: boolean;
}

/** T2 (31.10.2026): the unread message count shown on the Mesajlar menu item and the phone bar. */
@Injectable({ providedIn: 'root' })
export class CoachingMessagesUnreadService {
  private readonly http = inject(HttpClient);
  private request?: Subscription;
  readonly count = signal(0);

  /** Without options a request in flight is shared (one request per page change). */
  refresh(options: CoachingRefreshOptions = {}) {
    if (options.reset) this.count.set(0);
    if (options.force || options.reset) { this.request?.unsubscribe(); this.request = undefined; }
    if (this.request && !this.request.closed) return;
    this.request = this.http.get<{ count: number }>(`${environment.apiUrl}/coaching/messages/unread-count`).subscribe({
      next: response => this.count.set(Math.max(0, Number(response?.count) || 0)),
      error: () => this.count.set(0)
    });
  }
}
