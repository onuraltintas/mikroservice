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
import { Page } from '../../../../core/models/cms.models';
import { finalize } from 'rxjs/operators';
import { DatePipe } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
    selector: 'app-page-list',
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
        RouterModule,
        DatePipe
    ],
    templateUrl: './page-list.component.html',
    styleUrls: ['./page-list.component.scss']
})
export class PageListComponent extends BaseComponent implements OnInit, AfterViewInit {
    private cmsService = inject(CmsService);

    @ViewChild(MatPaginator) paginator!: MatPaginator;
    @ViewChild(MatSort) sort!: MatSort;

    dataSource = new MatTableDataSource<Page>([]);
    displayedColumns = ['title', 'slug', 'isPublished', 'updatedAt', 'actions'];

    ngOnInit() {
        this.loadPages();
    }

    ngAfterViewInit() {
        this.dataSource.paginator = this.paginator;
        this.dataSource.sort = this.sort;
    }

    loadPages() {
        this.loading.set(true);
        this.cmsService.getPages()
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: (data) => {
                    this.dataSource.data = data;
                },
                error: (err) => {
                    this.handleError(err, 'Sayfalar yüklenirken hata oluştu');
                }
            });
    }

    async deletePage(page: Page) {
        const confirmed = await this.toaster.confirm(
            `"${page.title}" sayfasını silmek istediğinize emin misiniz? Bu işlem geri alınamaz.`
        );

        if (confirmed) {
            this.cmsService.deletePage(page.id).subscribe({
                next: () => {
                    this.toaster.success('Sayfa başarıyla silindi', 3000);
                    this.loadPages();
                },
                error: (err) => {
                    console.error(err);
                    this.toaster.error('Sayfa silinirken hata oluştu', 3000);
                }
            });
        }
    }
}
