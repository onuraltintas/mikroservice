import { Component, Inject, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { MatTabsModule } from '@angular/material/tabs';
import { CmsService } from '../../../../core/services/cms.service';
import { ContactMessage } from '../../../../core/models/cms.models';
import { ToasterService } from '../../../../core/services/toaster.service';

@Component({
    selector: 'app-contact-message-detail',
    standalone: true,
    imports: [
        CommonModule,
        MatDialogModule,
        MatButtonModule,
        MatIconModule,
        MatFormFieldModule,
        MatInputModule,
        FormsModule,
        MatTabsModule,
    ],
    template: `
    <h2 mat-dialog-title>Mesaj Detayı</h2>
    <mat-dialog-content>
        <div class="message-meta mb-16">
            <div class="meta-row"><strong>Gönderen:</strong> {{ data.name }} ({{ data.email }})</div>
            <div class="meta-row"><strong>Tarih:</strong> {{ data.createdAt | date:'dd.MM.yyyy HH:mm' }}</div>
            <div class="meta-row"><strong>Konu:</strong> {{ data.subject }}</div>
        </div>

        <mat-tab-group>
            <mat-tab label="Mesaj İçeriği">
                <div class="message-body mt-16 p-16 bg-light border-rounded">
                    {{ data.message }}
                </div>
            </mat-tab>
            
            <mat-tab label="Yanıtla">
                <div class="reply-section mt-16">
                    <div *ngIf="data.isReplied" class="replied-info p-16 bg-success-light mb-16 border-rounded">
                        <div class="d-flex align-center gap-8 text-success font-bold mb-8">
                            <mat-icon>check_circle</mat-icon>
                            <span>Bu mesaj yanıtlanmış</span>
                        </div>
                        <div class="text-sm"><strong>Yanıt Tarihi:</strong> {{ data.repliedAt | date:'dd.MM.yyyy HH:mm' }}</div>
                        <hr class="my-8 opacity-20">
                        <div class="reply-content pre-wrap">{{ data.replyContent }}</div>
                    </div>

                    <mat-form-field appearance="outline" class="w-100" *ngIf="!data.isReplied">
                        <mat-label>Yanıtınız</mat-label>
                        <textarea matInput rows="8" [(ngModel)]="replyContent" placeholder="Yanıtınızı buraya yazın..."></textarea>
                    </mat-form-field>
                </div>
            </mat-tab>
        </mat-tab-group>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>Kapat</button>
        <button mat-flat-button color="primary" 
            *ngIf="!data.isReplied" 
            [disabled]="loading() || !replyContent.trim()" 
            (click)="sendReply()">
            <mat-icon *ngIf="!loading()">send</mat-icon>
            <span *ngIf="loading()">Gönderiliyor...</span>
            <span *ngIf="!loading()">Yanıtla & Gönder</span>
        </button>
    </mat-dialog-actions>
  `,
    styles: [`
    .meta-row { margin-bottom: 8px; font-size: 14px; }
    .bg-light { background-color: #f8f9fa; }
    .bg-success-light { background-color: #e8f5e9; }
    .text-success { color: #2e7d32; }
    .border-rounded { border-radius: 8px; border: 1px solid #e0e0e0; }
    .p-16 { padding: 16px; }
    .mt-16 { margin-top: 16px; }
    .mb-16 { margin-bottom: 16px; }
    .mb-8 { margin-bottom: 8px; }
    .w-100 { width: 100%; }
    .d-flex { display: flex; }
    .align-center { align-items: center; }
    .gap-8 { gap: 8px; }
    .pre-wrap { white-space: pre-wrap; }
    .font-bold { font-weight: 600; }
  `]
})
export class ContactMessageDetailComponent {
    private cmsService = inject(CmsService);
    private toaster = inject(ToasterService);

    replyContent = '';
    loading = signal(false);

    constructor(
        public dialogRef: MatDialogRef<ContactMessageDetailComponent>,
        @Inject(MAT_DIALOG_DATA) public data: ContactMessage
    ) { }

    sendReply() {
        if (!this.replyContent.trim()) return;

        this.loading.set(true);
        this.cmsService.replyToContactMessage(this.data.id, this.replyContent)
            .subscribe({
                next: () => {
                    this.toaster.success('Yanıt başarıyla gönderildi');
                    this.dialogRef.close(true);
                },
                error: (err) => {
                    console.error('Error sending reply', err);
                    this.toaster.error('Yanıt gönderilirken hata oluştu');
                    this.loading.set(false);
                }
            });
    }
}
