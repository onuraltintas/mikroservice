import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { NotificationPreferencesApi, NotificationPreferencesComponent } from '../../../../../shared/notification-preferences.component';

describe('Notification preferences (roadmap O8)', () => {
  const rows = [
    { category: 'Sessions', label: 'Koçluk seansları', inApp: true, email: true },
    { category: 'Exams', label: 'Deneme ve sınav sonuçları', inApp: true, email: false }
  ];

  it('reads and saves the preferences endpoint', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = new NotificationPreferencesApi(TestBed.inject(HttpClient), '/api');
    const http = TestBed.inject(HttpTestingController);
    api.list().subscribe(); http.expectOne(r => r.method === 'GET' && r.url === '/api/notifications/preferences').flush({ data: rows });
    api.save([{ category: 'Exams', inApp: true, email: true }]).subscribe();
    const put = http.expectOne(r => r.method === 'PUT' && r.url === '/api/notifications/preferences');
    expect(put.request.body).toEqual({ preferences: [{ category: 'Exams', inApp: true, email: true }] });
    put.flush({ data: rows }); http.verify();
  });

  function setup() {
    const api = { list: vi.fn(() => of(rows)), save: vi.fn((changes: unknown[]) => of(rows.map(r => r.category === 'Exams' ? { ...r, email: true } : r))) };
    TestBed.configureTestingModule({ imports: [NotificationPreferencesComponent] });
    const fixture = TestBed.createComponent(NotificationPreferencesComponent);
    fixture.componentRef.setInput('api', api); fixture.detectChanges();
    const el = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLInputElement;
    return { fixture, api, el };
  }

  it('shows each category with app and e-mail choices and saves only what changed', () => {
    const { fixture, api, el } = setup();
    expect(fixture.nativeElement.textContent).toContain('Koçluk seansları');
    expect(el('pref-email-Sessions').checked).toBe(true); expect(el('pref-email-Exams').checked).toBe(false);
    expect((el('pref-save') as unknown as HTMLButtonElement).disabled).toBe(true);
    el('pref-email-Exams').click(); fixture.detectChanges();
    (el('pref-save') as unknown as HTMLButtonElement).click(); fixture.detectChanges();
    expect(api.save).toHaveBeenCalledWith([{ category: 'Exams', inApp: true, email: true, sms: false, whatsApp: false }]);
    expect(fixture.nativeElement.textContent).toContain('kaydedildi');
    expect(fixture.componentInstance.dirty()).toBe(false);
  });

  it('keeps the choices when saving fails', () => {
    const { fixture, api, el } = setup();
    api.save.mockReturnValueOnce(throwError(() => new Error('down')));
    el('pref-app-Sessions').click(); fixture.detectChanges(); fixture.componentInstance.save(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('kaydedilemedi');
    expect(fixture.componentInstance.items().find(x => x.category === 'Sessions')!.inApp).toBe(false);
    fixture.componentInstance.reset(); expect(fixture.componentInstance.dirty()).toBe(false);
  });
  it('offers SMS and WhatsApp only where available, keeps locked e-mail on, and stores commercial consent (roadmap O9)', () => {
    const textRows = [
      { category: 'Sessions', label: 'Koçluk seansları', inApp: true, email: true, emailEditable: true, textAvailable: true, sms: false, whatsApp: false },
      { category: 'Subscription', label: 'Abonelik bitişi', inApp: true, email: true, emailEditable: false, textAvailable: true, sms: false, whatsApp: false },
      { category: 'Exams', label: 'Sınavlar', inApp: true, email: false, emailEditable: true, textAvailable: false }
    ];
    const consents = [{ channel: 'Sms', label: "Kampanya SMS'leri", granted: false, iysStatus: 'NotRecorded', changedAt: null }];
    const api = {
      list: vi.fn(() => of(textRows)), save: vi.fn(() => of(textRows)),
      consents: vi.fn(() => of(consents)), saveConsents: vi.fn(() => of([{ ...consents[0], granted: true, iysStatus: 'Pending' }]))
    };
    TestBed.configureTestingModule({ imports: [NotificationPreferencesComponent] });
    const fixture = TestBed.createComponent(NotificationPreferencesComponent);
    fixture.componentRef.setInput('api', api); fixture.detectChanges();
    const el = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLInputElement | null;
    expect(el('pref-sms-Sessions')).not.toBeNull(); expect(el('pref-sms-Exams')).toBeNull();
    expect(el('pref-email-Subscription')!.disabled).toBe(true);
    expect(el('text-note')!.textContent).toContain('henüz açılmadı');
    el('pref-sms-Sessions')!.click(); fixture.detectChanges(); fixture.componentInstance.save();
    expect(api.save).toHaveBeenCalledWith([{ category: 'Sessions', inApp: true, email: true, sms: true, whatsApp: false }]);
    el('consent-box-Sms')!.click(); fixture.detectChanges();
    expect(api.saveConsents).toHaveBeenCalledWith([{ channel: 'Sms', granted: true }]);
    expect(fixture.nativeElement.textContent).toContain("İYS'ye aktarılmayı bekliyor");
  });
});
