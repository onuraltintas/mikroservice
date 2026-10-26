import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PracticeExamInput, PracticeExamProfile, PracticeSection, practiceTotals } from '../../../core/models/practice-exam';

@Component({ selector: 'app-practice-exam-editor', standalone: true, imports: [CommonModule, FormsModule],
  styles: [`
    :host { display:block; margin:1rem 0; }
    label { display:flex; flex-direction:column; gap:.4rem; }
    input,select { box-sizing:border-box; min-height:2.5rem; width:100%; padding:.5rem; font:inherit; color:inherit; background:transparent; border:1px solid #b9c7cb; border-radius:.4rem; }
    .table-wrap { overflow-x:auto; margin-top:1rem; } table { width:100%; border-collapse:collapse; } th,td { text-align:left; padding:.5rem; border-bottom:1px solid #d6dfe2; } td input { min-width:4rem; } th:first-child { min-width:9rem; }
    .summary { padding:.8rem; border:1px solid #b9c7cb; border-radius:.5rem; } .error { color:#b42318; }
  `], template: `
    <label>Deneme kapsamı<select [ngModel]="value?.examCode || ''" (ngModelChange)="choose($event)" [disabled]="disabled || locked">
      <option value="">Deneme kapsamını seçin / diğer sınav</option>
      @for (profile of availableProfiles(); track profile.code) { <option [value]="profile.code">{{profile.name}}</option> }
    </select></label>
    @if (!profiles.length) { <p role="status">Deneme kapsamları henüz hazır değil. Liste gelmezse sayfayı yenileyin.</p> }
    @if (value; as input) {
      <p>Her dersin doğru, yanlış ve boş toplamı soru sayısına eşit olmalı. Net = doğru − yanlış / {{profile()?.wrongAnswerDivisor}}. Puan ve sıralama hesaplanmaz.</p>
      <div class="table-wrap"><table><thead><tr><th>Ders / test</th><th>Soru</th><th>Doğru</th><th>Yanlış</th><th>Boş</th><th>Net</th></tr></thead>
        <tbody>@for (section of profile()?.sections || []; track section.code) {
          @if (row(section.code); as answers) { <tr><th scope="row">{{section.name}}</th><td>{{section.questionCount}}</td>
            <td><input type="number" [attr.aria-label]="section.name + ' doğru'" [ngModel]="answers.correct" (ngModelChange)="edit(section.code, 'correct', $event)" [disabled]="disabled" min="0" [max]="section.questionCount" step="1"></td>
            <td><input type="number" [attr.aria-label]="section.name + ' yanlış'" [ngModel]="answers.wrong" (ngModelChange)="edit(section.code, 'wrong', $event)" [disabled]="disabled" min="0" [max]="section.questionCount" step="1"></td>
            <td><input type="number" [attr.aria-label]="section.name + ' boş'" [ngModel]="answers.empty" (ngModelChange)="edit(section.code, 'empty', $event)" [disabled]="disabled" min="0" [max]="section.questionCount" step="1"></td>
            <td>{{rowValid(section) ? ((answers.correct - answers.wrong / (profile()?.wrongAnswerDivisor || 1)) | number:'1.2-2') : '—'}}</td>
          </tr> }
        }</tbody></table></div>
      @if (totals(); as total) { <p class="summary" role="status">Toplam: {{total.correct}} doğru · {{total.wrong}} yanlış · {{total.empty}} boş · <strong>{{total.net | number:'1.2-2'}} net</strong></p> }
      @else { <p class="error" role="alert">Derslerdeki soru sayılarını kontrol edin. Eksik veya fazla cevap varken kaydedilemez.</p> }
    }
  ` })
export class PracticeExamEditorComponent {
  @Input() profiles: PracticeExamProfile[] = [];
  @Input() value: PracticeExamInput | null = null;
  @Input() examType = '';
  @Input() disabled = false;
  @Input() locked = false;
  @Output() valueChange = new EventEmitter<PracticeExamInput | null>();
  availableProfiles() { return this.profiles.filter(x => !this.examType || (this.examType === 'LGS' ? x.code === 'LGS' : this.examType === 'YKS' && x.code !== 'LGS')); }
  profile() { return this.profiles.find(x => x.code === this.value?.examCode); }
  row(code: string) { return this.value?.sections.find(x => x.sectionCode === code); }
  totals() { return practiceTotals(this.value, this.profiles); }
  rowValid(section: PracticeSection) {
    const row = this.row(section.code);
    return row && [row.correct, row.wrong, row.empty].every(x => Number.isInteger(x) && x >= 0)
      && row.correct + row.wrong + row.empty === section.questionCount;
  }
  choose(code: string) {
    if (this.disabled || this.locked) return;
    const profile = this.availableProfiles().find(x => x.code === code);
    this.valueChange.emit(profile ? { examCode: code, sections: profile.sections.map(s => ({ sectionCode: s.code, correct: 0, wrong: 0, empty: s.questionCount })) } : null);
  }
  edit(code: string, field: 'correct' | 'wrong' | 'empty', count: number) {
    if (this.disabled || !this.value) return;
    this.valueChange.emit({ ...this.value, sections: this.value.sections.map(x => x.sectionCode === code ? { ...x, [field]: count } : { ...x }) });
  }
}
