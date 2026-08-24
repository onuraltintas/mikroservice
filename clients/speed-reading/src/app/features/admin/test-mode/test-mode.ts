import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatOptionModule } from '@angular/material/core';
import { environment } from '../../../../environments/environment';
import { ProgramTemplateService, ExerciseProgramTemplate } from '../../../core/services/program-template.service';

@Component({
  selector: 'app-test-mode',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatSelectModule,
    MatFormFieldModule,
    MatOptionModule
  ],
  templateUrl: './test-mode.html',
  styleUrls: ['./test-mode.scss']
})
export class TestModeComponent implements OnInit {
  templates: ExerciseProgramTemplate[] = [];
  selectedTemplateId: string = '';
  isLoading = false;
  programStarted = false;

  constructor(
    private programTemplateService: ProgramTemplateService,
    private http: HttpClient,
    private router: Router,
    private snackBar: MatSnackBar
  ) { }

  ngOnInit() {
    this.loadTemplates();
  }

  loadTemplates() {
    this.programTemplateService.getAll()
      .subscribe({
        next: (data) => {
          this.templates = data.filter(t => t.isActive && !t.isDeleted);
        },
        error: (error) => {
          console.error('Error loading templates:', error);
          this.showError('Program listesi yüklenemedi.');
        }
      });
  }

  startTestProgram() {
    if (!this.selectedTemplateId) return;

    this.isLoading = true;

    // Note: This endpoint might also need to be verified/created in backend
    this.http.post(`${environment.apiUrl}/admin/test-mode/start-program`, {
      templateId: this.selectedTemplateId
    }).subscribe({
      next: (response: any) => {
        this.isLoading = false;
        this.programStarted = true;
        this.showSuccess('Test programı hesabınıza tanımlandı!');
      },
      error: (error) => {
        this.isLoading = false;
        console.error('Error starting program:', error);
        this.showError('Program başlatılırken bir hata oluştu.');
      }
    });
  }

  goToStudentDashboard() {
    this.router.navigate(['/student/dashboard']);
  }

  private showSuccess(message: string) {
    this.snackBar.open(message, 'Tamam', {
      duration: 3000,
      panelClass: ['success-snackbar']
    });
  }

  private showError(message: string) {
    this.snackBar.open(message, 'Kapat', {
      duration: 5000,
      panelClass: ['error-snackbar']
    });
  }
}
