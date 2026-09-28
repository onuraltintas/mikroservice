import { computed, inject, Injectable, signal } from '@angular/core';
import { AuthService, hasRole } from '../../core/auth/auth.service';

export type CoachingPortalView = 'Student' | 'Teacher' | 'Parent';

@Injectable({ providedIn: 'root' })
export class CoachingPortalViewService {
  private readonly auth = inject(AuthService);
  private readonly preferred = signal<CoachingPortalView | null>(null);

  readonly current = computed<CoachingPortalView | null>(() => {
    const profile = this.auth.userProfile();
    const preferred = this.preferred();
    if (preferred && hasRole(profile, preferred)) return preferred;
    if (hasRole(profile, 'Teacher')) return 'Teacher';
    if (hasRole(profile, 'Student')) return 'Student';
    if (hasRole(profile, 'Parent')) return 'Parent';
    return null;
  });

  canSelect(view: CoachingPortalView): boolean {
    return hasRole(this.auth.userProfile(), view);
  }

  select(view: CoachingPortalView): void {
    if (this.canSelect(view)) this.preferred.set(view);
  }
}
