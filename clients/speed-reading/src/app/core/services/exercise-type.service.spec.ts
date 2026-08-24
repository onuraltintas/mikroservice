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
});
