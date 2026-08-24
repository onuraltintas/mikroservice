import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { QuestionBankService } from '../../../../core/services/question-bank.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ExamType, QuestionCategory } from '../../../../core/models/exam-question.model';

import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-question-form',
  templateUrl: './question-form.html',
  styleUrls: ['./question-form.scss'],
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule
  ]
})
export class QuestionFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  questionId: string | null = null;
  isLoading = false;

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

  topics = [
    'Paragrafta Ana Fikir',
    'Paragrafta Çıkarım',
    'Paragrafta Detay',
    'Sözcük Anlamı',
    'Anlam Bütünlüğü',
    'Paragrafta Başlık',
    'Yazarın Amacı',
    'Anlatım Teknikleri',
    'Paragrafta Yapı',
    'Paragrafta Konu',
    'Paragrafta Anlam'
  ];

  constructor(
    private fb: FormBuilder,
    private questionService: QuestionBankService,
    private route: ActivatedRoute,
    private router: Router,
    private toaster: ToasterService
  ) {
    this.form = this.fb.group({
      content: ['', [Validators.required, Validators.minLength(50)]],
      question: ['', Validators.required],
      optionA: ['', Validators.required],
      optionB: ['', Validators.required],
      optionC: ['', Validators.required],
      optionD: ['', Validators.required],
      optionE: [''], // Optional depending on exam type
      correctOption: ['', Validators.required],
      examType: [ExamType.General, Validators.required],
      difficulty: [3, [Validators.required, Validators.min(1), Validators.max(5)]],
      wordCount: [0, [Validators.required, Validators.min(1)]],
      topic: [''],
      category: [QuestionCategory.None]
    });

    // Auto-calculate word count
    this.form.get('content')?.valueChanges.subscribe(val => {
      if (val) {
        const count = val.trim().split(/\s+/).length;
        this.form.patchValue({ wordCount: count }, { emitEvent: false });
      } else {
        this.form.patchValue({ wordCount: 0 }, { emitEvent: false });
      }
    });
  }

  ngOnInit(): void {
    this.questionId = this.route.snapshot.paramMap.get('id');
    if (this.questionId) {
      this.isEditMode = true;
      this.loadQuestion(this.questionId);
    }
  }

  loadQuestion(id: string) {
    this.isLoading = true;
    this.questionService.getQuestion(id).subscribe({
      next: (question) => {
        this.form.patchValue(question);
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading question', error);
        this.toaster.error('Soru yüklenirken hata oluştu');
        this.isLoading = false;
        this.router.navigate(['/admin/question-bank']);
      }
    });
  }

  setCorrectOption(option: string) {
    this.form.patchValue({ correctOption: option });
  }

  onSubmit() {
    if (this.form.invalid) {
      return;
    }

    this.isLoading = true;
    const questionData = this.form.value;

    if (this.isEditMode && this.questionId) {
      // Include the ID in the request body as the backend expects it
      const updateData = {
        ...questionData,
        id: this.questionId
      };

      this.questionService.updateQuestion(this.questionId, updateData).subscribe({
        next: () => {
          this.toaster.success('Soru başarıyla güncellendi');
          this.router.navigate(['/admin/question-bank']);
        },
        error: (error) => {
          console.error('Error updating question', error);
          this.toaster.error('Güncelleme sırasında hata oluştu');
          this.isLoading = false;
        }
      });
    } else {
      this.questionService.createQuestion(questionData).subscribe({
        next: () => {
          this.toaster.success('Soru başarıyla oluşturuldu');
          this.router.navigate(['/admin/question-bank']);
        },
        error: (error) => {
          console.error('Error creating question', error);
          this.toaster.error('Oluşturma sırasında hata oluştu');
          this.isLoading = false;
        }
      });
    }
  }

  cancel() {
    this.router.navigate(['/admin/question-bank']);
  }
}
