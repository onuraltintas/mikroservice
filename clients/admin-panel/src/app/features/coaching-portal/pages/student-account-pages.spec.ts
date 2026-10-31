import { CUSTOM_ELEMENTS_SCHEMA, Component, Input, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingStudentNavigationService } from '../../../core/services/coaching-student-navigation.service';
import { CoachingParentService } from '../parent/coaching-parent.service';
import { CoachingStudentTeachersService } from '../../../core/services/coaching-student-teachers.service';
import { COACHING_PORTAL_ROUTES } from '../coaching-portal.routes';
import { StudentNotificationSettingsComponent } from './student-notification-settings.component';
import { StudentStudyPreferencesComponent } from './student-study-preferences.component';
import { StudentCoachingProfileComponent } from './student-coaching-profile.component';
import { StudentPaymentsComponent } from './student-payments.component';
import { StudentPersonalDataComponent } from '../layout/student-personal-data.component';

// T5 (31.10.2026): Profil ve ayarlar has four sections — Profilim, Çalışma tercihlerim, Bildirimler, Görünüm — and
// Ödemeler is one page: Abonelik durumu, Havale bildirimleri, Ücretler, Faturalar.
const child = (parent: string, path: string) => COACHING_PORTAL_ROUTES.find(r => r.path === parent)!.children!.find(c => c.path === path)!;

describe('Profil ve ayarlar sections', () => {
  it('serves each section from its own address', async () => {
    expect(await child('account/settings', '').loadComponent!()).toBe(StudentStudyPreferencesComponent);
    expect(await child('account/settings', 'profile').loadComponent!()).toBe(StudentCoachingProfileComponent);
    expect(await child('account/settings', 'notifications').loadComponent!()).toBe(StudentNotificationSettingsComponent);
    expect(child('account/settings', '').canDeactivate?.length).toBe(1);
    expect(child('account/settings', 'profile').canDeactivate?.length).toBe(1);
  });

  it('asks before leaving when either the setup or the time zone has unsaved changes', async () => {
    const component = TestBed.runInInjectionContext(() => new StudentStudyPreferencesComponent());
    component.setup = { canLeavePage: vi.fn(async () => true) } as never;
    component.timeZone = { canLeavePage: vi.fn(async () => false) } as never;
    expect(await component.canLeavePage()).toBe(false);
    component.timeZone = { canLeavePage: vi.fn(async () => true) } as never;
    expect(await component.canLeavePage()).toBe(true);
  });

  it('puts the e-mail notifications and the calendar link under Bildirimler', async () => {
    TestBed.configureTestingModule({ imports: [StudentNotificationSettingsComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentNotificationSettingsComponent); fixture.detectChanges(); await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')?.textContent?.trim()).toBe('Bildirimler');
    expect(el.querySelector('edu-notification-preferences')).not.toBeNull();
    expect(el.querySelector('edu-coaching-calendar-subscription')).not.toBeNull();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url === '/api/notifications/preferences');
    expect(http.expectOne(r => r.url === '/api/calendar/subscription').request.params.get('role')).toBe('student');
  });

  it('shows the profile with the teachers and parent links under Profilim', () => {
    TestBed.configureTestingModule({ imports: [StudentCoachingProfileComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: CoachingParentService, useValue: { getLinks: () => of([]) } },
      { provide: CoachingStudentTeachersService, useValue: { list: () => of([]) } }] });
    const fixture = TestBed.createComponent(StudentCoachingProfileComponent); fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')?.textContent?.trim()).toBe('Profilim');
    expect(el.querySelector('edu-coaching-profile')).not.toBeNull();
    expect(el.querySelector('app-student-teachers')).not.toBeNull();
    expect(el.querySelector('app-student-parent-links')).not.toBeNull();
    expect(el.textContent).not.toContain('←');
  });
});

@Component({ selector: 'app-coaching-subscription', standalone: true, template: '<h2>Abonelik durumu</h2><h2>Havale bildirimleri</h2><ng-content select="[fees]" /><h2>Faturalar</h2>' })
class SubscriptionStub {}
@Component({ selector: 'app-coaching-finance', standalone: true, template: 'ücret listesi' })
class FinanceStub { @Input() mode = ''; }

describe('Ödemeler page', () => {
  function render(showFees: boolean) {
    TestBed.configureTestingModule({ imports: [StudentPaymentsComponent], providers: [
      { provide: CoachingStudentNavigationService, useValue: { showFees: signal(showFees) } }] });
    TestBed.overrideComponent(StudentPaymentsComponent, { set: { imports: [SubscriptionStub, FinanceStub] } });
    const fixture = TestBed.createComponent(StudentPaymentsComponent); fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the four sections in order on one page', () => {
    const el = render(true);
    expect([...el.querySelectorAll('h2')].map(h => h.textContent?.trim())).toEqual(['Abonelik durumu', 'Havale bildirimleri', 'Ücretler', 'Faturalar']);
    expect(el.querySelector('app-coaching-finance')).not.toBeNull();
  });

  it('leaves out Ücretler when the student has no coaching fees', () => {
    const el = render(false);
    expect([...el.querySelectorAll('h2')].map(h => h.textContent?.trim())).toEqual(['Abonelik durumu', 'Havale bildirimleri', 'Faturalar']);
  });
});

describe('Kişisel verilerim', () => {
  it('keeps a short heading and intro', () => {
    TestBed.configureTestingModule({ imports: [StudentPersonalDataComponent] });
    TestBed.overrideComponent(StudentPersonalDataComponent, { set: { imports: [], schemas: [CUSTOM_ELEMENTS_SCHEMA] } });
    const fixture = TestBed.createComponent(StudentPersonalDataComponent); fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')?.textContent?.trim()).toBe('Kişisel verilerim');
    expect(el.querySelector('h1 + p')?.textContent?.trim()).toBe('Hakkında tuttuğumuz bilgilerin bir kopyasını istediğin zaman indirebilirsin.');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
