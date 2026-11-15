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

  it('loads own institution and saves contact fields in the shared institution profile', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionSettingsComponent);
    fixture.detectChanges();
    http.expectOne('/api/institution/speed-reading/me').flush({ institutionId: 'institution-1', institutionName: 'Örnek Okul' });
    http.expectOne('/api/v1/institutions/institution-1').flush({
      id: 'institution-1', name: 'Örnek Okul', email: 'info@example.test', phone: '5555555555', address: 'Merkez',
      provinceId: 'city-1', districtId: 'district-1', studentCount: 12, teacherCount: 3,
    });
    http.expectOne('/api/locations/provinces').flush([{ id: 'city-1', name: 'İstanbul' }]);
    http.expectOne('/api/locations/provinces/city-1/districts').flush([{ id: 'district-1', name: 'Kadıköy', provinceId: 'city-1' }]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Kurum ayarları');
    // Plan, dates and seats are on the "Abonelik ve koltuklar" page (billing service), not here.
    expect(fixture.nativeElement.textContent).not.toContain('lisansı');

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
