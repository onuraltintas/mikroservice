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
import { MatChipsModule } from '@angular/material/chips';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDialog, MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { BaseComponent } from '../../../core/components/base.component';
import {
  CoachingService, Assignment, CoachingRelationship, Subject
} from '../../../core/services/coaching.service';

// ── Yeni Ödev Dialog ──────────────────────────────────────────────────────────
@Component({
  selector: 'app-new-coach-assignment-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule],
  template: `
    <h2 mat-dialog-title>Ödev Ver</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:380px;padding-top:8px">
      <mat-form-field appearance="outline">
        <mat-label>Öğrenci</mat-label>
        <mat-select formControlName="studentId">
          <mat-option *ngFor="let r of data.relationships" [value]="r.studentId">
            {{ r.studentName || (r.studentId.slice(0,8) + '...') }}
          </mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Ödev Başlığı</mat-label>
        <input matInput formControlName="title">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Ders</mat-label>
        <mat-select formControlName="subjectId">
          <mat-option [value]="null">-- Seçiniz --</mat-option>
          <mat-option *ngFor="let s of data.subjects" [value]="s.id">{{ s.name }}</mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Son Tarih</mat-label>
        <input matInput [matDatepicker]="dp" formControlName="dueDate">
        <mat-datepicker-toggle matIconSuffix [for]="dp"></mat-datepicker-toggle>
        <mat-datepicker #dp></mat-datepicker>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Hedef Soru Sayısı</mat-label>
        <input matInput type="number" formControlName="targetQuestions">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Hedef Süre (dakika)</mat-label>
        <input matInput type="number" formControlName="targetMinutes">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Açıklama</mat-label>
        <textarea matInput formControlName="description" rows="2"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Ver</button>
    </mat-dialog-actions>
  `
})
export class NewCoachAssignmentDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<NewCoachAssignmentDialogComponent>);
  @Inject(MAT_DIALOG_DATA) data: { relationships: CoachingRelationship[]; subjects: Subject[] } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      studentId: [null, Validators.required],
      title: ['', Validators.required],
      subjectId: [null],
      dueDate: [null, Validators.required],
      targetQuestions: [null],
      targetMinutes: [null],
      description: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      studentId: v.studentId,
      title: v.title,
      subjectId: v.subjectId || undefined,
      dueDate: (v.dueDate as Date).toISOString(),
      targetQuestions: v.targetQuestions || undefined,
      targetMinutes: v.targetMinutes || undefined,
      description: v.description || undefined
    });
  }
}

// ── Red Geri Bildirim Dialog ───────────────────────────────────────────────────
@Component({
  selector: 'app-reject-assignment-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Ödevi Reddet</h2>
    <mat-dialog-content style="min-width:320px;padding-top:8px">
      <mat-form-field appearance="outline" style="width:100%">
        <mat-label>Geri Bildirim (isteğe bağlı)</mat-label>
        <textarea matInput [(ngModel)]="feedback" rows="3"
          placeholder="Öğrenciye ne yapması gerektiğini açıklayın..."></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="warn" (click)="submit()">Reddet</button>
    </mat-dialog-actions>
  `
})
export class RejectAssignmentDialogComponent {
  feedback = '';
  private ref = inject(MatDialogRef<RejectAssignmentDialogComponent>);
  submit(): void { this.ref.close(this.feedback || undefined); }
}

// ── Ana Component ─────────────────────────────────────────────────────────────
@Component({
  selector: 'app-coach-assignments',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatTooltipModule, MatChipsModule, MatBadgeModule
  ],
  templateUrl: './coach-assignments.component.html'
})
export class CoachAssignmentsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);
  private dialog = inject(MatDialog);

  assignments: Assignment[] = [];
  relationships: CoachingRelationship[] = [];
  subjects: Subject[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterStatus = '';
  pendingReviewMode = false;
  pendingReviewCount = 0;

  displayedColumns = ['student', 'title', 'dueDate', 'status', 'review', 'actions'];

  statusOptions = [
    { value: '', label: 'Tüm Durumlar' },
    { value: 'Pending', label: 'Bekliyor' },
    { value: 'Completed', label: 'Tamamlandı' },
    { value: 'PartiallyCompleted', label: 'Kısmen' },
    { value: 'Overdue', label: 'Gecikti' },
    { value: 'Cancelled', label: 'İptal' }
  ];

  statusLabels: Record<string, string> = {
    Pending: 'Bekliyor', Completed: 'Tamamlandı', PartiallyCompleted: 'Kısmen',
    Overdue: 'Gecikti', Cancelled: 'İptal'
  };

  ngOnInit(): void {
    this.loadRelationships();
    this.load();
    this.loadPendingReviewCount();
  }

  loadRelationships(): void {
    this.service.getRelationships({ status: 'Active', pageSize: 100 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: r => {
          this.relationships = r.items;
          this.loadSubjects();
        }
      });
  }

  loadSubjects(): void {
    this.service.getSubjects()
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: s => this.subjects = s });
  }

  loadPendingReviewCount(): void {
    this.service.getAssignments({ pendingReview: true, pageSize: 1 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => this.pendingReviewCount = r.total });
  }

  load(): void {
    this.loading.set(true);
    this.service.getAssignments({
      status: this.filterStatus || undefined,
      pendingReview: this.pendingReviewMode || undefined,
      page: this.page,
      pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.assignments = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Ödevler yüklenemedi')
      });
  }

  togglePendingReview(): void {
    this.pendingReviewMode = !this.pendingReviewMode;
    this.filterStatus = '';
    this.page = 1;
    this.load();
  }

  approveAssignment(a: Assignment): void {
    this.service.reviewAssignment(a.id, { isApproved: true })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.handleSuccess('Ödev onaylandı'); this.load(); this.loadPendingReviewCount(); },
        error: e => this.handleError(e)
      });
  }

  rejectAssignment(a: Assignment): void {
    const ref = this.dialog.open(RejectAssignmentDialogComponent, { width: '360px' });
    ref.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(feedback => {
      if (feedback === undefined && feedback !== '') return; // iptal
      // null veya string her ikisi de kabul
      this.service.reviewAssignment(a.id, { isApproved: false, coachFeedback: feedback })
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Ödev reddedildi'); this.load(); this.loadPendingReviewCount(); },
          error: e => this.handleError(e)
        });
    });
  }

  openNewAssignment(): void {
    const ref = this.dialog.open(NewCoachAssignmentDialogComponent, {
      data: { relationships: this.relationships, subjects: this.subjects }, width: '440px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createAssignment(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Ödev verildi'); this.load(); },
          error: e => this.handleError(e)
        });
    });
  }

  async cancelAssignment(a: Assignment): Promise<void> {
    const ok = await this.confirm('Bu ödevi iptal etmek istiyor musunuz?');
    if (!ok) return;
    this.service.updateAssignment(a.id, { status: 'Cancelled' })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.handleSuccess('Ödev iptal edildi'); this.load(); },
        error: e => this.handleError(e)
      });
  }

  needsReview(a: Assignment): boolean {
    return (a.status === 'Completed' || a.status === 'PartiallyCompleted')
      && a.isApprovedByCoach == null;
  }

  isOverdue(dueDate: string): boolean {
    return new Date(dueDate) < new Date();
  }

  getStudentLabel(assignment: any): string {
    return assignment.studentName || (assignment.studentId.slice(0, 8) + '...');
  }

  onFilter(): void { this.pendingReviewMode = false; this.page = 1; this.load(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
}
