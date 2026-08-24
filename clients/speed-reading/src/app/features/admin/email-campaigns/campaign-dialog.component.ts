import { Component, OnInit, inject, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule, NativeDateAdapter, DateAdapter, MAT_DATE_FORMATS, MAT_DATE_LOCALE } from '@angular/material/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { QuillModule } from 'ngx-quill';
import { EmailCampaignsService } from '../../../core/services/email-campaigns.service';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

export const MY_DATE_FORMATS = {
  parse: {
    dateInput: 'DD/MM/YYYY',
  },
  display: {
    dateInput: 'DD/MM/YYYY',
    monthYearLabel: 'MMM YYYY',
    dateA11yLabel: 'LL',
    monthYearA11yLabel: 'MMMM YYYY',
  },
};

@Component({
  selector: 'app-campaign-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatIconModule,
    MatTabsModule,
    MatButtonToggleModule,
    MatProgressSpinnerModule,
    QuillModule
  ],
  providers: [
    { provide: DateAdapter, useClass: NativeDateAdapter },
    { provide: MAT_DATE_LOCALE, useValue: 'tr-TR' },
    { provide: MAT_DATE_FORMATS, useValue: MY_DATE_FORMATS }
  ],
  templateUrl: './campaign-dialog.component.html',
  styleUrls: ['./campaign-dialog.component.scss']
})
export class CampaignDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private campaignsService = inject(EmailCampaignsService);
  // toaster inherited from BaseComponent
  private dialogRef = inject(MatDialogRef<CampaignDialogComponent>);

  campaignForm!: FormGroup;
  isEditMode = false;

  quillConfig = {
    toolbar: [
      ['bold', 'italic', 'underline', 'strike'],
      ['blockquote', 'code-block'],
      [{ 'header': 1 }, { 'header': 2 }],
      [{ 'list': 'ordered' }, { 'list': 'bullet' }],
      [{ 'script': 'sub' }, { 'script': 'super' }],
      [{ 'indent': '-1' }, { 'indent': '+1' }],
      [{ 'direction': 'rtl' }],
      [{ 'size': ['small', false, 'large', 'huge'] }],
      [{ 'header': [1, 2, 3, 4, 5, 6, false] }],
      [{ 'color': [] }, { 'background': [] }],
      [{ 'font': [] }],
      [{ 'align': [] }],
      ['clean'],
      ['link', 'image', 'video']
    ]
  };

  constructor(@Inject(MAT_DIALOG_DATA) public data: { mode: 'create' | 'edit'; campaignId?: string }) {
    super();
    this.isEditMode = data.mode === 'edit';
  }

  ngOnInit(): void {
    this.initForm();

    if (this.isEditMode && this.data.campaignId) {
      this.loadCampaign();
    }
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  initForm(): void {
    this.campaignForm = this.fb.group({
      name: ['', Validators.required],
      subject: ['', Validators.required],
      body: ['', Validators.required],
      plainTextBody: [''],
      targetRoles: [[]],
      includeAllUsers: [false],
      includeSubscribers: [false],
      scheduledFor: [null]
    });
  }

  loadCampaign(): void {
    if (!this.data.campaignId) return;

    this.loading.set(true);
    this.campaignsService.getCampaignDetail(this.data.campaignId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (campaign) => {
          let targetRoles: string[] = [];
          if (campaign.targetRoles) {
            try {
              targetRoles = JSON.parse(campaign.targetRoles);
            } catch {
              targetRoles = [];
            }
          }

          this.campaignForm.patchValue({
            name: campaign.name,
            subject: campaign.subject,
            body: campaign.body,
            plainTextBody: campaign.plainTextBody,
            targetRoles: targetRoles,
            includeAllUsers: campaign.includeAllUsers,
            // Check if backend returns includeSubscribers (might need to update service interface types implicitly or explicitly)
            // For safety, we use 'as any' casting if TS complains, but usually JS objects just pass it through if it exists.
            includeSubscribers: (campaign as any).includeSubscribers || false,
            scheduledFor: campaign.scheduledFor
          });
        },
        error: (err) => {
          this.handleError(err, 'Kampanya yüklenirken hata oluştu');
        }
      });
  }

  onIncludeAllUsersChange(): void {
    const includeAll = this.campaignForm.get('includeAllUsers')?.value;
    if (includeAll) {
      this.campaignForm.get('targetRoles')?.setValue([]);
    }
  }

  getHtmlPreview(): string {
    const bodyValue = this.campaignForm.get('body')?.value;
    return bodyValue || '<p style="color: #999; text-align: center; padding: 40px;">Henüz içerik yok</p>';
  }

  save(): void {
    if (this.campaignForm.invalid) return;

    this.loading.set(true);
    const formValue = this.campaignForm.value;

    // Auto-generate plain text if not provided
    let plainTextBody = formValue.plainTextBody;
    if (!plainTextBody && formValue.body) {
      // Strip HTML tags for plain text version using DOMParser (safer than innerHTML)
      const parser = new DOMParser();
      const doc = parser.parseFromString(formValue.body, 'text/html');
      plainTextBody = doc.body.textContent || '';
    }

    const request = {
      name: formValue.name,
      subject: formValue.subject,
      body: formValue.body,
      plainTextBody: plainTextBody || undefined,
      targetRoles: formValue.includeAllUsers ? undefined : (formValue.targetRoles && formValue.targetRoles.length > 0 ? JSON.stringify(formValue.targetRoles) : undefined),
      includeAllUsers: formValue.includeAllUsers,
      includeSubscribers: formValue.includeSubscribers,
      scheduledFor: formValue.scheduledFor || undefined
    };

    if (this.isEditMode && this.data.campaignId) {
      this.campaignsService.updateCampaign(this.data.campaignId, request)
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => this.loading.set(false))
        )
        .subscribe({
          next: () => {
            this.handleSuccess('Kampanya başarıyla güncellendi');
            this.dialogRef.close(true);
          },
          error: (err: any) => {
            this.handleError(err, 'Kampanya güncellenirken hata oluştu');
          }
        });
    } else {
      this.campaignsService.createCampaign(request)
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => this.loading.set(false))
        )
        .subscribe({
          next: () => {
            this.handleSuccess('Kampanya başarıyla oluşturuldu');
            this.dialogRef.close(true);
          },
          error: (err: any) => {
            this.handleError(err, 'Kampanya oluşturulurken hata oluştu');
          }
        });
    }
  }
}
