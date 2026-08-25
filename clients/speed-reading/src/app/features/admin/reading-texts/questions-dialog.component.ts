import { Component, Inject, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatRadioModule } from '@angular/material/radio';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ToasterService } from '../../../core/services/toaster.service';
import { ReadingTextsService } from '../../../core/services/reading-texts.service';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil } from 'rxjs/operators';
import {
  ReadingText,
  ReadingQuestion,
  QuestionType,
  QUESTION_TYPE_LABELS
} from '../../../core/models/reading-text.model';

@Component({
  selector: 'app-questions-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatRadioModule,
    MatDividerModule,
    MatChipsModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './questions-dialog.component.html',
  styleUrls: ['./questions-dialog.component.scss']
})
export class QuestionsDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ReadingTextsService);
  // toaster inherited from BaseComponent

  questionForm: FormGroup;
  questions: ReadingQuestion[] = [];
  editingQuestion: ReadingQuestion | null = null;
  displayedColumns = ['index', 'question', 'type', 'correct', 'actions'];
  saving = false;
  // loading inherited from BaseComponent
  QuestionType = QuestionType;

  constructor(
    public dialogRef: MatDialogRef<QuestionsDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public text: ReadingText
  ) {
    super();
    this.questionForm = this.createForm();
  }

  ngOnInit() {
    // Questions are already loaded and passed via data
    this.questions = (this.text.readingQuestions || []).sort((a: ReadingQuestion, b: ReadingQuestion) => a.orderIndex - b.orderIndex);
    this.loading.set(false); // Set loading to false after questions are loaded
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
  }

  createForm(): FormGroup {
    return this.fb.group({
      questionText: ['', Validators.required],
      type: [QuestionType.Literal, Validators.required],
      optionA: ['', Validators.required],
      optionB: ['', Validators.required],
      optionC: ['', Validators.required],
      optionD: ['', Validators.required],
      correctAnswer: ['', Validators.required]
    });
  }

  loadQuestions() {
    this.loading.set(true);
    this.service.getTextWithQuestions(this.text.id).subscribe({
      next: (textWithQuestions) => {
        this.questions = (textWithQuestions.readingQuestions || []).sort((a: ReadingQuestion, b: ReadingQuestion) => a.orderIndex - b.orderIndex);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading questions:', error);
        this.toaster.error('Sorular yüklenirken hata oluştu', 3000);
        this.loading.set(false);
      }
    });
  }

  saveQuestion() {
    if (this.questionForm.invalid) {
      this.toaster.warning('Lütfen tüm alanları doldurun', 3000);
      return;
    }

    if (!this.editingQuestion && this.questions.length >= 10) {
      this.toaster.warning('En fazla 10 soru eklenebilir', 3000);
      return;
    }

    this.saving = true;
    const formValue = this.questionForm.value;

    const dto = {
      readingTextId: this.text.id,
      questionText: formValue.questionText,
      type: formValue.type,
      bloomLevel: this.editingQuestion?.bloomLevel,
      difficultyLevel: this.editingQuestion?.difficultyLevel,
      explanation: this.editingQuestion?.explanation,
      optionA: formValue.optionA,
      optionB: formValue.optionB,
      optionC: formValue.optionC,
      optionD: formValue.optionD,
      correctAnswer: formValue.correctAnswer,
      orderIndex: this.editingQuestion?.orderIndex ?? this.questions.length
    };

    const operation = this.editingQuestion
      ? this.service.updateQuestion(this.editingQuestion.id, dto)
      : this.service.createQuestion(dto);

    operation.subscribe({
      next: () => {
        this.toaster.success(
          this.editingQuestion ? 'Soru güncellendi' : 'Soru eklendi',
          3000
        );
        this.questionForm.reset({ type: QuestionType.Literal });
        this.editingQuestion = null;
        this.loadQuestions();
        this.saving = false;
      },
      error: (error) => {
        console.error('Error saving question:', error);
        this.toaster.error('Soru kaydedilirken hata oluştu', 3000);
        this.saving = false;
      }
    });
  }

  editQuestion(question: ReadingQuestion) {
    this.editingQuestion = question;
    this.questionForm.patchValue({
      questionText: question.questionText,
      type: question.type,
      optionA: question.optionA,
      optionB: question.optionB,
      optionC: question.optionC,
      optionD: question.optionD,
      correctAnswer: question.correctAnswer
    });
    // Scroll to form
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  cancelEdit() {
    this.editingQuestion = null;
    this.questionForm.reset({ type: QuestionType.Literal });
  }

  async deleteQuestion(question: ReadingQuestion) {
    const confirmed = await this.toaster.confirm(`"${question.questionText}" sorusunu silmek istediğinizden emin misiniz?`);
    if (confirmed) {
      this.saving = true;
      this.service.deleteQuestion(question.id).subscribe({
        next: () => {
          this.toaster.success('Soru silindi', 3000);
          this.loadQuestions();
          this.saving = false;
        },
        error: (error) => {
          console.error('Error deleting question:', error);
          this.toaster.error('Soru silinirken hata oluştu', 3000);
          this.saving = false;
        }
      });
    }
  }

  getTypeLabel(type: QuestionType): string {
    return QUESTION_TYPE_LABELS[type];
  }

  onClose() {
    this.dialogRef.close(true);
  }
}
