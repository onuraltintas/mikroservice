import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import {
  ChildSummary,
  CoachingPortalService,
  ExamResult,
  Goal,
  StudentAssignment
} from '../../../core/services/coaching-portal.service';

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
  readonly isLoading = signal(true);
  readonly isChildLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit() {
    if (this.authService.userProfile()?.role !== 'Parent') {
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

    forkJoin({
      assignments: this.coachingService.getStudentAssignments(child.userId, 1, 25),
      goals: this.coachingService.getStudentGoals(child.userId, 1, 25),
      examResults: this.coachingService.getStudentExamResults(child.userId, 1, 25)
    }).subscribe({
      next: result => {
        this.assignments.set(result.assignments.items);
        this.goals.set(result.goals.items);
        this.examResults.set(result.examResults.items);
      },
      error: () => {
        this.errorMessage.set('Çocuğun koçluk verileri yüklenemedi.');
        this.isChildLoading.set(false);
      },
      complete: () => this.isChildLoading.set(false)
    });
  }

  completedGoals() {
    return this.goals().filter(goal => goal.isCompleted).length;
  }

  submittedAssignments() {
    return this.assignments().filter(assignment => !!assignment.submittedAt).length;
  }
}
