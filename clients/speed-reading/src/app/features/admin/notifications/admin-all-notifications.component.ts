import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpParams } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { SelectionModel } from '@angular/cdk/collections';
import { Router } from '@angular/router';
import { NotificationService, NotificationType, NotificationPriority } from '../../../core/services/notification.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { environment } from '../../../../environments/environment';
import { NotificationDetailDialogComponent } from './notification-detail-dialog.component';
import { firstValueFrom } from 'rxjs';

interface AdminNotification {
    id: string;
    type: NotificationType;
    priority: NotificationPriority;
    title: string;
    message: string;
    actionUrl?: string;
    isRead: boolean;
    createdAt: string;
    userName: string;
    userEmail: string;
    userRole: string;
}

@Component({
    selector: 'app-admin-all-notifications',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        MatCardModule,
        MatButtonModule,
        MatIconModule,
        MatTableModule,
        MatPaginatorModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatDatepickerModule,
        MatNativeDateModule,
        MatChipsModule,
        MatProgressSpinnerModule,
        MatDialogModule,
        MatCheckboxModule
    ],
    templateUrl: './admin-all-notifications.component.html',
    styleUrls: ['./admin-all-notifications.component.scss']
})
export class AdminAllNotificationsComponent implements OnInit {
    private http = inject(HttpClient);
    notificationService = inject(NotificationService);
    private toaster = inject(ToasterService);
    private router = inject(Router);
    private dialog = inject(MatDialog);
    private apiUrl = environment.apiUrl;

    notifications = signal<AdminNotification[]>([]);
    loading = signal(false);
    totalCount = signal(0);
    selection = new SelectionModel<AdminNotification>(true, []);

    // Filters
    searchTerm = '';
    selectedRole = '';
    selectedType: number | null = null;
    selectedReadStatus: boolean | null = null;
    fromDate: Date | null = null;
    toDate: Date | null = null;

    // Pagination
    pageNumber = 1;
    pageSize = 20;

    displayedColumns = ['select', 'user', 'type', 'title', 'status', 'createdAt', 'actions'];

    roles = [
        { value: '', label: 'Tümü' },
        { value: 'Student', label: 'Öğrenci' },
        { value: 'Teacher', label: 'Öğretmen' },
        { value: 'Admin', label: 'Admin' },
        { value: 'Editor', label: 'Editör' }
    ];

    notificationTypes = [
        { value: null, label: 'Tümü' },
        { value: 1, label: 'Yeni Ödev' },
        { value: 4, label: 'Egzersiz Tamamlandı' },
        { value: 5, label: 'Milestone' },
        { value: 9, label: 'Sistem Duyurusu' },
        { value: 11, label: 'Başarı Kazanıldı' },
        { value: 15, label: 'Yeni Kullanıcı' },
        { value: 16, label: 'Sistem Hatası' }
    ];

    readStatuses = [
        { value: null, label: 'Tümü' },
        { value: false, label: 'Okunmamış' },
        { value: true, label: 'Okunmuş' }
    ];

    ngOnInit(): void {
        this.loadNotifications();
    }

    loadNotifications(): void {
        this.loading.set(true);
        this.selection.clear();

        let params = new HttpParams()
            .set('pageNumber', this.pageNumber.toString())
            .set('pageSize', this.pageSize.toString());

        if (this.searchTerm) params = params.set('searchTerm', this.searchTerm);
        if (this.selectedRole) params = params.set('userRole', this.selectedRole);
        if (this.selectedType !== null) params = params.set('type', this.selectedType.toString());
        if (this.selectedReadStatus !== null) params = params.set('isRead', this.selectedReadStatus.toString());
        if (this.fromDate) params = params.set('fromDate', this.fromDate.toISOString());
        if (this.toDate) params = params.set('toDate', this.toDate.toISOString());

        this.http.get<any>(`${this.apiUrl}/v1/notifications/all`, { params }).subscribe({
            next: (response: any) => {
                const data = response?.data || response;
                if (data && data.items) {
                    this.notifications.set(data.items);
                    this.totalCount.set(data.totalCount || 0);
                } else {
                    console.warn('Unexpected response structure:', response);
                    this.notifications.set([]);
                    this.totalCount.set(0);
                }
                this.loading.set(false);
            },
            error: (err: Error) => {
                console.error('Error loading notifications:', err);
                this.toaster.error('Bildirimler yüklenirken hata oluştu');
                this.loading.set(false);
            }
        });
    }

    onSearch(): void {
        this.pageNumber = 1;
        this.loadNotifications();
    }

    onPageChange(event: PageEvent): void {
        this.pageNumber = event.pageIndex + 1;
        this.pageSize = event.pageSize;
        this.loadNotifications();
    }

    clearFilters(): void {
        this.searchTerm = '';
        this.selectedRole = '';
        this.selectedType = null;
        this.selectedReadStatus = null;
        this.fromDate = null;
        this.toDate = null;
        this.pageNumber = 1;
        this.loadNotifications();
    }

    /** Whether the number of selected elements matches the total number of rows. */
    isAllSelected() {
        const numSelected = this.selection.selected.length;
        const numRows = this.notifications().length;
        return numSelected === numRows;
    }

    /** Selects all rows if they are not all selected; otherwise clear selection. */
    masterToggle() {
        this.isAllSelected() ?
            this.selection.clear() :
            this.notifications().forEach(row => this.selection.select(row));
    }

    async deleteNotification(notification: AdminNotification, event: Event) {
        event.stopPropagation();

        const confirmed = await this.toaster.confirm(
            `"${notification.title}" bildirimini silmek istediğinize emin misiniz?`,
            'Bildirimi Sil',
            'Sil',
            'İptal'
        );

        if (confirmed) {
            this.notificationService.deleteNotification(notification.id).subscribe({
                next: () => {
                    this.notifications.update(items => items.filter(n => n.id !== notification.id));
                    this.selection.deselect(notification);
                    this.totalCount.update(c => c - 1);
                    this.toaster.success('Bildirim silindi');
                },
                error: (err) => {
                    console.error('Delete error', err);
                    this.toaster.error('Silme işlemi başarısız');
                }
            });
        }
    }

    async deleteSelectedNotifications() {
        const count = this.selection.selected.length;
        if (count === 0) return;

        const confirmed = await this.toaster.confirm(
            `${count} adet bildirimi silmek istediğinize emin misiniz?`,
            'Bildirimleri Sil',
            'Sil',
            'İptal'
        );

        if (confirmed) {
            this.loading.set(true);
            try {
                const promises = this.selection.selected.map(n =>
                    firstValueFrom(this.notificationService.deleteNotification(n.id))
                );
                await Promise.all(promises);

                const deletedIds = this.selection.selected.map(n => n.id);
                this.notifications.update(items => items.filter(n => !deletedIds.includes(n.id)));
                this.totalCount.update(c => c - count);
                this.selection.clear();
                this.toaster.success(`${count} bildirim silindi`);
            } catch (err) {
                console.error('Bulk delete error', err);
                this.toaster.error('Bazı bildirimler silinemedi');
                this.loadNotifications();
            } finally {
                this.loading.set(false);
            }
        }
    }

    getNotificationIcon(type: NotificationType): string {
        return this.notificationService.getNotificationIcon(type);
    }

    getNotificationTypeLabel(type: NotificationType): string {
        return this.notificationService.getNotificationTypeLabel(type);
    }

    getPriorityColor(priority: NotificationPriority): string {
        return this.notificationService.getPriorityColor(priority);
    }

    getRoleColor(role: string): string {
        switch (role) {
            case 'Admin': return 'warn';
            case 'Teacher': return 'primary';
            case 'Student': return 'accent';
            default: return '';
        }
    }

    getRelativeTime(dateString: string): string {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMins / 60);
        const diffDays = Math.floor(diffHours / 24);

        if (diffMins < 1) return 'Şimdi';
        if (diffMins < 60) return `${diffMins} dk önce`;
        if (diffHours < 24) return `${diffHours} saat önce`;
        if (diffDays < 7) return `${diffDays} gün önce`;
        return date.toLocaleDateString('tr-TR');
    }

    navigateToBulkSend(): void {
        this.router.navigate(['/admin/notifications/send']);
    }

    openDetail(notification: AdminNotification): void {
        this.dialog.open(NotificationDetailDialogComponent, {
            width: '600px',
            maxWidth: '90vw',
            data: notification
        });

        if (!notification.isRead) {
            this.notificationService.markAsRead(notification.id).subscribe({
                next: () => {
                    this.notifications.update(notifications =>
                        notifications.map(n =>
                            n.id === notification.id ? { ...n, isRead: true } : n
                        )
                    );
                },
                error: (err) => console.error('Error marking notification as read:', err)
            });
        }
    }
}
