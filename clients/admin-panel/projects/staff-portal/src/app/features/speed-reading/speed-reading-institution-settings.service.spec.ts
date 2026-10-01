import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { SpeedReadingInstitutionSettingsService } from './speed-reading-institution-settings.service';

describe('SpeedReadingInstitutionSettingsService', () => {
  let http: HttpTestingController;
  let service: SpeedReadingInstitutionSettingsService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SpeedReadingInstitutionSettingsService, provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(SpeedReadingInstitutionSettingsService);
  });

  afterEach(() => http.verify());

  it('resolves the signed-in Speed Reading institution before reading its shared contact profile', async () => {
    const current = firstValueFrom(service.getMyInstitution());
    const currentRequest = http.expectOne('/api/institution/speed-reading/me');
    currentRequest.flush({ institutionId: 'institution-1', institutionName: 'Örnek Okul' });
    await expect(current).resolves.toMatchObject({ institutionId: 'institution-1' });

    const profile = firstValueFrom(service.getInstitutionProfile('institution-1'));
    const profileRequest = http.expectOne('/api/v1/institutions/institution-1');
    profileRequest.flush({ id: 'institution-1', name: 'Örnek Okul', email: 'info@example.test' });
    await expect(profile).resolves.toMatchObject({ name: 'Örnek Okul', contactEmail: 'info@example.test' });
  });

  it('updates only shared institution contact fields and reads the Hızlı Okuma license separately', async () => {
    const update = firstValueFrom(service.updateInstitutionProfile('institution-1', {
      name: 'Yeni Okul', email: 'info@example.test', phone: '5555555555', address: 'Merkez', provinceId: 'city-1', districtId: 'district-1',
    }));
    const updateRequest = http.expectOne('/api/v1/institutions/institution-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual({
      name: 'Yeni Okul', email: 'info@example.test', phone: '5555555555', address: 'Merkez', provinceId: 'city-1', districtId: 'district-1',
    });
    updateRequest.flush(null);
    await update;

    const license = firstValueFrom(service.getMySpeedReadingAccess());
    const licenseRequest = http.expectOne('/api/speed-reading/subscriptions/institution-access/my');
    licenseRequest.flush({ institutionId: 'institution-1', plan: { name: 'Kurumsal' }, seatCount: 30, usedSeatCount: 12 });
    await expect(license).resolves.toMatchObject({ seatCount: 30, usedSeatCount: 12 });
  });

  it('loads province and province-scoped district options', async () => {
    const provinces = firstValueFrom(service.getProvinces());
    http.expectOne('/api/locations/provinces').flush([{ id: 'city-1', name: 'İstanbul' }]);
    await expect(provinces).resolves.toHaveLength(1);

    const districts = firstValueFrom(service.getDistricts('city-1'));
    http.expectOne('/api/locations/provinces/city-1/districts').flush([{ id: 'district-1', name: 'Kadıköy', provinceId: 'city-1' }]);
    await expect(districts).resolves.toHaveLength(1);
  });
});
