import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MaxkampusApi } from '../core/api';
import { Seo } from '../core/seo';
import { LegalPageComponent } from './content-pages.component';

const flush = () => new Promise(resolve => setTimeout(resolve, 0));

describe('maxkampus.com SEO (27.11.2026)', () => {
  it('adds the brand to a title only when the title does not name it already', () => {
    TestBed.configureTestingModule({});
    const seo = TestBed.inject(Seo);
    seo.set({ title: 'maxkampüs: kurs ve özel ders yönetimi', path: '/' });
    expect(document.title).toBe('maxkampüs: kurs ve özel ders yönetimi');
    seo.set({ title: 'Fiyatlar', path: '/fiyatlar' });
    expect(document.title).toBe('Fiyatlar | maxkampüs');
  });

  it('follows the slug when a footer link moves from one legal document to another', async () => {
    const pages: Record<string, { title: string }> = { privacy: { title: 'Gizlilik politikası' }, kvkk: { title: 'KVKK aydınlatma metni' } };
    const api = { legal: vi.fn(async (slug: string) => ({ ...pages[slug], content: 'Metin', version: 1, createdAt: '2026-11-01T00:00:00Z' })) };
    TestBed.configureTestingModule({ imports: [LegalPageComponent], providers: [provideRouter([]), { provide: MaxkampusApi, useValue: api }] });
    const fixture = TestBed.createComponent(LegalPageComponent);
    fixture.componentRef.setInput('slug', 'privacy');
    fixture.detectChanges();
    await flush();
    expect(document.title).toBe('Gizlilik politikası | maxkampüs');

    fixture.componentRef.setInput('slug', 'kvkk');
    fixture.detectChanges();
    await flush();
    expect(api.legal).toHaveBeenCalledWith('kvkk');
    expect(fixture.componentInstance.title()).toBe('KVKK aydınlatma metni');
    expect(document.title).toBe('KVKK aydınlatma metni | maxkampüs');
    expect(document.head.querySelector('link[rel="canonical"]')?.getAttribute('href')).toBe('https://maxkampus.com/yasal/kvkk');
  });
});
