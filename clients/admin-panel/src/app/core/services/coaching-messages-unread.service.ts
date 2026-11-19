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
  private answeredAt = 0;
  readonly count = signal(0);
  /** Live test 19.11.2026 (C8): page changes ask at most once a minute; a read conversation or a new account asks at once. */
  static readonly MinimumIntervalMs = 60_000;

  /** Without options a request in flight is shared, and a recent answer is reused. */
  refresh(options: CoachingRefreshOptions = {}, now = Date.now()) {
    if (options.reset) this.count.set(0);
    if (options.force || options.reset) { this.request?.unsubscribe(); this.request = undefined; this.answeredAt = 0; }
    if (this.request && !this.request.closed) return;
    if (this.answeredAt && now - this.answeredAt < CoachingMessagesUnreadService.MinimumIntervalMs) return;
    this.request = this.http.get<{ count: number }>(`${environment.apiUrl}/coaching/messages/unread-count`).subscribe({
      next: response => { this.answeredAt = Date.now(); this.count.set(Math.max(0, Number(response?.count) || 0)); },
      error: () => this.count.set(0)
    });
  }
}
