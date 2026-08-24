import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingAdminService } from './speed-reading-admin.service';

describe('SpeedReadingAdminService', () => {
  let service: SpeedReadingAdminService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SpeedReadingAdminService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(SpeedReadingAdminService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads capabilities through the Gateway speed-reading route', () => {
    const response = {
      mode: 'Standalone',
      coachingIntegrationEnabled: false,
      notificationIntegrationEnabled: false,
      subscriptionIntegrationEnabled: false
    };

    service.getCapabilities().subscribe(value => expect(value).toEqual(response));

    const request = http.expectOne('/api/speed-reading/capabilities');
    expect(request.request.method).toBe('GET');
    request.flush(response);
  });

  it('loads exercise types from the legacy content catalog', () => {
    const response = {
      items: [{
        id: 'type-1',
        name: 'schulte',
        displayName: 'Schulte Tablosu',
        description: 'Odaklanma',
        iconName: 'grid',
        colorCode: '#2563eb',
        sortOrder: 1,
        isActive: true,
        engineType: 'SchulteTable',
        categoryId: null
      }],
      pageNumber: 1,
      pageSize: 20,
      totalCount: 1
    };

    service.getExerciseTypes().subscribe(value => expect(value).toEqual(response));

    const request = http.expectOne('/api/speed-reading/exercise-types?pageNumber=1&pageSize=20');
    expect(request.request.method).toBe('GET');
    request.flush(response);
  });
});
