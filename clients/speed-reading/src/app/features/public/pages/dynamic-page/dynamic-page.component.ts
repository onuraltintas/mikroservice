import { Component, OnInit, inject, SecurityContext } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer } from '@angular/platform-browser';
import { PublicCmsService, PageDto } from '../../../../core/services/public-cms.service';
import { SeoService } from '../../../../core/services/seo.service';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SafeHtmlPipe } from '../../../../shared/pipes/safe-html.pipe';

@Component({
    selector: 'app-dynamic-page',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule,
        SafeHtmlPipe
    ],
    templateUrl: './dynamic-page.component.html',
    styleUrls: ['./dynamic-page.component.scss']
})
export class DynamicPageComponent implements OnInit {
    private cmsService = inject(PublicCmsService);
    private route = inject(ActivatedRoute);
    private router = inject(Router);
    private sanitizer = inject(DomSanitizer);
    private seo = inject(SeoService);

    page: PageDto | null = null;
    loading = true;



    ngOnInit(): void {
        this.route.params.subscribe(params => {
            const slug = params['slug'];
            if (slug) {
                this.loadPage(slug);
            }
        });
    }

    loadPage(slug: string): void {
        this.loading = true;
        this.cmsService.getPage(slug).subscribe({
            next: (page) => {
                if (!page.isPublished) {
                    this.router.navigate(['/error/404']);
                    return;
                }
                this.page = page;
                this.updateMetaTags(page);
                this.loading = false;
            },
            error: (error) => {
                console.error('Error loading page:', error);
                this.router.navigate(['/error/404']);
                this.loading = false;
            }
        });
    }

    updateMetaTags(page: PageDto): void {
        const url = `${window.location.origin}/${page.slug}`;

        this.seo.updateTags({
            title: page.seoSettings?.metaTitle || page.title,
            description: page.seoSettings?.metaDescription || '',
            keywords: page.seoSettings?.metaKeywords,
            url: url,
            type: 'website'
        });

        this.seo.setCanonicalUrl(url);
        this.seo.setNoIndex(page.seoSettings?.noIndex || false);

        this.seo.generateStructuredData('WebPage', {
            title: page.title,
            description: page.seoSettings?.metaDescription || '',
            url: url
        });
    }
}
