import { Component, OnInit, inject, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { AssignmentService, AssignmentDto } from '../../../core/services/assignment.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { debounceTime, distinctUntilChanged } from 'rxjs';

@Component({
    selector: 'app-student-assignments-page',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule
    ],
    templateUrl: './student-assignments-page.component.html',
    styleUrls: ['./student-assignments-page.component.scss']
})
export class StudentAssignmentsPageComponent implements OnInit {
    private assignmentService = inject(AssignmentService);
    private router = inject(Router);
    private toaster = inject(ToasterService);

    assignments: AssignmentDto[] = [];
    filteredAssignments: AssignmentDto[] = [];
    loading = true;

    searchControl = new FormControl('');
    selectedTab = 0;

    stats = {
        pending: 0,
        completed: 0,
        averageScore: 0
    };

    ngOnInit() {
        this.loadAssignments();

        // Setup filter listeners
        this.searchControl.valueChanges.pipe(
            debounceTime(300),
            distinctUntilChanged()
        ).subscribe(() => this.filterAssignments());
    }

    loadAssignments() {
        this.loading = true;
        this.assignmentService.getMyAssignments().subscribe({
            next: (res) => {
                this.assignments = res;
                this.calculateStats();
                this.filterAssignments();

                // Auto-switch to history tab if no pending assignments
                if (this.stats.pending === 0 && this.stats.completed > 0) {
                    this.selectedTab = 1;
                }

                this.loading = false;
            },
            error: (err) => {
                console.error('Error loading assignments', err);
                this.toaster.error('Ödevler yüklenirken bir hata oluştu');
                this.loading = false;
            }
        });
    }

    calculateStats() {
        this.stats.pending = this.assignments.filter(a => !a.isCompleted).length;
        this.stats.completed = this.assignments.filter(a => a.isCompleted).length;

        // Mock score calc - in real app would come from assignment result if available
        // Assuming backend might add score to DTO later
        this.stats.averageScore = 0;
    }

    filterAssignments() {
        let temp = [...this.assignments];
        const query = (this.searchControl.value || '').toLowerCase();

        if (query) {
            temp = temp.filter(a =>
                a.title.toLowerCase().includes(query) ||
                a.exerciseTitle.toLowerCase().includes(query)
            );
        }

        this.filteredAssignments = temp;
    }

    get activeAssignments() {
        return this.filteredAssignments.filter(a => !a.isCompleted)
            .sort((a, b) => new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime());
    }

    get completedAssignments() {
        return this.filteredAssignments.filter(a => a.isCompleted)
            .sort((a, b) => new Date(b.completionDate || 0).getTime() - new Date(a.completionDate || 0).getTime());
    }

    onTabChange(event: any) {
        // Optional: sync tab change with URL query params
    }

    startAssignment(assignment: AssignmentDto, isReplay = false) {
        this.router.navigate(['/student/exercises/universal-player', assignment.exerciseId], {
            queryParams: {
                assignmentId: assignment.studentAssignmentId,
                practiceMode: isReplay
            }
        });
    }

    getDueDateClass(item: AssignmentDto): string {
        const due = new Date(item.dueDate);
        const now = new Date();
        const diffHours = (due.getTime() - now.getTime()) / (1000 * 60 * 60);

        if (diffHours < 0) return 'urgent'; // Overdue
        if (diffHours < 48) return 'soon'; // Within 2 days
        return 'normal';
    }

    formatDueDate(dateStr: string): string {
        const date = new Date(dateStr);
        const now = new Date();

        if (date < now) return 'Süresi Doldu';

        // If this year, don't show year
        const options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'long' };
        if (date.getFullYear() !== now.getFullYear()) options.year = 'numeric';

        return date.toLocaleDateString('tr-TR', options);
    }

    formatCompletionDate(dateStr?: string): string {
        if (!dateStr) return '-';

        return new Date(dateStr).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
    }
}
