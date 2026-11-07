import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { Subscription, catchError, filter, forkJoin, map, of, startWith } from 'rxjs';
import { CoachingWeeklyDraftService } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';

const root = '/coaching-portal/plan';
const STEPS = [
  { label: 'Ders düzenin', path: `${root}/weekly`, hint: 'Okul ve kurs derslerini haftaya yerleştir.' },
  { label: 'Boş saatlerin', path: `${root}/hours`, hint: 'Çalışabileceğin saatleri işaretle.' },
  { label: 'Planını oluştur', path: `${root}/wizard`, also: [`${root}/preview`], hint: 'Tarihleri seç, planını gözden geçir ve yayımla.' }
] as const;

/**
 * T4 (31.10.2026): "Planı düzenle" is one flow in three steps over the existing pages — weekly lessons, free hours,
 * then the plan wizard (or the weekly preview). It only navigates; each page keeps its own saving and validation.
 * Paket F #37: a step is shown as done only when its data exists (lessons in the weekly draft, saved free hours), read again on
 * every step change; an unknown state (loading, failed) is not done. The last step has no "done" state of its own.
 */
@Component({
  selector: 'app-student-plan-edit-steps',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (current() >= 0) {
    <nav class="edit-steps" aria-label="Planı düzenleme adımları">
      <ol class="steps">
        @for (step of steps; track step.path; let i = $index) {
          <li [class.done]="done()[i]" [class.now]="i === current()">
            <a [routerLink]="step.path" [attr.aria-current]="i === current() ? 'step' : null" [attr.aria-label]="done()[i] ? (i + 1) + ' ' + step.label + ' (tamamlandı)' : null"><span class="n" aria-hidden="true">{{ i + 1 }}</span> {{ step.label }}</a>
          </li>
        }
      </ol>
      <div class="foot">
        <p class="hint">{{ steps[current()].hint }}</p>
        <div class="moves">
          @if (current() > 0) { <a class="prev" [routerLink]="steps[current() - 1].path">Geri</a> }
          @if (current() < steps.length - 1) { <a class="next" [routerLink]="steps[current() + 1].path">Sonraki adım</a> }
          @else { <a class="next" routerLink="/coaching-portal/plan">Planıma dön</a> }
        </div>
      </div>
    </nav>
  }`,
  styles: [`:host{display:block}
    .edit-steps{margin:0 0 var(--coaching-space-6);padding:var(--coaching-space-4);border:1px solid var(--ui-border);border-radius:var(--coaching-radius-card);background:var(--ui-surface)}
    .steps{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--coaching-space-2);margin:0;padding:0;list-style:none;counter-reset:none}
    li{position:relative}
    li a{display:flex;align-items:center;gap:var(--coaching-space-2);min-height:44px;padding:0 var(--coaching-space-3);border-radius:var(--coaching-radius-control);color:var(--ui-text-muted);font-size:var(--coaching-text-sm);font-weight:650;text-decoration:none}
    li a:focus-visible,.moves a:focus-visible{outline:3px solid var(--coaching-focus);outline-offset:2px}
    .n{display:inline-grid;place-items:center;flex:none;width:26px;height:26px;border-radius:50%;border:2px solid var(--ui-border);font-weight:800}
    li.done .n{border-color:var(--ui-success);background:var(--ui-success);color:var(--ui-brand-contrast)}
    li.now a{background:var(--ui-brand-soft);color:var(--ui-brand-text)}
    li.now .n{border-color:var(--ui-brand);background:var(--ui-brand);color:var(--ui-brand-contrast)}
    .foot{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:var(--coaching-space-3);margin-top:var(--coaching-space-3)}
    .hint{margin:0;color:var(--ui-text-muted);font-size:var(--coaching-text-sm)}
    .moves{display:flex;gap:var(--coaching-space-2)}
    .moves a{display:inline-flex;align-items:center;min-height:44px;padding:0 var(--coaching-space-4);border-radius:var(--coaching-radius-pill);font-weight:700;text-decoration:none}
    .prev{border:1px solid var(--ui-border);color:var(--ui-text)}
    .next{background:var(--ui-brand);color:var(--ui-brand-contrast)}
    @media (max-width:560px){.steps{grid-template-columns:1fr 1fr 1fr}li a{flex-direction:column;justify-content:center;gap:4px;min-height:60px;padding:var(--coaching-space-2);text-align:center;font-size:12px}.foot{flex-direction:column;align-items:stretch}.moves a{flex:1;justify-content:center}}`]
})
export class StudentPlanEditStepsComponent {
  readonly steps = STEPS;
  private readonly router = inject(Router);
  private readonly url = toSignal(this.router.events.pipe(filter(e => e instanceof NavigationEnd), map(() => this.router.url), startWith(this.router.url)), { initialValue: '' });
  readonly current = computed(() => {
    const path = this.url().split(/[?#]/)[0];
    return STEPS.findIndex(step => path === step.path || ('also' in step && step.also.some(other => path === other)));
  });
  private readonly weekly = inject(CoachingWeeklyDraftService);
  private readonly planning = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly state = signal<{ lessons: boolean; hours: boolean }>({ lessons: false, hours: false });
  readonly done = computed(() => [this.state().lessons, this.state().hours, false]);
  private request?: Subscription;

  constructor() {
    // Read again on each step change, so what was just saved on the previous step counts.
    effect(() => { if (this.current() >= 0) untracked(() => this.load()); });
    this.destroyRef.onDestroy(() => this.request?.unsubscribe());
  }

  private load() {
    this.request?.unsubscribe();
    this.request = forkJoin({
      lessons: this.weekly.get().pipe(map(page => !!page.draft?.slots.length), catchError(() => of(false))),
      hours: this.planning.getAvailability().pipe(map(availability => !!availability?.windows.length), catchError(() => of(false)))
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(state => this.state.set(state));
  }
}
