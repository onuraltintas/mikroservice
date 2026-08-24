import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { PublicCmsService } from '../../../core/services/public-cms.service';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
    selector: 'app-newsletter-widget',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatSnackBarModule
    ],
    templateUrl: './newsletter-widget.component.html',
    styleUrls: ['./newsletter-widget.component.scss']
})
export class NewsletterWidgetComponent {
    private fb = inject(FormBuilder);
    private cmsService = inject(PublicCmsService);
    private snackBar = inject(MatSnackBar);

    newsletterForm: FormGroup;
    loading = false;
    isMinimized = false;

    constructor() {
        this.newsletterForm = this.fb.group({
            email: ['', [Validators.required, Validators.email]],
            name: ['']
        });
    }

    onSubmit(): void {
        if (this.newsletterForm.invalid) {
            this.newsletterForm.markAllAsTouched();
            return;
        }

        this.loading = true;
        this.cmsService.subscribeNewsletter(this.newsletterForm.value).subscribe({
            next: () => {
                this.snackBar.open('🎉 Başarıyla abone oldunuz! Hoş geldiniz.', 'Kapat', {
                    duration: 5000,
                    horizontalPosition: 'center',
                    verticalPosition: 'top',
                    panelClass: ['success-snackbar']
                });
                this.newsletterForm.reset();
                this.loading = false;
                this.isMinimized = true;
            },
            error: (error) => {
                console.error('Error subscribing to newsletter:', error);
                this.snackBar.open('Bir hata oluştu. Lütfen tekrar deneyin.', 'Kapat', {
                    duration: 5000,
                    horizontalPosition: 'center',
                    verticalPosition: 'top',
                    panelClass: ['error-snackbar']
                });
                this.loading = false;
            }
        });
    }

    toggleMinimize(): void {
        this.isMinimized = !this.isMinimized;
    }

    getErrorMessage(): string {
        const emailField = this.newsletterForm.get('email');
        if (emailField?.hasError('required')) {
            return 'E-posta adresi gereklidir';
        }
        if (emailField?.hasError('email')) {
            return 'Geçerli bir e-posta adresi giriniz';
        }
        return '';
    }
}
