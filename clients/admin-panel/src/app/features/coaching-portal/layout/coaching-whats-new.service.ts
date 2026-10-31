import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthService } from '../../../core/auth/auth.service';

/**
 * T2 (31.10.2026): the one-time "Yenilikler" card after the new student menu ships. There is no per-student preference
 * store on the server, so the dismissal is kept in this browser per account; a blocked storage only means the card
 * closes for this visit.
 */
@Injectable({ providedIn: 'root' })
export class CoachingWhatsNewService {
  private readonly auth = inject(AuthService);
  private readonly dismissed = signal<Set<string>>(new Set());
  private readonly key = computed(() => `coaching-whats-new-v1:${this.auth.userProfile()?.id ?? 'anonymous'}`);
  readonly visible = computed(() => {
    const key = this.key();
    if (this.dismissed().has(key)) return false;
    try { return localStorage.getItem(key) !== 'seen'; } catch { return true; }
  });

  dismiss() {
    const key = this.key();
    this.dismissed.update(set => new Set(set).add(key));
    try { localStorage.setItem(key, 'seen'); } catch { /* storage blocked: closed for this visit */ }
  }
}
