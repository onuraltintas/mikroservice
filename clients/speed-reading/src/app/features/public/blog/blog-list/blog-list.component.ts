import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { PublicCmsService, BlogPostDto, BlogListVm } from '../../../../core/services/public-cms.service';
import { SeoService } from '../../../../core/services/seo.service';
import { calculateReadTime, getCategory, getCategoryColor } from '../../../../core/models/blog.model';
import { NewsletterWidgetComponent } from '../../../../shared/components/newsletter-widget/newsletter-widget.component';
import { NavbarComponent } from '../../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../../shared/components/footer/footer';
import { finalize } from 'rxjs/operators';

interface Category {
    name: string;
    count: number;
}

@Component({
    selector: 'app-blog-list',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatCardModule,
        MatChipsModule,
        NewsletterWidgetComponent,
        NavbarComponent,
        FooterComponent
    ],
    templateUrl: './blog-list.component.html',
    styleUrl: './blog-list.component.scss'
})
export class BlogListComponent implements OnInit {
    private cmsService = inject(PublicCmsService);
    private seoService = inject(SeoService);

    posts: BlogPostDto[] = [];
    filteredPosts: BlogPostDto[] = [];
    featuredPost: BlogPostDto | null = null;
    categories: Category[] = [];
    selectedCategory = 'Tümü';
    searchQuery = '';
    loading = false;
    currentPage = 1;
    totalPages = 1;
    pageSize = 9;

    ngOnInit() {
        this.setSeoTags();
        this.loadBlogPosts();
    }

    private setSeoTags() {
        this.seoService.updateTags({
            title: 'Blog | Hızlı Okuma Teknikleri ve Kişisel Gelişim Yazıları',
            description: 'Hızlı okuma, anlama teknikleri, kişisel gelişim ve eğitim dünyasından en güncel makaleler, ipuçları ve rehberler.',
            keywords: 'hızlı okuma blog, kişisel gelişim yazıları, okuma teknikleri, eğitim makaleleri, anlama stratejileri',
            url: window.location.href,
            type: 'blog',
            image: 'https://masterhizliokuma.com/assets/images/blog-banner.jpg' // Varsa genel blog görseli
        });
    }

    loadBlogPosts(page: number = 1, tag?: string) {
        this.loading = true;
        this.cmsService.getBlogPosts(page, tag)
            .pipe(finalize(() => this.loading = false))
            .subscribe({
                next: (response: BlogListVm) => {
                    // Safely access posts array
                    this.posts = response?.posts || [];
                    this.currentPage = response?.pageNumber || 1;
                    this.totalPages = response?.totalPages || 1;

                    // Set featured post (first post if on page 1 and no filter)
                    if (page === 1 && !tag) {
                        this.featuredPost = this.posts.length > 0 ? this.posts[0] : null;
                    } else {
                        this.featuredPost = null;
                    }

                    // Calculate categories from tags
                    this.calculateCategories();

                    // Apply search filter
                    this.filterPosts();
                },
                error: (err) => {
                    console.error('Error loading blog posts:', err);
                    this.posts = [];
                    this.filteredPosts = [];
                }
            });
    }

    calculateCategories() {
        const tagCounts = new Map<string, number>();

        this.posts.forEach(post => {
            post.tags.forEach(tag => {
                tagCounts.set(tag, (tagCounts.get(tag) || 0) + 1);
            });
        });

        this.categories = [
            { name: 'Tümü', count: this.posts.length },
            ...Array.from(tagCounts.entries()).map(([name, count]) => ({ name, count }))
        ];
    }

    filterPosts() {
        let filtered = this.posts;

        // Category filter
        if (this.selectedCategory !== 'Tümü') {
            filtered = filtered.filter(post => post.tags.includes(this.selectedCategory));
        }

        // Search filter
        if (this.searchQuery) {
            const query = this.searchQuery.toLowerCase();
            filtered = filtered.filter(post =>
                post.title.toLowerCase().includes(query) ||
                post.summary.toLowerCase().includes(query)
            );
        }

        this.filteredPosts = filtered;
    }

    selectCategory(category: string) {
        this.selectedCategory = category;
        if (category === 'Tümü') {
            this.loadBlogPosts(1);
        } else {
            this.loadBlogPosts(1, category);
        }
    }

    onSearch() {
        this.filterPosts();
    }

    onSearchChange() {
        this.onSearch();
    }

    loadMore() {
        if (this.currentPage < this.totalPages) {
            this.loadBlogPosts(this.currentPage + 1, this.selectedCategory !== 'Tümü' ? this.selectedCategory : undefined);
        }
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
