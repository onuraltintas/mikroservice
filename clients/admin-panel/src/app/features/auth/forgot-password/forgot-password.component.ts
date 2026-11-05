import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { IdentityService } from '../../../core/services/identity.service';
import { getApiErrorMessage } from '../../../core/http/api-error-message';

@Component({
    selector: 'app-forgot-password',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterModule,
        MatIconModule,
        MatButtonModule,
        MatProgressSpinnerModule
    ],
    templateUrl: './forgot-password.component.html',
    styleUrl: './forgot-password.component.scss'
})
export class ForgotPasswordComponent {
    private identityService = inject(IdentityService);

    email = '';
    isLoading = signal(false);
    emailSent = signal(false);
    errorMessage = signal<string | null>(null);

    onSubmit(event: Event) {
        event.preventDefault();
        if (!this.email || this.isLoading()) return;

        this.isLoading.set(true);
        this.errorMessage.set(null);

        this.identityService.forgotPassword(this.email).subscribe({
            next: () => {
                this.emailSent.set(true);
                this.isLoading.set(false);
            },
            error: (err) => {
                this.isLoading.set(false);
                // Rate limits, network and server failures mean nothing was sent: say so.
                if (err?.status === 0 || err?.status === 429 || err?.status >= 500) {
                    this.errorMessage.set(getApiErrorMessage(err, 'Sıfırlama bağlantısı şu an gönderilemedi. Lütfen biraz sonra yeniden deneyin.'));
                    return;
                }
                // Security best practice: don't reveal whether the e-mail exists.
                this.emailSent.set(true);
            }
        });
    }
}
