import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PublicCmsService, BlogPostDto } from '../../../../../core/services/public-cms.service';
import { calculateReadTime, getCategory, getCategoryColor } from '../../../../../core/models/blog.model';

@Component({
  selector: 'app-blog-section',
  standalone: true,
  imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule, MatIconModule],
  templateUrl: './blog-section.html',
  styleUrl: './blog-section.scss'
})
export class BlogSectionComponent implements OnInit {
  private cmsService = inject(PublicCmsService);

  sectionTitle = 'Blog & Kaynaklar';
  sectionSubtitle = 'Hızlı okuma, öğrenme teknikleri ve başarı hikayeleri hakkında en güncel içerikler';
  posts: BlogPostDto[] = [];
  loading = true;

  ngOnInit() {
    this.loadContent();
    this.loadPosts();
  }

  private loadContent() {
    this.cmsService.getLandingContent().subscribe({
      next: (content) => {
        if (content.blocks['blog_section_title'])    this.sectionTitle    = content.blocks['blog_section_title'];
        if (content.blocks['blog_section_subtitle']) this.sectionSubtitle = content.blocks['blog_section_subtitle'];
      }
    });
  }

  private loadPosts() {
    this.cmsService.getBlogPosts(1).subscribe({
      next: (response) => {
        this.posts = (response.posts || []).slice(0, 3);
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  getReadTime(content: string): string { return calculateReadTime(content || ''); }
  getCategory(tags: string[]): string { return getCategory(tags); }
  getCategoryColor(category: string): string { return getCategoryColor(category); }

  getAuthorInitials(author?: string): string {
    if (!author) return 'AN';
    return author.substring(0, 2).toUpperCase();
  }

  formatDate(dateString?: string): string {
    if (!dateString) return '';
    return new Date(dateString).toLocaleDateString('tr-TR', { year: 'numeric', month: 'long', day: 'numeric' });
  }
}
