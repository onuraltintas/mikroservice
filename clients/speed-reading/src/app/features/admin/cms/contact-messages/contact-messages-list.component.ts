import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FormsModule } from '@angular/forms';
import { CmsService } from '../../../../core/services/cms.service';
import { ContactMessage } from '../../../../core/models/cms.models';
import { ContactMessageDetailComponent } from './contact-message-detail.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { DomSanitizer } from '@angular/platform-browser';

@Component({
    selector: 'app-contact-messages-list',
    standalone: true,
    imports: [
        CommonModule,
        MatTableModule,
        MatCardModule,
        MatButtonModule,
        MatIconModule,
        MatInputModule,
        MatFormFieldModule,
        MatSelectModule,
        MatTooltipModule,
        MatChipsModule,
        MatPaginatorModule,
        MatProgressSpinnerModule,
        MatDialogModule,
        FormsModule
    ],
    templateUrl: './contact-messages-list.component.html',
    styleUrls: ['./contact-messages-list.component.scss']
})
export class ContactMessagesListComponent implements OnInit {
    private cmsService = inject(CmsService);
    private dialog = inject(MatDialog);

    messages: ContactMessage[] = [];
    totalCount = 0;
    loading = signal(false);

    displayedColumns = ['status', 'sender', 'subject', 'date', 'replyStatus', 'actions'];

    pageNumber = 1;
    pageSize = 10;

    filters = {
        search: '',
        isRead: undefined as boolean | undefined,
        isReplied: undefined as boolean | undefined
    };

    ngOnInit() {
        this.loadMessages();
    }

    loadMessages() {
        this.loading.set(true);
        this.cmsService.getContactMessages(
            this.pageNumber,
            this.pageSize,
            this.filters.search || undefined,
            this.filters.isRead,
            this.filters.isReplied
        ).subscribe({
            next: (response) => {
                this.messages = response.items;
                this.totalCount = response.totalCount;
                this.loading.set(false);
            },
            error: (err) => {
                console.error('Error loading messages', err);
                this.loading.set(false);
            }
        });
    }

    onFilterChange() {
        this.pageNumber = 1; // Reset to first page
        this.loadMessages();
    }

    onPageChange(event: PageEvent) {
        this.pageNumber = event.pageIndex + 1;
        this.pageSize = event.pageSize;
        this.loadMessages();
    }

    openDetail(message: ContactMessage) {
        // Optimistic update: mark as read immediately in UI
        if (!message.isRead) {
            message.isRead = true;
            this.cmsService.markContactMessageAsRead(message.id, true).subscribe();
        }

        const dialogRef = this.dialog.open(ContactMessageDetailComponent, {
            width: '800px',
            maxWidth: '95vw',
            data: message
        });

        dialogRef.afterClosed().subscribe(result => {
            // If user replied in dialog, reload to show updated status
            if (result) {
                this.loadMessages();
            }
        });
    }

    deleteMessage(message: ContactMessage, event: Event) {
        event.stopPropagation(); // Prevent opening detail

        const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
            width: '400px',
            data: {
                title: 'Mesajı Sil',
                message: `'${message.subject}' başlıklı mesajı kalıcı olarak silmek istediğinize emin misiniz? Bu işlem geri alınamaz.`,
                confirmText: 'Sil',
                cancelText: 'Vazgeç'
            }
        });

        dialogRef.afterClosed().subscribe(confirmed => {
            if (confirmed) {
                this.loading.set(true);
                this.cmsService.deleteContactMessage(message.id).subscribe({
                    next: () => {
                        this.loadMessages(); // Reload list
                    },
                    error: (err) => {
                        console.error('Error deleting message', err);
                        this.loading.set(false);
                    }
                });
            }
        });
    }
}
