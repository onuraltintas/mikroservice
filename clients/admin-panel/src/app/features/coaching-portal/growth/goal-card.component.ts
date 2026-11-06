import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';
import { homeGoalCard } from './growth-model';

/**
 * Paket C (#4): the goal card's states and wording for the parent's "…'in hedefi" and the teacher's view of a student — the
 * same as the student's "Bugün" card, in the third person (`childName`).
 */
@Component({
  selector: 'app-goal-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (card(); as c) {
      <div class="goal-card" data-testid="goal-card" [attr.data-state]="c.state" [attr.data-status]="c.status">
        @if (title()) { <p class="goal-card-kicker">{{ title() }}</p> }
        @if (c.state === 'none' && link()) {
          <!-- The student's own page: "Hedefini belirle" opens the wizard's target question. Parents and staff get no link. -->
          <a [href]="targetUrl" (click)="open($event)"><strong>{{ c.headline }}</strong></a>
        } @else { <strong>{{ c.headline }}</strong> }
        @if (c.targetName) { <span class="goal-card-name">{{ c.targetName }}</span> }
        @if (c.detail) { <small>{{ c.detail }}</small> }
        @if (c.label) { <small class="goal-card-label" data-testid="goal-card-label">{{ c.label }}</small> }
        @if (c.note) { <small>{{ c.note }}</small> }
        @for (line of c.secondary; track line) { <small class="goal-card-secondary">{{ line }}</small> }
      </div>
    }`,
  styles: [`
    .goal-card { display: grid; gap: 4px; padding: 12px 16px; border-left: 4px solid var(--ui-accent); border-radius: 0 10px 10px 0; background: var(--ui-surface); color: var(--ui-text); }
    .goal-card[data-state='none'] { border-left-color: var(--ui-border-strong); }
    .goal-card[data-status='reached'] { border-left-color: var(--ui-success); }
    .goal-card-kicker { margin: 0; font-size: 13px; color: var(--ui-text-muted); }
    strong { font-size: 17px; font-weight: 800; }
    a { color: var(--ui-brand-text); }
    .goal-card-name { font-weight: 700; }
    small { font-size: 14px; color: var(--ui-text-muted); }
    .goal-card-label { justify-self: start; padding: 0 8px; border-radius: 999px; background: var(--ui-surface-muted); }
  `]
})
export class GoalCardComponent {
  readonly estimate = input.required<ScoreEstimate>();
  /** The student's name for a parent, "Öğrenci" for staff; null speaks to the student. */
  readonly childName = input<string | null>(null);
  readonly title = input<string | null>(null);
  /** The student's own view: "Hedefini belirle" links to the target question. */
  readonly link = input(false);
  readonly card = computed(() => homeGoalCard(this.estimate(), this.childName()));
  readonly targetUrl = '/coaching-portal/welcome?step=target';
  private readonly router = inject(Router, { optional: true });
  /** In-app navigation when the router is there (the card is also used where there is none). */
  open(event: Event) { if (this.router) { event.preventDefault(); void this.router.navigateByUrl(this.targetUrl); } }
}
