import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { LearningPathService } from '../../../../core/services/learning-path.service';
import { LearningPathNextItemDto, PersonalizedPathAvailabilityDto } from '../../../../core/models/learning-path.model';

@Component({
    selector: 'app-assignments-widget',
    standalone: true,
    imports: [CommonModule],
    templateUrl: './assignments-widget.component.html',
    styleUrls: ['./assignments-widget.component.scss']
})
export class AssignmentsWidgetComponent implements OnInit {
    private learningPathService = inject(LearningPathService);
    private router = inject(Router);
    private cdr = inject(ChangeDetectorRef);

    loading = true;
    error = false;
    availability: PersonalizedPathAvailabilityDto | null = null;
    nextLearningPathItem: LearningPathNextItemDto | null = null;

    ngOnInit(): void {
        this.learningPathService.getPersonalizedPathAvailability().subscribe({
            next: availability => {
                this.availability = availability;
                if (!availability.isAvailable) {
                    this.loading = false;
                    this.cdr.detectChanges();
                    return;
                }
                this.learningPathService.getPersonalizedLearningPathProgress().subscribe({
                    next: progress => {
                        this.nextLearningPathItem = progress.nextItem;
                        this.loading = false;
                        this.cdr.detectChanges();
                    },
                    error: () => {
                        this.error = true;
                        this.loading = false;
                        this.cdr.detectChanges();
                    }
                });
            },
            error: () => {
                this.error = true;
                this.loading = false;
                this.cdr.detectChanges();
            }
        });
    }

    viewLearningPath(): void {
        this.router.navigate(['/student/learning-path']);
    }
}
