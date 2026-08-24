import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize, takeUntil } from 'rxjs/operators';
import { BaseComponent } from '../../../../../core/components/base.component';
import { CoachingApiService, CoachingSubject } from '../../../../../core/services/coaching-api.service';

@Component({
  selector: 'app-coaching-assignment-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="cd-header">
      <mat-icon>assignment</mat-icon>
      <h2>Ödev Ver</h2>
      <button mat-icon-button (click)="cancel()"><mat-icon>close</mat-icon></button>
    </div>
    <mat-dialog-content>
      <form [formGroup]="form" class="cd-form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Ödev Başlığı</mat-label>
          <input matInput formControlName="title" placeholder="Örn: TYT Matematik Konu Tekrarı">
          <mat-error>Zorunlu alan</mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Açıklama</mat-label>
          <textarea matInput formControlName="description" rows="2" placeholder="İsteğe bağlı..."></textarea>
        </mat-form-field>
        <div class="row-2">
          <mat-form-field appearance="outline" class="full">
            <mat-label>Ders</mat-label>
            <mat-select formControlName="subjectId" (selectionChange)="onSubjectChange($event.value)">
              <mat-option value="">-- Seç --</mat-option>
              <mat-option *ngFor="let s of subjects" [value]="s.id">{{ s.name }}</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full">
            <mat-label>Konu</mat-label>
            <mat-select formControlName="topicId">
              <mat-option value="">-- Seç --</mat-option>
              <mat-option *ngFor="let t of topics" [value]="t.id">{{ t.name }}</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Son Teslim Tarihi</mat-label>
          <input matInput type="date" formControlName="dueDate">
          <mat-error>Zorunlu alan</mat-error>
        </mat-form-field>
        <div class="row-2">
          <mat-form-field appearance="outline" class="full">
            <mat-label>Hedef Soru Sayısı</mat-label>
            <input matInput type="number" formControlName="targetQuestions" min="1" placeholder="İsteğe bağlı">
            <mat-icon matSuffix>quiz</mat-icon>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full">
            <mat-label>Hedef Süre (dk)</mat-label>
            <input matInput type="number" formControlName="targetMinutes" min="1" placeholder="İsteğe bağlı">
            <mat-icon matSuffix>timer</mat-icon>
          </mat-form-field>
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button (click)="cancel()">İptal</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="loading() || form.invalid">
        <mat-spinner *ngIf="loading()" diameter="16" style="display:inline-flex;margin-right:6px"></mat-spinner>
        Ödev Ver
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .cd-header { display:flex; align-items:center; gap:10px; padding:16px 20px 0;
      mat-icon{color:#6366f1} h2{flex:1;margin:0;font-size:1.05rem;font-weight:700}
    }
    mat-dialog-content{padding:12px 20px!important}
    .cd-form{display:flex;flex-direction:column;gap:10px}
    .full{width:100%}
    .row-2{display:flex;gap:10px; .full{flex:1}}
    mat-dialog-actions{padding:8px 20px 16px!important}
  `]
})
export class CoachingAssignmentDialogComponent extends BaseComponent implements OnInit {
  private fb       = inject(FormBuilder);
  private coaching = inject(CoachingApiService);
  dialogRef        = inject(MatDialogRef<CoachingAssignmentDialogComponent>);
  data: { studentId: string; relationshipId: string } = inject(MAT_DIALOG_DATA);

  subjects: CoachingSubject[] = [];
  topics: { id: string; name: string; orderIndex: number }[] = [];

  form = this.fb.group({
    title:           ['', Validators.required],
    description:     [''],
    subjectId:       [''],
    topicId:         [''],
    dueDate:         ['', Validators.required],
    targetQuestions: [null as number | null],
    targetMinutes:   [null as number | null],
  });

  ngOnInit(): void {
    // Default due date: 7 days from now
    const d = new Date(); d.setDate(d.getDate() + 7);
    this.form.patchValue({ dueDate: d.toISOString().split('T')[0] });

    this.coaching.getSubjects({ includeCustom: true })
      .pipe(takeUntil(this.destroy$))
      .subscribe(s => this.subjects = s);
  }

  onSubjectChange(subjectId: string): void {
    this.form.patchValue({ topicId: '' });
    const subject = this.subjects.find(s => s.id === subjectId);
    this.topics = subject?.topics ?? [];
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.loading.set(true);

    this.coaching.createAssignment({
      studentId:       this.data.studentId,
      relationshipId:  this.data.relationshipId,
      title:           v.title!,
      description:     v.description || undefined,
      subjectId:       v.subjectId || undefined,
      topicId:         v.topicId || undefined,
      dueDate:         v.dueDate!,
      targetQuestions: v.targetQuestions ?? undefined,
      targetMinutes:   v.targetMinutes ?? undefined,
    }).pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => { this.toaster.success('Ödev verildi'); this.dialogRef.close(true); },
        error: () => this.toaster.error('İşlem başarısız')
      });
  }

  cancel(): void { this.dialogRef.close(false); }
}
