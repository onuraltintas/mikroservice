import { coachingFeedback } from '../coaching-feedback';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, StudentAssignment, StudentAssignmentFilter } from '../../../core/services/coaching-portal.service';
import { CsEmptyStateComponent, CsSegmentedTabsComponent, CsStatusChipComponent } from '../ui';
import { STUDENT_ASSIGNMENT_FILTERS, assignmentCardState, assignmentDateTime, assignmentScore } from './student-assignment-view';

@Component({
  selector: 'app-student-assignments',
  standalone: true,
  imports: [RouterLink, CsEmptyStateComponent, CsSegmentedTabsComponent, CsStatusChipComponent],
  templateUrl: './student-assignments.component.html',
  styleUrl: './student-assignments.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentAssignmentsComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly assignments = signal<StudentAssignment[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  // Roadmap O1 / T5: the list opens on work still to do (late first); every filter runs on the server.
  readonly filter = signal<StudentAssignmentFilter>('open');
  readonly filters = STUDENT_ASSIGNMENT_FILTERS;
  readonly empty = computed(() => this.filter() === 'submitted'
    ? { title: 'Henüz teslim ettiğin ödev yok', text: 'Teslim ettiğin ödevler ve puanların burada toplanır.' }
    : this.filter() === 'all'
      ? { title: 'Henüz ödevin yok', text: 'Öğretmenin ödev verdiğinde burada görürsün.' }
      : { title: 'Yapılacak ödevin yok', text: 'Yeni ödev gelince burada görürsün.' });
  readonly cardState = (assignment: StudentAssignment) => assignmentCardState(assignment);
  readonly score = assignmentScore;

  ngOnInit() {
    this.load();
  }

  load() {
    const studentId = this.authService.userProfile()?.id;
    if (!studentId) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğrenci profili bulunamadı.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.coachingService.getStudentAssignments(studentId, this.pageNumber(), 25, this.filter()).subscribe({
      next: page => {
        this.assignments.set(page.items);
        this.totalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => {
        this.errorMessage.set('Ödevler yüklenemedi. Bağlantını kontrol edip yeniden dene.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  assignmentStatusLabel(assignment: StudentAssignment) {
    return assignmentCardState(assignment).label;
  }

  /** "Son tarih 2 Kasım, 20:00" — or when it was handed in. */
  dueLine(assignment: StudentAssignment) {
    if (assignment.submittedAt) return `Teslim ettin: ${assignmentDateTime(assignment.submittedAt)}`;
    return `Son tarih ${assignmentDateTime(assignment.dueDate)}`;
  }

  setFilter(value: StudentAssignmentFilter) {
    this.filter.set(value);
    this.pageNumber.set(1);
    this.load();
  }

  previousPage() {
    if (this.pageNumber() <= 1) return;
    this.pageNumber.update(page => page - 1);
    this.load();
  }

  nextPage() {
    if (this.pageNumber() >= this.totalPages()) return;
    this.pageNumber.update(page => page + 1);
    this.load();
  }

  private readonly feedback = coachingFeedback(undefined, this.errorMessage);
}
