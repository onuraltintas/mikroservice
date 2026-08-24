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

  it('creates an exercise type with an idempotency key', () => {
    service.createExerciseType({
      name: 'schulte',
      displayName: 'Schulte Tablosu',
      description: 'Odaklanma',
      iconName: 'grid',
      colorCode: '#2563eb',
      sortOrder: 1,
      isActive: true,
      engineType: 'SchulteTable',
      categoryId: null
    }, 'admin-type-key-123456').subscribe();

    const request = http.expectOne('/api/speed-reading/exercise-types');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('admin-type-key-123456');
    request.flush({ id: 'type-1' });
  });

  it('loads exercises with paging and writes through the dedicated route', () => {
    service.getExercises(2, 10).subscribe();
    const listRequest = http.expectOne('/api/speed-reading/exercises?pageNumber=2&pageSize=10');
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0 });

    service.createExercise({
      title: 'Odak egzersizi',
      description: 'Kısa açıklama',
      difficultyLevel: 2,
      exerciseTypeId: 'type-1',
      configurationJson: '{}',
      targetAgeGroupConfigurationId: null
    }, 'exercise-key-123456').subscribe();
    const createRequest = http.expectOne('/api/speed-reading/exercises');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('exercise-key-123456');
    createRequest.flush({ id: 'exercise-1' });
  });
});
