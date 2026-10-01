import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';
import { SpeedReadingAssignmentsService } from './speed-reading-assignments.service';

describe('SpeedReadingAssignmentsService', () => {
  let http: HttpTestingController;
  let service: SpeedReadingAssignmentsService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SpeedReadingAssignmentsService, provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(SpeedReadingAssignmentsService);
  });

  afterEach(() => http.verify());

  it('loads teacher assignments with bounded pagination and normalized filters', async () => {
    const response = firstValueFrom(service.getTeacherAssignments(3, 500, '  okuma  ', false));
    const request = http.expectOne(candidate =>
      candidate.url === '/api/speed-reading/assignments/teacher-assignments' &&
      candidate.params.get('pageNumber') === '3' &&
      candidate.params.get('pageSize') === '100' &&
      candidate.params.get('searchTerm') === 'okuma' &&
      candidate.params.get('isActive') === 'false');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], totalCount: 0, pageNumber: 3, pageSize: 100 });
    await expect(response).resolves.toMatchObject({ totalCount: 0, pageNumber: 3 });
  });

  it('keeps institution assignment reads and creates inside the selected Speed Reading institution', async () => {
    const page = firstValueFrom(service.getInstitutionAssignments('institution-1', 1, 25, '', true, 'type-1', 'teacher-1'));
    const request = http.expectOne(candidate =>
      candidate.url === '/api/speed-reading/institutions/institution-1/assignments' &&
      candidate.params.get('teacherId') === 'teacher-1' &&
      candidate.params.get('exerciseTypeId') === 'type-1' &&
      candidate.params.get('isActive') === 'true');
    request.flush({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    await page;

    const created = firstValueFrom(service.createInstitutionAssignment('institution-1', {
      teacherId: 'teacher-1', exerciseId: 'exercise-1', readingTextId: null,
      studentIds: ['student-1'], title: 'Haftalık okuma', description: '', dueDate: '2026-10-01T00:00:00.000Z',
    }));
    const createRequest = http.expectOne('/api/speed-reading/institutions/institution-1/assignments');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toMatchObject({ teacherId: 'teacher-1', studentIds: ['student-1'] });
    createRequest.flush('assignment-1');
    await expect(created).resolves.toBe('assignment-1');
  });

  it('uses teacher-scoped student membership for the assignment picker', async () => {
    const response = firstValueFrom(service.getInstitutionTeacherStudents('institution-1', 'teacher-1', 2, 10));
    const request = http.expectOne(candidate =>
      candidate.url === '/api/speed-reading/institutions/institution-1/teachers/teacher-1/students' &&
      candidate.params.get('pageNumber') === '2' &&
      candidate.params.get('pageSize') === '10');
    request.flush({ items: [], totalCount: 0, pageNumber: 2, pageSize: 10 });
    await response;
  });

  it('loads only active exercise types and the selected type exercise catalog', async () => {
    const response = firstValueFrom(service.getExerciseTypes());
    const typesRequest = http.expectOne(candidate =>
      candidate.url === '/api/speed-reading/exercise-types' &&
      candidate.params.get('isActive') === 'true' && candidate.params.get('pageSize') === '100');
    typesRequest.flush({ items: [{ id: 'type-1', displayName: 'Hızlı Okuma' }], totalCount: 1, pageNumber: 1, pageSize: 100 });
    await expect(response).resolves.toHaveLength(1);

    const exercises = firstValueFrom(service.getExercises('type-1'));
    const exercisesRequest = http.expectOne(candidate =>
      candidate.url === '/api/speed-reading/exercises' &&
      candidate.params.get('exerciseTypeId') === 'type-1' && candidate.params.get('pageSize') === '100');
    exercisesRequest.flush({ items: [{ id: 'exercise-1', title: 'Metin çalışması' }], totalCount: 1, pageNumber: 1, pageSize: 100 });
    await expect(exercises).resolves.toHaveLength(1);
  });

  it('loads assignment details and mutates teacher assignment membership only through teacher-scoped endpoints', async () => {
    const details = firstValueFrom(service.getAssignmentDetails('assignment-1'));
    const detailsRequest = http.expectOne('/api/speed-reading/assignments/assignment-1/details');
    detailsRequest.flush({ id: 'assignment-1', students: [] });
    await expect(details).resolves.toMatchObject({ id: 'assignment-1' });

    const add = firstValueFrom(service.addStudentToTeacherAssignment('assignment-1', 'student-1'));
    const addRequest = http.expectOne('/api/speed-reading/assignments/assignment-1/students');
    expect(addRequest.request.method).toBe('POST');
    expect(addRequest.request.body).toEqual({ assignmentId: 'assignment-1', studentId: 'student-1' });
    addRequest.flush(null);
    await add;

    const remove = firstValueFrom(service.removeStudentFromTeacherAssignment('assignment-1', 'student-1'));
    const removeRequest = http.expectOne('/api/speed-reading/assignments/assignment-1/students/student-1');
    expect(removeRequest.request.method).toBe('DELETE');
    removeRequest.flush(null);
    await remove;
  });
});
