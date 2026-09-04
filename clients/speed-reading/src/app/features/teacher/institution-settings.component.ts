import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { InstitutionsService } from '../../core/services/institutions.service';
import { AuthService } from '../../core/services/auth.service';
import { ToasterService } from '../../core/services/toaster.service';
import { Institution } from '../../core/models/institution.model';

@Component({
    selector: 'app-institution-settings',
    standalone: true,
    imports: [
        CommonModule,
        MatCardModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule,
        MatTabsModule,
        ReactiveFormsModule
    ],
    templateUrl: './institution-settings.component.html',
    styles: [`
    .settings-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 32px 24px;
    }

    .tab-content { padding-top: 24px; }
    .contact-email-field { margin-top: 16px; }
    .settings-submit { height: 48px; padding: 0 32px; }
    .button-spinner { display: inline-block; margin-right: 8px; }

    .header-section {
      margin-bottom: 32px;
      
      h1 {
        font-size: 28px;
        font-weight: 700;
        color: #1a1f36;
        margin: 0 0 8px 0;
      }
      
      p {
        color: #697386;
        font-size: 16px;
        margin: 0;
      }
    }

    .main-grid {
        display: grid;
        grid-template-columns: 1fr;
        gap: 24px;
        padding-top: 24px;
        
        @media (min-width: 768px) {
            grid-template-columns: 3fr 2fr;
        }
    }

    .settings-card {
      border-radius: 16px;
      padding: 24px;
      border: 1px solid rgba(0,0,0,0.06);
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
      background: white;
      height: 100%;
      
      h2 {
        font-size: 18px;
        font-weight: 600;
        margin-bottom: 24px;
        color: #1a1f36;
        display: flex;
        align-items: center;
        gap: 8px;

        mat-icon {
            color: #3f51b5;
        }
      }
    }

    form {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .form-row {
        display: grid;
        grid-template-columns: 1fr;
        gap: 16px;
        
        @media (min-width: 600px) {
            grid-template-columns: 1fr 1fr;
        }
    }

    .modern-input {
      width: 100%;
    }

    .form-actions {
      display: flex;
      justify-content: flex-end;
      margin-top: 24px;
      grid-column: 1 / -1;
    }
    
    .loading-shade {
        position: absolute;
        top: 0;
        left: 0;
        bottom: 0;
        right: 0;
        background: rgba(255,255,255,0.7);
        z-index: 10;
        display: flex;
        align-items: center;
        justify-content: center;
        border-radius: 16px;
    }
  `]
})
export class InstitutionSettingsComponent implements OnInit {
    private fb = inject(FormBuilder);
    private institutionsService = inject(InstitutionsService);
    private authService = inject(AuthService);
    private toaster = inject(ToasterService);

    settingsForm: FormGroup;
    passwordForm: FormGroup;

    loading = signal(false);
    passwordLoading = signal(false);

    institutionId: string | null = null;
    institution = signal<Institution | null>(null);

    constructor() {
        this.settingsForm = this.fb.group({
            name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
            contactEmail: ['', [Validators.required, Validators.email]],
            phoneNumber: ['', [Validators.pattern('^[0-9\\+\\-\\(\\) \\s]{10,20}$')]],
            city: ['', [Validators.maxLength(100)]],
            district: ['', [Validators.maxLength(100)]],
            address: ['']
        });

        this.passwordForm = this.fb.group({
            currentPassword: ['', [Validators.required]],
            newPassword: ['', [
                Validators.required,
                Validators.minLength(8),
                Validators.pattern('^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$')
            ]],
            confirmNewPassword: ['', [Validators.required]]
        }, { validators: this.passwordMatchValidator });
    }

    passwordMatchValidator(g: FormGroup) {
        return g.get('newPassword')?.value === g.get('confirmNewPassword')?.value
            ? null : { mismatch: true };
    }

    ngOnInit(): void {
        const user = this.authService.currentUserValue;

        // Check if user has institutionId claim/property
        if (user && (user as any).institutionId) {
            this.institutionId = (user as any).institutionId;
            this.loadInstitution();
        } else {
            this.toaster.error('Kurum bilgisine ulaşılamadı. Lütfen yönetici ile iletişime geçin.');
            console.error('Institution ID not found in user object:', user);
        }
    }

    loadInstitution() {
        if (!this.institutionId) return;

        this.loading.set(true);
        this.institutionsService.getInstitutionById(this.institutionId).subscribe({
            next: (data: Institution) => {
                this.institution.set(data);

                this.settingsForm.patchValue({
                    name: data.name,
                    contactEmail: data.contactEmail,
                    phoneNumber: data.phoneNumber,
                    address: data.address,
                    city: data.city,
                    district: data.district
                });
                this.loading.set(false);
            },
            error: (err: any) => {
                console.error('Error loading institution', err);
                this.toaster.error('Kurum bilgileri yüklenirken hata oluştu');
                this.loading.set(false);
            }
        });
    }

    onSubmit() {
        if (this.settingsForm.invalid || !this.institutionId) return;

        this.loading.set(true);
        const formValue = this.settingsForm.value;

        this.institutionsService.updateInstitution(this.institutionId, {
            name: formValue.name,
            email: formValue.contactEmail,
            phone: formValue.phoneNumber,
            address: formValue.address,
            city: formValue.city,
            district: formValue.district
        }).subscribe({
            next: (updated: any) => {
                this.toaster.success('Kurum ayarları güncellendi');
                this.loading.set(false);
            },
            error: (err: any) => {
                console.error('Error updating settings', err);
                this.toaster.error('Güncelleme başarısız oldu');
                this.loading.set(false);
            }
        });
    }

    changePassword() {
        if (this.passwordForm.invalid) return;

        this.passwordLoading.set(true);
        const val = this.passwordForm.value;

        this.authService.changePassword({
            currentPassword: val.currentPassword,
            newPassword: val.newPassword,
            confirmNewPassword: val.confirmNewPassword
        })
            .pipe(finalize(() => this.passwordLoading.set(false)))
            .subscribe({
                next: () => {
                    this.toaster.success('Şifreniz başarıyla değiştirildi.');
                    this.passwordForm.reset();
                },
                error: (err) => {
                    console.error('Error changing password', err);
                    const msg = err.error?.detail || err.error?.title || 'Şifre değiştirilemedi.';
                    this.toaster.error(msg);
                }
            });
    }
}
