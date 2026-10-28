import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingCmsEntry, CoachingSubscriptionPlan, CoachingManagementService } from '../../../core/services/coaching-management.service';
import { CoachingPublicHomeComponent } from './coaching-public-home.component';
import { SiteAnalyticsService } from '../../../core/services/site-analytics.service';

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

  // Roadmap O11
  it('sends priced student and teacher plans to registration with the campaign tags and always shows the price', () => {
    const fixture = setup([
      { ...basePlan('ogrenci-yillik', 'Öğrenci yıllık', 'Individual'), isContactOnly: false, price: 2400, trialDays: 7 },
      { ...basePlan('ogretmen-10', 'Öğretmen 10', 'Teacher'), isContactOnly: false, price: 5000, includedStudentSeats: 10 }
    ], { currency: null });
    const analytics = TestBed.inject(SiteAnalyticsService);
    analytics.utm.set({ utmSource: 'instagram', utmMedium: 'social', utmCampaign: null });
    fixture.detectChanges();

    const student = fixture.nativeElement.querySelector('[data-testid="plan-register-ogrenci-yillik"]') as HTMLAnchorElement;
    expect(student.getAttribute('href')).toContain('/auth/register/student?');
    expect(student.getAttribute('href')).toContain('utm_source=instagram');
    expect(student.getAttribute('href')).toContain('plan=ogrenci-yillik');
    expect(student.textContent).toContain('ücretsiz dene');
    const teacher = fixture.nativeElement.querySelector('[data-testid="plan-register-ogretmen-10"]') as HTMLAnchorElement;
    expect(teacher.getAttribute('href')).toContain('/auth/register/teacher');
    const card = fixture.nativeElement.querySelector('[data-testid="plan-ogretmen-10"]').textContent as string;
    expect(card).toContain('Öğretmenler için'); expect(card).toContain('10 öğrenci koltuğu');
    expect(fixture.nativeElement.textContent).toContain('2.400');
    expect(fixture.nativeElement.textContent).not.toContain('Ücret bilgisi için giriş yapın');
  });

  it('shows a load error with a retry instead of "no plans" when the plan service fails', () => {
    const fixture = setup([]);
    const service = TestBed.inject(CoachingManagementService) as unknown as { getPublicSubscriptionPlans: ReturnType<typeof vi.fn> };
    service.getPublicSubscriptionPlans.mockReturnValueOnce(throwError(() => new Error('503')));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="plans-error"]')).toBeTruthy();
    expect(fixture.nativeElement.textContent).not.toContain('Yayınlanmış koçluk planı henüz yok');
    fixture.componentInstance.loadPlans(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="plans-error"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Yayınlanmış koçluk planı henüz yok');
  });

  it('describes the page for search engines and link previews', () => {
    const fixture = setup();
    fixture.detectChanges();
    expect(document.querySelector('meta[property="og:title"]')?.getAttribute('content')).toContain('Hedefinden başla');
    expect(document.querySelector('link[rel="canonical"]')?.getAttribute('href')).toMatch(/\/coaching$/);
    expect(JSON.parse(document.getElementById('site-jsonld')!.textContent!)[0]['@type']).toBe('Organization');
  });

  function basePlan(id: string, name: string, audience: 'Individual' | 'Institution' | 'Teacher'): CoachingSubscriptionPlan {
    return { id, slug: id, name, description: 'Plan', audience, price: 0, isContactOnly: true, billingPeriod: 'Annual', durationDays: 365,
      includedStudentSeats: null, features: [], isActive: true, isPublic: true, sortOrder: 0 };
  }

  function setup(plans?: CoachingSubscriptionPlan[], settings: { currency: string | null } = { currency: 'TRY' }) {
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
      getPublicSubscriptionPlans: vi.fn(() => of(plans ?? [
        plan('student-plan', 'Öğrenci planı', 'Individual'),
        plan('institution-plan', 'Kurum planı', 'Institution'),
        plan('hidden-plan', 'Gizli plan', 'Individual', false)
      ])),
      getPublicBankTransferSettings: vi.fn(() => of(settings)),
      getPublicCmsBlog: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 3, totalCount: 0 }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingPublicHomeComponent],
      providers: [
        provideRouter([]),
        { provide: CoachingManagementService, useValue: service },
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
