import { Component, OnInit, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
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
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { BaseComponent } from '../../../core/components/base.component';
import {
  CoachingService, ExamResult, CoachingRelationship
} from '../../../core/services/coaching.service';

// ── Yeni Sınav Sonucu Dialog ──────────────────────────────────────────────────
@Component({
  selector: 'app-new-coach-exam-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule],
  template: `
    <h2 mat-dialog-title>Sınav Sonucu Gir</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:380px;padding-top:8px">
      <mat-form-field appearance="outline">
        <mat-label>Öğrenci</mat-label>
        <mat-select formControlName="studentId">
          <mat-option *ngFor="let r of data.relationships" [value]="r.studentId">
            {{ r.studentId | slice:0:8 }}...
          </mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Sınav Adı</mat-label>
        <input matInput formControlName="examName" placeholder="Örn: 5. TYT Denemesi">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Sınav Tarihi</mat-label>
        <input matInput [matDatepicker]="dp" formControlName="examDate">
        <mat-datepicker-toggle matIconSuffix [for]="dp"></mat-datepicker-toggle>
        <mat-datepicker #dp></mat-datepicker>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Sınav Tipi</mat-label>
        <mat-select formControlName="examType">
          <mat-option value="YKS_TYT">YKS TYT</mat-option>
          <mat-option value="YKS_AYT_Sayisal">AYT Sayısal</mat-option>
          <mat-option value="YKS_AYT_EsitAgirlik">AYT Eşit Ağırlık</mat-option>
          <mat-option value="YKS_AYT_Sozel">AYT Sözel</mat-option>
          <mat-option value="LGS">LGS</mat-option>
          <mat-option value="General">Genel</mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Toplam Net</mat-label>
        <input matInput type="number" step="0.25" formControlName="totalNet">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Notlar</mat-label>
        <textarea matInput formControlName="notes" rows="2"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Kaydet</button>
    </mat-dialog-actions>
  `
})
export class NewCoachExamDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<NewCoachExamDialogComponent>);
  @Inject(MAT_DIALOG_DATA) data: { relationships: CoachingRelationship[] } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      studentId: [null, Validators.required],
      examName: ['', Validators.required],
      examDate: [null, Validators.required],
      examType: ['YKS_TYT', Validators.required],
      totalNet: [null],
      notes: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      studentId: v.studentId,
      examName: v.examName,
      examDate: (v.examDate as Date).toISOString().split('T')[0],
      examType: v.examType,
      totalNet: v.totalNet || undefined,
      notes: v.notes || undefined
    });
  }
}

// ── Ana Component ─────────────────────────────────────────────────────────────
@Component({
  selector: 'app-coach-exam-results',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatTooltipModule
  ],
  templateUrl: './coach-exam-results.component.html'
})
export class CoachExamResultsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);
  private dialog = inject(MatDialog);

  examResults: ExamResult[] = [];
  relationships: CoachingRelationship[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterExamType = '';

  displayedColumns = ['student', 'examName', 'examDate', 'examType', 'totalNet', 'actions'];

  examTypeOptions = [
    { value: '', label: 'Tüm Sınavlar' },
    { value: 'YKS_TYT', label: 'YKS TYT' },
    { value: 'YKS_AYT_Sayisal', label: 'AYT Sayısal' },
    { value: 'YKS_AYT_EsitAgirlik', label: 'AYT Eşit Ağırlık' },
    { value: 'YKS_AYT_Sozel', label: 'AYT Sözel' },
    { value: 'LGS', label: 'LGS' },
    { value: 'General', label: 'Genel' }
  ];

  examTypeLabels: Record<string, string> = {
    YKS_TYT: 'TYT', YKS_AYT_Sayisal: 'AYT Say.', YKS_AYT_EsitAgirlik: 'AYT E.A.',
    YKS_AYT_Sozel: 'AYT Söz.', LGS: 'LGS', General: 'Genel'
  };

  ngOnInit(): void {
    this.loadRelationships();
    this.load();
  }

  loadRelationships(): void {
    this.service.getRelationships({ status: 'Active', pageSize: 100 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => this.relationships = r.items });
  }

  load(): void {
    this.loading.set(true);
    this.service.getExamResults({
      examType: this.filterExamType || undefined,
      page: this.page,
      pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.examResults = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Sınav sonuçları yüklenemedi')
      });
  }

  openNewExam(): void {
    const ref = this.dialog.open(NewCoachExamDialogComponent, {
      data: { relationships: this.relationships }, width: '440px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createExamResult(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Sınav sonucu kaydedildi'); this.load(); },
          error: e => this.handleError(e)
        });
    });
  }

  async deleteResult(e: ExamResult): Promise<void> {
    const ok = await this.confirm('Bu sınav sonucunu silmek istiyor musunuz?');
    if (!ok) return;
    this.service.deleteExamResult(e.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.handleSuccess('Sınav sonucu silindi'); this.load(); },
        error: err => this.handleError(err)
      });
  }

  getStudentLabel(studentId: string): string {
    return studentId.slice(0, 8) + '...';
  }

  onFilter(): void { this.page = 1; this.load(); }
  onPage(ev: PageEvent): void { this.page = ev.pageIndex + 1; this.pageSize = ev.pageSize; this.load(); }
}
