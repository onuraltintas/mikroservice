import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { CoachingPlanningPermissionService } from '../../../core/services/coaching-planning-permission.service';
import { STUDENT_HUBS, StudentHubTab } from './student-hubs';
import { StudentPlanEditStepsComponent } from './student-plan-edit-steps.component';

/** T2 (31.10.2026): one menu item, several pages — the hub shows its sections as tabs above the current page. */
@Component({
  selector: 'app-student-hub',
  standalone: true,
  imports: [RouterLink, RouterOutlet, StudentPlanEditStepsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (tabs().length > 1) {
      <nav class="hub-tabs" [attr.aria-label]="hub.label + ' bölümleri'">
        @for (tab of tabs(); track tab.path) {
          <a [routerLink]="tab.path" [class.is-active]="isActive(tab)" [attr.aria-current]="isActive(tab) ? 'page' : null">{{ tab.label }}</a>
        }
      </nav>
    }
    @if (teacherPlans()) {
      <p class="hub-notice" role="status" data-testid="teacher-plans-notice">
        Çalışma planını öğretmenin hazırlıyor. Planı sen değiştiremezsin; çalışmalarını işaretlemeye devam edebilirsin.
        <a [routerLink]="hoursPath">Çalışabileceğin saatleri güncelle</a>
      </p>
    }
    @if (hub === plan && !teacherPlans()) { <app-student-plan-edit-steps /> }
    <router-outlet />`,
  styles: [`:host{display:block}
    .hub-tabs{display:flex;gap:4px;max-width:100%;overflow-x:auto;margin:0 0 var(--coaching-space-6);padding:4px;border-radius:var(--coaching-radius-pill);background:var(--ui-surface-muted);width:fit-content;scrollbar-width:none}
    .hub-tabs::-webkit-scrollbar{display:none}
    a{display:inline-flex;align-items:center;min-height:40px;padding:0 var(--coaching-space-4);border-radius:var(--coaching-radius-pill);color:var(--ui-text-muted);font-size:var(--coaching-text-body);font-weight:650;white-space:nowrap;text-decoration:none}
    a:hover{color:var(--ui-text)}
    a.is-active{background:var(--ui-surface);color:var(--ui-text);box-shadow:var(--coaching-card-shadow)}
    .hub-notice{margin:0 0 var(--coaching-space-5);padding:var(--coaching-space-3) var(--coaching-space-4);border-radius:var(--coaching-radius-md,12px);background:var(--ui-surface-muted);color:var(--ui-text);font-size:var(--coaching-text-body)}
    .hub-notice a{display:inline;min-height:0;padding:0;border-radius:0;color:inherit;font-weight:650;text-decoration:underline}
    @media (max-width:800px){.hub-tabs{width:auto;margin-bottom:var(--coaching-space-5)}}`]
})
export class StudentHubComponent {
  private readonly router = inject(Router);
  private readonly navigation = inject(CoachingStudentNavigationService);
  readonly hub = STUDENT_HUBS[inject(ActivatedRoute).snapshot.data['hub'] as keyof typeof STUDENT_HUBS];
  protected readonly plan = STUDENT_HUBS.plan;
  // Review of PR 122: a fees tab waits for the navigation answer (no flash).
  /** User decision 15.11.2026: with a teacher and without her permission the student sees the plan but does not edit it. */
  readonly teacherPlans = signal(false);
  protected readonly hoursPath = `${STUDENT_HUBS.plan.tabs[0].path}/hours`;
  readonly tabs = computed(() => this.hub.tabs.filter(tab => (!tab.needsFees || this.navigation.loaded() && this.navigation.showFees())
    && !(this.teacherPlans() && tab.path.endsWith('/plan/wizard'))));
  private readonly url = toSignal(this.router.events.pipe(filter(event => event instanceof NavigationEnd), map(() => this.router.url), startWith(this.router.url)), { initialValue: '' });

  constructor() {
    // A failed answer leaves the pages as they are; the server refuses a change it does not allow.
    if (this.hub === STUDENT_HUBS.plan)
      inject(CoachingPlanningPermissionService).own().subscribe({ next: state => this.teacherPlans.set(!state.studentMayPlan), error: () => undefined });
    // On a phone the tab strip scrolls sideways; keep the current tab in sight.
    const host = inject(ElementRef<HTMLElement>);
    afterRenderEffect(() => {
      this.url();
      const active = (host.nativeElement as HTMLElement).querySelector<HTMLElement>('.hub-tabs a.is-active');
      const strip = active?.parentElement;
      if (active && strip && strip.scrollWidth > strip.clientWidth) strip.scrollLeft = active.offsetLeft - (strip.clientWidth - active.offsetWidth) / 2;
    });
  }

  isActive(tab: StudentHubTab): boolean {
    const path = this.url().split(/[?#]/)[0];
    if (tab.match?.some(other => path === other || path.startsWith(other + '/'))) return true;
    return tab.exact ? path === tab.path : path === tab.path || path.startsWith(tab.path + '/');
  }
}
