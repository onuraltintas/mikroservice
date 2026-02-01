import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ToasterService } from '../../../../core/services/toaster.service';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { passwordMatchValidator, strongPasswordValidator } from '../../../../core/validators/password.validator';

import { MatCardModule } from '@angular/material/card';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { environment } from '../../../../../environments/environment.development';

@Component({
    selector: 'app-institution-register',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterLink,
        MatCardModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule
    ],
    templateUrl: './institution-register.component.html'
})
export class InstitutionRegisterComponent {
    private fb = inject(FormBuilder);
    private http = inject(HttpClient);
    private router = inject(Router);
    private toaster = inject(ToasterService);

    isLoading = signal(false);
    errorMessage = signal<string | null>(null);
    hidePassword = signal(true);

    form = this.fb.group({
        institutionName: ['', Validators.required],
        taxNumber: ['', Validators.required],
        firstName: ['', Validators.required], // Manager First Name
        lastName: ['', Validators.required],  // Manager Last Name
        email: ['', [Validators.required, Validators.email]],
        password: ['', [Validators.required, strongPasswordValidator()]],
        confirmPassword: ['', Validators.required]
    }, { validators: passwordMatchValidator });

    async onSubmit() {
        if (this.form.invalid) return;

        this.isLoading.set(true);
        const formData = this.form.value;

        const payload = {
            institutionName: formData.institutionName,
            taxNumber: formData.taxNumber,
            managerFirstName: formData.firstName,
            managerLastName: formData.lastName,
            email: formData.email,
            password: formData.password
        };

        this.http.post(`${environment.apiUrl}/auth/register-institution`, payload).subscribe({
            next: () => {
                this.toaster.success('Kayıt işleminiz başarıyla tamamlandı. E-posta adresinizi doğrulamak için size gönderdiğimiz onay linkine tıklayın.', 'Doğrulama Gerekli');
                this.router.navigate(['/auth/login'], { queryParams: { registered: 'true', role: 'institution' } });
            },
            error: (err) => {
                this.isLoading.set(false);
                const msg = err.error?.Error || 'Kayıt başarısız oldu.';
                this.toaster.error(msg);
                this.errorMessage.set(msg);
            }
        });
    }

    togglePassword(e: Event) { e.preventDefault(); this.hidePassword.update(v => !v); }
}
