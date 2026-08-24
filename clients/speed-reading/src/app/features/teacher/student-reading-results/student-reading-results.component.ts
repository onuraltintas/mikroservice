import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { ReadingSession } from '../../../core/models/reading-text.model';
import { ToasterService } from '../../../core/services/toaster.service';
import { ReadingTextsService } from '../../../core/services/reading-texts.service';

interface Student {
  id: string;
  fullName: string;
}

interface AggregateStats {
  averageWPM: number;
  averageComprehension: number;
  totalSessions: number;
  averageEfficiency: number;
}

@Component({
  selector: 'app-student-reading-results',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatChipsModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './student-reading-results.component.html',
  styleUrls: ['./student-reading-results.component.scss']
})
export class StudentReadingResultsComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private readingTextsService = inject(ReadingTextsService);
  private API_URL = `${environment.apiUrl}/TeacherReading`;

  sessions: ReadingSession[] = [];
  students: Student[] = [];
  categories: string[] = [];
  displayedColumns = ['student', 'date', 'text', 'wpm', 'comprehension', 'efficiency', 'performance'];
  loading = false;
  Math = Math;

  aggregateStats: AggregateStats | null = null;

  filters = {
    studentId: '',
    textSearch: '',
    category: '',
    startDate: null as Date | null,
    endDate: null as Date | null
  };

  ngOnInit() {
    this.loadCategories();
    this.loadStudents();
    this.loadResults();
  }

  loadCategories() {
    this.readingTextsService.getCategories().subscribe({
      next: (categories) => {
        this.categories = categories;
      },
      error: (error) => {
        console.error('Error loading categories:', error);
      }
    });
  }

  loadStudents() {
    // Load teacher's students
    this.http.get<Student[]>(`${environment.apiUrl}/teachers/my-students`).subscribe({
      next: (students) => {
        this.students = students;
      },
      error: (error) => {
        console.error('Error loading students:', error);
      }
    });
  }

  loadResults() {
    this.loading = true;

    let url = `${this.API_URL}/student-results?`;
    const params: string[] = [];

    if (this.filters.studentId) params.push(`studentId=${this.filters.studentId}`);
    if (this.filters.textSearch) params.push(`textSearch=${this.filters.textSearch}`);
    if (this.filters.category) params.push(`category=${this.filters.category}`);
    if (this.filters.startDate) params.push(`startDate=${this.filters.startDate.toISOString()}`);
    if (this.filters.endDate) params.push(`endDate=${this.filters.endDate.toISOString()}`);

    url += params.join('&');

    this.http.get<ReadingSession[]>(url).subscribe({
      next: (sessions) => {
        this.sessions = sessions.sort((a, b) =>
          new Date(b.completedAt).getTime() - new Date(a.completedAt).getTime()
        );
        this.calculateAggregateStats();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading results:', error);
        this.toaster.error('Sonuçlar yüklenirken hata oluştu', 3000);
        this.loading = false;
      }
    });
  }

  calculateAggregateStats() {
    if (this.sessions.length === 0) {
      this.aggregateStats = null;
      return;
    }

    this.aggregateStats = {
      totalSessions: this.sessions.length,
      averageWPM: this.sessions.reduce((sum, s) => sum + s.calculatedWPM, 0) / this.sessions.length,
      averageComprehension: this.sessions.reduce((sum, s) => sum + s.comprehensionRate, 0) / this.sessions.length,
      averageEfficiency: this.sessions.reduce((sum, s) => sum + s.efficiencyScore, 0) / this.sessions.length
    };
  }

  onFilterChange() {
    this.loadResults();
  }

  clearFilters() {
    this.filters = {
      studentId: '',
      textSearch: '',
      category: '',
      startDate: null,
      endDate: null
    };
    this.loadResults();
  }

  hasActiveFilters(): boolean {
    return !!(
      this.filters.studentId ||
      this.filters.textSearch ||
      this.filters.category ||
      this.filters.startDate ||
      this.filters.endDate
    );
  }

  getStudentName(userId: string): string {
    const student = this.students.find(s => s.id === userId);
    return student?.fullName || 'Bilinmiyor';
  }

  formatDate(date: Date): string {
    const d = new Date(date);
    return d.toLocaleDateString('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    });
  }

  getComprehensionClass(rate: number): string {
    if (rate >= 80) return 'high';
    if (rate >= 60) return 'medium';
    return 'low';
  }
}
