import { Component, ElementRef, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService, BlogPostDto } from '../../../../core/services/public-cms.service';
import { SeoService } from '../../../../core/services/seo.service';
import { calculateReadTime, getCategory, getCategoryColor } from '../../../../core/models/blog.model';
import { finalize } from 'rxjs/operators';
import { NavbarComponent } from '../../../../shared/components/navbar/navbar';
import { SafeHtmlPipe } from '../../../../shared/pipes/safe-html.pipe';
import { FooterComponent } from '../../../../shared/components/footer/footer';
import { canonicalPageUrl } from '../../../../core/utils/public-site-url';

@Component({
    selector: 'app-blog-detail',
    host: { 'data-brand': 'ho' },
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
    private seoService = inject(SeoService);
    private host = inject<ElementRef<HTMLElement>>(ElementRef);

    post: BlogPostDto | null = null;
    relatedPosts: BlogPostDto[] = [];
    prevPost: BlogPostDto | null = null;
    nextPost: BlogPostDto | null = null;
    loading = false;
    /** The cover image failed to load: the whole cover box is hidden, not left empty. */
    coverFailed = false;
    /** What the copy-link button says it did, read out by screen readers. */
    shareStatus = '';
    private headingCache: { content: string; headings: string[] } | null = null;



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
                    this.coverFailed = false;
                    this.shareStatus = '';
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
        const fallbackUrl = canonicalPageUrl(`/blog/${post.slug}`);
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
            publishedTime: post.publishedAt
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

    /** The post's own section headings (its h2 elements), for the table of contents. */
    headings(content: string | null | undefined): string[] {
        const value = content ?? '';
        if (this.headingCache?.content === value) return this.headingCache.headings;
        const headings = [...value.matchAll(/<h2\b[^>]*>([\s\S]*?)<\/h2>/gi)]
            .map(match => decodeEntities(match[1].replace(/<[^>]*>/g, '')).trim())
            .filter(text => text.length > 0);
        this.headingCache = { content: value, headings };
        return headings;
    }

    scrollToHeading(index: number): void {
        const heading = this.host.nativeElement.querySelectorAll('.article-body h2')[index] as HTMLElement | undefined;
        if (!heading) return;
        const reduceMotion = typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
        heading.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
        heading.setAttribute('tabindex', '-1');
        heading.focus({ preventScroll: true });
    }

    /** Share addresses for the post's public URL; they open the network's own share page. */
    shareLinks(post: BlogPostDto): { facebook: string; x: string; linkedin: string } {
        const url = encodeURIComponent(this.postUrl(post));
        const text = encodeURIComponent(post.title);
        return {
            facebook: `https://www.facebook.com/sharer/sharer.php?u=${url}`,
            x: `https://x.com/intent/post?url=${url}&text=${text}`,
            linkedin: `https://www.linkedin.com/sharing/share-offsite/?url=${url}`
        };
    }

    async copyLink(post: BlogPostDto): Promise<void> {
        try {
            await navigator.clipboard.writeText(this.postUrl(post));
            this.shareStatus = 'Bağlantı kopyalandı.';
        } catch {
            this.shareStatus = 'Bağlantı kopyalanamadı. Adres çubuğundaki bağlantıyı kopyalayabilirsiniz.';
        }
    }

    private postUrl(post: BlogPostDto): string {
        return post.seoSettings?.canonicalUrl?.trim() || canonicalPageUrl(`/blog/${post.slug}`);
    }

    hideBrokenCover(event: Event): void {
        (event.currentTarget as HTMLImageElement).hidden = true;
    }

    formatDate(dateString?: string): string {
        if (!dateString) return '';
        const date = new Date(dateString);
        return date.toLocaleDateString('tr-TR', { year: 'numeric', month: 'long', day: 'numeric' });
    }
}

const ENTITIES: Record<string, string> = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ' };

function decodeEntities(value: string): string {
    return value.replace(/&(#x[0-9a-f]+|#\d+|[a-z]+);/gi, (whole, code: string) => {
        if (code[0] === '#') {
            const point = code[1].toLowerCase() === 'x' ? parseInt(code.slice(2), 16) : parseInt(code.slice(1), 10);
            return Number.isFinite(point) ? String.fromCodePoint(point) : whole;
        }
        return ENTITIES[code.toLowerCase()] ?? whole;
    });
}
