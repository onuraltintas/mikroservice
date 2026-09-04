import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
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
  selector: 'app-coaching-exam-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="cd-header">
      <mat-icon>quiz</mat-icon>
      <h2>Sınav Sonucu Ekle</h2>
      <button mat-icon-button type="button" (click)="cancel()" aria-label="Sınav penceresini kapat"><mat-icon>close</mat-icon></button>
    </div>
    <mat-dialog-content>
      <form [formGroup]="form" class="cd-form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Sınav Adı</mat-label>
          <input matInput formControlName="examName" placeholder="Örn: TYT Deneme #3">
          <mat-error>Zorunlu alan</mat-error>
        </mat-form-field>
        <div class="row-2">
          <mat-form-field appearance="outline" class="full">
            <mat-label>Sınav Tarihi</mat-label>
            <input matInput type="date" formControlName="examDate">
            <mat-error>Zorunlu alan</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full">
            <mat-label>Sınav Türü</mat-label>
            <mat-select formControlName="examType">
              <mat-option value="YKS_TYT">YKS TYT</mat-option>
              <mat-option value="AYT_Sayisal">AYT Sayısal</mat-option>
              <mat-option value="AYT_EsitAgirlik">AYT Eşit Ağırlık</mat-option>
              <mat-option value="AYT_Sozel">AYT Sözel</mat-option>
              <mat-option value="LGS">LGS</mat-option>
              <mat-option value="General">Genel</mat-option>
              <mat-option value="Custom">Diğer</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Notlar</mat-label>
          <textarea matInput formControlName="notes" rows="2" placeholder="İsteğe bağlı..."></textarea>
        </mat-form-field>

        <!-- Subject Results -->
        <div class="subjects-section">
          <div class="subjects-header">
            <span class="subjects-label">Ders Detayları (isteğe bağlı)</span>
            <button mat-stroked-button type="button" class="add-row-btn" (click)="addSubjectRow()">
              <mat-icon>add</mat-icon> Ders Ekle
            </button>
          </div>

          <div formArrayName="subjectResults">
            <div *ngFor="let row of subjectResultsArray.controls; let i = index"
                 [formGroupName]="i" class="subject-row">
              <mat-form-field appearance="outline" class="sr-subject">
                <mat-label>Ders</mat-label>
                <mat-select formControlName="subjectId">
                  <mat-option *ngFor="let s of subjects" [value]="s.id">{{ s.name }}</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline" class="sr-num">
                <mat-label>D</mat-label>
                <input matInput type="number" formControlName="correct" min="0" placeholder="0">
              </mat-form-field>
              <mat-form-field appearance="outline" class="sr-num">
                <mat-label>Y</mat-label>
                <input matInput type="number" formControlName="wrong" min="0" placeholder="0">
              </mat-form-field>
              <mat-form-field appearance="outline" class="sr-num">
                <mat-label>B</mat-label>
                <input matInput type="number" formControlName="empty" min="0" placeholder="0">
              </mat-form-field>
      <button mat-icon-button type="button" aria-label="Ders satırını kaldır" class="remove-btn" (click)="removeSubjectRow(i)">
                <mat-icon>delete</mat-icon>
              </button>
            </div>
          </div>
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button (click)="cancel()">İptal</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="loading() || form.invalid">
        <mat-spinner *ngIf="loading()" class="dialog-spinner" diameter="16"></mat-spinner>
        Kaydet
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-spinner { display:inline-flex; margin-right:6px; }
    .cd-header { display:flex; align-items:center; gap:10px; padding:16px 20px 0;
      mat-icon{color:#6366f1} h2{flex:1;margin:0;font-size:1.05rem;font-weight:700}
    }
    mat-dialog-content{padding:12px 20px!important;max-height:70vh;overflow-y:auto}
    .cd-form{display:flex;flex-direction:column;gap:10px}
    .full{width:100%}
    .row-2{display:flex;gap:10px; .full{flex:1}}
    mat-dialog-actions{padding:8px 20px 16px!important}

    .subjects-section{
      border:1px solid #e5e7eb;border-radius:10px;padding:12px;
    }
    .subjects-header{
      display:flex;align-items:center;justify-content:space-between;margin-bottom:8px;
      .subjects-label{font-size:0.85rem;font-weight:600;color:#374151}
    }
    .add-row-btn{height:30px;font-size:0.78rem;
      mat-icon{font-size:16px;width:16px;height:16px}
    }
    .subject-row{
      display:flex;align-items:flex-start;gap:8px;
      .sr-subject{flex:2}
      .sr-num{flex:1;min-width:60px}
      .remove-btn{color:#d1d5db;margin-top:4px;flex-shrink:0;
        &:hover{color:#ef4444}
      }
    }
  `]
})
export class CoachingExamDialogComponent extends BaseComponent implements OnInit {
  private fb       = inject(FormBuilder);
  private coaching = inject(CoachingApiService);
  dialogRef        = inject(MatDialogRef<CoachingExamDialogComponent>);
  data: { studentId: string } = inject(MAT_DIALOG_DATA);

  subjects: CoachingSubject[] = [];

  form = this.fb.group({
    examName: ['', Validators.required],
    examDate: ['', Validators.required],
    examType: ['YKS_TYT', Validators.required],
    notes:    [''],
    subjectResults: this.fb.array([]),
  });

  get subjectResultsArray(): FormArray {
    return this.form.get('subjectResults') as FormArray;
  }

  ngOnInit(): void {
    const today = new Date().toISOString().split('T')[0];
    this.form.patchValue({ examDate: today });

    this.coaching.getSubjects({ includeCustom: false })
      .pipe(takeUntil(this.destroy$))
      .subscribe(s => this.subjects = s);
  }

  addSubjectRow(): void {
    this.subjectResultsArray.push(this.fb.group({
      subjectId: ['', Validators.required],
      correct:   [0],
      wrong:     [0],
      empty:     [0],
    }));
  }

  removeSubjectRow(i: number): void {
    this.subjectResultsArray.removeAt(i);
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.loading.set(true);

    const subjectResults = v.subjectResults
      .filter((r: any) => r.subjectId)
      .map((r: any) => {
        const correct = Number(r.correct) || 0;
        const wrong   = Number(r.wrong) || 0;
        const empty   = Number(r.empty) || 0;
        const total   = correct + wrong + empty;
        const net     = correct - (wrong / 4);
        return {
          subjectId: r.subjectId,
          totalQuestions: total,
          correct,
          wrong,
          empty,
          net: Math.round(net * 100) / 100,
        };
      });

    this.coaching.createExamResult({
      studentId:      this.data.studentId,
      examName:       v.examName!,
      examDate:       v.examDate!,
      examType:       v.examType!,
      notes:          v.notes || undefined,
      subjectResults: subjectResults.length > 0 ? subjectResults : undefined,
    }).pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => { this.toaster.success('Sınav sonucu kaydedildi'); this.dialogRef.close(true); },
        error: () => this.toaster.error('İşlem başarısız')
      });
  }

  cancel(): void { this.dialogRef.close(false); }
}
