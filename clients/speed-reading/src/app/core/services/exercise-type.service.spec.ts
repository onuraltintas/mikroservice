import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ExerciseTypeService } from './exercise-type.service';

describe('ExerciseTypeService', () => {
  let service: ExerciseTypeService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ExerciseTypeService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ExerciseTypeService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads active exercise types from the dedicated service', () => {
    service.getActiveExerciseTypes().subscribe();

    const request = http.expectOne('/api/speed-reading/exercise-types?pageNumber=1&pageSize=100&isActive=true');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0 });
  });

  it('reads categories and writes exercise type commands through the dedicated service', () => {
    service.getCategories().subscribe();
    const categoriesRequest = http.expectOne('/api/speed-reading/exercise-types/categories');
    expect(categoriesRequest.request.method).toBe('GET');
    categoriesRequest.flush([]);

    service.createExerciseType({
      name: 'EyeTracking',
      displayName: 'Göz Takibi',
      description: 'Açıklama',
      iconName: 'visibility',
      colorCode: '#123456',
      sortOrder: 1,
      isActive: true,
      engineType: '   ',
      categoryId: 'category-1'
    }, 'type-create-key').subscribe();

    const createRequest = http.expectOne('/api/speed-reading/exercise-types');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.headers.get('Idempotency-Key')).toBe('type-create-key');
    expect(createRequest.request.body.engineType).toBe('EyeTracking');
    createRequest.flush({ id: 'type-1' });

    service.updateExerciseType('type-1', {
      name: 'EyeTracking',
      displayName: 'Göz Takibi 2',
      description: 'Açıklama',
      iconName: 'visibility',
      colorCode: '#123456',
      sortOrder: 1,
      isActive: false,
      engineType: 'eye_tracking',
      categoryId: undefined
    }, 'type-update-key').subscribe();

    const updateRequest = http.expectOne('/api/speed-reading/exercise-types/type-1');
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.headers.get('Idempotency-Key')).toBe('type-update-key');
    expect(updateRequest.request.body.engineType).toBe('eye_tracking');
    updateRequest.flush({ id: 'type-1' });

    service.deleteExerciseType('type-1', 'type-delete-key').subscribe();
    const deleteRequest = http.expectOne('/api/speed-reading/exercise-types/type-1');
    expect(deleteRequest.request.method).toBe('DELETE');
    expect(deleteRequest.request.headers.get('Idempotency-Key')).toBe('type-delete-key');
    deleteRequest.flush(null);
  });
});
