import { Component, OnInit, inject, SecurityContext } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
import { PublicCmsService, BlogPostDto } from '../../../../core/services/public-cms.service';
import { SeoService } from '../../../../core/services/seo.service';
import { calculateReadTime, getCategory, getCategoryColor } from '../../../../core/models/blog.model';
import { finalize } from 'rxjs/operators';
import { NavbarComponent } from '../../../../shared/components/navbar/navbar';
import { SafeHtmlPipe } from '../../../../shared/pipes/safe-html.pipe';
import { FooterComponent } from '../../../../shared/components/footer/footer';

@Component({
    selector: 'app-blog-detail',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        MatCardModule,
        MatButtonModule,
        MatIconModule,
        NavbarComponent,
        FooterComponent,
        SafeHtmlPipe
    ],
    templateUrl: './blog-detail.component.html',
    styleUrl: './blog-detail.component.scss'
})
export class BlogDetailComponent implements OnInit {
    private route = inject(ActivatedRoute);
    private router = inject(Router);
    private cmsService = inject(PublicCmsService);
    private sanitizer = inject(DomSanitizer);
    private seoService = inject(SeoService);

    post: BlogPostDto | null = null;
    relatedPosts: BlogPostDto[] = [];
    prevPost: BlogPostDto | null = null;
    nextPost: BlogPostDto | null = null;
    loading = false;



    ngOnInit() {
        this.route.params.subscribe(params => {
            const slug = params['slug'];
            if (slug) {
                this.loadPost(slug);
            }
        });
    }

    loadPost(slug: string) {
        this.loading = true;
        this.cmsService.getBlogPost(slug)
            .pipe(finalize(() => this.loading = false))
            .subscribe({
                next: (post) => {
                    if (!post.isPublished) {
                        this.router.navigate(['/error/404']);
                        return;
                    }
                    this.post = post;
                    this.updateMetaTags(post);
                    this.loadRelatedPosts(post);
                },
                error: (err) => {
                    console.error('Error loading blog post:', err);
                    this.router.navigate(['/error/404']);
                }
            });
    }

    loadRelatedPosts(post: BlogPostDto) {
        if (post.tags.length === 0) return;

        // Load posts with same tag
        this.cmsService.getBlogPosts(1, post.tags[0])
            .subscribe({
                next: (response) => {
                    this.relatedPosts = response.posts
                        .filter(p => p.id !== post.id)
                        .slice(0, 3);
                },
                error: (err) => console.error('Error loading related posts:', err)
            });
    }

    updateMetaTags(post: BlogPostDto) {
        const fallbackUrl = `${window.location.origin}/blog/${post.slug}`;
        const settings = post.seoSettings ?? { noIndex: false };
        const canonicalUrl = settings.canonicalUrl?.trim() || fallbackUrl;
        const image = settings.ogImage?.trim() || post.coverImageUrl;

        this.seoService.updateTags({
            title: settings.metaTitle || post.title,
            description: settings.metaDescription || post.summary,
            keywords: settings.metaKeywords || undefined,
            url: canonicalUrl,
            canonicalUrl,
            ogTitle: settings.ogTitle || undefined,
            ogDescription: settings.ogDescription || undefined,
            type: 'article',
            image
        });

        this.seoService.setCanonicalUrl(canonicalUrl);
        this.seoService.setNoIndex(settings.noIndex || false);

        this.seoService.generateStructuredData('Article', {
            title: post.title,
            description: settings.ogDescription || settings.metaDescription || post.summary,
            url: canonicalUrl,
            image,
            author: post.author || 'Anonim',
            publishedAt: post.publishedAt
        });
    }

    // Helper methods for template
    getReadTime(content: string): string {
        return calculateReadTime(content);
    }

    getCategory(tags: string[]): string {
        return getCategory(tags);
    }

    getCategoryColor(category: string): string {
        return getCategoryColor(category);
    }

    getAuthorInitials(author?: string): string {
        if (!author) return 'AN';
        return author.substring(0, 2).toUpperCase();
    }

    formatDate(dateString?: string): string {
        if (!dateString) return '';
        const date = new Date(dateString);
        return date.toLocaleDateString('tr-TR', { year: 'numeric', month: 'long', day: 'numeric' });
    }
}
