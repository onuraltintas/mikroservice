import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { TeacherStudentReportComponent } from './teacher-student-report.component';

describe('TeacherStudentReportComponent', () => {
  let fixture: ComponentFixture<TeacherStudentReportComponent>;
  let service: Record<string, ReturnType<typeof vi.fn>>;

  beforeEach(() => {
    service = {
      getTeacherStudents: vi.fn(() => of({ items: [{ userId: 'student-1', fullName: 'Ada Gerçek' }], pageNumber: 1, pageSize: 1, totalCount: 1, totalPages: 1 })),
      getStudentProgress: vi.fn(() => of({ studentId: 'student-1', totalAssignments: 2, submittedAssignments: 1, totalExams: 1, totalSessions: 1, totalGoals: 1 })),
      getStudentAssignments: vi.fn(() => of({ items: [{ id: 'assignment-1', title: 'Matematik', status: 'Submitted', dueDate: '2030-01-01' }], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })),
      getStudentExamResults: vi.fn(() => of({ items: [{ examId: 'exam-1', examTitle: 'Deneme', score: 80, maxScore: 100, examDate: '2030-01-01' }], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })),
      getStudentGoals: vi.fn(() => of({ items: [{ id: 'goal-1', title: 'Hedef', progress: 50 }], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })),
      getStudentSessions: vi.fn(() => of({ items: [{ id: 'session-1', subject: 'Görüşme' }], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }))
    };
    TestBed.configureTestingModule({
      imports: [TeacherStudentReportComponent],
      providers: [
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'student-1' }), queryParamMap: convertToParamMap({ name: 'Sahte İsim' }) } } }
      ]
    });
    fixture = TestBed.createComponent(TeacherStudentReportComponent);
  });

  it('shows the assigned student report sections', () => {
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Ödevler');
    expect(text).toContain('Matematik');
    expect(text).toContain('Deneme');
    expect(text).toContain('Hedef');
    expect(text).toContain('Görüşme');
    expect(service['getStudentProgress']).toHaveBeenCalledWith('student-1');
  });

  it('uses the verified roster name instead of the URL display name', () => {
    fixture.detectChanges();
    expect(service['getTeacherStudents']).toHaveBeenCalledWith(1, 1, 'student-1');
    expect(fixture.nativeElement.textContent).toContain('Ada Gerçek');
    expect(fixture.nativeElement.textContent).not.toContain('Sahte İsim');
  });

  it('does not expose partial data when access is denied', () => {
    service['getStudentProgress'].mockReturnValue(throwError(() => ({ status: 403 })));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Rapor yüklenemedi');
    expect(fixture.nativeElement.textContent).not.toContain('Matematik');
  });

  it('loads older assignment records without losing the first page', () => {
    service['getStudentAssignments']
      .mockReturnValueOnce(of({ items: [{ id: 'assignment-1', title: 'İlk ödev' }], pageNumber: 1, pageSize: 25, totalCount: 26, totalPages: 2 }))
      .mockReturnValueOnce(of({ items: [{ id: 'assignment-2', title: 'Eski ödev' }], pageNumber: 2, pageSize: 25, totalCount: 26, totalPages: 2 }));
    fixture.detectChanges();
    fixture.componentInstance.loadMoreAssignments();
    fixture.detectChanges();
    expect(service['getStudentAssignments']).toHaveBeenLastCalledWith('student-1', 2, 25);
    expect(fixture.nativeElement.textContent).toContain('İlk ödev');
    expect(fixture.nativeElement.textContent).toContain('Eski ödev');
  });
});
