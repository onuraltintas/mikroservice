import { ToasterService } from '../../../core/services/toaster.service';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { StudentAssignmentsComponent } from './student-assignments.component';

describe('StudentAssignmentsComponent cancelled assignments', () => {
  it('labels cancelled work as cancelled even when its due date has passed', () => {
    TestBed.configureTestingModule({
      imports: [StudentAssignmentsComponent],
      providers: [
        { provide: AuthService, useValue: { userProfile: signal<UserProfile | null>({
          id: 'student-1', email: 'student@example.test', firstName: 'Ada', lastName: 'Öğrenci',
          username: 'student@example.test', roles: ['Student'], role: 'Student', permissions: []
        }) } },
        { provide: CoachingPortalService, useValue: { getStudentAssignments: () => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 }) } },
        { provide: ActivatedRoute, useValue: {} }
      ]
    });
    const component = TestBed.createComponent(StudentAssignmentsComponent).componentInstance;
    component.assignments.set([
      { id: 'pending', title: 'Bekleyen', dueDate: '', status: 'Assigned', isOverdue: false },
      { id: 'overdue', title: 'Geciken', dueDate: '', status: 'Assigned', isOverdue: true },
      { id: 'cancelled', title: 'İptal', dueDate: '', status: 'Assigned', assignmentStatus: 'Cancelled', isOverdue: true }
    ]);

    expect(component.assignmentStatusLabel(component.assignments()[2])).toBe('İptal edildi');
  });
});

// T5 (31.10.2026): three filters, and cards that show subject, title, one label and the score.
describe('StudentAssignmentsComponent cards', () => {
  function render(items: any[]) {
    const getStudentAssignments = vi.fn(() => of({ items, pageNumber: 1, pageSize: 25, totalCount: items.length, totalPages: 1 }));
    TestBed.configureTestingModule({
      imports: [StudentAssignmentsComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { userProfile: signal<UserProfile | null>({
          id: 'student-1', email: 'student@example.test', firstName: 'Ada', lastName: 'Öğrenci',
          username: 'student@example.test', roles: ['Student'], role: 'Student', permissions: []
        }) } },
        { provide: CoachingPortalService, useValue: { getStudentAssignments } }
      ]
    });
    const fixture = TestBed.createComponent(StudentAssignmentsComponent);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, getStudentAssignments };
  }

  it('shows Yapılacak, Teslim edilen and Tümü and loads open work first', () => {
    const { el, getStudentAssignments } = render([]);
    expect([...el.querySelectorAll('[role=tab]')].map(tab => tab.textContent?.trim())).toEqual(['Yapılacak', 'Teslim edilen', 'Tümü']);
    expect(getStudentAssignments).toHaveBeenCalledWith('student-1', 1, 25, 'open');
  });

  it('switches to submitted work with the Teslim edilen filter', () => {
    const { fixture, el, getStudentAssignments } = render([]);
    (el.querySelectorAll('[role=tab]')[1] as HTMLButtonElement).click(); fixture.detectChanges();
    expect(getStudentAssignments).toHaveBeenLastCalledWith('student-1', 1, 25, 'submitted');
  });

  it('shows subject, title, label, score and a feedback mark on each card', () => {
    const { el } = render([
      { id: 'late', title: 'Paragraf testi', subject: 'Türkçe', dueDate: '2020-01-01T10:00:00Z', status: 'Assigned', isOverdue: true },
      { id: 'graded', title: 'Üslü sayılar', subject: 'Matematik', dueDate: '2020-01-01T10:00:00Z', status: 'Graded', score: 85, maxScore: 100,
        teacherFeedback: 'Güzel', isOverdue: false }
    ]);
    const cards = [...el.querySelectorAll('a.assignment-card')] as HTMLAnchorElement[];
    expect(cards.map(card => card.getAttribute('href'))).toEqual(['/coaching-portal/assignments/late', '/coaching-portal/assignments/graded']);
    expect(cards[0].querySelector('.subject')?.textContent?.trim()).toBe('Türkçe');
    expect(cards[0].querySelector('h2')?.textContent?.trim()).toBe('Paragraf testi');
    expect(cards[0].querySelector('cs-status-chip')?.textContent?.trim()).toBe('Gecikti');
    expect(cards[1].querySelector('.score')?.textContent?.trim()).toBe('85/100');
    expect(cards[1].querySelector('.feedback-mark')?.getAttribute('aria-label')).toBe('Öğretmenin yorum yazdı');
    expect(cards[0].querySelector('.feedback-mark')).toBeNull();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
