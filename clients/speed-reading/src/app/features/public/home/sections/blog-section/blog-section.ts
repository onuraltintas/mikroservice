import { Component, Input, OnInit, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { PublicCmsService, BlogPostDto } from '../../../../../core/services/public-cms.service';
import { calculateReadTime, getCategory } from '../../../../../core/models/blog.model';
import { DEFAULT_HOME_PAGE_CONTENT, HomeSectionHeading } from '../../home-page-content';

@Component({
  selector: 'app-blog-section',
  standalone: true,
  imports: [RouterModule],
  templateUrl: './blog-section.html',
  styleUrl: './blog-section.scss'
})
export class BlogSectionComponent implements OnInit {
  private cmsService = inject(PublicCmsService);

  @Input() content: HomeSectionHeading = DEFAULT_HOME_PAGE_CONTENT.blog;
  posts: BlogPostDto[] = [];
  loading = true;

  ngOnInit() {
    this.loadPosts();
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



  hideBrokenCover(event: Event): void {
    (event.currentTarget as HTMLImageElement).hidden = true;
  }

  formatDate(dateString?: string): string {
    if (!dateString) return '';
    return new Date(dateString).toLocaleDateString('tr-TR', { year: 'numeric', month: 'long', day: 'numeric' });
  }
}
