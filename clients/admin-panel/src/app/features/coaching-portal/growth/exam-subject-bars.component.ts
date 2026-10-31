import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { StudentExamSubject } from '../../../core/services/coaching-student-exam-overview.service';
import { subjectBars } from './growth-model';

/** T6: average net per subject over the last three exams of a scope; the weakest subject is called out in words too. */
@Component({
  selector: 'cs-exam-subject-bars',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (weakest(); as weak) { <p class="hint">En çok zorlandığın ders <strong>{{ weak.name }}</strong>: {{ weak.questionCount }} soruda ortalama {{ weak.averageNet }} net.</p> }
    <ul>
      @for (bar of bars(); track bar.code) {
        <li [attr.data-weakest]="bar.weakest" [style.--tone]="'var(--coaching-lesson-' + bar.tone + ')'" [style.--tone-text]="'var(--coaching-lesson-text-' + bar.tone + ')'">
          <span class="name">{{ bar.name }}@if (bar.weakest) { <em>odak</em> }</span>
          <span class="track" role="progressbar" [attr.aria-label]="bar.name + ' başarı oranı'" aria-valuemin="0" aria-valuemax="100" [attr.aria-valuenow]="bar.percent"
            [attr.aria-valuetext]="bar.averageNet + ' net, ' + bar.questionCount + ' sorudan, yüzde ' + bar.percent"><span class="fill" [style.width.%]="bar.percent"></span></span>
          <span class="net">{{ bar.averageNet }}<small>/{{ bar.questionCount }}</small></span>
        </li>
      }
    </ul>`,
  styles: [`:host{display:block}
    .hint{margin:0 0 var(--coaching-space-3);font-size:var(--coaching-text-body);color:var(--ui-text-muted)}
    .hint strong{color:var(--ui-text)}
    ul{display:grid;gap:var(--coaching-space-2);margin:0;padding:0;list-style:none}
    li{display:grid;grid-template-columns:minmax(88px,30%) minmax(0,1fr) 64px;align-items:center;gap:var(--coaching-space-3);padding:6px var(--coaching-space-2);border-radius:var(--coaching-radius-control)}
    li[data-weakest=true]{background:var(--ui-warning-soft);outline:1px solid var(--ui-warning)}
    .name{display:flex;align-items:center;gap:6px;min-width:0;font-size:var(--coaching-text-sm);font-weight:700;color:var(--ui-text);overflow-wrap:anywhere}
    em{font-style:normal;padding:0 6px;border-radius:var(--coaching-radius-pill);background:var(--ui-surface);color:var(--ui-warning);font-size:11px;font-weight:800}
    .track{display:block;height:12px;border-radius:var(--coaching-radius-pill);background:var(--ui-surface-muted);overflow:hidden}
    .fill{display:block;height:100%;border-radius:inherit;background:var(--tone-text)}
    .net{text-align:right;font-weight:800;font-size:var(--coaching-text-body);color:var(--ui-text);font-variant-numeric:tabular-nums}
    small{font-size:11px;font-weight:600;color:var(--ui-text-muted)}`]
})
export class ExamSubjectBarsComponent {
  readonly subjects = input.required<StudentExamSubject[]>();
  readonly examCode = input.required<string>();
  readonly bars = computed(() => subjectBars(this.subjects(), this.examCode()));
  readonly weakest = computed(() => this.bars().find(x => x.weakest) ?? null);
}
