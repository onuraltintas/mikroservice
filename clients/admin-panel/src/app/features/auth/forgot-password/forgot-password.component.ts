import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { IdentityService } from '../../../core/services/identity.service';
import { ToasterService } from '../../../core/services/toaster.service';

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
    private toaster = inject(ToasterService);

    email = '';
    isLoading = signal(false);
    emailSent = signal(false);

    onSubmit(event: Event) {
        event.preventDefault();
        if (!this.email || this.isLoading()) return;

        this.isLoading.set(true);

        this.identityService.forgotPassword(this.email).subscribe({
            next: () => {
                this.emailSent.set(true);
                this.isLoading.set(false);
            },
            error: (err) => {
                // Security best practice: Don't reveal if email exists
                // Always show success message
                this.emailSent.set(true);
                this.isLoading.set(false);
            }
        });
    }
}
