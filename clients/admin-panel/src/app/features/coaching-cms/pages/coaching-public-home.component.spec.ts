import { provideRouter } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingCmsEntry, CoachingSubscriptionPlan, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { CoachingPublicHomeComponent } from './coaching-public-home.component';

describe('CoachingPublicHomeComponent', () => {
  it('loads the hero and the editorial sections from the Coaching CMS', () => {
    const fixture = setup();
    fixture.detectChanges();

    const service = TestBed.inject(CoachingManagementService) as unknown as {
      getPublicCmsBlocks: ReturnType<typeof vi.fn>;
    };
    expect(service.getPublicCmsBlocks).toHaveBeenCalledWith('HomeHero');
    expect(service.getPublicCmsBlocks).toHaveBeenCalledWith('HomePage');
    expect(service.getPublicCmsBlocks).toHaveBeenCalledWith('HomeHowItWorks');
    expect(fixture.nativeElement.textContent).toContain('Hedefinden başla, adım adım ilerle.');
    expect(fixture.nativeElement.textContent).toContain('Öğrenciler için görünür ilerleme');
    expect(fixture.nativeElement.textContent).toContain('Nasıl çalışır?');
  });

  it('shows public individual and institution plans without exposing unpublished plans', () => {
    const fixture = setup();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Öğrenci planı');
    expect(text).toContain('Kurum planı');
    expect(text).not.toContain('Gizli plan');
    expect(text).toContain('Kurum çözümlerini incele');
  });

  it('renders published FAQ and authentic user feedback from their dedicated CMS groups', () => {
    const fixture = setup();
    const service = TestBed.inject(CoachingManagementService) as unknown as {
      getPublicCmsBlocks: ReturnType<typeof vi.fn>;
    };
    const faq = entry('faq-1', 'Block', 'HomeFaq', 'Davetle nasıl katılırım?');
    faq.content = 'Koçluk davet bağlantısını kullanın.';
    const testimonial = entry('testimonial-1', 'Block', 'HomeTestimonials', 'Ada');
    testimonial.summary = 'Öğrenci';
    testimonial.content = 'Planımı takip etmek artık daha kolay.';
    service.getPublicCmsBlocks.mockImplementation((group: string) => of(
      group === 'HomeFaq' ? [faq] : group === 'HomeTestimonials' ? [testimonial] : []
    ));

    fixture.detectChanges();

    expect(service.getPublicCmsBlocks).toHaveBeenCalledWith('HomeFaq');
    expect(service.getPublicCmsBlocks).toHaveBeenCalledWith('HomeTestimonials');
    expect(fixture.nativeElement.textContent).toContain('Davetle nasıl katılırım?');
    expect(fixture.nativeElement.textContent).toContain('Planımı takip etmek artık daha kolay.');
  });

  function setup() {
    const hero = entry('hero', 'Block', 'HomeHero', 'Hedefinden başla, adım adım ilerle.');
    const section = entry('student-section', 'Block', 'HomePage', 'Öğrenciler için görünür ilerleme');
    const plan = (id: string, name: string, audience: 'Individual' | 'Institution', isPublic = true): CoachingSubscriptionPlan => ({
      id, slug: id, name, description: 'Hedef ve gelişim takibi', audience, price: 0,
      isContactOnly: true, billingPeriod: 'Annual', durationDays: 365,
      includedStudentSeats: audience === 'Institution' ? 100 : null, features: [],
      isActive: true, isPublic, sortOrder: 0
    });
    const service = {
      getPublicCmsBlocks: vi.fn((group: string) => of(group === 'HomeHero' ? [hero] : [section])),
      getPublicSubscriptionPlans: vi.fn(() => of([
        plan('student-plan', 'Öğrenci planı', 'Individual'),
        plan('institution-plan', 'Kurum planı', 'Institution'),
        plan('hidden-plan', 'Gizli plan', 'Individual', false)
      ])),
      getPublicBankTransferSettings: vi.fn(() => of({ currency: 'TRY' })),
      getPublicCmsBlog: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 3, totalCount: 0 }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingPublicHomeComponent],
      providers: [
        provideRouter([]),
        { provide: CoachingManagementService, useValue: service },
        { provide: Title, useValue: { setTitle: vi.fn() } },
        { provide: Meta, useValue: { updateTag: vi.fn() } }
      ]
    });
    return TestBed.createComponent(CoachingPublicHomeComponent);
  }

  function entry(id: string, kind: 'Block', group: string, title: string): CoachingCmsEntry {
    return {
      id, kind, group, title, slug: id, summary: 'Koçluk sürecini anlaşılır hale getirin.',
      content: 'İlerlemeyi küçük adımlarla takip edin.', seoTitle: null, seoDescription: null,
      tags: [], isPublished: true, scheduledPublishAt: null, sortOrder: 0, viewCount: 0,
      createdAt: '2026-09-28T00:00:00Z', updatedAt: null, version: 1
    };
  }
});
