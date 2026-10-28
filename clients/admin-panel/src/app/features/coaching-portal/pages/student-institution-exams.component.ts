import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { CoachingInstitutionExamRanksService, StudentInstitutionExamRank } from '../../../core/services/coaching-institution-exam-ranks.service';
import { CoachingDatePipe } from '../coaching-labels';

/** Roadmap O4: where the student stands in their institution's practice exams. Hidden when there are none. */
@Component({
  selector: 'app-student-institution-exams',
  standalone: true,
  imports: [CommonModule, CoachingDatePipe],
  template: `
    @if (ranks().length) {
      <section class="panel" aria-labelledby="institution-exams-heading">
        <h2 id="institution-exams-heading">Kurum denemelerim</h2>
        @for (item of ranks(); track item.examId) {
          <p class="plan-row"><strong>{{ item.title }}</strong> · {{ item.examName }} · {{ item.examDate | coachingDate }}<br>
            {{ net(item.net) }} net · Kurumda {{ item.rank }}. / {{ item.participants }} öğrenci · İlk {{ percent(item.topPercent) }}</p>
        }
      </section>
    }`
})
export class StudentInstitutionExamsComponent implements OnInit {
  private readonly api = inject(CoachingInstitutionExamRanksService);
  readonly ranks = signal<StudentInstitutionExamRank[]>([]);
  ngOnInit(): void { this.api.mine().subscribe({ next: value => this.ranks.set(value), error: () => this.ranks.set([]) }); }
  net(value: number): string { return value.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
  percent(value: number): string { return '%' + value.toLocaleString('tr-TR', { maximumFractionDigits: 1 }); }
}
