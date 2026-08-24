import { Component, OnInit, inject, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCardModule } from '@angular/material/card';
import { BaseComponent } from '../../../../core/components/base.component';
import { CmsService } from '../../../../core/services/cms.service';
import { NewsletterSubscriber } from '../../../../core/models/cms.models';
import { finalize } from 'rxjs/operators';
import { DatePipe } from '@angular/common';

@Component({
    selector: 'app-newsletter-list',
    standalone: true,
    imports: [
        CommonModule,
        MatTableModule,
        MatPaginatorModule,
        MatSortModule,
        MatButtonModule,
        MatIconModule,
        MatTooltipModule,
        MatCardModule,
        DatePipe
    ],
    templateUrl: './newsletter-list.component.html',
    styleUrls: ['./newsletter-list.component.scss']
})
export class NewsletterListComponent extends BaseComponent implements OnInit, AfterViewInit {
    private cmsService = inject(CmsService);

    @ViewChild(MatPaginator) paginator!: MatPaginator;
    @ViewChild(MatSort) sort!: MatSort;

    dataSource = new MatTableDataSource<NewsletterSubscriber>([]);
    displayedColumns = ['email', 'source', 'isActive', 'createdAt', 'actions'];

    ngOnInit() {
        this.loadSubscribers();
    }

    ngAfterViewInit() {
        this.dataSource.paginator = this.paginator;
        this.dataSource.sort = this.sort;
    }

    loadSubscribers() {
        this.loading.set(true);
        this.cmsService.getNewsletterSubscribers()
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: (data) => {
                    this.dataSource.data = data;
                },
                error: (err) => {
                    this.handleError(err, 'Aboneler yüklenirken hata oluştu');
                }
            });
    }

    exportToCsv() {
        const data = this.dataSource.data;
        if (data.length === 0) {
            this.toaster.info('Dışa aktarılacak veri yok', 3000);
            return;
        }

        const csvRows = [];
        const headers = ['Email', 'Kaynak', 'Durum', 'Kayıt Tarihi'];
        csvRows.push(headers.join(','));

        for (const row of data) {
            const values = [
                `"${row.email}"`,
                `"${row.source || ''}"`,
                `"${row.isActive ? 'Aktif' : 'Pasif'}"`,
                `"${new Date(row.createdAt).toLocaleDateString('tr-TR')}"`
            ];
            csvRows.push(values.join(','));
        }

        const csvContent = '\uFEFF' + csvRows.join('\n'); // Add BOM for Excel
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.setAttribute('href', url);
        link.setAttribute('download', `newsletter_subscribers_${new Date().toISOString().split('T')[0]}.csv`);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }

    async deleteSubscriber(subscriber: NewsletterSubscriber) {
        const confirmed = await this.toaster.confirm(
            `${subscriber.email} kullanıcısını silmek istediğinize emin misiniz? Bu işlem geri alınamaz!`,
            'Aboneliği Sil',
            'Sil',
            'İptal'
        );

        if (confirmed) {
            this.loading.set(true);
            this.cmsService.deleteNewsletterSubscriber(subscriber.id, true) // hardDelete = true
                .pipe(finalize(() => this.loading.set(false)))
                .subscribe({
                    next: () => {
                        this.toaster.success('Kullanıcı başarıyla silindi', 3000);
                        this.loadSubscribers(); // Refresh list
                    },
                    error: (err) => {
                        this.handleError(err, 'Silme işlemi başarısız');
                    }
                });
        }
    }
}
