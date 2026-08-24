import { Component, OnInit, ViewChild, AfterViewInit } from '@angular/core';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';

import { QuestionBankService } from '../../../../core/services/question-bank.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ExamQuestion, ExamType, QuestionCategory } from '../../../../core/models/exam-question.model';

import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-question-bank-list',
  templateUrl: './question-bank-list.html',
  styleUrls: ['./question-bank-list.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatTooltipModule
  ]
})
export class QuestionBankListComponent implements OnInit, AfterViewInit {
  displayedColumns: string[] = ['content', 'examType', 'difficulty', 'category', 'wordCount', 'createdAt', 'actions'];
  dataSource = new MatTableDataSource<ExamQuestion>([]);
  totalCount = 0;
  isLoading = false;

  filterExamType?: ExamType;
  filterDifficulty?: number;
  filterCategory?: QuestionCategory;
  searchTerm = '';
  searchSubject = new Subject<string>();

  examTypes = [
    { value: ExamType.LGS, label: 'LGS' },
    { value: ExamType.YKS, label: 'YKS' },
    { value: ExamType.KPSS, label: 'KPSS' },
    { value: ExamType.ALES, label: 'ALES' },
    { value: ExamType.DGS, label: 'DGS' },
    { value: ExamType.General, label: 'Genel' }
  ];

  categories = [
    { value: QuestionCategory.None, label: 'Seçilmedi' },
    { value: QuestionCategory.MainIdea, label: 'Ana Fikir' },
    { value: QuestionCategory.Inference, label: 'Çıkarım' },
    { value: QuestionCategory.VocabularyInContext, label: 'Sözcük Anlamı' },
    { value: QuestionCategory.Detail, label: 'Detay' },
    { value: QuestionCategory.Coherence, label: 'Anlam Bütünlüğü' },
    { value: QuestionCategory.Title, label: 'Başlık' },
    { value: QuestionCategory.AuthorPurpose, label: 'Yazarın Amacı' },
    { value: QuestionCategory.NarrativeTechniques, label: 'Anlatım Teknikleri' }
  ];

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    private questionService: QuestionBankService,
    private router: Router,
    private toaster: ToasterService
  ) {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(term => {
      this.searchTerm = term;
      this.paginator.pageIndex = 0;
      this.loadQuestions();
    });
  }

  ngOnInit(): void {
    this.loadQuestions();
  }

  ngAfterViewInit() {
    this.sort.sortChange.subscribe(() => {
      this.paginator.pageIndex = 0;
      // In a real app, we would pass sort direction to API
      this.loadQuestions();
    });
  }

  loadQuestions() {
    this.isLoading = true;
    const pageIndex = this.paginator ? this.paginator.pageIndex + 1 : 1;
    const pageSize = this.paginator ? this.paginator.pageSize : 10;

    this.questionService.getQuestions(
      pageIndex,
      pageSize,
      this.filterExamType,
      this.filterDifficulty,
      this.filterCategory,
      this.searchTerm
    ).subscribe({
      next: (result) => {
        this.dataSource.data = result.items;
        this.totalCount = result.totalCount;
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading questions', error);
        this.toaster.error('Sorular yüklenirken hata oluştu');
        this.isLoading = false;
      }
    });
  }

  onPageChange(event: PageEvent) {
    this.loadQuestions();
  }

  onSearch(event: Event) {
    const input = event.target as HTMLInputElement;
    this.searchSubject.next(input.value);
  }

  onFilterChange() {
    this.paginator.pageIndex = 0;
    this.loadQuestions();
  }

  addQuestion() {
    this.router.navigate(['/admin/question-bank/new']);
  }

  editQuestion(id: string) {
    this.router.navigate(['/admin/question-bank/edit', id]);
  }

  async deleteQuestion(question: ExamQuestion) {
    const confirmed = await this.toaster.confirm(
      `"${question.content.substring(0, 30)}..." sorusunu silmek istediğinize emin misiniz?`,
      'Soru Silinecek',
      'Sil',
      'İptal'
    );

    if (confirmed) {
      this.questionService.deleteQuestion(question.id).subscribe({
        next: () => {
          this.toaster.success('Soru başarıyla silindi');
          this.loadQuestions();
        },
        error: (error) => {
          console.error('Error deleting question', error);
          this.toaster.error('Soru silinirken hata oluştu');
        }
      });
    }
  }

  async hardDeleteQuestion(question: ExamQuestion) {
    const confirmed = await this.toaster.confirm(
      `"${question.content.substring(0, 30)}..." sorusunu KALICI OLARAK silmek istediğinize emin misiniz? Bu işlem geri alınamaz!`,
      'Kalıcı Silme Onayı',
      'Kalıcı Olarak Sil',
      'İptal'
    );

    if (confirmed) {
      this.questionService.hardDeleteQuestion(question.id).subscribe({
        next: () => {
          this.toaster.success('Soru kalıcı olarak silindi');
          this.loadQuestions();
        },
        error: (error) => {
          console.error('Error hard deleting question', error);
          this.toaster.error('Kalıcı silme sırasında hata oluştu');
        }
      });
    }
  }

  getExamTypeLabel(type: ExamType): string {
    return this.examTypes.find(t => t.value === type)?.label || 'Bilinmiyor';
  }

  getCategoryLabel(category: QuestionCategory): string {
    return this.categories.find(c => c.value === category)?.label || 'Seçilmedi';
  }

  goToDashboard() {
    this.router.navigate(['/admin/dashboard']);
  }
}
