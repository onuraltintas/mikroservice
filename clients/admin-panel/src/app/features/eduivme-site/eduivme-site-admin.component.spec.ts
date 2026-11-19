import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { vi } from 'vitest';
import { SiteEntry } from '../../core/services/eduivme-site.service';
import { ToasterService } from '../../core/services/toaster.service';
import { EduivmeSiteAdminComponent } from './eduivme-site-admin.component';
import { draftFrom, emptyDraft, publishState, requestFrom } from './eduivme-site-fields';

function entry(overrides: Partial<SiteEntry>): SiteEntry {
  return {
    id: 'id', site: 'eduivme', endsAt: null, kind: 'product', group: '', slug: null, title: 'Ürün', subtitle: null, body: '', highlights: [], brand: 'eduivme', icon: null,
    linkLabel: null, linkUrl: null, secondaryLinkLabel: null, secondaryLinkUrl: null, imageId: null, imageUrl: null, ctaTitle: null, ctaBody: null,
    seoTitle: null, seoDescription: null, sortOrder: 10, isPublished: true, publishAt: null, version: 1,
    createdAt: '2026-11-19T06:00:00Z', updatedAt: '2026-11-19T06:00:00Z', ...overrides
  };
}

describe('eduivme sitesi form rules', () => {
  it('sends trimmed text, one highlight per line and no empty optional fields', () => {
    const draft = emptyDraft('product', '', 30);
    Object.assign(draft, { title: '  Kurs Yönetimi ', highlightsText: 'Bir\n\n  İki  \n', linkUrl: ' /urunler/kurs-yonetimi ', linkLabel: '   ', subtitle: '' });
    const request = requestFrom(draft);
    expect(request.title).toBe('Kurs Yönetimi');
    expect(request.highlights).toEqual(['Bir', 'İki']);
    expect(request.linkUrl).toBe('/urunler/kurs-yonetimi');
    expect(request.linkLabel).toBeNull();
    expect(request.subtitle).toBeNull();
    expect(request.version).toBeUndefined();
  });

  it('keeps the version of an edited entry so a stale edit is refused', () => {
    const request = requestFrom(draftFrom(entry({ version: 4, highlights: ['A', 'B'] })));
    expect(request.version).toBe(4);
    expect(request.highlights).toEqual(['A', 'B']);
  });

  it('tells published, scheduled and hidden entries apart', () => {
    const now = new Date('2026-11-19T06:00:00Z');
    expect(publishState(entry({}), now).label).toBe('Yayında');
    expect(publishState(entry({ publishAt: '2026-11-20T06:00:00Z' }), now).label).toBe('Zamanlandı');
    expect(publishState(entry({ isPublished: false }), now).label).toBe('Yayında değil');
    expect(publishState(entry({ endsAt: '2026-11-19T05:00:00Z' }), now).label).toBe('Süresi bitti');
  });

  it('sends a maxkampüs notice with its site, audience and end', () => {
    const draft = emptyDraft('notice', '', 10, null, 'maxkampus');
    Object.assign(draft, { title: 'Bakım', body: 'Pazar 02.00', endsAtLocal: '2026-11-30T10:00' });
    const request = requestFrom(draft);
    expect(request).toMatchObject({ site: 'maxkampus', kind: 'notice', audience: 'all', brand: null });
    expect(request.endsAt).toBe(new Date('2026-11-30T10:00').toISOString());
  });
});

describe('maxkampüs site content', () => {
  const toaster = { success: vi.fn(), error: vi.fn() };

  function setup(view: string) {
    TestBed.configureTestingModule({
      imports: [EduivmeSiteAdminComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { paramMap: new BehaviorSubject(convertToParamMap({ view })), snapshot: { data: { site: 'maxkampus' } } } },
        { provide: ToasterService, useValue: toaster }]
    });
    const fixture = TestBed.createComponent(EduivmeSiteAdminComponent);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('reads only maxkampus.com entries and lists blog posts newest first', async () => {
    const { fixture, component, http } = setup('blog');
    http.expectOne('/api/site-admin/entries?site=maxkampus').flush([
      entry({ id: 'a', site: 'maxkampus', kind: 'post', slug: 'eski', title: 'Eski yazı', publishAt: '2026-11-01T06:00:00Z', category: 'Veli' }),
      entry({ id: 'b', site: 'maxkampus', kind: 'post', slug: 'yeni', title: 'Yeni yazı', publishAt: '2026-11-18T06:00:00Z' })
    ]);
    http.expectOne('/api/site-admin/media').flush([]);
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('maxkampüs sitesi');
    expect(component.groups()[0].entries.map(item => item.id)).toEqual(['b', 'a']);
    expect(component.groups()[0].ordered).toBe(false);

    component.add(component.groups()[0]);
    expect(component.draft()).toMatchObject({ site: 'maxkampus', kind: 'post', brand: null });
    expect(component.slugHint('post', 'ders-programi')).toBe('maxkampus.com/blog/ders-programi');
  });

  it('describes billing plans of Kurs Yönetimi on the pricing page', async () => {
    const { component, http } = setup('plans');
    http.expectOne('/api/site-admin/entries?site=maxkampus').flush([]);
    http.expectOne('/api/site-admin/media').flush([]);
    await new Promise(resolve => setTimeout(resolve));
    http.expectOne('/api/billing/v1/admin/plans').flush([
      { id: 'p1', name: 'Kurum 50', productCodes: ['course-management'], sortOrder: 10 },
      { id: 'p2', name: 'Koçluk', productCodes: ['coaching'], sortOrder: 5 }
    ]);
    await new Promise(resolve => setTimeout(resolve));
    expect(component.plans().map(plan => plan.name)).toEqual(['Kurum 50']);
    component.add(component.groups()[0]);
    expect(component.draft()).toMatchObject({ kind: 'plan', slug: 'p1' });
  });
});

describe('EduivmeSiteAdminComponent', () => {
  const params = new BehaviorSubject(convertToParamMap({ view: 'products' }));
  const toaster = { success: vi.fn(), error: vi.fn() };

  function setup() {
    TestBed.configureTestingModule({
      imports: [EduivmeSiteAdminComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { paramMap: params } },
        { provide: ToasterService, useValue: toaster }]
    });
    const fixture = TestBed.createComponent(EduivmeSiteAdminComponent);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http };
  }

  async function flushLoad(http: HttpTestingController, entries: SiteEntry[]) {
    http.expectOne('/api/site-admin/entries').flush(entries);
    http.expectOne('/api/site-admin/media').flush([]);
    await new Promise(resolve => setTimeout(resolve));
  }

  it('lists the product cards in their order and saves a new one', async () => {
    const { fixture, component, http } = setup();
    await flushLoad(http, [entry({ id: 'b', title: 'İkinci', sortOrder: 20 }), entry({ id: 'a', title: 'Birinci', sortOrder: 10 }), entry({ id: 'f', kind: 'faq', title: 'Soru' })]);
    fixture.detectChanges();
    const rows = Array.from(fixture.nativeElement.querySelectorAll('.row strong')).map(node => (node as HTMLElement).textContent?.trim());
    expect(rows).toEqual(['Birinci', 'İkinci']);

    component.add(component.groups()[0]);
    Object.assign(component.draft()!, { title: 'Yeni ürün', linkUrl: 'https://example.com' });
    const saving = component.save();
    const request = http.expectOne('/api/site-admin/entries');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toMatchObject({ kind: 'product', group: '', title: 'Yeni ürün', sortOrder: 30, isPublished: true });
    request.flush(entry({ id: 'c', title: 'Yeni ürün', sortOrder: 30 }));
    await new Promise(resolve => setTimeout(resolve));
    await flushLoad(http, []);
    await saving;
    expect(toaster.success).toHaveBeenCalled();
    expect(component.draft()?.id).toBe('c');
  });

  it('shows the service\'s message next to the field it refused', async () => {
    const { fixture, component, http } = setup();
    await flushLoad(http, [entry({ id: 'a' })]);
    component.edit(component.groups()[0].entries[0]);
    const saving = component.save();
    http.expectOne('/api/site-admin/entries/a').flush(
      { detail: 'İçerik kaydedilemedi; işaretli alanları düzeltin.', errors: { linkUrl: ['Adres https:// ile ya da / ile başlamalı.'] } },
      { status: 400, statusText: 'Bad Request' });
    await saving;
    fixture.detectChanges();
    expect(component.error('linkUrl')).toBe('Adres https:// ile ya da / ile başlamalı.');
    expect(fixture.nativeElement.textContent).toContain('İçerik kaydedilemedi; işaretli alanları düzeltin.');
  });
});
