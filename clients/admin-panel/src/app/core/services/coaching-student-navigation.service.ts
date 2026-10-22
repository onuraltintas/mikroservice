import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { environment } from '../../../environments/environment';

interface StudentNavigation { showAssignments: boolean; showSessions: boolean; showFees?: boolean }

@Injectable({ providedIn: 'root' })
export class CoachingStudentNavigationService {
  private readonly http = inject(HttpClient);
  private readonly state = signal<StudentNavigation | null>(null);
  private request?: Subscription;
  // An unavailable status must never hide access to existing student records.
  readonly showAssignments = computed(() => this.state()?.showAssignments ?? true);
  readonly showFees = computed(() => this.state()?.showFees ?? true);
  readonly showSessions = computed(() => this.state()?.showSessions ?? true);
  refresh(resetForAccount = false) {
    this.request?.unsubscribe();
    if (resetForAccount) this.state.set(null);
    this.request = this.http.get<{data: StudentNavigation}>(`${environment.apiUrl}/coaching/profiles/me/navigation`)
      .subscribe({next: response => this.state.set(response.data), error: () => this.state.set(null)});
  }
}
