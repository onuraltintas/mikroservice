import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingInstitutionSettingsComponent } from './speed-reading-institution-settings.component';

describe('SpeedReadingInstitutionSettingsComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SpeedReadingInstitutionSettingsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads own institution, displays product license and saves contact fields in the shared institution profile', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionSettingsComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({ institutionId: 'institution-1', institutionName: 'Örnek Okul' });
    http.expectOne('/api/v1/institutions/institution-1').flush({
      id: 'institution-1', name: 'Örnek Okul', email: 'info@example.test', phone: '5555555555', address: 'Merkez',
      provinceId: 'city-1', districtId: 'district-1', studentCount: 12, teacherCount: 3, maxStudents: 100, maxTeachers: 10,
    });
    http.expectOne('/api/speed-reading/subscriptions/institution-access/my').flush({
      institutionId: 'institution-1', plan: { name: 'Kurumsal' }, status: 'Active', seatCount: 30, usedSeatCount: 12,
      startDate: '2026-01-01T00:00:00Z', endDate: '2027-01-01T00:00:00Z',
    });
    http.expectOne('/api/locations/provinces').flush([{ id: 'city-1', name: 'İstanbul' }]);
    http.expectOne('/api/locations/provinces/city-1/districts').flush([{ id: 'district-1', name: 'Kadıköy', provinceId: 'city-1' }]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Kurum ayarları');
    expect(fixture.nativeElement.textContent).toContain('Kurumsal');
    expect(fixture.nativeElement.textContent).toContain('12 / 30');

    fixture.componentInstance.name = 'Yeni Okul';
    fixture.componentInstance.saveProfile();
    const update = http.expectOne('/api/v1/institutions/institution-1');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toMatchObject({ name: 'Yeni Okul', email: 'info@example.test', provinceId: 'city-1', districtId: 'district-1' });
    update.flush(null);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Kurum bilgileri güncellendi');
  });
});
