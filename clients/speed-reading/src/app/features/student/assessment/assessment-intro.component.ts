import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AssessmentService } from '../../../services/assessment.service';

@Component({
  selector: 'app-assessment-intro',
  standalone: true,
  imports: [
    CommonModule
  ],
  templateUrl: './assessment-intro.component.html',
  styleUrl: './assessment-intro.component.scss'
})
export class AssessmentIntroComponent {
  private router = inject(Router);
  private assessmentService = inject(AssessmentService);
  
  loading = false;

  startAssessment(): void {
    this.router.navigate(['/student/assessment']);
  }

  skipAssessment(): void {
    this.loading = true;
    
    this.assessmentService.skipAssessment().subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/student/dashboard']);
      },
      error: (err) => {
        console.error('❌ Error skipping assessment:', err);
        this.loading = false;
        
        // Navigate to dashboard anyway
        this.router.navigate(['/student/dashboard']);
      }
    });
  }
}
