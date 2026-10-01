import { Component, NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { BlogListVm, BlogPostDto, PublicCmsService } from '../../../../core/services/public-cms.service';
import { SeoService } from '../../../../core/services/seo.service';
import { FooterComponent } from '../../../../shared/components/footer/footer';
import { NavbarComponent } from '../../../../shared/components/navbar/navbar';
import { NewsletterWidgetComponent } from '../../../../shared/components/newsletter-widget/newsletter-widget.component';
import { BlogListComponent } from './blog-list.component';

@Component({ selector: 'app-navbar', standalone: true, template: '' })
class NavbarStubComponent {}

@Component({ selector: 'app-footer', standalone: true, template: '' })
class FooterStubComponent {}

@Component({ selector: 'app-newsletter-widget', standalone: true, template: '' })
class NewsletterWidgetStubComponent {}

describe('BlogListComponent CMS covers', () => {
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
  const response: BlogListVm = {
    posts: [post], totalCount: 1, pageNumber: 1, pageSize: 10, totalPages: 1
  };
  const cmsService = { getBlogPosts: jasmine.createSpy('getBlogPosts').and.returnValue(of(response)) };
  const seoService = { updateTags: jasmine.createSpy('updateTags') };
  let fixture: ComponentFixture<BlogListComponent>;

  beforeEach(async () => {
    cmsService.getBlogPosts.and.returnValue(of(response));

    await TestBed.configureTestingModule({
      imports: [BlogListComponent],
      providers: [
        provideRouter([]),
        { provide: PublicCmsService, useValue: cmsService },
        { provide: SeoService, useValue: seoService }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    })
      .overrideComponent(BlogListComponent, {
        remove: { imports: [NewsletterWidgetComponent, NavbarComponent, FooterComponent] },
        add: { imports: [NewsletterWidgetStubComponent, NavbarStubComponent, FooterStubComponent] }
      })
      .compileComponents();

    fixture = TestBed.createComponent(BlogListComponent);
    fixture.detectChanges();
  });

  it('uses the CMS cover for the featured story and post cards', () => {
    const featuredImage = fixture.nativeElement.querySelector('.featured-image img') as HTMLImageElement;
    const postImage = fixture.nativeElement.querySelector('.post-card img') as HTMLImageElement;

    expect(featuredImage.getAttribute('src')).toBe(post.coverImageUrl!);
    expect(featuredImage.alt).toBe(post.title);
    expect(featuredImage.loading).toBe('eager');
    expect(postImage.getAttribute('src')).toBe(post.coverImageUrl!);
    expect(postImage.alt).toBe(post.title);
    expect(postImage.loading).toBe('lazy');
  });

  it('uses placeholders when no CMS cover is configured', () => {
    cmsService.getBlogPosts.and.returnValue(of({ ...response, posts: [{ ...post, coverImageUrl: undefined }] }));
    fixture.componentInstance.loadBlogPosts();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.featured-image img')).toBeNull();
    expect(fixture.nativeElement.querySelector('.post-card img')).toBeNull();
    expect(fixture.nativeElement.querySelector('.featured-image .image-placeholder')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.post-card .image-placeholder')).not.toBeNull();
  });
});
