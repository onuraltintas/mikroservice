import { Component, Input, NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PublicCmsService, BlogPostDto } from '../../../../core/services/public-cms.service';
import { SeoService } from '../../../../core/services/seo.service';
import { FooterComponent } from '../../../../shared/components/footer/footer';
import { NavbarComponent } from '../../../../shared/components/navbar/navbar';
import { BlogDetailComponent } from './blog-detail.component';

@Component({ selector: 'app-navbar', standalone: true, template: '' })
class NavbarStubComponent {
  @Input() forceOpaque = false;
}

@Component({ selector: 'app-footer', standalone: true, template: '' })
class FooterStubComponent {}

describe('BlogDetailComponent CMS covers', () => {
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
  const relatedPost: BlogPostDto = {
    ...post,
    id: 'post-2',
    title: 'Okurken dikkati korumak',
    slug: 'dikkati-korumak',
    coverImageUrl: 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw=='
  };
  let fixture: ComponentFixture<BlogDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BlogDetailComponent],
      providers: [
        provideRouter([]),
        { provide: PublicCmsService, useValue: {} },
        { provide: SeoService, useValue: {} }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    })
      .overrideComponent(BlogDetailComponent, {
        remove: { imports: [NavbarComponent, FooterComponent] },
        add: { imports: [NavbarStubComponent, FooterStubComponent] }
      })
      .compileComponents();

    fixture = TestBed.createComponent(BlogDetailComponent);
    fixture.componentInstance.post = post;
    fixture.componentInstance.relatedPosts = [relatedPost];
    fixture.detectChanges();
  });

  it('renders the article and related-post CMS covers accessibly', () => {
    const articleImage = fixture.nativeElement.querySelector('.featured-image img') as HTMLImageElement;
    const relatedImage = fixture.nativeElement.querySelector('.related-image img') as HTMLImageElement;

    expect(articleImage.getAttribute('src')).toBe(post.coverImageUrl!);
    expect(articleImage.alt).toBe(post.title);
    expect(articleImage.loading).toBe('eager');
    expect(relatedImage.getAttribute('src')).toBe(relatedPost.coverImageUrl!);
    expect(relatedImage.alt).toBe(relatedPost.title);
    expect(relatedImage.loading).toBe('lazy');
  });
});
