import { Meta, Title } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingCmsEntry, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { CoachingPublicBlogComponent } from './coaching-public-blog.component';

describe('CoachingPublicBlogComponent', () => {
  it('shows the CMS-managed blog heading and post author, publication date, and cover image', () => {
    const post: CoachingCmsEntry = {
      id: 'post-1', kind: 'Blog', group: null, title: 'Koçlukta planlama', slug: 'planlama',
      summary: 'Yazı özeti', content: 'Yazı içeriği', seoTitle: null, seoDescription: null,
      tags: [], isPublished: true, scheduledPublishAt: null, sortOrder: 0, viewCount: 0,
      createdAt: '2026-09-01T00:00:00Z', updatedAt: null, version: 1, author: 'Ece Öğretmen',
      publishedAt: '2026-09-20T09:30:00Z', coverImageUrl: '/api/coaching/cms/media/12345678-1234-1234-1234-123456789abc'
    };
    const heading: CoachingCmsEntry = {
      ...post, id: 'blog-heading', kind: 'Block', group: 'BlogLanding', title: 'Koçluk rehberleri',
      summary: 'Uygulanabilir kısa öneriler', author: null, publishedAt: null, coverImageUrl: null
    };
    const service = {
      getPublicCmsBlocks: vi.fn(() => of([heading])),
      getPublicCmsBlog: vi.fn(() => of({ items: [post], pageNumber: 1, pageSize: 10, totalCount: 1 }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingPublicBlogComponent],
      providers: [
        provideRouter([]),
        { provide: CoachingManagementService, useValue: service },
        { provide: Title, useValue: { setTitle: vi.fn() } },
        { provide: Meta, useValue: { updateTag: vi.fn() } }
      ]
    });
    const fixture = TestBed.createComponent(CoachingPublicBlogComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Koçluk rehberleri');
    expect(fixture.nativeElement.textContent).toContain('Ece Öğretmen');
    expect(fixture.nativeElement.textContent).toContain('Koçlukta planlama');
    expect(fixture.nativeElement.querySelector('img')?.getAttribute('src')).toBe(post.coverImageUrl);
    expect(service.getPublicCmsBlocks).toHaveBeenCalledWith('BlogLanding');
  });
});
