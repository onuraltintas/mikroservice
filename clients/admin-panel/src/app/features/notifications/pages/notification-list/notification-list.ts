import { Component, OnInit, inject, Inject, ChangeDetectorRef, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatTableModule } from '@angular/material/table';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup, FormsModule } from '@angular/forms';
import { SelectionModel } from '@angular/cdk/collections';
import { NotificationService, Notification } from '../../../../core/services/notification.service';
import { formatDistanceToNow } from 'date-fns';
import { tr } from 'date-fns/locale';
import { ToasterService } from '../../../../core/services/toaster.service';

type FilterStatus = 'all' | 'unread' | 'read';

interface CategoryInfo {
    key: string;
    label: string;
    icon: string;
}

@Component({
    selector: 'app-notification-list',
    standalone: true,
    imports: [
        CommonModule,
        MatIconModule,
        MatButtonModule,
        MatDividerModule,
        MatTableModule,
        MatCheckboxModule,
        MatDialogModule,
        MatTooltipModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        ReactiveFormsModule,
        FormsModule
    ],
    templateUrl: './notification-list.html',
    styleUrl: './notification-list.scss'
})
export class NotificationListComponent implements OnInit {
    notificationService = inject(NotificationService);
    toaster = inject(ToasterService);
    dialog = inject(MatDialog);
    private cdr = inject(ChangeDetectorRef);

    displayedColumns: string[] = ['select', 'status', 'content', 'date', 'actions'];
    selection = new SelectionModel<Notification>(true, []);

    // Selection of active filters
    activeCategory = signal<string>('All');
    activeStatus = signal<FilterStatus>('all');
    searchQuery = signal<string>('');

    // Predefined mapping for known categories
    private categoryMap: Record<string, { label: string, icon: string }> = {
        'All': { label: 'Tüm Kategoriler', icon: 'list' },
        'SupportRequest': { label: 'Destek Talepleri', icon: 'support_agent' },
        'System': { label: 'Sistem', icon: 'settings_suggest' },
        'Account': { label: 'Hesap İşlemleri', icon: 'person' },
        'Info': { label: 'Bilgi', icon: 'info' }
    };

    // Dynamically computed categories based on actual data
    categories = computed<CategoryInfo[]>(() => {
        const notifications = this.notificationService.notifications();
        // Get unique types from notifications
        const uniqueTypes = Array.from(new Set(notifications.map(n => n.type)));

        // Start with the 'All' option
        const result: CategoryInfo[] = [
            { key: 'All', ...this.categoryMap['All'] }
        ];

        // Add known and discovered categories
        uniqueTypes.forEach(type => {
            if (this.categoryMap[type]) {
                result.push({ key: type, ...this.categoryMap[type] });
            } else {
                // Discover and auto-format unknown categories
                result.push({
                    key: type,
                    label: type.charAt(0).toUpperCase() + type.slice(1),
                    icon: 'notifications'
                });
            }
        });

        // Ensure uniqueness (in case 'All' or others were somehow in the data)
        return result.filter((cat, index, self) =>
            index === self.findIndex((t) => t.key === cat.key)
        );
    });

    filteredNotifications = computed(() => {
        const notifications = this.notificationService.notifications();
        const category = this.activeCategory();
        const status = this.activeStatus();
        const search = this.searchQuery().toLowerCase().trim();

        return notifications.filter(n => {
            const matchesCategory = category === 'All' || n.type === category;
            const matchesStatus = status === 'all' ||
                (status === 'unread' && !n.isRead) ||
                (status === 'read' && n.isRead);
            const matchesSearch = !search ||
                n.title.toLowerCase().includes(search) ||
                n.message.toLowerCase().includes(search);

            return matchesCategory && matchesStatus && matchesSearch;
        });
    });

    ngOnInit() {
        this.notificationService.fetchNotifications();
    }

    setCategory(category: string) {
        this.activeCategory.set(category);
        this.selection.clear();
    }

    setStatus(status: FilterStatus) {
        this.activeStatus.set(status);
        this.selection.clear();
    }

    onSearchChange(event: Event) {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery.set(value);
        this.selection.clear();
    }

    getCategoryLabel(key: string): string {
        const cat = this.categories().find(c => c.key === key);
        return cat ? cat.label : key;
    }

    getActiveCategoryIcon(): string {
        const cat = this.categories().find(c => c.key === this.activeCategory());
        return cat?.icon || 'list';
    }

    getActiveCategoryLabel(): string {
        const cat = this.categories().find(c => c.key === this.activeCategory());
        return cat?.label || '';
    }

    isAllSelected() {
        const numSelected = this.selection.selected.length;
        const numRows = this.filteredNotifications().length;
        return numRows > 0 && numSelected === numRows;
    }

    toggleAllRows() {
        if (this.isAllSelected()) {
            this.selection.clear();
            return;
        }
        this.selection.select(...this.filteredNotifications());
    }

    formatTime(date: Date) {
        try {
            return formatDistanceToNow(new Date(date), { addSuffix: true, locale: tr });
        } catch {
            return '';
        }
    }

    viewDetail(notification: Notification) {
        if (!notification.isRead) {
            this.notificationService.markAsRead(notification.id).subscribe(() => {
                this.notificationService.fetchNotifications();
            });
        }

        setTimeout(() => {
            this.dialog.open(NotificationDetailDialog, {
                data: notification,
                width: '600px',
                maxWidth: '95vw'
            });
        });
    }

    markAsRead(id: string) {
        this.notificationService.markAsRead(id).subscribe(() => {
            this.notificationService.fetchNotifications();
            this.toaster.success('Bildirim okundu olarak işaretlendi.');
        });
    }

    deleteNotification(id: string) {
        this.toaster.confirm('Bildirimi Sil', 'Bu bildirimi silmek istediğinize emin misiniz?').then(confirm => {
            if (confirm) {
                this.notificationService.deleteNotification(id).subscribe(() => {
                    this.notificationService.fetchNotifications();
                    this.toaster.success('Bildirim silindi.');
                });
            }
        });
    }

    deleteSelected() {
        if (this.selection.selected.length === 0) return;

        this.toaster.confirm('Seçilileri Sil', `${this.selection.selected.length} bildirimi silmek istediğinize emin misiniz?`).then(confirm => {
            if (confirm) {
                const promises = this.selection.selected.map(n => this.notificationService.deleteNotification(n.id).toPromise());
                Promise.all(promises).then(() => {
                    this.notificationService.fetchNotifications();
                    this.selection.clear();
                    this.toaster.success('Seçili bildirimler silindi.');
                });
            }
        });
    }

    markAllAsRead() {
        this.notificationService.markAllAsRead().subscribe(() => {
            this.notificationService.fetchNotifications();
            this.toaster.success('Tüm bildirimler okundu olarak işaretlendi.');
        });
    }
}

@Component({
    selector: 'app-notification-detail-dialog',
    standalone: true,
    imports: [CommonModule, MatIconModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule],
    template: `
    <div class="p-6">
      <div class="flex items-center justify-between mb-6">
        <div class="flex items-center gap-3">
          <div [ngClass]="{
            'bg-blue-100 text-blue-600': data.type === 'SupportRequest',
            'bg-orange-100 text-orange-600': data.type === 'System',
            'bg-green-100 text-green-600': data.type === 'Account' || data.type === 'Info'
          }" class="w-12 h-12 rounded-2xl flex items-center justify-center">
            <mat-icon>{{ data.type === 'SupportRequest' ? 'support_agent' : data.type === 'Account' ? 'person' : 'info' }}</mat-icon>
          </div>
          <div>
            <h2 class="text-xl font-bold mb-0">Bildirim Detayı</h2>
            <p class="text-[10px] text-gray-400 uppercase tracking-widest mb-0">{{ getCategoryLabel(data.type) }}</p>
          </div>
        </div>
        <button mat-icon-button mat-dialog-close>
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <div class="space-y-6">
        <div>
          <label class="text-[11px] font-bold text-gray-400 uppercase tracking-wider mb-1 block">Konu</label>
          <p class="text-lg font-bold text-gray-900 dark:text-white">{{ data.title }}</p>
        </div>

        <div>
          <label class="text-[11px] font-bold text-gray-400 uppercase tracking-wider mb-1 block">Mesaj İçeriği</label>
          <p class="text-sm text-gray-700 dark:text-gray-300 leading-relaxed bg-gray-50 dark:bg-gray-800 p-5 rounded-2xl border border-gray-100 dark:border-gray-700 whitespace-pre-wrap">
            {{ data.message }}
          </p>
        </div>

        <div *ngIf="data.type === 'SupportRequest' && data.relatedEntityId && !showReplyForm" class="pt-2">
            <button mat-flat-button color="primary" class="!rounded-xl !px-6 w-full py-6" (click)="toggleReplyForm()">
                <mat-icon class="mr-2">reply</mat-icon> Cevapla (E-posta ile)
            </button>
        </div>

        <div *ngIf="showReplyForm" class="bg-indigo-50 dark:bg-indigo-900/10 p-5 rounded-2xl border border-indigo-100 dark:border-indigo-800">
            <form [formGroup]="replyForm" (ngSubmit)="sendReply()">
                <label class="text-[11px] font-bold text-indigo-600 dark:text-indigo-400 uppercase tracking-wider mb-3 block">Yanıtınız</label>
                <mat-form-field appearance="outline" class="w-full">
                    <textarea matInput formControlName="message" rows="5" placeholder="Kullanıcıya gönderilecek e-posta içeriğini yazın..."></textarea>
                    <mat-hint align="end">{{replyForm.value.message?.length || 0}}/2000</mat-hint>
                </mat-form-field>
                
                <div class="flex gap-2 mt-4">
                    <button type="button" mat-stroked-button class="flex-1 !rounded-xl" (click)="showReplyForm = false" [disabled]="isSubmitting">İptal</button>
                    <button type="submit" mat-flat-button color="primary" class="flex-2 !rounded-xl !px-8" [disabled]="replyForm.invalid || isSubmitting">
                        <mat-icon *ngIf="!isSubmitting" class="mr-2">send</mat-icon>
                        {{ isSubmitting ? 'Gönderiliyor...' : 'Gönder' }}
                    </button>
                </div>
            </form>
        </div>

        <div class="flex items-center justify-between text-xs text-gray-400 pt-2 border-t border-gray-50 dark:border-gray-800">
            <span>{{ data.createdAt | date:'dd.MM.yyyy HH:mm' }}</span>
            <span *ngIf="data.isRead" class="flex items-center gap-1 text-green-500 font-medium">
                <mat-icon class="!text-sm !w-4 !h-4 !text-[16px]">done_all</mat-icon> Okundu
            </span>
        </div>
      </div>

      <div *ngIf="!showReplyForm" class="mt-8 flex justify-end">
        <button mat-flat-button color="primary" mat-dialog-close class="!rounded-xl !px-8 !py-6 font-bold">Kapat</button>
      </div>
    </div>
  `
})
export class NotificationDetailDialog {
    showReplyForm = false;
    isSubmitting = false;
    replyForm: FormGroup;

    // We can also make this dynamic if needed, but for now we share the same mapping logic
    private categoryMap: Record<string, { label: string, icon: string }> = {
        'SupportRequest': { label: 'Destek Talepleri', icon: 'support_agent' },
        'System': { label: 'Sistem', icon: 'settings_suggest' },
        'Account': { label: 'Hesap İşlemleri', icon: 'person' },
        'Info': { label: 'Bilgi', icon: 'info' }
    };

    private fb = inject(FormBuilder);
    private notificationService = inject(NotificationService);
    private toaster = inject(ToasterService);
    private dialogRef = inject(MatDialogRef<NotificationDetailDialog>);
    private cdr = inject(ChangeDetectorRef);

    constructor(@Inject(MAT_DIALOG_DATA) public data: Notification) {
        this.replyForm = this.fb.group({
            message: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(2000)]]
        });
    }

    getCategoryLabel(key: string): string {
        return this.categoryMap[key]?.label || key.charAt(0).toUpperCase() + key.slice(1);
    }

    toggleReplyForm() {
        this.showReplyForm = true;
        this.cdr.detectChanges();
    }

    sendReply() {
        if (this.replyForm.invalid || !this.data.relatedEntityId) return;

        this.isSubmitting = true;
        this.cdr.detectChanges();

        this.notificationService.replyToSupportRequest(this.data.relatedEntityId, this.replyForm.value.message)
            .subscribe({
                next: () => {
                    this.toaster.success('Cevabınız kullanıcıya e-posta olarak gönderildi.');
                    setTimeout(() => this.dialogRef.close());
                },
                error: (err) => {
                    this.toaster.error('Cevap gönderilirken bir hata oluştu.');
                    this.isSubmitting = false;
                    this.cdr.detectChanges();
                }
            });
    }
}
