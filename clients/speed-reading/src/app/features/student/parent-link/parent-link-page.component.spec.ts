import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ToasterService } from '../../../core/services/toaster.service';
import { ParentLinkPageComponent } from './parent-link-page.component';
import { ParentLink } from './parent-link.service';
import { SHARE_NAVIGATOR, ShareNavigator } from './share-navigator';
import { parentPanelLinks } from '../../../core/utils/parent-panel-links';
import { routes } from '../../../app.routes';

const LINKS = '/api/users/me/parent-links';

function link(overrides: Partial<ParentLink> = {}): ParentLink {
  return {
    id: 'link-1', parentUserId: 'parent-1', parentName: 'Ayşe Yılmaz', studentUserId: 'student-1', studentName: 'Elif Yılmaz',
    relationship: 'Mother', status: 'Verified', requestedAt: '2026-10-20T08:00:00Z', verifiedAt: '2026-10-21T08:00:00Z',
    products: [
      { product: 'Coaching', studentHasAccess: true, visibleToParent: false },
      { product: 'SpeedReading', studentHasAccess: true, visibleToParent: true }
    ],
    ...overrides
  };
}

describe('ParentLinkPageComponent (Veli bağlantısı)', () => {
  let fixture: ComponentFixture<ParentLinkPageComponent>;
  let http: HttpTestingController;
  let toaster: jasmine.SpyObj<ToasterService>;
  let nav: ShareNavigator;

  beforeEach(() => {
    nav = { clipboard: { writeText: jasmine.createSpy('writeText').and.resolveTo() } };
    toaster = jasmine.createSpyObj<ToasterService>('ToasterService', ['confirm', 'success', 'error']);
    toaster.confirm.and.resolveTo(true);
    TestBed.configureTestingModule({
      imports: [ParentLinkPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: ToasterService, useValue: toaster },
        { provide: SHARE_NAVIGATOR, useFactory: () => nav }]
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ParentLinkPageComponent);
  });

  afterEach(() => http.verify());

  function page(): HTMLElement { return fixture.nativeElement as HTMLElement; }
  function el<T extends HTMLElement>(selector: string): T { return page().querySelector<T>(selector)!; }
  async function load(links: ParentLink[]): Promise<void> {
    fixture.detectChanges();
    http.expectOne(LINKS).flush(links);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('explains what the parent sees and shows an empty state without links', async () => {
    await load([]);
    expect(page().querySelector('h1')?.textContent).toContain('Veli bağlantısı');
    const text = page().textContent ?? '';
    expect(text).toContain('okuma hızını');
    expect(text).toContain('hiçbir şeyi değiştiremez');
    expect(el('[data-testid="parent-links-empty"]')).toBeTruthy();
    expect(el('[role="status"][aria-live="polite"]')).toBeTruthy();
  });

  it('creates a one-time code and shows it with its expiry', async () => {
    await load([]);
    el<HTMLButtonElement>('[data-testid="create-code"] button').click();
    const request = http.expectOne('/api/users/me/parent-link-codes');
    expect(request.request.method).toBe('POST');
    request.flush({ code: 'ABCD-EFGH', expiresAt: '2026-11-03T21:00:00Z' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el('[data-testid="parent-code"]').textContent).toContain('ABCD-EFGH');
    expect(el('[data-testid="parent-code-expiry"]').textContent).toContain('2026');
    expect(el('[role="status"]').textContent).toContain('Kod oluşturuldu');
    expect(el<HTMLButtonElement>('[data-testid="create-code"] button').textContent).toContain('Yeni kod oluştur');
  });

  async function createCode(expiresInDays = 7): Promise<void> {
    await load([]);
    el<HTMLButtonElement>('[data-testid="create-code"] button').click();
    const expiresAt = new Date(Date.now() + expiresInDays * 86_400_000 - 60_000).toISOString();
    http.expectOne('/api/users/me/parent-link-codes').flush({ code: 'ABCD-EFGH', expiresAt });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const MESSAGE = () => `Veli panelinden beni ekle: ${parentPanelLinks().register} · Kod: ABCD-EFGH (7 gün geçerli)`;

  it('keeps button icon ligatures out of the accessible names', async () => {
    await createCode();
    const icons = page().querySelectorAll('sp-button .material-icons');
    expect(icons.length).toBeGreaterThan(0);
    icons.forEach(icon => expect(icon.getAttribute('aria-hidden')).toBe('true'));
  });

  it('offers no share message before a code exists', async () => {
    await load([]);
    expect(page().querySelector('[data-testid="parent-share"]')).toBeNull();
  });

  it('shows a ready-to-share message with the parent-panel link, the code and its validity', async () => {
    await createCode();
    expect(el('[data-testid="parent-share-message"]').textContent?.trim()).toBe(MESSAGE());
  });

  it('copies the message with the Clipboard API and confirms it in the live region', async () => {
    await createCode();
    el<HTMLButtonElement>('[data-testid="copy-share"] button').click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(nav.clipboard!.writeText).toHaveBeenCalledOnceWith(MESSAGE());
    const live = el('[role="status"][aria-live="polite"]');
    expect(live.textContent).toContain('Mesaj kopyalandı');
  });

  it('falls back to a hidden text area copy when the Clipboard API is missing', async () => {
    nav = {};
    const exec = spyOn(document, 'execCommand').and.returnValue(true);
    await createCode();
    el<HTMLButtonElement>('[data-testid="copy-share"] button').click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(exec).toHaveBeenCalledWith('copy');
    expect(document.querySelector('textarea[data-share-fallback]')).toBeNull();
    expect(el('[role="status"]').textContent).toContain('Mesaj kopyalandı');
  });

  it('says how to copy by hand when copying is not possible', async () => {
    nav = { clipboard: { writeText: jasmine.createSpy('writeText').and.rejectWith(new Error('denied')) } };
    spyOn(document, 'execCommand').and.returnValue(false);
    await createCode();
    el<HTMLButtonElement>('[data-testid="copy-share"] button').click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el('[role="alert"]').textContent).toContain('kendin kopyalayabilirsin');
  });

  it('hides the share button when the browser cannot share', async () => {
    await createCode();
    expect(page().querySelector('[data-testid="native-share"]')).toBeNull();
  });

  it('shares the message natively where supported and stays quiet when the student cancels', async () => {
    const share = jasmine.createSpy('share').and.resolveTo();
    nav = { share };
    await createCode();
    el<HTMLButtonElement>('[data-testid="native-share"] button').click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(share).toHaveBeenCalledOnceWith({ title: 'Veli bağlantı kodu', text: MESSAGE() });

    share.and.rejectWith(new DOMException('cancelled', 'AbortError'));
    el<HTMLButtonElement>('[data-testid="native-share"] button').click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(page().querySelector('[role="alert"]')).toBeNull();
  });

  it('approves a pending request after the student confirms and reloads the list', async () => {
    await load([link({ status: 'Pending', verifiedAt: null })]);
    expect(page().querySelector('[data-testid="speed-reading-visibility"]')).toBeNull();
    el<HTMLButtonElement>('[data-testid="approve-link"] button').click();
    await fixture.whenStable();

    expect(toaster.confirm).toHaveBeenCalled();
    const approve = http.expectOne(`${LINKS}/link-1/approve`);
    expect(approve.request.method).toBe('POST');
    approve.flush(null);
    await fixture.whenStable();
    http.expectOne(LINKS).flush([link()]);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el('[data-testid="speed-reading-visibility"]')).toBeTruthy();
  });

  it('removes a link only after confirmation', async () => {
    await load([link()]);
    toaster.confirm.and.resolveTo(false);
    el<HTMLButtonElement>('[data-testid="revoke-link"] button').click();
    await fixture.whenStable();
    http.expectNone(`${LINKS}/link-1/revoke`);

    toaster.confirm.and.resolveTo(true);
    el<HTMLButtonElement>('[data-testid="revoke-link"] button').click();
    await fixture.whenStable();
    const revoke = http.expectOne(`${LINKS}/link-1/revoke`);
    expect(revoke.request.method).toBe('POST');
    revoke.flush(null);
    await fixture.whenStable();
    http.expectOne(LINKS).flush([]);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el('[data-testid="parent-links-empty"]')).toBeTruthy();
  });

  it('turns speed-reading visibility off while keeping the coaching choice unchanged', async () => {
    await load([link()]);
    const toggle = el<HTMLInputElement>('[data-testid="speed-reading-visibility"]');
    expect(toggle.getAttribute('role')).toBe('switch');
    expect(toggle.checked).toBeTrue();
    expect(page().querySelector(`label[for="${toggle.id}"]`)?.textContent).toContain('Hızlı okuma ilerlememi görsün');

    toggle.click();
    const put = http.expectOne(`${LINKS}/link-1/products`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ coaching: false, speedReading: false });
    put.flush(null);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el<HTMLInputElement>('[data-testid="speed-reading-visibility"]').checked).toBeFalse();
    expect(el('[role="status"]').textContent).toContain('görmeyecek');
  });

  it('keeps the previous choice and says so when saving the switch fails', async () => {
    await load([link({ products: undefined })]);
    el<HTMLInputElement>('[data-testid="speed-reading-visibility"]').click();
    const put = http.expectOne(`${LINKS}/link-1/products`);
    expect(put.request.body).toEqual({ coaching: true, speedReading: false });
    put.flush({ error: { code: 'ParentLink.NotFound', message: 'Veli bağlantısı bulunamadı.' } }, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el<HTMLInputElement>('[data-testid="speed-reading-visibility"]').checked).toBeTrue();
    expect(el('[role="alert"]').textContent).toContain('Veli bağlantısı bulunamadı.');
  });

  it('is routed at /veli-baglantisi for students, before the public catch-all', () => {
    const index = routes.findIndex(route => route.path === 'veli-baglantisi');
    expect(index).toBeGreaterThan(-1);
    expect(index).toBeLessThan(routes.findIndex(route => route.path === ''));
    expect(routes[index].data?.['role']).toEqual(['Student']);
  });

  it('shows an error when the links cannot be loaded', async () => {
    fixture.detectChanges();
    http.expectOne(LINKS).flush({}, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el('[role="alert"]').textContent).toContain('yüklenemedi');
  });
});
