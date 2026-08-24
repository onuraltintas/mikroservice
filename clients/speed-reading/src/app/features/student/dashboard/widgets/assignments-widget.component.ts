import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AssignmentService, AssignmentDto } from '../../../../core/services/assignment.service';

@Component({
    selector: 'app-assignments-widget',
    standalone: true,
    imports: [
        CommonModule
    ],
    templateUrl: './assignments-widget.component.html',
    styleUrls: ['./assignments-widget.component.scss']
})
export class AssignmentsWidgetComponent implements OnInit {
    private assignmentService = inject(AssignmentService);
    private router = inject(Router);

    assignments: AssignmentDto[] = [];
    loading = true;

    ngOnInit() {
        this.loadAssignments();
    }

    loadAssignments() {
        this.assignmentService.getMyAssignments().subscribe({
            next: (res) => {
                // Show all assigned (both complete and incomplete) but sort incomplete first
                // Or user requested specifically: if any incomplete -> red border
                this.assignments = res.sort((a, b) => {
                    if (a.isCompleted === b.isCompleted) {
                        return new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime();
                    }
                    return a.isCompleted ? 1 : -1;
                });
                this.loading = false;
            },
            error: (err) => {
                console.error('Error loading assignments', err);
                this.loading = false;
            }
        });
    }

    get pendingCount() {
        return this.assignments.filter(a => !a.isCompleted).length;
    }

    // --- Status Logic for UI ---

    getCardStatusClass(): string {
        if (this.loading) return '';
        if (this.assignments.length === 0) return ''; // Neutral

        // If ANY incomplete assignment exists => Red (Pending)
        if (this.pendingCount > 0) return 'status-pending';

        // If ALL assignments are completed => Green (Completed)
        return 'status-completed';
    }

    getStatusIcon(): string {
        if (this.pendingCount > 0) return 'assignment_late';
        if (this.assignments.length > 0 && this.pendingCount === 0) return 'assignment_turned_in';
        return 'assignment';
    }

    getAssignmentStatusClass(item: AssignmentDto): string {
        if (item.isCompleted) return 'status-done';
        if (this.isOverdue(item.dueDate)) return 'status-overdue';
        if (this.isSoon(item.dueDate)) return 'status-soon';
        return 'status-normal';
    }

    getDueDateClass(item: AssignmentDto): string {
        if (item.isCompleted) return '';
        if (this.isOverdue(item.dueDate)) return 'text-error';
        if (this.isSoon(item.dueDate)) return 'text-warning';
        return '';
    }

    startAssignment(assignment: AssignmentDto) {
        this.router.navigate(['/student/exercises/universal-player', assignment.exerciseId], {
            queryParams: { assignmentId: assignment.studentAssignmentId }
        });
    }

    viewAll() {
        this.router.navigate(['/student/assignments']);
    }

    // --- Helpers ---

    isOverdue(dateStr: string): boolean {
        return new Date(dateStr) < new Date();
    }

    isSoon(dateStr: string): boolean {
        const diff = new Date(dateStr).getTime() - new Date().getTime();
        return diff > 0 && diff < 2 * 24 * 60 * 60 * 1000; // 2 days
    }

    formatDueDate(dateStr: string): string {
        const date = new Date(dateStr);
        // If overdue, show "Gecikti" or relative
        const now = new Date();
        if (date < now) return 'Süresi Doldu';

        return date.toLocaleDateString('tr-TR', { day: 'numeric', month: 'short' });
    }
}
