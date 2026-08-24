import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { NotificationService, NotificationType, NotificationPriority } from '../../../core/services/notification.service';

@Component({
    selector: 'app-notification-detail-dialog',
    standalone: true,
    imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule, MatChipsModule],
    template: `
    <h2 mat-dialog-title>
        <div class="header-content">
            <div class="header-title">
                <mat-icon [style.color]="getPriorityColor(data.priority)">{{ getIcon(data.type) }}</mat-icon>
                <span>{{ getTypeLabel(data.type) }}</span>
            </div>
            <mat-chip [style.backgroundColor]="getPriorityColor(data.priority)" [style.color]="'white'" class="priority-chip" selected>
                {{ getPriorityLabel(data.priority) }}
            </mat-chip>
        </div>
    </h2>
    <mat-dialog-content class="dialog-content">
        <div class="detail-section user-section">
            <div class="label">İlgili Kullanıcı</div>
            <div class="value user-value">
                <mat-icon class="small-icon">person</mat-icon>
                {{ data.userName }} ({{ data.userRole }})
            </div>
            <div class="value email-value">{{ data.userEmail }}</div>
        </div>

        <div class="detail-section">
            <div class="label">Konu</div>
            <div class="value title-value">{{ data.title }}</div>
        </div>

        <div class="detail-section">
            <div class="label">Mesaj</div>
            <div class="value message-box">{{ data.message }}</div>
        </div>

        <div class="footer-info">
            <span class="date">
                <mat-icon class="tiny-icon">calendar_today</mat-icon>
                {{ data.createdAt | date:'dd.MM.yyyy HH:mm' }}
            </span>
            <span class="status" [class.read]="data.isRead" [class.unread]="!data.isRead">
                <mat-icon class="tiny-icon">{{ data.isRead ? 'mark_email_read' : 'mark_email_unread' }}</mat-icon>
                {{ data.isRead ? 'Okundu' : 'Okunmadı' }}
            </span>
        </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>Kapat</button>
    </mat-dialog-actions>
  `,
    styles: [`
    .header-content { display: flex; justify-content: space-between; align-items: center; width: 100%; border-bottom: 1px solid #eee; padding-bottom: 16px; margin-bottom: 8px; }
    .header-title { display: flex; align-items: center; gap: 12px; font-weight: 500; font-size: 1.1em; color: #2c3e50; }
    .dialog-content { min-width: 500px; max-width: 800px; padding-top: 24px !important; }
    
    .priority-chip { min-height: 24px; font-size: 0.85em; font-weight: 500; }

    .detail-section { margin-bottom: 24px; }
    .label { font-size: 0.75em; color: #95a5a6; text-transform: uppercase; letter-spacing: 0.8px; margin-bottom: 6px; font-weight: 600; }
    
    .user-section { background: #f8f9fa; padding: 12px; border-radius: 8px; border: 1px solid #eee; }
    .user-value { display: flex; align-items: center; gap: 8px; font-weight: 600; font-size: 1.05em; color: #2c3e50; }
    .small-icon { font-size: 20px; width: 20px; height: 20px; color: #7f8c8d; }
    .email-value { color: #7f8c8d; font-size: 0.9em; margin-left: 28px; margin-top: 2px; }
    
    .title-value { font-size: 1.3em; font-weight: 600; color: #2c3e50; line-height: 1.3; }
    
    .message-box { background: #fff; border-left: 4px solid #3498db; padding: 16px; border-radius: 0 4px 4px 0; white-space: pre-wrap; line-height: 1.6; color: #34495e; font-size: 1.05em; box-shadow: 0 2px 5px rgba(0,0,0,0.03); }
    
    .footer-info { display: flex; justify-content: space-between; margin-top: 32px; padding-top: 16px; border-top: 1px solid #eee; font-size: 0.9em; color: #7f8c8d; }
    .date, .status { display: flex; align-items: center; gap: 6px; }
    .tiny-icon { font-size: 16px; width: 16px; height: 16px; }
    
    .status.read { color: #27ae60; font-weight: 600; }
    .status.unread { color: #e74c3c; font-weight: 600; }
  `]
})
export class NotificationDetailDialogComponent {
    private notificationService = inject(NotificationService);

    constructor(
        public dialogRef: MatDialogRef<NotificationDetailDialogComponent>,
        @Inject(MAT_DIALOG_DATA) public data: any
    ) { }

    getTypeLabel(type: NotificationType): string {
        return this.notificationService.getNotificationTypeLabel(type);
    }

    getIcon(type: NotificationType): string {
        return this.notificationService.getNotificationIcon(type);
    }

    getPriorityLabel(priority: NotificationPriority): string {
        switch (priority) {
            case NotificationPriority.Low: return 'Düşük Öncelik';
            case NotificationPriority.Normal: return 'Normal';
            case NotificationPriority.High: return 'Yüksek Öncelik';
            case NotificationPriority.Urgent: return 'ACİL';
            default: return 'Normal';
        }
    }

    getPriorityColor(priority: NotificationPriority): string {
        switch (priority) {
            case NotificationPriority.Low: return '#95a5a6'; // Gray
            case NotificationPriority.Normal: return '#3498db'; // Blue
            case NotificationPriority.High: return '#f39c12'; // Orange
            case NotificationPriority.Urgent: return '#e74c3c'; // Red
            default: return '#3498db';
        }
    }
}
