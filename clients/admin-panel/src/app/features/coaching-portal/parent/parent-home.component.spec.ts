import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingParentService } from './coaching-parent.service';
import { ParentHomeComponent } from './parent-home.component';

// V4 (01.11.2026): one card per child with the products the child shares, a status line per product and link management.
describe('ParentHomeComponent', () => {
  const both = [{ product: 'Coaching', studentHasAccess: true, visibleToParent: true }, { product: 'SpeedReading', studentHasAccess: true, visibleToParent: true }];
  const coachingOnly = [{ product: 'Coaching', studentHasAccess: true, visibleToParent: true }, { product: 'SpeedReading', studentHasAccess: false, visibleToParent: true }];
  const readingOnly = [{ product: 'Coaching', studentHasAccess: false, visibleToParent: true }, { product: 'SpeedReading', studentHasAccess: true, visibleToParent: true }];
  const hidden = [{ product: 'Coaching', studentHasAccess: true, visibleToParent: false }, { product: 'SpeedReading', studentHasAccess: false, visibleToParent: true }];
  const base = { parentName: 'Ayşe Veli', relationship: 'Mother', status: 'Verified', requestedAt: '2026-10-01T00:00:00Z', verifiedAt: '2026-10-02T00:00:00Z' };
  const elif = { ...base, id: 'link-1', studentUserId: 'child-1', studentName: 'Elif Yılmaz', products: both };
  const can = { ...base, id: 'link-2', studentUserId: 'child-2', studentName: 'Can Yılmaz', relationship: 'Father', products: readingOnly };
  const ada = { ...base, id: 'link-3', studentUserId: 'child-3', studentName: 'Ada Yılmaz', products: coachingOnly };
  const mert = { ...base, id: 'link-4', studentUserId: 'child-4', studentName: 'Mert Yılmaz', products: hidden };
  const pending = { ...base, id: 'link-5', studentUserId: 'child-5', studentName: 'Deniz Yılmaz', status: 'Pending', verifiedAt: null };
  const confirm = vi.fn().mockResolvedValue(true);

  function service(overrides: Record<string, unknown> = {}) {
    return {
      getLinks: vi.fn(() => of([elif, can, ada, mert, pending])),
      requestLink: vi.fn(() => of(pending)),
      revokeLink: vi.fn(() => of(undefined)),
      getCoachingWeek: vi.fn(() => of({ weekStart: '2026-10-26', planned: 14, completed: 9, overdue: [], nextSession: null, examCountdown: null, attention: [] })),
      getSpeedReadingOverview: vi.fn(() => of({ program: { name: 'Temel', level: 2, currentDay: 12, totalDays: 28 }, thisWeek: { minutes: 40, activeDays: 3, exercisesCompleted: 6 } })),
      ...overrides
    };
  }
  function setup(mock: object) {
    confirm.mockClear();
    TestBed.configureTestingModule({ imports: [ParentHomeComponent], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
      { provide: CoachingParentService, useValue: mock },
      { provide: ToasterService, useValue: { confirm, success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }] });
    const fixture = TestBed.createComponent(ParentHomeComponent); fixture.detectChanges(); return fixture;
  }
  const card = (root: HTMLElement, name: string) =>
    [...root.querySelectorAll('[data-testid="child-card"]')].find(el => el.textContent?.includes(name)) as HTMLElement;

  it('shows one card per linked child with product badges and a status per product', () => {
    const fixture = setup(service());
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelectorAll('[data-testid="child-card"]').length).toBe(4);
    const first = card(root, 'Elif Yılmaz');
    expect([...first.querySelectorAll('[data-testid="product-badge"]')].map(b => b.textContent?.trim())).toEqual(['Koçluk', 'Hızlı okuma']);
    expect(first.textContent).toContain('Anne');
    expect(first.textContent).toContain('Bu hafta 9/14 görev');
    expect(first.textContent).toContain('Program 12/28. gün');
    expect(first.querySelector('a[href="/coaching-portal/parent/children/child-1?urun=kocluk"]')).toBeTruthy();
    expect(first.querySelector('a[href="/coaching-portal/parent/children/child-1?urun=hizli-okuma"]')).toBeTruthy();
  });

  it('asks only the products a child shares', () => {
    const mock = service(); const fixture = setup(mock);
    const root = fixture.nativeElement as HTMLElement;
    expect(mock.getCoachingWeek).toHaveBeenCalledWith('child-1');
    expect(mock.getCoachingWeek).toHaveBeenCalledWith('child-3');
    expect(mock.getCoachingWeek).not.toHaveBeenCalledWith('child-2');
    expect(mock.getCoachingWeek).not.toHaveBeenCalledWith('child-4');
    expect(mock.getSpeedReadingOverview).toHaveBeenCalledWith('child-2');
    expect(mock.getSpeedReadingOverview).not.toHaveBeenCalledWith('child-3');
    const reading = card(root, 'Can Yılmaz');
    expect([...reading.querySelectorAll('[data-testid="product-badge"]')].map(b => b.textContent?.trim())).toEqual(['Hızlı okuma']);
    expect(reading.textContent).toContain('Baba');
  });

  it('explains a child who shares nothing and offers no page to open', () => {
    const fixture = setup(service());
    const hiddenCard = card(fixture.nativeElement, 'Mert Yılmaz');
    expect(hiddenCard.textContent).toContain('paylaşmayı kapattı');
    expect(hiddenCard.querySelector('a[href^="/coaching-portal/parent/children/"]')).toBeNull();
  });

  it('keeps the card when a status cannot be read', () => {
    const fixture = setup(service({ getCoachingWeek: vi.fn(() => throwError(() => ({ status: 403 }))) }));
    const first = card(fixture.nativeElement, 'Elif Yılmaz');
    expect(first.textContent).toContain('Program 12/28. gün');
    expect(first.textContent).toContain('Durum şu an alınamadı');
  });

  it('lists pending requests and lets the parent withdraw them', async () => {
    const mock = service(); const fixture = setup(mock);
    const section = fixture.nativeElement.querySelector('[data-testid="pending-links"]') as HTMLElement;
    expect(section.textContent).toContain('Deniz Yılmaz'); expect(section.textContent).toContain('onayını bekliyor');
    (section.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(confirm).toHaveBeenCalled();
    expect(mock.revokeLink).toHaveBeenCalledWith('link-5');
  });

  it('removes a link only after confirmation', async () => {
    const mock = service(); const fixture = setup(mock);
    confirm.mockResolvedValueOnce(false);
    const remove = card(fixture.nativeElement, 'Elif Yılmaz').querySelector('[data-testid="remove-link"]') as HTMLButtonElement;
    expect(remove.textContent).toContain('Bağlantıyı kaldır');
    remove.click(); await fixture.whenStable();
    expect(mock.revokeLink).not.toHaveBeenCalled();
    remove.click(); await fixture.whenStable();
    expect(confirm.mock.calls.at(-1)?.[0]).toContain('Elif Yılmaz');
    expect(mock.revokeLink).toHaveBeenCalledWith('link-1');
    expect(mock.getLinks).toHaveBeenCalledTimes(2);
  });

  it('sends the code with the chosen relationship and shows why a code failed', async () => {
    const mock = service({ getLinks: vi.fn(() => of([])) }); const fixture = setup(mock); const component = fixture.componentInstance;
    component.code = ' abcd-2345 '; component.relationship = 'Father';
    await component.submit();
    expect(mock.requestLink).toHaveBeenCalledWith('abcd-2345', 'Father');
    mock.requestLink.mockReturnValueOnce(throwError(() => ({ error: { error: { description: 'Kod geçersiz veya süresi dolmuş.' } } })) as never);
    component.code = 'WXYZ-9999'; await component.submit();
    expect(component.error()).toContain('Kod geçersiz');
  });

  it('points to where the child creates a code today', () => {
    const fixture = setup(service({ getLinks: vi.fn(() => of([])) }));
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Profil ve ayarlar › Profilim');
    expect(text).toContain('Veli bağlantısı');
    expect(text).not.toContain('Ayarlar sayfasından');
  });

  it('leaves notification choices and personal data to the account menu', () => {
    const fixture = setup(service());
    expect(fixture.nativeElement.querySelector('edu-notification-preferences')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-student-data-export')).toBeNull();
  });
});
