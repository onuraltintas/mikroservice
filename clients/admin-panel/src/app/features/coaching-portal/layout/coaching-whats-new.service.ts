import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';

/** Paket F #20: the new menu went live on 01.11.2026 (Türkiye); students who registered from then on never saw the old one. */
export const WHATS_NEW_SINCE = Date.parse('2026-11-01T00:00:00+03:00');

/**
 * T2 (31.10.2026): the one-time "Yenilikler" card after the new student menu ships. There is no per-student preference
 * store on the server, so the dismissal is kept in this browser per account; a blocked storage only means the card
 * closes for this visit. Paket F #20: only students who registered before the new menu see it (the profile's creation date
 * comes with the shell's navigation answer; while it is unknown the card stays hidden).
 */
@Injectable({ providedIn: 'root' })
export class CoachingWhatsNewService {
  private readonly auth = inject(AuthService);
  private readonly navigation = inject(CoachingStudentNavigationService);
  private readonly dismissed = signal<Set<string>>(new Set());
  private readonly key = computed(() => `coaching-whats-new-v1:${this.auth.userProfile()?.id ?? 'anonymous'}`);
  readonly visible = computed(() => {
    const key = this.key();
    if (this.dismissed().has(key)) return false;
    const registered = Date.parse(this.navigation.registeredAt() ?? '');
    if (!Number.isFinite(registered) || registered >= WHATS_NEW_SINCE) return false;
    try { return localStorage.getItem(key) !== 'seen'; } catch { return true; }
  });

  dismiss() {
    const key = this.key();
    this.dismissed.update(set => new Set(set).add(key));
    try { localStorage.setItem(key, 'seen'); } catch { /* storage blocked: closed for this visit */ }
  }
}
