import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { StudyTask } from '../../../core/services/coaching-study-planning.service';
import { clock } from './weekly-calendar';

/**
 * T4 (31.10.2026): one study task as a single compact row on Planım. The row opens the task's details; the round
 * button completes it with one tap (planned minutes), and "Geri al" withdraws a completion made a moment ago.
 */
@Component({
  selector: 'app-plan-task-row',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="row" [class.done]="task().isCompleted" [class.late]="overdue()" [style.--lesson]="color() || 'var(--ui-brand)'">
    <button type="button" class="main" (click)="opened.emit()" [attr.aria-label]="task().title + ', ayrıntıları aç'">
      <span class="lesson">{{ task().lessonName || 'Çalışma' }}</span>
      <span class="title">{{ task().title }}</span>
      <span class="meta">{{ timeText() }} · {{ task().plannedMinutes }} dk @if (task().plannedQuestions) { · {{ task().plannedQuestions }} soru }</span>
      @if (overdue()) { <span class="flag late-flag">Gecikti</span> }
      @else if (partial()) { <span class="flag">Yarım · {{ task().actualMinutes }} dk</span> }
    </button>
    <div class="act">
      @if (task().isCompleted) {
        <span class="tick" role="img" aria-label="Tamamlandı">✓</span>
        @if (justCompleted()) { <button type="button" class="undo" (click)="undone.emit()" [disabled]="busy()">Geri al</button> }
      } @else if (canComplete()) {
        <button type="button" class="complete" (click)="completed.emit()" [disabled]="busy()" [attr.aria-label]="task().title + ', tamamlandı olarak işaretle'">✓</button>
      }
    </div>
  </div>`,
  styles: [`:host{display:block}
    .row{display:flex;align-items:center;gap:var(--coaching-space-3);min-height:64px;padding:var(--coaching-space-2) var(--coaching-space-3) var(--coaching-space-2) 0;border:1px solid var(--ui-border);border-left:5px solid var(--lesson);border-radius:14px;background:var(--ui-surface)}
    .row.done{background:var(--ui-surface-muted)}
    .row.done .title{color:var(--ui-text-muted);text-decoration:line-through;text-decoration-thickness:1px}
    .row.late{border-color:var(--ui-danger);border-left-color:var(--lesson)}
    .main{flex:1;min-width:0;display:grid;gap:2px;padding:var(--coaching-space-2) 0 var(--coaching-space-2) var(--coaching-space-3);border:0;background:transparent;color:var(--ui-text);font:inherit;text-align:left;cursor:pointer;border-radius:10px}
    .main:focus-visible,.complete:focus-visible,.undo:focus-visible{outline:3px solid var(--coaching-focus);outline-offset:2px}
    .lesson{color:var(--lesson);font-size:var(--coaching-text-sm);font-weight:700}
    .title{font-size:var(--coaching-text-body);font-weight:650;line-height:1.35;overflow-wrap:anywhere}
    .meta{color:var(--ui-text-muted);font-size:var(--coaching-text-sm)}
    .flag{justify-self:start;margin-top:4px;padding:2px 8px;border-radius:var(--coaching-radius-pill);background:var(--ui-warning-soft);color:var(--ui-text);font-size:12px;font-weight:700}
    .late-flag{background:var(--ui-danger-soft);color:var(--ui-danger)}
    .act{display:flex;flex-direction:column;align-items:center;gap:4px}
    .complete{width:44px;height:44px;border-radius:50%;border:2px solid var(--lesson);background:var(--ui-surface);color:var(--lesson);font-size:20px;font-weight:800;cursor:pointer}
    .complete:hover:not(:disabled){background:var(--lesson);color:var(--ui-surface)}
    .complete:disabled,.undo:disabled{opacity:.55;cursor:not-allowed}
    .tick{display:grid;place-items:center;width:44px;height:44px;border-radius:50%;background:var(--ui-success);color:var(--ui-brand-contrast);font-size:20px;font-weight:800}
    .undo{min-height:32px;padding:0 10px;border:0;border-radius:var(--coaching-radius-pill);background:transparent;color:var(--ui-brand-text);font:inherit;font-size:var(--coaching-text-sm);font-weight:700;text-decoration:underline;cursor:pointer}
    @media (prefers-reduced-motion:no-preference){.complete{transition:background .15s ease,color .15s ease}}`]
})
export class PlanTaskRowComponent {
  readonly task = input.required<StudyTask>();
  readonly color = input<string | undefined>(undefined);
  readonly overdue = input(false);
  readonly canComplete = input(false);
  readonly justCompleted = input(false);
  readonly busy = input(false);
  readonly opened = output<void>();
  readonly completed = output<void>();
  readonly undone = output<void>();

  readonly partial = computed(() => !this.task().isCompleted && this.task().actualMinutes != null);
  readonly timeText = computed(() => {
    const task = this.task();
    return task.plannedStartMinute == null ? 'Gün içinde' : `${clock(task.plannedStartMinute)}–${clock(task.plannedStartMinute + task.plannedMinutes)}`;
  });
}
