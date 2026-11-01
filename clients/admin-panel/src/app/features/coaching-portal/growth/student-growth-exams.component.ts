import { ChangeDetectionStrategy, Component, signal, viewChild } from '@angular/core';
import { StudentExamResultsComponent } from '../pages/student-exam-results.component';
import { CsBottomSheetComponent, CsPageHeaderComponent } from '../ui';
import { StudentExamOverviewComponent } from './student-exam-overview.component';

/** T6 (31.10.2026): Gelişimim › Denemeler — the overview, and the entry form in a sheet for a new or an own result. */
@Component({
  selector: 'app-student-growth-exams',
  standalone: true,
  imports: [CsBottomSheetComponent, CsPageHeaderComponent, StudentExamOverviewComponent, StudentExamResultsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <cs-page-header title="Denemelerin" subtitle="Kendi girdiğin, öğretmeninin ve kurumunun denemeleri bir arada.">
      <button actions type="button" class="cs-growth-primary" (click)="openForm(null)">+ Deneme ekle</button>
    </cs-page-header>
    <app-student-exam-overview (add)="openForm(null)" (edit)="openForm($event)" />
    <cs-bottom-sheet class="cs-growth-sheet" [title]="editId() ? 'Denemeni düzenle' : 'Deneme ekle'" [open]="sheetOpen()" (openChange)="sheetChanged($event)">
      @if (sheetOpen()) { <app-student-exam-results [formOnly]="true" [editId]="editId()" (saved)="saved()" /> }
    </cs-bottom-sheet>`
})
export class StudentGrowthExamsComponent {
  private readonly overview = viewChild(StudentExamOverviewComponent);
  private readonly form = viewChild(StudentExamResultsComponent);
  private readonly sheet = viewChild(CsBottomSheetComponent);
  readonly sheetOpen = signal(false);
  readonly editId = signal<string | null>(null);

  openForm(id: string | null) { this.editId.set(id); this.sheetOpen.set(true); }

  async sheetChanged(open: boolean) {
    if (open) return;
    const form = this.form();
    // The sheet has already closed itself; keep the form on screen while it saves or while unsaved changes are confirmed.
    if (form && (form.busy() || form.dirty)) {
      this.sheet()?.open.set(true);
      if (!form.busy() && await form.canLeavePage()) this.close();
      return;
    }
    this.close();
  }

  private close() { this.sheetOpen.set(false); this.sheet()?.open.set(false); }

  saved() { this.overview()?.reload(); this.close(); }

  async canLeavePage() { return !this.sheetOpen() || (await (this.form()?.canLeavePage() ?? Promise.resolve(true))); }
}
