import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingAssignmentManagementComponent } from './speed-reading-assignment-management.component';

describe('SpeedReadingAssignmentManagementComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SpeedReadingAssignmentManagementComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('creates a teacher assignment with only the selected Speed Reading students', () => {
    const fixture = TestBed.createComponent(SpeedReadingAssignmentManagementComponent);
    fixture.detectChanges();
    http.expectOne(candidate => candidate.url === '/api/speed-reading/assignments/teacher-assignments').flush({
      items: [], totalCount: 0, pageNumber: 1, pageSize: 25,
    });
    http.expectOne(candidate => candidate.url === '/api/speed-reading/exercise-types').flush({
      items: [{ id: 'type-1', displayName: 'Hızlı Okuma' }], totalCount: 1, pageNumber: 1, pageSize: 100,
    });
    http.expectOne(candidate => candidate.url === '/api/speed-reading/teachers/me/students').flush({
      items: [{ id: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.test' }],
      totalCount: 1, pageNumber: 1, pageSize: 100,
    });

    fixture.componentInstance.assignmentTitle = 'Haftalık okuma';
    fixture.componentInstance.selectedExerciseTypeId = 'type-1';
    fixture.componentInstance.loadExercises();
    http.expectOne(candidate => candidate.url === '/api/speed-reading/exercises' && candidate.params.get('exerciseTypeId') === 'type-1').flush({
      items: [{ id: 'exercise-1', title: 'Metin çalışması', exerciseTypeId: 'type-1' }], totalCount: 1, pageNumber: 1, pageSize: 100,
    });
    fixture.componentInstance.selectedExerciseId = 'exercise-1';
    fixture.componentInstance.toggleStudent('student-1', true);
    fixture.componentInstance.submitAssignment();

    const request = http.expectOne('/api/speed-reading/assignments');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toMatchObject({
      exerciseId: 'exercise-1',
      readingTextId: null,
      studentIds: ['student-1'],
      title: 'Haftalık okuma',
    });
    request.flush('assignment-1');
    http.expectOne(candidate => candidate.url === '/api/speed-reading/assignments/teacher-assignments').flush({
      items: [], totalCount: 0, pageNumber: 1, pageSize: 25,
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ödev başarıyla oluşturuldu');
  });

  it('keeps institution assignment creation within its institution and selected teacher', () => {
    const fixture = TestBed.createComponent(SpeedReadingAssignmentManagementComponent);
    fixture.componentRef.setInput('institutionId', 'institution-1');
    fixture.detectChanges();
    http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/assignments').flush({
      items: [], totalCount: 0, pageNumber: 1, pageSize: 25,
    });
    http.expectOne(candidate => candidate.url === '/api/speed-reading/exercise-types').flush({
      items: [{ id: 'type-1', displayName: 'Hızlı Okuma' }], totalCount: 1, pageNumber: 1, pageSize: 100,
    });
    http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/members').flush({
      items: [{ userId: 'teacher-1', firstName: 'Emre', lastName: 'Yılmaz' }],
      totalCount: 1, pageNumber: 1, pageSize: 100,
    });

    fixture.componentInstance.selectedTeacherId = 'teacher-1';
    fixture.componentInstance.onTeacherChange();
    http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/teachers/teacher-1/students').flush({
      items: [{ studentUserId: 'student-1', firstName: 'Deniz', lastName: 'Kaya' }],
      totalCount: 1, pageNumber: 1, pageSize: 100,
    });
    fixture.componentInstance.assignmentTitle = 'Kurum ödevi';
    fixture.componentInstance.selectedExerciseTypeId = 'type-1';
    fixture.componentInstance.loadExercises();
    http.expectOne(candidate => candidate.url === '/api/speed-reading/exercises').flush({
      items: [{ id: 'exercise-1', title: 'Metin çalışması', exerciseTypeId: 'type-1' }], totalCount: 1, pageNumber: 1, pageSize: 100,
    });
    fixture.componentInstance.selectedExerciseId = 'exercise-1';
    fixture.componentInstance.toggleStudent('student-1', true);
    fixture.componentInstance.submitAssignment();

    const request = http.expectOne('/api/speed-reading/institutions/institution-1/assignments');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toMatchObject({
      teacherId: 'teacher-1', exerciseId: 'exercise-1', studentIds: ['student-1'],
    });
    request.flush('assignment-1');
    http.expectOne(candidate => candidate.url === '/api/speed-reading/institutions/institution-1/assignments').flush({
      items: [], totalCount: 0, pageNumber: 1, pageSize: 25,
    });
  });
});
