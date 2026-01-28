import { Component, inject, signal } from '@angular/core';
import { ToasterService } from '../../../../core/services/toaster.service';
import { strongPasswordValidator } from '../../../../core/validators/password.validator';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';

import { MatCardModule } from '@angular/material/card';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { environment } from '../../../../../environments/environment.development';

@Component({
    selector: 'app-student-register',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterLink,
        MatCardModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule
    ],
    templateUrl: './student-register.component.html'
})
export class StudentRegisterComponent {
    private fb = inject(FormBuilder);
    private http = inject(HttpClient);
    private router = inject(Router);
    private toaster = inject(ToasterService);

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);
    hidePassword = signal(true);

    form = this.fb.group({
        firstName: ['', Validators.required],
        lastName: ['', Validators.required],
        email: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, strongPasswordValidator()]]
    });

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
                this.toaster.success('Öğrenci kaydınız başarıyla oluşturuldu.', 'Giriş Yap');
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
