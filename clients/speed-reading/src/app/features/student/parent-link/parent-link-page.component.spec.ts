import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ToasterService } from '../../../core/services/toaster.service';
import { ParentLinkPageComponent } from './parent-link-page.component';
import { ParentLink } from './parent-link.service';

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

  beforeEach(() => {
    toaster = jasmine.createSpyObj<ToasterService>('ToasterService', ['confirm', 'success', 'error']);
    toaster.confirm.and.resolveTo(true);
    TestBed.configureTestingModule({
      imports: [ParentLinkPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: ToasterService, useValue: toaster }]
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
    el<HTMLButtonElement>('[data-testid="create-code"]').click();
    const request = http.expectOne('/api/users/me/parent-link-codes');
    expect(request.request.method).toBe('POST');
    request.flush({ code: 'ABCD-EFGH', expiresAt: '2026-11-03T21:00:00Z' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el('[data-testid="parent-code"]').textContent).toContain('ABCD-EFGH');
    expect(el('[data-testid="parent-code-expiry"]').textContent).toContain('2026');
    expect(el('[role="status"]').textContent).toContain('Kod oluşturuldu');
    expect(el<HTMLButtonElement>('[data-testid="create-code"]').textContent).toContain('Yeni kod oluştur');
  });

  it('approves a pending request after the student confirms and reloads the list', async () => {
    await load([link({ status: 'Pending', verifiedAt: null })]);
    expect(page().querySelector('[data-testid="speed-reading-visibility"]')).toBeNull();
    el<HTMLButtonElement>('[data-testid="approve-link"]').click();
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
    el<HTMLButtonElement>('[data-testid="revoke-link"]').click();
    await fixture.whenStable();
    http.expectNone(`${LINKS}/link-1/revoke`);

    toaster.confirm.and.resolveTo(true);
    el<HTMLButtonElement>('[data-testid="revoke-link"]').click();
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

  it('shows an error when the links cannot be loaded', async () => {
    fixture.detectChanges();
    http.expectOne(LINKS).flush({}, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el('[role="alert"]').textContent).toContain('yüklenemedi');
  });
});
