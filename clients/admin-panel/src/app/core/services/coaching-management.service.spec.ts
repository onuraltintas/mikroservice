import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingManagementService } from './coaching-management.service';

describe('CoachingManagementService public pages', () => {
  it('reads the public plans and their currency from the billing catalog', () => {
    const { service, http } = setup();
    const plan = { id: 'pl1', name: 'TEST Aylık', description: 'TEST', buyerType: 'Individual', price: 200, currency: 'TRY', durationDays: 30, trialDays: 7,
      seatCount: null, extraSeatPrice: null, isContactOnly: false, features: ['TEST özellik'], sortOrder: 2 };
    service.getPublicSubscriptionPlans().subscribe(plans => expect(plans).toEqual([{ id: 'pl1', slug: 'pl1', name: 'TEST Aylık', description: 'TEST',
      audience: 'Individual', price: 200, isContactOnly: false, billingPeriod: '', durationDays: 30, includedStudentSeats: null, features: ['TEST özellik'],
      isActive: true, isPublic: true, sortOrder: 2, trialDays: 7, extraSeatPrice: null }]));
    http.expectOne('/api/billing/v1/catalog/coaching').flush({ plans: [plan] });
    service.getPublicPlanCurrency().subscribe(currency => expect(currency).toBe('TRY'));
    http.expectOne('/api/billing/v1/catalog/coaching').flush({ plans: [plan] });
    service.getPublicPlanCurrency().subscribe(currency => expect(currency).toBeNull());
    http.expectOne('/api/billing/v1/catalog/coaching').flush({ plans: [] });
    http.verify();
  });

  it('reads only public Coaching pages, homepage blocks, blog and navigation', () => {
    const { service, http } = setup();

    service.getPublicCmsPage('about-us').subscribe(page => expect(page.slug).toBe('about-us'));
    http.expectOne('/api/coaching/cms/pages/about-us').flush({ success: true, data: { slug: 'about-us' } });

    service.getPublicCmsBlocks('HomePage').subscribe(blocks => expect(blocks).toHaveLength(1));
    const blocks = http.expectOne('/api/coaching/cms/blocks?group=HomePage');
    blocks.flush({ success: true, data: [{ id: 'block-1' }] });

    service.getPublicCmsNavigation('Main').subscribe(items => expect(items[0].label).toBe('Hakkımızda'));
    const navigation = http.expectOne('/api/coaching/cms/navigation?menu=Main');
    navigation.flush({ success: true, data: [{ id: 'nav-1', label: 'Hakkımızda' }] });

    service.getPublicCmsBlog(2, 10).subscribe(page => expect(page.totalCount).toBe(1));
    const blog = http.expectOne('/api/coaching/cms/blog?pageNumber=2&pageSize=10');
    blog.flush({ success: true, data: { items: [], pageNumber: 2, pageSize: 10, totalCount: 1 } });

    service.getPublicCmsBlogPost('launch-news').subscribe(post => expect(post.kind).toBe('Blog'));
    http.expectOne('/api/coaching/cms/blog/launch-news').flush({ success: true, data: { kind: 'Blog' } });
    http.verify();
  });

  it('lists, uploads and removes media only through the Coaching CMS API', () => {
    const { service, http } = setup();

    service.getCmsMediaAssets(2, 30).subscribe(page => expect(page.totalCount).toBe(1));
    http.expectOne('/api/coaching-admin/cms/media?pageNumber=2&pageSize=30')
      .flush({ success: true, data: { items: [], pageNumber: 2, pageSize: 30, totalCount: 1 } });

    const file = new File(['image bytes'], 'hero.png', { type: 'image/png' });
    service.uploadCmsMedia(file, 'Ana sayfa görseli').subscribe(asset => expect(asset.id).toBe('asset-1'));
    const upload = http.expectOne('/api/coaching-admin/cms/media');
    expect(upload.request.method).toBe('POST');
    expect(upload.request.body).toBeInstanceOf(FormData);
    expect((upload.request.body as FormData).get('altText')).toBe('Ana sayfa görseli');
    upload.flush({ success: true, data: { id: 'asset-1' } });

    service.deleteCmsMedia('asset-1').subscribe();
    http.expectOne('/api/coaching-admin/cms/media/asset-1').flush({ success: true });
    http.verify();
  });

  it('filters CMS sections by group and carries structured editorial fields', () => {
    const { service, http } = setup();

    service.getCmsEntries('Block', 2, 25, 'davet', 'HomeFaq').subscribe(page => expect(page.totalCount).toBe(1));
    const listing = http.expectOne('/api/coaching-admin/cms/entries?kind=Block&pageNumber=2&pageSize=25&search=davet&group=HomeFaq');
    listing.flush({ success: true, data: { items: [], pageNumber: 2, pageSize: 25, totalCount: 1 } });

    service.createCmsEntry({
      kind: 'Blog', group: null, title: 'Koçluk yazısı', slug: 'kocluk-yazisi', summary: null,
      content: 'Yazı', seoTitle: null, seoDescription: null, tags: [], isPublished: false,
      scheduledPublishAt: null, sortOrder: 0, author: 'Ece', publishedAt: '2026-09-20T09:30:00Z',
      coverImageUrl: '/api/coaching/cms/media/12345678-1234-1234-1234-123456789abc'
    }).subscribe();
    const create = http.expectOne('/api/coaching-admin/cms/entries');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toMatchObject({ author: 'Ece', publishedAt: '2026-09-20T09:30:00Z' });
    create.flush({ success: true, data: { id: 'entry-1' } });
    http.verify();
  });

  function setup() {
    TestBed.configureTestingModule({ providers: [CoachingManagementService, provideHttpClient(), provideHttpClientTesting()] });
    return { service: TestBed.inject(CoachingManagementService), http: TestBed.inject(HttpTestingController) };
  }
});

describe('CoachingManagementService Coaching newsletter', () => {
  it('keeps public signup, confirmation and unsubscribe on Coaching-owned endpoints', () => {
    const { service, http } = setup();

    service.getCoachingNewsletterRecaptchaConfiguration().subscribe(configuration =>
      expect(configuration).toEqual({ enabled: true, siteKey: 'public-site-key' }));
    http.expectOne('/api/coaching/cms/newsletter/recaptcha').flush({
      success: true, data: { enabled: true, siteKey: 'public-site-key' }
    });

    service.subscribeToCoachingNewsletter({
      email: ' Learner@Example.com ', consentGiven: true, privacyPolicyVersion: 4,
      newsletterConsentVersion: 2, honeypot: '', recaptchaToken: 'captcha-token'
    }).subscribe();
    const signup = http.expectOne('/api/coaching/cms/newsletter/subscriptions');
    expect(signup.request.method).toBe('POST');
    expect(signup.request.body).toEqual({
      email: 'Learner@Example.com', consentGiven: true, privacyPolicyVersion: 4,
      newsletterConsentVersion: 2, honeypot: '', recaptchaToken: 'captcha-token'
    });
    signup.flush({ success: true });

    service.confirmCoachingNewsletter('confirm-token').subscribe();
    const confirm = http.expectOne('/api/coaching/cms/newsletter/confirm');
    expect(confirm.request.body).toEqual({ token: 'confirm-token' });
    confirm.flush({ success: true });

    service.unsubscribeFromCoachingNewsletter('unsubscribe-token').subscribe();
    const unsubscribe = http.expectOne('/api/coaching/cms/newsletter/unsubscribe');
    expect(unsubscribe.request.body).toEqual({ token: 'unsubscribe-token' });
    unsubscribe.flush({ success: true });
    http.verify();
  });

  it('filters, exports, unsubscribes and deletes subscribers through Coaching admin endpoints', () => {
    const { service, http } = setup();
    const subscriber = { id: 'subscriber-1', email: 'learner@example.com', status: 'Active' };

    service.getCoachingNewsletterSubscribers(2, 10, ' learner ', 'Active')
      .subscribe(page => expect(page.items).toEqual([subscriber]));
    const listing = http.expectOne('/api/coaching-admin/cms/newsletter/subscribers?pageNumber=2&pageSize=10&search=learner&status=Active');
    listing.flush({ success: true, data: { items: [subscriber], pageNumber: 2, pageSize: 10, totalCount: 1 } });

    service.exportCoachingNewsletterSubscribers('Active').subscribe(blob => expect(blob.type).toContain('text/csv'));
    const exportRequest = http.expectOne('/api/coaching-admin/cms/newsletter/subscribers/export?status=Active');
    expect(exportRequest.request.responseType).toBe('blob');
    exportRequest.flush(new Blob(['Email,Status\r\n'], { type: 'text/csv' }));

    service.unsubscribeCoachingNewsletterSubscriber(subscriber.id).subscribe();
    const unsubscribe = http.expectOne(`/api/coaching-admin/cms/newsletter/subscribers/${subscriber.id}/unsubscribe`);
    expect(unsubscribe.request.method).toBe('POST');
    unsubscribe.flush({ success: true });

    service.deleteCoachingNewsletterSubscriber(subscriber.id).subscribe();
    const remove = http.expectOne(`/api/coaching-admin/cms/newsletter/subscribers/${subscriber.id}`);
    expect(remove.request.method).toBe('DELETE');
    remove.flush({ success: true });
    http.verify();
  });

  function setup() {
    TestBed.configureTestingModule({ providers: [CoachingManagementService, provideHttpClient(), provideHttpClientTesting()] });
    return { service: TestBed.inject(CoachingManagementService), http: TestBed.inject(HttpTestingController) };
  }
});
