import { Component, inject, signal, OnInit } from '@angular/core';
import { ToasterService } from '../../../../core/services/toaster.service';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { passwordMatchValidator, strongPasswordValidator } from '../../../../core/validators/password.validator';

import { MatCardModule } from '@angular/material/card';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { environment } from '../../../../../environments/environment.development';
import { ConfigurationService } from '../../../../core/services/settings/configuration.service';

@Component({
    selector: 'app-student-register',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterLink,
        MatCardModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule
    ],
    templateUrl: './student-register.component.html'
})
export class StudentRegisterComponent implements OnInit {
    private fb = inject(FormBuilder);
    private http = inject(HttpClient);
    private router = inject(Router);
    private toaster = inject(ToasterService);
    private configService = inject(ConfigurationService);

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);
    hidePassword = signal(true);

    form = this.fb.group({
        firstName: ['', Validators.required],
        lastName: ['', Validators.required],
        email: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, strongPasswordValidator()]],
        confirmPassword: ['', Validators.required]
    }, { validators: passwordMatchValidator });

    ngOnInit() {
        this.configService.getPublicConfigurationValue('auth.allowregistration')
            .subscribe({
                next: (val) => {
                    const allowed = val?.replace(/"/g, '').trim().toLowerCase() === 'true';
                    if (!allowed) {
                        this.toaster.warning('Yeni kullanıcı kayıtları şu an kapalıdır.');
                        this.router.navigate(['/auth/register']);
                    }
                },
                error: () => {
                    // Fallback: If config fails, assume allowed (or log error)
                }
            });
    }

    async onSubmit() {
        if (this.form.invalid) return;

        this.isLoading.set(true);
        const formData = this.form.value;

        const payload = {
            firstName: formData.firstName,
            lastName: formData.lastName,
            email: formData.email,
            password: formData.password,
            // Öğrenciye özel otomatik alanlar
            studentNumber: 'ST-' + Math.floor(Math.random() * 100000),
            dateOfBirth: new Date().toISOString() // İleride tarih seçici eklenirse değiştirilir
        };

        this.http.post(`${environment.apiUrl}/auth/register-student`, payload).subscribe({
            next: () => {
                this.toaster.success('Kayıt işleminiz başarıyla tamamlandı. E-posta adresinizi doğrulamak için size gönderdiğimiz onay linkine tıklayın.', 'Doğrulama Gerekli');
                this.router.navigate(['/auth/login'], { queryParams: { registered: 'true', role: 'student' } });
            },
            error: (err) => {
                this.isLoading.set(false);
                const msg = err.error?.Error || 'Kayıt başarısız oldu.';
                this.toaster.error(msg);
                this.errorMessage.set(msg);
            }
        });
    }

    togglePassword(e: Event) {
        e.preventDefault();
        this.hidePassword.update(v => !v);
    }
}
