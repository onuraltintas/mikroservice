import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { BlogPostDto, PublicCmsService } from '../../../../../core/services/public-cms.service';
import { BlogSectionComponent } from './blog-section';

describe('BlogSectionComponent CMS covers', () => {
  const post: BlogPostDto = {
    id: 'post-1',
    title: 'Okuma alışkanlığı nasıl geliştirilir?',
    slug: 'okuma-aliskanligi',
    summary: 'Küçük ve düzenli adımlarla okuma rutini oluşturun.',
    content: 'Okuma rutini için kısa bir rehber.',
    tags: ['Öğrenme'],
    coverImageUrl: 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==',
    seoSettings: { noIndex: false },
    viewCount: 0,
    isPublished: true
  };

  async function createFixture(posts: BlogPostDto[]) {
    const cmsService = {
      getBlogPosts: jasmine.createSpy('getBlogPosts').and.returnValue(of({ posts }))
    };

    await TestBed.configureTestingModule({
      imports: [BlogSectionComponent],
      providers: [
        provideRouter([]),
        { provide: PublicCmsService, useValue: cmsService }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(BlogSectionComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the CMS cover image with useful alt text and lazy loading', async () => {
    const fixture = await createFixture([post]);
    const image = fixture.nativeElement.querySelector('.blog-card img') as HTMLImageElement;

    expect(image).not.toBeNull();
    expect(image.getAttribute('src')).toBe(post.coverImageUrl!);
    expect(image.alt).toBe(post.title);
    expect(image.loading).toBe('lazy');
    expect(image.decoding).toBe('async');
    expect(getComputedStyle(image).objectFit).toBe('cover');
  });

  it('keeps a designed placeholder when a CMS post has no cover image', async () => {
    const fixture = await createFixture([{ ...post, coverImageUrl: undefined }]);

    expect(fixture.nativeElement.querySelector('.blog-card img')).toBeNull();
    expect(fixture.nativeElement.querySelector('.blog-card .image-placeholder')).not.toBeNull();
  });

  it('falls back to the placeholder when the CMS image cannot be loaded', async () => {
    const fixture = await createFixture([post]);
    const image = fixture.nativeElement.querySelector('.blog-card img') as HTMLImageElement;

    image.dispatchEvent(new Event('error'));
    fixture.detectChanges();

    expect(image.hidden).toBeTrue();
    expect(fixture.nativeElement.querySelector('.blog-card .image-placeholder')).not.toBeNull();
  });
});
