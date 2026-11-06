import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { environment } from '../../../environments/environment';

/** registeredAt (Paket F #20): when the student's coaching profile was made (UTC ISO), null without one. */
interface StudentNavigation { showAssignments: boolean; showSessions: boolean; showFees?: boolean; registeredAt?: string | null }

@Injectable({ providedIn: 'root' })
export class CoachingStudentNavigationService {
  private readonly http = inject(HttpClient);
  private readonly state = signal<StudentNavigation | null>(null);
  private readonly settled = signal(false);
  private request?: Subscription;
  // An unavailable status must never hide access to existing student records.
  readonly showAssignments = computed(() => this.state()?.showAssignments ?? true);
  readonly showFees = computed(() => this.state()?.showFees ?? true);
  readonly showSessions = computed(() => this.state()?.showSessions ?? true);
  readonly registeredAt = computed(() => this.state()?.registeredAt ?? null);
  /** Paket F #36: the server has answered (or failed); until then the menu shows only its fixed items. */
  readonly loaded = this.settled.asReadonly();

  refresh(resetForAccount = false) {
    if (resetForAccount) { this.request?.unsubscribe(); this.request = undefined; this.state.set(null); this.settled.set(false); }
    // Paket F #36: a request in flight is shared, not aborted and sent again.
    if (this.request && !this.request.closed) return;
    this.request = this.http.get<{data: StudentNavigation}>(`${environment.apiUrl}/coaching/profiles/me/navigation`)
      .subscribe({
        next: response => { this.state.set(response.data); this.settled.set(true); },
        error: () => { this.state.set(null); this.settled.set(true); }
      });
  }
}
