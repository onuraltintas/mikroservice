import { Component, OnInit, inject, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { BaseComponent } from '../../../../core/components/base.component';
import { CmsService } from '../../../../core/services/cms.service';
import { BlogPost } from '../../../../core/models/cms.models';
import { finalize } from 'rxjs/operators';
import { DatePipe } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
    selector: 'app-blog-list',
    standalone: true,
    imports: [
        CommonModule,
        MatTableModule,
        MatPaginatorModule,
        MatSortModule,
        MatButtonModule,
        MatButtonModule,
        MatIconModule,
        MatTooltipModule,
        MatCardModule,
        MatChipsModule,
        RouterModule,
        DatePipe
    ],
    templateUrl: './blog-list.component.html',
    styleUrls: ['./blog-list.component.scss']
})
export class BlogListComponent extends BaseComponent implements OnInit, AfterViewInit {
    private cmsService = inject(CmsService);

    @ViewChild(MatPaginator) paginator!: MatPaginator;
    @ViewChild(MatSort) sort!: MatSort;

    dataSource = new MatTableDataSource<BlogPost>([]);
    displayedColumns = ['title', 'author', 'tags', 'isPublished', 'createdAt', 'actions'];

    ngOnInit() {
        this.loadBlogPosts();
    }

    ngAfterViewInit() {
        this.dataSource.paginator = this.paginator;
        this.dataSource.sort = this.sort;
    }

    loadBlogPosts() {
        this.loading.set(true);
        this.cmsService.getBlogPosts()
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: (data) => {
                    this.dataSource.data = data;
                },
                error: (err) => {
                    this.handleError(err, 'Blog yazıları yüklenirken hata oluştu');
                }
            });
    }

    async deleteBlogPost(post: BlogPost) {
        const confirmed = await this.toaster.confirm(
            `"${post.title}" yazısını silmek istediğinize emin misiniz?`
        );

        if (confirmed) {
            this.cmsService.deleteBlogPost(post.id).subscribe({
                next: () => {
                    this.toaster.success('Yazı başarıyla silindi', 3000);
                    this.loadBlogPosts();
                },
                error: (err) => {
                    console.error(err);
                    this.toaster.error('Yazı silinirken hata oluştu', 3000);
                }
            });
        }
    }
}
