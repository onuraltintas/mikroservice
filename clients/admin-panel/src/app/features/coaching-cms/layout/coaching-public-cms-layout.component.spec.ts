import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingManagementService } from '../../../core/services/coaching-management.service';
import { CoachingPublicCmsLayoutComponent } from './coaching-public-cms-layout.component';

describe('CoachingPublicCmsLayoutComponent', () => {
  it('loads the CMS-managed brand and both site navigation menus', () => {
    const service = {
      getPublicCmsNavigation: vi.fn((menu: string) => of([{
        id: menu, menu, label: menu === 'Main' ? 'Yazılar' : 'SSS',
        url: menu === 'Main' ? '/coaching/blog' : '/coaching/faq', icon: null,
        sortOrder: 0, isVisible: true, openInNewTab: false
      }])),
      getPublicCmsBlocks: vi.fn(() => of([{
        id: 'brand', kind: 'Block', group: 'HomeBranding', title: 'Eduİvme Koçluk',
        eyebrow: 'Öğrenci Koçluğu', summary: 'Koçluk için açıklama', slug: 'home-branding',
        content: '', seoTitle: null, seoDescription: null, tags: [], isPublished: true,
        scheduledPublishAt: null, sortOrder: 0, viewCount: 0,
        createdAt: '2026-09-28T00:00:00Z', updatedAt: null, version: 1
      }]))
    };
    TestBed.configureTestingModule({
      imports: [CoachingPublicCmsLayoutComponent],
      providers: [provideRouter([]), { provide: CoachingManagementService, useValue: service }]
    });
    const fixture = TestBed.createComponent(CoachingPublicCmsLayoutComponent);
    fixture.detectChanges();

    expect(service.getPublicCmsNavigation).toHaveBeenCalledWith('Main');
    expect(service.getPublicCmsNavigation).toHaveBeenCalledWith('Footer');
    expect(service.getPublicCmsBlocks).toHaveBeenCalledWith('HomeBranding');
    expect(fixture.nativeElement.textContent).toContain('Koçluk için açıklama');
    expect(fixture.nativeElement.textContent).toContain('SSS');
    expect(fixture.nativeElement.querySelector('a[href="/coaching/faq"]')).not.toBeNull();
  });
});
