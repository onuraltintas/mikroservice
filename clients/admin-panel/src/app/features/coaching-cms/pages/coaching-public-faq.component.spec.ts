import { provideRouter } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingCmsEntry, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { CoachingPublicFaqComponent } from './coaching-public-faq.component';

describe('CoachingPublicFaqComponent', () => {
  it('renders only the published FAQ entries provided by the Coaching CMS', () => {
    const heading = createEntry('faq-heading', 'HomeFaqHeading', 'Koçluk hakkında merak edilenler');
    const item = createEntry('faq-account', 'HomeFaq', 'Koçluk hesabı nasıl açılır?');
    item.content = 'Kayıt sayfasından Koçluk rolünü seçin.';
    const service = {
      getPublicCmsBlocks: vi.fn((group: string) => of(group === 'HomeFaqHeading' ? [heading] : [item]))
    };
    TestBed.configureTestingModule({
      imports: [CoachingPublicFaqComponent],
      providers: [
        provideRouter([]),
        { provide: CoachingManagementService, useValue: service },
        { provide: Title, useValue: { setTitle: vi.fn() } },
        { provide: Meta, useValue: { updateTag: vi.fn() } }
      ]
    });

    const fixture = TestBed.createComponent(CoachingPublicFaqComponent);
    fixture.detectChanges();

    expect(service.getPublicCmsBlocks).toHaveBeenCalledWith('HomeFaqHeading');
    expect(service.getPublicCmsBlocks).toHaveBeenCalledWith('HomeFaq');
    expect(fixture.nativeElement.textContent).toContain('Koçluk hesabı nasıl açılır?');
    expect(fixture.nativeElement.textContent).toContain('Kayıt sayfasından Koçluk rolünü seçin.');
  });

  function createEntry(id: string, group: string, title: string): CoachingCmsEntry {
    return {
      id, kind: 'Block', group, title, slug: id, summary: null, content: '',
      seoTitle: null, seoDescription: null, tags: [], isPublished: true,
      scheduledPublishAt: null, sortOrder: 0, viewCount: 0,
      createdAt: '2026-09-28T00:00:00Z', updatedAt: null, version: 1
    };
  }
});
