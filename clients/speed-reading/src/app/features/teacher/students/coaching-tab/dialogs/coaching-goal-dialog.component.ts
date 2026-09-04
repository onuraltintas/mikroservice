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
import { Observable } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { BaseComponent } from '../../../../../core/components/base.component';
import { CoachingApiService, CoachingGoal } from '../../../../../core/services/coaching-api.service';
import { AuthService } from '../../../../../core/services/auth.service';

@Component({
  selector: 'app-coaching-goal-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="cd-header">
      <mat-icon>flag</mat-icon>
      <h2>{{ data.goal ? 'Hedef Düzenle' : 'Yeni Hedef' }}</h2>
      <button mat-icon-button type="button" (click)="cancel()" aria-label="Hedef penceresini kapat"><mat-icon>close</mat-icon></button>
    </div>
    <mat-dialog-content>
      <form [formGroup]="form" class="cd-form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Hedef Başlığı</mat-label>
          <input matInput formControlName="title" placeholder="Örn: YKS 2025 Hedefi">
          <mat-error>Zorunlu alan</mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Açıklama</mat-label>
          <textarea matInput formControlName="description" rows="2" placeholder="İsteğe bağlı..."></textarea>
        </mat-form-field>
        <div class="row-2">
          <mat-form-field appearance="outline" class="full">
            <mat-label>Seviye</mat-label>
            <mat-select formControlName="level">
              <mat-option value="Yearly">Yıllık</mat-option>
              <mat-option value="Monthly">Aylık</mat-option>
              <mat-option value="Weekly">Haftalık</mat-option>
              <mat-option value="Daily">Günlük</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full">
            <mat-label>Hedef Tarih</mat-label>
            <input matInput type="date" formControlName="targetDate">
            <mat-error>Zorunlu alan</mat-error>
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Başarı Kriterleri</mat-label>
          <textarea matInput formControlName="successCriteria" rows="2" placeholder="Hedef ne zaman başarıldı sayılır?"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button (click)="cancel()">İptal</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="loading() || form.invalid">
        <mat-spinner *ngIf="loading()" class="dialog-spinner" diameter="16"></mat-spinner>
        {{ data.goal ? 'Güncelle' : 'Oluştur' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-spinner { display:inline-flex; margin-right:6px; }
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
export class CoachingGoalDialogComponent extends BaseComponent implements OnInit {
  private fb      = inject(FormBuilder);
  private coaching = inject(CoachingApiService);
  private auth    = inject(AuthService);
  dialogRef       = inject(MatDialogRef<CoachingGoalDialogComponent>);
  data: { studentId: string; goal?: CoachingGoal } = inject(MAT_DIALOG_DATA);

  form = this.fb.group({
    title:           ['', Validators.required],
    description:     [''],
    level:           ['Monthly', Validators.required],
    targetDate:      ['', Validators.required],
    successCriteria: [''],
  });

  ngOnInit(): void {
    if (this.data.goal) {
      const g = this.data.goal;
      this.form.patchValue({
        title: g.title,
        description: g.description ?? '',
        level: g.level,
        targetDate: g.targetDate.split('T')[0],
        successCriteria: g.successCriteria ?? '',
      });
    } else {
      // Default target date: 30 days from now
      const d = new Date(); d.setDate(d.getDate() + 30);
      this.form.patchValue({ targetDate: d.toISOString().split('T')[0] });
    }
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.loading.set(true);

    const op: Observable<any> = this.data.goal
      ? this.coaching.updateGoal(this.data.goal.id, {
          title: v.title!,
          description: v.description || undefined,
          targetDate: v.targetDate!,
          successCriteria: v.successCriteria || undefined,
        })
      : this.coaching.createGoal({
          studentId: this.data.studentId,
          coachId: this.auth.currentUserValue?.id,
          level: v.level!,
          title: v.title!,
          description: v.description || undefined,
          targetDate: v.targetDate!,
          successCriteria: v.successCriteria || undefined,
        });

    op.pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => { this.toaster.success(this.data.goal ? 'Hedef güncellendi' : 'Hedef oluşturuldu'); this.dialogRef.close(true); },
        error: () => this.toaster.error('İşlem başarısız')
      });
  }

  cancel(): void { this.dialogRef.close(false); }
}
