import { Component, DestroyRef, Input, OnChanges, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, finalize } from 'rxjs';
import { CoachingPlanningPermissionService, PlanningPermission } from '../../../../../../src/app/core/services/coaching-planning-permission.service';

/**
 * Live test 16.11.2026 (#26): "Öğrenci kendi planını yapabilsin" stood only inside the student's report. The teacher who
 * manages a plan from Çalışma takibi finds the same switch next to the plan.
 */
@Component({
  selector: 'staff-coaching-planning-permission', standalone: true,
  styles: [':host{display:block}label{display:inline-flex;align-items:center;gap:.5rem;font-weight:600}p{margin:.25rem 0 0}'],
  template: `
    @if (planning(); as permission) {
      <div data-testid="plan-page-planning-permission">
        @if (!permission.hasTeacher) { <p>Öğrencinin atanmış öğretmeni yok; planını kendisi yapar.</p> }
        @else {
          <label>
            <input type="checkbox" [checked]="permission.allowedByTeacher" [disabled]="saving()" (change)="set($any($event.target).checked)">
            Öğrenci kendi planını yapabilsin
          </label>
          <p>{{ permission.allowedByTeacher
            ? 'Öğrenci planını ve haftalık düzenini kendisi değiştirebilir.'
            : 'Planı ve haftalık düzeni yalnız öğretmeni değiştirir; öğrenci çalışmalarını işaretler. Değişiklik öğrenciye bildirilir.' }}</p>
        }
        @if (error()) { <p role="alert">{{ error() }}</p> }
      </div>
    }`
})
export class CoachingPlanningPermissionComponent implements OnChanges {
  @Input({ required: true }) studentId = '';
  private readonly permissions = inject(CoachingPlanningPermissionService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  readonly planning = signal<PlanningPermission | null>(null);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  ngOnChanges(): void {
    this.request?.unsubscribe(); this.planning.set(null); this.error.set(null);
    if (!this.studentId) return;
    this.request = this.permissions.forStudent(this.studentId).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: state => this.planning.set(state), error: () => undefined });
  }

  set(allowed: boolean): void {
    if (this.saving()) return;
    this.saving.set(true); this.error.set(null);
    this.permissions.set(this.studentId, allowed).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false)))
      .subscribe({ next: state => this.planning.set(state),
        // The box goes back to the saved state.
        error: () => { this.error.set('Ayar kaydedilemedi. Yetkiyi ve bağlantıyı kontrol edin.'); this.planning.update(state => state ? { ...state } : state); } });
  }
}
