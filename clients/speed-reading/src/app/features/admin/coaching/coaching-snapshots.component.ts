import { Component, OnInit, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { finalize, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { BaseComponent } from '../../../core/components/base.component';
import { CoachingService, StudentSnapshot } from '../../../core/services/coaching.service';

// ── Koç Notu Dialog ───────────────────────────────────────────────────────────
@Component({
  selector: 'app-snapshot-note-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Özet Yenile — {{ data.label }}</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:360px;padding-top:8px">
      <mat-form-field appearance="outline">
        <mat-label>Koç Notu (opsiyonel)</mat-label>
        <textarea matInput formControlName="coachNote" rows="3"
          placeholder="Bu dönem için koç değerlendirmesi..."></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" (click)="submit()">Yenile</button>
    </mat-dialog-actions>
  `
})
export class SnapshotNoteDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<SnapshotNoteDialogComponent>);
  data: { label: string } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({ coachNote: [''] });
  }

  submit(): void {
    const v = this.form.value;
    this.ref.close({ coachNote: v.coachNote || undefined });
  }
}

@Component({
  selector: 'app-coaching-snapshots',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatTooltipModule
  ],
  templateUrl: './coaching-snapshots.component.html'
})
export class CoachingSnapshotsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);
  private dialog = inject(MatDialog);

  records: StudentSnapshot[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterType = '';

  displayedColumns = ['studentName', 'period', 'type', 'studyMinutes', 'questions', 'goals', 'assignments', 'exams', 'note', 'actions'];

  typeOptions = [
    { value: '', label: 'Tüm Tipler' },
    { value: 'Weekly', label: 'Haftalık' },
    { value: 'Monthly', label: 'Aylık' }
  ];

  typeLabels: Record<string, string> = { Weekly: 'Haftalık', Monthly: 'Aylık' };

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getSnapshots({
      type: this.filterType || undefined,
      page: this.page,
      pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.records = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Özetler yüklenemedi')
      });
  }

  goalRate(r: StudentSnapshot): string {
    if (!r.goalsTotal) return '—';
    return `${r.goalsCompleted}/${r.goalsTotal}`;
  }

  assignmentRate(r: StudentSnapshot): string {
    if (!r.assignmentsTotal) return '—';
    return `${r.assignmentsCompleted}/${r.assignmentsTotal}`;
  }

  generateSnapshot(r: StudentSnapshot): void {
    const label = this.typeLabels[r.snapshotType] ?? r.snapshotType;
    const ref = this.dialog.open(SnapshotNoteDialogComponent, {
      data: { label: `${label}` }, width: '400px'
    });
    ref.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(data => {
      if (data === undefined) return; // dialog iptal edildi
      this.service.generateSnapshot({
        studentId: r.studentId,
        snapshotType: r.snapshotType,
        periodStart: r.periodStart,
        coachNote: data?.coachNote
      })
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess(`${label} özeti güncellendi`); this.load(); },
          error: e => this.handleError(e)
        });
    });
  }

  onFilter(): void { this.page = 1; this.load(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
}
