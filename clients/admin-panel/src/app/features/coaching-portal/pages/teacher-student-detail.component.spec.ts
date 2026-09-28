import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { vi } from 'vitest';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { TeacherStudentDetailComponent } from './teacher-student-detail.component';

describe('TeacherStudentDetailComponent', () => {
  let fixture: ComponentFixture<TeacherStudentDetailComponent>;
  let service: {
    getTeacherStudents: ReturnType<typeof vi.fn>;
    getStudentProgress: ReturnType<typeof vi.fn>;
    getTeacherStudentHistory: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    service = {
      getTeacherStudents: vi.fn(() => of({
        items: [{
          userId: 'student-1', firstName: 'Ada', lastName: 'Yılmaz', fullName: 'Ada Yılmaz',
          assignmentStartDate: '2030-01-01T00:00:00Z'
        }], pageNumber: 1, pageSize: 1, totalCount: 1, totalPages: 1
      })),
      getStudentProgress: vi.fn(() => of({
        studentId: 'student-1', totalAssignments: 6, submittedAssignments: 4, gradedAssignments: 3,
        averageAssignmentPercentage: 82, totalExams: 2, averageExamPercentage: 77,
        totalGoals: 3, completedGoals: 1, averageGoalProgress: 65, totalSessions: 4,
        upcomingSessions: 1, attendedSessions: 2, attendancePercentage: 80
      })),
      getTeacherStudentHistory: vi.fn(() => of({
        items: [{ id: 'event-1', type: 'Exams', title: 'Matematik denemesi',
          eventDate: '2030-01-01T00:00:00Z', status: 'Completed', score: 77, maxScore: 100 }],
        pageNumber: 1, pageSize: 10, totalCount: 1, totalPages: 1
      }))
    };
    TestBed.configureTestingModule({
      imports: [TeacherStudentDetailComponent],
      providers: [
        provideRouter([]),
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'student-1' } } } }
      ]
    });
    fixture = TestBed.createComponent(TeacherStudentDetailComponent);
    fixture.detectChanges();
  });

  it('loads the student identity, progress summary and paged history', () => {
    expect(service.getTeacherStudents).toHaveBeenCalledWith(1, 1, undefined, ['student-1']);
    expect(service.getStudentProgress).toHaveBeenCalledWith('student-1');
    expect(service.getTeacherStudentHistory).toHaveBeenCalledWith('student-1', 'Assignments', 1, 10);
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('6');
  });

  it('does not request progress when the student is no longer on the current roster', () => {
    service.getTeacherStudents.mockReturnValue(of({
      items: [], pageNumber: 1, pageSize: 1, totalCount: 0, totalPages: 0
    }));
    service.getTeacherStudents.mockClear();
    service.getStudentProgress.mockClear();
    service.getTeacherStudentHistory.mockClear();
    fixture.destroy();
    fixture = TestBed.createComponent(TeacherStudentDetailComponent);
    fixture.detectChanges();

    expect(service.getStudentProgress).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('aktif öğrenci listenizde değil');
  });

  it('ignores a stale history response after switching categories', () => {
    const pending = new Subject<any>();
    service.getTeacherStudentHistory.mockReturnValueOnce(pending.asObservable())
      .mockReturnValueOnce(of({ items: [{ id: 'new', type: 'Exams', title: 'Yeni sınav', eventDate: '2030-01-01', status: 'Completed' }], pageNumber: 1, pageSize: 10, totalCount: 1, totalPages: 1 }));
    fixture.destroy();
    fixture = TestBed.createComponent(TeacherStudentDetailComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.selectHistoryType('Exams');
    pending.next({ items: [{ id: 'old', type: 'Assignments', title: 'Eski ödev', eventDate: '2030-01-01', status: 'Completed' }], pageNumber: 1, pageSize: 10, totalCount: 1, totalPages: 1 });
    expect(component.history().map(item => item.id)).toEqual(['new']);
  });

  it('applies history filters at the server and starts from the first page', () => {
    const component = fixture.componentInstance;
    component.historySearch = 'Math';
    component.historyStatus = 'Graded';
    component.historyFromDate = '2030-01-01';
    component.historyToDate = '2030-01-31';
    component.applyHistoryFilters();
    expect(service.getTeacherStudentHistory).toHaveBeenLastCalledWith('student-1', 'Assignments', 1, 10, {
      fromDate: '2030-01-01T00:00:00.000Z', toDate: '2030-01-31T23:59:59.999Z', status: 'Graded', search: 'Math'
    });
  });
});
