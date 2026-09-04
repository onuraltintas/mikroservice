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
import { CoachingApiService, CoachingSession } from '../../../../../core/services/coaching-api.service';

@Component({
  selector: 'app-coaching-session-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="cd-header">
      <mat-icon>video_call</mat-icon>
      <h2>{{ data.session ? 'Seans Düzenle' : 'Seans Planla' }}</h2>
      <button mat-icon-button type="button" (click)="cancel()" aria-label="Seans penceresini kapat"><mat-icon>close</mat-icon></button>
    </div>
    <mat-dialog-content>
      <form [formGroup]="form" class="cd-form">
        <div class="row-2">
          <mat-form-field appearance="outline" class="full">
            <mat-label>Tarih & Saat</mat-label>
            <input matInput type="datetime-local" formControlName="scheduledAt">
            <mat-error>Zorunlu alan</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full">
            <mat-label>Planlanan Süre (dk)</mat-label>
            <input matInput type="number" formControlName="plannedDurationMinutes" min="15">
            <mat-error>Zorunlu alan</mat-error>
          </mat-form-field>
        </div>
        <div class="row-2">
          <mat-form-field appearance="outline" class="full">
            <mat-label>Seans Türü</mat-label>
            <mat-select formControlName="sessionType">
              <mat-option value="Regular">Rutin</mat-option>
              <mat-option value="GoalReview">Hedef Değerlendirme</mat-option>
              <mat-option value="ExamReview">Sınav Değerlendirme</mat-option>
              <mat-option value="Emergency">Acil</mat-option>
              <mat-option value="Closing">Kapanış</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full" *ngIf="data.session">
            <mat-label>Durum</mat-label>
            <mat-select formControlName="status">
              <mat-option value="Scheduled">Planlandı</mat-option>
              <mat-option value="Completed">Tamamlandı</mat-option>
              <mat-option value="Cancelled">İptal</mat-option>
              <mat-option value="NoShow">Gelmedi</mat-option>
              <mat-option value="Rescheduled">Ertelendi</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Görüşme Linki (isteğe bağlı)</mat-label>
          <input matInput formControlName="meetingLink" placeholder="https://meet.google.com/...">
          <mat-icon matPrefix>link</mat-icon>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Paylaşılan Notlar</mat-label>
          <textarea matInput formControlName="sharedNotes" rows="2" placeholder="Öğrenci de görebilir..."></textarea>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Özel Notlar (sadece sen görürsün)</mat-label>
          <textarea matInput formControlName="coachPrivateNotes" rows="2" placeholder="Sadece koça özel..."></textarea>
          <mat-icon matPrefix>lock</mat-icon>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full" *ngIf="data.session">
          <mat-label>Aksiyon Maddeleri</mat-label>
          <textarea matInput formControlName="actionItems" rows="2" placeholder="Seansdan çıkan görevler..."></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button (click)="cancel()">İptal</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="loading() || form.invalid">
        <mat-spinner *ngIf="loading()" class="dialog-spinner" diameter="16"></mat-spinner>
        {{ data.session ? 'Güncelle' : 'Planla' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-spinner { display:inline-flex; margin-right:6px; }
    .cd-header { display:flex; align-items:center; gap:10px; padding:16px 20px 0;
      mat-icon{color:#6366f1} h2{flex:1;margin:0;font-size:1.05rem;font-weight:700}
    }
    mat-dialog-content{padding:12px 20px!important;max-height:65vh;overflow-y:auto}
    .cd-form{display:flex;flex-direction:column;gap:10px}
    .full{width:100%}
    .row-2{display:flex;gap:10px; .full{flex:1}}
    mat-dialog-actions{padding:8px 20px 16px!important}
  `]
})
export class CoachingSessionDialogComponent extends BaseComponent implements OnInit {
  private fb       = inject(FormBuilder);
  private coaching = inject(CoachingApiService);
  dialogRef        = inject(MatDialogRef<CoachingSessionDialogComponent>);
  data: { studentId: string; studentName: string; coachId: string; relationshipId: string; session?: CoachingSession } = inject(MAT_DIALOG_DATA);

  form = this.fb.group({
    scheduledAt:            ['', Validators.required],
    plannedDurationMinutes: [60, [Validators.required, Validators.min(15)]],
    sessionType:            ['Regular', Validators.required],
    status:                 ['Scheduled'],
    meetingLink:            [''],
    sharedNotes:            [''],
    coachPrivateNotes:      [''],
    actionItems:            [''],
  });

  ngOnInit(): void {
    if (this.data.session) {
      const s = this.data.session;
      this.form.patchValue({
        scheduledAt:            s.scheduledAt.slice(0, 16),
        plannedDurationMinutes: s.plannedDurationMinutes,
        sessionType:            s.sessionType,
        status:                 s.status,
        meetingLink:            s.meetingLink ?? '',
        sharedNotes:            s.sharedNotes ?? '',
        coachPrivateNotes:      s.coachPrivateNotes ?? '',
        actionItems:            s.actionItems ?? '',
      });
    } else {
      // Default: tomorrow same time
      const d = new Date(); d.setDate(d.getDate() + 1); d.setMinutes(0, 0, 0);
      this.form.patchValue({ scheduledAt: d.toISOString().slice(0, 16) });
    }
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.loading.set(true);

    const op: Observable<any> = this.data.session
      ? this.coaching.updateSession(this.data.session.id, {
          status: v.status ?? undefined,
          scheduledAt: v.scheduledAt ? new Date(v.scheduledAt).toISOString() : undefined,
          sharedNotes: v.sharedNotes || undefined,
          coachPrivateNotes: v.coachPrivateNotes || undefined,
          actionItems: v.actionItems || undefined,
          meetingLink: v.meetingLink || undefined,
        })
      : this.coaching.createSession({
          coachId: this.data.coachId,
          studentId: this.data.studentId,
          scheduledAt: new Date(v.scheduledAt!).toISOString(),
          plannedDurationMinutes: v.plannedDurationMinutes!,
          sessionType: v.sessionType!,
          meetingLink: v.meetingLink || undefined,
          sharedNotes: v.sharedNotes || undefined,
          coachPrivateNotes: v.coachPrivateNotes || undefined,
        });

    op.pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => { this.toaster.success(this.data.session ? 'Seans güncellendi' : 'Seans planlandı'); this.dialogRef.close(true); },
        error: () => this.toaster.error('İşlem başarısız')
      });
  }

  cancel(): void { this.dialogRef.close(false); }
}
