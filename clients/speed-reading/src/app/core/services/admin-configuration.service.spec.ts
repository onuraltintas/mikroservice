import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminConfigurationService } from './admin-configuration.service';

describe('AdminConfigurationService', () => {
  let service: AdminConfigurationService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AdminConfigurationService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AdminConfigurationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads configurations from the identity service contract', () => {
    service.getAll().subscribe(configurations => expect(configurations[0].key).toBe('Site.Name'));

    const request = http.expectOne('/api/configurations');
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 'config-1', key: 'Site.Name', value: 'Hızlı Okuma', description: 'Site adı', dataType: 0, isPublic: true, group: 'Platform' }]);
  });

  it('updates one configuration and can refresh the server cache', () => {
    service.update('Site.Name', 'Yeni Ad').subscribe();
    const update = http.expectOne('/api/configurations/Site.Name');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ value: 'Yeni Ad' });
    update.flush(null);

    service.refreshCache().subscribe();
    const refresh = http.expectOne('/api/configurations/refresh-cache');
    expect(refresh.request.method).toBe('POST');
    refresh.flush({ message: 'Cache refreshed successfully' });
  });
});
