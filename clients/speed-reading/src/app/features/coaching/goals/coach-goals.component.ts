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
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { BaseComponent } from '../../../core/components/base.component';
import {
  CoachingService, Goal, CoachingRelationship
} from '../../../core/services/coaching.service';

// ── Hedef İlerleme Check-in Dialog ────────────────────────────────────────────
@Component({
  selector: 'app-coach-goal-checkin',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>İlerleme Güncelle — {{ data.title }}</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:320px;padding-top:8px">
      <p style="margin:0;color:#666">Mevcut: %{{ data.current }}</p>
      <mat-form-field appearance="outline">
        <mat-label>Yeni Tamamlanma Yüzdesi (0-100)</mat-label>
        <input matInput type="number" formControlName="newPercentage" min="0" max="100">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Not (opsiyonel)</mat-label>
        <textarea matInput formControlName="note" rows="2"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Güncelle</button>
    </mat-dialog-actions>
  `
})
export class GoalCheckinDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<GoalCheckinDialogComponent>);
  data: { current: number; title: string } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      newPercentage: [this.data.current, [Validators.required, Validators.min(0), Validators.max(100)]],
      note: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({ newPercentage: v.newPercentage, note: v.note || undefined });
  }
}

// ── Yeni Hedef Dialog ─────────────────────────────────────────────────────────
@Component({
  selector: 'app-new-coach-goal-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatDatepickerModule, MatNativeDateModule],
  template: `
    <h2 mat-dialog-title>Yeni Hedef</h2>
    <mat-dialog-content [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;min-width:380px;padding-top:8px">
      <mat-form-field appearance="outline">
        <mat-label>Öğrenci</mat-label>
        <mat-select formControlName="studentId">
          <mat-option *ngFor="let r of data.relationships" [value]="r.studentId">
            {{ r.studentName || (r.studentId | slice:0:8) + '...' }}
          </mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Başlık</mat-label>
        <input matInput formControlName="title">
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Seviye</mat-label>
        <mat-select formControlName="level">
          <mat-option value="Yearly">Yıllık</mat-option>
          <mat-option value="Monthly">Aylık</mat-option>
          <mat-option value="Weekly">Haftalık</mat-option>
          <mat-option value="Daily">Günlük</mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Hedef Tarih</mat-label>
        <input matInput [matDatepicker]="dp" formControlName="targetDate">
        <mat-datepicker-toggle matIconSuffix [for]="dp"></mat-datepicker-toggle>
        <mat-datepicker #dp></mat-datepicker>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Başarı Kriteri</mat-label>
        <textarea matInput formControlName="successCriteria" rows="2" placeholder="Örn: Matematik netim 35 olacak"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>İptal</button>
      <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="submit()">Kaydet</button>
    </mat-dialog-actions>
  `
})
export class NewCoachGoalDialogComponent {
  form: FormGroup;
  private fb = inject(FormBuilder);
  private ref = inject(MatDialogRef<NewCoachGoalDialogComponent>);
  @Inject(MAT_DIALOG_DATA) data: { relationships: CoachingRelationship[] } = inject(MAT_DIALOG_DATA);

  constructor() {
    this.form = this.fb.group({
      studentId: [null, Validators.required],
      title: ['', Validators.required],
      level: ['Monthly', Validators.required],
      targetDate: [null, Validators.required],
      successCriteria: ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    this.ref.close({
      studentId: v.studentId,
      title: v.title,
      level: v.level,
      targetDate: (v.targetDate as Date).toISOString().split('T')[0],
      successCriteria: v.successCriteria || undefined
    });
  }
}

// ── Ana Component ─────────────────────────────────────────────────────────────
@Component({
  selector: 'app-coach-goals',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatProgressBarModule, MatTooltipModule
  ],
  templateUrl: './coach-goals.component.html'
})
export class CoachGoalsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);
  private dialog = inject(MatDialog);

  goals: Goal[] = [];
  relationships: CoachingRelationship[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterLevel = '';
  filterStatus = 'Active';

  displayedColumns = ['student', 'title', 'level', 'targetDate', 'progress', 'status', 'actions', 'delete'];

  levelOptions = [
    { value: '', label: 'Tüm Seviyeler' },
    { value: 'Yearly', label: 'Yıllık' },
    { value: 'Monthly', label: 'Aylık' },
    { value: 'Weekly', label: 'Haftalık' },
    { value: 'Daily', label: 'Günlük' }
  ];

  statusOptions = [
    { value: '', label: 'Tüm Durumlar' },
    { value: 'Active', label: 'Aktif' },
    { value: 'Completed', label: 'Tamamlandı' },
    { value: 'Failed', label: 'Başarısız' },
    { value: 'Paused', label: 'Duraklıyor' }
  ];

  levelLabels: Record<string, string> = { Yearly: 'Yıllık', Monthly: 'Aylık', Weekly: 'Haftalık', Daily: 'Günlük' };
  statusLabels: Record<string, string> = { Active: 'Aktif', Completed: 'Tamamlandı', Failed: 'Başarısız', Paused: 'Duraklıyor', Cancelled: 'İptal' };

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
    this.service.getGoals({
      level: this.filterLevel || undefined,
      status: this.filterStatus || undefined,
      page: this.page,
      pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.goals = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Hedefler yüklenemedi')
      });
  }

  openNewGoal(): void {
    const ref = this.dialog.open(NewCoachGoalDialogComponent, {
      data: { relationships: this.relationships }, width: '440px'
    });
    ref.afterClosed().subscribe(data => {
      if (!data) return;
      this.service.createGoal(data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('Hedef oluşturuldu'); this.load(); },
          error: e => this.handleError(e)
        });
    });
  }

  checkIn(goal: Goal): void {
    const ref = this.dialog.open(GoalCheckinDialogComponent, {
      data: { current: goal.completionPercentage, title: goal.title }, width: '380px'
    });
    ref.afterClosed().pipe(takeUntil(this.destroy$)).subscribe(data => {
      if (!data) return;
      this.service.checkInGoal(goal.id, data)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.handleSuccess('İlerleme güncellendi'); this.load(); },
          error: e => this.handleError(e)
        });
    });
  }

  async deleteGoal(goal: Goal): Promise<void> {
    const ok = await this.confirm(`"${goal.title}" hedefini silmek istiyor musunuz?`);
    if (!ok) return;
    this.service.deleteGoal(goal.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.handleSuccess('Hedef silindi'); this.load(); },
        error: e => this.handleError(e)
      });
  }

  getStudentLabel(studentId: string): string {
    const rel = this.relationships.find(r => r.studentId === studentId);
    return rel?.studentName || studentId.slice(0, 8) + '...';
  }

  onFilter(): void { this.page = 1; this.load(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
}
