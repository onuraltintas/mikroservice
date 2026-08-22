import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { AuthService, hasRole } from '../../../core/auth/auth.service';
import {
  ChildSummary,
  CoachingPortalService,
  CoachingSession,
  ExamResult,
  Goal,
  StudentAssignment,
  StudentProgressSummary
} from '../../../core/services/coaching-portal.service';

type ParentAssignmentFilter = 'all' | 'pending' | 'submitted' | 'overdue';
type ParentCollection = 'assignments' | 'goals' | 'sessions';

@Component({
  selector: 'app-parent-children',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './parent-children.component.html',
  styleUrl: './parent-children.component.scss'
})
export class ParentChildrenComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly children = signal<ChildSummary[]>([]);
  readonly selectedChild = signal<ChildSummary | null>(null);
  readonly assignments = signal<StudentAssignment[]>([]);
  readonly goals = signal<Goal[]>([]);
  readonly examResults = signal<ExamResult[]>([]);
  readonly sessions = signal<CoachingSession[]>([]);
  readonly progressSummary = signal<StudentProgressSummary | null>(null);
  readonly assignmentFilter = signal<ParentAssignmentFilter>('all');
  readonly assignmentPageNumber = signal(1);
  readonly assignmentTotalPages = signal(1);
  readonly goalPageNumber = signal(1);
  readonly goalTotalPages = signal(1);
  readonly sessionPageNumber = signal(1);
  readonly sessionTotalPages = signal(1);
  readonly loadingMore = signal<ParentCollection | null>(null);
  readonly isLoading = signal(true);
  readonly isChildLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit() {
    if (!hasRole(this.authService.userProfile(), 'Parent')) {
      this.isLoading.set(false);
      this.errorMessage.set('Bu alan yalnızca veli hesabı ile kullanılabilir.');
      return;
    }

    this.coachingService.getMyChildren().subscribe({
      next: children => {
        this.children.set(children);
        if (children.length > 0) this.selectChild(children[0]);
      },
      error: () => {
        this.errorMessage.set('Çocuk profilleri yüklenemedi.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  selectChild(child: ChildSummary) {
    this.selectedChild.set(child);
    this.isChildLoading.set(true);
    this.errorMessage.set(null);
    this.progressSummary.set(null);
    this.assignmentPageNumber.set(1);
    this.goalPageNumber.set(1);
    this.sessionPageNumber.set(1);
    this.assignmentTotalPages.set(1);
    this.goalTotalPages.set(1);
    this.sessionTotalPages.set(1);

    forkJoin({
      assignments: this.coachingService.getStudentAssignments(child.userId, 1, 25),
      goals: this.coachingService.getStudentGoals(child.userId, 1, 25),
      examResults: this.coachingService.getStudentExamResults(child.userId, 1, 25),
      sessions: this.coachingService.getStudentSessions(child.userId, 1, 25),
      progress: this.coachingService.getStudentProgress(child.userId).pipe(catchError(() => of(null)))
    }).subscribe({
      next: result => {
        this.assignments.set(result.assignments.items);
        this.goals.set(result.goals.items);
        this.examResults.set(result.examResults.items);
        this.sessions.set(result.sessions.items);
        this.progressSummary.set(result.progress);
        this.assignmentTotalPages.set(result.assignments.totalPages ?? Math.max(1, Math.ceil(result.assignments.totalCount / result.assignments.pageSize)));
        this.goalTotalPages.set(result.goals.totalPages ?? Math.max(1, Math.ceil(result.goals.totalCount / result.goals.pageSize)));
        this.sessionTotalPages.set(result.sessions.totalPages ?? Math.max(1, Math.ceil(result.sessions.totalCount / result.sessions.pageSize)));
      },
      error: () => {
        this.errorMessage.set('Çocuğun koçluk verileri yüklenemedi.');
        this.isChildLoading.set(false);
      },
      complete: () => this.isChildLoading.set(false)
    });
  }

  completedGoals() {
    return this.progressSummary()?.completedGoals ?? this.goals().filter(goal => goal.isCompleted).length;
  }

  submittedAssignments() {
    return this.progressSummary()?.submittedAssignments ?? this.assignments().filter(assignment => !!assignment.submittedAt).length;
  }

  loadMoreAssignments() {
    const child = this.selectedChild();
    if (!child || this.assignmentPageNumber() >= this.assignmentTotalPages() || this.loadingMore()) return;
    const nextPage = this.assignmentPageNumber() + 1;
    this.loadingMore.set('assignments');
    this.coachingService.getStudentAssignments(child.userId, nextPage, 25).pipe(
      finalize(() => this.loadingMore.set(null))
    ).subscribe({
      next: page => {
        this.assignments.update(items => [...items, ...page.items]);
        this.assignmentPageNumber.set(nextPage);
      },
      error: () => this.errorMessage.set('Ödevlerin devamı yüklenemedi.')
    });
  }

  loadMoreGoals() {
    const child = this.selectedChild();
    if (!child || this.goalPageNumber() >= this.goalTotalPages() || this.loadingMore()) return;
    const nextPage = this.goalPageNumber() + 1;
    this.loadingMore.set('goals');
    this.coachingService.getStudentGoals(child.userId, nextPage, 25).pipe(
      finalize(() => this.loadingMore.set(null))
    ).subscribe({
      next: page => {
        this.goals.update(items => [...items, ...page.items]);
        this.goalPageNumber.set(nextPage);
      },
      error: () => this.errorMessage.set('Hedeflerin devamı yüklenemedi.')
    });
  }

  loadMoreSessions() {
    const child = this.selectedChild();
    if (!child || this.sessionPageNumber() >= this.sessionTotalPages() || this.loadingMore()) return;
    const nextPage = this.sessionPageNumber() + 1;
    this.loadingMore.set('sessions');
    this.coachingService.getStudentSessions(child.userId, nextPage, 25).pipe(
      finalize(() => this.loadingMore.set(null))
    ).subscribe({
      next: page => {
        this.sessions.update(items => [...items, ...page.items]);
        this.sessionPageNumber.set(nextPage);
      },
      error: () => this.errorMessage.set('Seansların devamı yüklenemedi.')
    });
  }

  setAssignmentFilter(filter: ParentAssignmentFilter) {
    this.assignmentFilter.set(filter);
  }

  visibleAssignments(): StudentAssignment[] {
    const filter = this.assignmentFilter();
    if (filter === 'submitted') return this.assignments().filter(assignment => !!assignment.submittedAt);
    if (filter === 'overdue') return this.assignments().filter(assignment => assignment.isOverdue && !assignment.submittedAt);
    if (filter === 'pending') return this.assignments().filter(assignment => !assignment.submittedAt && !assignment.isOverdue);
    return this.assignments();
  }
}
