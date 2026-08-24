import { Component, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ReportTemplatesService } from '../../../core/services/report-templates.service';
import { ReportTemplate } from '../../../core/models/report.model';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-report-schedule-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatSlideToggleModule,
    MatButtonModule,
    ReactiveFormsModule
  ],
  template: `
    <h2 mat-dialog-title>Rapor Zamanla: {{ data.name }}</h2>
    <mat-dialog-content>
        <form [formGroup]="form">
            <mat-form-field appearance="outline" class="full-width">
                <mat-label>Sıklık</mat-label>
                <mat-select formControlName="frequency" required>
                    <mat-option [value]="0">Günlük</mat-option>
                    <mat-option [value]="1">Haftalık</mat-option>
                    <mat-option [value]="2">Aylık</mat-option>
                </mat-select>
            </mat-form-field>

            @if (form.get('frequency')?.value === 1) {
            <mat-form-field appearance="outline" class="full-width">
                <mat-label>Haftanın Günü</mat-label>
                <mat-select formControlName="dayOfWeek">
                    <mat-option [value]="0">Pazar</mat-option>
                    <mat-option [value]="1">Pazartesi</mat-option>
                    <mat-option [value]="2">Salı</mat-option>
                    <mat-option [value]="3">Çarşamba</mat-option>
                    <mat-option [value]="4">Perşembe</mat-option>
                    <mat-option [value]="5">Cuma</mat-option>
                    <mat-option [value]="6">Cumartesi</mat-option>
                </mat-select>
            </mat-form-field>
            }

            @if (form.get('frequency')?.value === 2) {
            <mat-form-field appearance="outline" class="full-width">
                <mat-label>Ayın Günü</mat-label>
                <input matInput type="number" formControlName="dayOfMonth" min="1" max="31">
            </mat-form-field>
            }

            <mat-form-field appearance="outline" class="full-width">
                <mat-label>Saat (SS:DD)</mat-label>
                <input matInput type="time" formControlName="time" required>
            </mat-form-field>

            <div class="delivery-options">
                <h3>Teslimat Seçenekleri</h3>
                <div class="checkbox-row">
                    <mat-checkbox formControlName="sendEmail">E-posta ile Gönder</mat-checkbox>
                </div>
                
                @if (form.get('sendEmail')?.value) {
                <mat-form-field appearance="outline" class="full-width">
                    <mat-label>E-posta Alıcıları (virgülle ayırınız)</mat-label>
                    <input matInput formControlName="recipients" placeholder="ornek@email.com, diger@email.com">
                    <mat-hint>Birden fazla alıcı için virgül kullanın</mat-hint>
                </mat-form-field>
                }
                
                <div class="checkbox-row">
                    <mat-checkbox formControlName="saveToDashboard">Panoya Kaydet</mat-checkbox>
                </div>
            </div>
        </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
        <button mat-button (click)="cancel()">İptal</button>
        <button mat-raised-button color="primary" (click)="save()" [disabled]="form.invalid || loading()">
            {{ loading() ? 'Zamanla' : 'Zamanla' }}
        </button>
    </mat-dialog-actions>
  `,
  styleUrls: ['./report-schedule-dialog.component.scss']
})
export class ReportScheduleDialogComponent extends BaseComponent {
  private fb = inject(FormBuilder);
  private templatesService = inject(ReportTemplatesService);
  private dialogRef = inject(MatDialogRef<ReportScheduleDialogComponent>);

  form: FormGroup;

  constructor(@Inject(MAT_DIALOG_DATA) public data: ReportTemplate) {
    super();
    this.form = this.fb.group({
      frequency: [0, Validators.required], // Default to Daily (0)
      dayOfWeek: [1],
      dayOfMonth: [1],
      time: ['09:00', Validators.required],
      sendEmail: [true],
      recipients: [''],
      saveToDashboard: [true]
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  save(): void {
    if (this.form.invalid) return;

    this.loading.set(true);
    const formValue = this.form.value;

    const timeStr = formValue.time;
    // Backend expects HH:mm:ss for TimeSpan
    const deliveryTime = timeStr.length === 5 ? `${timeStr}:00` : timeStr;

    // Backend expects comma separated string for recipients, not array
    const recipients = formValue.recipients ? formValue.recipients : null;

    const command = {
      reportTemplateId: this.data.id,
      frequency: Number(formValue.frequency),
      dayOfWeek: formValue.dayOfWeek,
      dayOfMonth: formValue.dayOfMonth,
      deliveryTime: deliveryTime,
      sendEmail: formValue.sendEmail,
      saveToDashboard: formValue.saveToDashboard,
      emailRecipients: recipients
    };

    this.templatesService.createScheduledReport(command as any).subscribe({
      next: () => {
        this.loading.set(false);
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.loading.set(false);
        this.handleError(err, 'Rapor zamanlanırken bir hata oluştu');
      }
    });
  }
}
