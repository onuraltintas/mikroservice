import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CoachingTeacherStudentsComponent } from './coaching-teacher-students.component';

describe('CoachingTeacherStudentsComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoachingTeacherStudentsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows the teacher-scoped roster with grade and institution context', () => {
    const fixture = TestBed.createComponent(CoachingTeacherStudentsComponent);
    fixture.detectChanges();
    http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25').flush({
      items: [{
        userId: 'student-1',
        firstName: 'Ayşe',
        lastName: 'Yılmaz',
        fullName: 'Ayşe Yılmaz',
        gradeLevel: 8,
        institutionName: 'Atatürk Ortaokulu',
        subject: 'Matematik',
        assignmentStartDate: '2026-09-01T00:00:00Z'
      }],
      pageNumber: 1,
      pageSize: 25,
      totalCount: 1,
      totalPages: 1
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Atatürk Ortaokulu');
    expect(fixture.nativeElement.textContent).toContain('8. sınıf');
  });

  it('retries a failed roster request from the visible error state', () => {
    const fixture = TestBed.createComponent(CoachingTeacherStudentsComponent);
    fixture.detectChanges();
    http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25').flush({}, {
      status: 500,
      statusText: 'Server Error'
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci listesi yüklenemedi');
    const retryButton = fixture.nativeElement.querySelector('button[data-testid="retry-roster"]') as HTMLButtonElement;
    retryButton.click();
    fixture.detectChanges();
    http.expectOne('/api/teachers/me/students?pageNumber=1&pageSize=25').flush({
      items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 1
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci bulunamadı');
  });
});
