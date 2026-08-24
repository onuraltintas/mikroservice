import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { ToasterService } from '../../../core/services/toaster.service';
import { environment } from '../../../../environments/environment';

interface BulkNotificationRequest {
    title: string;
    message: string;
    priority: number;
    targetType: string;
    targetRole?: string;
    actionUrl?: string;
    sendEmail: boolean;
}

interface BulkNotificationResult {
    totalSent: number;
    emailsSent: number;
    errors: string[];
    success: boolean;
}

interface ApiResponse<T> {
    data: T;
    success: boolean;
    message?: string;
}

@Component({
    selector: 'app-bulk-notification',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        MatCardModule,
        MatButtonModule,
        MatIconModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatCheckboxModule,
        MatProgressSpinnerModule,
        MatRadioModule
    ],
    templateUrl: './bulk-notification.component.html',
    styleUrls: ['./bulk-notification.component.scss']
})
export class BulkNotificationComponent {
    private http = inject(HttpClient);
    private toaster = inject(ToasterService);
    private router = inject(Router);
    private apiUrl = environment.apiUrl;

    sending = signal(false);
    result = signal<BulkNotificationResult | null>(null);

    // Form fields
    title = '';
    message = '';
    priority = 2; // Normal
    targetType = 'All';
    targetRole = '';
    actionUrl = '';
    sendEmail = false;

    priorities = [
        { value: 1, label: 'Düşük', icon: 'arrow_downward', color: 'gray' },
        { value: 2, label: 'Normal', icon: 'remove', color: 'blue' },
        { value: 3, label: 'Yüksek', icon: 'arrow_upward', color: 'orange' },
        { value: 4, label: 'Acil', icon: 'priority_high', color: 'red' }
    ];

    roles = [
        { value: 'Student', label: 'Öğrenciler' },
        { value: 'Teacher', label: 'Öğretmenler' },
        { value: 'Admin', label: 'Adminler' },
        { value: 'Editor', label: 'Editörler' }
    ];

    get isValid(): boolean {
        if (!this.title.trim() || !this.message.trim()) return false;
        if (this.targetType === 'Role' && !this.targetRole) return false;
        return true;
    }

    async sendNotification(): Promise<void> {
        if (!this.isValid) {
            this.toaster.warning('Lütfen tüm zorunlu alanları doldurun');
            return;
        }

        const confirmed = await this.toaster.confirm(
            `${this.targetType === 'All' ? 'Tüm kullanıcılara' : this.targetRole + ' rolündeki kullanıcılara'} bildirim göndermek istediğinizden emin misiniz?`
        );

        if (!confirmed) return;

        this.sending.set(true);
        this.result.set(null);

        const request: BulkNotificationRequest = {
            title: this.title.trim(),
            message: this.message.trim(),
            priority: this.priority,
            targetType: this.targetType,
            targetRole: this.targetType === 'Role' ? this.targetRole : undefined,
            actionUrl: this.actionUrl.trim() || undefined,
            sendEmail: this.sendEmail
        };

        this.http.post<BulkNotificationResult>(`${this.apiUrl}/v1/notifications/bulk`, request).subscribe({
            next: (response) => {
                this.result.set(response);
                this.sending.set(false);

                if (response.success) {
                    this.toaster.success(`${response.totalSent} kullanıcıya bildirim gönderildi`);
                    this.resetForm();
                } else {
                    this.toaster.warning('Bildirim gönderildi ancak bazı hatalar oluştu');
                }
            },
            error: (err: Error) => {
                console.error('Error sending bulk notification:', err);
                this.toaster.error('Bildirim gönderilirken hata oluştu');
                this.sending.set(false);
            }
        });
    }

    resetForm(): void {
        this.title = '';
        this.message = '';
        this.priority = 2;
        this.targetType = 'All';
        this.targetRole = '';
        this.actionUrl = '';
        this.sendEmail = false;
        this.result.set(null);
    }

    navigateBack(): void {
        this.router.navigate(['/admin/notifications/all']);
    }
}
