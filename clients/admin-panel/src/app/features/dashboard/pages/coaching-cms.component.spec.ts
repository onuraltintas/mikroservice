import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingManagementService } from '../../../core/services/coaching-management.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingCmsComponent } from './coaching-cms';

describe('CoachingCmsComponent', () => {
  it('exposes the dedicated homepage, FAQ, and testimonial editors', () => {
    const { fixture } = setup();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Ana sayfa');
    expect(text).toContain('SSS');
    expect(text).toContain('Yorumlar');
  });

  it('loads newsletter subscribers in the Coaching CMS and displays consent state', () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    clickTab(fixture, 'Bülten aboneleri');

    expect(service.getCoachingNewsletterSubscribers).toHaveBeenLastCalledWith(1, 25, '', '');
    expect(fixture.nativeElement.textContent).toContain('learner@example.com');
    expect(fixture.nativeElement.textContent).toContain('Onaylandı');
    expect(fixture.nativeElement.textContent).toContain('Onay zamanı');
  });

  it('loads FAQ entries from their own Coaching CMS group', () => {
    const { fixture, service } = setup();
    fixture.detectChanges();

    clickTab(fixture, 'SSS');

    expect(service.getCmsEntries).toHaveBeenLastCalledWith('Block', 1, 25, '', 'HomeFaq');
    expect(fixture.nativeElement.textContent).toContain('Soru ekle');
  });

  it('loads testimonials from their own Coaching CMS group', () => {
    const { fixture, service } = setup();
    fixture.detectChanges();

    clickTab(fixture, 'Yorumlar');

    expect(service.getCmsEntries).toHaveBeenLastCalledWith('Block', 1, 25, '', 'HomeTestimonials');
    expect(fixture.nativeElement.textContent).toContain('Yorum ekle');
  });

  it('saves FAQ answers as unpublished entries in the FAQ group', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    clickTab(fixture, 'SSS');
    const component = fixture.componentInstance;
    component.startSpecialCreate();
    component.specialDraft.title = 'Koçluk hesabı nasıl açılır?';
    component.specialDraft.content = 'Kayıt sayfasını kullanın.';
    await component.saveSpecialEntry();

    expect(service.createCmsEntry).toHaveBeenCalledWith(expect.objectContaining({
      kind: 'Block', group: 'HomeFaq', title: 'Koçluk hesabı nasıl açılır?',
      content: 'Kayıt sayfasını kullanın.', isPublished: false
    }));
  });

  it('requires explicit publication consent before enabling a testimonial publish save', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    clickTab(fixture, 'Yorumlar');
    const component = fixture.componentInstance;
    expect(component.tab()).toBe('testimonials');
    component.startSpecialCreate();
    component.specialDraft.isPublished = true;
    fixture.detectChanges(false);
    expect(fixture.nativeElement.querySelector('[name="testimonialConsent"]')).not.toBeNull();
    const saveButton = fixture.nativeElement.querySelector('form button[type="submit"]') as HTMLButtonElement;
    expect(saveButton.disabled).toBe(true);
    component.specialDraft.testimonialConsentConfirmed = true;
    expect(component.specialDraft.isPublished).toBe(true);
    expect(component.specialDraft.testimonialConsentConfirmed).toBe(true);
    expect(component.canSaveSpecialEntry()).toBe(true);
  });

  it('limits menu editing to the header and footer menus used by the public site', () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    clickTab(fixture, 'Menüler');
    const addButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Menü öğesi ekle'));
    expect(addButton).toBeTruthy();
    (addButton as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(service.getNavigation).toHaveBeenLastCalledWith('Main', true);
    const menuField = fixture.nativeElement.querySelector('[name="menuName"]') as HTMLSelectElement;
    expect(menuField.tagName).toBe('SELECT');
    expect(Array.from(menuField.options).map(option => option.value)).toEqual(['Main', 'Footer']);
  });

  it('previews the unsaved draft without issuing a write request', () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.draft.title = 'Taslak içerik';
    component.draft.content = 'Henüz kaydedilmemiş metin.';
    component.showPreview(component.draft);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Kaydedilmemiş taslak');
    expect(fixture.nativeElement.textContent).toContain('Henüz kaydedilmemiş metin.');
    expect(service.createCmsEntry).not.toHaveBeenCalled();
    expect(service.updateCmsEntry).not.toHaveBeenCalled();
  });

  it('saves blog author, publication date, and a Coaching media cover from the editor', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.kind.set('Blog');
    component.draft = {
      ...component.draft, kind: 'Blog', title: 'CMS blog yazısı', slug: 'cms-blog-yazisi',
      content: 'Yazı gövdesi', author: 'Ece Öğretmen',
      coverImageUrl: '/api/coaching/cms/media/12345678-1234-1234-1234-123456789abc'
    };
    component.publishedAtLocal = '2026-09-20T09:30';
    await component.saveEntry();

    expect(service.createCmsEntry).toHaveBeenCalledWith(expect.objectContaining({
      author: 'Ece Öğretmen', publishedAt: expect.any(String),
      coverImageUrl: '/api/coaching/cms/media/12345678-1234-1234-1234-123456789abc'
    }));
  });

  function clickTab(fixture: ReturnType<typeof setup>['fixture'], label: string) {
    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(candidate => candidate.textContent?.includes(label));
    expect(button).toBeTruthy();
    (button as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function setup() {
    const subscriber = {
      id: 'subscriber-1', email: 'learner@example.com', status: 'Active', source: 'CoachingWebsite',
      consentTextVersion: 'coaching-newsletter-v1', consentedAt: '2026-09-28T09:00:00Z',
      confirmedAt: '2026-09-28T09:01:00Z', unsubscribedAt: null, createdAt: '2026-09-28T09:00:00Z'
    };
    const service = {
      getCmsEntries: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 })),
      createCmsEntry: vi.fn(() => of({ data: { id: 'created' } })),
      updateCmsEntry: vi.fn(() => of(void 0)),
      deleteCmsEntry: vi.fn(() => of(void 0)),
      getNavigation: vi.fn(() => of([])),
      getCmsMediaAssets: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0 })),
      getCmsRevisions: vi.fn(() => of([])),
      getCoachingNewsletterSubscribers: vi.fn(() => of({ items: [subscriber], pageNumber: 1, pageSize: 25, totalCount: 1 })),
      exportCoachingNewsletterSubscribers: vi.fn(() => of(new Blob(['csv'], { type: 'text/csv' }))),
      unsubscribeCoachingNewsletterSubscriber: vi.fn(() => of(void 0)),
      deleteCoachingNewsletterSubscriber: vi.fn(() => of(void 0))
    };
    TestBed.configureTestingModule({
      imports: [CoachingCmsComponent],
      providers: [
        provideRouter([]),
        { provide: CoachingManagementService, useValue: service },
        { provide: ToasterService, useValue: { confirm: vi.fn(() => Promise.resolve(true)), success: vi.fn() } }
      ]
    });
    return { fixture: TestBed.createComponent(CoachingCmsComponent), service };
  }
});
